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
            "Requesting TFS test runs for {BuildCount} build ids via {EndpointPath} with api-version {ApiVersion}.",
            requestedBuildIds.Length,
            BuildQualityTestRunsEndpointPath,
            BuildQualityTestRunsApiVersion);

        var results = await ExecuteWithRetryAsync(async () =>
        {
            var results = new List<TestRunDto>();

            _logger.LogInformation(
                "Attempting TFS test run retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

            foreach (var buildId in requestedBuildIds)
            {
                _logger.LogInformation(
                    "Attempting TFS test run retrieval for build {BuildId}.",
                    buildId);

                var skip = 0;
                var retrievedRunCount = 0;
                var parsedRunCount = 0;
                while (true)
                {
                    var url = ProjectUrlWithApiVersionOverride(
                        config,
                        $"{BuildQualityTestRunsEndpointPath}?buildIds={buildId}" +
                        $"&$top={TestRunPageSize}&$skip={skip}",
                        BuildQualityTestRunsApiVersion);
                    _logger.LogDebug(
                        "Requesting TFS test runs page for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, api-version {ApiVersion}).",
                        buildId,
                        url,
                        BuildQualityTestRunsEndpointPath,
                        BuildQualityTestRunsApiVersion);

                    var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "TFS test runs endpoint failed for build {BuildId} via {RequestUrl} (endpoint {EndpointPath}, api-version {ApiVersion}) with status code {StatusCode}.",
                            buildId,
                            url,
                            BuildQualityTestRunsEndpointPath,
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
                        var dto = ParseTestRunDto(run, buildId);
                        if (dto is not null)
                        {
                            results.Add(dto);
                            parsedRunCount++;
                        }
                    }

                    retrievedRunCount += pageRetrievedCount;

                    if (pageRetrievedCount == 0)
                    {
                        break;
                    }

                    skip += TestRunPageSize;
                }

                _logger.LogInformation(
                    "Retrieved {RetrievedCount} raw TFS test run elements for build {BuildId}.",
                    retrievedRunCount,
                    buildId);
                _logger.LogInformation(
                    "Parsed {ParsedCount}/{RetrievedCount} TFS test runs for build {BuildId}.",
                    parsedRunCount,
                    retrievedRunCount,
                    buildId);
            }

            _logger.LogInformation(
                "Attempted TFS test run retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

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

        var coverageResults = await ExecuteWithRetryAsync(async () =>
        {
            var coverageResults = new List<CoverageDto>();

            _logger.LogInformation(
                "Attempting TFS coverage retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

            foreach (var batch in requestedBuildIds.Chunk(25))
            {
                var batchTasks = batch.Select(buildId => GetCoverageForBuildAsync(config, httpClient, buildId, cancellationToken));
                var batchResults = await Task.WhenAll(batchTasks);
                foreach (var result in batchResults)
                {
                    coverageResults.AddRange(result);
                }
            }

            _logger.LogInformation(
                "Attempted TFS coverage retrieval for {AttemptedBuildCount} requested build IDs.",
                requestedBuildIds.Length);

            return coverageResults;
        }, cancellationToken);

        _logger.LogInformation(
            "Retrieved {ResultCount} TFS coverage rows for {BuildCount} build ids.",
            coverageResults.Count(),
            requestedBuildIds.Length);

        return coverageResults;
    }

    private async Task<List<CoverageDto>> GetCoverageForBuildAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        int buildId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Attempting TFS coverage retrieval for build {BuildId}.",
            buildId);

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
            return [];
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
        return results;
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
}
