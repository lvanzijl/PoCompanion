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

        var historicalWorkPackageProjects = priorSnapshots
            .SelectMany(snapshot => snapshot.Items)
            .Where(item => item.WorkPackage is not null)
            .Select(item => item.ProjectKey)
            .Distinct()
            .ToArray();

        foreach (var projectGroup in candidateSnapshot.Items.GroupBy(item => item.ProjectKey))
        {
            if (historicalWorkPackageProjects.Contains(projectGroup.Key)
                && projectGroup.Any(item => item.WorkPackage is null))
            {
                throw new InvalidOperationException(
                    $"Project '{projectGroup.Key}' previously used work-package breakdown and cannot revert to project-level rows.");
            }
        }

        var projectKeysToValidate = historicalWorkPackageProjects
            .Concat(candidateSnapshot.Items.Where(item => item.WorkPackage is not null).Select(item => item.ProjectKey))
            .Distinct()
            .ToArray();

        foreach (var projectKey in projectKeysToValidate)
        {
            var latestHistoricalLifecycleByKey = GetLatestHistoricalLifecycleByKey(priorSnapshots, projectKey);
            if (latestHistoricalLifecycleByKey.Count == 0)
            {
                continue;
            }

            var candidateItems = candidateSnapshot.Items
                .Where(item => item.ProjectKey == projectKey && item.WorkPackage is not null)
                .ToArray();

            var retiredKeys = latestHistoricalLifecycleByKey
                .Where(entry => entry.Value == WorkPackageLifecycleState.Retired)
                .Select(entry => entry.Key)
                .ToHashSet();
            if (candidateItems.Any(item =>
                    item.LifecycleState == WorkPackageLifecycleState.Active
                    && retiredKeys.Contains(item.BusinessKey)))
            {
                throw new InvalidOperationException(
                    $"Project '{projectKey}' cannot reactivate a retired work package.");
            }

            var requiredActiveKeys = latestHistoricalLifecycleByKey
                .Where(entry => entry.Value == WorkPackageLifecycleState.Active)
                .Select(entry => entry.Key)
                .ToHashSet();
            var candidateKeys = candidateItems
                .Select(item => item.BusinessKey)
                .ToHashSet();

            if (!requiredActiveKeys.IsSubsetOf(candidateKeys))
            {
                throw new InvalidOperationException(
                    $"Project '{projectKey}' omits one or more required active work packages from the latest breakdown snapshot.");
            }
        }
    }

    private static Dictionary<PortfolioSnapshotBusinessKey, WorkPackageLifecycleState> GetLatestHistoricalLifecycleByKey(
        IEnumerable<PortfolioSnapshot> priorSnapshots,
        PortfolioSnapshotProjectKey projectKey)
    {
        var lifecycleByKey = new Dictionary<PortfolioSnapshotBusinessKey, WorkPackageLifecycleState>();

        foreach (var item in priorSnapshots
                     .SelectMany(snapshot => snapshot.Items)
                     .Where(item => item.ProjectKey == projectKey && item.WorkPackage is not null))
        {
            lifecycleByKey[item.BusinessKey] = item.LifecycleState;
        }

        return lifecycleByKey;
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
            .OrderBy(key => key, PortfolioSnapshotBusinessKeyComparer.Instance)
            .ToArray();

        var items = allKeys
            .Select(key =>
            {
                previousItems.TryGetValue(key, out var previousItem);
                currentItems.TryGetValue(key, out var currentItem);
                var previousAggregationItem = previousItem?.LifecycleState == WorkPackageLifecycleState.Active ? previousItem : null;
                var currentAggregationItem = currentItem?.LifecycleState == WorkPackageLifecycleState.Active ? currentItem : null;

                return new PortfolioSnapshotComparisonItem(
                    key.ProductId,
                    key.ProjectNumber,
                    key.WorkPackage,
                    previousItem?.LifecycleState,
                    currentItem?.LifecycleState,
                    previousAggregationItem?.Progress,
                    currentAggregationItem?.Progress,
                    ComputeDelta(previousAggregationItem?.Progress, currentAggregationItem?.Progress),
                    previousAggregationItem?.TotalWeight,
                    currentAggregationItem?.TotalWeight,
                    ComputeDelta(previousAggregationItem?.TotalWeight, currentAggregationItem?.TotalWeight));
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
