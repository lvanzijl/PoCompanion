using Mediator;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Commands;

/// <summary>
/// Command to update the tags of a work item in TFS and refresh the local cache.
/// Used by Product Roadmap editor to add/remove the roadmap tag.
/// </summary>
/// <param name="TfsId">TFS work item ID whose tags should be updated.</param>
/// <param name="Tags">The complete list of tags to set on the work item.</param>
public sealed record UpdateWorkItemTagsCommand(int TfsId, List<string> Tags) : ICommand<WorkItemDto?>;
