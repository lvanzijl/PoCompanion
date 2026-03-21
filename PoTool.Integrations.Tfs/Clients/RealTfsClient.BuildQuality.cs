using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
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

        return await ExecuteWithRetryAsync(async () =>
        {
            var buildWindows = await GetBuildQueryWindowsAsync(config, httpClient, requestedBuildIds, cancellationToken);
            if (buildWindows.ValidBuildIds.Count == 0 || buildWindows.Windows.Count == 0)
            {
                _logger.LogInformation(
                    "No valid build metadata found for requested test run retrieval: {BuildIds}",
                    string.Join(", ", requestedBuildIds));
                return [];
            }

            var results = new List<TestRunDto>();
            var buildIdFilter = string.Join(",", buildWindows.ValidBuildIds);

            foreach (var window in buildWindows.Windows)
            {
                var url = ProjectUrl(
                    config,
                    $"_apis/testresults/runs?minLastUpdatedDate={Uri.EscapeDataString(window.Start.ToString("O"))}" +
                    $"&maxLastUpdatedDate={Uri.EscapeDataString(window.End.ToString("O"))}" +
                    $"&buildIds={Uri.EscapeDataString(buildIdFilter)}");

                string? continuationToken = null;
                var pageUrl = url;

                do
                {
                    var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                    if (!response.IsSuccessStatusCode)
                    {
                        await HandleHttpErrorsAsync(response, cancellationToken);
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            var dto = ParseTestRunDto(run);
                            if (dto is not null && buildWindows.ValidBuildIds.Contains(dto.BuildId))
                            {
                                results.Add(dto);
                            }
                        }
                    }

                    continuationToken = GetContinuationToken(response, doc);
                    pageUrl = AddContinuationToken(url, continuationToken);
                } while (!string.IsNullOrWhiteSpace(continuationToken));
            }

            return results;
        }, cancellationToken);
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

        return await ExecuteWithRetryAsync(async () =>
        {
            var buildWindows = await GetBuildQueryWindowsAsync(config, httpClient, requestedBuildIds, cancellationToken);
            if (buildWindows.ValidBuildIds.Count == 0)
            {
                _logger.LogInformation(
                    "No valid build metadata found for requested coverage retrieval: {BuildIds}",
                    string.Join(", ", requestedBuildIds));
                return [];
            }

            var coverageResults = new List<CoverageDto>();

            foreach (var batch in buildWindows.ValidBuildIds.Chunk(25))
            {
                var batchTasks = batch.Select(buildId => GetCoverageForBuildAsync(config, httpClient, buildId, cancellationToken));
                var batchResults = await Task.WhenAll(batchTasks);
                foreach (var result in batchResults)
                {
                    coverageResults.AddRange(result);
                }
            }

            return coverageResults;
        }, cancellationToken);
    }

    private async Task<BuildQueryWindows> GetBuildQueryWindowsAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        IReadOnlyCollection<int> buildIds,
        CancellationToken cancellationToken)
    {
        var buildMetadata = await GetBuildMetadataAsync(config, httpClient, buildIds, cancellationToken);
        var validBuildIds = buildMetadata
            .Select(metadata => metadata.BuildId)
            .Distinct()
            .ToHashSet();

        var timestamps = buildMetadata
            .SelectMany(metadata => new[] { metadata.StartTime, metadata.FinishTime })
            .Where(timestamp => timestamp.HasValue)
            .Select(timestamp => timestamp!.Value)
            .OrderBy(timestamp => timestamp)
            .ToList();

        if (timestamps.Count == 0)
        {
            return new BuildQueryWindows(validBuildIds, []);
        }

        var minTimestamp = timestamps[0].AddDays(-1);
        var maxTimestamp = timestamps[^1].AddDays(1);

        var windows = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var windowStart = minTimestamp;
        while (windowStart < maxTimestamp)
        {
            var windowEnd = windowStart.AddDays(7);
            if (windowEnd > maxTimestamp)
            {
                windowEnd = maxTimestamp;
            }

            windows.Add((windowStart, windowEnd));
            windowStart = windowEnd;
        }

        return new BuildQueryWindows(validBuildIds, windows);
    }

    private async Task<List<BuildMetadata>> GetBuildMetadataAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        IReadOnlyCollection<int> buildIds,
        CancellationToken cancellationToken)
    {
        var metadata = new List<BuildMetadata>();

        foreach (var batch in buildIds.Chunk(200))
        {
            var url = ProjectUrl(
                config,
                $"_apis/build/builds?buildIds={Uri.EscapeDataString(string.Join(",", batch))}&$top={batch.Length}");

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
            if (!response.IsSuccessStatusCode)
            {
                await HandleHttpErrorsAsync(response, cancellationToken);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                continue;
            }

            foreach (var build in valueArray.EnumerateArray())
            {
                if (!TryGetIntProperty(build, "id", out var buildId))
                {
                    continue;
                }

                metadata.Add(new BuildMetadata(
                    buildId,
                    TryGetDateTimeOffsetProperty(build, "startTime"),
                    TryGetDateTimeOffsetProperty(build, "finishTime")));
            }
        }

        return metadata;
    }

    private async Task<List<CoverageDto>> GetCoverageForBuildAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        int buildId,
        CancellationToken cancellationToken)
    {
        var url = ProjectUrl(config, $"_apis/testresults/codecoverage?buildId={buildId}");
        var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            await HandleHttpErrorsAsync(response, cancellationToken);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseCoverageDtos(doc.RootElement, buildId).ToList();
    }

    private TestRunDto? ParseTestRunDto(JsonElement run)
    {
        if (!TryGetBuildIdFromTestRun(run, out var buildId))
        {
            _logger.LogWarning("Skipping TFS test run payload without a verified build.id linkage.");
            return null;
        }

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

        var rawBuildId = buildIdElement.GetString();
        return int.TryParse(rawBuildId, out buildId);
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

    private sealed record BuildMetadata(int BuildId, DateTimeOffset? StartTime, DateTimeOffset? FinishTime);

    private sealed record BuildQueryWindows(
        HashSet<int> ValidBuildIds,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Windows);
}
