using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.PullRequests.Filters;
using PoTool.Core.WorkItems;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

public sealed class EfPullRequestQueryStore : IPullRequestQueryStore
{
    private readonly PoToolDbContext _context;

    public EfPullRequestQueryStore(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<PullRequestInsightsQueryData> GetInsightsDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        var configuration = await _context.TfsConfigs
            .AsNoTracking()
            .Select(config => new PullRequestConfigurationInfo(config.Url, config.Project))
            .FirstOrDefaultAsync(cancellationToken);

        var teamName = await LoadTeamNameAsync(filter, cancellationToken);
        var pullRequests = await LoadScopedPullRequestsAsync(filter, cancellationToken);
        var pullRequestIds = pullRequests.Select(pullRequest => pullRequest.Id).ToList();

        var iterationsByPullRequestId = await LoadIterationCountsAsync(pullRequestIds, cancellationToken);
        var commentsByPullRequestId = await LoadCommentCountsAsync(pullRequestIds, cancellationToken);
        var distinctFilesByPullRequestId = await LoadDistinctFileCountsAsync(pullRequestIds, cancellationToken);

        return new PullRequestInsightsQueryData(
            configuration,
            teamName,
            pullRequests,
            iterationsByPullRequestId,
            commentsByPullRequestId,
            distinctFilesByPullRequestId);
    }

