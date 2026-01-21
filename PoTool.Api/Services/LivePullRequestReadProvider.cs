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
    private readonly ILogger<LivePullRequestReadProvider> _logger;

    public LivePullRequestReadProvider(
        ITfsClient tfsClient,
        ILogger<LivePullRequestReadProvider> logger)
    {
        _tfsClient = tfsClient;
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
        
        // Get all pull requests with date filter and filter by product ID in-memory
        // In Live mode, we don't have a direct way to filter by product in TFS
        var allPullRequests = await GetAllAsync(fromDate, cancellationToken);
        
        if (productIds == null || productIds.Count == 0)
        {
            return allPullRequests;
        }
        
        return allPullRequests.Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value));
    }

    public async Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("LivePullRequestReadProvider: Fetching pull request by ID from TFS: {PullRequestId}", pullRequestId);
        
        // Get all pull requests and find by ID
        // Note: TFS API doesn't have a direct get-by-ID for PRs, so we fetch all and filter
        var allPullRequests = await GetAllAsync(null, cancellationToken);
        return allPullRequests.FirstOrDefault(pr => pr.Id == pullRequestId);
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
}
