using Core.WorkItems;

namespace Core.Contracts;

/// <summary>
/// Interface for TFS/Azure DevOps client operations.
/// All TFS communication must go through implementations of this interface.
/// </summary>
public interface ITfsClient
{
    /// <summary>
    /// Retrieves all work items (Epics, Features, PBIs) under the specified area path.
    /// </summary>
    /// <param name="areaPath">The area path to query work items for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of work items.</returns>
    Task<IReadOnlyCollection<WorkItemDto>> GetWorkItemsByAreaPathAsync(
        string areaPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific work item by its TFS ID.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item if found, null otherwise.</returns>
    Task<WorkItemDto?> GetWorkItemByIdAsync(
        int tfsId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection and authentication to TFS.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
