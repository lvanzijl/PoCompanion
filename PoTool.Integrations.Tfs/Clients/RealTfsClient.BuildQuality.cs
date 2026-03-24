using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
    private const string BuildQualityTestRunsEndpointPath = "_apis/test/runs";
    private const string BuildQualityCoverageEndpointPath = "_apis/testresults/codecoverage";
    private const string BuildQualityTestRunsApiVersion = "7.0";
    private const string BuildQualityCoverageApiVersion = "7.0-preview";
    private const string BuildQualityTestRunsQueryShape = "buildUri";
    private const int TestRunPageSize = 200;

    public async Task<IEnumerable<TestRunDto>> GetTestRunsByBuildIdsAsync(
        IEnumerable<int> buildIds,
        CancellationToken cancellationToken = default)
    {
        var requestedBuildIds = buildIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (requestedBuildIds.Length == 0)
        {
            return [];
        }

        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;
        var httpClient = GetAuthenticatedHttpClient();
        _logger.LogInformation(
            "Requesting TFS test runs for {BuildCount} build ids via {EndpointPath} with query shape {QueryShape} and api-version {ApiVersion}.",
            requestedBuildIds.Length,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape,
            BuildQualityTestRunsApiVersion);
        var overallStopwatch = Stopwatch.StartNew();

        var results = await ExecuteWithRetryAsync(async () =>
        {
            var results = new List<TestRunDto>();
            var buildSummaries = new List<TestRunBuildRetrievalSummary>(requestedBuildIds.Length);

            _logger.LogInformation(
                "Attempting TFS test run retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

            foreach (var buildId in requestedBuildIds)
            {
                var buildSummary = await GetTestRunsForBuildAsync(config, httpClient, buildId, cancellationToken);
                buildSummaries.Add(buildSummary);
                results.AddRange(buildSummary.TestRuns);
            }

            var totalHttpRequestCount = buildSummaries.Sum(summary => summary.HttpRequestCount);
            var totalPageCount = buildSummaries.Sum(summary => summary.PagesRequested);
            var totalRawRunCount = buildSummaries.Sum(summary => summary.RawRunCount);
            var totalDtoCount = buildSummaries.Sum(summary => summary.DtoCount);
            _logger.LogInformation(
                "BUILDQUALITY_TESTRUN_REQUEST_SUMMARY: attemptedBuildCount={AttemptedBuildCount}, httpRequestCount={HttpRequestCount}, pageCount={PageCount}, rawRunCount={RawRunCount}, dtoCount={DtoCount}, elapsedMs={ElapsedMs}",
                requestedBuildIds.Length,
                totalHttpRequestCount,
                totalPageCount,
                totalRawRunCount,
                totalDtoCount,
                overallStopwatch.ElapsedMilliseconds);

            return results;
        }, cancellationToken);

        _logger.LogInformation(
            "Retrieved {ResultCount} TFS test runs for {BuildCount} build ids.",
            results.Count(),
            requestedBuildIds.Length);

        return results;
    }

    public async Task<IEnumerable<CoverageDto>> GetCoverageByBuildIdsAsync(
        IEnumerable<int> buildIds,
        CancellationToken cancellationToken = default)
    {
        var requestedBuildIds = buildIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (requestedBuildIds.Length == 0)
        {
            return [];
        }

        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;
        var httpClient = GetAuthenticatedHttpClient();
        _logger.LogInformation(
            "Requesting TFS coverage for {BuildCount} build ids via {EndpointPath} with api-version {ApiVersion}.",
            requestedBuildIds.Length,
            BuildQualityCoverageEndpointPath,
            BuildQualityCoverageApiVersion);
        var overallStopwatch = Stopwatch.StartNew();

        var coverageResults = await ExecuteWithRetryAsync(async () =>
        {
            var coverageResults = new List<CoverageDto>();
            var buildSummaries = new List<CoverageBuildRetrievalSummary>(requestedBuildIds.Length);

            _logger.LogInformation(
                "Attempting TFS coverage retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

            foreach (var batch in requestedBuildIds.Chunk(25))
            {
                var batchTasks = batch.Select(buildId => GetCoverageForBuildAsync(config, httpClient, buildId, cancellationToken));
                var batchResults = await Task.WhenAll(batchTasks);
                foreach (var result in batchResults)
                {
                    buildSummaries.Add(result);
                    coverageResults.AddRange(result.CoverageRows);
                }
            }

            var totalHttpRequestCount = buildSummaries.Sum(summary => summary.HttpRequestCount);
            var totalDtoCount = buildSummaries.Sum(summary => summary.RowCount);
            _logger.LogInformation(
                "BUILDQUALITY_COVERAGE_REQUEST_SUMMARY: attemptedBuildCount={AttemptedBuildCount}, httpRequestCount={HttpRequestCount}, dtoCount={DtoCount}, elapsedMs={ElapsedMs}",
                requestedBuildIds.Length,
                totalHttpRequestCount,
                totalDtoCount,
                overallStopwatch.ElapsedMilliseconds);

            return coverageResults;
        }, cancellationToken);

        _logger.LogInformation(
            "Retrieved {ResultCount} TFS coverage rows for {BuildCount} build ids.",
            coverageResults.Count(),
            requestedBuildIds.Length);

        return coverageResults;
    }

    private async Task<TestRunBuildRetrievalSummary> GetTestRunsForBuildAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        int buildId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Attempting TFS test run retrieval for build {BuildId} via {EndpointPath} with query shape {QueryShape}.",
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape);

        var buildStopwatch = Stopwatch.StartNew();
        var requestedBuildUri = GetTestRunBuildUri(buildId);
        var skip = 0;
        var rawRunCount = 0;
        var parsedRunCount = 0;
        var pageCount = 0;
        var testRuns = new List<TestRunDto>();

        while (true)
        {
            var url = ProjectUrlWithApiVersionOverride(
                config,
                $"{BuildQualityTestRunsEndpointPath}?buildUri={Uri.EscapeDataString(requestedBuildUri)}" +
                $"&$top={TestRunPageSize}&$skip={skip}",
                BuildQualityTestRunsApiVersion);
            _logger.LogDebug(
                "Requesting TFS test runs page for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, query shape {QueryShape}, api-version {ApiVersion}).",
                buildId,
                url,
                BuildQualityTestRunsEndpointPath,
                BuildQualityTestRunsQueryShape,
                BuildQualityTestRunsApiVersion);

            pageCount++;
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TFS test runs endpoint failed for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, query shape {QueryShape}, api-version {ApiVersion}) with status code {StatusCode}.",
                    buildId,
                    url,
                    BuildQualityTestRunsEndpointPath,
                    BuildQualityTestRunsQueryShape,
                    BuildQualityTestRunsApiVersion,
                    (int)response.StatusCode);
                await HandleHttpErrorsAsync(response, cancellationToken);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var pageRetrievedCount = 0;
            foreach (var run in EnumerateTestRunElements(doc.RootElement))
            {
                pageRetrievedCount++;
                var hasBuildId = TryGetBuildIdFromTestRun(run, out _);
                var hasBuildUri = TryGetBuildUriFromTestRun(run, out _);
                if (!MatchesRequestedBuild(run, buildId, requestedBuildUri))
                {
                    _logger.LogDebug(
                        "Skipping TFS test run for requested build {BuildId} because the payload build linkage does not match (hasBuildId={HasBuildId}, hasBuildUri={HasBuildUri}).",
                        buildId,
                        hasBuildId,
                        hasBuildUri);
                    continue;
                }

                var dto = ParseTestRunDto(run, buildId);
                if (dto is not null)
                {
                    testRuns.Add(dto);
                    parsedRunCount++;
                }
            }

            rawRunCount += pageRetrievedCount;

            if (pageRetrievedCount == 0)
            {
                break;
            }

            skip += TestRunPageSize;
        }

        _logger.LogInformation(
            "Retrieved {RetrievedCount} raw TFS test run elements for build {BuildId} via {EndpointPath} with query shape {QueryShape}.",
            rawRunCount,
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape);
        _logger.LogInformation(
            "Parsed {ParsedCount}/{RetrievedCount} TFS test runs for build {BuildId} via {EndpointPath} with query shape {QueryShape}.",
            parsedRunCount,
            rawRunCount,
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape);
        var httpRequestCount = pageCount;
        _logger.LogInformation(
            "BUILDQUALITY_TESTRUN_BUILD_SUMMARY: buildId={BuildId}, endpointPath={EndpointPath}, queryShape={QueryShape}, pageCount={PageCount}, httpRequestCount={HttpRequestCount}, rawRunCount={RawRunCount}, dtoCount={DtoCount}, elapsedMs={ElapsedMs}",
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape,
            pageCount,
            httpRequestCount,
            rawRunCount,
            parsedRunCount,
            buildStopwatch.ElapsedMilliseconds);

        return new TestRunBuildRetrievalSummary(testRuns, pageCount, httpRequestCount, rawRunCount, parsedRunCount);
    }

    private async Task<CoverageBuildRetrievalSummary> GetCoverageForBuildAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        int buildId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Attempting TFS coverage retrieval for build {BuildId}.",
            buildId);
        var buildStopwatch = Stopwatch.StartNew();

        var url = ProjectUrlWithApiVersionOverride(
            config,
            $"{BuildQualityCoverageEndpointPath}?buildId={buildId}",
            BuildQualityCoverageApiVersion);
        var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "TFS coverage endpoint returned 404 for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, api-version {ApiVersion}).",
                buildId,
                url,
                BuildQualityCoverageEndpointPath,
                BuildQualityCoverageApiVersion);
            _logger.LogInformation(
                "BUILDQUALITY_COVERAGE_BUILD_SUMMARY: buildId={BuildId}, httpRequestCount={HttpRequestCount}, rowCount={RowCount}, elapsedMs={ElapsedMs}",
                buildId,
                1,
                0,
                buildStopwatch.ElapsedMilliseconds);
            return new CoverageBuildRetrievalSummary([], 1, 0);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "TFS coverage endpoint failed for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, api-version {ApiVersion}) with status code {StatusCode}.",
                buildId,
                url,
                BuildQualityCoverageEndpointPath,
                BuildQualityCoverageApiVersion,
                (int)response.StatusCode);
            await HandleHttpErrorsAsync(response, cancellationToken);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var results = ParseCoverageDtos(doc.RootElement, buildId).ToList();
        _logger.LogInformation(
            "Retrieved {ResultCount} TFS coverage rows for build {BuildId} via {EndpointPath} with api-version {ApiVersion}.",
            results.Count,
            buildId,
            BuildQualityCoverageEndpointPath,
            BuildQualityCoverageApiVersion);
        _logger.LogInformation(
            "BUILDQUALITY_COVERAGE_BUILD_SUMMARY: buildId={BuildId}, httpRequestCount={HttpRequestCount}, rowCount={RowCount}, elapsedMs={ElapsedMs}",
            buildId,
            1,
            results.Count,
            buildStopwatch.ElapsedMilliseconds);
        return new CoverageBuildRetrievalSummary(results, 1, results.Count);
    }

    private IEnumerable<JsonElement> EnumerateTestRunElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (root.TryGetProperty("value", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in valueArray.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    private string ProjectUrlWithApiVersionOverride(
        TfsConfigEntity config,
        string relativePath,
        string apiVersion)
    {
        ValidateCollectionUrl(config.Url);
        var encodedProject = Uri.EscapeDataString(config.Project);
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{encodedProject}/{path}{separator}api-version={Uri.EscapeDataString(apiVersion)}";
    }

    private static string GetTestRunBuildUri(int buildId)
    {
        return $"vstfs:///Build/Build/{buildId}";
    }

    private TestRunDto? ParseTestRunDto(JsonElement run, int requestedBuildId)
    {
        // TFS test run responses retrieved per build may omit run.build.id, so the
        // current requested build context is the authoritative fallback for linkage.
        var buildId = TryGetBuildIdFromTestRun(run, out var resolvedBuildId)
            ? resolvedBuildId
            : requestedBuildId;

        if (!TryGetIntProperty(run, "totalTests", out var totalTests) ||
            !TryGetIntProperty(run, "passedTests", out var passedTests) ||
            !TryGetIntProperty(run, "notApplicableTests", out var notApplicableTests))
        {
            _logger.LogWarning(
                "Skipping TFS test run payload for build {BuildId} because one or more required raw counters are missing.",
                buildId);
            return null;
        }

        _ = TryGetIntProperty(run, "id", out var externalId);

        return new TestRunDto
        {
            BuildId = buildId,
            ExternalId = externalId,
            TotalTests = totalTests,
            PassedTests = passedTests,
            NotApplicableTests = notApplicableTests,
            Timestamp = TryGetDateTimeOffsetProperty(run, "completedDate")
        };
    }

    private IEnumerable<CoverageDto> ParseCoverageDtos(JsonElement summary, int buildId)
    {
        if (!summary.TryGetProperty("coverageData", out var coverageData) ||
            coverageData.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var coverageEntry in coverageData.EnumerateArray())
        {
            if (!coverageEntry.TryGetProperty("coverageStats", out var coverageStats) ||
                coverageStats.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            JsonElement? linesStat = null;
            foreach (var coverageStat in coverageStats.EnumerateArray())
            {
                if (!coverageStat.TryGetProperty("label", out var labelElement))
                {
                    continue;
                }

                var label = labelElement.GetString();
                // UNCERTAIN: public Azure DevOps contracts expose label-based coverage stats rather than
                // dedicated CoveredLines/TotalLines fields. Public consumers check for both "Line" and
                // "Lines", so ingestion keeps that explicit source mapping here instead of inferring from formulas.
                if (string.Equals(label, "Line", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(label, "Lines", StringComparison.OrdinalIgnoreCase))
                {
                    linesStat = coverageStat;
                    break;
                }
            }

            if (!linesStat.HasValue ||
                !TryGetIntProperty(linesStat.Value, "covered", out var coveredLines) ||
                !TryGetIntProperty(linesStat.Value, "total", out var totalLines))
            {
                continue;
            }

            yield return new CoverageDto
            {
                BuildId = buildId,
                CoveredLines = coveredLines,
                TotalLines = totalLines
            };
        }
    }

    private bool TryGetBuildIdFromTestRun(JsonElement run, out int buildId)
    {
        buildId = default;
        if (!run.TryGetProperty("build", out var buildReference) ||
            !buildReference.TryGetProperty("id", out var buildIdElement))
        {
            return false;
        }

        return buildIdElement.ValueKind switch
        {
            JsonValueKind.Number => buildIdElement.TryGetInt32(out buildId),
            JsonValueKind.String => int.TryParse(buildIdElement.GetString(), out buildId),
            _ => false
        };
    }

    private bool MatchesRequestedBuild(JsonElement run, int requestedBuildId, string requestedBuildUri)
    {
        var hasBuildId = TryGetBuildIdFromTestRun(run, out var runBuildId);
        if (hasBuildId)
        {
            return runBuildId == requestedBuildId;
        }

        var hasBuildUri = TryGetBuildUriFromTestRun(run, out var runBuildUri);
        if (hasBuildUri)
        {
            return string.Equals(runBuildUri, requestedBuildUri, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool TryGetBuildUriFromTestRun(JsonElement run, out string buildUri)
    {
        buildUri = string.Empty;

        if (TryGetStringProperty(run, "buildUri", out buildUri))
        {
            return true;
        }

        if (!run.TryGetProperty("build", out var buildReference))
        {
            return false;
        }

        return TryGetStringProperty(buildReference, "uri", out buildUri);
    }

    private static bool TryGetIntProperty(JsonElement element, string propertyName, out int value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(property.GetString(), out value),
            _ => false
        };
    }

    private static DateTimeOffset? TryGetDateTimeOffsetProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.TryGetDateTimeOffset(out var value)
            ? value
            : null;
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var propertyValue = property.GetString();
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            return false;
        }

        value = propertyValue;
        return true;
    }

    private sealed record TestRunBuildRetrievalSummary(
        List<TestRunDto> TestRuns,
        int PagesRequested,
        int HttpRequestCount,
        int RawRunCount,
        int DtoCount);

    private sealed record CoverageBuildRetrievalSummary(
        List<CoverageDto> CoverageRows,
        int HttpRequestCount,
        int RowCount);
}
