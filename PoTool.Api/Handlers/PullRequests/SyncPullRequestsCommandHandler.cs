using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.PullRequests.Commands;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for SyncPullRequestsCommand.
/// Synchronizes pull requests from TFS to local cache.
/// Uses bulk fetch method to prevent N+1 query pattern.
/// </summary>
public sealed class SyncPullRequestsCommandHandler : ICommandHandler<SyncPullRequestsCommand, int>
{
    private readonly IPullRequestRepository _repository;
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<SyncPullRequestsCommandHandler> _logger;

    public SyncPullRequestsCommandHandler(
        IPullRequestRepository repository,
        ITfsClient tfsClient,
        ILogger<SyncPullRequestsCommandHandler> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<int> Handle(
        SyncPullRequestsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pull request sync");

        // Use the bulk method to fetch all PR data in a single call.
        // This prevents the N+1 pattern where we would call:
        // - GetPullRequestsAsync (1 call)
        // - GetPullRequestIterationsAsync (N calls, one per PR)
        // - GetPullRequestCommentsAsync (N calls, one per PR)
        // - GetPullRequestFileChangesAsync (M calls, one per iteration)
        // Instead, GetPullRequestsWithDetailsAsync fetches all data efficiently.
        var syncResult = await _tfsClient.GetPullRequestsWithDetailsAsync(cancellationToken: cancellationToken);

        // Log performance instrumentation showing call reduction
        _logger.LogInformation(
            "Bulk PR fetch completed with {TfsCallCount} TFS call(s) - retrieved {PrCount} PRs, {IterCount} iterations, {CommentCount} comments, {FileCount} file changes",
            syncResult.TfsCallCount,
            syncResult.PullRequests.Count,
            syncResult.Iterations.Count,
            syncResult.Comments.Count,
            syncResult.FileChanges.Count);

        // Save to repository
        await _repository.SaveAsync(syncResult.PullRequests.ToList(), cancellationToken);
        await _repository.SaveIterationsAsync(syncResult.Iterations.ToList(), cancellationToken);
        await _repository.SaveCommentsAsync(syncResult.Comments.ToList(), cancellationToken);
        await _repository.SaveFileChangesAsync(syncResult.FileChanges.ToList(), cancellationToken);

        _logger.LogInformation("Pull request sync completed. Synced {Count} PRs", syncResult.PullRequests.Count);
        return syncResult.PullRequests.Count;
    }
}
