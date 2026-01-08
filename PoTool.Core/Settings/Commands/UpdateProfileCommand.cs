using Mediator;
using PoTool.Core.Settings;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update an existing profile.
/// </summary>
public sealed record UpdateProfileCommand(
    int Id,
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds,
    ProfilePictureType PictureType = ProfilePictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
) : ICommand<ProfileDto>;
