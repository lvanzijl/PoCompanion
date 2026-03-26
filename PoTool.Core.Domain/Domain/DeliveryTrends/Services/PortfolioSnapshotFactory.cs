using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Services;

/// <summary>
/// Creates deterministic CDC portfolio snapshots from current delivery state and the immediately previous snapshot.
/// </summary>
public interface IPortfolioSnapshotFactory
{
    /// <summary>
    /// Creates a lifecycle-aware snapshot using a single captured timestamp for all rows.
    /// </summary>
    PortfolioSnapshot Create(PortfolioSnapshotFactoryRequest request);
}

/// <summary>
/// Deterministic snapshot factory that assigns active/retired lifecycle state without mutating historical snapshots.
/// </summary>
public sealed class PortfolioSnapshotFactory : IPortfolioSnapshotFactory
{
    private readonly IPortfolioSnapshotValidationService _validationService;

    public PortfolioSnapshotFactory(IPortfolioSnapshotValidationService? validationService = null)
    {
        _validationService = validationService ?? new PortfolioSnapshotValidationService();
    }

    /// <inheritdoc />
    public PortfolioSnapshot Create(PortfolioSnapshotFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Epics);

        var activeItems = request.Epics
            .Select(epic => new PortfolioSnapshotItem(
                epic.ProductId,
                epic.ProjectNumber,
                epic.WorkPackage,
                epic.Progress,
                epic.Weight,
                WorkPackageLifecycleState.Active))
            .ToArray();
        var activeItemsByKey = activeItems.ToDictionary(item => item.BusinessKey);

        var retiredItems = request.PreviousSnapshot?.Items
            .Where(item =>
                item.WorkPackage is not null
                && item.LifecycleState == WorkPackageLifecycleState.Active
                && !activeItemsByKey.ContainsKey(item.BusinessKey))
            .Select(item => new PortfolioSnapshotItem(
                item.ProductId,
                item.ProjectNumber,
                item.WorkPackage,
                item.Progress,
                item.TotalWeight,
                WorkPackageLifecycleState.Retired))
            .ToArray() ?? [];

        if (request.PreviousSnapshot is not null
            && activeItems.Any(item =>
                request.PreviousSnapshot.Items.Any(previousItem =>
                    previousItem.BusinessKey == item.BusinessKey
                    && previousItem.LifecycleState == WorkPackageLifecycleState.Retired)))
        {
            throw new InvalidOperationException("Portfolio snapshot factory cannot reactivate a retired work package.");
        }

        var snapshot = new PortfolioSnapshot(
            request.Timestamp,
            activeItems
                .Concat(retiredItems)
                .OrderBy(item => item.BusinessKey, PortfolioSnapshotBusinessKeyComparer.Instance)
                .ToArray());

        _validationService.ValidateCreation(
            request.PreviousSnapshot is null ? [] : [request.PreviousSnapshot],
            snapshot);

        return snapshot;
    }
}
