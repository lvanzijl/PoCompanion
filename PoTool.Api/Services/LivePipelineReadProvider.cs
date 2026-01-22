using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

/// <summary>
/// Live pipeline read provider that queries TFS directly without cache.
/// Used when DataSourceMode is Live.
/// DTOs only - no persistence, no EF, no repositories.
/// </summary>
public sealed class LivePipelineReadProvider : IPipelineReadProvider
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<LivePipelineReadProvider> _logger;

    public LivePipelineReadProvider(
        ITfsClient tfsClient,
        ILogger<LivePipelineReadProvider> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching all pipelines from TFS");
        
        // Fetch pipelines directly from TFS
        var pipelines = await _tfsClient.GetPipelinesAsync(cancellationToken);
        return pipelines;
    }

    public async Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline by ID from TFS: {PipelineId}", pipelineId);
        
        // Get all pipelines and find by ID
        // Note: TFS API doesn't always have a direct get-by-ID for pipelines, so we fetch all and filter
        var allPipelines = await GetAllAsync(cancellationToken);
        return allPipelines.FirstOrDefault(p => p.Id == pipelineId);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline runs for pipeline {PipelineId} from TFS", pipelineId);
        
        // First get the pipeline to validate it exists
        var pipeline = await GetByIdAsync(pipelineId, cancellationToken);
        if (pipeline == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found", pipelineId);
            return Enumerable.Empty<PipelineRunDto>();
        }
        
        // Fetch pipeline runs from TFS
        return await _tfsClient.GetPipelineRunsAsync(pipelineId, top, cancellationToken);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching all pipeline runs from TFS");
        
        // Get all pipelines first
        var allPipelines = await GetAllAsync(cancellationToken);
        
        // Get runs for each pipeline (in-memory aggregation)
        var allRuns = new List<PipelineRunDto>();
        foreach (var pipeline in allPipelines)
        {
            var runs = await GetRunsAsync(pipeline.Id, 100, cancellationToken);
            allRuns.AddRange(runs);
        }
        
        return allRuns;
    }

    public async Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "LivePipelineReadProvider: Fetching pipeline runs for {Count} pipelines with filters (branch: {Branch}, minTime: {MinTime})",
            pipelineIds.Count(), branchName ?? "none", minStartTime?.ToString("o") ?? "none");
        
        // Use the new bulk method with filtering from TFS client
        return await _tfsClient.GetPipelineRunsAsync(pipelineIds, branchName, minStartTime, top, cancellationToken);
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching all pipeline definitions from TFS");
        
        // In Live mode, we fetch pipeline definitions per repository
        // Since TFS API doesn't have a method to get all definitions directly,
        // we'll return empty for now as this would require knowing all repositories
        // In practice, callers should use GetDefinitionsByProductIdAsync or GetDefinitionsByRepositoryIdAsync
        _logger.LogWarning("GetAllDefinitionsAsync in Live mode is not fully supported - use product or repository filtering");
        return Enumerable.Empty<PipelineDefinitionDto>();
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline definitions for product {ProductId} from TFS", productId);
        
        // In Live mode, get all definitions and filter by product ID in-memory
        // This is a limitation of the Live mode for now
        _logger.LogWarning("GetDefinitionsByProductIdAsync in Live mode requires cached product-repository mapping");
        return Enumerable.Empty<PipelineDefinitionDto>();
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline definitions for repository {RepositoryId} from TFS", repositoryId);
        
        // In Live mode, this would require mapping repository ID to repository name
        // This is a limitation of the Live mode for now - requires repository lookup
        _logger.LogWarning("GetDefinitionsByRepositoryIdAsync in Live mode requires repository ID to name mapping");
        return Enumerable.Empty<PipelineDefinitionDto>();
    }
}
