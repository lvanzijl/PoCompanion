using Mediator;

namespace PoTool.Core.WorkItems.Queries;

/// <summary>
/// Query to retrieve area paths directly from TFS.
/// Used when cache is not yet populated (e.g., Add Profile flow).
/// </summary>
public sealed record GetAreaPathsFromTfsQuery : IQuery<IEnumerable<string>>;
