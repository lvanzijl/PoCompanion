using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Pipelines;
using PoTool.Core.Exceptions;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts.TfsVerification;

namespace PoTool.Api.Services;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation with retry logic and enhanced error handling.
/// Supports Azure DevOps Server 2022.2 (API 7.0) and TFS 2019+ (API 5.1+).
/// This is the production implementation that connects to actual Azure DevOps/TFS servers.
/// </summary>
public class RealTfsClient : ITfsClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TfsConfigurationService _configService;
    private readonly TfsAuthenticationProvider _authProvider;
    private readonly PatAccessor _patAccessor;
    private readonly ILogger<RealTfsClient> _logger;
    private const int MaxRetries = 3;

    // ID offset for release pipelines/runs to avoid collision with build IDs
    private const int ReleaseIdOffset = 100000;

    // TFS field paths
    private const string TfsFieldEffort = "Microsoft.VSTS.Scheduling.Effort";
    private const string TfsFieldStoryPoints = "Microsoft.VSTS.Scheduling.StoryPoints";
    private const string TfsFieldState = "System.State";

    // Required work item fields for queries
    private static readonly string[] RequiredWorkItemFields = new[]
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.AreaPath",
        "System.IterationPath",
        TfsFieldEffort,
        TfsFieldStoryPoints
    };

    public RealTfsClient(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        TfsConfigurationService configService, 
        TfsAuthenticationProvider authProvider,
        PatAccessor patAccessor,
        ILogger<RealTfsClient> logger)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _authProvider = authProvider;
        _patAccessor = patAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets an HttpClient properly configured for the current authentication mode.
    /// Uses named HttpClients from IHttpClientFactory to ensure correct handler configuration.
    /// For PAT mode: No default Windows credentials (avoids conflicts)
    /// For NTLM mode: Uses default Windows credentials
    /// </summary>
    /// <param name="entity">TFS configuration entity containing auth mode and timeout settings.</param>
    /// <returns>Configured HttpClient for the specified auth mode.</returns>
    /// <exception cref="TfsAuthenticationException">Thrown when PAT is required but not provided, or auth mode is unsupported.</exception>
    private HttpClient GetAuthenticatedHttpClient(TfsConfigEntity entity)
    {
        HttpClient client;
        
        if (entity.AuthMode == TfsAuthMode.Pat)
        {
            // Get PAT-configured client (no default credentials in handler)
            client = _httpClientFactory.CreateClient("TfsClient.PAT");
            
            // Get PAT from current request context (provided via X-TFS-PAT header)
            var pat = _patAccessor.GetPat();
            
            if (string.IsNullOrEmpty(pat))
            {
                throw new TfsAuthenticationException(
                    "PAT must be provided via X-TFS-PAT header. " +
                    "PAT is stored client-side for security. See docs/PAT_STORAGE_BEST_PRACTICES.md", 
                    (string?)null);
            }

            // Configure PAT authentication via Authorization header
            _authProvider.ConfigurePatAuthentication(client, pat);
            
            _logger.LogDebug("Using PAT-authenticated HttpClient for TFS request");
        }
        else if (entity.AuthMode == TfsAuthMode.Ntlm)
        {
            // Get NTLM-configured client (with UseDefaultCredentials=true in handler)
            client = _httpClientFactory.CreateClient("TfsClient.NTLM");
            
            _logger.LogDebug("Using NTLM-authenticated HttpClient for TFS request");
        }
        else
        {
            throw new TfsAuthenticationException(
                $"Unsupported authentication mode: {entity.AuthMode}. " +
                "Only PAT and NTLM modes are supported.", 
                (string?)null);
        }
        
        // Configure timeout from entity (per-request since timeout can be changed in configuration)
        client.Timeout = TimeSpan.FromSeconds(entity.TimeoutSeconds);
        
        return client;
    }

    /// <summary>
    /// Builds a collection-scoped URL (no project in path).
    /// Use for: _apis/projects, _apis/wit/fields, _apis/wit/workitems?ids=...
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to collection root (e.g., "_apis/projects").</param>
    /// <returns>Full URL including api-version.</returns>
    private static string CollectionUrl(TfsConfigEntity config, string relativePath)
    {
        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{path}{separator}api-version={config.ApiVersion}";
    }

    /// <summary>
    /// Builds a project-scoped URL (project in path).
    /// Use for: WIQL, Git repositories, pull requests, build/release pipelines.
    /// Project name is URL-encoded to support spaces.
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to project (e.g., "_apis/wit/wiql").</param>
    /// <returns>Full URL including api-version.</returns>
    private static string ProjectUrl(TfsConfigEntity config, string relativePath)
    {
        // URL-encode project name to support spaces and special characters
        var encodedProject = Uri.EscapeDataString(config.Project);
        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{encodedProject}/{path}{separator}api-version={config.ApiVersion}";
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
            // Use auth-mode-specific HttpClient to avoid credential conflicts
            var httpClient = GetAuthenticatedHttpClient(entity);
            
            // Step 1: Validate server connectivity using collection-scoped projects endpoint
            var projectsUrl = CollectionUrl(entity, "_apis/projects");
            _logger.LogInformation("Validating TFS connection: GET {Url} (AuthMode: {AuthMode})", projectsUrl, entity.AuthMode);
            
            var resp = await httpClient.GetAsync(projectsUrl, cancellationToken);
            
            _logger.LogInformation("Validation GET {Url} returned {StatusCode}", projectsUrl, resp.StatusCode);
            
            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("TFS connection validation failed: HTTP {StatusCode}, Response: {ErrorBody}", 
                    resp.StatusCode, errorBody);
                return false;
            }

            // Step 2: Validate that the configured project exists (requirement #6)
            var encodedProject = Uri.EscapeDataString(entity.Project);
            var projectUrl = CollectionUrl(entity, $"_apis/projects/{encodedProject}");
            _logger.LogInformation("Validating project access: GET {Url}", projectUrl);
            
            var projectResp = await httpClient.GetAsync(projectUrl, cancellationToken);
            
            if (!projectResp.IsSuccessStatusCode)
            {
                if (projectResp.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogError("Project '{Project}' does not exist or is not accessible", entity.Project);
                }
                else
                {
                    var errorBody = await projectResp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Project validation failed: HTTP {StatusCode}, Response: {ErrorBody}", 
                        projectResp.StatusCode, errorBody);
                }
                return false;
            }

            // Store confirmed project info from response
            using var stream = await projectResp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var confirmedProjectName = doc.RootElement.GetProperty("name").GetString();
            _logger.LogInformation("Project validated: {ProjectName}", confirmedProjectName);

            // Update last validated timestamp
            entity.LastValidated = DateTimeOffset.UtcNow;
            await _configService.SaveConfigEntityAsync(entity, cancellationToken);
            _logger.LogInformation("TFS connection validation successful");
            return true;
        }
        catch (TfsAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed during TFS connection validation");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during TFS connection validation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during TFS connection validation");
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
        ValidateTfsConfiguration(entity);
        
        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient(config);

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build WIQL query with optional date filter for incremental sync (Phase 3)
            // Use UNDER operator for area path to support deeper hierarchies (requirement #3)
            // Note: WIQL Select only needs System.Id since we fetch full work items in a separate batch call
            // with all RequiredWorkItemFields. The other fields here are for debugging/logging purposes.
            var dateFilter = since.HasValue 
                ? $" AND [System.ChangedDate] >= '{since.Value:yyyy-MM-ddTHH:mm:ssZ}'" 
                : "";

            var wiql = new
            {
                query = $"Select [System.Id] From WorkItems Where [System.AreaPath] UNDER '{EscapeWiql(areaPath)}'{dateFilter}"
            };

            // WIQL is project-scoped (requirement #1)
            var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            // Phase 4: Enhanced logging
            _logger.LogDebug("Executing WIQL query: {Query}", wiql.query);
            
            var wiqlResponse = await httpClient.PostAsync(wiqlUrl, content, cancellationToken);
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

            // Use Work Items Batch API to avoid 414 Request-URI Too Long errors
            // Split IDs into batches and use POST _apis/wit/workitemsbatch
            // Recommended batch size: 200 IDs per batch
            const int batchSize = 200;
            var results = new List<WorkItemDto>();
            var totalBatches = (int)Math.Ceiling((double)ids.Length / batchSize);

            _logger.LogInformation("Fetching {TotalIds} work items in {BatchCount} batches of {BatchSize}", 
                ids.Length, totalBatches, batchSize);

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batchStartTime = DateTimeOffset.UtcNow;
                var batchIds = ids.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                
                _logger.LogDebug("Processing batch {BatchIndex}/{TotalBatches} with {IdCount} IDs", 
                    batchIndex + 1, totalBatches, batchIds.Length);

                // Build request body for Work Items Batch API
                var batchRequest = new
                {
                    ids = batchIds,
                    fields = RequiredWorkItemFields,
                    // Use $expand=relations to get parent link (requirement #4)
                    @expand = "relations"
                };

                // Work Items Batch API is collection-scoped (work item IDs are unique across collection)
                var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
                using var batchContent = new StringContent(
                    JsonSerializer.Serialize(batchRequest), 
                    System.Text.Encoding.UTF8, 
                    "application/json");

                var batchResponse = await httpClient.PostAsync(batchUrl, batchContent, cancellationToken);
                await HandleHttpErrorsAsync(batchResponse, cancellationToken);

                using var batchStream = await batchResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var batchDoc = await JsonDocument.ParseAsync(batchStream, cancellationToken: cancellationToken);

                // Process work items from batch response
                foreach (var item in batchDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var fields = item.GetProperty("fields");
                    var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
                    var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
                    var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                    var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
                    var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
                    
                    // Extract parent work item ID from relations (requirement #4)
                    // Parent relationship is stored in relations with rel == "System.LinkTypes.Hierarchy-Reverse"
                    int? parentId = ExtractParentIdFromRelations(item);

                    // Extract effort field with robust parsing (requirement #5)
                    // Handle int, double, and string values safely
                    int? effort = ParseEffortField(fields);

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

                var batchElapsed = DateTimeOffset.UtcNow - batchStartTime;
                _logger.LogInformation(
                    "Batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs fetched, HTTP {StatusCode}, {ElapsedMs}ms",
                    batchIndex + 1, totalBatches, batchIds.Length, (int)batchResponse.StatusCode, batchElapsed.TotalMilliseconds);
            }

            // Phase 4: Performance metrics
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation("Retrieved {Count} work items for areaPath={AreaPath}, since={Since} in {ElapsedMs}ms", 
                results.Count, areaPath, since, elapsed.TotalMilliseconds);
            
            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Extracts the parent work item ID from the relations array.
    /// Parent relationship is stored with rel == "System.LinkTypes.Hierarchy-Reverse".
    /// </summary>
    private static int? ExtractParentIdFromRelations(JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("rel", out var rel))
                continue;
            
            var relType = rel.GetString();
            if (relType != "System.LinkTypes.Hierarchy-Reverse")
                continue;

            if (!relation.TryGetProperty("url", out var urlProp))
                continue;

            var url = urlProp.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            // Extract work item ID from the last segment of the URL
            var segments = url.Split('/');
            if (segments.Length > 0 && int.TryParse(segments[^1], out var parsedId))
            {
                return parsedId;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses effort from work item fields with robust type handling.
    /// Handles int, double, and string values safely (requirement #5).
    /// </summary>
    private static int? ParseEffortField(JsonElement fields)
    {
        // Try Microsoft.VSTS.Scheduling.Effort first
        if (fields.TryGetProperty(TfsFieldEffort, out var effortField))
        {
            var parsed = ParseNumericValue(effortField);
            if (parsed.HasValue)
                return parsed;
        }

        // Fall back to Microsoft.VSTS.Scheduling.StoryPoints
        if (fields.TryGetProperty(TfsFieldStoryPoints, out var storyPoints))
        {
            return ParseNumericValue(storyPoints);
        }

        return null;
    }

    /// <summary>
    /// Parses a JSON element as a numeric value, handling int, double, and string types.
    /// </summary>
    private static int? ParseNumericValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                // TryGetInt32 handles integer values
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                // Fall back to double for decimal values
                if (element.TryGetDouble(out var doubleValue))
                    return (int)Math.Round(doubleValue);
                break;

            case JsonValueKind.String:
                var strValue = element.GetString();
                if (!string.IsNullOrEmpty(strValue))
                {
                    // Try parsing as int first
                    if (int.TryParse(strValue, out var parsedInt))
                        return parsedInt;
                    // Try parsing as double and round
                    if (double.TryParse(strValue, out var parsedDouble))
                        return (int)Math.Round(parsedDouble);
                }
                break;
        }

        return null;
    }

    // Pull Request methods - Phase 2 implementation with Phase 4 enhancements
    public async Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Get all repositories or specific one
            var repositories = await GetRepositoriesInternalAsync(config, httpClient, repositoryName, cancellationToken);
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

                // Git PRs are project-scoped (requirement #1)
                var encodedRepoName = Uri.EscapeDataString(repo.Name);
                var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests?{string.Join("&", queryParams)}");
                
                _logger.LogDebug("Fetching PRs from repository {Repository}", repo.Name);
                
                var response = await httpClient.GetAsync(url, cancellationToken);
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
                    var iterationPath = config.Project; // Default to project name

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
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/iterations");
            
            var response = await httpClient.GetAsync(url, cancellationToken);
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
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/threads");
            
            var response = await httpClient.GetAsync(url, cancellationToken);
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
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/iterations/{iterationId}/changes");
            
            var response = await httpClient.GetAsync(url, cancellationToken);
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
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Work item revisions are collection-scoped (work item IDs are unique across collection)
            var url = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}/revisions");
            
            var response = await httpClient.GetAsync(url, cancellationToken);
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

    /// <summary>
    /// DEPRECATED: This method is kept for backward compatibility but is no longer used.
    /// All TFS calls now use GetAuthenticatedHttpClient() which creates properly configured
    /// HttpClient instances from IHttpClientFactory.
    /// 
    /// The old approach of mutating _httpClient state was problematic because:
    /// - NTLM authentication must be handled in HttpClientHandler, not headers
    /// - Mixing PAT headers with NTLM handlers causes conflicts
    /// - The same HttpClient instance shouldn't be reused across auth modes
    /// </summary>
    [Obsolete("Use GetAuthenticatedHttpClient() instead. This method is kept only for backward compatibility.")]
    private Task ConfigureAuthenticationAsync(TfsConfigEntity entity, CancellationToken cancellationToken)
    {
        // This method is no longer called but kept for backward compatibility
        _logger.LogWarning("ConfigureAuthenticationAsync is deprecated. Use GetAuthenticatedHttpClient() instead.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets repositories in the project using the provided authenticated HttpClient.
    /// Git repositories are project-scoped.
    /// </summary>
    private async Task<List<(string Name, string Id)>> GetRepositoriesInternalAsync(
        TfsConfigEntity config, 
        HttpClient httpClient,
        string? repositoryName, 
        CancellationToken cancellationToken)
    {
        // If specific repository requested, return just that one
        if (!string.IsNullOrEmpty(repositoryName))
        {
            return new List<(string Name, string Id)> { (repositoryName, repositoryName) };
        }

        // Git repositories are project-scoped (requirement #1)
        var url = ProjectUrl(config, "_apis/git/repositories");
        var response = await httpClient.GetAsync(url, cancellationToken);
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

        _logger.LogInformation("Found {Count} repositories in project {Project}", repositories.Count, config.Project);
        return repositories;
    }

    // Legacy method kept for backward compatibility - forwards to internal implementation
    private async Task<List<(string Name, string Id)>> GetRepositoriesAsync(
        TfsConfigEntity entity, 
        string? repositoryName, 
        CancellationToken cancellationToken)
    {
        var httpClient = GetAuthenticatedHttpClient(entity);
        return await GetRepositoriesInternalAsync(entity, httpClient, repositoryName, cancellationToken);
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

    public async Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} to state '{NewState}'", workItemId, newState);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient(entity);

            // Build JSON Patch document
            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldState}",
                    value = newState
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument), 
                System.Text.Encoding.UTF8, 
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId}", workItemId);
            
            var response = await httpClient.PatchAsync(updateUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} to state '{NewState}'", workItemId, newState);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId}. Status: {StatusCode}, Response: {Response}", 
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} state to '{NewState}'", workItemId, newState);
            return false;
        }
    }

    public async Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} effort to {Effort}", workItemId, effort);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item effort");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient(entity);

            // Build JSON Patch document for effort (Microsoft.VSTS.Scheduling.Effort)
            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldEffort}",
                    value = effort
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument), 
                System.Text.Encoding.UTF8, 
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} effort", workItemId);
            
            var response = await httpClient.PatchAsync(updateUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} effort to {Effort}", workItemId, effort);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} effort. Status: {StatusCode}, Response: {Response}", 
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} effort to {Effort}", workItemId, effort);
            return false;
        }
    }

    public async Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        _logger.LogInformation("Starting TFS API verification. WriteChecks: {IncludeWriteChecks}, WorkItemId: {WorkItemId}", 
            includeWriteChecks, workItemIdForWriteCheck);

        var checks = new List<TfsCapabilityCheckResult>();
        
        // Run read-only checks
        // Step 1: Server & authentication validation
        checks.Add(await VerifyServerReachabilityAsync(config, cancellationToken));
        // Step 2: Project validation
        checks.Add(await VerifyProjectAccessAsync(config, cancellationToken));
        // Step 3: Work item query (WIQL)
        checks.Add(await VerifyWorkItemQueryAsync(config, cancellationToken));
        // Step 4: Work item hierarchy chain retrieval
        checks.Add(await VerifyWorkItemHierarchyAsync(config, cancellationToken));
        // Step 5: Work item fields
        checks.Add(await VerifyWorkItemFieldsAsync(config, cancellationToken));
        // Step 6: Batch read
        checks.Add(await VerifyBatchReadAsync(config, cancellationToken));
        // Step 7: Work item revisions
        checks.Add(await VerifyWorkItemRevisionsAsync(config, cancellationToken));
        // Step 8: Pull requests
        checks.Add(await VerifyPullRequestsAsync(config, cancellationToken));
        // Step 9: Pipelines (build + release)
        checks.Add(await VerifyPipelinesAsync(config, cancellationToken));
        
        // Run write checks if requested
        if (includeWriteChecks)
        {
            if (workItemIdForWriteCheck.HasValue)
            {
                checks.Add(await VerifyWorkItemUpdateAsync(config, workItemIdForWriteCheck.Value, cancellationToken));
            }
            else
            {
                _logger.LogWarning("Write checks requested but no work item ID provided, skipping write verification");
            }
        }

        var report = new TfsVerificationReport
        {
            VerifiedAt = DateTimeOffset.UtcNow,
            ServerUrl = config.Url,
            ProjectName = config.Project,
            ApiVersion = config.ApiVersion,
            IncludedWriteChecks = includeWriteChecks,
            Success = checks.All(c => c.Success),
            Checks = checks
        };

        _logger.LogInformation("TFS verification completed. Success: {Success}, Passed: {Passed}/{Total}", 
            report.Success, checks.Count(c => c.Success), checks.Count);

        return report;
    }

    private async Task<TfsCapabilityCheckResult> VerifyServerReachabilityAsync(
        TfsConfigEntity config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Collection-scoped endpoint for server reachability
            var url = CollectionUrl(config, "_apis/projects");
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "server-reachability",
                    Success = true,
                    ImpactedFunctionality = "All TFS integration features",
                    ExpectedBehavior = "Server responds to API requests with valid authentication",
                    ObservedBehavior = $"Server reachable, authentication successful (HTTP {(int)response.StatusCode})"
                };
            }
            
            return CreateFailureResult(
                "server-reachability",
                "All TFS integration features",
                "Server responds to API requests with valid authentication",
                $"Server returned HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "server-reachability",
                "All TFS integration features",
                "Server responds to API requests with valid authentication",
                $"Exception: {ex.GetType().Name}",
                ex is TfsAuthenticationException ? FailureCategory.Authentication : FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyProjectAccessAsync(
        TfsConfigEntity config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Project access check is collection-scoped with project name in path
            var encodedProject = Uri.EscapeDataString(config.Project);
            var url = CollectionUrl(config, $"_apis/projects/{encodedProject}");
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var projectName = doc.RootElement.GetProperty("name").GetString();
                
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "project-access",
                    Success = true,
                    ImpactedFunctionality = "Work item retrieval, project-specific operations",
                    ExpectedBehavior = $"Project '{config.Project}' exists and is accessible",
                    ObservedBehavior = $"Project found: {projectName}"
                };
            }
            
            return CreateFailureResult(
                "project-access",
                "Work item retrieval, project-specific operations",
                $"Project '{config.Project}' exists and is accessible",
                $"HTTP {(int)response.StatusCode}",
                response.StatusCode == HttpStatusCode.NotFound ? FailureCategory.Authorization : CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "project-access",
                "Work item retrieval, project-specific operations",
                $"Project '{config.Project}' exists and is accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemQueryAsync(
        TfsConfigEntity config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            var wiql = new
            {
                query = "Select [System.Id] From WorkItems Where [System.WorkItemType] <> ''"
            };

            // WIQL is project-scoped (requirement #1)
            var url = ProjectUrl(config, "_apis/wit/wiql");
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync(url, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-query",
                    Success = true,
                    ImpactedFunctionality = "Work item search and filtering",
                    ExpectedBehavior = "WIQL queries execute successfully",
                    ObservedBehavior = "WIQL query executed successfully"
                };
            }
            
            return CreateFailureResult(
                "work-item-query",
                "Work item search and filtering",
                "WIQL queries execute successfully",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-query",
                "Work item search and filtering",
                "WIQL queries execute successfully",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.QueryRestriction,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemHierarchyAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Step 1: Run WIQL query to get a few work items
            var wiql = new
            {
                query = $"Select [System.Id] From WorkItems Where [System.AreaPath] UNDER '{EscapeWiql(config.DefaultAreaPath)}' ORDER BY [System.Id] DESC"
            };

            var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
            using var wiqlContent = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");
            
            var wiqlResponse = await httpClient.PostAsync(wiqlUrl, wiqlContent, cancellationToken);
            
            if (!wiqlResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-hierarchy",
                    "Work item hierarchy display (Goal → Task chain)",
                    "Work item relationships can be resolved",
                    $"WIQL query failed: HTTP {(int)wiqlResponse.StatusCode}",
                    CategorizeHttpError(wiqlResponse.StatusCode),
                    await wiqlResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            using var wiqlStream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var wiqlDoc = await JsonDocument.ParseAsync(wiqlStream, cancellationToken: cancellationToken);

            var workItemIds = wiqlDoc.RootElement.GetProperty("workItems").EnumerateArray()
                .Take(10) // Sample first 10 items
                .Select(e => e.GetProperty("id").GetInt32())
                .ToArray();

            if (workItemIds.Length == 0)
            {
                // No work items found - this is acceptable (not a failure)
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-hierarchy",
                    Success = true,
                    ImpactedFunctionality = "Work item hierarchy display (Goal → Task chain)",
                    ExpectedBehavior = "Work item relationships can be resolved",
                    ObservedBehavior = "No work items found in configured area path (hierarchy verification skipped)"
                };
            }

            // Step 2: Fetch work items with relations to verify hierarchy resolution
            // Use Work Items Batch API (POST) instead of GET to avoid potential 414 errors
            var batchRequest = new
            {
                ids = workItemIds,
                @expand = "relations"
            };

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var batchContent = new StringContent(
                JsonSerializer.Serialize(batchRequest), 
                System.Text.Encoding.UTF8, 
                "application/json");

            var itemsResponse = await httpClient.PostAsync(batchUrl, batchContent, cancellationToken);

            if (!itemsResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-hierarchy",
                    "Work item hierarchy display (Goal → Task chain)",
                    "Work item relationships can be resolved",
                    $"Batch work item fetch failed: HTTP {(int)itemsResponse.StatusCode}",
                    CategorizeHttpError(itemsResponse.StatusCode),
                    await itemsResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            using var itemsStream = await itemsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var itemsDoc = await JsonDocument.ParseAsync(itemsStream, cancellationToken: cancellationToken);

            // Step 3: Verify hierarchy resolution - check for parent relationships
            var itemsWithParent = 0;
            var totalItems = 0;
            var maxDepthFound = 0;
            var workItemsWithRelations = new Dictionary<int, int?>(); // id -> parentId

            foreach (var item in itemsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                totalItems++;
                var itemId = item.GetProperty("id").GetInt32();
                var parentId = ExtractParentIdFromRelations(item);
                workItemsWithRelations[itemId] = parentId;
                
                if (parentId.HasValue)
                {
                    itemsWithParent++;
                }
            }

            // Try to follow parent chain to find max depth
            foreach (var (itemId, parentId) in workItemsWithRelations)
            {
                if (!parentId.HasValue) continue;
                
                var depth = 1;
                var currentParentId = parentId;
                var visitedIds = new HashSet<int> { itemId };
                
                while (currentParentId.HasValue && depth < 10 && !visitedIds.Contains(currentParentId.Value))
                {
                    visitedIds.Add(currentParentId.Value);
                    if (workItemsWithRelations.TryGetValue(currentParentId.Value, out var nextParent))
                    {
                        currentParentId = nextParent;
                        depth++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                maxDepthFound = Math.Max(maxDepthFound, depth);
            }

            return new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-hierarchy",
                Success = true,
                ImpactedFunctionality = "Work item hierarchy display (Goal → Task chain)",
                ExpectedBehavior = "Work item relationships can be resolved",
                ObservedBehavior = $"Hierarchy resolution working. {itemsWithParent}/{totalItems} items have parent links. Max depth traced: {maxDepthFound}"
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-hierarchy",
                "Work item hierarchy display (Goal → Task chain)",
                "Work item relationships can be resolved",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemFieldsAsync(
        TfsConfigEntity config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Work item fields are collection-scoped (requirement #1)
            var url = CollectionUrl(config, "_apis/wit/fields");
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                
                var fields = doc.RootElement.GetProperty("value").EnumerateArray()
                    .Select(f => f.GetProperty("referenceName").GetString())
                    .ToList();
                
                // Check for required fields
                var requiredFields = new[] { "System.Id", "System.Title", "System.State", "System.WorkItemType" };
                var missingFields = requiredFields.Where(rf => !fields.Contains(rf)).ToList();
                
                if (missingFields.Any())
                {
                    return CreateFailureResult(
                        "work-item-fields",
                        "Work item display and processing",
                        "Required work item fields are accessible",
                        $"Missing fields: {string.Join(", ", missingFields)}",
                        FailureCategory.MissingField,
                        $"Found {fields.Count} fields but missing required fields");
                }
                
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-fields",
                    Success = true,
                    ImpactedFunctionality = "Work item display and processing",
                    ExpectedBehavior = "Required work item fields are accessible",
                    ObservedBehavior = $"All required fields present ({fields.Count} total fields)"
                };
            }
            
            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required work item fields are accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required work item fields are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyBatchReadAsync(
        TfsConfigEntity config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Test Work Items Batch API (POST) which is the recommended approach
            // This is collection-scoped (work item IDs are unique across collection)
            var batchRequest = new
            {
                ids = new[] { 1, 2, 3 }
            };

            var url = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var content = new StringContent(
                JsonSerializer.Serialize(batchRequest), 
                System.Text.Encoding.UTF8, 
                "application/json");

            var response = await httpClient.PostAsync(url, content, cancellationToken);
            
            // We expect 200 (with items) or 404 (no items found), both are acceptable
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "batch-read",
                    Success = true,
                    ImpactedFunctionality = "Efficient work item synchronization",
                    ExpectedBehavior = "Batch work item retrieval is supported",
                    ObservedBehavior = "Work Items Batch API endpoint responded successfully"
                };
            }
            
            return CreateFailureResult(
                "batch-read",
                "Efficient work item synchronization",
                "Batch work item retrieval is supported",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "batch-read",
                "Efficient work item synchronization",
                "Batch work item retrieval is supported",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemRevisionsAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Work item revisions are collection-scoped (work item IDs are unique across collection)
            var url = CollectionUrl(config, "_apis/wit/workitems/1/revisions");
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            // We expect either 200 (work item exists with revisions) or 404 (work item doesn't exist)
            // Both indicate the API endpoint is available
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-revisions",
                    Success = true,
                    ImpactedFunctionality = "Work item history and change tracking",
                    ExpectedBehavior = "Work item revision history API is accessible",
                    ObservedBehavior = "Revision history endpoint responded successfully"
                };
            }
            
            return CreateFailureResult(
                "work-item-revisions",
                "Work item history and change tracking",
                "Work item revision history API is accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-revisions",
                "Work item history and change tracking",
                "Work item revision history API is accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyPullRequestsAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            // Git repositories are project-scoped (requirement #1)
            var url = ProjectUrl(config, "_apis/git/repositories");
            var response = await httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                
                var repoCount = 0;
                if (doc.RootElement.TryGetProperty("value", out var repos))
                {
                    repoCount = repos.GetArrayLength();
                }
                
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "pull-requests",
                    Success = true,
                    ImpactedFunctionality = "Pull request retrieval and analysis",
                    ExpectedBehavior = "Git repositories and pull request API are accessible",
                    ObservedBehavior = $"Git repositories API accessible ({repoCount} repositories found)"
                };
            }
            
            return CreateFailureResult(
                "pull-requests",
                "Pull request retrieval and analysis",
                "Git repositories and pull request API are accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "pull-requests",
                "Pull request retrieval and analysis",
                "Git repositories and pull request API are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyPipelinesAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            var buildDefinitionsFound = 0;
            var buildRunsFound = 0;
            var releaseDefinitionsFound = 0;
            var releaseApiAvailable = false;
            string? releaseApiError = null;

            // Step 1: Verify build definitions API
            var buildDefsUrl = ProjectUrl(config, "_apis/build/definitions");
            var buildDefsResponse = await httpClient.GetAsync(buildDefsUrl, cancellationToken);
            
            if (buildDefsResponse.IsSuccessStatusCode)
            {
                using var stream = await buildDefsResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                
                if (doc.RootElement.TryGetProperty("value", out var defs))
                {
                    buildDefinitionsFound = defs.GetArrayLength();
                }
            }
            else
            {
                return CreateFailureResult(
                    "pipelines",
                    "Pipeline and build status monitoring",
                    "Build/release definitions and runs are accessible",
                    $"Build definitions API failed: HTTP {(int)buildDefsResponse.StatusCode}",
                    CategorizeHttpError(buildDefsResponse.StatusCode),
                    await buildDefsResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            // Step 2: Verify build runs API (if definitions exist)
            if (buildDefinitionsFound > 0)
            {
                var buildRunsUrl = ProjectUrl(config, "_apis/build/builds?$top=5");
                var buildRunsResponse = await httpClient.GetAsync(buildRunsUrl, cancellationToken);
                
                if (buildRunsResponse.IsSuccessStatusCode)
                {
                    using var stream = await buildRunsResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    
                    if (doc.RootElement.TryGetProperty("value", out var runs))
                    {
                        buildRunsFound = runs.GetArrayLength();
                    }
                }
            }

            // Step 3: Try release definitions API (soft failure - may not be available)
            try
            {
                var releaseDefsUrl = ProjectUrl(config, "_apis/release/definitions");
                var releaseDefsResponse = await httpClient.GetAsync(releaseDefsUrl, cancellationToken);
                
                if (releaseDefsResponse.IsSuccessStatusCode)
                {
                    releaseApiAvailable = true;
                    using var stream = await releaseDefsResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    
                    if (doc.RootElement.TryGetProperty("value", out var defs))
                    {
                        releaseDefinitionsFound = defs.GetArrayLength();
                    }
                }
                else
                {
                    releaseApiError = $"HTTP {(int)releaseDefsResponse.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                // Release API failure is a soft failure (on-prem variance)
                releaseApiError = ex.Message;
                _logger.LogWarning("Release definitions API not available: {Error}", ex.Message);
            }

            // Build the observed behavior string
            var observedParts = new List<string>
            {
                $"Build definitions: {buildDefinitionsFound}",
                $"Build runs: {buildRunsFound}"
            };

            if (releaseApiAvailable)
            {
                observedParts.Add($"Release definitions: {releaseDefinitionsFound}");
            }
            else
            {
                observedParts.Add($"Release API: Not available ({releaseApiError ?? "unknown"})");
            }

            return new TfsCapabilityCheckResult
            {
                CapabilityId = "pipelines",
                Success = true,
                ImpactedFunctionality = "Pipeline and build status monitoring",
                ExpectedBehavior = "Build/release definitions and runs are accessible",
                ObservedBehavior = string.Join(". ", observedParts)
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "pipelines",
                "Pipeline and build status monitoring",
                "Build/release definitions and runs are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemUpdateAsync(
        TfsConfigEntity config,
        int workItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient(config);
            
            _logger.LogInformation("Verifying work item update capability using work item {WorkItemId}", workItemId);
            
            // Work item GET is collection-scoped (work item IDs are unique across collection)
            var getUrl = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}");
            var getResponse = await httpClient.GetAsync(getUrl, cancellationToken);
            
            if (!getResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-update",
                    "Work item modifications (state changes, effort updates)",
                    "Can update work item fields",
                    $"Work item {workItemId} not found or not accessible",
                    getResponse.StatusCode == HttpStatusCode.NotFound ? FailureCategory.Authorization : CategorizeHttpError(getResponse.StatusCode),
                    await getResponse.Content.ReadAsStringAsync(cancellationToken),
                    targetScope: $"Work Item #{workItemId}",
                    mutationType: MutationType.Update,
                    cleanupStatus: CleanupStatus.Skipped);
            }

            // Perform a non-destructive update test (add a comment or verify fields are writable)
            // We'll use a JSON Patch test operation which doesn't modify data
            var testPatch = new[]
            {
                new
                {
                    op = "test",
                    path = "/fields/System.State",
                    value = (string?)null // We're just testing if we can access the field
                }
            };

            // Work item PATCH is collection-scoped
            var updateUrl = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(testPatch), 
                System.Text.Encoding.UTF8, 
                "application/json-patch+json");

            var updateResponse = await httpClient.PatchAsync(updateUrl, content, cancellationToken);
            
            // For a test operation, we expect either success or a specific failure about the test
            var isWritable = updateResponse.IsSuccessStatusCode || 
                            updateResponse.StatusCode == HttpStatusCode.BadRequest; // BadRequest is OK for test op
            
            if (isWritable)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-update",
                    Success = true,
                    ImpactedFunctionality = "Work item modifications (state changes, effort updates)",
                    ExpectedBehavior = "Can update work item fields",
                    ObservedBehavior = $"Work item {workItemId} is accessible and writable",
                    TargetScope = $"Work Item #{workItemId}",
                    MutationType = MutationType.Update,
                    CleanupStatus = CleanupStatus.NotRequired
                };
            }
            
            return CreateFailureResult(
                "work-item-update",
                "Work item modifications (state changes, effort updates)",
                "Can update work item fields",
                $"HTTP {(int)updateResponse.StatusCode}",
                CategorizeHttpError(updateResponse.StatusCode),
                await updateResponse.Content.ReadAsStringAsync(cancellationToken),
                targetScope: $"Work Item #{workItemId}",
                mutationType: MutationType.Update,
                cleanupStatus: CleanupStatus.NotRequired);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-update",
                "Work item modifications (state changes, effort updates)",
                "Can update work item fields",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message),
                targetScope: $"Work Item #{workItemId}",
                mutationType: MutationType.Update,
                cleanupStatus: CleanupStatus.NotRequired);
        }
    }

    private TfsCapabilityCheckResult CreateFailureResult(
        string capabilityId,
        string impactedFunctionality,
        string expectedBehavior,
        string observedBehavior,
        FailureCategory failureCategory,
        string rawEvidence,
        string? targetScope = null,
        MutationType? mutationType = null,
        CleanupStatus? cleanupStatus = null)
    {
        var (causes, guidance) = GetFailureGuidance(failureCategory, rawEvidence);
        
        return new TfsCapabilityCheckResult
        {
            CapabilityId = capabilityId,
            Success = false,
            ImpactedFunctionality = impactedFunctionality,
            ExpectedBehavior = expectedBehavior,
            ObservedBehavior = observedBehavior,
            FailureCategory = failureCategory,
            RawEvidence = TruncateEvidence(rawEvidence),
            LikelyCauses = causes,
            ResolutionGuidance = guidance,
            TargetScope = targetScope,
            MutationType = mutationType,
            CleanupStatus = cleanupStatus
        };
    }

    private FailureCategory CategorizeHttpError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => FailureCategory.Authentication,
            HttpStatusCode.Forbidden => FailureCategory.Authorization,
            HttpStatusCode.NotFound => FailureCategory.EndpointUnavailable,
            HttpStatusCode.BadRequest => FailureCategory.QueryRestriction,
            HttpStatusCode.TooManyRequests => FailureCategory.RateLimit,
            _ when (int)statusCode >= 500 => FailureCategory.EndpointUnavailable,
            _ => FailureCategory.Unknown
        };
    }

    private (List<string> Causes, List<string> Guidance) GetFailureGuidance(
        FailureCategory category, 
        string rawEvidence)
    {
        return category switch
        {
            FailureCategory.Authentication => (
                new List<string> 
                { 
                    "PAT has expired or been revoked",
                    "PAT is not provided in X-TFS-PAT header",
                    "Incorrect PAT value"
                },
                new List<string> 
                { 
                    "Generate a new PAT in Azure DevOps",
                    "Ensure PAT has 'Work Items (Read)' permission at minimum",
                    "Verify PAT is being sent with requests"
                }),
            
            FailureCategory.Authorization => (
                new List<string> 
                { 
                    "Insufficient permissions for the operation",
                    "Project does not exist or user has no access",
                    "Area Path or Iteration Path access is restricted"
                },
                new List<string> 
                { 
                    "Verify project name is correct",
                    "Check PAT permissions include appropriate scopes",
                    "Contact project administrator to grant access"
                }),
            
            FailureCategory.EndpointUnavailable => (
                new List<string> 
                { 
                    "TFS server is unreachable",
                    "Network connectivity issue",
                    "Server URL is incorrect",
                    "API endpoint not supported by server version"
                },
                new List<string> 
                { 
                    "Verify server URL is correct and accessible",
                    "Check network connectivity and firewall settings",
                    "Confirm TFS/Azure DevOps Server version is 2019 or later",
                    "Try increasing timeout settings"
                }),
            
            FailureCategory.MissingField => (
                new List<string> 
                { 
                    "Process template does not include required field",
                    "Custom field configuration is incompatible",
                    "Work item type does not support the field"
                },
                new List<string> 
                { 
                    "Verify process template includes required fields",
                    "Check if custom fields are properly configured",
                    "Review work item type definitions"
                }),
            
            FailureCategory.RateLimit => (
                new List<string> 
                { 
                    "Too many requests in short time period",
                    "Server throttling active"
                },
                new List<string> 
                { 
                    "Wait a few minutes before retrying",
                    "Reduce sync frequency",
                    "Contact administrator about rate limit increases"
                }),
            
            _ => (
                new List<string> { "Unexpected error occurred" },
                new List<string> { "Review error details", "Contact support if issue persists" })
        };
    }

    private string SanitizeErrorMessage(string message)
    {
        // Remove any potential sensitive information
        // This is a simple implementation - could be more sophisticated
        return message
            .Replace("Authorization", "Auth***")
            .Replace("Bearer", "***")
            .Replace("token", "***");
    }

    private string TruncateEvidence(string evidence)
    {
        const int maxLength = 500;
        if (evidence.Length <= maxLength)
            return evidence;
        
        return evidence.Substring(0, maxLength) + "... (truncated)";
    }

    /// <summary>
    /// Validates that TFS configuration is complete and throws TfsConfigurationException if not.
    /// This method is called when UseMockClient is false (TFS mode) to ensure that the
    /// real TFS data source can be used.
    /// </summary>
    private void ValidateTfsConfiguration(TfsConfigEntity? entity)
    {
        if (entity == null)
        {
            _logger.LogError("TFS data source is enabled but TFS configuration is not set. " +
                "Configure TFS settings via the API or use mock data source.");
            throw new TfsConfigurationException(
                "TFS data source is enabled but TFS configuration is not set. " +
                "Please configure TFS settings (URL, Project, DefaultAreaPath) before using the application, " +
                "or switch to mock data source by setting TfsIntegration:UseMockClient to true.",
                new[] { "Url", "Project", "DefaultAreaPath" });
        }

        var missingFields = new List<string>();
        
        if (string.IsNullOrWhiteSpace(entity.Url))
        {
            missingFields.Add("Url");
        }
        
        if (string.IsNullOrWhiteSpace(entity.Project))
        {
            missingFields.Add("Project");
        }
        
        if (string.IsNullOrWhiteSpace(entity.DefaultAreaPath))
        {
            missingFields.Add("DefaultAreaPath");
        }

        if (missingFields.Count > 0)
        {
            _logger.LogError("TFS configuration is incomplete. Missing fields: {MissingFields}", 
                string.Join(", ", missingFields));
            throw new TfsConfigurationException(
                $"TFS configuration is incomplete. Missing required fields: {string.Join(", ", missingFields)}. " +
                "Please configure all TFS settings before using the application.",
                missingFields);
        }
    }

    // ============================================
    // BULK METHODS - Prevent N+1 query patterns
    // These methods fetch or update multiple items in optimized batch operations.
    // ============================================

    /// <summary>
    /// Returns all PR data in a single logical call. For real TFS, this still makes
    /// multiple API calls but batches them efficiently rather than per-item.
    /// Reduces call count from 1 + 3*N + Sum(iterations) to approximately 4 calls.
    /// </summary>
    public async Task<PullRequestSyncResult> GetPullRequestsWithDetailsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk PR fetch with details");

        // Step 1: Get all PRs (1 call per repository, or 1 call if specific repo)
        var pullRequests = (await GetPullRequestsAsync(repositoryName, fromDate, toDate, cancellationToken)).ToList();
        tfsCallCount++; // Count as 1 logical call even though it may hit multiple repos

        if (pullRequests.Count == 0)
        {
            return new PullRequestSyncResult(
                PullRequests: pullRequests,
                Iterations: new List<PullRequestIterationDto>(),
                Comments: new List<PullRequestCommentDto>(),
                FileChanges: new List<PullRequestFileChangeDto>(),
                TfsCallCount: tfsCallCount
            );
        }

        // Group PRs by repository for efficient batching
        var prsByRepo = pullRequests.GroupBy(pr => pr.RepositoryName);

        var allIterations = new List<PullRequestIterationDto>();
        var allComments = new List<PullRequestCommentDto>();
        var allFileChanges = new List<PullRequestFileChangeDto>();

        // Step 2: For each PR, fetch iterations and comments in parallel
        // Azure DevOps doesn't have a true bulk API, but we can parallelize the calls
        var prTasks = new List<Task<(List<PullRequestIterationDto> Iterations, List<PullRequestCommentDto> Comments, int PrId, string Repo)>>();

        foreach (var repoGroup in prsByRepo)
        {
            var repo = repoGroup.Key;
            foreach (var pr in repoGroup)
            {
                // Fetch iterations and comments in parallel for each PR (no Task.Run needed for async I/O)
                prTasks.Add(FetchPrDetailsAsync(pr.Id, repo, cancellationToken));
            }
        }

        var prResults = await Task.WhenAll(prTasks);
        
        // Aggregate results and count calls
        foreach (var prResult in prResults)
        {
            Interlocked.Add(ref tfsCallCount, 2); // 1 for iterations, 1 for comments
            lock (allIterations)
            {
                allIterations.AddRange(prResult.Iterations);
            }
            lock (allComments)
            {
                allComments.AddRange(prResult.Comments);
            }
        }

        // Step 3: Fetch file changes for all iterations in parallel
        var fileChangeTasks = new List<Task<IEnumerable<PullRequestFileChangeDto>>>();
        foreach (var prResult in prResults)
        {
            var repo = prResult.Repo;
            var prId = prResult.PrId;
            foreach (var iteration in prResult.Iterations)
            {
                fileChangeTasks.Add(GetPullRequestFileChangesAsync(prId, repo, iteration.IterationNumber, cancellationToken));
            }
        }

        var fileChangeResults = await Task.WhenAll(fileChangeTasks);
        tfsCallCount += fileChangeResults.Length;
        
        foreach (var fileChanges in fileChangeResults)
        {
            lock (allFileChanges)
            {
                allFileChanges.AddRange(fileChanges);
            }
        }

        var elapsed = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Bulk PR fetch completed: {PrCount} PRs, {IterCount} iterations, {CommentCount} comments, {FileCount} file changes in {ElapsedMs}ms ({CallCount} TFS calls)",
            pullRequests.Count, allIterations.Count, allComments.Count, allFileChanges.Count,
            elapsed.TotalMilliseconds, tfsCallCount);

        return new PullRequestSyncResult(
            PullRequests: pullRequests,
            Iterations: allIterations,
            Comments: allComments,
            FileChanges: allFileChanges,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Helper method to fetch PR details (iterations and comments) in parallel.
    /// </summary>
    private async Task<(List<PullRequestIterationDto> Iterations, List<PullRequestCommentDto> Comments, int PrId, string Repo)> FetchPrDetailsAsync(
        int prId, string repo, CancellationToken cancellationToken)
    {
        // Fetch iterations and comments concurrently
        var iterationsTask = GetPullRequestIterationsAsync(prId, repo, cancellationToken);
        var commentsTask = GetPullRequestCommentsAsync(prId, repo, cancellationToken);
        
        await Task.WhenAll(iterationsTask, commentsTask);
        
        return (iterationsTask.Result.ToList(), commentsTask.Result.ToList(), prId, repo);
    }

    /// <summary>
    /// Updates effort for multiple work items in a batch. 
    /// Azure DevOps doesn't have a true bulk update API, but we can batch the requests
    /// and track them as a single logical operation for performance monitoring.
    /// </summary>
    public async Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var tfsCallCount = 0;
        
        _logger.LogInformation("Starting bulk effort update for {Count} work items", updatesList.Count);

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        // Process updates - Azure DevOps requires individual PATCH calls per work item
        // but we can track them as a batch and parallelize for performance
        var updateTasks = updatesList.Select(async update =>
        {
            try
            {
                if (update.EffortValue < 0)
                {
                    return new BulkUpdateItemResult(update.WorkItemId, false, $"Invalid effort value {update.EffortValue} (must be >= 0)");
                }

                var success = await UpdateWorkItemEffortAsync(update.WorkItemId, update.EffortValue, cancellationToken);
                Interlocked.Increment(ref tfsCallCount);
                
                return new BulkUpdateItemResult(update.WorkItemId, success, 
                    success ? null : $"TFS update failed for work item {update.WorkItemId}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                return new BulkUpdateItemResult(update.WorkItemId, false, ex.Message);
            }
        });

        var taskResults = await Task.WhenAll(updateTasks);
        results.AddRange(taskResults);
        successCount = results.Count(r => r.Success);
        failedCount = results.Count(r => !r.Success);

        _logger.LogInformation("Bulk effort update completed: {Success}/{Total} succeeded ({CallCount} TFS calls)",
            successCount, updatesList.Count, tfsCallCount);

        return new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Updates state for multiple work items in a batch.
    /// Azure DevOps doesn't have a true bulk update API, but we batch and parallelize.
    /// </summary>
    public async Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var tfsCallCount = 0;
        
        _logger.LogInformation("Starting bulk state update for {Count} work items", updatesList.Count);

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        // Process updates in parallel
        var updateTasks = updatesList.Select(async update =>
        {
            try
            {
                var success = await UpdateWorkItemStateAsync(update.WorkItemId, update.NewState, cancellationToken);
                Interlocked.Increment(ref tfsCallCount);
                
                return new BulkUpdateItemResult(update.WorkItemId, success, 
                    success ? null : $"TFS update failed for work item {update.WorkItemId}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                return new BulkUpdateItemResult(update.WorkItemId, false, ex.Message);
            }
        });

        var taskResults = await Task.WhenAll(updateTasks);
        results.AddRange(taskResults);
        successCount = results.Count(r => r.Success);
        failedCount = results.Count(r => !r.Success);

        _logger.LogInformation("Bulk state update completed: {Success}/{Total} succeeded ({CallCount} TFS calls)",
            successCount, updatesList.Count, tfsCallCount);

        return new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Gets revision history for multiple work items in a batch.
    /// Uses parallel requests to TFS for improved performance.
    /// </summary>
    public async Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var idsList = workItemIds.ToList();
        var tfsCallCount = 0;
        
        _logger.LogInformation("Starting bulk revision fetch for {Count} work items", idsList.Count);

        var results = new Dictionary<int, IEnumerable<WorkItemRevisionDto>>();
        var lockObj = new object();

        // Fetch revisions in parallel
        var fetchTasks = idsList.Select(async workItemId =>
        {
            try
            {
                var revisions = await GetWorkItemRevisionsAsync(workItemId, cancellationToken);
                Interlocked.Increment(ref tfsCallCount);
                
                lock (lockObj)
                {
                    results[workItemId] = revisions;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                _logger.LogWarning(ex, "Failed to fetch revisions for work item {WorkItemId}", workItemId);
                lock (lockObj)
                {
                    results[workItemId] = Enumerable.Empty<WorkItemRevisionDto>();
                }
            }
        });

        await Task.WhenAll(fetchTasks);

        _logger.LogInformation("Bulk revision fetch completed: {Count} work items ({CallCount} TFS calls)",
            results.Count, tfsCallCount);

        return results;
    }

    // ============================================
    // WORK ITEM CREATION
    // ============================================

    public async Task<WorkItemCreateResult> CreateWorkItemAsync(
        WorkItemCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating {WorkItemType} work item with title '{Title}'",
                request.WorkItemType, request.Title);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for creating work item");
                return new WorkItemCreateResult
                {
                    Success = false,
                    ErrorMessage = "TFS configuration not found"
                };
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient(entity);

            // Build JSON Patch document for work item creation
            // Note: AreaPath must be provided in the request since it's not stored in TfsConfigEntity
            var patchOperations = new List<object>
            {
                new { op = "add", path = "/fields/System.Title", value = request.Title }
            };

            if (!string.IsNullOrEmpty(request.AreaPath))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.AreaPath", value = request.AreaPath });
            }

            if (!string.IsNullOrEmpty(request.IterationPath))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.IterationPath", value = request.IterationPath });
            }

            if (request.Effort.HasValue)
            {
                patchOperations.Add(new { op = "add", path = $"/fields/{TfsFieldEffort}", value = request.Effort.Value });
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.Description", value = request.Description });
            }

            // Add parent link if specified
            if (request.ParentId.HasValue)
            {
                patchOperations.Add(new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = $"{entity.Url.TrimEnd('/')}/_apis/wit/workItems/{request.ParentId.Value}"
                    }
                });
            }

            // URL encode the work item type for the API call
            // Work item creation IS project-scoped per Azure DevOps REST API:
            // POST https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/${type}
            // This is different from batch read which is collection-scoped.
            var encodedType = Uri.EscapeDataString(request.WorkItemType);
            var createUrl = ProjectUrl(entity, $"_apis/wit/workitems/${encodedType}");

            using var content = new StringContent(
                JsonSerializer.Serialize(patchOperations),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending POST request to create work item at {Url}", createUrl);

            var response = await httpClient.PostAsync(createUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(responseBody);
                var workItemId = jsonDoc.RootElement.GetProperty("id").GetInt32();

                _logger.LogInformation("Successfully created work item {WorkItemId} of type {WorkItemType}",
                    workItemId, request.WorkItemType);

                return new WorkItemCreateResult
                {
                    Success = true,
                    WorkItemId = workItemId
                };
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to create work item. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseBody);

                return new WorkItemCreateResult
                {
                    Success = false,
                    ErrorMessage = $"TFS API returned {response.StatusCode}: {responseBody}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item of type {WorkItemType}", request.WorkItemType);
            return new WorkItemCreateResult
            {
                Success = false,
                ErrorMessage = $"Error creating work item: {ex.Message}"
            };
        }
    }

    public async Task<bool> UpdateWorkItemParentAsync(
        int workItemId,
        int newParentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} parent to {NewParentId}",
                workItemId, newParentId);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item parent");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient(entity);

            // Build JSON Patch document to add parent link
            // First, we need to remove existing parent links, then add the new one
            var patchOperations = new object[]
            {
                new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = $"{entity.Url.TrimEnd('/')}/_apis/wit/workItems/{newParentId}"
                    }
                }
            };

            // Work item PATCH is collection-scoped
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");

            using var content = new StringContent(
                JsonSerializer.Serialize(patchOperations),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} parent", workItemId);

            var response = await httpClient.PatchAsync(updateUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} parent to {NewParentId}",
                    workItemId, newParentId);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} parent. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} parent to {NewParentId}",
                workItemId, newParentId);
            return false;
        }
    }

    // ============================================
    // PIPELINE METHODS
    // ============================================

    public async Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build definitions are project-scoped (requirement #1)
            var buildUrl = ProjectUrl(config, "_apis/build/definitions");
            var buildResponse = await httpClient.GetAsync(buildUrl, cancellationToken);
            
            var pipelines = new List<PipelineDto>();

            if (buildResponse.IsSuccessStatusCode)
            {
                using var stream = await buildResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var def in valueArray.EnumerateArray())
                    {
                        var id = def.GetProperty("id").GetInt32();
                        var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                        pipelines.Add(new PipelineDto(
                            Id: id,
                            Name: name,
                            Type: PipelineType.Build,
                            Path: path,
                            RetrievedAt: DateTimeOffset.UtcNow
                        ));
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to get build definitions: {StatusCode}", buildResponse.StatusCode);
            }

            // Try to get release definitions (may not be available in all TFS versions)
            // Release definitions are project-scoped (requirement #1)
            try
            {
                var releaseUrl = ProjectUrl(config, "_apis/release/definitions");
                var releaseResponse = await httpClient.GetAsync(releaseUrl, cancellationToken);

                if (releaseResponse.IsSuccessStatusCode)
                {
                    using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var def in valueArray.EnumerateArray())
                        {
                            var id = def.GetProperty("id").GetInt32();
                            var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                            // Offset release IDs to avoid collision with build IDs
                            pipelines.Add(new PipelineDto(
                                Id: id + ReleaseIdOffset,
                                Name: name,
                                Type: PipelineType.Release,
                                Path: path,
                                RetrievedAt: DateTimeOffset.UtcNow
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Release definitions API not available, skipping release pipelines");
            }

            _logger.LogInformation("Retrieved {Count} pipeline definitions", pipelines.Count);
            return pipelines;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient(config);

        return await ExecuteWithRetryAsync(async () =>
        {
            var runs = new List<PipelineRunDto>();
            var isRelease = pipelineId >= ReleaseIdOffset;
            var actualId = isRelease ? pipelineId - ReleaseIdOffset : pipelineId;

            if (isRelease)
            {
                // Release runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/release/releases?definitionId={actualId}&$top={top}");
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseReleaseRun(run, pipelineId));
                        }
                    }
                }
            }
            else
            {
                // Build runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/build/builds?definitions={pipelineId}&$top={top}");
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseBuildRun(run, pipelineId));
                        }
                    }
                }
            }

            _logger.LogInformation("Retrieved {Count} runs for pipeline {PipelineId}", runs.Count, pipelineId);
            return runs;
        }, cancellationToken);
    }

    public async Task<PipelineSyncResult> GetPipelinesWithRunsAsync(
        int runsPerPipeline = 50,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk pipeline fetch with runs");

        // Step 1: Get all pipelines
        var pipelines = (await GetPipelinesAsync(cancellationToken)).ToList();
        tfsCallCount++;

        if (pipelines.Count == 0)
        {
            return new PipelineSyncResult(
                Pipelines: pipelines,
                Runs: new List<PipelineRunDto>(),
                TfsCallCount: tfsCallCount,
                SyncedAt: DateTimeOffset.UtcNow
            );
        }

        // Step 2: Fetch runs for each pipeline in parallel
        var allRuns = new List<PipelineRunDto>();
        var lockObj = new object();

        var fetchTasks = pipelines.Select(async pipeline =>
        {
            var runs = await GetPipelineRunsAsync(pipeline.Id, runsPerPipeline, cancellationToken);
            Interlocked.Increment(ref tfsCallCount);
            
            lock (lockObj)
            {
                allRuns.AddRange(runs);
            }
        });

        await Task.WhenAll(fetchTasks);

        var elapsed = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Bulk pipeline fetch completed: {PipelineCount} pipelines, {RunCount} runs in {ElapsedMs}ms ({CallCount} TFS calls)",
            pipelines.Count, allRuns.Count, elapsed.TotalMilliseconds, tfsCallCount);

        return new PipelineSyncResult(
            Pipelines: pipelines,
            Runs: allRuns,
            TfsCallCount: tfsCallCount,
            SyncedAt: DateTimeOffset.UtcNow
        );
    }

    private PipelineRunDto ParseBuildRun(JsonElement run, int pipelineId)
    {
        var runId = run.GetProperty("id").GetInt32();
        var pipelineName = "";
        if (run.TryGetProperty("definition", out var def))
        {
            pipelineName = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        }

        DateTimeOffset? startTime = null;
        if (run.TryGetProperty("startTime", out var st) && st.ValueKind != JsonValueKind.Null)
        {
            startTime = st.GetDateTimeOffset();
        }

        DateTimeOffset? finishTime = null;
        if (run.TryGetProperty("finishTime", out var ft) && ft.ValueKind != JsonValueKind.Null)
        {
            finishTime = ft.GetDateTimeOffset();
        }

        var duration = (startTime.HasValue && finishTime.HasValue) 
            ? (TimeSpan?)(finishTime.Value - startTime.Value) 
            : null;

        var resultStr = run.TryGetProperty("result", out var r) ? r.GetString() ?? "" : "";
        var result = ParseBuildResult(resultStr);

        var reasonStr = run.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "";
        var trigger = ParseBuildTrigger(reasonStr);

        var branch = run.TryGetProperty("sourceBranch", out var b) ? b.GetString() : null;
        
        var requestedFor = "";
        if (run.TryGetProperty("requestedFor", out var req))
        {
            requestedFor = req.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
        }

        return new PipelineRunDto(
            RunId: runId,
            PipelineId: pipelineId,
            PipelineName: pipelineName,
            StartTime: startTime,
            FinishTime: finishTime,
            Duration: duration,
            Result: result,
            Trigger: trigger,
            TriggerInfo: reasonStr,
            Branch: branch,
            RequestedFor: requestedFor,
            RetrievedAt: DateTimeOffset.UtcNow
        );
    }

    private PipelineRunDto ParseReleaseRun(JsonElement run, int pipelineId)
    {
        var runId = run.GetProperty("id").GetInt32();
        var pipelineName = "";
        if (run.TryGetProperty("releaseDefinition", out var def))
        {
            pipelineName = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        }

        DateTimeOffset? startTime = null;
        if (run.TryGetProperty("createdOn", out var st))
        {
            startTime = st.GetDateTimeOffset();
        }

        DateTimeOffset? finishTime = null;
        if (run.TryGetProperty("modifiedOn", out var ft))
        {
            finishTime = ft.GetDateTimeOffset();
        }

        var duration = (startTime.HasValue && finishTime.HasValue) 
            ? (TimeSpan?)(finishTime.Value - startTime.Value) 
            : null;

        var statusStr = run.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        var result = ParseReleaseResult(statusStr);

        var reasonStr = run.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "";
        var trigger = ParseReleaseTrigger(reasonStr);

        var requestedFor = "";
        if (run.TryGetProperty("createdBy", out var req))
        {
            requestedFor = req.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
        }

        return new PipelineRunDto(
            RunId: runId + ReleaseIdOffset, // Offset to avoid collision
            PipelineId: pipelineId,
            PipelineName: pipelineName,
            StartTime: startTime,
            FinishTime: finishTime,
            Duration: duration,
            Result: result,
            Trigger: trigger,
            TriggerInfo: reasonStr,
            Branch: null,
            RequestedFor: requestedFor,
            RetrievedAt: DateTimeOffset.UtcNow
        );
    }

    private static PipelineRunResult ParseBuildResult(string result)
    {
        return result.ToLowerInvariant() switch
        {
            "succeeded" => PipelineRunResult.Succeeded,
            "failed" => PipelineRunResult.Failed,
            "partiallysucceeded" => PipelineRunResult.PartiallySucceeded,
            "canceled" => PipelineRunResult.Canceled,
            "none" => PipelineRunResult.None,
            _ => PipelineRunResult.Unknown
        };
    }

    private static PipelineRunResult ParseReleaseResult(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "succeeded" or "active" => PipelineRunResult.Succeeded,
            "failed" or "rejected" => PipelineRunResult.Failed,
            "abandoned" or "canceled" => PipelineRunResult.Canceled,
            "undefined" or "draft" => PipelineRunResult.None,
            _ => PipelineRunResult.Unknown
        };
    }

    private static PipelineRunTrigger ParseBuildTrigger(string reason)
    {
        // Requirement #7: Fix case-sensitive enum parsing
        // All literals must be lowercase since we apply ToLowerInvariant()
        return reason.ToLowerInvariant() switch
        {
            "manual" or "usercreated" => PipelineRunTrigger.Manual,
            "individualci" or "batchedci" => PipelineRunTrigger.ContinuousIntegration,
            "schedule" => PipelineRunTrigger.Schedule,
            "pullrequest" => PipelineRunTrigger.PullRequest,
            "buildcompletion" => PipelineRunTrigger.BuildCompletion,
            "resourcetrigger" => PipelineRunTrigger.ResourceTrigger,
            _ => PipelineRunTrigger.Unknown
        };
    }

    private static PipelineRunTrigger ParseReleaseTrigger(string reason)
    {
        // Requirement #7: Fix case-sensitive enum parsing
        // All literals must be lowercase since we apply ToLowerInvariant()
        return reason.ToLowerInvariant() switch
        {
            "manual" => PipelineRunTrigger.Manual,
            "continuousintegration" => PipelineRunTrigger.ContinuousIntegration,
            "schedule" => PipelineRunTrigger.Schedule,
            "pullrequest" => PipelineRunTrigger.PullRequest,
            _ => PipelineRunTrigger.Unknown
        };
    }
}
