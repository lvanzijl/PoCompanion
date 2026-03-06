using Mediator;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to update the BacklogPriority of a work item in TFS and refresh the local cache.
/// Used by Product Roadmaps to reorder product lanes (Objectives) via swap-with-neighbour.
/// </summary>
/// <param name="TfsId">TFS work item ID whose BacklogPriority should be updated.</param>
/// <param name="NewBacklogPriority">The new BacklogPriority value to set in TFS.</param>
public sealed record UpdateWorkItemBacklogPriorityCommand(int TfsId, double NewBacklogPriority) : ICommand<bool>;
