using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update an existing profile.
/// </summary>
/// <param name="PictureType">Optional: Type of profile picture to update</param>
/// <param name="DefaultPictureId">Optional: ID of default picture (0-63). Only used when PictureType is provided.</param>
/// <param name="CustomPicturePath">Optional: Path to custom picture. Only used when PictureType is Custom.</param>
public sealed record UpdateProfileCommand(
    int Id,
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds,
    ProfilePictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
) : ICommand<ProfileDto>;
