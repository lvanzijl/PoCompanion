using PoTool.Core.WorkItems;

namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for TFS/Azure DevOps integration.
/// Abstracts all TFS communication from the application.
/// </summary>
public interface ITfsClient
{
    /// <summary>
    /// Retrieves work items under the specified area path.
    /// </summary>
    /// <param name="areaPath">The area path to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of work item DTOs.</returns>
    Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the TFS connection is working with the configured PAT.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is valid, false otherwise.</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
}
