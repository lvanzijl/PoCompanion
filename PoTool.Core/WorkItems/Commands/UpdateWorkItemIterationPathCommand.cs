using Mediator;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to update the IterationPath (sprint assignment) of a work item in TFS and refresh the local cache.
/// Used by the Plan Board to move PBIs and bugs between sprints.
/// </summary>
/// <param name="TfsId">TFS work item ID whose IterationPath should be updated.</param>
/// <param name="NewIterationPath">The new IterationPath value to set in TFS.</param>
public sealed record UpdateWorkItemIterationPathCommand(int TfsId, string NewIterationPath) : ICommand<bool>;
