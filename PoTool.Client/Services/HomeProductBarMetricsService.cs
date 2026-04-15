using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Service for Home product bar contextual metrics.
/// </summary>
public class HomeProductBarMetricsService
{
    private readonly IMetricsClient _metricsClient;

    public HomeProductBarMetricsService(IMetricsClient metricsClient)
    {
        _metricsClient = metricsClient;
    }

    public async Task<HomeProductBarMetricsResult> GetAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _metricsClient.GetHomeProductBarMetricsAsync(productOwnerId, productId, cancellationToken);

            return response.State switch
            {
                DataStateDto.Available => CreateAvailableResult(response),
                DataStateDto.Empty => HomeProductBarMetricsResult.Empty(
                    response.Reason ?? "No context metrics matched the current scope."),
                DataStateDto.NotReady => HomeProductBarMetricsResult.NotReady(
                    response.Reason ?? "Cached context metrics are not ready yet."),
                DataStateDto.Failed => HomeProductBarMetricsResult.Failed(
                    response.Reason ?? "Context metrics unavailable."),
                _ => HomeProductBarMetricsResult.Failed("Context metrics unavailable.")
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException)
        {
            return HomeProductBarMetricsResult.Failed("Context metrics unavailable.");
        }
    }

    private static HomeProductBarMetricsResult CreateAvailableResult(
        DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto response)
    {
        var payload = response.GetDataOrDefault();
        if (payload is null)
        {
            return HomeProductBarMetricsResult.Failed("Context metrics unavailable.");
        }

        return HomeProductBarMetricsResult.Available(
            CanonicalClientResponseFactory.Create(payload));
    }
}

public sealed record HomeProductBarMetricsResult(
    DataStateDto State,
    HomeProductBarMetricsDto? Data = null,
    CanonicalFilterMetadata? FilterMetadata = null,
    string? Reason = null)
{
    public static HomeProductBarMetricsResult Available(CanonicalClientResponse<HomeProductBarMetricsDto> response)
        => new(DataStateDto.Available, response.Data, response.FilterMetadata);

    public static HomeProductBarMetricsResult Empty(string reason)
        => new(DataStateDto.Empty, Reason: reason);

    public static HomeProductBarMetricsResult NotReady(string reason)
        => new(DataStateDto.NotReady, Reason: reason);

    public static HomeProductBarMetricsResult Failed(string reason)
        => new(DataStateDto.Failed, Reason: reason);
}
