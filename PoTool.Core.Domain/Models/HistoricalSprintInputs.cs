namespace PoTool.Core.Domain.Models;

/// <summary>
/// Minimal work item data required by canonical sprint-history helpers.
/// </summary>
public sealed record WorkItemSnapshot(
    int WorkItemId,
    string WorkItemType,
    string? CurrentState,
    string? CurrentIterationPath);

/// <summary>
/// Minimal sprint data required by canonical sprint-history helpers.
/// </summary>
public sealed record SprintDefinition(
    int SprintId,
    int TeamId,
    string Path,
    string Name,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc);

/// <summary>
/// Minimal field-change event data required by canonical sprint-history helpers.
/// </summary>
public sealed record FieldChangeEvent(
    int EventId,
    int WorkItemId,
    int UpdateId,
    string FieldRefName,
    DateTimeOffset Timestamp,
    DateTime TimestampUtc,
    string? OldValue,
    string? NewValue);
