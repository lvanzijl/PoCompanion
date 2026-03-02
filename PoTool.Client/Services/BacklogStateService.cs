using System.Net.Http.Json;
using PoTool.Shared.Health;

namespace PoTool.Client.Services;

/// <summary>
/// Client service for the product-scoped backlog state API.
/// Retrieves hierarchical refinement scores (Epic → Feature → PBI) for a product.
/// </summary>
public class BacklogStateService
{
    private readonly HttpClient _httpClient;

    public BacklogStateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the backlog state for the specified product.
    /// Returns null when the product is not found or the request fails.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Product backlog state, or null if not found.</returns>
    public async Task<ProductBacklogStateDto?> GetBacklogStateAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/WorkItems/backlog-state/{productId}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GET api/WorkItems/backlog-state/{productId} failed with status {(int)response.StatusCode} {response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<ProductBacklogStateDto>(cancellationToken);
    }
}
