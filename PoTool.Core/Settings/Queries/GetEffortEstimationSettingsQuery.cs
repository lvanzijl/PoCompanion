using Mediator;

using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get effort estimation settings.
/// </summary>
public sealed record GetEffortEstimationSettingsQuery : IQuery<EffortEstimationSettingsDto>;
