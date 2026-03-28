using PoTool.Client.ApiClient;
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

    public async Task<HomeProductBarMetricsDto?> GetAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _metricsClient.GetHomeProductBarMetricsEnvelopeAsync(productOwnerId, productId, cancellationToken);
            return response.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return null;
        }
    }
}
