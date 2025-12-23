using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Exceptions;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation with retry logic and enhanced error handling.
/// Supports Azure DevOps Server 2022.2 (API 7.0) and TFS 2019+ (API 5.1+).
/// This is the production implementation that connects to actual Azure DevOps/TFS servers.
/// </summary>
public class RealTfsClient : ITfsClient
{
    private readonly HttpClient _httpClient;
    private readonly TfsConfigurationService _configService;
    private readonly TfsAuthenticationProvider _authProvider;
    private readonly PatAccessor _patAccessor;
    private readonly ILogger<RealTfsClient> _logger;
    private const int MaxRetries = 3;

    public RealTfsClient(
        HttpClient httpClient, 
        TfsConfigurationService configService, 
        TfsAuthenticationProvider authProvider,
        PatAccessor patAccessor,
        ILogger<RealTfsClient> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _authProvider = authProvider;
        _patAccessor = patAccessor;
        _logger = logger;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("No TFS configuration found for validation");
            return false;
        }

        try
        {
            await ConfigureAuthenticationAsync(entity, cancellationToken);
            
            var url = $"{entity.Url.TrimEnd('/')}/_apis/projects?api-version={entity.ApiVersion}";
            var resp = await _httpClient.GetAsync(url, cancellationToken);
            
            _logger.LogInformation("Validation GET {Url} returned {StatusCode}", url, resp.StatusCode);
            
            if (resp.IsSuccessStatusCode)
            {
                // Update last validated timestamp
                entity.LastValidated = DateTimeOffset.UtcNow;
                await _configService.SaveConfigEntityAsync(entity, cancellationToken);
                return true;
            }
            
            return false;
        }
        catch (TfsAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed during TFS connection validation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TFS connection");
            return false;
        }
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
    }

    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        // Apply timeout from configuration (Phase 4)
        _httpClient.Timeout = TimeSpan.FromSeconds(entity.TimeoutSeconds);

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build WIQL query with optional date filter for incremental sync (Phase 3)
            var dateFilter = since.HasValue 
                ? $" AND [System.ChangedDate] >= '{since.Value:yyyy-MM-ddTHH:mm:ssZ}'" 
                : "";

            var wiql = new
            {
                query = $"Select [System.Id], [System.WorkItemType], [System.Title], [System.State] From WorkItems Where [System.AreaPath] = '{EscapeWiql(areaPath)}'{dateFilter}"
            };

            var wiqlUrl = $"{entity.Url.TrimEnd('/')}/_apis/wit/wiql?api-version={entity.ApiVersion}";
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            // Phase 4: Enhanced logging
            _logger.LogDebug("Executing WIQL query: {Query}", wiql.query);
            
            var wiqlResponse = await _httpClient.PostAsync(wiqlUrl, content, cancellationToken);
            await HandleHttpErrorsAsync(wiqlResponse, cancellationToken);

            using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var ids = doc.RootElement.GetProperty("workItems").EnumerateArray()
                        .Select(e => e.GetProperty("id").GetInt32())
                        .ToArray();

            if (ids.Length == 0)
            {
                _logger.LogInformation("No work items found for areaPath={AreaPath}, since={Since}", areaPath, since);
                return Enumerable.Empty<WorkItemDto>();
            }

            _logger.LogDebug("Found {Count} work item IDs, fetching details", ids.Length);

            // Batch get work items - use $expand=All to get all fields including System.Parent
            var idsQuery = string.Join(',', ids);
            var itemsUrl = $"{entity.Url.TrimEnd('/')}/_apis/wit/workitems?ids={idsQuery}&$expand=All&api-version={entity.ApiVersion}";
            var itemsResponse = await _httpClient.GetAsync(itemsUrl, cancellationToken);
            await HandleHttpErrorsAsync(itemsResponse, cancellationToken);

            using var itemsStream = await itemsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var itemsDoc = await JsonDocument.ParseAsync(itemsStream, cancellationToken: cancellationToken);

            var results = new List<WorkItemDto>();

            foreach (var item in itemsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var fields = item.GetProperty("fields");
                var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
                var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
                var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
                var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
                
                // Extract parent work item ID if present
                int? parentId = null;
                if (fields.TryGetProperty("System.Parent", out var parent) && parent.ValueKind == JsonValueKind.String)
                {
                    var parentUrl = parent.GetString();
                    if (!string.IsNullOrEmpty(parentUrl))
                    {
                        var segments = parentUrl.Split('/');
                        if (segments.Length > 0 && int.TryParse(segments[^1], out var parsedId))
                        {
                            parentId = parsedId;
                        }
                    }
                }

                // Phase 3: Extract effort field (common custom fields: Microsoft.VSTS.Scheduling.Effort, Microsoft.VSTS.Scheduling.StoryPoints)
                int? effort = null;
                if (fields.TryGetProperty("Microsoft.VSTS.Scheduling.Effort", out var effortField) && effortField.ValueKind == JsonValueKind.Number)
                {
                    effort = effortField.GetInt32();
                }
                else if (fields.TryGetProperty("Microsoft.VSTS.Scheduling.StoryPoints", out var storyPoints) && storyPoints.ValueKind == JsonValueKind.Number)
                {
                    effort = (int)storyPoints.GetDouble(); // Story points might be decimal
                }

                results.Add(new WorkItemDto(
                    TfsId: id,
                    Type: type,
                    Title: title,
                    ParentTfsId: parentId,
                    AreaPath: area,
                    IterationPath: iteration,
                    State: state,
                    JsonPayload: item.GetRawText(),
                    RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: effort
                ));
            }

            // Phase 4: Performance metrics
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation("Retrieved {Count} work items for areaPath={AreaPath}, since={Since} in {ElapsedMs}ms", 
                results.Count, areaPath, since, elapsed.TotalMilliseconds);
            
            return results;
        }, cancellationToken);
    }

    // Pull Request methods - Phase 2 implementation with Phase 4 enhancements
    public async Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        // Phase 4: Apply timeout from configuration
        _httpClient.Timeout = TimeSpan.FromSeconds(entity.TimeoutSeconds);

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Get all repositories or specific one
            var repositories = await GetRepositoriesAsync(entity, repositoryName, cancellationToken);
            var allPRs = new List<PullRequestDto>();

            _logger.LogDebug("Querying {RepoCount} repositories for pull requests", repositories.Count);

            foreach (var repo in repositories)
            {
                // Build query parameters
                var queryParams = new List<string>
                {
                    "searchCriteria.status=all" // Get all PRs (active, completed, abandoned)
                };

                if (fromDate.HasValue)
                {
                    // Azure DevOps API uses minTime for filtering
                    queryParams.Add($"searchCriteria.minTime={fromDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }

                if (toDate.HasValue)
                {
                    queryParams.Add($"searchCriteria.maxTime={toDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }

                var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/git/repositories/{repo.Name}/pullrequests?{string.Join("&", queryParams)}&api-version={entity.ApiVersion}";
                
                _logger.LogDebug("Fetching PRs from repository {Repository}", repo.Name);
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                    continue;

                foreach (var pr in valueArray.EnumerateArray())
                {
                    var prId = pr.GetProperty("pullRequestId").GetInt32();
                    var title = pr.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var status = pr.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    var sourceBranch = pr.TryGetProperty("sourceRefName", out var src) ? src.GetString() ?? "" : "";
                    var targetBranch = pr.TryGetProperty("targetRefName", out var tgt) ? tgt.GetString() ?? "" : "";
                    
                    var createdBy = "";
                    if (pr.TryGetProperty("createdBy", out var creator))
                    {
                        createdBy = creator.TryGetProperty("displayName", out var name) ? name.GetString() ?? "" : "";
                    }

                    var createdDate = pr.TryGetProperty("creationDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                    var completedDate = pr.TryGetProperty("closedDate", out var cld) && cld.ValueKind != JsonValueKind.Null 
                        ? (DateTimeOffset?)cld.GetDateTimeOffset() 
                        : null;

                    // Determine iteration path from work items or use default
                    var iterationPath = entity.Project; // Default to project name

                    allPRs.Add(new PullRequestDto(
                        Id: prId,
                        RepositoryName: repo.Name,
                        Title: title,
                        CreatedBy: createdBy,
                        CreatedDate: createdDate,
                        CompletedDate: completedDate,
                        Status: status,
                        IterationPath: iterationPath,
                        SourceBranch: sourceBranch,
                        TargetBranch: targetBranch,
                        RetrievedAt: DateTimeOffset.UtcNow
                    ));
                }
            }

            // Phase 4: Performance metrics
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation("Retrieved {Count} pull requests across {RepoCount} repositories in {ElapsedMs}ms", 
                allPRs.Count, repositories.Count, elapsed.TotalMilliseconds);
            
            return allPRs;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/iterations?api-version={entity.ApiVersion}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var iterations = new List<PullRequestIterationDto>();

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                return iterations;

            foreach (var iteration in valueArray.EnumerateArray())
            {
                var iterationId = iteration.GetProperty("id").GetInt32();
                var createdDate = iteration.TryGetProperty("createdDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                var updatedDate = iteration.TryGetProperty("updatedDate", out var ud) ? ud.GetDateTimeOffset() : createdDate;
                
                // Count commits and changes
                var commitCount = 0;
                if (iteration.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
                {
                    commitCount = commits.GetArrayLength();
                }

                var changeCount = 0;
                if (iteration.TryGetProperty("changeList", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    changeCount = changes.GetArrayLength();
                }

                iterations.Add(new PullRequestIterationDto(
                    PullRequestId: pullRequestId,
                    IterationNumber: iterationId,
                    CreatedDate: createdDate,
                    UpdatedDate: updatedDate,
                    CommitCount: commitCount,
                    ChangeCount: changeCount
                ));
            }

            _logger.LogInformation("Retrieved {Count} iterations for PR {PullRequestId}", iterations.Count, pullRequestId);
            return iterations;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/threads?api-version={entity.ApiVersion}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var comments = new List<PullRequestCommentDto>();

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                return comments;

            foreach (var thread in valueArray.EnumerateArray())
            {
                var threadId = thread.GetProperty("id").GetInt32();
                var threadStatus = thread.TryGetProperty("status", out var ts) ? ts.GetString() ?? "" : "";
                var isResolved = threadStatus.Equals("fixed", StringComparison.OrdinalIgnoreCase) || 
                                threadStatus.Equals("closed", StringComparison.OrdinalIgnoreCase);

                if (!thread.TryGetProperty("comments", out var threadComments) || threadComments.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var comment in threadComments.EnumerateArray())
                {
                    var commentId = comment.GetProperty("id").GetInt32();
                    var content = comment.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    var createdDate = comment.TryGetProperty("publishedDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                    var updatedDate = comment.TryGetProperty("lastUpdatedDate", out var ud) && ud.ValueKind != JsonValueKind.Null
                        ? (DateTimeOffset?)ud.GetDateTimeOffset()
                        : null;

                    var author = "";
                    if (comment.TryGetProperty("author", out var auth))
                    {
                        author = auth.TryGetProperty("displayName", out var name) ? name.GetString() ?? "" : "";
                    }

                    // Check if this comment resolved the thread
                    string? resolvedBy = null;
                    DateTimeOffset? resolvedDate = null;
                    if (isResolved && thread.TryGetProperty("lastUpdatedDate", out var threadUpdated))
                    {
                        resolvedDate = threadUpdated.GetDateTimeOffset();
                        // The author of the last comment in a resolved thread is typically the resolver
                        resolvedBy = author;
                    }

                    comments.Add(new PullRequestCommentDto(
                        Id: commentId,
                        PullRequestId: pullRequestId,
                        ThreadId: threadId,
                        Author: author,
                        Content: content,
                        CreatedDate: createdDate,
                        UpdatedDate: updatedDate,
                        IsResolved: isResolved,
                        ResolvedDate: resolvedDate,
                        ResolvedBy: resolvedBy
                    ));
                }
            }

            _logger.LogInformation("Retrieved {Count} comments for PR {PullRequestId}", comments.Count, pullRequestId);
            return comments;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/iterations/{iterationId}/changes?api-version={entity.ApiVersion}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var fileChanges = new List<PullRequestFileChangeDto>();

            if (!doc.RootElement.TryGetProperty("changeEntries", out var changeEntries))
                return fileChanges;

            foreach (var change in changeEntries.EnumerateArray())
            {
                var filePath = "";
                if (change.TryGetProperty("item", out var item))
                {
                    filePath = item.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                }

                var changeType = change.TryGetProperty("changeType", out var ct) ? ct.GetString() ?? "" : "";

                // Note: Line-level statistics require additional API call to get diff
                // For now, we'll set counts to 0 and can enhance later with diff API
                var linesAdded = 0;
                var linesDeleted = 0;
                var linesModified = 0;

                fileChanges.Add(new PullRequestFileChangeDto(
                    PullRequestId: pullRequestId,
                    IterationId: iterationId,
                    FilePath: filePath,
                    ChangeType: changeType,
                    LinesAdded: linesAdded,
                    LinesDeleted: linesDeleted,
                    LinesModified: linesModified
                ));
            }

            _logger.LogInformation("Retrieved {Count} file changes for PR {PullRequestId} iteration {IterationId}", 
                fileChanges.Count, pullRequestId, iterationId);
            return fileChanges;
        }, cancellationToken);
    }

    public async Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Get work item with all revisions using the revisions expand parameter
            var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/wit/workitems/{workItemId}/revisions?api-version={entity.ApiVersion}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var revisions = new List<WorkItemRevisionDto>();

            if (!doc.RootElement.TryGetProperty("value", out var revisionsArray))
                return revisions;

            WorkItemRevisionDto? previousRevision = null;

            foreach (var revision in revisionsArray.EnumerateArray())
            {
                var revNumber = revision.TryGetProperty("rev", out var rev) ? rev.GetInt32() : 0;
                
                var changedBy = "";
                if (revision.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("System.ChangedBy", out var cb))
                    {
                        if (cb.ValueKind == JsonValueKind.Object && cb.TryGetProperty("displayName", out var displayName))
                        {
                            changedBy = displayName.GetString() ?? "";
                        }
                        else if (cb.ValueKind == JsonValueKind.String)
                        {
                            changedBy = cb.GetString() ?? "";
                        }
                    }
                }

                var changedDate = DateTimeOffset.UtcNow;
                if (revision.TryGetProperty("fields", out var fieldsForDate))
                {
                    if (fieldsForDate.TryGetProperty("System.ChangedDate", out var cd) && cd.ValueKind != JsonValueKind.Null)
                    {
                        changedDate = cd.GetDateTimeOffset();
                    }
                }

                var comment = "";
                if (revision.TryGetProperty("fields", out var fieldsForComment))
                {
                    if (fieldsForComment.TryGetProperty("System.History", out var hist) && hist.ValueKind == JsonValueKind.String)
                    {
                        comment = hist.GetString();
                    }
                }

                // Calculate field changes by comparing with previous revision
                var fieldChanges = new Dictionary<string, WorkItemFieldChange>();
                
                if (previousRevision != null && revision.TryGetProperty("fields", out var currentFields))
                {
                    // Get all fields from current revision
                    foreach (var field in currentFields.EnumerateObject())
                    {
                        var fieldName = field.Name;
                        var newValue = GetFieldValueAsString(field.Value);
                        
                        // Skip system fields that are not interesting for history
                        if (fieldName.StartsWith("System.Watermark") || 
                            fieldName.StartsWith("System.Rev") ||
                            fieldName == "System.ChangedDate" ||
                            fieldName == "System.ChangedBy" ||
                            fieldName == "System.RevisedDate")
                        {
                            continue;
                        }

                        // Find the old value from previous revision
                        string? oldValue = null;
                        if (previousRevision != null)
                        {
                            // Try to get from the previous revision's raw data (we'll need to store it)
                            // For now, we'll mark it as changed if the field exists
                            oldValue = null; // Will be populated if we had previous revision data
                        }

                        // Only add if value actually changed or is a new field
                        if (oldValue != newValue)
                        {
                            fieldChanges[fieldName] = new WorkItemFieldChange(
                                FieldName: fieldName,
                                OldValue: oldValue,
                                NewValue: newValue
                            );
                        }
                    }
                }

                var revisionDto = new WorkItemRevisionDto(
                    RevisionNumber: revNumber,
                    WorkItemId: workItemId,
                    ChangedBy: changedBy,
                    ChangedDate: changedDate,
                    FieldChanges: fieldChanges,
                    Comment: comment
                );

                revisions.Add(revisionDto);
                previousRevision = revisionDto;
            }

            _logger.LogInformation("Retrieved {Count} revisions for work item {WorkItemId}", 
                revisions.Count, workItemId);
            return revisions;
        }, cancellationToken);
    }

    // Private helper methods

    private static string? GetFieldValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.GetRawText(), // For complex objects, return JSON
            JsonValueKind.Array => element.GetRawText(),
            _ => null
        };
    }

    private async Task ConfigureAuthenticationAsync(TfsConfigEntity entity, CancellationToken cancellationToken)
    {
        if (entity.AuthMode == TfsAuthMode.Pat)
        {
            // Get PAT from current request context (provided via X-TFS-PAT header)
            var pat = _patAccessor.GetPat();
            
            if (string.IsNullOrEmpty(pat))
            {
                throw new TfsAuthenticationException(
                    "PAT must be provided via X-TFS-PAT header. " +
                    "PAT is stored client-side for security. See docs/PAT_STORAGE_BEST_PRACTICES.md", 
                    (string?)null);
            }

            // Configure HTTP client with PAT authentication
            _authProvider.ConfigurePatAuthentication(_httpClient, pat);
            
            _logger.LogDebug("Configured PAT authentication for TFS request");
        }
        // NTLM is configured via HttpClientHandler, so no additional configuration needed here
    }

    private async Task<List<(string Name, string Id)>> GetRepositoriesAsync(
        TfsConfigEntity entity, 
        string? repositoryName, 
        CancellationToken cancellationToken)
    {
        // If specific repository requested, return just that one
        if (!string.IsNullOrEmpty(repositoryName))
        {
            return new List<(string Name, string Id)> { (repositoryName, repositoryName) };
        }

        // Otherwise, get all repositories in the project
        var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/git/repositories?api-version={entity.ApiVersion}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        await HandleHttpErrorsAsync(response, cancellationToken);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var repositories = new List<(string Name, string Id)>();

        if (doc.RootElement.TryGetProperty("value", out var valueArray))
        {
            foreach (var repo in valueArray.EnumerateArray())
            {
                var name = repo.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var id = repo.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                
                if (!string.IsNullOrEmpty(name))
                {
                    repositories.Add((name, id));
                }
            }
        }

        _logger.LogInformation("Found {Count} repositories in project {Project}", repositories.Count, entity.Project);
        return repositories;
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken, int maxRetries = MaxRetries)
    {
        int attempt = 0;
        
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
            {
                attempt++;
                var delay = CalculateBackoffDelay(attempt);
                
                _logger.LogWarning(ex, 
                    "TFS request failed (attempt {Attempt}/{MaxRetries}), retrying after {DelayMs}ms", 
                    attempt, maxRetries, delay.TotalMilliseconds);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private bool IsTransient(Exception ex)
    {
        return ex is TfsRateLimitException 
            || ex is HttpRequestException
            || (ex is TfsException tfsEx && tfsEx.StatusCode >= 500);
    }

    private TimeSpan CalculateBackoffDelay(int attempt)
    {
        // Exponential backoff with jitter: 2^attempt seconds + random jitter up to 1 second
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        return baseDelay + jitter;
    }

    private async Task HandleHttpErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        var exception = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new TfsAuthenticationException(
                "TFS authentication failed. Check your PAT or credentials.", errorContent),
            HttpStatusCode.Forbidden => new TfsAuthorizationException(
                "TFS authorization failed. Insufficient permissions.", errorContent),
            HttpStatusCode.NotFound => new TfsResourceNotFoundException(
                "TFS resource not found. Check project name and URL.", errorContent),
            HttpStatusCode.TooManyRequests => new TfsRateLimitException(
                "TFS rate limit exceeded. Please try again later.", errorContent, 
                GetRetryAfter(response)),
            _ when (int)response.StatusCode >= 500 => new TfsException(
                $"TFS server error: {response.StatusCode}", (int)response.StatusCode, errorContent),
            _ => new TfsException(
                $"TFS request failed: {response.StatusCode}", (int)response.StatusCode, errorContent)
        };

        _logger.LogError("TFS HTTP error: {StatusCode} - {Message}", response.StatusCode, exception.Message);
        throw exception;
    }

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }
        return null;
    }

    private string EscapeWiql(string value)
    {
        return value.Replace("'", "''");
    }
}
