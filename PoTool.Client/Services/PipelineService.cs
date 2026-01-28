using PoTool.Client.ApiClient;

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
    public async Task<IEnumerable<PipelineMetricsDto>> GetMetricsAsync(string? productIds = null)
    {
        return await _pipelinesClient.GetMetricsAsync(productIds) ?? Array.Empty<PipelineMetricsDto>();
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
    /// Note: This is a temporary client-side implementation that queries individual pipelines.
    /// Once the API client is regenerated, this will use the new /api/Pipelines/runs endpoint.
    /// Current limitations: Only queries first 10 pipelines, 100 runs each.
    /// </summary>
    /// <param name="productIds">Optional comma-separated product IDs to filter by (currently uses only first ID)</param>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsForProductsAsync(string? productIds = null)
    {
        // TODO: Replace with direct API call once client is regenerated:
        // return await _pipelinesClient.GetRunsForProductsAsync(productIds) ?? Array.Empty<PipelineRunDto>();
        
        // Get pipeline definitions for the first product (temporary limitation)
        var definitions = (await GetDefinitionsAsync(
            productId: !string.IsNullOrWhiteSpace(productIds) && int.TryParse(productIds.Split(',').FirstOrDefault(), out var firstId) ? firstId : null
        )).ToList();

        if (definitions.Count == 0)
        {
            return Array.Empty<PipelineRunDto>();
        }

        // Get runs for each pipeline (limited to first 10 pipelines to avoid overwhelming the API)
        var allRuns = new List<PipelineRunDto>();
        var pipelinesToQuery = definitions.Take(10).ToList();

        foreach (var definition in pipelinesToQuery)
        {
            try
            {
                var runs = await GetRunsAsync(definition.PipelineDefinitionId, top: 100);
                allRuns.AddRange(runs);
            }
            catch (Exception)
            {
                // Continue if one pipeline fails - individual pipeline errors shouldn't fail entire load
                // Once proper API endpoint is in place, error handling will be centralized
            }
        }

        return allRuns;
    }

    /// <summary>
    /// Gets YAML pipeline definitions.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsAsync(int? productId = null, int? repositoryId = null)
    {
        return await _pipelinesClient.GetDefinitionsAsync(productId, repositoryId) ?? Array.Empty<PipelineDefinitionDto>();
    }
}
