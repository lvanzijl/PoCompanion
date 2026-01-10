using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to create a new profile (Product Owner).
/// </summary>
/// <param name="PictureType">Type of profile picture (Default or Custom)</param>
/// <param name="DefaultPictureId">ID of default picture (0-63). Randomized in service layer if not specified.</param>
/// <param name="CustomPicturePath">Path to custom picture when PictureType is Custom</param>
public sealed record CreateProfileCommand(
    string Name,
    List<int> GoalIds,
    ProfilePictureType PictureType = ProfilePictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null
) : ICommand<ProfileDto>;
