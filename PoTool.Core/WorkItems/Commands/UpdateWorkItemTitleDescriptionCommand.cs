using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to update the title and/or description of a work item in TFS and refresh the local cache.
/// Used by Product Roadmap editor drawer for epic editing.
/// </summary>
/// <param name="TfsId">TFS work item ID whose fields should be updated.</param>
/// <param name="Title">New title (null to leave unchanged).</param>
/// <param name="Description">New description (null to leave unchanged).</param>
public sealed record UpdateWorkItemTitleDescriptionCommand(int TfsId, string? Title, string? Description) : ICommand<WorkItemDto?>;
