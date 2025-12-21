namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing sprint capacity planning with effort vs capacity analysis.
/// </summary>
public sealed record SprintCapacityPlanDto(
    string IterationPath,
    string SprintName,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    int TotalPlannedEffort,
    int TotalCapacity,
    double UtilizationPercentage,
    CapacityStatus Status,
    IReadOnlyList<TeamMemberCapacity> TeamCapacities,
    IReadOnlyList<CapacityWarning> Warnings,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Capacity information for a team member.
/// </summary>
public sealed record TeamMemberCapacity(
    string MemberName,
    int AssignedEffort,
    int AvailableCapacity,
    double UtilizationPercentage,
    CapacityStatus Status
);

/// <summary>
/// Warning about capacity planning issues.
/// </summary>
public sealed record CapacityWarning(
    WarningLevel Level,
    string Message,
    IReadOnlyList<string> AffectedMembers
);

/// <summary>
/// Severity level of a capacity warning.
/// </summary>
public enum WarningLevel
{
    Info,
    Warning,
    Critical
}
