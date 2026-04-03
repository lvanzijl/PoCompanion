using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all sprints for a specific team.
/// </summary>
public sealed record GetSprintsForTeamQuery(int TeamId) : IQuery<IEnumerable<SprintDto>>;

/// <summary>
/// Query to get the current sprint for a specific team.
/// Returns only an explicitly current or currently overlapping sprint for the supplied team.
/// </summary>
public sealed record GetCurrentSprintForTeamQuery(int TeamId) : IQuery<SprintDto?>;
