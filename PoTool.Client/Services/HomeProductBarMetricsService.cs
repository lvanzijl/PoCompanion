using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for Home product bar contextual metrics.
/// </summary>
public class HomeProductBarMetricsService
{
    private readonly IMetricsClient _metricsClient;

    public HomeProductBarMetricsService(IMetricsClient metricsClient)
    {
        _metricsClient = metricsClient;
    }

    public async Task<DataStateResult<HomeProductBarMetricsDto>> GetAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _metricsClient.GetHomeProductBarMetricsAsync(productOwnerId, productId, cancellationToken))
                .ToDataStateResponse()
                .ToDataStateResult();
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return DataStateResult<HomeProductBarMetricsDto>.Failed("Context metrics unavailable.");
        }
    }
}
