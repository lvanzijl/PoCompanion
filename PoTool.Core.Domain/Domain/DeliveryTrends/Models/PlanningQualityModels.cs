namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Canonical Planning Quality signal codes.
/// </summary>
public static class PlanningQualitySignalCodes
{
    public const string FeatureMissingEffort = "PQ-1";
    public const string FeatureMissingProgressBasis = "PQ-2";
    public const string FeatureUsingOverride = "PQ-3";
    public const string SuspiciousOverrideRange = "PQ-4";
    public const string EpicContainsExcludedFeatures = "PQ-5";
    public const string ProductContainsExcludedEpics = "PQ-6";
    public const string MissingForecastData = "PQ-7";
}

/// <summary>
/// Severity of a Planning Quality signal.
/// </summary>
public enum PlanningQualitySeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>
/// Scope of a Planning Quality signal.
/// </summary>
public enum PlanningQualityScope
{
    Feature = 0,
    Epic = 1,
    Product = 2
}

/// <summary>
/// Prepared inputs for the centralized Planning Quality engine.
/// </summary>
public sealed record PlanningQualityRequest(
    int ProductId,
    IReadOnlyList<FeatureProgress> Features,
    IReadOnlyList<EpicProgress> Epics,
    ProductAggregationResult Product);

/// <summary>
/// One read-only Planning Quality signal.
/// </summary>
public sealed record PlanningQualitySignal(
    string Code,
    PlanningQualitySeverity Severity,
    PlanningQualityScope Scope,
    string Message,
    int EntityId);

/// <summary>
/// Centralized Planning Quality output.
/// </summary>
public sealed record PlanningQualityResult(
    int Score,
    IReadOnlyList<PlanningQualitySignal> Signals);
