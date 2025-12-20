using Mediator;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve iterations for a specific pull request.
/// </summary>
public sealed record GetPullRequestIterationsQuery(int PullRequestId) : IQuery<IEnumerable<PullRequestIterationDto>>;
