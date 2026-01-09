using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve file changes for a specific pull request.
/// </summary>
public sealed record GetPullRequestFileChangesQuery(int PullRequestId) : IQuery<IEnumerable<PullRequestFileChangeDto>>;
