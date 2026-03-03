using System.Net.Http.Json;
using PoTool.Shared.Pipelines;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for fetching per-sprint pipeline trend metrics.
/// </summary>
public class PipelineSprintTrendsService
{
    private readonly HttpClient _httpClient;

    public PipelineSprintTrendsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets per-sprint pipeline metrics for a set of sprint IDs.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID.</param>
    /// <param name="sprintIds">Sprint IDs to aggregate runs for.</param>
    /// <param name="productIds">Optional explicit product IDs to filter pipeline definitions. Takes priority over teamId.</param>
    /// <param name="teamId">Optional team ID; resolved to linked products via ProductTeamLinks when productIds is null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<GetPipelineSprintTrendsResponse?> GetSprintTrendsAsync(
        int productOwnerId,
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
        var url = $"api/Pipelines/sprint-trends?productOwnerId={productOwnerId}&{sprintParams}{productParam}{teamParam}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GetPipelineSprintTrendsResponse
            {
                Success = false,
                ErrorMessage = $"API error: {response.StatusCode}"
            };
        }

        return await response.Content.ReadFromJsonAsync<GetPipelineSprintTrendsResponse>(cancellationToken);
    }
}
