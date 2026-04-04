using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for loading Sprint Delivery metrics with optional drilldown detail payloads.
/// </summary>
public class SprintDeliveryMetricsService
{
    private readonly IMetricsClient _metricsClient;

    public SprintDeliveryMetricsService(IMetricsClient metricsClient)
    {
        _metricsClient = metricsClient;
    }

    public async Task<CanonicalClientResponse<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds = null,
        bool recompute = false,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sprintIds);

        var sprintIdList = sprintIds.ToList();
        if (sprintIdList.Count == 0)
        {
            return new CanonicalClientResponse<GetSprintTrendMetricsResponse>(
                new GetSprintTrendMetricsResponse
                {
                    Success = false,
                    ErrorMessage = "At least one sprint ID is required."
                });
        }

        try
        {
            var response = await _metricsClient.GetSprintTrendMetricsAsync(
                productOwnerId,
                sprintIdList,
                productIds,
                recompute,
                includeDetails,
                cancellationToken);

            var envelope = GeneratedCacheEnvelopeHelper.GetDataOrDefault<object>(response);
            if (envelope is null)
            {
                return new CanonicalClientResponse<GetSprintTrendMetricsResponse>(
                    new GetSprintTrendMetricsResponse
                    {
                        Success = false,
                        ErrorMessage = "Sprint metrics are not currently available from the cache-backed endpoint."
                    });
            }

            return CanonicalClientResponseFactory.CreateGenerated<GetSprintTrendMetricsResponse>(envelope, CanonicalFilterKind.Sprint);
        }
        catch (ApiException ex)
        {
            return new CanonicalClientResponse<GetSprintTrendMetricsResponse>(
                new GetSprintTrendMetricsResponse
                {
                    Success = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(ex.Response)
                        ? $"Sprint metrics request failed with HTTP {ex.StatusCode}."
                        : ex.Response
                });
        }
    }
}
