namespace PoTool.Client.Services;

public static class CapacityCollisionHintDetector
{
    public static IReadOnlyList<CapacityCollisionWindow> Build(
        IReadOnlyList<CapacityCollisionEpicInput> epics,
        DateTimeOffset axisStart,
        DateTimeOffset axisEnd)
    {
        ArgumentNullException.ThrowIfNull(epics);

        if (axisEnd <= axisStart || epics.Count < 2)
        {
            return [];
        }

        var boundaries = epics
            .SelectMany(epic => new[] { epic.StartDate, epic.EndDate })
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (boundaries.Count < 2)
        {
            return [];
        }

        var axisDays = Math.Max(1d, (axisEnd - axisStart).TotalDays);
        var segments = new List<CapacityCollisionWindow>();

        for (int i = 0; i < boundaries.Count - 1; i++)
        {
            var segmentStart = boundaries[i];
            var segmentEnd = boundaries[i + 1];
            if (segmentEnd <= segmentStart)
            {
                continue;
            }

            var activeEpics = epics
                .Where(epic => epic.StartDate < segmentEnd && epic.EndDate > segmentStart)
                .ToList();
            if (activeEpics.Count < 2)
            {
                continue;
            }

            var sharedTeams = activeEpics
                .SelectMany(epic => epic.TeamIds.Distinct())
                .GroupBy(teamId => teamId)
                .Where(group => group.Count() >= 2)
                .Select(group => group.Key)
                .OrderBy(teamId => teamId)
                .ToList();
            if (sharedTeams.Count == 0)
            {
                continue;
            }

            var contributingEpics = activeEpics
                .Where(epic => epic.TeamIds.Any(sharedTeams.Contains))
                .OrderBy(epic => epic.EpicId)
                .ToList();
            if (contributingEpics.Count < 2)
            {
                continue;
            }

            var leftPercent = Math.Max(0d, (segmentStart - axisStart).TotalDays / axisDays * 100d);
            var rightPercent = Math.Min(100d, (segmentEnd - axisStart).TotalDays / axisDays * 100d);

            segments.Add(new CapacityCollisionWindow(
                segmentStart,
                segmentEnd,
                leftPercent,
                Math.Max(0d, rightPercent - leftPercent),
                contributingEpics.Select(epic => epic.EpicId).ToList(),
                contributingEpics.Select(epic => epic.ProductName).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                sharedTeams));
        }

        return MergeAdjacentSegments(segments);
    }

    private static IReadOnlyList<CapacityCollisionWindow> MergeAdjacentSegments(IReadOnlyList<CapacityCollisionWindow> segments)
    {
        if (segments.Count <= 1)
        {
            return segments;
        }

        var ordered = segments
            .OrderBy(segment => segment.StartDate)
            .ThenBy(segment => segment.EndDate)
            .ToList();

        var merged = new List<CapacityCollisionWindow> { ordered[0] };
        for (int i = 1; i < ordered.Count; i++)
        {
            var previous = merged[^1];
            var current = ordered[i];

            if (previous.EndDate == current.StartDate
                && previous.EpicIds.SequenceEqual(current.EpicIds)
                && previous.SharedTeamIds.SequenceEqual(current.SharedTeamIds))
            {
                merged[^1] = previous with
                {
                    EndDate = current.EndDate,
                    WidthPercent = (current.LeftPercent + current.WidthPercent) - previous.LeftPercent
                };
                continue;
            }

            merged.Add(current);
        }

        return merged;
    }
}

public sealed record CapacityCollisionEpicInput(
    int EpicId,
    string ProductName,
    IReadOnlyList<int> TeamIds,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate);

public sealed record CapacityCollisionWindow(
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    double LeftPercent,
    double WidthPercent,
    IReadOnlyList<int> EpicIds,
    IReadOnlyList<string> ProductNames,
    IReadOnlyList<int> SharedTeamIds);
