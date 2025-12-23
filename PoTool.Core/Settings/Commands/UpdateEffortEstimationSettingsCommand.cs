using Mediator;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update effort estimation settings.
/// </summary>
public sealed record UpdateEffortEstimationSettingsCommand(
    EffortEstimationSettingsDto Settings
) : ICommand;
