namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort imbalance analysis across teams and sprints.
/// Identifies disproportionate allocations and provides rebalancing recommendations.
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
    Low,      // Deviation < 30%
    Medium,   // Deviation 30-50%
    High,     // Deviation 50-80%
    Critical  // Deviation > 80%
}

/// <summary>
/// Represents a recommendation for rebalancing effort distribution.
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
