using PoTool.Shared.Metrics;

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
