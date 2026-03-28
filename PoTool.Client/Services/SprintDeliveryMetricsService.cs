using PoTool.Client.ApiClient;
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

    public async Task<GetSprintTrendMetricsResponse?> GetSprintTrendMetricsAsync(
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
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = "At least one sprint ID is required."
            };
        }

        try
        {
            var envelope = await _metricsClient.GetSprintTrendMetricsEnvelopeAsync(
                productOwnerId,
                sprintIdList,
                productIds,
                recompute,
                includeDetails,
                cancellationToken);

            return envelope.Data;
        }
        catch (ApiException ex)
        {
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(ex.Response)
                    ? $"Sprint metrics request failed with HTTP {ex.StatusCode}."
                    : ex.Response
            };
        }
    }
}
