using System.Net.Http.Json;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for the Capacity Calibration API endpoint.
/// </summary>
public class CapacityCalibrationService
{
    private readonly HttpClient _httpClient;

    public CapacityCalibrationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets capacity calibration metrics for a selected sprint range.
    /// Returns velocity distribution (median/P25/P75), predictability, and outlier sprint names.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID.</param>
    /// <param name="sprintIds">Sprint IDs (ordered chronologically).</param>
    /// <param name="productIds">Optional product IDs to filter. Null or empty = all products.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CapacityCalibrationDto, or null on failure.</returns>
    public async Task<CapacityCalibrationDto?> GetCapacityCalibrationAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"productOwnerId={productOwnerId}" };
        parts.AddRange(sprintIds.Select(id => $"sprintIds={id}"));

        var productIdList = productIds?.ToList();
        if (productIdList is { Count: > 0 })
        {
            parts.AddRange(productIdList.Select(id => $"productIds={id}"));
        }

        var url = $"api/Metrics/capacity-calibration?{string.Join("&", parts)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CapacityCalibrationDto>(cancellationToken);
    }
}
