using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing effort distribution across area paths and iterations.
/// Provides visual heat map data for capacity planning.
/// </summary>
public sealed record EffortDistributionDto(
    IReadOnlyList<EffortByAreaPath> EffortByArea,
    IReadOnlyList<EffortByIteration> EffortByIteration,
    IReadOnlyList<EffortHeatMapCell> HeatMapData,
    int TotalEffort,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Effort aggregated by area path.
/// </summary>
public sealed record EffortByAreaPath(
    string AreaPath,
    int TotalEffort,
    int WorkItemCount,
    double AverageEffortPerItem
);

/// <summary>
/// Effort aggregated by iteration.
/// </summary>
public sealed record EffortByIteration(
    string IterationPath,
    string SprintName,
    int TotalEffort,
    int WorkItemCount,
    int? Capacity,
    double UtilizationPercentage
);

/// <summary>
/// Heat map cell showing effort at the intersection of area path and iteration.
/// </summary>
public sealed record EffortHeatMapCell(
    string AreaPath,
    string IterationPath,
    int Effort,
    int WorkItemCount,
    CapacityStatus Status
);
