namespace PoTool.Core.Settings;

/// <summary>
/// Enumeration for profile picture types.
/// </summary>
public enum ProfilePictureType
{
    /// <summary>
    /// Uses a default maritime-themed picture (0-63).
    /// </summary>
    Default = 0,
    
    /// <summary>
    /// Uses a custom user-provided picture.
    /// </summary>
    Custom = 1
}

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
