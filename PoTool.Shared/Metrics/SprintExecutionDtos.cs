namespace PoTool.Shared.Metrics;

/// <summary>
/// Top-level response DTO for sprint execution analysis.
/// Provides internal diagnostics on how a sprint actually unfolded.
/// </summary>
public record SprintExecutionDto
{
    /// <summary>
    /// Sprint database ID.
    /// </summary>
    public required int SprintId { get; init; }

    /// <summary>
    /// Sprint display name.
    /// </summary>
    public required string SprintName { get; init; }

    /// <summary>
    /// Sprint start date.
    /// </summary>
    public DateTimeOffset? StartUtc { get; init; }

    /// <summary>
    /// Sprint end date.
    /// </summary>
    public DateTimeOffset? EndUtc { get; init; }

    /// <summary>
    /// Aggregated execution summary metrics.
    /// </summary>
    public required SprintExecutionSummaryDto Summary { get; init; }

    /// <summary>
    /// PBIs completed during the sprint, ordered by completion date (ascending).
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> CompletedPbis { get; init; }

    /// <summary>
    /// PBIs that remained unfinished at the end of the sprint.
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> UnfinishedPbis { get; init; }

    /// <summary>
    /// PBIs added during the sprint (not part of initial scope).
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> AddedDuringSprint { get; init; }

    /// <summary>
    /// PBIs removed from the sprint after it started.
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> RemovedDuringSprint { get; init; }

    /// <summary>
    /// PBIs flagged as potentially starved — present early in the sprint but
    /// never completed, while later-added work was completed.
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> StarvedPbis { get; init; }

    /// <summary>
    /// Whether any data was found for the selected sprint.
    /// </summary>
    public bool HasData { get; init; }
}

/// <summary>
/// Aggregated summary of sprint execution metrics.
/// </summary>
public record SprintExecutionSummaryDto
{
    /// <summary>
    /// Number of PBIs assigned to the sprint at the start (initial scope).
    /// </summary>
    public int InitialScopeCount { get; init; }

    /// <summary>
    /// Total effort of initial scope PBIs.
    /// </summary>
    public int InitialScopeEffort { get; init; }

    /// <summary>
    /// Number of PBIs added to the sprint after it started.
    /// </summary>
    public int AddedDuringSprintCount { get; init; }

    /// <summary>
    /// Total effort of PBIs added during the sprint.
    /// </summary>
    public int AddedDuringSprintEffort { get; init; }

    /// <summary>
    /// Number of PBIs removed from the sprint after it started.
    /// </summary>
    public int RemovedDuringSprintCount { get; init; }

    /// <summary>
    /// Total effort of PBIs removed during the sprint.
    /// </summary>
    public int RemovedDuringSprintEffort { get; init; }

    /// <summary>
    /// Number of PBIs completed during the sprint.
    /// </summary>
    public int CompletedCount { get; init; }

    /// <summary>
    /// Total effort of completed PBIs.
    /// </summary>
    public int CompletedEffort { get; init; }

    /// <summary>
    /// Number of PBIs that remained unfinished at the end of the sprint.
    /// </summary>
    public int UnfinishedCount { get; init; }

    /// <summary>
    /// Total effort of unfinished PBIs.
    /// </summary>
    public int UnfinishedEffort { get; init; }

    /// <summary>
    /// Number of PBIs flagged as potentially starved.
    /// </summary>
    public int StarvedCount { get; init; }
}

/// <summary>
/// Individual PBI in the sprint execution analysis.
/// </summary>
public record SprintExecutionPbiDto
{
    /// <summary>
    /// TFS work item ID.
    /// </summary>
    public required int TfsId { get; init; }

    /// <summary>
    /// Work item title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Effort (story points) assigned to the PBI.
    /// </summary>
    public int? Effort { get; init; }

    /// <summary>
    /// Current state of the PBI.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Date the PBI was closed/completed, if applicable.
    /// </summary>
    public DateTimeOffset? ClosedDate { get; init; }

    /// <summary>
    /// Date the PBI entered the sprint iteration.
    /// Null if the PBI was in the sprint from the beginning or the date cannot be determined.
    /// </summary>
    public DateTimeOffset? EnteredSprintDate { get; init; }

    /// <summary>
    /// Date the PBI was removed from the sprint, if applicable.
    /// </summary>
    public DateTimeOffset? RemovedFromSprintDate { get; init; }

    /// <summary>
    /// Product name the PBI belongs to, if resolvable.
    /// </summary>
    public string? ProductName { get; init; }

    /// <summary>
    /// Completion order within the sprint (1-based). Null if not completed.
    /// </summary>
    public int? CompletionOrder { get; init; }
}
