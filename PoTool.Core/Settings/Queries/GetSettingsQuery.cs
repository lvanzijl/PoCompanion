using Mediator;

using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to retrieve application settings.
/// </summary>
public sealed record GetSettingsQuery : IQuery<SettingsDto?>;
