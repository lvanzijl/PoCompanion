namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Supported calculation modes for feature progress.
/// </summary>
public enum FeatureProgressMode
{
    /// <summary>
    /// Calculate progress using story points.
    /// </summary>
    StoryPoints = 0,

    /// <summary>
    /// Calculate progress using PBI counts.
    /// </summary>
    Count = 1
}

/// <summary>
/// Known feature-progress validation signals emitted by the canonical progress engine.
/// </summary>
public static class FeatureProgressValidationSignals
{
    public const string OverrideOutOfRange = "OverrideOutOfRange";
    public const string OverrideLikelyWrongScale = "OverrideLikelyWrongScale";
}

/// <summary>
/// Pure calculation request for the canonical feature progress engine.
/// </summary>
public sealed record FeatureProgressCalculationRequest(
    FeatureProgressMode Mode,
    int TotalPbiCount,
    int CompletedPbiCount,
    double TotalStoryPoints,
    double CompletedStoryPoints,
    double? Override);

/// <summary>
/// Canonical feature progress engine result.
/// </summary>
public sealed record FeatureProgressResult(
    double? CalculatedProgress,
    double? Override,
    double? EffectiveProgress,
    IReadOnlyList<string> ValidationSignals);
