using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines;
using PoTool.Core.Pipelines.Commands;
using PoTool.Api.Repositories;

namespace PoTool.Api.Handlers.Pipelines;

/// <summary>
/// Handler for SyncPipelinesCommand.
/// Synchronizes pipelines from TFS to local cache.
/// Now includes YAML pipeline definition sync per product/repository.
/// </summary>
public sealed class SyncPipelinesCommandHandler : ICommandHandler<SyncPipelinesCommand, PipelineSyncResult>
{
    private readonly IPipelineRepository _repository;
    private readonly RepositoryRepository _repoRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<SyncPipelinesCommandHandler> _logger;

    public SyncPipelinesCommandHandler(
        IPipelineRepository repository,
        RepositoryRepository repoRepository,
        IProductRepository productRepository,
        ITfsClient tfsClient,
        ILogger<SyncPipelinesCommandHandler> logger)
    {
        _repository = repository;
        _repoRepository = repoRepository;
        _productRepository = productRepository;
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<PipelineSyncResult> Handle(
        SyncPipelinesCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pipeline sync with {RunsPerPipeline} runs per pipeline", command.RunsPerPipeline);

        // Sync pipeline runs (existing functionality - in-memory)
        var syncResult = await _tfsClient.GetPipelinesWithRunsAsync(command.RunsPerPipeline, cancellationToken);

        _logger.LogInformation(
            "Pipeline fetch completed with {TfsCallCount} TFS call(s) - retrieved {PipelineCount} pipelines, {RunCount} runs",
            syncResult.TfsCallCount,
            syncResult.Pipelines.Count,
            syncResult.Runs.Count);

        // Save to repository (in-memory)
        await _repository.SaveAsync(syncResult, cancellationToken);

        // Sync pipeline definitions (new functionality - database)
        if (command.SyncDefinitions)
        {
            await SyncPipelineDefinitionsAsync(command.ProductIds, cancellationToken);
        }

        _logger.LogInformation("Pipeline sync completed. Synced {Count} pipelines", syncResult.Pipelines.Count);
        return syncResult;
    }

    private async Task SyncPipelineDefinitionsAsync(
        List<int>? productIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pipeline definitions sync");

        // Get repositories for the specified products
        var repositories = await GetRepositoriesForProductsAsync(productIds, cancellationToken);

        if (!repositories.Any())
        {
            _logger.LogWarning("No repositories configured for the specified products");
            return;
        }

        var allDefinitions = new List<PipelineDefinitionDto>();
        var affectedProductIds = new HashSet<int>();

        // Fetch pipeline definitions for each repository
        foreach (var (productId, repoId, repoName) in repositories)
        {
            _logger.LogInformation(
                "Fetching pipeline definitions for repository '{Repository}' (Product ID: {ProductId}, Repo ID: {RepoId})",
                repoName, productId, repoId);

            try
            {
                var definitions = await _tfsClient.GetPipelineDefinitionsForRepositoryAsync(repoName, cancellationToken);
                var definitionsList = definitions.ToList();

                _logger.LogInformation(
                    "Retrieved {Count} pipeline definitions for repository '{Repository}'",
                    definitionsList.Count, repoName);

                // Set ProductId and RepositoryId on all definitions
                var enrichedDefinitions = definitionsList.Select(def => def with
                {
                    ProductId = productId,
                    RepositoryId = repoId
                }).ToList();

                allDefinitions.AddRange(enrichedDefinitions);
                affectedProductIds.Add(productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to fetch pipeline definitions for repository '{Repository}' (Product ID: {ProductId})",
                    repoName, productId);
            }
        }

        // Save all definitions to database
        if (allDefinitions.Any())
        {
            await _repository.SaveDefinitionsAsync(allDefinitions, affectedProductIds, cancellationToken);

            _logger.LogInformation(
                "Pipeline definitions sync completed. Synced {Count} definitions across {RepoCount} repositories for products: {ProductIds}",
                allDefinitions.Count,
                repositories.Count,
                string.Join(", ", affectedProductIds));
        }
        else
        {
            _logger.LogWarning("No pipeline definitions found to sync");
        }
    }

    private async Task<List<(int ProductId, int RepoId, string RepoName)>> GetRepositoriesForProductsAsync(
        List<int>? productIds,
        CancellationToken cancellationToken)
    {
        var result = new List<(int ProductId, int RepoId, string RepoName)>();

        if (productIds == null || !productIds.Any())
        {
            // If no product IDs specified, get all products
            var allProducts = await _productRepository.GetAllProductsAsync(cancellationToken);
            productIds = allProducts.Select(p => p.Id).ToList();
        }

        // Fetch repositories for all products in a single query (avoids N+1 pattern)
        var repositoriesByProduct = await _repoRepository.GetRepositoriesByProductIdsAsync(productIds, cancellationToken);

        foreach (var (productId, repos) in repositoriesByProduct)
        {
            result.AddRange(repos.Select(r => (productId, r.Id, r.Name)));
        }

        return result;
    }
}
