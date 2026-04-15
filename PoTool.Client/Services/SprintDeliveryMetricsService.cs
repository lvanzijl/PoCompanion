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

    public async Task<DataStateResult<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsAsync(
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
            return DataStateResult<GetSprintTrendMetricsResponse>.Invalid("At least one sprint ID is required.");
        }

        try
        {
            return (await _metricsClient.GetSprintTrendMetricsAsync(
                productOwnerId,
                sprintIdList,
                productIds,
                recompute,
                includeDetails,
                cancellationToken))
                .ToDataStateResponse()
                .ToDataStateResult();
        }
        catch (ApiException ex)
        {
            return ex.StatusCode == 400
                ? DataStateResult<GetSprintTrendMetricsResponse>.Invalid(
                    string.IsNullOrWhiteSpace(ex.Response)
                        ? "Sprint metrics request was rejected by the server."
                        : ex.Response)
                : DataStateResult<GetSprintTrendMetricsResponse>.Failed(
                    string.IsNullOrWhiteSpace(ex.Response)
                        ? $"Sprint metrics request failed with HTTP {ex.StatusCode}."
                        : ex.Response);
        }
    }
}
