using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve a specific work item by TFS ID.
/// </summary>
public sealed record GetWorkItemByIdQuery(int TfsId) : IQuery<WorkItemDto?>;
