using PoTool.Shared.Settings;

namespace PoTool.Core.Settings;

/// <summary>
/// Immutable DTO for user profile.
/// A profile represents a set of area paths, a team, and selected goals.
/// </summary>
public sealed record ProfileDto(
    int Id,
    string Name,
    List<string> AreaPaths,
    string TeamName,
    List<int> GoalIds,
    ProfilePictureType PictureType,
    int DefaultPictureId,
    string? CustomPicturePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModified
);
