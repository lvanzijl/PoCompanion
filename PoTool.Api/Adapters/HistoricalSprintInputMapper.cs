using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.Models;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Adapters;

internal static class HistoricalSprintInputMapper
{
    public static WorkItemSnapshot ToSnapshot(this WorkItemEntity entity)
    {
        return new WorkItemSnapshot(
            entity.TfsId,
            entity.Type,
            Normalize(entity.State),
            Normalize(entity.IterationPath));
    }

    public static WorkItemSnapshot ToSnapshot(this WorkItemDto dto)
    {
        return new WorkItemSnapshot(
            dto.TfsId,
            dto.Type,
            Normalize(dto.State),
            Normalize(dto.IterationPath));
    }

    public static IReadOnlyDictionary<int, WorkItemSnapshot> ToSnapshotDictionary(this IEnumerable<WorkItemEntity> workItems)
    {
        return workItems.ToDictionary(workItem => workItem.TfsId, workItem => workItem.ToSnapshot());
    }

    public static IReadOnlyDictionary<int, WorkItemSnapshot> ToSnapshotDictionary(this IEnumerable<WorkItemDto> workItems)
    {
        return workItems.ToDictionary(workItem => workItem.TfsId, workItem => workItem.ToSnapshot());
    }

    public static SprintDefinition ToDefinition(this SprintEntity entity)
    {
        return new SprintDefinition(
            entity.Id,
            entity.TeamId,
            entity.Path,
            entity.Name,
            entity.StartUtc,
            entity.EndUtc);
    }

    public static SprintDefinition ToDefinition(this SprintDto dto)
    {
        return new SprintDefinition(
            dto.Id,
            dto.TeamId,
            dto.Path,
            dto.Name,
            dto.StartUtc,
            dto.EndUtc);
    }

    public static FieldChangeEvent ToFieldChangeEvent(this ActivityEventLedgerEntryEntity entity)
    {
        var timestamp = entity.EventTimestamp != default
            ? entity.EventTimestamp
            : new DateTimeOffset(DateTime.SpecifyKind(entity.EventTimestampUtc, DateTimeKind.Utc));
        var timestampUtc = entity.EventTimestampUtc != default
            ? DateTime.SpecifyKind(entity.EventTimestampUtc, DateTimeKind.Utc)
            : timestamp.UtcDateTime;

        return new FieldChangeEvent(
            entity.Id,
            entity.WorkItemId,
            entity.UpdateId,
            entity.FieldRefName,
            timestamp,
            timestampUtc,
            entity.OldValue,
            entity.NewValue);
    }

    public static IReadOnlyList<FieldChangeEvent> ToFieldChangeEvents(this IEnumerable<ActivityEventLedgerEntryEntity> activityEvents)
    {
        return activityEvents.Select(activityEvent => activityEvent.ToFieldChangeEvent()).ToList();
    }

    public static IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> GroupByWorkItemId(this IEnumerable<FieldChangeEvent> fieldChanges)
    {
        return fieldChanges
            .GroupBy(fieldChange => fieldChange.WorkItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<FieldChangeEvent>)group.ToList());
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
