using Mediator;
using PoTool.Core.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all profiles.
/// </summary>
public sealed record GetAllProfilesQuery : IQuery<IEnumerable<ProfileDto>>;
