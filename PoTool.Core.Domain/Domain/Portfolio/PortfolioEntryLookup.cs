using System.Globalization;
using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.Portfolio;

/// <summary>
/// Detects the first time a work item entered a resolved product portfolio from the membership ledger.
/// </summary>
public static class PortfolioEntryLookup
{
    public const string ResolvedProductIdFieldRefName = "PoTool.ResolvedProductId";

    public static IReadOnlyDictionary<int, DateTimeOffset> Build(
        IEnumerable<FieldChangeEvent> activityEvents,
        int productId)
    {
        return activityEvents
            .Where(activityEvent =>
                string.Equals(activityEvent.FieldRefName, ResolvedProductIdFieldRefName, StringComparison.OrdinalIgnoreCase)
                && IsPortfolioEntryTransition(activityEvent, productId))
            .GroupBy(activityEvent => activityEvent.WorkItemId)
            .Select(group => new
            {
                WorkItemId = group.Key,
                EnteredPortfolioAt = GetFirstEnteredPortfolioTimestamp(group, productId)
            })
            .Where(entry => entry.EnteredPortfolioAt.HasValue)
            .ToDictionary(entry => entry.WorkItemId, entry => entry.EnteredPortfolioAt!.Value);
    }

    public static DateTimeOffset? GetFirstEnteredPortfolioTimestamp(
        IEnumerable<FieldChangeEvent>? activityEvents,
        int productId)
    {
        if (activityEvents == null)
        {
            return null;
        }

        foreach (var activityEvent in activityEvents
                     .Where(activityEvent => string.Equals(activityEvent.FieldRefName, ResolvedProductIdFieldRefName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(activityEvent => activityEvent.TimestampUtc)
                     .ThenBy(activityEvent => activityEvent.EventId)
                     .ThenBy(activityEvent => activityEvent.UpdateId))
        {
            if (IsPortfolioEntryTransition(activityEvent, productId))
            {
                return activityEvent.Timestamp;
            }
        }

        return null;
    }

    private static bool IsPortfolioEntryTransition(FieldChangeEvent activityEvent, int productId)
    {
        var oldProductId = ParseNullableProductId(activityEvent.OldValue);
        var newProductId = ParseNullableProductId(activityEvent.NewValue);

        return newProductId == productId && oldProductId != productId;
    }

    private static int? ParseNullableProductId(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
