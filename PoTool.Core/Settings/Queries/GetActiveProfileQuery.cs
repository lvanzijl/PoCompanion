using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get the active profile.
/// Returns null if no profile is set as active.
/// </summary>
public sealed record GetActiveProfileQuery : IQuery<ProfileDto?>;
