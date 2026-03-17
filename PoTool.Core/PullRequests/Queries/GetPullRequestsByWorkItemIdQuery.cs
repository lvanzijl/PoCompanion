using Mediator;

using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to retrieve cached pull requests linked to a specific work item.
/// </summary>
public sealed record GetPullRequestsByWorkItemIdQuery(int WorkItemId) : IQuery<IEnumerable<PullRequestDto>>;
