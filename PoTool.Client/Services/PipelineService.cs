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
        var response = await _pipelinesClient.GetAllAsync();
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault(response, Array.Empty<PipelineDto>());
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
        var response = await _pipelinesClient.GetMetricsAsync(productIds, fromDate, toDate, CancellationToken.None);
        var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault<object>(response);
        return payload is null
            ? new CanonicalClientResponse<IReadOnlyList<PipelineMetricsDto>>(Array.Empty<PipelineMetricsDto>())
            : CanonicalClientResponseFactory.CreateGenerated<IReadOnlyList<PipelineMetricsDto>>(payload, CanonicalFilterKind.Pipeline);
    }

    /// <summary>
    /// Gets runs for a specific pipeline.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100)
    {
        var response = await _pipelinesClient.GetRunsAsync(pipelineId, top);
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault(response, Array.Empty<PipelineRunDto>());
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

        var response = await _pipelinesClient.GetRunsForProductsAsync(productIds, fromDate, toDate, CancellationToken.None);
        var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault<object>(response);
        return payload is null
            ? new CanonicalClientResponse<IReadOnlyList<PipelineRunDto>>(Array.Empty<PipelineRunDto>())
            : CanonicalClientResponseFactory.CreateGenerated<IReadOnlyList<PipelineRunDto>>(payload, CanonicalFilterKind.Pipeline);
    }

    /// <summary>
    /// Gets YAML pipeline definitions.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsAsync(int? productId = null, int? repositoryId = null)
    {
        return await _pipelinesClient.GetDefinitionsAsync(productId, repositoryId) ?? Array.Empty<PipelineDefinitionDto>();
    }
}
