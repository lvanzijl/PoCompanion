using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Validates CDC portfolio snapshot creation rules before persistence or further processing.
/// </summary>
public interface IPortfolioSnapshotValidationService
{
    /// <summary>
    /// Ensures a candidate snapshot can be added after the existing ordered or unordered snapshot history.
    /// </summary>
    void ValidateCreation(IEnumerable<PortfolioSnapshot> existingSnapshots, PortfolioSnapshot candidateSnapshot);
}

/// <summary>
/// Compares two validated CDC portfolio snapshots by exact business key.
/// </summary>
public interface IPortfolioSnapshotComparisonService
{
    /// <summary>
    /// Produces a deterministic delta output for all comparable business keys.
    /// </summary>
    PortfolioSnapshotComparisonResult Compare(PortfolioSnapshotComparisonRequest request);
}

/// <summary>
/// Enforces the canonical CDC snapshot lifecycle rules around project/work-package consistency.
/// </summary>
public sealed class PortfolioSnapshotValidationService : IPortfolioSnapshotValidationService
{
    /// <inheritdoc />
    public void ValidateCreation(IEnumerable<PortfolioSnapshot> existingSnapshots, PortfolioSnapshot candidateSnapshot)
    {
        ArgumentNullException.ThrowIfNull(existingSnapshots);
        ArgumentNullException.ThrowIfNull(candidateSnapshot);

        var priorSnapshots = existingSnapshots
            .Where(snapshot => snapshot.Timestamp < candidateSnapshot.Timestamp)
            .OrderBy(snapshot => snapshot.Timestamp)
            .ToList();

        foreach (var projectGroup in candidateSnapshot.Items.GroupBy(item => item.ProjectKey))
        {
            var latestHistoricalBreakdown = priorSnapshots
                .SelectMany(snapshot => snapshot.Items)
                .Where(item => item.ProjectKey == projectGroup.Key && item.WorkPackage is not null)
                .GroupBy(item => item.Timestamp)
                .OrderBy(group => group.Key)
                .LastOrDefault();

            if (latestHistoricalBreakdown is null)
            {
                continue;
            }

            if (projectGroup.Any(item => item.WorkPackage is null))
            {
                throw new InvalidOperationException(
                    $"Project '{projectGroup.Key}' previously used work-package breakdown and cannot revert to project-level rows.");
            }

            var requiredWorkPackages = latestHistoricalBreakdown
                .Select(item => item.WorkPackage!)
                .ToHashSet(StringComparer.Ordinal);
            var candidateWorkPackages = projectGroup
                .Select(item => item.WorkPackage!)
                .ToHashSet(StringComparer.Ordinal);

            if (!requiredWorkPackages.IsSubsetOf(candidateWorkPackages))
            {
                throw new InvalidOperationException(
                    $"Project '{projectGroup.Key}' omits one or more required work packages from the latest breakdown snapshot.");
            }
        }
    }
}

/// <summary>
/// Deterministic CDC portfolio snapshot delta engine with exact business-key matching only.
/// </summary>
public sealed class PortfolioSnapshotComparisonService : IPortfolioSnapshotComparisonService
{
    /// <inheritdoc />
    public PortfolioSnapshotComparisonResult Compare(PortfolioSnapshotComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Current);

        if (request.Previous is not null && request.Previous.Timestamp > request.Current.Timestamp)
        {
            throw new ArgumentException("Previous snapshot timestamp must be earlier than or equal to the current snapshot timestamp.", nameof(request));
        }

        var previousItems = request.Previous?.Items.ToDictionary(item => item.BusinessKey) ?? [];
        var currentItems = request.Current.Items.ToDictionary(item => item.BusinessKey);

        var allKeys = previousItems.Keys
            .Concat(currentItems.Keys)
            .Distinct()
            .OrderBy(key => key.ProductId)
            .ThenBy(key => key.ProjectNumber, StringComparer.Ordinal)
            .ThenBy(key => key.WorkPackage is null ? 0 : 1)
            .ThenBy(key => key.WorkPackage, StringComparer.Ordinal)
            .ToArray();

        var items = allKeys
            .Select(key =>
            {
                previousItems.TryGetValue(key, out var previousItem);
                currentItems.TryGetValue(key, out var currentItem);

                return new PortfolioSnapshotComparisonItem(
                    key.ProductId,
                    key.ProjectNumber,
                    key.WorkPackage,
                    previousItem?.Progress,
                    currentItem?.Progress,
                    ComputeDelta(previousItem?.Progress, currentItem?.Progress),
                    previousItem?.TotalWeight,
                    currentItem?.TotalWeight,
                    ComputeDelta(previousItem?.TotalWeight, currentItem?.TotalWeight));
            })
            .ToArray();

        return new PortfolioSnapshotComparisonResult(items);
    }

    private static double? ComputeDelta(double? previous, double? current)
    {
        if (!previous.HasValue || !current.HasValue)
        {
            return null;
        }

        return current.Value - previous.Value;
    }
}
