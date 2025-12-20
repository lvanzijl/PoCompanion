using Mediator;

namespace PoTool.Core.PullRequests.Commands;

/// <summary>
/// Command to synchronize pull requests from TFS to local cache.
/// </summary>
public sealed record SyncPullRequestsCommand : ICommand<int>;
