using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Cache-backed analytical Work Item query boundary.
/// Owns materialized cache reads used by analytical handlers only.
/// </summary>
public interface IWorkItemQuery
{
    Task<IReadOnlyList<WorkItemDto>> GetGoalHierarchyAsync(
        IReadOnlyList<int> goalIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemDto>> GetWorkItemsForListingAsync(
        IReadOnlyList<int>? productIds,
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemDto>> GetGoalsForListingAsync(
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken);

    Task<DependencyGraphQuerySource> GetDependencyGraphSourceAsync(
        string? areaPathFilter,
        IReadOnlyList<int>? workItemIds,
        IReadOnlyList<string>? workItemTypes,
        CancellationToken cancellationToken);

    Task<ValidationImpactQuerySource> GetValidationImpactSourceAsync(
        string? areaPathFilter,
        string? iterationPathFilter,
        CancellationToken cancellationToken);

    Task<ProductBacklogAnalyticsSource?> GetProductBacklogAnalyticsSourceAsync(
        int productId,
        CancellationToken cancellationToken);
}

public sealed record DependencyGraphQuerySource(
    IReadOnlyList<WorkItemDto> ScopedWorkItems,
    IReadOnlyList<WorkItemDto> RelevantWorkItems);

public sealed record ValidationImpactQuerySource(
    IReadOnlyList<WorkItemDto> WorkItems,
    IReadOnlyDictionary<int, IReadOnlyList<int>> ChildrenByParentId);

public sealed record ProductBacklogAnalyticsSource(
    int ProductId,
    IReadOnlyList<WorkItemDto> WorkItems);
