using System.Net.Http.Json;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

public sealed class MetricsStateService
{
    private readonly HttpClient _httpClient;

    public MetricsStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>?> GetWorkItemActivityDetailsStateAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string> { $"productOwnerId={productOwnerId}" };
        if (sprintId.HasValue)
        {
            queryParts.Add($"sprintId={sprintId.Value}");
        }

        if (periodStartUtc.HasValue)
        {
            queryParts.Add($"periodStartUtc={Uri.EscapeDataString(periodStartUtc.Value.ToString("O"))}");
        }

        if (periodEndUtc.HasValue)
        {
            queryParts.Add($"periodEndUtc={Uri.EscapeDataString(periodEndUtc.Value.ToString("O"))}");
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>>(
            $"/api/metrics/state/work-item-activity/{workItemId}?{string.Join("&", queryParts)}",
            cancellationToken);
    }

    public async Task<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>?> GetPortfolioProgressTrendStateAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken = default)
    {
        var queryParts = new List<string> { $"productOwnerId={productOwnerId}" };
        queryParts.AddRange(sprintIds.Select(sprintId => $"sprintIds={sprintId}"));

        if (productIds != null)
        {
            queryParts.AddRange(productIds.Select(productId => $"productIds={productId}"));
        }

        return await _httpClient.GetFromJsonAsync<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>>(
            $"/api/metrics/state/portfolio-progress-trend?{string.Join("&", queryParts)}",
            cancellationToken);
    }
}
