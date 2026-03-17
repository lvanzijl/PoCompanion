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
        var linkedPullRequestIds = _context.PullRequestWorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemId == query.WorkItemId)
            .Select(link => link.PullRequestId)
            .Distinct();

        var pullRequests = await _context.PullRequests
            .AsNoTracking()
            .Where(pullRequest => linkedPullRequestIds.Contains(pullRequest.Id))
            .OrderByDescending(pullRequest => pullRequest.CreatedDateUtc)
            .ThenByDescending(pullRequest => pullRequest.Id)
            .Select(pullRequest => new PullRequestDto(
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
            .ToListAsync(cancellationToken);

        return pullRequests;
    }
}
