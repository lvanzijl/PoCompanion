using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using PoTool.Api.Exceptions;
using PoTool.Core.Configuration;
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
    private const int DefaultMaxRunsPerPipeline = 100;
    
    private readonly ITfsClient _tfsClient;
    private readonly IProductRepository _productRepository;
    private readonly IRepositoryConfigRepository _repositoryConfigRepository;
    private readonly ILogger<LivePipelineReadProvider> _logger;
    private readonly IDataSourceModeProvider? _modeProvider;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public LivePipelineReadProvider(
        ITfsClient tfsClient,
        IProductRepository productRepository,
        IRepositoryConfigRepository repositoryConfigRepository,
        ILogger<LivePipelineReadProvider> logger)
        : this(tfsClient, productRepository, repositoryConfigRepository, logger, null, null)
    {
    }

    public LivePipelineReadProvider(
        ITfsClient tfsClient,
        IProductRepository productRepository,
        IRepositoryConfigRepository repositoryConfigRepository,
        ILogger<LivePipelineReadProvider> logger,
        IDataSourceModeProvider? modeProvider,
        IHttpContextAccessor? httpContextAccessor)
    {
        _tfsClient = tfsClient;
        _productRepository = productRepository;
        _repositoryConfigRepository = repositoryConfigRepository;
        _logger = logger;
        _modeProvider = modeProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetAllAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetAllAsync));
        _logger.LogDebug("LivePipelineReadProvider: Fetching all pipelines from TFS");
        
        // Fetch pipelines directly from TFS
        var pipelines = await _tfsClient.GetPipelinesAsync(cancellationToken);
        return pipelines;
    }

    public async Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetByIdAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetByIdAsync));
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline by ID from TFS: {PipelineId}", pipelineId);
        
        // Use direct get-by-ID method for better performance
        return await _tfsClient.GetPipelineByIdAsync(pipelineId, cancellationToken);
    }

    public async Task<IEnumerable<PipelineDto>> GetByIdsAsync(IEnumerable<int> pipelineIds, CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetByIdsAsync));
        var normalizedIds = pipelineIds
            .Distinct()
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return [];
        }

        var pipelines = new List<PipelineDto>(normalizedIds.Count);
        foreach (var pipelineId in normalizedIds)
        {
            var pipeline = await GetByIdAsync(pipelineId, cancellationToken);
            if (pipeline is not null)
            {
                pipelines.Add(pipeline);
            }
        }

        return pipelines;
    }

    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetRunsAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetRunsAsync));
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
        EnsureLiveUsageAllowed(nameof(GetAllRunsAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetAllRunsAsync));
        _logger.LogDebug("LivePipelineReadProvider: Fetching all pipeline runs from TFS");
        
        // Get all pipelines first
        var allPipelines = await GetAllAsync(cancellationToken);
        var pipelineIds = allPipelines.Select(p => p.Id).ToList();
        
        if (!pipelineIds.Any())
        {
            _logger.LogInformation("No pipelines found, returning empty runs list");
            return Enumerable.Empty<PipelineRunDto>();
        }
        
        // Use bulk method to get runs for all pipelines efficiently
        _logger.LogDebug("Fetching runs for {Count} pipelines using bulk method", pipelineIds.Count);
        return await _tfsClient.GetPipelineRunsAsync(pipelineIds, branchName: null, minStartTime: null, top: DefaultMaxRunsPerPipeline, cancellationToken);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        DateTimeOffset? maxStartTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetRunsForPipelinesAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetRunsForPipelinesAsync));
        _logger.LogDebug(
            "LivePipelineReadProvider: Fetching pipeline runs for {Count} pipelines with filters (branch: {Branch}, minTime: {MinTime})",
            pipelineIds.Count(), branchName ?? "none", minStartTime?.ToString("o") ?? "none");
        
        // Use the new bulk method with filtering from TFS client
        return await _tfsClient.GetPipelineRunsAsync(pipelineIds, branchName, minStartTime, top, cancellationToken);
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        EnsureLiveUsageAllowed(nameof(GetDefinitionsByProductIdAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetDefinitionsByProductIdAsync));
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
        EnsureLiveUsageAllowed(nameof(GetDefinitionsByRepositoryIdAsync));
        _logger.LogWarning("LivePipelineReadProvider.{Method} called — may indicate cache bypass", nameof(GetDefinitionsByRepositoryIdAsync));
        _logger.LogDebug("LivePipelineReadProvider: Fetching pipeline definitions for repository {RepositoryId} from TFS", repositoryId);
        
        // Note: This loads all repositories to find one by ID. 
        // For better performance, consider adding GetRepositoryByIdAsync to IRepositoryConfigRepository
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

    private void EnsureLiveUsageAllowed(string method)
    {
        if (_modeProvider?.Mode == DataSourceMode.Cache)
        {
            var blockedRoute = _httpContextAccessor?.HttpContext?.Request.Path.Value ?? "<unknown>";
            _logger.LogError(
                "[Violation] Route={Route} Mode=CacheOnly AttemptedProvider=Live Action=Blocked Method={Method}",
                blockedRoute,
                method);
            throw new InvalidDataSourceUsageException(blockedRoute, "CacheOnly", "Live");
        }

        var route = _httpContextAccessor?.HttpContext?.Request.Path.Value ?? "<unknown>";
        _logger.LogInformation(
            "[DataSourceMode] Route={Route} Mode=LiveAllowed Provider=Live Method={Method}",
            route,
            method);
    }
}
