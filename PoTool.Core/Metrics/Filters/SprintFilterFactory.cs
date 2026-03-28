using PoTool.Core.Filters;

namespace PoTool.Core.Metrics.Filters;

public static class SprintFilterFactory
{
    public static SprintEffectiveFilter ForIterationPath(string iterationPath)
        => new(
            new SprintFilterContext(
                FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.Selected([iterationPath]),
                FilterTimeSelection.None()),
            null,
            null,
            null,
            Array.Empty<int>(),
            [iterationPath],
            null,
            null);

    public static SprintEffectiveFilter ForSprintId(int sprintId, IReadOnlyList<int>? productIds = null)
        => new(
            new SprintFilterContext(
                ToProductSelection(productIds),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.Sprint(sprintId)),
            null,
            null,
            sprintId,
            Array.Empty<int>(),
            Array.Empty<string>(),
            sprintId,
            null);

    public static SprintEffectiveFilter ForSprintIds(IReadOnlyList<int> sprintIds, IReadOnlyList<int>? productIds = null)
    {
        var normalizedSprintIds = sprintIds?.Distinct().ToArray() ?? Array.Empty<int>();
        return new SprintEffectiveFilter(
            new SprintFilterContext(
                ToProductSelection(productIds),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.MultiSprint(normalizedSprintIds)),
            null,
            null,
            null,
            normalizedSprintIds,
            Array.Empty<string>(),
            null,
            null);
    }

    public static SprintEffectiveFilter ForDateRange(
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        IReadOnlyList<int>? productIds = null)
        => new(
            new SprintFilterContext(
                ToProductSelection(productIds),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterTimeSelection.DateRange(rangeStartUtc, rangeEndUtc)),
            rangeStartUtc,
            rangeEndUtc,
            null,
            Array.Empty<int>(),
            Array.Empty<string>(),
            null,
            null);

    public static SprintEffectiveFilter ForProductAndArea(
        IReadOnlyList<int>? productIds,
        string? areaPath)
        => new(
            new SprintFilterContext(
                ToProductSelection(productIds),
                FilterSelection<int>.All(),
                string.IsNullOrWhiteSpace(areaPath)
                    ? FilterSelection<string>.All()
                    : FilterSelection<string>.Selected([areaPath]),
                FilterSelection<string>.All(),
                FilterTimeSelection.None()),
            null,
            null,
            null,
            Array.Empty<int>(),
            Array.Empty<string>(),
            null,
            null);

    private static FilterSelection<int> ToProductSelection(IReadOnlyList<int>? productIds)
        => productIds is { Count: > 0 }
            ? FilterSelection<int>.Selected(productIds)
            : FilterSelection<int>.All();
}
