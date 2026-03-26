using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public interface IPortfolioReadModelMapper
{
    PortfolioLifecycleState ToLifecycleState(WorkPackageLifecycleState lifecycleState);

    PortfolioSnapshotItemDto ToSnapshotItemDto(
        PortfolioSnapshotItem item,
        IReadOnlyDictionary<int, string> productNames);

    PortfolioComparisonItemDto ToComparisonItemDto(
        PortfolioSnapshotComparisonItem item,
        IReadOnlyDictionary<int, string> productNames);
}

public sealed class PortfolioReadModelMapper : IPortfolioReadModelMapper
{
    public PortfolioLifecycleState ToLifecycleState(WorkPackageLifecycleState lifecycleState)
        => lifecycleState switch
        {
            WorkPackageLifecycleState.Active => PortfolioLifecycleState.Active,
            WorkPackageLifecycleState.Retired => PortfolioLifecycleState.Retired,
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycleState), lifecycleState, "Unsupported lifecycle state.")
        };

    public PortfolioSnapshotItemDto ToSnapshotItemDto(
        PortfolioSnapshotItem item,
        IReadOnlyDictionary<int, string> productNames)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(productNames);

        if (!productNames.TryGetValue(item.ProductId, out var productName) || string.IsNullOrWhiteSpace(productName))
        {
            throw new InvalidOperationException($"Product name mapping is missing for ProductId {item.ProductId}.");
        }

        return new PortfolioSnapshotItemDto
        {
            ProductId = item.ProductId,
            ProductName = productName,
            ProjectNumber = item.ProjectNumber,
            WorkPackage = item.WorkPackage,
            LifecycleState = ToLifecycleState(item.LifecycleState),
            Progress = item.Progress,
            Weight = item.TotalWeight
        };
    }

    public PortfolioComparisonItemDto ToComparisonItemDto(
        PortfolioSnapshotComparisonItem item,
        IReadOnlyDictionary<int, string> productNames)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(productNames);

        if (!productNames.TryGetValue(item.ProductId, out var productName) || string.IsNullOrWhiteSpace(productName))
        {
            throw new InvalidOperationException($"Product name mapping is missing for ProductId {item.ProductId}.");
        }

        return new PortfolioComparisonItemDto
        {
            ProductId = item.ProductId,
            ProductName = productName,
            ProjectNumber = item.ProjectNumber,
            WorkPackage = item.WorkPackage,
            PreviousLifecycleState = item.PreviousLifecycleState.HasValue ? ToLifecycleState(item.PreviousLifecycleState.Value) : null,
            CurrentLifecycleState = item.CurrentLifecycleState.HasValue ? ToLifecycleState(item.CurrentLifecycleState.Value) : null,
            PreviousProgress = item.PreviousProgress,
            CurrentProgress = item.CurrentProgress,
            ProgressDelta = item.ProgressDelta,
            PreviousWeight = item.PreviousWeight,
            CurrentWeight = item.CurrentWeight,
            WeightDelta = item.WeightDelta
        };
    }
}
