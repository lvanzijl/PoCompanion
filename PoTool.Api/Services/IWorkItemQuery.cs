using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Cache-backed analytical Work Item query boundary.
/// Owns materialized cache reads used by analytical handlers only.
/// </summary>
public interface IWorkItemQuery
{
    Task<IReadOnlyList<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemDto>> GetByAreaPathsAsync(
        IReadOnlyList<string> areaPaths,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemDto>> GetByRootIdsAsync(
        IReadOnlyList<int> rootWorkItemIds,
        CancellationToken cancellationToken);
}
