using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Computes deterministic epic progress from canonical feature progress inputs only.
/// </summary>
public interface IEpicProgressService
{
    /// <summary>
    /// Computes the canonical epic progress ratio for one epic.
    /// </summary>
    EpicProgressResult Compute(EpicProgressCalculationRequest request);
}

/// <summary>
/// Pure epic progress engine used as the single source of truth for epic weighted progress.
/// </summary>
public sealed class EpicProgressService : IEpicProgressService
{
    /// <inheritdoc />
    public EpicProgressResult Compute(EpicProgressCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Epic);
        ArgumentNullException.ThrowIfNull(request.Features);

        if (!CanonicalWorkItemTypes.IsEpic(request.Epic.WorkItemType))
        {
            throw new ArgumentException("Epic progress can only be computed for Epic work items.", nameof(request));
        }

        var validFeatures = request.Features
            .Where(feature => CanonicalWorkItemTypes.IsFeature(feature.Feature.WorkItemType))
            .ToList();

        foreach (var feature in validFeatures)
        {
            if (feature.EffectiveProgress < 0d || feature.EffectiveProgress > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Feature effective progress must be between 0 and 1.");
            }

            if (feature.TotalEffort < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Feature total effort cannot be negative.");
            }
        }

        var totalWeight = validFeatures.Sum(feature => feature.TotalEffort);
        var includedFeaturesCount = validFeatures.Count(feature => feature.TotalEffort > 0d);
        var excludedFeaturesCount = validFeatures.Count - includedFeaturesCount;
        var epicProgress = totalWeight > 0d
            ? validFeatures.Sum(feature => feature.EffectiveProgress * feature.TotalEffort) / totalWeight
            : 0d;

        return new EpicProgressResult(
            epicProgress,
            excludedFeaturesCount,
            includedFeaturesCount,
            totalWeight);
    }
}
