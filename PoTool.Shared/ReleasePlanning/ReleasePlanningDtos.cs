namespace PoTool.Shared.ReleasePlanning;

/// <summary>
/// Represents a Lane (Objective) on the Release Planning Board.
/// </summary>
public sealed record LaneDto
{
    public int Id { get; init; }
    public int ObjectiveId { get; init; }
    public string ObjectiveTitle { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Validation indicator status for Epic cards.
/// </summary>
public enum ValidationIndicator
{
    None = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// Represents an Epic's placement on the Release Planning Board.
/// </summary>
public sealed record EpicPlacementDto
{
    public int Id { get; init; }
    public int EpicId { get; init; }
    public string EpicTitle { get; init; } = string.Empty;
    public int LaneId { get; init; }
    public int RowIndex { get; init; }
    public int OrderInRow { get; init; }
    public int? Effort { get; init; }
    public string State { get; init; } = string.Empty;
    public ValidationIndicator ValidationIndicator { get; init; } = ValidationIndicator.None;
}

/// <summary>
/// Types of milestones.
/// </summary>
public enum MilestoneType
{
    Release = 0,
    Deadline = 1,
    Custom = 2
}

/// <summary>
/// Represents a Milestone Line on the Release Planning Board.
/// </summary>
public sealed record MilestoneLineDto
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public double VerticalPosition { get; init; }
    public MilestoneType Type { get; init; } = MilestoneType.Release;
}

/// <summary>
/// Represents an Iteration Line on the Release Planning Board.
/// </summary>
public sealed record IterationLineDto
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public double VerticalPosition { get; init; }
}

/// <summary>
/// Types of connectors for rendering.
/// </summary>
public enum ConnectorType
{
    Direct = 0,
    Split = 1,
    Merge = 2,
    Parallel = 3
}

/// <summary>
/// Represents a connector between Epics (derived, not persisted).
/// </summary>
public sealed record ConnectorDto
{
    public int SourcePlacementId { get; init; }
    public int TargetPlacementId { get; init; }
    public ConnectorType Type { get; init; } = ConnectorType.Direct;
}

/// <summary>
/// Represents the complete Release Planning Board state.
/// </summary>
public sealed record ReleasePlanningBoardDto
{
    public IReadOnlyList<LaneDto> Lanes { get; init; } = [];
    public IReadOnlyList<EpicPlacementDto> Placements { get; init; } = [];
    public IReadOnlyList<MilestoneLineDto> MilestoneLines { get; init; } = [];
    public IReadOnlyList<IterationLineDto> IterationLines { get; init; } = [];
    public IReadOnlyList<ConnectorDto> Connectors { get; init; } = [];
    public int MaxRowIndex { get; init; }
    public int TotalRows { get; init; }
}

/// <summary>
/// Represents an Epic that is not yet placed on the Release Planning Board.
/// </summary>
public sealed record UnplannedEpicDto
{
    public int EpicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int ObjectiveId { get; init; }
    public string ObjectiveTitle { get; init; } = string.Empty;
    public int? Effort { get; init; }
    public string State { get; init; } = string.Empty;
    public ValidationIndicator ValidationIndicator { get; init; } = ValidationIndicator.None;
    public int TfsOrder { get; init; }
}

/// <summary>
/// DTO representing an Epic in the context of its parent Objective.
/// </summary>
public sealed record ObjectiveEpicDto
{
    public int EpicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsPlanned { get; init; }
    public int? Effort { get; init; }
    public string State { get; init; } = string.Empty;
    public ValidationIndicator ValidationIndicator { get; init; } = ValidationIndicator.None;
    public int? RowIndex { get; init; }
}

/// <summary>
/// Result of a lane operation.
/// </summary>
public sealed record LaneOperationResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? LaneId { get; init; }
}

/// <summary>
/// Result of an Epic placement operation.
/// </summary>
public sealed record EpicPlacementResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? PlacementId { get; init; }
}

/// <summary>
/// Result of a line operation.
/// </summary>
public sealed record LineOperationResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? LineId { get; init; }
}

/// <summary>
/// Result of a validation cache refresh.
/// </summary>
public sealed record ValidationCacheResultDto
{
    public bool Success { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of an Epic split operation.
/// </summary>
public sealed record EpicSplitResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int OriginalEpicId { get; init; }
    public int? ExtractedEpicId { get; init; }
    public int? OriginalEpicNewEffort { get; init; }
    public int? ExtractedEpicEffort { get; init; }
}
