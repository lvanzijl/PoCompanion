using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
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
        var httpClient = GetAuthenticatedHttpClient();

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

                    // Determine iteration path from work items or use project root
                    var iterationPath = config.Project; // Default to project root path

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
        var httpClient = GetAuthenticatedHttpClient();

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
        var httpClient = GetAuthenticatedHttpClient();

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
        var httpClient = GetAuthenticatedHttpClient();

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
}
