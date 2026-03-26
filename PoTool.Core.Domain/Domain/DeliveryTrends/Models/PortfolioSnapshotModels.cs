namespace PoTool.Core.Domain.DeliveryTrends.Models;

public enum WorkPackageLifecycleState
{
    Active = 0,
    Retired = 1
}

/// <summary>
/// Canonical portfolio snapshot captured at a single point in time for CDC delivery comparison.
/// </summary>
public sealed record PortfolioSnapshot
{
    public PortfolioSnapshot(DateTimeOffset timestamp, IReadOnlyList<PortfolioSnapshotItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("Portfolio snapshot must contain at least one item.", nameof(items));
        }

        var duplicateKey = items
            .GroupBy(item => item.BusinessKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new ArgumentException(
                $"Portfolio snapshot contains duplicate business key '{duplicateKey.Key}'.",
                nameof(items));
        }

        foreach (var projectGroup in items.GroupBy(item => item.ProjectKey))
        {
            var hasProjectLevelRows = projectGroup.Any(item => item.WorkPackage is null);
            var hasWorkPackageRows = projectGroup.Any(item => item.WorkPackage is not null);

            if (hasProjectLevelRows && hasWorkPackageRows)
            {
                throw new ArgumentException(
                    $"Project '{projectGroup.Key}' mixes project-level and work-package rows in the same snapshot.",
                    nameof(items));
            }
        }

        Timestamp = timestamp;
        Items = items.ToArray();
    }

    public DateTimeOffset Timestamp { get; }

    public IReadOnlyList<PortfolioSnapshotItem> Items { get; }
}

/// <summary>
/// Canonical project or work-package row captured inside a CDC portfolio snapshot.
/// </summary>
public sealed record PortfolioSnapshotItem
{
    public PortfolioSnapshotItem(
        int productId,
        string projectNumber,
        string? workPackage,
        double progress,
        double totalWeight,
        WorkPackageLifecycleState lifecycleState = WorkPackageLifecycleState.Active)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "ProductId");
        DeliveryTrendModelValidation.ValidateRequiredText(projectNumber, nameof(projectNumber), "ProjectNumber");

        if (workPackage is not null)
        {
            DeliveryTrendModelValidation.ValidateRequiredText(workPackage, nameof(workPackage), "WorkPackage");
        }

        DeliveryTrendModelValidation.ValidateUnitInterval(progress, nameof(progress), "Progress");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(totalWeight, nameof(totalWeight), "TotalWeight");

        ProductId = productId;
        ProjectNumber = projectNumber;
        WorkPackage = workPackage;
        Progress = progress;
        TotalWeight = totalWeight;
        LifecycleState = lifecycleState;
    }

    public int ProductId { get; }

    public string ProjectNumber { get; }

    public string? WorkPackage { get; }

    public double Progress { get; }

    public double TotalWeight { get; }

    public WorkPackageLifecycleState LifecycleState { get; }

    internal PortfolioSnapshotProjectKey ProjectKey => new(ProductId, ProjectNumber);

    internal PortfolioSnapshotBusinessKey BusinessKey => new(ProductId, ProjectNumber, WorkPackage);
}

/// <summary>
/// Deterministic capture request for a new portfolio snapshot.
/// </summary>
public sealed record PortfolioSnapshotFactoryRequest(
    DateTimeOffset Timestamp,
    IReadOnlyList<PortfolioSnapshotFactoryEpicInput> Epics,
    PortfolioSnapshot? PreviousSnapshot)
{
    public PortfolioSnapshotFactoryRequest(DateTimeOffset timestamp, IReadOnlyList<PortfolioSnapshotFactoryEpicInput> epics)
        : this(timestamp, epics, null)
    {
    }
}

/// <summary>
/// Canonical current-state input row for deterministic portfolio snapshot capture.
/// </summary>
public sealed record PortfolioSnapshotFactoryEpicInput
{
    public PortfolioSnapshotFactoryEpicInput(
        int productId,
        string projectNumber,
        string? workPackage,
        double progress,
        double weight)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "ProductId");
        DeliveryTrendModelValidation.ValidateRequiredText(projectNumber, nameof(projectNumber), "ProjectNumber");

        if (workPackage is not null)
        {
            DeliveryTrendModelValidation.ValidateRequiredText(workPackage, nameof(workPackage), "WorkPackage");
        }

        DeliveryTrendModelValidation.ValidateUnitInterval(progress, nameof(progress), "Progress");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(weight, nameof(weight), "Weight");

        ProductId = productId;
        ProjectNumber = projectNumber;
        WorkPackage = workPackage;
        Progress = progress;
        Weight = weight;
    }

    public int ProductId { get; }

    public string ProjectNumber { get; }

    public string? WorkPackage { get; }

    public double Progress { get; }

    public double Weight { get; }
}

/// <summary>
/// Comparison request for two CDC portfolio snapshots.
/// </summary>
public sealed record PortfolioSnapshotComparisonRequest(
    PortfolioSnapshot? Previous,
    PortfolioSnapshot Current);

/// <summary>
/// Delta output for one exact business key across two portfolio snapshots.
/// </summary>
public sealed record PortfolioSnapshotComparisonItem(
    int ProductId,
    string ProjectNumber,
    string? WorkPackage,
    WorkPackageLifecycleState? PreviousLifecycleState,
    WorkPackageLifecycleState? CurrentLifecycleState,
    double? PreviousProgress,
    double? CurrentProgress,
    double? ProgressDelta,
    double? PreviousWeight,
    double? CurrentWeight,
    double? WeightDelta);

/// <summary>
/// Deterministic comparison result for a portfolio snapshot delta calculation.
/// </summary>
public sealed record PortfolioSnapshotComparisonResult(
    IReadOnlyList<PortfolioSnapshotComparisonItem> Items);

internal readonly record struct PortfolioSnapshotProjectKey(
    int ProductId,
    string ProjectNumber)
{
    public override string ToString() => $"{ProductId}:{ProjectNumber}";
}

internal readonly record struct PortfolioSnapshotBusinessKey(
    int ProductId,
    string ProjectNumber,
    string? WorkPackage)
{
    public override string ToString() => $"{ProductId}:{ProjectNumber}:{WorkPackage ?? "<project>"}";
}

internal sealed class PortfolioSnapshotBusinessKeyComparer : IComparer<PortfolioSnapshotBusinessKey>
{
    public static PortfolioSnapshotBusinessKeyComparer Instance { get; } = new();

    public int Compare(PortfolioSnapshotBusinessKey x, PortfolioSnapshotBusinessKey y)
    {
        var productComparison = x.ProductId.CompareTo(y.ProductId);
        if (productComparison != 0)
        {
            return productComparison;
        }

        var projectComparison = StringComparer.Ordinal.Compare(x.ProjectNumber, y.ProjectNumber);
        if (projectComparison != 0)
        {
            return projectComparison;
        }

        var workPackageKindComparison = (x.WorkPackage is null ? 0 : 1).CompareTo(y.WorkPackage is null ? 0 : 1);
        if (workPackageKindComparison != 0)
        {
            return workPackageKindComparison;
        }

        return StringComparer.Ordinal.Compare(x.WorkPackage, y.WorkPackage);
    }
}
