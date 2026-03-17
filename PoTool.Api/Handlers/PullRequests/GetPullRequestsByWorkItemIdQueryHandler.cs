using Mediator;
using Microsoft.EntityFrameworkCore;

using PoTool.Api.Persistence;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Returns cached pull requests linked to a specific work item ID.
/// </summary>
public sealed class GetPullRequestsByWorkItemIdQueryHandler
    : IQueryHandler<GetPullRequestsByWorkItemIdQuery, IEnumerable<PullRequestDto>>
{
    private readonly PoToolDbContext _context;

    public GetPullRequestsByWorkItemIdQueryHandler(PoToolDbContext context)
    {
        _context = context;
    }

    public async ValueTask<IEnumerable<PullRequestDto>> Handle(
        GetPullRequestsByWorkItemIdQuery query,
        CancellationToken cancellationToken)
    {
        var pullRequests = await (
                from link in _context.PullRequestWorkItemLinks.AsNoTracking()
                join pullRequest in _context.PullRequests.AsNoTracking()
                    on link.PullRequestId equals pullRequest.Id
                where link.WorkItemId == query.WorkItemId
                orderby pullRequest.CreatedDateUtc descending, pullRequest.Id descending
                select new PullRequestDto(
                    pullRequest.Id,
                    pullRequest.RepositoryName,
                    pullRequest.Title,
                    pullRequest.CreatedBy,
                    pullRequest.CreatedDate,
                    pullRequest.CompletedDate,
                    pullRequest.Status,
                    pullRequest.IterationPath,
                    pullRequest.SourceBranch,
                    pullRequest.TargetBranch,
                    pullRequest.RetrievedAt,
                    pullRequest.ProductId))
            .Distinct()
            .ToListAsync(cancellationToken);

        return pullRequests;
    }
}
