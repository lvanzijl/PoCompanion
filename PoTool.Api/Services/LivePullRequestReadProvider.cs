using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Live pull request read provider that queries TFS directly without cache.
/// Used when DataSourceMode is Live.
/// DTOs only - no persistence, no EF, no repositories.
/// </summary>
public sealed class LivePullRequestReadProvider : IPullRequestReadProvider
{
    private readonly ITfsClient _tfsClient;
    private readonly IRepositoryConfigRepository _repositoryConfigRepository;
    private readonly ILogger<LivePullRequestReadProvider> _logger;

    public LivePullRequestReadProvider(
        ITfsClient tfsClient,
        IRepositoryConfigRepository repositoryConfigRepository,
        ILogger<LivePullRequestReadProvider> logger)
    {
        _tfsClient = tfsClient;
        _repositoryConfigRepository = repositoryConfigRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<PullRequestDto>> GetAllAsync(DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching all pull requests from TFS (fromDate: {FromDate})", fromDate);
        
        // Fetch pull requests directly from TFS with date filtering
        var pullRequests = await _tfsClient.GetPullRequestsAsync(
            repositoryName: null,
            fromDate: fromDate,
            toDate: null,
            cancellationToken);
        
        return pullRequests;
    }

    public async Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching pull requests by product IDs from TFS (fromDate: {FromDate})", fromDate);
        
        // If no product IDs specified, return all PRs
        if (productIds == null || productIds.Count == 0)
        {
            _logger.LogDebug("No product IDs specified, returning all pull requests");
            return await GetAllAsync(fromDate, cancellationToken);
        }
        
        // Load repository configurations for the specified products
        var reposByProduct = await _repositoryConfigRepository.GetRepositoriesByProductIdsAsync(productIds, cancellationToken);
        
        if (reposByProduct.Count == 0)
        {
            _logger.LogWarning("No repositories configured for product IDs: {ProductIds}", string.Join(", ", productIds));
            return Enumerable.Empty<PullRequestDto>();
        }
        
        var totalRepoCount = reposByProduct.Values.Sum(repos => repos.Count);
        _logger.LogDebug("Found {RepoCount} repositories across {ProductCount} products", totalRepoCount, reposByProduct.Count);
        
        // Fetch PRs for each repository and map to product
        var allPullRequests = new List<PullRequestDto>();
        
        foreach (var (productId, repositories) in reposByProduct)
        {
            if (repositories.Count == 0)
            {
                _logger.LogDebug("Product {ProductId} has no configured repositories, skipping", productId);
                continue;
            }
            
            _logger.LogDebug("Fetching PRs for Product {ProductId} from {RepoCount} repositories: {RepoNames}", 
                productId, repositories.Count, string.Join(", ", repositories.Select(r => r.Name)));
            
            foreach (var repo in repositories)
            {
                try
                {
                    // Fetch PRs for this specific repository
                    var repoPullRequests = await _tfsClient.GetPullRequestsAsync(
                        repositoryName: repo.Name,
                        fromDate: fromDate,
                        toDate: null,
                        cancellationToken);
                    
                    // Map PRs to include ProductId based on repository-to-product mapping
                    var mappedPRs = repoPullRequests.Select(pr => pr with { ProductId = productId });
                    allPullRequests.AddRange(mappedPRs);
                    
                    _logger.LogDebug("Retrieved {Count} PRs from repository {RepoName} for Product {ProductId}", 
                        repoPullRequests.Count(), repo.Name, productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch PRs from repository {RepoName} for Product {ProductId}", repo.Name, productId);
                    // Continue with other repositories even if one fails
                }
            }
        }
        
        _logger.LogInformation("Retrieved total of {Count} PRs for {ProductCount} products from {RepoCount} repositories", 
            allPullRequests.Count, productIds.Count, totalRepoCount);
        
        return allPullRequests;
    }

    public async Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching pull request by ID from TFS: {PullRequestId}", pullRequestId);
        
        // TFS API doesn't have a direct get-by-ID for PRs
        // Optimization: Only fetch PRs from configured repositories instead of all PRs
        var configuredRepositories = await _repositoryConfigRepository.GetAllRepositoriesAsync(cancellationToken);
        var repositoryList = configuredRepositories.ToList();
        
        if (repositoryList.Count == 0)
        {
            _logger.LogWarning("No repositories configured in the system, cannot fetch PR {PullRequestId}", pullRequestId);
            return null;
        }
        
        _logger.LogDebug("Searching for PR {PullRequestId} across {RepoCount} configured repositories", 
            pullRequestId, repositoryList.Count);
        
        // Search for the PR in each configured repository
        foreach (var repo in repositoryList)
        {
            try
            {
                var repoPullRequests = await _tfsClient.GetPullRequestsAsync(
                    repositoryName: repo.Name,
                    fromDate: null,
                    toDate: null,
                    cancellationToken);
                
                var foundPr = repoPullRequests.FirstOrDefault(pr => pr.Id == pullRequestId);
                if (foundPr != null)
                {
                    _logger.LogDebug("Found PR {PullRequestId} in repository {RepoName}", pullRequestId, repo.Name);
                    return foundPr;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch PRs from repository {RepoName} while searching for PR {PullRequestId}", 
                    repo.Name, pullRequestId);
                // Continue searching in other repositories
            }
        }
        
        _logger.LogWarning("Pull request {PullRequestId} not found in any of the {RepoCount} configured repositories", 
            pullRequestId, repositoryList.Count);
        return null;
    }

