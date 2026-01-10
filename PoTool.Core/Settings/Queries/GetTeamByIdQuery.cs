using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get a team by ID.
/// </summary>
public sealed record GetTeamByIdQuery(int Id) : IQuery<TeamDto?>;
