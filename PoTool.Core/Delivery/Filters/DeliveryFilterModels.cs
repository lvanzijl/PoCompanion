using PoTool.Core.Filters;

namespace PoTool.Core.Delivery.Filters;

public sealed record DeliveryFilterContext(
    FilterSelection<int> ProductIds,
    FilterTimeSelection Time)
{
    public static DeliveryFilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterTimeSelection.None());
}

public sealed record DeliveryEffectiveFilter(
    DeliveryFilterContext Context,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? SprintId,
    IReadOnlyList<int> SprintIds);
