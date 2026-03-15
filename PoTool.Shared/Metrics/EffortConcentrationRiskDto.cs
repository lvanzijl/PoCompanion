namespace PoTool.Shared.Metrics;

/// <summary>
/// DTO representing effort concentration risk analysis.
/// Identifies scenarios where effort hours are concentrated in single areas or iterations.
/// The concentration index is an HHI-style score calculated from the full area and iteration distributions,
/// while the returned risk lists include only buckets that reach the fixed 25% low-risk threshold.
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
    None,     // Concentration below 25%
    Low,      // Concentration from 25% up to < 40%
    Medium,   // Concentration from 40% up to < 60%
    High,     // Concentration from 60% up to < 80%
    Critical  // Concentration at or above 80%
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
