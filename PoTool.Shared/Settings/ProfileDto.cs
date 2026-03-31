namespace PoTool.Shared.Settings;

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
/// Immutable DTO for user profile (Product Owner).
/// A profile represents a Product Owner who manages products and teams.
/// </summary>
public sealed record ProfileDto(
    int Id,
    string Name,
    List<int> GoalIds,
    ProfilePictureType PictureType,
    int DefaultPictureId,
    string? CustomPicturePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModified
)
{
    public ProfileDto()
        : this(0, string.Empty, new List<int>(), ProfilePictureType.Default, 0, null, default, default)
    {
    }
}
