using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get a profile by ID.
/// </summary>
public sealed record GetProfileByIdQuery(int Id) : IQuery<ProfileDto?>;
