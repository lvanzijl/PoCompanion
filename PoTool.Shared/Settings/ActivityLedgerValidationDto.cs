namespace PoTool.Shared.Settings;

public sealed class ActivityLedgerValidationDto
{
    public int WorkItemId { get; set; }
    public CachedWorkItemSnapshotDto? Snapshot { get; set; }
    public List<ActivityLedgerSprintGroupDto> SprintGroups { get; set; } = new();
    public List<ActivityLedgerEventDto> UnknownSprintEvents { get; set; } = new();
}

public sealed class CachedWorkItemSnapshotDto
{
    public int WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? CurrentIterationPath { get; set; }
    public int? ParentId { get; set; }
    public string? ParentTitle { get; set; }
    public int? FeatureId { get; set; }
    public string? FeatureTitle { get; set; }
    public int? EpicId { get; set; }
    public string? EpicTitle { get; set; }
    public DateTimeOffset? LastChangedDate { get; set; }
}

public sealed class ActivityLedgerSprintGroupDto
{
    public int SprintId { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;
    public DateTimeOffset? SprintStart { get; set; }
    public DateTimeOffset? SprintEnd { get; set; }
    public int TotalEventCount { get; set; }
    public int DistinctFieldsTouchedCount { get; set; }
    public int DistinctUsersCount { get; set; }
    public List<ActivityLedgerEventDto> Events { get; set; } = new();
}

public sealed class ActivityLedgerEventDto
{
    public DateTimeOffset Timestamp { get; set; }
    public int UpdateId { get; set; }
    public string? ChangedBy { get; set; }
    public string FieldRefName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IterationPathAtTime { get; set; }
    public int? ParentIdAtTime { get; set; }
    public int? FeatureIdAtTime { get; set; }
    public int? EpicIdAtTime { get; set; }
}
