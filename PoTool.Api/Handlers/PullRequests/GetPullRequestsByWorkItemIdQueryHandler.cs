using Mediator;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Returns cached pull requests linked to a specific work item ID.
/// </summary>
public sealed class GetPullRequestsByWorkItemIdQueryHandler
    : IQueryHandler<GetPullRequestsByWorkItemIdQuery, IEnumerable<PullRequestDto>>
{
    private readonly IPullRequestQueryStore _queryStore;

    public GetPullRequestsByWorkItemIdQueryHandler(IPullRequestQueryStore queryStore)
    {
        _queryStore = queryStore;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetPullRequestsByWorkItemIdQuery query,
        CancellationToken cancellationToken)
    {
        return await _queryStore.GetByWorkItemIdAsync(query.WorkItemId, cancellationToken);
    }
}
