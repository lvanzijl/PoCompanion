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
        return await _pipelinesClient.GetPipelinesAsync() ?? Array.Empty<PipelineDto>();
    }

    /// <summary>
    /// Gets aggregated metrics for all pipelines.
    /// </summary>
    public async Task<IEnumerable<PipelineMetricsDto>> GetMetricsAsync()
    {
        return await _pipelinesClient.GetMetricsAsync() ?? Array.Empty<PipelineMetricsDto>();
    }

    /// <summary>
    /// Gets runs for a specific pipeline.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100)
    {
        return await _pipelinesClient.GetRunsAsync(pipelineId, top) ?? Array.Empty<PipelineRunDto>();
    }

    /// <summary>
    /// Synchronizes pipelines from TFS.
    /// </summary>
    public async Task<PipelineSyncResult> SyncAsync(int runsPerPipeline = 50)
    {
        return await _pipelinesClient.CreateSyncAsync(runsPerPipeline);
    }
}
