using Mediator;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to trigger synchronization of work items from TFS.
/// </summary>
public sealed record SyncWorkItemsCommand(string AreaPath) : ICommand;
