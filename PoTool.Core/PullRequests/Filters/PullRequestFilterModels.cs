using PoTool.Core.Filters;

namespace PoTool.Core.PullRequests.Filters;

public sealed record PullRequestFilterContext(
    FilterSelection<int> ProductIds,
    FilterSelection<int> TeamIds,
    FilterSelection<string> RepositoryNames,
    FilterSelection<string> IterationPaths,
    FilterSelection<string> CreatedBys,
    FilterSelection<string> Statuses,
    FilterTimeSelection Time)
{
    public static PullRequestFilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterSelection<int>.All(),
        FilterSelection<string>.All(),
        FilterSelection<string>.All(),
        FilterSelection<string>.All(),
        FilterSelection<string>.All(),
        FilterTimeSelection.None());
}

public sealed record PullRequestEffectiveFilter(
    PullRequestFilterContext Context,
    IReadOnlyList<string> RepositoryScope,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? SprintId,
    IReadOnlyList<int> SprintIds);
