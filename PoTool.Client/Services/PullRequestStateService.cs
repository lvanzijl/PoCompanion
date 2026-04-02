using System.Net.Http.Json;
using PoTool.Shared.DataState;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Services;

public sealed class PullRequestStateService
{
    private readonly HttpClient _httpClient;

    public PullRequestStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<PullRequestInsightsDto>>?> GetInsightsStateAsync(
        int productOwnerId,
        int? sprintId,
        int? teamId,
        string? repositoryName,
        CancellationToken cancellationToken = default)
        => GetAsync<PullRequestQueryResponseDto<PullRequestInsightsDto>>(
            BuildUrl(
                "/api/pullrequests/insights",
                ("productOwnerId", productOwnerId),
                ("sprintId", sprintId),
                ("teamId", teamId),
                ("repositoryName", repositoryName)),
            cancellationToken);

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<PrDeliveryInsightsDto>>?> GetDeliveryInsightsStateAsync(
        int productOwnerId,
        int? sprintId,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<PullRequestQueryResponseDto<PrDeliveryInsightsDto>>(
            BuildUrl(
                "/api/pullrequests/delivery-insights",
                ("productOwnerId", productOwnerId),
                ("sprintId", sprintId),
                ("teamId", teamId)),
            cancellationToken);

    public Task<DataStateResponseDto<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>?> GetSprintTrendsStateAsync(
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        int? teamId,
        CancellationToken cancellationToken = default)
        => GetAsync<PullRequestQueryResponseDto<GetPrSprintTrendsResponse>>(
            BuildUrl(
                "/api/pullrequests/sprint-trends",
                ("sprintIds", sprintIds),
                ("productIds", productIds),
                ("teamId", teamId)),
            cancellationToken);

    private Task<DataStateResponseDto<T>?> GetAsync<T>(string url, CancellationToken cancellationToken)
        => _httpClient.GetFromJsonAsync<DataStateResponseDto<T>>(url, cancellationToken);

    private static string BuildUrl(string path, params (string Key, object? Value)[] parameters)
    {
        var query = new List<string>();

        foreach (var (key, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
                }

                continue;
            }

            if (value is IEnumerable<int> intValues && value is not string)
            {
                query.AddRange(intValues.Select(item => $"{Uri.EscapeDataString(key)}={item}"));
                continue;
            }

            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.ToString()!)}");
        }

        return query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
    }
}
