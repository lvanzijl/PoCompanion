using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve all area paths from TFS/Azure DevOps server.
/// Unlike GetDistinctAreaPathsQuery which returns cached area paths from work items,
/// this query directly fetches the area path hierarchy from the TFS server.
/// </summary>
public sealed record GetAreaPathsFromTfsQuery : IQuery<IEnumerable<string>>;
