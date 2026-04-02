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

    private Task<DataStateResponseDto<T>?> GetAsync<T>(string url, CancellationToken cancellationToken)
        => _httpClient.GetFromJsonAsync<DataStateResponseDto<T>>(url, cancellationToken);

    private static string BuildUrl(string path, params (string Key, object? Value)[] parameters)
    {
        var query = parameters
            .Where(item =>
            {
                if (item.Value is null)
                {
                    return false;
                }

                return item.Value is not string text || !string.IsNullOrWhiteSpace(text);
            })
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!.ToString()!)}");

        var materialized = query.ToList();
        return materialized.Count == 0 ? path : $"{path}?{string.Join("&", materialized)}";
    }
}
