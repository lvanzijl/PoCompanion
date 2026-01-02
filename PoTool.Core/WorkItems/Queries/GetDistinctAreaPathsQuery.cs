using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve all distinct area paths from cached work items.
/// </summary>
public sealed record GetDistinctAreaPathsQuery : IQuery<IEnumerable<string>>;
