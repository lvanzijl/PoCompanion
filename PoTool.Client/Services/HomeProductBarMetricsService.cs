using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for Home product bar contextual metrics.
/// </summary>
public class HomeProductBarMetricsService
{
    private readonly HttpClient _httpClient;

    public HomeProductBarMetricsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HomeProductBarMetricsDto?> GetAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken = default)
    {
        var query = productId.HasValue
            ? $"?productOwnerId={productOwnerId}&productId={productId.Value}"
            : $"?productOwnerId={productOwnerId}";

        try
        {
            return await _httpClient.GetFromJsonAsync<HomeProductBarMetricsDto>(
                $"api/Metrics/home-product-bar{query}",
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
