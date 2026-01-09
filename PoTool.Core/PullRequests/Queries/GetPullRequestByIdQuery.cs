using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve a specific pull request by ID.
/// </summary>
public sealed record GetPullRequestByIdQuery(int PullRequestId) : IQuery<PullRequestDto?>;
