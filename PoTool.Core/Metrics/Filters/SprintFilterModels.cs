using PoTool.Core.Filters;

namespace PoTool.Core.Metrics.Filters;

public sealed record SprintFilterContext(
    FilterSelection<int> ProductIds,
    FilterSelection<int> TeamIds,
    FilterSelection<string> AreaPaths,
    FilterSelection<string> IterationPaths,
    FilterTimeSelection Time)
{
    public static SprintFilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterSelection<int>.All(),
        FilterSelection<string>.All(),
        FilterSelection<string>.All(),
        FilterTimeSelection.None());
}

public sealed record SprintEffectiveFilter(
    SprintFilterContext Context,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? SprintId,
    IReadOnlyList<int> SprintIds,
    IReadOnlyList<string> IterationPaths,
    int? CurrentSprintId,
    int? PreviousSprintId)
{
    public string? IterationPath => IterationPaths.Count == 1 ? IterationPaths[0] : null;
}
