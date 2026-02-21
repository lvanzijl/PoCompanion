namespace PoTool.Integrations.Tfs.Clients;

internal readonly record struct WorkItemIdRangeSegment(int Start, int End)
{
    public int Span => End - Start + 1;
}

internal static class WorkItemIdRangeSegmentBuilder
{
    private const int DefaultMaxRangeSize = 500;

    public static IReadOnlyList<WorkItemIdRangeSegment> Build(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        int gapTolerance = 0,
        int maxRangeSize = DefaultMaxRangeSize)
    {
        if (scopedWorkItemIds == null || scopedWorkItemIds.Count == 0)
        {
            return [];
        }

        var orderedIds = scopedWorkItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (orderedIds.Length == 0)
        {
            return [];
        }

        var tolerance = Math.Max(0, gapTolerance);
        var maxSpan = Math.Max(1, maxRangeSize);
        var segments = new List<WorkItemIdRangeSegment>();
        var rangeStart = orderedIds[0];
        var rangeEnd = orderedIds[0];

        for (var i = 1; i < orderedIds.Length; i++)
        {
            var candidate = orderedIds[i];
            var gap = candidate - rangeEnd - 1;
            if (gap <= tolerance)
            {
                rangeEnd = candidate;
                continue;
            }

            AddSplitSegments(segments, rangeStart, rangeEnd, maxSpan);
            rangeStart = candidate;
            rangeEnd = candidate;
        }

        AddSplitSegments(segments, rangeStart, rangeEnd, maxSpan);
        return segments;
    }

    private static void AddSplitSegments(List<WorkItemIdRangeSegment> segments, int start, int end, int maxSpan)
    {
        var current = start;
        while (current <= end)
        {
            var segmentEnd = Math.Min(end, current + maxSpan - 1);
            segments.Add(new WorkItemIdRangeSegment(current, segmentEnd));
            current = segmentEnd + 1;
        }
    }
}
