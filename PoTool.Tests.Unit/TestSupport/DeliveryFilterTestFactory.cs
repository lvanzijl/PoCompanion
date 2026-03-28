using PoTool.Core.Delivery.Filters;
using PoTool.Core.Filters;

namespace PoTool.Tests.Unit.TestSupport;

internal static class DeliveryFilterTestFactory
{
    public static DeliveryEffectiveFilter MultiSprint(
        IReadOnlyList<int> productIds,
        IReadOnlyList<int> sprintIds,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null)
        => new(
            new DeliveryFilterContext(
                FilterSelection<int>.Selected(productIds),
                FilterTimeSelection.MultiSprint(sprintIds)),
            rangeStartUtc,
            rangeEndUtc,
            null,
            sprintIds.ToArray());

    public static DeliveryEffectiveFilter SingleSprint(
        IReadOnlyList<int> productIds,
        int sprintId,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null)
        => new(
            new DeliveryFilterContext(
                FilterSelection<int>.Selected(productIds),
                FilterTimeSelection.Sprint(sprintId)),
            rangeStartUtc,
            rangeEndUtc,
            sprintId,
            Array.Empty<int>());

    public static DeliveryEffectiveFilter DateRange(
        IReadOnlyList<int> productIds,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc)
        => new(
            new DeliveryFilterContext(
                FilterSelection<int>.Selected(productIds),
                FilterTimeSelection.DateRange(rangeStartUtc, rangeEndUtc)),
            rangeStartUtc,
            rangeEndUtc,
            null,
            Array.Empty<int>());
}
