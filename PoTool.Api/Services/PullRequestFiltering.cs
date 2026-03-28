using PoTool.Api.Persistence.Entities;
using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

internal static class PullRequestFiltering
{
    public static IQueryable<PullRequestEntity> ApplyScope(
        IQueryable<PullRequestEntity> query,
        PullRequestEffectiveFilter filter)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.RepositoryScope.Count == 0)
        {
            return query.Where(_ => false);
        }

        if (!filter.Context.ProductIds.IsAll)
        {
            query = query.Where(pr => pr.ProductId.HasValue && filter.Context.ProductIds.Values.Contains(pr.ProductId.Value));
        }

        query = query.Where(pr => filter.RepositoryScope.Contains(pr.RepositoryName));

        if (filter.RangeStartUtc.HasValue)
        {
            var fromUtc = filter.RangeStartUtc.Value.UtcDateTime;
            query = query.Where(pr => pr.CreatedDateUtc >= fromUtc);
        }

        if (filter.RangeEndUtc.HasValue)
        {
            var toUtc = filter.RangeEndUtc.Value.UtcDateTime;
            query = query.Where(pr => pr.CreatedDateUtc <= toUtc);
        }

        return query;
    }

    public static IReadOnlyList<PullRequestDto> ApplyLocalSelections(
        IEnumerable<PullRequestDto> pullRequests,
        PullRequestEffectiveFilter filter)
    {
        ArgumentNullException.ThrowIfNull(pullRequests);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.RepositoryScope.Count == 0)
        {
            return Array.Empty<PullRequestDto>();
        }

        if (!filter.Context.ProductIds.IsAll)
        {
            var productIds = filter.Context.ProductIds.Values.ToHashSet();
            pullRequests = pullRequests.Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value));
        }

        var repositoryScope = filter.RepositoryScope.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<PullRequestDto> filtered = pullRequests.Where(pr => repositoryScope.Contains(pr.RepositoryName));

        if (filter.RangeStartUtc.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate >= filter.RangeStartUtc.Value);
        }

        if (filter.RangeEndUtc.HasValue)
        {
            filtered = filtered.Where(pr => pr.CreatedDate <= filter.RangeEndUtc.Value);
        }

        if (!filter.Context.IterationPaths.IsAll)
        {
            var iterationPaths = filter.Context.IterationPaths.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(pr => iterationPaths.Contains(pr.IterationPath));
        }

        if (!filter.Context.CreatedBys.IsAll)
        {
            var createdBys = filter.Context.CreatedBys.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(pr => createdBys.Contains(pr.CreatedBy));
        }

        if (!filter.Context.Statuses.IsAll)
        {
            var statuses = filter.Context.Statuses.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(pr => statuses.Contains(pr.Status));
        }

        return filtered.ToList();
    }
}
