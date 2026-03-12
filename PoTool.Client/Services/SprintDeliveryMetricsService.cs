using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for loading Sprint Delivery metrics with optional drilldown detail payloads.
/// </summary>
public class SprintDeliveryMetricsService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SprintDeliveryMetricsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GetSprintTrendMetricsResponse?> GetSprintTrendMetricsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        bool recompute = false,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sprintIds);

        var sprintIdList = sprintIds.ToList();
        if (sprintIdList.Count == 0)
        {
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = "At least one sprint ID is required."
            };
        }

        var urlBuilder = new StringBuilder("/api/Metrics/sprint-trend?");
        urlBuilder.Append("productOwnerId=").Append(Uri.EscapeDataString(productOwnerId.ToString()));

        foreach (var sprintId in sprintIdList)
        {
            urlBuilder.Append("&sprintIds=").Append(Uri.EscapeDataString(sprintId.ToString()));
        }

        urlBuilder.Append("&recompute=").Append(Uri.EscapeDataString(recompute.ToString().ToLowerInvariant()));
        urlBuilder.Append("&includeDetails=").Append(Uri.EscapeDataString(includeDetails.ToString().ToLowerInvariant()));

        using var response = await _httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return new GetSprintTrendMetricsResponse
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(error)
                    ? $"Sprint metrics request failed with HTTP {(int)response.StatusCode}."
                    : error
            };
        }

        return await response.Content.ReadFromJsonAsync<GetSprintTrendMetricsResponse>(JsonOptions, cancellationToken);
    }
}