    public async Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching iterations for PR {PullRequestId} from TFS", pullRequestId);
        
        // First get the PR to get its repository name
        var pr = await GetByIdAsync(pullRequestId, cancellationToken);
        if (pr == null)
        {
            _logger.LogWarning("Pull request {PullRequestId} not found", pullRequestId);
            return Enumerable.Empty<PullRequestIterationDto>();
        }
        
        // Fetch iterations from TFS
        return await _tfsClient.GetPullRequestIterationsAsync(pullRequestId, pr.RepositoryName, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching iterations for PR {PullRequestId} from TFS (repository: {RepositoryName})", pullRequestId, repositoryName);
        
        // Fetch iterations from TFS directly using provided repository name
        return await _tfsClient.GetPullRequestIterationsAsync(pullRequestId, repositoryName, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching comments for PR {PullRequestId} from TFS", pullRequestId);
        
        // First get the PR to get its repository name
        var pr = await GetByIdAsync(pullRequestId, cancellationToken);
        if (pr == null)
        {
            _logger.LogWarning("Pull request {PullRequestId} not found", pullRequestId);
            return Enumerable.Empty<PullRequestCommentDto>();
        }
        
        // Fetch comments from TFS
        return await _tfsClient.GetPullRequestCommentsAsync(pullRequestId, pr.RepositoryName, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching comments for PR {PullRequestId} from TFS (repository: {RepositoryName})", pullRequestId, repositoryName);
        
        // Fetch comments from TFS directly using provided repository name
        return await _tfsClient.GetPullRequestCommentsAsync(pullRequestId, repositoryName, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching file changes for PR {PullRequestId} from TFS", pullRequestId);
        
        // First get the PR to get its repository name
        var pr = await GetByIdAsync(pullRequestId, cancellationToken);
        if (pr == null)
        {
            _logger.LogWarning("Pull request {PullRequestId} not found", pullRequestId);
            return Enumerable.Empty<PullRequestFileChangeDto>();
        }
        
        // Get iterations to find the latest one
        var iterations = await GetIterationsAsync(pullRequestId, cancellationToken);
        var latestIteration = iterations.OrderByDescending(i => i.IterationNumber).FirstOrDefault();
        
        if (latestIteration == null)
        {
            _logger.LogWarning("No iterations found for pull request {PullRequestId}", pullRequestId);
            return Enumerable.Empty<PullRequestFileChangeDto>();
        }
        
        // Fetch file changes from TFS
        return await _tfsClient.GetPullRequestFileChangesAsync(pullRequestId, pr.RepositoryName, latestIteration.IterationNumber, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching file changes for PR {PullRequestId} from TFS (repository: {RepositoryName})", pullRequestId, repositoryName);
        
        // Get iterations to find the latest one - use optimized overload
        var iterations = await GetIterationsAsync(pullRequestId, repositoryName, cancellationToken);
        var latestIteration = iterations.OrderByDescending(i => i.IterationNumber).FirstOrDefault();
        
        if (latestIteration == null)
        {
            _logger.LogWarning("No iterations found for pull request {PullRequestId}", pullRequestId);
            return Enumerable.Empty<PullRequestFileChangeDto>();
        }
        
        // Fetch file changes from TFS directly using provided repository name
        return await _tfsClient.GetPullRequestFileChangesAsync(pullRequestId, repositoryName, latestIteration.IterationNumber, cancellationToken);
    }
}
