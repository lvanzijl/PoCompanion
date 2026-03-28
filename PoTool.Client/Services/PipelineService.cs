using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for interacting with the Pipelines API.
/// </summary>
public class PipelineService
{
    private readonly IPipelinesClient _pipelinesClient;

    public PipelineService(IPipelinesClient pipelinesClient)
    {
        _pipelinesClient = pipelinesClient;
    }

    /// <summary>
    /// Gets all cached pipelines.
    /// </summary>
    public async Task<IEnumerable<PipelineDto>> GetAllAsync()
    {
        return await _pipelinesClient.GetAllAsync() ?? Array.Empty<PipelineDto>();
    }

    /// <summary>
    /// Gets aggregated metrics for pipelines, optionally filtered by products.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by</param>
    public async Task<CanonicalClientResponse<IReadOnlyList<PipelineMetricsDto>>> GetMetricsAsync(
        string? productIds = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null)
    {
        var response = await _pipelinesClient.GetMetricsEnvelopeAsync(productIds, fromDate, toDate, CancellationToken.None);
        return CanonicalClientResponseFactory.Create(response);
    }

    /// <summary>
    /// Gets runs for a specific pipeline.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100)
    {
        return await _pipelinesClient.GetRunsAsync(pipelineId, top) ?? Array.Empty<PipelineRunDto>();
    }

    /// <summary>
    /// Gets all pipeline runs for specified products (last 6 months, main branch).
    /// Uses the canonical filter envelope and returns only the payload data to the UI.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by (currently uses only first ID)</param>
    public async Task<CanonicalClientResponse<IReadOnlyList<PipelineRunDto>>> GetRunsForProductsAsync(
        string? productIds = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null)
    {
        if (string.IsNullOrWhiteSpace(productIds))
        {
            return new CanonicalClientResponse<IReadOnlyList<PipelineRunDto>>(Array.Empty<PipelineRunDto>());
        }
        
        // Parse and validate the first product ID
        var firstProductIdStr = productIds.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(firstProductIdStr) || 
            !int.TryParse(firstProductIdStr, out var firstId) || 
            firstId <= 0)
        {
            return new CanonicalClientResponse<IReadOnlyList<PipelineRunDto>>(Array.Empty<PipelineRunDto>());
        }

        var response = await _pipelinesClient.GetRunsForProductsEnvelopeAsync(productIds, fromDate, toDate, CancellationToken.None);
        return CanonicalClientResponseFactory.Create(response);
    }

    /// <summary>
    /// Gets YAML pipeline definitions.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsAsync(int? productId = null, int? repositoryId = null)
    {
        return await _pipelinesClient.GetDefinitionsAsync(productId, repositoryId) ?? Array.Empty<PipelineDefinitionDto>();
    }
}
