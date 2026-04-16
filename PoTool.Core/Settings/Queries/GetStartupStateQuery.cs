using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to resolve the authoritative startup-state contract for the current client route.
/// </summary>
public sealed record GetStartupStateQuery(string? ReturnUrl, int? ProfileHintId) : IQuery<StartupStateResponseDto>;
