using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Queries;

/// <summary>
/// Query to retrieve all Epics for a specific Objective.
/// Used in the Objective modal dialog to show which Epics are planned/unplanned.
/// </summary>
/// <param name="ObjectiveId">The TFS ID of the Objective.</param>
public sealed record GetObjectiveEpicsQuery(int ObjectiveId) : IQuery<IReadOnlyList<ObjectiveEpicDto>>;
