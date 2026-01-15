using Mediator;

namespace PoTool.Core.PullRequests.Commands;

/// <summary>
/// Command to synchronize pull requests from TFS to local cache.
/// </summary>
/// <param name="ProductIds">List of product IDs to sync PRs for. If null or empty, syncs for all products.</param>
public sealed record SyncPullRequestsCommand(
    List<int>? ProductIds = null
) : ICommand<int>;
