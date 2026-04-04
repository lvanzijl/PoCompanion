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

    public async Task<CanonicalClientResponse<HomeProductBarMetricsDto>?> GetAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _metricsClient.GetHomeProductBarMetricsAsync(productOwnerId, productId, cancellationToken);
            var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault(
                response,
                static data => data.ToShared());
            return payload is null
                ? null
                : CanonicalClientResponseFactory.Create(payload);
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return null;
        }
    }
}
