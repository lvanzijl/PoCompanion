using PoTool.Core.Configuration;

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
    private const string DefaultCapFallbackMode = "SingleRange";

    public static IReadOnlyList<WorkItemIdRangeSegment> Build(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        int maxGap = DefaultMaxGap,
        int maxSpan = DefaultMaxSpan,
        int minIdsPerSegment = DefaultMinIdsPerSegment,
        int maxSegmentsPerWindow = DefaultMaxSegmentsPerWindow)
    {
        return Build(scopedWorkItemIds, new WorkItemIdSegmentationOptions(
            maxGap,
            maxSpan,
            minIdsPerSegment,
            maxSegmentsPerWindow,
            DefaultCapFallbackMode));
    }

    public static IReadOnlyList<WorkItemIdRangeSegment> Build(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        WorkItemIdSegmentationOptions options)
    {
        return BuildWithMetadata(scopedWorkItemIds, options).Segments;
    }

    public static SegmentBuildResult BuildWithMetadata(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        int maxGap = DefaultMaxGap,
        int maxSpan = DefaultMaxSpan,
        int minIdsPerSegment = DefaultMinIdsPerSegment,
        int maxSegmentsPerWindow = DefaultMaxSegmentsPerWindow)
    {
        return BuildWithMetadata(scopedWorkItemIds, new WorkItemIdSegmentationOptions(
            maxGap,
            maxSpan,
            minIdsPerSegment,
            maxSegmentsPerWindow,
            DefaultCapFallbackMode));
    }

    public static SegmentBuildResult BuildWithMetadata(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        WorkItemIdSegmentationOptions options)
    {
        if (scopedWorkItemIds == null || scopedWorkItemIds.Count == 0)
        {
            return new SegmentBuildResult([], false, null);
        }

        var orderedIds = scopedWorkItemIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (orderedIds.Length == 0)
        {
            return new SegmentBuildResult([], false, null);
        }

        var effectiveMaxGap = Math.Max(0, options.MaxGap);
        var effectiveMaxSpan = Math.Max(1, options.MaxSpan);
        var effectiveMinIdsPerSegment = Math.Max(1, options.MinIds);
        var effectiveMaxSegmentsPerWindow = Math.Max(1, options.MaxSegmentsPerWindow);
        var fallbackMode = RevisionIngestionPaginationOptions.IsValidSegmentCapFallbackMode(options.CapFallbackMode) &&
                           string.Equals(options.CapFallbackMode, "MergeAdjacent", StringComparison.OrdinalIgnoreCase)
            ? "MergeAdjacent"
            : "SingleRange";

        if (orderedIds.Length < effectiveMinIdsPerSegment)
        {
            return new SegmentBuildResult(
                [new WorkItemIdRangeSegment(orderedIds[0], orderedIds[^1])],
                FallbackApplied: false,
                FallbackMode: null);
        }

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
            rangeStart = candidate;
            rangeEnd = candidate;
            idsInCurrentSegment = 1;
        }

        segments.Add(new WorkItemIdRangeSegment(rangeStart, rangeEnd));
        if (segments.Count > effectiveMaxSegmentsPerWindow)
        {
            if (string.Equals(fallbackMode, "MergeAdjacent", StringComparison.Ordinal))
            {
                var mergedSegments = new List<WorkItemIdRangeSegment>(segments);
                while (mergedSegments.Count > effectiveMaxSegmentsPerWindow)
                {
                    var mergeIndex = 0;
                    var smallestGap = int.MaxValue;
                    for (var i = 0; i < mergedSegments.Count - 1; i++)
                    {
                        var gapBetween = mergedSegments[i + 1].Start - mergedSegments[i].End - 1;
                        if (gapBetween < smallestGap)
                        {
                            smallestGap = gapBetween;
                            mergeIndex = i;
                        }
                    }

                    var merged = new WorkItemIdRangeSegment(
                        mergedSegments[mergeIndex].Start,
                        mergedSegments[mergeIndex + 1].End);
                    mergedSegments[mergeIndex] = merged;
                    mergedSegments.RemoveAt(mergeIndex + 1);
                }

                return new SegmentBuildResult(mergedSegments, FallbackApplied: true, FallbackMode: "MergeAdjacent");
            }

            return new SegmentBuildResult(
                [new WorkItemIdRangeSegment(orderedIds[0], orderedIds[^1])],
                FallbackApplied: true,
                FallbackMode: "SingleRange");
        }

        return new SegmentBuildResult(segments, FallbackApplied: false, FallbackMode: null);
    }
}

public readonly record struct WorkItemIdSegmentationOptions(
    int MaxGap,
    int MaxSpan,
    int MinIds,
    int MaxSegmentsPerWindow,
    string CapFallbackMode);

public readonly record struct SegmentBuildResult(
    IReadOnlyList<WorkItemIdRangeSegment> Segments,
    bool FallbackApplied,
    string? FallbackMode);
