using Mediator;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to re-fetch a single work item from TFS and update the local DB.
/// Used by the Fix Session "Refresh from TFS" feature.
/// </summary>
/// <param name="TfsId">TFS work item ID to refresh.</param>
public sealed record RefreshWorkItemFromTfsCommand(int TfsId) : ICommand<bool>;
