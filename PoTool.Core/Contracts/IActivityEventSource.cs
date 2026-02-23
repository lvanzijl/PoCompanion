namespace PoTool.Core.Contracts;

public sealed class ActivityEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public int WorkItemId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string? IterationPath { get; init; }
    public int? ParentId { get; init; }
    public int? FeatureId { get; init; }
    public int? EpicId { get; init; }
    public string? FieldRefName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public interface IActivityEventSource
{
    Task<IReadOnlyList<ActivityEvent>> GetActivityEventsAsync(
        IReadOnlyCollection<int> workItemIds,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
