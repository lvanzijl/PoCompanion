using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to set the active profile.
/// </summary>
public sealed record SetActiveProfileCommand(int? ProfileId) : ICommand<SettingsDto>;
