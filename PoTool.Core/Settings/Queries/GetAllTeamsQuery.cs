using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all teams.
/// </summary>
/// <param name="IncludeArchived">If true, includes archived teams. Default is false.</param>
public sealed record GetAllTeamsQuery(bool IncludeArchived = false) : IQuery<IEnumerable<TeamDto>>;
