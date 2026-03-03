using System.Net.Http.Json;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for fetching per-sprint PR trend metrics.
/// </summary>
public class PrSprintTrendsService
{
    private readonly HttpClient _httpClient;

    public PrSprintTrendsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets per-sprint PR metrics for a set of sprint IDs.
    /// </summary>
    /// <param name="sprintIds">Sprint IDs defining the trend horizon.</param>
    /// <param name="productIds">Optional explicit product IDs to filter PRs. Takes priority over teamId.</param>
    /// <param name="teamId">Optional team ID; resolved to linked products via ProductTeamLinks when productIds is null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<GetPrSprintTrendsResponse?> GetSprintTrendsAsync(
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds = null,
        int? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var sprintParams = string.Join("&", sprintIds.Select(id => $"sprintIds={id}"));
        var productParam = productIds != null
            ? $"&productIds={string.Join(",", productIds)}"
            : string.Empty;
        var teamParam = teamId.HasValue ? $"&teamId={teamId.Value}" : string.Empty;
        var url = $"api/PullRequests/sprint-trends?{sprintParams}{productParam}{teamParam}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GetPrSprintTrendsResponse
            {
                Success = false,
                ErrorMessage = $"API error: {response.StatusCode}"
            };
        }

        return await response.Content.ReadFromJsonAsync<GetPrSprintTrendsResponse>(cancellationToken);
    }
}
