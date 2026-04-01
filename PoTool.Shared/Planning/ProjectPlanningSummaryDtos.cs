namespace PoTool.Shared.Planning;

/// <summary>
/// Read-only planning summary for a project-scoped planning context.
/// </summary>
public sealed record ProjectPlanningSummaryDto(
    string ProjectAlias,
    int ProductCount,
    int TotalEpics,
    int TotalPBIs,
    int PlannedPBIs,
    int UnplannedPBIs,
    int TotalEffort,
    int PlannedEffort,
    double CapacityPerSprint,
    bool OvercommitIndicator,
    IReadOnlyList<ProjectPlanningProductSummaryDto> Products)
{
    public ProjectPlanningSummaryDto()
        : this(string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, false, Array.Empty<ProjectPlanningProductSummaryDto>())
    {
    }
}

/// <summary>
/// Read-only product-level breakdown inside a project planning summary.
/// </summary>
public sealed record ProjectPlanningProductSummaryDto(
    int ProductId,
    string ProductName,
    int EpicCount,
    int TotalPBIs,
    int PlannedPBIs,
    int UnplannedPBIs,
    int TotalEffort,
    int PlannedEffort,
    double CapacityPerSprint)
{
    public ProjectPlanningProductSummaryDto()
        : this(0, string.Empty, 0, 0, 0, 0, 0, 0, 0)
    {
    }
}
