using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Queries;

/// <summary>
/// Query to retrieve all Epics that are not yet placed on the Release Planning Board.
/// </summary>
public sealed record GetUnplannedEpicsQuery : IQuery<IReadOnlyList<UnplannedEpicDto>>;
