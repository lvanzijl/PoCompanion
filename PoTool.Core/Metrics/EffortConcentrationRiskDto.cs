namespace PoTool.Core.Metrics;

/// <summary>
/// DTO representing effort concentration risk analysis.
/// Identifies scenarios where effort is concentrated in single features or areas.
/// </summary>
public sealed record EffortConcentrationRiskDto(
    IReadOnlyList<ConcentrationRisk> AreaPathRisks,
    IReadOnlyList<ConcentrationRisk> IterationRisks,
    ConcentrationRiskLevel OverallRiskLevel,
    double ConcentrationIndex,
    IReadOnlyList<RiskMitigationRecommendation> Recommendations,
    DateTimeOffset AnalysisTimestamp
);

/// <summary>
/// Represents concentration risk for a specific area path or iteration.
/// </summary>
public sealed record ConcentrationRisk(
    string Name,
    string Path,
    int EffortAmount,
    double PercentageOfTotal,
    ConcentrationRiskLevel RiskLevel,
    string Description,
    IReadOnlyList<string> TopWorkItems
);

/// <summary>
/// Risk level for concentration analysis.
/// </summary>
public enum ConcentrationRiskLevel
{
    None,     // < 25% concentration
    Low,      // 25-40% concentration
    Medium,   // 40-60% concentration
    High,     // 60-80% concentration
    Critical  // > 80% concentration
}

/// <summary>
/// Represents a recommendation for mitigating concentration risk.
/// </summary>
public sealed record RiskMitigationRecommendation(
    MitigationStrategy Strategy,
    string Title,
    string Description,
    ConcentrationRiskLevel Priority,
    string? TargetPath = null,
    int? EffortToRedistribute = null
);

/// <summary>
/// Type of mitigation strategy.
/// </summary>
public enum MitigationStrategy
{
    DiversifyAcrossAreas,
    SpreadAcrossSprints,
    BreakDownLargeItems,
    AddParallelCapacity,
    DeferNonCritical,
    IncreaseBacklog
}
