using Mediator;

using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve goals directly from TFS.
/// Used when cache is not yet populated (e.g., Add Profile flow).
/// </summary>
public sealed record GetGoalsFromTfsQuery : IQuery<IEnumerable<WorkItemDto>>;
