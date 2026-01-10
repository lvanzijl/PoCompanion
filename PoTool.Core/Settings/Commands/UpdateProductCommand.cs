using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to update an existing product.
/// </summary>
/// <param name="Id">Product ID</param>
/// <param name="Name">Product name</param>
/// <param name="ProductAreaPath">Area path that defines the backlog scope</param>
/// <param name="BacklogRootWorkItemId">Optional root work item ID for the backlog</param>
/// <param name="PictureType">Optional: Type of product picture to update</param>
/// <param name="DefaultPictureId">Optional: ID of default picture (0-63)</param>
/// <param name="CustomPicturePath">Optional: Path to custom picture</param>
public sealed record UpdateProductCommand(
    int Id,
    string Name,
    string ProductAreaPath,
    int? BacklogRootWorkItemId = null,
    ProductPictureType? PictureType = null,
    int? DefaultPictureId = null,
    string? CustomPicturePath = null
) : ICommand<ProductDto>;
