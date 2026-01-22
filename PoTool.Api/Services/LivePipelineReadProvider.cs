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
    private readonly IProductRepository _productRepository;
    private readonly IRepositoryConfigRepository _repositoryConfigRepository;
    private readonly ILogger<LivePipelineReadProvider> _logger;

    public LivePipelineReadProvider(
        ITfsClient tfsClient,
        IProductRepository productRepository,
        IRepositoryConfigRepository repositoryConfigRepository,
        ILogger<LivePipelineReadProvider> logger)
    {
        _tfsClient = tfsClient;
        _productRepository = productRepository;
        _repositoryConfigRepository = repositoryConfigRepository;
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

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline definitions for product {ProductId} from TFS", productId);
        
        // Get repositories for the product
        var repositories = await _repositoryConfigRepository.GetRepositoriesByProductAsync(productId, cancellationToken);
        var repositoriesList = repositories.ToList();
        
        if (!repositoriesList.Any())
        {
            _logger.LogWarning("No repositories found for product {ProductId}", productId);
            return Enumerable.Empty<PipelineDefinitionDto>();
        }
        
        // Fetch pipeline definitions for each repository from TFS
        var allDefinitions = new List<PipelineDefinitionDto>();
        foreach (var repo in repositoriesList)
        {
            var definitions = await _tfsClient.GetPipelineDefinitionsForRepositoryAsync(repo.Name, cancellationToken);
            
            // Create new instances with ProductId and RepositoryId set
            foreach (var definition in definitions)
            {
                var enrichedDefinition = definition with 
                { 
                    ProductId = productId, 
                    RepositoryId = repo.Id 
                };
                allDefinitions.Add(enrichedDefinition);
            }
        }
        
        _logger.LogInformation(
            "Fetched {Count} pipeline definitions for product {ProductId} from {RepoCount} repositories",
            allDefinitions.Count, productId, repositoriesList.Count);
        
        return allDefinitions;
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline definitions for repository {RepositoryId} from TFS", repositoryId);
        
        // Get all repositories to find the repository by ID
        var allRepositories = await _repositoryConfigRepository.GetAllRepositoriesAsync(cancellationToken);
        var repository = allRepositories.FirstOrDefault(r => r.Id == repositoryId);
        
        if (repository == null)
        {
            _logger.LogWarning("Repository with ID {RepositoryId} not found", repositoryId);
            return Enumerable.Empty<PipelineDefinitionDto>();
        }
        
        // Fetch pipeline definitions for the repository from TFS
        var definitions = await _tfsClient.GetPipelineDefinitionsForRepositoryAsync(repository.Name, cancellationToken);
        
        // Create new instances with ProductId and RepositoryId set
        var definitionsList = definitions.Select(definition => definition with
        {
            ProductId = repository.ProductId,
            RepositoryId = repositoryId
        }).ToList();
        
        _logger.LogInformation(
            "Fetched {Count} pipeline definitions for repository {RepositoryId} ({RepositoryName})",
            definitionsList.Count, repositoryId, repository.Name);
        
        return definitionsList;
    }
}
