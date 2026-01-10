namespace PoTool.Shared.Settings;

/// <summary>
/// Enumeration for team picture types.
/// </summary>
public enum TeamPictureType
{
    /// <summary>
    /// Uses a default picture (0-63).
    /// </summary>
    Default = 0,

    /// <summary>
    /// Uses a custom user-provided picture.
    /// </summary>
    Custom = 1
}

/// <summary>
/// Immutable DTO for a Team.
/// A Team represents a delivery team that can work on multiple products.
/// </summary>
public sealed record TeamDto(
    int Id,
    string Name,
    string TeamAreaPath,
    bool IsArchived,
    TeamPictureType PictureType,
    int DefaultPictureId,
    string? CustomPicturePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModified
);
