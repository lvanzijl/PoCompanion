using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

internal partial class RealTfsClient
{
    private const string BuildQualityTestRunsEndpointPath = "_apis/test/runs";
    private const string BuildQualityCoverageEndpointPath = "_apis/testresults/codecoverage";
    private const string BuildQualityTestRunsApiVersion = "7.0";
    private const string BuildQualityCoverageApiVersion = "7.0-preview";
    private const string BuildQualityTestRunsQueryShape = "buildUri";

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
            var totalRawRunCount = buildSummaries.Sum(summary => summary.RawRunCount);
            var totalDtoCount = buildSummaries.Sum(summary => summary.DtoCount);
            _logger.LogInformation(
                "BUILDQUALITY_TESTRUN_REQUEST_SUMMARY: attemptedBuildCount={AttemptedBuildCount}, httpRequestCount={HttpRequestCount}, rawRunCount={RawRunCount}, dtoCount={DtoCount}, elapsedMs={ElapsedMs}",
                requestedBuildIds.Length,
                totalHttpRequestCount,
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
        var url = ProjectUrlWithApiVersionOverride(
            config,
            $"{BuildQualityTestRunsEndpointPath}?buildUri={Uri.EscapeDataString($"vstfs:///Build/Build/{buildId}")}",
            BuildQualityTestRunsApiVersion);
        _logger.LogDebug(
            "Requesting aggregated TFS test run for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, query shape {QueryShape}, api-version {ApiVersion}).",
            buildId,
            url,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape,
            BuildQualityTestRunsApiVersion);

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
        var aggregatedRuns = EnumerateTestRunElements(doc.RootElement).ToList();
        var testRuns = new List<TestRunDto>(capacity: 1);

        if (aggregatedRuns.Count > 0)
        {
            var run = aggregatedRuns[0];
            if (!TryGetIntProperty(run, "totalTests", out var totalTests) ||
                !TryGetIntProperty(run, "passedTests", out var passedTests) ||
                !TryGetIntProperty(run, "notApplicableTests", out var notApplicableTests))
            {
                _logger.LogWarning(
                    "Skipping aggregated TFS test run payload for build {BuildId} because one or more required raw counters are missing.",
                    buildId);
            }
            else
            {
                _ = TryGetIntProperty(run, "id", out var externalId);
                testRuns.Add(new TestRunDto
                {
                    BuildId = buildId,
                    ExternalId = externalId,
                    TotalTests = totalTests,
                    PassedTests = passedTests,
                    NotApplicableTests = notApplicableTests,
                    Timestamp = TryGetDateTimeOffsetProperty(run, "completedDate")
                });
            }
        }

        if (testRuns.Count > 0)
        {
            var retrievedRun = testRuns[0];
            _logger.LogInformation(
                "Build {BuildId} -> 1 aggregated run retrieved (Total={TotalTests}, Passed={PassedTests}, NotApplicable={NotApplicableTests}).",
                buildId,
                retrievedRun.TotalTests,
                retrievedRun.PassedTests,
                retrievedRun.NotApplicableTests);
        }
        else
        {
            _logger.LogInformation(
                "Build {BuildId} -> 0 aggregated runs retrieved.",
                buildId);
        }

        _logger.LogInformation(
            "Retrieved {RetrievedCount} raw TFS test run elements for build {BuildId} via {EndpointPath} with query shape {QueryShape}.",
            aggregatedRuns.Count,
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape);
        _logger.LogInformation(
            "Parsed {ParsedCount}/{RetrievedCount} aggregated TFS test runs for build {BuildId} via {EndpointPath} with query shape {QueryShape}.",
            testRuns.Count,
            aggregatedRuns.Count,
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape);
        _logger.LogInformation(
            "BUILDQUALITY_TESTRUN_BUILD_SUMMARY: buildId={BuildId}, endpointPath={EndpointPath}, queryShape={QueryShape}, httpRequestCount={HttpRequestCount}, rawRunCount={RawRunCount}, dtoCount={DtoCount}, elapsedMs={ElapsedMs}",
            buildId,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsQueryShape,
            1,
            aggregatedRuns.Count,
            testRuns.Count,
            buildStopwatch.ElapsedMilliseconds);

        return new TestRunBuildRetrievalSummary(testRuns, 1, aggregatedRuns.Count, testRuns.Count);
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
        if (results.Count > 0)
        {
            var coverage = results[0];
            _logger.LogInformation(
                "Build {BuildId} -> coverage retrieved (Covered={CoveredLines}, Total={TotalLines}).",
                buildId,
                coverage.CoveredLines,
                coverage.TotalLines);
        }
        else
        {
            _logger.LogInformation(
                "Build {BuildId} -> no coverage data.",
                buildId);
        }

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

    private IEnumerable<CoverageDto> ParseCoverageDtos(JsonElement summary, int buildId)
    {
        if (!summary.TryGetProperty("coverageData", out var coverageData) ||
            coverageData.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because the payload does not contain a coverageData array.",
                buildId);
            yield break;
        }

        var coverageEntryCount = coverageData.GetArrayLength();
        if (coverageEntryCount == 0)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because the payload contains no coverageData entries.",
                buildId);
            yield break;
        }

        if (coverageEntryCount > 1)
        {
            _logger.LogWarning(
                "Build {BuildId} -> received multiple coverageData entries; attempting to use the first valid Lines stat only.",
                buildId);
        }

        var sawCoverageStats = false;
        var sawLinesStat = false;
        var sawMissingCoveredOrTotal = false;
        var sawInvalidNumericIntegrity = false;

        foreach (var coverageEntry in coverageData.EnumerateArray())
        {
            if (!coverageEntry.TryGetProperty("coverageStats", out var coverageStats) ||
                coverageStats.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            sawCoverageStats = true;
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
                    sawLinesStat = true;
                    break;
                }
            }

            if (!linesStat.HasValue)
            {
                continue;
            }

            if (!TryGetIntProperty(linesStat.Value, "covered", out var coveredLines) ||
                !TryGetIntProperty(linesStat.Value, "total", out var totalLines))
            {
                sawMissingCoveredOrTotal = true;
                continue;
            }

            if (coveredLines < 0 || totalLines < 0 || coveredLines > totalLines)
            {
                sawInvalidNumericIntegrity = true;
                continue;
            }

            yield return new CoverageDto
            {
                BuildId = buildId,
                CoveredLines = coveredLines,
                TotalLines = totalLines
            };
            yield break;
        }

        if (!sawCoverageStats)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because no coverageStats array was found.",
                buildId);
            yield break;
        }

        if (!sawLinesStat)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because no Lines coverage stat was found.",
                buildId);
            yield break;
        }

        if (sawMissingCoveredOrTotal)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because the Lines coverage stat is missing covered or total values.",
                buildId);
            yield break;
        }

        if (sawInvalidNumericIntegrity)
        {
            _logger.LogWarning(
                "Build {BuildId} -> no coverage data because the Lines coverage stat has invalid numeric values.",
                buildId);
        }
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

    private sealed record TestRunBuildRetrievalSummary(
        List<TestRunDto> TestRuns,
        int HttpRequestCount,
        int RawRunCount,
        int DtoCount);

    private sealed record CoverageBuildRetrievalSummary(
        List<CoverageDto> CoverageRows,
        int HttpRequestCount,
        int RowCount);
}
