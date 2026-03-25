using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes canonical feature progress, override handling, and validation signals.
/// </summary>
public interface IFeatureProgressService
{
    /// <summary>
    /// Computes the canonical feature progress result for one feature.
    /// </summary>
    FeatureProgressResult Compute(FeatureProgressCalculationRequest request);
}

/// <summary>
/// Pure feature progress engine used as the single source of truth for calculated and effective progress.
/// </summary>
public sealed class FeatureProgressService : IFeatureProgressService
{
    /// <inheritdoc />
    public FeatureProgressResult Compute(FeatureProgressCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TotalPbiCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TotalPbiCount));
        }

        if (request.CompletedPbiCount < 0 || request.CompletedPbiCount > request.TotalPbiCount)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CompletedPbiCount));
        }

        if (request.TotalStoryPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TotalStoryPoints));
        }

        if (request.CompletedStoryPoints < 0 || request.CompletedStoryPoints > request.TotalStoryPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CompletedStoryPoints));
        }

        var validationSignals = new List<string>();
        var rawOverride = request.Override;
        double? clampedOverride = rawOverride.HasValue
            ? Math.Clamp(rawOverride.Value, 0d, 100d)
            : null;

        if (rawOverride is < 0d or > 100d)
        {
            validationSignals.Add(FeatureProgressValidationSignals.OverrideOutOfRange);
        }

        if (rawOverride is > 0d and <= 1d)
        {
            validationSignals.Add(FeatureProgressValidationSignals.OverrideLikelyWrongScale);
        }

        double? calculatedProgress = request.Mode switch
        {
            FeatureProgressMode.StoryPoints when request.TotalStoryPoints > 0
                => request.CompletedStoryPoints / request.TotalStoryPoints * 100d,
            FeatureProgressMode.Count when request.TotalPbiCount > 0
                => request.CompletedPbiCount / (double)request.TotalPbiCount * 100d,
            _ => null
        };

        return new FeatureProgressResult(
            calculatedProgress,
            rawOverride,
            clampedOverride ?? calculatedProgress,
            validationSignals);
    }
}
