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
    /// PBIs that spilled from the committed scope into the next sprint.
    /// </summary>
    public required IReadOnlyList<SprintExecutionPbiDto> SpilloverPbis { get; init; }

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
    /// Number of committed PBIs that moved directly into the next sprint unfinished.
    /// </summary>
    public int SpilloverCount { get; init; }

    /// <summary>
    /// Total effort of committed PBIs that spilled into the next sprint unfinished.
    /// </summary>
    public int SpilloverEffort { get; init; }

    /// <summary>
    /// Legacy abbreviated contract name for canonical committed story points at the sprint commitment timestamp.
    /// Derived estimates are excluded from sprint commitment.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double CommittedSP { get; init; }

    /// <summary>
    /// Legacy abbreviated contract name for canonical story points added to the sprint after commitment.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double AddedSP { get; init; }

    /// <summary>
    /// Canonical story points removed from the sprint after commitment.
    /// </summary>
    public double RemovedSP { get; init; }

    /// <summary>
    /// Legacy abbreviated contract name for canonical delivered story points whose first Done transition occurred during the sprint.
    /// Derived estimates are excluded from delivered velocity.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double DeliveredSP { get; init; }

    /// <summary>
    /// Canonical delivered story points from work added after commitment.
    /// Derived estimates are excluded from delivered velocity.
    /// </summary>
    public double DeliveredFromAddedSP { get; init; }

    /// <summary>
    /// Legacy abbreviated contract name for canonical story points that spilled from committed scope into the next sprint.
    /// Compatibility alias; deprecated in future contract revision.
    /// </summary>
    public double SpilloverSP { get; init; }

    /// <summary>
    /// Canonical committed story points at the sprint commitment timestamp.
    /// Maps to the same internal value as CommittedSP.
    /// </summary>
    public double CommittedStoryPoints => CommittedSP;

    /// <summary>
    /// Canonical story points added to the sprint after commitment.
    /// Maps to the same internal value as AddedSP.
    /// </summary>
    public double AddedStoryPoints => AddedSP;

    /// <summary>
    /// Canonical delivered story points whose first Done transition occurred during the sprint.
    /// Maps to the same internal value as DeliveredSP.
    /// </summary>
    public double DeliveredStoryPoints => DeliveredSP;

    /// <summary>
    /// Canonical remaining story points after sprint delivery and scope changes.
    /// Computed from the same internal committed, added, removed, and delivered values already exposed by this DTO.
    /// </summary>
    public double RemainingStoryPoints => CommittedSP + AddedSP - RemovedSP - DeliveredSP;

    /// <summary>
    /// Canonical story points that spilled from committed scope into the next sprint.
    /// Maps to the same internal value as SpilloverSP.
    /// </summary>
    public double SpilloverStoryPoints => SpilloverSP;

    /// <summary>
    /// Canonical scope churn rate: (AddedSP + RemovedSP) / (CommittedSP + AddedSP).
    /// </summary>
    public double ChurnRate { get; init; }

    /// <summary>
    /// Canonical commitment completion: DeliveredSP / (CommittedSP - RemovedSP).
    /// </summary>
    public double CommitmentCompletion { get; init; }

    /// <summary>
    /// Canonical spillover rate: SpilloverSP / (CommittedSP - RemovedSP).
    /// </summary>
    public double SpilloverRate { get; init; }

    /// <summary>
    /// Canonical added delivery rate: DeliveredFromAddedSP / AddedSP.
    /// </summary>
    public double AddedDeliveryRate { get; init; }

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
