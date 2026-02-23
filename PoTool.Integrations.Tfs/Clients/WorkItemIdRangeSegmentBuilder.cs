namespace PoTool.Integrations.Tfs.Clients;

public readonly record struct WorkItemIdRangeSegment(int Start, int End)
{
    public int Span => End - Start + 1;
}

public static class WorkItemIdRangeSegmentBuilder
{
    private const int DefaultMaxGap = 200;
    private const int DefaultMaxSpan = 5000;
    private const int DefaultMinIdsPerSegment = 25;
    private const int DefaultMaxSegmentsPerWindow = 200;

    public static IReadOnlyList<WorkItemIdRangeSegment> Build(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        int maxGap = DefaultMaxGap,
        int maxSpan = DefaultMaxSpan,
        int minIdsPerSegment = DefaultMinIdsPerSegment,
        int maxSegmentsPerWindow = DefaultMaxSegmentsPerWindow)
    {
        return BuildWithMetadata(scopedWorkItemIds, maxGap, maxSpan, minIdsPerSegment, maxSegmentsPerWindow).Segments;
    }

    public static SegmentBuildResult BuildWithMetadata(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        int maxGap = DefaultMaxGap,
        int maxSpan = DefaultMaxSpan,
        int minIdsPerSegment = DefaultMinIdsPerSegment,
        int maxSegmentsPerWindow = DefaultMaxSegmentsPerWindow)
    {
        if (scopedWorkItemIds == null || scopedWorkItemIds.Count == 0)
        {
            return new SegmentBuildResult([], false);
        }

        var orderedIds = scopedWorkItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (orderedIds.Length == 0)
        {
            return new SegmentBuildResult([], false);
        }

        var effectiveMaxGap = Math.Max(0, maxGap);
        var effectiveMaxSpan = Math.Max(1, maxSpan);
        var effectiveMinIdsPerSegment = Math.Max(1, minIdsPerSegment);
        var effectiveMaxSegmentsPerWindow = Math.Max(1, maxSegmentsPerWindow);
        var segments = new List<WorkItemIdRangeSegment>();
        var rangeStart = orderedIds[0];
        var rangeEnd = orderedIds[0];
        var idsInCurrentSegment = 1;

        for (var i = 1; i < orderedIds.Length; i++)
        {
            var candidate = orderedIds[i];
            var gap = candidate - rangeEnd - 1;
            var spanIfExtended = candidate - rangeStart;
            var canExtendByBounds = gap <= effectiveMaxGap && spanIfExtended <= effectiveMaxSpan;
            var shouldForceExtendToMinIds = idsInCurrentSegment < effectiveMinIdsPerSegment && spanIfExtended <= effectiveMaxSpan;
            if (canExtendByBounds || shouldForceExtendToMinIds)
            {
                rangeEnd = candidate;
                idsInCurrentSegment++;
                continue;
            }

            segments.Add(new WorkItemIdRangeSegment(rangeStart, rangeEnd));
            if (segments.Count > effectiveMaxSegmentsPerWindow)
            {
                return new SegmentBuildResult(
                    [new WorkItemIdRangeSegment(orderedIds[0], orderedIds[^1])],
                    WasCondensedToSingleSegment: true);
            }
            rangeStart = candidate;
            rangeEnd = candidate;
            idsInCurrentSegment = 1;
        }

        segments.Add(new WorkItemIdRangeSegment(rangeStart, rangeEnd));
        if (segments.Count > effectiveMaxSegmentsPerWindow)
        {
            return new SegmentBuildResult(
                [new WorkItemIdRangeSegment(orderedIds[0], orderedIds[^1])],
                WasCondensedToSingleSegment: true);
        }

        return new SegmentBuildResult(segments, WasCondensedToSingleSegment: false);
    }
}

public readonly record struct SegmentBuildResult(
    IReadOnlyList<WorkItemIdRangeSegment> Segments,
    bool WasCondensedToSingleSegment);
