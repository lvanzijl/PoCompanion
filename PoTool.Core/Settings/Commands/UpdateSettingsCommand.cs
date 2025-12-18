using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update application settings.
/// </summary>
public sealed record UpdateSettingsCommand(
    DataMode DataMode,
    List<int> ConfiguredGoalIds
) : ICommand<SettingsDto>;
