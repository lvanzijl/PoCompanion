using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to retrieve work item type definitions from TFS.
/// This returns the available work item types and their valid states.
/// </summary>
public sealed record GetWorkItemTypeDefinitionsQuery : IRequest<IEnumerable<WorkItemTypeDefinitionDto>>
{
    // No parameters needed - retrieves for the configured TFS project
}
