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

/// <summary>
/// Home dashboard product-bar load result, preserving the cache-backed data state
/// and any backend reason text alongside the mapped metrics payload.
/// </summary>
/// <param name="State">Cache-backed data-state returned for the product-bar request.</param>
/// <param name="Data">Mapped Home product-bar metrics when <paramref name="State"/> is <see cref="DataStateDto.Available"/>.</param>
/// <param name="FilterMetadata">Canonical requested/effective filter metadata associated with <paramref name="Data"/>.</param>
/// <param name="Reason">Backend-provided empty/not-ready/failure explanation that the UI can surface directly.</param>
public sealed record HomeProductBarMetricsResult(
    DataStateDto State,
    HomeProductBarMetricsDto? Data = null,
    CanonicalFilterMetadata? FilterMetadata = null,
    string? Reason = null)
{
    /// <summary>
    /// Creates a successful result with mapped metrics data and canonical filter metadata.
    /// </summary>
    public static HomeProductBarMetricsResult Available(CanonicalClientResponse<HomeProductBarMetricsDto> response)
        => new(DataStateDto.Available, response.Data, response.FilterMetadata);

    /// <summary>
    /// Creates an empty-state result when the backend completed successfully but no scoped metrics were available.
    /// </summary>
    public static HomeProductBarMetricsResult Empty(string reason)
        => new(DataStateDto.Empty, Reason: reason);

    /// <summary>
    /// Creates a not-ready result when the cache-backed backend response indicates data is still warming.
    /// The reason should be shown to users instead of a generic unavailable message.
    /// </summary>
    public static HomeProductBarMetricsResult NotReady(string reason)
        => new(DataStateDto.NotReady, Reason: reason);

    /// <summary>
    /// Creates a failed result when the request could not be completed and the page should surface the failure reason.
    /// </summary>
    public static HomeProductBarMetricsResult Failed(string reason)
        => new(DataStateDto.Failed, Reason: reason);
}
