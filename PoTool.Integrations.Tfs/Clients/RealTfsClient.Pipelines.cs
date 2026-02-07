using System.Net;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Shared.Pipelines;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
    public async Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build definitions are project-scoped (requirement #1)
            var pipelines = new List<PipelineDto>();
            var buildUrl = ProjectUrl(config, "_apis/build/definitions");
            string? continuationToken = null;
            var pageUrl = buildUrl;

            do
            {
                var buildResponse = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

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

                    continuationToken = GetContinuationToken(buildResponse, doc);
                    pageUrl = AddContinuationToken(buildUrl, continuationToken);
                }
                else
                {
                    _logger.LogWarning("Failed to get build definitions: {StatusCode}", buildResponse.StatusCode);
                    break;
                }
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            // Try to get release definitions (may not be available in all TFS versions)
            // Release definitions are project-scoped (requirement #1)
            try
            {
                var releaseUrl = ProjectUrl(config, "_apis/release/definitions");
                string? releaseContinuationToken = null;
                var releasePageUrl = releaseUrl;

                do
                {
                    var releaseResponse = await SendGetAsync(httpClient, config, releasePageUrl, cancellationToken, handleErrors: false);

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

                        releaseContinuationToken = GetContinuationToken(releaseResponse, doc);
                        releasePageUrl = AddContinuationToken(releaseUrl, releaseContinuationToken);
                    }
                    else
                    {
                        break;
                    }
                } while (!string.IsNullOrWhiteSpace(releaseContinuationToken));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Release definitions API not available, skipping release pipelines");
            }

            _logger.LogInformation("Retrieved {Count} pipeline definitions", pipelines.Count);
            return pipelines;
        }, cancellationToken);
    }

    public async Task<PipelineDto?> GetPipelineByIdAsync(
        int pipelineId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Determine if this is a build or release pipeline based on ID offset
            var isRelease = pipelineId >= ReleaseIdOffset;
            var actualId = isRelease ? pipelineId - ReleaseIdOffset : pipelineId;

            if (isRelease)
            {
                // Try to get release definition by ID
                try
                {
                    var releaseUrl = ProjectUrl(config, $"_apis/release/definitions/{actualId}");
                    var releaseResponse = await SendGetAsync(httpClient, config, releaseUrl, cancellationToken, handleErrors: false);

                    if (releaseResponse.IsSuccessStatusCode)
                    {
                        using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                        var def = doc.RootElement;
                        var id = def.GetProperty("id").GetInt32();
                        var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                        return new PipelineDto(
                            Id: id + ReleaseIdOffset,
                            Name: name,
                            Type: PipelineType.Release,
                            Path: path,
                            RetrievedAt: DateTimeOffset.UtcNow
                        );
                    }
                    else if (releaseResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation("Release pipeline {PipelineId} (API ID: {ActualId}) not found: {StatusCode}", pipelineId, actualId, releaseResponse.StatusCode);
                        return null;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get release pipeline {PipelineId} (API ID: {ActualId}): {StatusCode}", pipelineId, actualId, releaseResponse.StatusCode);
                        return null;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "HTTP error retrieving release pipeline {PipelineId} (API ID: {ActualId})", pipelineId, actualId);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Release definitions API not available or error retrieving pipeline {PipelineId} (API ID: {ActualId})", pipelineId, actualId);
                    return null;
                }
            }
            else
            {
                // Get build definition by ID
                var buildUrl = ProjectUrl(config, $"_apis/build/definitions/{actualId}");
                var buildResponse = await SendGetAsync(httpClient, config, buildUrl, cancellationToken, handleErrors: false);

                if (buildResponse.IsSuccessStatusCode)
                {
                    using var stream = await buildResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    var def = doc.RootElement;
                    var id = def.GetProperty("id").GetInt32();
                    var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                    return new PipelineDto(
                        Id: id,
                        Name: name,
                        Type: PipelineType.Build,
                        Path: path,
                        RetrievedAt: DateTimeOffset.UtcNow
                    );
                }
                else if (buildResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Build pipeline {PipelineId} (API ID: {ActualId}) not found: {StatusCode}", pipelineId, actualId, buildResponse.StatusCode);
                }
                else
                {
                    _logger.LogWarning("Failed to get build pipeline {PipelineId} (API ID: {ActualId}): {StatusCode}", pipelineId, actualId, buildResponse.StatusCode);
                }
            }

            return null;
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
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var runs = new List<PipelineRunDto>();
            var isRelease = pipelineId >= ReleaseIdOffset;
            var actualId = isRelease ? pipelineId - ReleaseIdOffset : pipelineId;

            if (isRelease)
            {
                // Release runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/release/releases?definitionId={actualId}&$top={top}");
                string? continuationToken = null;
                var pageUrl = url;

                do
                {
                    var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseReleaseRun(run, pipelineId));
                        }
                    }

                    continuationToken = GetContinuationToken(response, doc);
                    pageUrl = AddContinuationToken(url, continuationToken);
                } while (!string.IsNullOrWhiteSpace(continuationToken));
            }
            else
            {
                // Build runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/build/builds?definitions={pipelineId}&$top={top}");
                string? continuationToken = null;
                var pageUrl = url;

                do
                {
                    var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseBuildRun(run, pipelineId));
                        }
                    }

                    continuationToken = GetContinuationToken(response, doc);
                    pageUrl = AddContinuationToken(url, continuationToken);
                } while (!string.IsNullOrWhiteSpace(continuationToken));
            }

            _logger.LogInformation("Retrieved {Count} runs for pipeline {PipelineId}", runs.Count, pipelineId);
            return runs;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var allRuns = new List<PipelineRunDto>();
            var pipelineIdsList = pipelineIds.ToList();

            // Separate build and release pipeline IDs
            var buildIds = pipelineIdsList.Where(id => id < ReleaseIdOffset).ToList();
            var releaseIds = pipelineIdsList.Where(id => id >= ReleaseIdOffset).Select(id => id - ReleaseIdOffset).ToList();

            // Fetch build runs with filters
            if (buildIds.Any())
            {
                var queryParams = new List<string>
                {
                    $"definitions={string.Join(",", buildIds)}",
                    $"$top={top}"
                };

                if (!string.IsNullOrEmpty(branchName))
                {
                    queryParams.Add($"branchName={Uri.EscapeDataString(branchName)}");
                }

                if (minStartTime.HasValue)
                {
                    queryParams.Add($"minTime={Uri.EscapeDataString(minStartTime.Value.ToString("o"))}");
                }

                var url = ProjectUrl(config, $"_apis/build/builds?{string.Join("&", queryParams)}");
                string? continuationToken = null;
                var pageUrl = url;

                do
                {
                    var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                    if (!response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            // Get the actual pipeline ID from the run
                            if (run.TryGetProperty("definition", out var def) && 
                                def.TryGetProperty("id", out var defId))
                            {
                                var pipelineId = defId.GetInt32();
                                allRuns.Add(ParseBuildRun(run, pipelineId));
                            }
                        }
                    }

                    continuationToken = GetContinuationToken(response, doc);
                    pageUrl = AddContinuationToken(url, continuationToken);
                } while (!string.IsNullOrWhiteSpace(continuationToken));
            }

            // Fetch release runs (branch filtering not supported for releases)
            if (releaseIds.Any())
            {
                foreach (var releaseId in releaseIds)
                {
                    var queryParams = new List<string>
                    {
                        $"definitionId={releaseId}",
                        $"$top={top}"
                    };

                    if (minStartTime.HasValue)
                    {
                        queryParams.Add($"minCreatedTime={Uri.EscapeDataString(minStartTime.Value.ToString("o"))}");
                    }

                    var url = ProjectUrl(config, $"_apis/release/releases?{string.Join("&", queryParams)}");
                    string? continuationToken = null;
                    var pageUrl = url;

                    do
                    {
                        var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                        if (!response.IsSuccessStatusCode)
                        {
                            break;
                        }

                        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                        if (doc.RootElement.TryGetProperty("value", out var valueArray))
                        {
                            foreach (var run in valueArray.EnumerateArray())
                            {
                                allRuns.Add(ParseReleaseRun(run, releaseId + ReleaseIdOffset));
                            }
                        }

                        continuationToken = GetContinuationToken(response, doc);
                        pageUrl = AddContinuationToken(url, continuationToken);
                    } while (!string.IsNullOrWhiteSpace(continuationToken));
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} runs for {PipelineCount} pipelines with filters (branch: {Branch}, minTime: {MinTime})",
                allRuns.Count, pipelineIdsList.Count, branchName ?? "none", minStartTime?.ToString("o") ?? "none");
            
            return allRuns;
        }, cancellationToken);
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


    // ============================================
    // PIPELINE DEFINITION METHODS (YAML) - API 7.0
    // ============================================

    /// <summary>
    /// Retrieves repository IDs for multiple repository names in a single API call.
    /// Prevents N+1 pattern when syncing pipeline definitions for multiple repositories.
    /// </summary>
    private async Task<Dictionary<string, string>> GetRepositoryIdsByNamesAsync(
        IEnumerable<string> repositoryNames,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var repositories = await GetRepositoriesInternalAsync(config, httpClient, null, cancellationToken);
            var repoNamesSet = new HashSet<string>(repositoryNames, StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var repo in repositories)
            {
                var name = repo.Name;
                var id = repo.Id;

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id) && repoNamesSet.Contains(name))
                {
                    result[name] = id;
                }
            }

            _logger.LogInformation("Resolved {Count} repository IDs from {Total} requested names",
                result.Count, repoNamesSet.Count);

            return result;
        }, cancellationToken);
    }

    public async Task<string?> GetRepositoryIdByNameAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var repositories = await GetRepositoriesInternalAsync(config, httpClient, repositoryName, cancellationToken);
            if (repositories.Count == 0)
            {
                _logger.LogWarning("Repository '{RepoName}' not found in project '{Project}'", repositoryName, config.Project);
                return null;
            }

            var repository = repositories[0];
            if (!string.IsNullOrEmpty(repository.Id))
            {
                _logger.LogInformation("Found repository '{RepoName}' with ID: {RepoId}", repository.Name, repository.Id);
                return repository.Id;
            }

            _logger.LogWarning("Repository '{RepoName}' not found in project '{Project}'", repositoryName, config.Project);
            return null;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var definitions = new List<PipelineDefinitionDto>();

            // Step 1: Get repository ID
            var repoId = await GetRepositoryIdByNameAsync(repositoryName, cancellationToken);
            if (string.IsNullOrEmpty(repoId))
            {
                _logger.LogWarning("Cannot fetch pipeline definitions for repository '{RepoName}' - repository ID not found", repositoryName);
                return definitions;
            }

            // Step 2: Get all build definitions with full properties
            // GET {ServerUri}/{Project}/_apis/build/definitions?api-version=7.0&includeAllProperties=true
            var url = ProjectUrl(config, "_apis/build/definitions?includeAllProperties=true");
            _logger.LogDebug("Fetching build definitions from: {Url}", url);

            var syncTime = DateTimeOffset.UtcNow;
            int processedCount = 0;
            int filteredCount = 0;
            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get build definitions: {StatusCode}", response.StatusCode);
                    return definitions;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogDebug("Build definitions response missing 'value' array");
                    return definitions;
                }

                foreach (var def in valueArray.EnumerateArray())
                {
                    processedCount++;

                    // Filter by repository: check definition.repository.id or definition.repository.name
                    if (!def.TryGetProperty("repository", out var repository))
                    {
                        _logger.LogDebug("Build definition {DefId} has no 'repository' property, skipping", 
                            def.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1);
                        continue;
                    }

                    // Preferred: match by repository.id (GUID)
                    bool matchesRepo = false;
                    if (repository.TryGetProperty("id", out var repoIdElement))
                    {
                        var defRepoId = repoIdElement.GetString();
                        if (string.Equals(defRepoId, repoId, StringComparison.OrdinalIgnoreCase))
                        {
                            matchesRepo = true;
                        }
                    }

                    // Fallback: match by repository.name
                    if (!matchesRepo && repository.TryGetProperty("name", out var repoNameElement))
                    {
                        var defRepoName = repoNameElement.GetString();
                        if (string.Equals(defRepoName, repositoryName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchesRepo = true;
                        }
                    }

                    if (!matchesRepo)
                    {
                        continue;
                    }

                    filteredCount++;

                    // Extract definition properties
                    var pipelineId = def.GetProperty("id").GetInt32();
                    var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    
                    // Extract YAML path from process.yamlFilename
                    string? yamlPath = null;
                    if (def.TryGetProperty("process", out var process))
                    {
                        if (process.TryGetProperty("yamlFilename", out var yamlFileElement))
                        {
                            var rawPath = yamlFileElement.GetString();
                            if (!string.IsNullOrEmpty(rawPath))
                            {
                                // Normalize: ensure leading /
                                yamlPath = rawPath.StartsWith("/") ? rawPath : $"/{rawPath}";
                            }
                        }
                    }

                    // Extract folder/path
                    var folder = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                    // Extract web URL
                    string? webUrl = null;
                    if (def.TryGetProperty("_links", out var links))
                    {
                        if (links.TryGetProperty("web", out var web))
                        {
                            if (web.TryGetProperty("href", out var href))
                            {
                                webUrl = href.GetString();
                            }
                        }
                    }

                    var dto = new PipelineDefinitionDto
                    {
                        PipelineDefinitionId = pipelineId,
                        RepoId = repoId,
                        RepoName = repositoryName,
                        Name = name,
                        YamlPath = yamlPath,
                        Folder = folder,
                        Url = webUrl,
                        LastSyncedUtc = syncTime
                    };

                    definitions.Add(dto);

                    _logger.LogDebug(
                        "Mapped pipeline definition: ID={Id}, Name={Name}, YamlPath={YamlPath}",
                        pipelineId, name, yamlPath ?? "(none)");
                }

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation(
                "Retrieved {Count} pipeline definitions for repository '{RepoName}' (processed {Processed}, filtered {Filtered})",
                definitions.Count, repositoryName, processedCount, filteredCount);

            return (IEnumerable<PipelineDefinitionDto>)definitions;
        }, cancellationToken);
    }
}
