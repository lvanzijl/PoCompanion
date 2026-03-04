using System.Net.Http.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for the Portfolio Progress Trend API endpoint.
/// </summary>
public class PortfolioProgressTrendService
{
    private readonly HttpClient _httpClient;

    public PortfolioProgressTrendService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets portfolio progress trend metrics for a range of sprints.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID.</param>
    /// <param name="sprintIds">Sprint IDs (ordered chronologically).</param>
    /// <param name="productIds">Optional product IDs to filter. Null or empty = all products.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Portfolio progress trend DTO, or null on failure.</returns>
    public async Task<PortfolioProgressTrendDto?> GetPortfolioProgressTrendAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"productOwnerId={productOwnerId}" };
        parts.AddRange(sprintIds.Select(id => $"sprintIds={id}"));

        var productIdList = productIds?.ToList();
        if (productIdList is { Count: > 0 })
        {
            parts.AddRange(productIdList.Select(id => $"productIds={id}"));
        }

        var url = $"api/Metrics/portfolio-progress-trend?{string.Join("&", parts)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PortfolioProgressTrendDto>(cancellationToken);
    }
}
