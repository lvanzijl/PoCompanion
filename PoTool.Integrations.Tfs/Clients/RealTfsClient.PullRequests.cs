using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
    private const long UnknownContentLength = -1L;

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
            var totalPagesProcessed = 0;
            var nonSuccessCount = 0;
            var jsonParseFailuresCount = 0;
            var missingValueCount = 0;
            var emptyValueCount = 0;

            var fromDateText = fromDate.HasValue ? fromDate.Value.ToString("O") : "null";
            var toDateText = toDate.HasValue ? toDate.Value.ToString("O") : "null";

            _logger.LogInformation(
                "PR_INGEST_CLIENT_START: repositoryNameParameter={RepositoryNameParameter}, fromDate={FromDate}, toDate={ToDate}, status={Status}, resolvedRepositories={ResolvedRepositories}",
                repositoryName ?? "null",
                fromDateText,
                toDateText,
                "all",
                repositories.Count);

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
                    queryParams.Add($"searchCriteria.minTime={Uri.EscapeDataString(FormatUtcTimestamp(fromDate.Value))}");
                }

                if (toDate.HasValue)
                {
                    queryParams.Add($"searchCriteria.maxTime={Uri.EscapeDataString(FormatUtcTimestamp(toDate.Value))}");
                }

                // Git PRs are project-scoped (requirement #1)
                var encodedRepoName = Uri.EscapeDataString(repo.Name);
                var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests?{string.Join("&", queryParams)}");

                _logger.LogInformation(
                    "PR_INGEST_REPO_START: repoName={RepoName}, repoId={RepoId}, fromDate={FromDate}, toDate={ToDate}, initialUrl={InitialUrl}",
                    repo.Name,
                    string.IsNullOrWhiteSpace(repo.Id) ? "n/a" : repo.Id,
                    fromDateText,
                    toDateText,
                    SanitizeUrlForDiagnostics(url));

                string? continuationToken = null;
                var pageUrl = url;
                var pagesProcessedForRepo = 0;
                var prsParsedForRepo = 0;
                var lastContinuationTokenPresent = false;

                do
                {
                    pagesProcessedForRepo++;
                    totalPagesProcessed++;
                    var pageIndex = pagesProcessedForRepo;
                    var requestUrlForLog = SanitizeUrlForDiagnostics(pageUrl);

                    var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                    var headerContinuationToken = response.Headers.TryGetValues("x-ms-continuationtoken", out var headerTokens)
                        ? headerTokens.FirstOrDefault()
                        : null;
                    var headerContinuationTokenPresent = !string.IsNullOrWhiteSpace(headerContinuationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        nonSuccessCount++;
                    }

                    await HandleHttpErrorsAsync(response, cancellationToken);

                    var contentLength = response.Content.Headers.ContentLength ?? UnknownContentLength;
                    JsonDocument? doc = null;
                    var jsonParsedSuccessfully = false;
                    var hasValue = false;
                    var valueItemsCount = 0;

                    try
                    {
                        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        doc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
                        jsonParsedSuccessfully = true;

                        if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                        {
                            missingValueCount++;
                            LogPullRequestPageDiagnostics(
                                repo.Name,
                                pageIndex,
                                requestUrlForLog,
                                (int)response.StatusCode,
                                headerContinuationTokenPresent,
                                headerContinuationToken,
                                contentLength,
                                jsonParsedSuccessfully,
                                hasValue,
                                valueItemsCount);
                            break;
                        }

                        hasValue = true;
                        valueItemsCount = valueArray.GetArrayLength();
                        if (valueItemsCount == 0)
                        {
                            emptyValueCount++;
                        }

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
                            prsParsedForRepo++;
                        }

                        continuationToken = GetContinuationToken(response, doc);
                        lastContinuationTokenPresent = !string.IsNullOrWhiteSpace(continuationToken);
                        pageUrl = AddContinuationToken(url, continuationToken);

                        LogPullRequestPageDiagnostics(
                            repo.Name,
                            pageIndex,
                            requestUrlForLog,
                            (int)response.StatusCode,
                            headerContinuationTokenPresent,
                            headerContinuationToken,
                            contentLength,
                            jsonParsedSuccessfully,
                            hasValue,
                            valueItemsCount);
                    }
                    catch (JsonException)
                    {
                        jsonParseFailuresCount++;
                        LogPullRequestPageDiagnostics(
                            repo.Name,
                            pageIndex,
                            requestUrlForLog,
                            (int)response.StatusCode,
                            headerContinuationTokenPresent,
                            headerContinuationToken,
                            contentLength,
                            jsonParsedSuccessfully,
                            hasValue,
                            valueItemsCount);
                        throw;
                    }
                    finally
                    {
                        doc?.Dispose();
                    }
                } while (!string.IsNullOrWhiteSpace(continuationToken));

                _logger.LogInformation(
                    "PR_INGEST_REPO_DONE: repoName={RepoName}, pagesProcessed={PagesProcessed}, prsParsedForRepo={PrsParsedForRepo}, lastContinuationTokenPresent={LastContinuationTokenPresent}",
                    repo.Name,
                    pagesProcessedForRepo,
                    prsParsedForRepo,
                    lastContinuationTokenPresent);
            }

            // Phase 4: Performance metrics
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "PR_INGEST_RUN_SUMMARY: totalReposQueried={TotalReposQueried}, totalPagesProcessed={TotalPagesProcessed}, totalPRsParsed={TotalPRsParsed}, elapsedMs={ElapsedMs}, fromDate={FromDate}, toDate={ToDate}",
                repositories.Count,
                totalPagesProcessed,
                allPRs.Count,
                elapsed.TotalMilliseconds,
                fromDateText,
                toDateText);

            if (allPRs.Count == 0)
            {
                var observedBuckets = new List<string>();
                if (nonSuccessCount > 0)
                {
                    observedBuckets.Add("HTTP non-success");
                }

                if (jsonParseFailuresCount > 0)
                {
                    observedBuckets.Add("JSON parse failed");
                }

                if (missingValueCount > 0)
                {
                    observedBuckets.Add("Missing 'value'");
                }

                if (emptyValueCount > 0)
                {
                    observedBuckets.Add("Empty 'value'");
                }

                if (repositories.Count == 0)
                {
                    observedBuckets.Add("All repos list empty");
                }

                var bucketsText = observedBuckets.Count == 0 ? "none" : string.Join(", ", observedBuckets);
                _logger.LogWarning(
                    "PR_INGEST_ZERO_RESULT_OBSERVED: buckets=[{Buckets}]",
                    bucketsText);
            }

            return allPRs;
        }, cancellationToken);
    }

    private static string SanitizeUrlForDiagnostics(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "[invalid-url]";
        }

        return $"{uri.AbsolutePath}{uri.Query}";
    }

    private void LogPullRequestPageDiagnostics(
        string repoName,
        int pageIndex,
        string requestUrl,
        int statusCode,
        bool continuationTokenHeaderPresent,
        string? continuationTokenHeaderValue,
        long contentLengthBytes,
        bool jsonParsed,
        bool hasValue,
        int valueCount)
    {
        _logger.LogDebug(
            "PR_INGEST_REPO_PAGE: repoName={RepoName}, pageIndex={PageIndex}, requestUrl={RequestUrl}, statusCode={StatusCode}, continuationTokenHeaderPresent={ContinuationTokenHeaderPresent}, continuationTokenHeaderValue={ContinuationTokenHeaderValue}, contentLengthBytes={ContentLengthBytes}, jsonParsed={JsonParsed}, hasValue={HasValue}, valueCount={ValueCount}",
            repoName,
            pageIndex,
            requestUrl,
            statusCode,
            continuationTokenHeaderPresent,
            continuationTokenHeaderValue ?? "null",
            contentLengthBytes,
            jsonParsed,
            hasValue,
            valueCount);
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

            var iterations = new List<PullRequestIterationDto>();

            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                    break;

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

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

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

            var comments = new List<PullRequestCommentDto>();

            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                    break;

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

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

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

            var fileChanges = new List<PullRequestFileChangeDto>();

            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("changeEntries", out var changeEntries))
                    break;

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

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation("Retrieved {Count} file changes for PR {PullRequestId} iteration {IterationId}",
                fileChanges.Count, pullRequestId, iterationId);
            return fileChanges;
        }, cancellationToken);
    }

    public async Task<IEnumerable<int>> GetPullRequestWorkItemLinksAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // GET {baseUrl}/{project}/_apis/git/repositories/{repo}/pullRequests/{id}/workitems
            // Returns: { "count": N, "value": [ { "id": 123, "url": "..." }, ... ] }
            // This endpoint returns all linked work items in a single page — no pagination needed.
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config,
                $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/workitems");

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

            // Older TFS Server versions (< 2017) may not support this endpoint and return 404.
            // A 404 is not retryable (IsRetryableStatusCode only handles 5xx/408), so checking
            // before HandleHttpErrorsAsync is safe: we avoid throwing an exception for a known
            // "unsupported capability" scenario and fall back to an empty result instead.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug(
                    "PR work item links endpoint returned 404 for PR {PullRequestId} in repo {Repo}; " +
                    "server may not support this endpoint (requires TFS 2017+ or Azure DevOps)",
                    pullRequestId, repositoryName);
                return Enumerable.Empty<int>();
            }

            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var workItemIds = new List<int>();

            if (doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var workItemId))
                    {
                        workItemIds.Add(workItemId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "PR work item links response contained an entry without a valid integer 'id' for PR {PullRequestId} in repo {Repo}; skipping entry",
                            pullRequestId, repositoryName);
                    }
                }
            }

            _logger.LogDebug(
                "Retrieved {Count} work item link(s) for PR {PullRequestId} in repo {Repo}",
                workItemIds.Count, pullRequestId, repositoryName);

            return workItemIds;
        }, cancellationToken);
    }
}
