namespace PoTool.Client.Models;

public sealed record TrendSprintRangeRequest(
    IReadOnlyList<int> SprintIds,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    bool IsResolved,
    string? FailureReason)
{
    public static TrendSprintRangeRequest Unresolved(string reason)
        => new(Array.Empty<int>(), null, null, false, reason);

    public static TrendSprintRangeRequest Resolved(
        IReadOnlyList<int> sprintIds,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc)
        => new(sprintIds, rangeStartUtc, rangeEndUtc, true, null);
}
