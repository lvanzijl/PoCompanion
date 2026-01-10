using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to create a new team.
/// </summary>
/// <param name="Name">Team name</param>
/// <param name="TeamAreaPath">Area path that defines the team's work area</param>
/// <param name="PictureType">Type of team picture (Default or Custom)</param>
/// <param name="DefaultPictureId">ID of default picture (0-63)</param>
/// <param name="CustomPicturePath">Path to custom picture when PictureType is Custom</param>
public sealed record CreateTeamCommand(
    string Name,
    string TeamAreaPath,
    TeamPictureType PictureType = TeamPictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
) : ICommand<TeamDto>;
