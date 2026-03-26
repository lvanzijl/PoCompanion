using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

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
        ArgumentNullException.ThrowIfNull(request.Feature);
        ArgumentNullException.ThrowIfNull(request.Children);

        if (!CanonicalWorkItemTypes.IsFeature(request.Feature.WorkItemType))
        {
            throw new ArgumentException("Feature progress can only be computed for Feature work items.", nameof(request));
        }

        if (request.Feature.TimeCriticality is < 0d or > 100d)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Feature TimeCriticality must be between 0 and 100.");
        }

        var progressChildren = request.Children
            .Where(child => CanonicalWorkItemTypes.IsFeatureProgressContributor(child.WorkItem.WorkItemType))
            .ToList();

        double completedEffort = 0d;
        double totalEffort = 0d;

        foreach (var child in progressChildren)
        {
            var effort = child.WorkItem.Effort ?? 0d;
            if (effort < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Child work item effort cannot be negative.");
            }

            if (child.StateClassification != StateClassification.Removed)
            {
                totalEffort += effort;
            }

            if (child.StateClassification == StateClassification.Done)
            {
                completedEffort += effort;
            }
        }

        var calculatedProgress = totalEffort > 0d
            ? completedEffort / totalEffort
            : 0d;
        var effectiveProgress = request.Feature.TimeCriticality.HasValue
            ? request.Feature.TimeCriticality.Value / 100d
            : calculatedProgress;

        return new FeatureProgressResult(
            calculatedProgress,
            request.Feature.TimeCriticality,
            effectiveProgress,
            completedEffort,
            totalEffort,
            Array.Empty<string>());
    }
}
