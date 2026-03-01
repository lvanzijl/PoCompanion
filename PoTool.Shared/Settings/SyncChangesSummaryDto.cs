namespace PoTool.Shared.Settings;

/// <summary>
/// Summary of changes detected between the previous and the latest successful sync.
/// Used to power the "What's New Since Last Sync" section on the Home page.
/// </summary>
public sealed class SyncChangesSummaryDto
{
    /// <summary>
    /// Start of the change window (timestamp of the previous successful sync).
    /// Null when no previous sync exists — indicates no change data is available.
    /// </summary>
    public DateTimeOffset? SinceTime { get; set; }

    /// <summary>
    /// End of the change window (timestamp of the last successful sync).
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; set; }

    /// <summary>
    /// Whether change data is available (requires at least two completed syncs).
    /// </summary>
    public bool HasData => SinceTime.HasValue;

    // ── Bugs opened (state changed to a non-done state) ──────────────────────────

    /// <summary>Total count of bugs that were opened/activated in the sync window.</summary>
    public int BugsOpenedCount { get; set; }

    /// <summary>Work item summaries for bugs opened. Capped to the first 20 items.</summary>
    public List<WorkItemChangeSummary> BugsOpened { get; set; } = [];

    // ── Bugs closed ───────────────────────────────────────────────────────────────

    /// <summary>Total count of bugs that were closed/resolved in the sync window.</summary>
    public int BugsClosedCount { get; set; }

    /// <summary>Work item summaries for bugs closed. Capped to the first 20 items.</summary>
    public List<WorkItemChangeSummary> BugsClosed { get; set; } = [];

    // ── Validation issues on recently changed items ───────────────────────────────

    /// <summary>
    /// Count of work items that were changed in the sync window AND currently have
    /// at least one validation issue.
    /// Note: this reflects items changed-and-now-invalid; it does NOT guarantee
    /// the issue was newly introduced (a baseline snapshot is not stored).
    /// </summary>
    public int ValidationIssueWorkItemCount { get; set; }

    /// <summary>Work item summaries for items with validation issues. Capped to the first 20 items.</summary>
    public List<WorkItemChangeSummary> ValidationIssueItems { get; set; } = [];

    // ── Sprint completions ────────────────────────────────────────────────────────

    /// <summary>Count of sprints whose end date falls within the sync window.</summary>
    public int SprintsCompletedCount { get; set; }

    /// <summary>Sprint summaries for completed sprints.</summary>
    public List<SprintChangeSummary> SprintsCompleted { get; set; } = [];
}

/// <summary>
/// Lightweight summary of a work item change within a sync window.
/// </summary>
public sealed class WorkItemChangeSummary
{
    /// <summary>TFS work item ID.</summary>
    public int TfsId { get; set; }

    /// <summary>Work item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Work item type (Bug, PBI, Feature, …).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>State value before the change (may be null for new items).</summary>
    public string? OldState { get; set; }

    /// <summary>State value after the change (current state).</summary>
    public string? NewState { get; set; }

    /// <summary>Timestamp of the change event from TFS.</summary>
    public DateTimeOffset? ChangedAt { get; set; }
}

/// <summary>
/// Lightweight summary of a sprint that completed within the sync window.
/// </summary>
public sealed class SprintChangeSummary
{
    /// <summary>Sprint display name.</summary>
    public string SprintName { get; set; } = string.Empty;

    /// <summary>Full iteration path.</summary>
    public string IterationPath { get; set; } = string.Empty;

    /// <summary>Sprint end date/time (UTC).</summary>
    public DateTimeOffset? EndDate { get; set; }
}
