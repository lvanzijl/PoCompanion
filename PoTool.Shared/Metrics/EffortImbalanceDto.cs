namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort imbalance analysis across teams and sprints.
/// Identifies disproportionate effort-hour allocations by deviation from the mean
/// and provides rebalancing recommendations.
/// </summary>
public sealed record EffortImbalanceDto(
    IReadOnlyList<TeamImbalance> TeamImbalances,
    IReadOnlyList<SprintImbalance> SprintImbalances,
    ImbalanceRiskLevel OverallRiskLevel,
    double ImbalanceScore,
    IReadOnlyList<RebalancingRecommendation> Recommendations,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Represents effort imbalance for a specific team/area path.
/// </summary>
public sealed record TeamImbalance(
    string AreaPath,
    int TotalEffort,
    int AverageEffortAcrossTeams,
    double DeviationPercentage,
    ImbalanceRiskLevel RiskLevel,
    string Description
);

/// <summary>
/// Represents effort imbalance for a specific sprint.
/// </summary>
public sealed record SprintImbalance(
    string IterationPath,
    string SprintName,
    int TotalEffort,
    int AverageEffortAcrossSprints,
    double DeviationPercentage,
    ImbalanceRiskLevel RiskLevel,
    string Description
);

/// <summary>
/// Risk level for imbalance detection.
/// </summary>
public enum ImbalanceRiskLevel
{
    Low,      // Deviation below the configured threshold
    Medium,   // Deviation from threshold up to < 1.5x threshold
    High,     // Deviation from 1.5x threshold up to < 2.5x threshold
    Critical  // Deviation at or above 2.5x threshold
}

/// <summary>
/// Represents a recommendation for rebalancing effort-hour distribution.
/// </summary>
public sealed record RebalancingRecommendation(
    RecommendationType Type,
    string Title,
    string Description,
    ImbalanceRiskLevel Priority,
    string? TargetAreaPath = null,
    string? TargetIterationPath = null,
    int? SuggestedEffortChange = null
);

/// <summary>
/// Type of rebalancing recommendation.
/// </summary>
public enum RecommendationType
{
    ReduceTeamLoad,
    IncreaseTeamLoad,
    LevelSprintLoad,
    RedistributeAcrossTeams,
    AddCapacity,
    DeferWork
}
