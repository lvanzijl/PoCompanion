using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Service for managing work item state classifications.
/// </summary>
public interface IWorkItemStateClassificationService
{
    /// <summary>
    /// Gets the current state classifications for the configured TFS project.
    /// Returns default mappings if no custom configuration exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state classifications response.</returns>
    Task<GetStateClassificationsResponse> GetClassificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves state classifications for the configured TFS project.
    /// Replaces all existing classifications with the provided set.
    /// </summary>
    /// <param name="request">The save request with classifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> SaveClassificationsAsync(SaveStateClassificationsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a state is classified as "Done" for a given work item type.
    /// </summary>
    /// <param name="workItemType">The work item type.</param>
    /// <param name="state">The state name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the state is classified as Done, false otherwise.</returns>
    Task<bool> IsDoneStateAsync(string workItemType, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a state is classified as "In Progress" for a given work item type.
    /// </summary>
    /// <param name="workItemType">The work item type.</param>
    /// <param name="state">The state name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the state is classified as In Progress, false otherwise.</returns>
    Task<bool> IsInProgressStateAsync(string workItemType, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a state is classified as "New" for a given work item type.
    /// </summary>
    /// <param name="workItemType">The work item type.</param>
    /// <param name="state">The state name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the state is classified as New, false otherwise.</returns>
    Task<bool> IsNewStateAsync(string workItemType, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the classification for a specific state of a work item type.
    /// </summary>
    /// <param name="workItemType">The work item type.</param>
    /// <param name="state">The state name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state classification.</returns>
    Task<StateClassification> GetClassificationAsync(string workItemType, string state, CancellationToken cancellationToken = default);
}
