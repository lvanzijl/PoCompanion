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
    /// Gets YAML pipeline definitions.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsAsync(int? productId = null, int? repositoryId = null)
    {
        return await _pipelinesClient.GetDefinitionsAsync(productId, repositoryId) ?? Array.Empty<PipelineDefinitionDto>();
    }
}
