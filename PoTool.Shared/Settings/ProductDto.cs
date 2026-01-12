namespace PoTool.Shared.Settings;

/// <summary>
/// Enumeration for product picture types.
/// </summary>
public enum ProductPictureType
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
/// Immutable DTO for a Product.
/// A Product represents a single Scrum product with a product backlog.
/// The backlog is defined by a root work item.
/// </summary>
public sealed record ProductDto(
    int Id,
    int? ProductOwnerId,
    string Name,
    int BacklogRootWorkItemId,
    int Order,
    ProductPictureType PictureType,
    int DefaultPictureId,
    string? CustomPicturePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModified,
    DateTimeOffset? LastSyncedAt,
    List<int> TeamIds
);
