using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve comments for a specific pull request.
/// </summary>
public sealed record GetPullRequestCommentsQuery(int PullRequestId) : IQuery<IEnumerable<PullRequestCommentDto>>;
