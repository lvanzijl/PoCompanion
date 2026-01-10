using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update an existing team.
/// </summary>
/// <param name="Id">Team ID</param>
/// <param name="Name">Team name</param>
/// <param name="TeamAreaPath">Area path that defines the team's work area</param>
/// <param name="PictureType">Optional: Type of team picture to update</param>
/// <param name="DefaultPictureId">Optional: ID of default picture (0-63)</param>
/// <param name="CustomPicturePath">Optional: Path to custom picture</param>
public sealed record UpdateTeamCommand(
    int Id,
    string Name,
    string TeamAreaPath,
    TeamPictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
) : ICommand<TeamDto>;
