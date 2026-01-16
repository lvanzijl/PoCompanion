using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Repositories;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.PullRequests.Commands;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for SyncPullRequestsCommand.
/// Synchronizes pull requests from TFS to local cache.
/// Uses bulk fetch method to prevent N+1 query pattern.
/// </summary>
public sealed class SyncPullRequestsCommandHandler : ICommandHandler<SyncPullRequestsCommand, int>
{
    private readonly IPullRequestRepository _repository;
    private readonly RepositoryRepository _repoRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<SyncPullRequestsCommandHandler> _logger;

    public SyncPullRequestsCommandHandler(
        IPullRequestRepository repository,
        RepositoryRepository repoRepository,
        IProductRepository productRepository,
        ITfsClient tfsClient,
        ILogger<SyncPullRequestsCommandHandler> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _repoRepository = repoRepository;
        _productRepository = productRepository;
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<int> Handle(
        SyncPullRequestsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pull request sync for products");

        // Get repositories for the specified products
        var repositories = await GetRepositoriesForProductsAsync(command.ProductIds, cancellationToken);

        if (!repositories.Any())
        {
            _logger.LogWarning("No repositories configured for the specified products");
            return 0;
        }

        int totalPrsSynced = 0;
        var allPrs = new List<PullRequestDto>();
        var allIterations = new List<PullRequestIterationDto>();
        var allComments = new List<PullRequestCommentDto>();
        var allFileChanges = new List<PullRequestFileChangeDto>();

        // Sync PRs for each repository
        foreach (var (productId, repoName) in repositories)
        {
            _logger.LogInformation("Syncing PRs for repository '{Repository}' (Product ID: {ProductId})", repoName, productId);

            // Fetch PRs for this repository
            var syncResult = await _tfsClient.GetPullRequestsWithDetailsAsync(
                repositoryName: repoName,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Bulk PR fetch for '{Repository}' completed with {TfsCallCount} TFS call(s) - retrieved {PrCount} PRs",
                repoName,
                syncResult.TfsCallCount,
                syncResult.PullRequests.Count);

            // Set ProductId on all PRs from this repository
            var prsWithProductId = syncResult.PullRequests.Select(pr => pr with { ProductId = productId }).ToList();
            
            allPrs.AddRange(prsWithProductId);
            allIterations.AddRange(syncResult.Iterations);
            allComments.AddRange(syncResult.Comments);
            allFileChanges.AddRange(syncResult.FileChanges);

            totalPrsSynced += syncResult.PullRequests.Count;
        }

        // Save all PRs, iterations, comments, and file changes to repository
        if (allPrs.Any())
        {
            await _repository.SaveAsync(allPrs, cancellationToken);
            await _repository.SaveIterationsAsync(allIterations, cancellationToken);
            await _repository.SaveCommentsAsync(allComments, cancellationToken);
            await _repository.SaveFileChangesAsync(allFileChanges, cancellationToken);
        }

        _logger.LogInformation(
            "Pull request sync completed. Synced {Count} PRs across {RepoCount} repositories",
            totalPrsSynced,
            repositories.Count);

        return totalPrsSynced;
    }

    private async Task<List<(int ProductId, string RepoName)>> GetRepositoriesForProductsAsync(
        List<int>? productIds,
        CancellationToken cancellationToken)
    {
        var result = new List<(int ProductId, string RepoName)>();

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
            result.AddRange(repos.Select(r => (productId, r.Name)));
        }

        return result;
    }
}
