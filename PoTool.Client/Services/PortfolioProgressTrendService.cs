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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Portfolio progress trend DTO, or null on failure.</returns>
    public async Task<PortfolioProgressTrendDto?> GetPortfolioProgressTrendAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        var sprintIdParams = string.Join("&", sprintIds.Select(id => $"sprintIds={id}"));
        var url = $"api/Metrics/portfolio-progress-trend?productOwnerId={productOwnerId}&{sprintIdParams}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PortfolioProgressTrendDto>(cancellationToken);
    }
}
