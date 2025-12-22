using Mediator;
using PoTool.Core.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to create a new profile.
/// </summary>
public sealed record CreateProfileCommand(
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds
) : ICommand<ProfileDto>;
