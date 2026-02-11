namespace PoTool.Shared.Settings;

/// <summary>
/// Request for revision cache validation.
/// </summary>
public class RevisionValidationRequest
{
    /// <summary>
    /// Validation mode: "single", "sample", or "recent".
    /// </summary>
    public string Mode { get; set; } = "single";

    /// <summary>
    /// Work item ID for single mode.
    /// </summary>
    public int? WorkItemId { get; set; }

    /// <summary>
    /// Number of items to validate for sample/recent modes.
    /// </summary>
    public int SampleSize { get; set; } = 5;
}

/// <summary>
/// Result of revision cache validation.
/// </summary>
public class RevisionValidationReport
{
    /// <summary>
    /// Timestamp of the validation run.
    /// </summary>
    public DateTimeOffset ValidatedAt { get; set; }

    /// <summary>
    /// Validation mode used.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Number of work items validated.
    /// </summary>
    public int TotalValidated { get; set; }

    /// <summary>
    /// Number of work items that passed validation.
    /// </summary>
    public int Passed { get; set; }

    /// <summary>
    /// Number of work items that failed validation.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Per-item validation results.
    /// </summary>
    public List<WorkItemValidationResult> Results { get; set; } = new();

    /// <summary>
    /// Fields used for comparison (the whitelist).
    /// </summary>
    public List<string> ComparedFields { get; set; } = new();
}

/// <summary>
/// Validation result for a single work item.
/// </summary>
public class WorkItemValidationResult
{
    /// <summary>
    /// Work item ID.
    /// </summary>
    public int WorkItemId { get; set; }

    /// <summary>
    /// Whether validation passed (no diffs).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Last revision number used for replay.
    /// </summary>
    public int LastRevisionNumber { get; set; }

    /// <summary>
    /// Number of revisions replayed.
    /// </summary>
    public int RevisionCount { get; set; }

    /// <summary>
    /// Error message if validation could not be performed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Field differences found.
    /// </summary>
    public List<FieldDiffDto> Diffs { get; set; } = new();
}

/// <summary>
/// A single field difference between replayed and REST state.
/// </summary>
public class FieldDiffDto
{
    /// <summary>
    /// TFS field reference name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Value from replaying revisions.
    /// </summary>
    public string? ReplayedValue { get; set; }

    /// <summary>
    /// Value from REST API / cached work item.
    /// </summary>
    public string? RestValue { get; set; }
}