    public async Task<IReadOnlyList<PullRequestDto>> GetScopedPullRequestsAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        return await LoadScopedPullRequestsAsync(filter, cancellationToken);
    }

    public async Task<PullRequestMetricsQueryData> GetMetricsDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        var pullRequests = await LoadScopedPullRequestsAsync(filter, cancellationToken);
        var pullRequestIds = pullRequests
            .Select(pullRequest => pullRequest.Id)
            .Distinct()
            .ToArray();

        var iterationsByPullRequestId = await LoadIterationsByPullRequestIdAsync(pullRequestIds, cancellationToken);
        var commentsByPullRequestId = await LoadCommentsByPullRequestIdAsync(pullRequestIds, cancellationToken);
        var fileChangesByPullRequestId = await LoadFileChangesByPullRequestIdAsync(pullRequestIds, cancellationToken);

        return new PullRequestMetricsQueryData(
            pullRequests,
            iterationsByPullRequestId,
            commentsByPullRequestId,
            fileChangesByPullRequestId);
    }

    public async Task<PrDeliveryInsightsQueryData> GetDeliveryInsightsDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        var teamName = await LoadTeamNameAsync(filter, cancellationToken);
        var sprintName = await LoadSprintNameAsync(filter.SprintId, cancellationToken);
        var pullRequests = await LoadScopedPullRequestsAsync(filter, cancellationToken);
        var pullRequestIds = pullRequests.Select(pullRequest => pullRequest.Id).ToList();

        var iterationsByPullRequestId = await LoadIterationCountsAsync(pullRequestIds, cancellationToken);
        var distinctFilesByPullRequestId = await LoadDistinctFileCountsAsync(pullRequestIds, cancellationToken);

        var workItemLinkRows = pullRequestIds.Count == 0
            ? []
            : await _context.PullRequestWorkItemLinks
                .AsNoTracking()
                .Where(link => pullRequestIds.Contains(link.PullRequestId))
                .Select(link => new { link.PullRequestId, link.WorkItemId })
                .ToListAsync(cancellationToken);

        var linkedWorkItemIdsByPullRequestId = workItemLinkRows
            .GroupBy(link => link.PullRequestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(link => link.WorkItemId).ToList());

        var workItemsById = await _context.WorkItems
            .AsNoTracking()
            .Select(workItem => new PullRequestWorkItemNode(
                workItem.TfsId,
                workItem.Type,
                workItem.Title,
                workItem.ParentTfsId))
            .ToDictionaryAsync(workItem => workItem.TfsId, cancellationToken);

        var pbiCountRows = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Type == WorkItemType.Pbi)
            .Select(workItem => new { workItem.ParentTfsId })
            .ToListAsync(cancellationToken);

        var pbiCountByFeatureId = pbiCountRows
            .Where(row => row.ParentTfsId.HasValue)
            .GroupBy(row => row.ParentTfsId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return new PrDeliveryInsightsQueryData(
            teamName,
            sprintName,
            pullRequests,
            iterationsByPullRequestId,
            distinctFilesByPullRequestId,
            linkedWorkItemIdsByPullRequestId,
            workItemsById,
            pbiCountByFeatureId);
    }

    public async Task<PrSprintTrendQueryData> GetSprintTrendDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        var sprintIdList = filter.SprintIds.Distinct().ToList();
        var sprints = await _context.Sprints
            .Where(sprint => sprintIdList.Contains(sprint.Id) && sprint.StartDateUtc.HasValue && sprint.EndDateUtc.HasValue)
            .OrderBy(sprint => sprint.StartDateUtc)
            .AsNoTracking()
            .Select(sprint => new PrSprintWindow(
                sprint.Id,
                sprint.Name,
                sprint.StartDateUtc!.Value,
                sprint.EndDateUtc!.Value,
                sprint.StartUtc,
                sprint.EndUtc))
            .ToListAsync(cancellationToken);

        var pullRequests = await PullRequestFiltering.ApplyScope(
                _context.PullRequests.AsNoTracking(),
                filter)
            .Select(pullRequest => new PrSprintTrendPullRequest(
                pullRequest.Id,
                pullRequest.CreatedBy,
                pullRequest.CreatedDateUtc,
                pullRequest.CompletedDate,
                pullRequest.Status))
            .ToListAsync(cancellationToken);

        var pullRequestIds = pullRequests.Select(pullRequest => pullRequest.Id).ToList();

        var fileChanges = pullRequestIds.Count == 0
            ? []
            : await _context.PullRequestFileChanges
                .Where(fileChange => pullRequestIds.Contains(fileChange.PullRequestId))
                .Select(fileChange => new PrSprintTrendFileChange(
                    fileChange.PullRequestId,
                    fileChange.FilePath,
                    fileChange.LinesAdded,
                    fileChange.LinesDeleted))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        var comments = pullRequestIds.Count == 0
            ? []
            : await _context.PullRequestComments
                .Where(comment => pullRequestIds.Contains(comment.PullRequestId))
                .Select(comment => new PrSprintTrendComment(
                    comment.PullRequestId,
                    comment.Author,
                    comment.CreatedDateUtc))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        return new PrSprintTrendQueryData(sprints, pullRequests, fileChanges, comments);
    }

    public async Task<IReadOnlyList<PullRequestDto>> GetByWorkItemIdAsync(
        int workItemId,
        CancellationToken cancellationToken)
    {
        var linkedPullRequestIds = _context.PullRequestWorkItemLinks
            .AsNoTracking()
            .Where(link => link.WorkItemId == workItemId)
            .Select(link => link.PullRequestId)
            .Distinct();

        return await _context.PullRequests
            .AsNoTracking()
            .Where(pullRequest => linkedPullRequestIds.Contains(pullRequest.Id))
            .OrderByDescending(pullRequest => pullRequest.CreatedDateUtc)
            .ThenByDescending(pullRequest => pullRequest.Id)
            .Select(MapPullRequestProjection())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PullRequestDto>> GetReviewBottleneckPullRequestsAsync(
        DateTime cutoffDateUtc,
        int maxPullRequests,
        CancellationToken cancellationToken)
    {
        if (maxPullRequests <= 0)
        {
            return Array.Empty<PullRequestDto>();
        }

        return await _context.PullRequests
            .AsNoTracking()
            .Where(pullRequest => pullRequest.CreatedDateUtc >= cutoffDateUtc)
            .OrderByDescending(pullRequest => pullRequest.CreatedDateUtc)
            .ThenByDescending(pullRequest => pullRequest.Id)
            .Take(maxPullRequests)
            .Select(MapPullRequestProjection())
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> LoadTeamNameAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.Context.TeamIds.IsAll || filter.Context.TeamIds.Values.Count == 0)
        {
            return null;
        }

        var teamId = filter.Context.TeamIds.Values[0];

        return await _context.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => team.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> LoadSprintNameAsync(
        int? sprintId,
        CancellationToken cancellationToken)
    {
        if (!sprintId.HasValue)
        {
            return null;
        }

        return await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.Id == sprintId.Value)
            .Select(sprint => sprint.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<PullRequestDto>> LoadScopedPullRequestsAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        return await PullRequestFiltering.ApplyScope(
                _context.PullRequests.AsNoTracking(),
                filter)
            .Select(MapPullRequestProjection())
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, int>> LoadIterationCountsAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var iterationRows = await _context.PullRequestIterations
            .AsNoTracking()
            .Where(iteration => pullRequestIds.Contains(iteration.PullRequestId))
            .Select(iteration => iteration.PullRequestId)
            .ToListAsync(cancellationToken);

        return iterationRows
            .GroupBy(pullRequestId => pullRequestId)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async Task<IReadOnlyDictionary<int, int>> LoadCommentCountsAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var commentRows = await _context.PullRequestComments
            .AsNoTracking()
            .Where(comment => pullRequestIds.Contains(comment.PullRequestId))
            .Select(comment => comment.PullRequestId)
            .ToListAsync(cancellationToken);

        return commentRows
            .GroupBy(pullRequestId => pullRequestId)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async Task<IReadOnlyDictionary<int, int>> LoadDistinctFileCountsAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var fileRows = await _context.PullRequestFileChanges
            .AsNoTracking()
            .Where(fileChange => pullRequestIds.Contains(fileChange.PullRequestId))
            .Select(fileChange => new { fileChange.PullRequestId, fileChange.FilePath })
            .ToListAsync(cancellationToken);

        return fileRows
            .GroupBy(fileChange => fileChange.PullRequestId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(fileChange => fileChange.FilePath).Distinct().Count());
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestIterationDto>>> LoadIterationsByPullRequestIdAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return EmptyLookup<PullRequestIterationDto>();
        }

        var entities = await _context.PullRequestIterations
            .AsNoTracking()
            .Where(iteration => pullRequestIds.Contains(iteration.PullRequestId))
            .OrderBy(iteration => iteration.PullRequestId)
            .ThenBy(iteration => iteration.IterationNumber)
            .ToListAsync(cancellationToken);

        return GroupByPullRequestId(
            entities,
            static iteration => iteration.PullRequestId,
            static iteration => new PullRequestIterationDto(
                iteration.PullRequestId,
                iteration.IterationNumber,
                iteration.CreatedDate,
                iteration.UpdatedDate,
                iteration.CommitCount,
                iteration.ChangeCount));
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestCommentDto>>> LoadCommentsByPullRequestIdAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return EmptyLookup<PullRequestCommentDto>();
        }

        var entities = await _context.PullRequestComments
            .AsNoTracking()
            .Where(comment => pullRequestIds.Contains(comment.PullRequestId))
            .OrderBy(comment => comment.PullRequestId)
            .ThenBy(comment => comment.CreatedDateUtc)
            .ThenBy(comment => comment.InternalId)
            .ToListAsync(cancellationToken);

        return GroupByPullRequestId(
            entities,
            static comment => comment.PullRequestId,
            static comment => new PullRequestCommentDto(
                comment.Id,
                comment.PullRequestId,
                comment.ThreadId,
                comment.Author,
                comment.Content,
                comment.CreatedDate,
                comment.UpdatedDate,
                comment.IsResolved,
                comment.ResolvedDate,
                comment.ResolvedBy));
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<PullRequestFileChangeDto>>> LoadFileChangesByPullRequestIdAsync(
        IReadOnlyList<int> pullRequestIds,
        CancellationToken cancellationToken)
    {
        if (pullRequestIds.Count == 0)
        {
            return EmptyLookup<PullRequestFileChangeDto>();
        }

        var entities = await _context.PullRequestFileChanges
            .AsNoTracking()
            .Where(fileChange => pullRequestIds.Contains(fileChange.PullRequestId))
            .OrderBy(fileChange => fileChange.PullRequestId)
            .ThenBy(fileChange => fileChange.IterationId)
            .ThenBy(fileChange => fileChange.FilePath)
            .ToListAsync(cancellationToken);

        return GroupByPullRequestId(
            entities,
            static fileChange => fileChange.PullRequestId,
            static fileChange => new PullRequestFileChangeDto(
                fileChange.PullRequestId,
                fileChange.IterationId,
                fileChange.FilePath,
                fileChange.ChangeType,
                fileChange.LinesAdded,
                fileChange.LinesDeleted,
                fileChange.LinesModified));
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<TDto>> GroupByPullRequestId<TEntity, TDto>(
        IEnumerable<TEntity> entities,
        Func<TEntity, int> getPullRequestId,
        Func<TEntity, TDto> map)
    {
        return entities
            .GroupBy(getPullRequestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TDto>)group.Select(map).ToList());
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<TDto>> EmptyLookup<TDto>()
        => new Dictionary<int, IReadOnlyList<TDto>>();

    private static Expression<Func<PullRequestEntity, PullRequestDto>> MapPullRequestProjection()
    {
        return pullRequest => new PullRequestDto(
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
            pullRequest.ProductId);
    }
}
