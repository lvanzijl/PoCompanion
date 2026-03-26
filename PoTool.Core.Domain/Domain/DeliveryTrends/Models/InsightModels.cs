namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Canonical insight codes produced by the centralized decision-signal layer.
/// </summary>
public static class InsightCodes
{
    public const string ProgressStalled = "IN-1";
    public const string ProgressReversed = "IN-2";
    public const string ScopeIncreasedFasterThanDelivery = "IN-3";
    public const string HealthyProgress = "IN-4";
    public const string LowPlanningQuality = "IN-5";
    public const string VeryLowPlanningQuality = "IN-6";
    public const string ForecastUnreliable = "IN-7";
    public const string ProgressUnknown = "IN-8";
}

/// <summary>
/// Severity of a derived insight.
/// </summary>
public enum InsightSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// Combined upstream inputs for the centralized insight engine.
/// </summary>
public sealed record InsightRequest(
    ProductAggregationResult Product,
    SnapshotComparisonResult Comparison,
    PlanningQualityResult PlanningQuality);

/// <summary>
/// Explainability payload attached to every insight.
/// </summary>
public sealed record InsightContext(
    double? ProgressDelta,
    double? ForecastRemainingDelta,
    int PlanningQualityScore);

/// <summary>
/// One derived decision signal.
/// </summary>
public sealed record Insight(
    string Code,
    InsightSeverity Severity,
    string Message,
    InsightContext Context);

/// <summary>
/// Centralized insight output.
/// </summary>
public sealed record InsightResult(
    IReadOnlyList<Insight> Insights);
