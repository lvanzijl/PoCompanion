using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Commands;

/// <summary>
/// Command to create a new product.
/// </summary>
/// <param name="ProductOwnerId">ID of the Product Owner (Profile) who owns this product. Null for orphaned products.</param>
/// <param name="Name">Product name</param>
/// <param name="BacklogRootWorkItemIds">Root work item IDs that define the backlog</param>
/// <param name="PictureType">Type of product picture (Default or Custom)</param>
/// <param name="DefaultPictureId">ID of default picture (0-63)</param>
/// <param name="CustomPicturePath">Path to custom picture when PictureType is Custom</param>
public sealed record CreateProductCommand(
    int? ProductOwnerId,
    string Name,
    List<int> BacklogRootWorkItemIds,
    ProductPictureType PictureType = ProductPictureType.Default,
    int DefaultPictureId = 0,
    string? CustomPicturePath = null,
    EstimationMode EstimationMode = EstimationMode.StoryPoints
) : ICommand<ProductDto>;
