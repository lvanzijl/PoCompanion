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

        try
        {
            // ========================================
            // STEP A: COLLECT (network calls, no database access)
            // ========================================
            _logger.LogInformation("Step A (Collect): Starting PR data collection from TFS");

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

            // Collect PRs for each repository (network calls only, no DB writes)
            foreach (var (productId, repoName) in repositories)
            {
                _logger.LogInformation("Collecting PRs for repository '{Repository}' (Product ID: {ProductId})", repoName, productId);

                // Fetch PRs for this repository (network call - returns in-memory data)
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

            _logger.LogInformation(
                "Step A (Collect): Completed. Collected {PrCount} PRs, {IterCount} iterations, {CommentCount} comments, {FileCount} file changes",
                totalPrsSynced, allIterations.Count, allComments.Count, allFileChanges.Count);

            // ========================================
            // STEP B: PERSIST (single atomic database operation)
            // ========================================
            if (allPrs.Any())
            {
                _logger.LogInformation("Step B (Persist): Starting atomic persistence of all PR data");

                // Save all data in a single atomic operation to avoid concurrent DB access
                await _repository.SaveBulkAsync(
                    allPrs,
                    allIterations,
                    allComments,
                    allFileChanges,
                    cancellationToken);

                _logger.LogInformation("Step B (Persist): Completed successfully");

                // ========================================
                // STEP C: BACKFILL (assign timeframe iterations to old PRs)
                // ========================================
                _logger.LogInformation("Step C (Backfill): Starting timeframe iteration assignment for existing PRs");
                await _repository.BackfillTimeframeIterationsAsync(cancellationToken);
                _logger.LogInformation("Step C (Backfill): Completed successfully");
            }
            else
            {
                _logger.LogInformation("Step B (Persist): Skipped - no PRs to save");
            }

            _logger.LogInformation(
                "Pull request sync completed. Synced {Count} PRs across {RepoCount} repositories",
                totalPrsSynced,
                repositories.Count);

            return totalPrsSynced;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pull request sync failed: {ErrorMessage}", ex.Message);
            throw;
        }
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
