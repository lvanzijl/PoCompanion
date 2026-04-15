using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public static class GlobalFilterTrendRequestMapper
{
    public static TrendSprintRangeRequest ResolveRange(FilterState state, IEnumerable<SprintDto>? availableSprints)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Time.Mode != FilterTimeMode.Range)
        {
            return TrendSprintRangeRequest.Unresolved("Trend requests require a resolved sprint range.");
        }

        if (!state.Time.StartSprintId.HasValue || !state.Time.EndSprintId.HasValue)
        {
            return TrendSprintRangeRequest.Unresolved("The selected sprint range is incomplete.");
        }

        var orderedSprints = (availableSprints ?? Array.Empty<SprintDto>())
            .Where(static sprint => sprint.StartUtc.HasValue || sprint.EndUtc.HasValue)
            .DistinctBy(static sprint => sprint.Id)
            .OrderBy(static sprint => sprint.StartUtc ?? sprint.EndUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(static sprint => sprint.Id)
            .ToList();

        if (orderedSprints.Count == 0)
        {
            return TrendSprintRangeRequest.Unresolved("No sprint boundaries are available for the selected team.");
        }

        var startIndex = orderedSprints.FindIndex(sprint => sprint.Id == state.Time.StartSprintId.Value);
        var endIndex = orderedSprints.FindIndex(sprint => sprint.Id == state.Time.EndSprintId.Value);
        if (startIndex < 0 || endIndex < 0)
        {
            return TrendSprintRangeRequest.Unresolved("The selected sprint range does not match the available sprint boundaries.");
        }

        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        var selectedSprints = orderedSprints
            .Skip(startIndex)
            .Take(endIndex - startIndex + 1)
            .ToList();
        if (selectedSprints.Count == 0)
        {
            return TrendSprintRangeRequest.Unresolved("The selected sprint range resolved to an empty sprint scope.");
        }

        var rangeStartUtc = selectedSprints.First().StartUtc ?? selectedSprints.First().EndUtc;
        var rangeEndUtc = selectedSprints.Last().EndUtc ?? selectedSprints.Last().StartUtc;
        if (!rangeStartUtc.HasValue || !rangeEndUtc.HasValue)
        {
            return TrendSprintRangeRequest.Unresolved("The selected sprint range is missing start or end boundaries.");
        }

        return TrendSprintRangeRequest.Resolved(
            selectedSprints.Select(static sprint => sprint.Id).ToArray(),
            rangeStartUtc,
            rangeEndUtc);
    }
}
