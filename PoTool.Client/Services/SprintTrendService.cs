using System.Net.Http.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for accessing Sprint Trend metrics.
/// </summary>
public class SprintTrendService
{
    private readonly HttpClient _httpClient;

    public SprintTrendService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets sprint trend metrics for a range of sprints.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID.</param>
    /// <param name="sprintIds">Sprint IDs to get metrics for.</param>
    /// <param name="recompute">Whether to recompute metrics from revision data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sprint trend metrics response.</returns>
    public async Task<GetSprintTrendMetricsResponse?> GetSprintTrendMetricsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        bool recompute = false,
        CancellationToken cancellationToken = default)
    {
        var sprintIdsQuery = string.Join("&sprintIds=", sprintIds);
        var url = $"api/Metrics/sprint-trend?productOwnerId={productOwnerId}&sprintIds={sprintIdsQuery}&recompute={recompute.ToString().ToLower()}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = $"API error: {response.StatusCode}"
            };
        }

        return await response.Content.ReadFromJsonAsync<GetSprintTrendMetricsResponse>(cancellationToken);
    }
}
