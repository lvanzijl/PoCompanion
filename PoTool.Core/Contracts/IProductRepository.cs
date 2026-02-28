using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for product persistence.
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// Gets all products for a Product Owner.
    /// </summary>
    Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new product.
    /// </summary>
    Task<ProductDto> CreateProductAsync(
        int? productOwnerId,
        string name,
        List<int> backlogRootWorkItemIds,
        ProductPictureType pictureType,
        int defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    Task<ProductDto> UpdateProductAsync(
        int id,
        string name,
        List<int> backlogRootWorkItemIds,
        ProductPictureType? pictureType,
        int? defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the Product Owner for a product.
    /// </summary>
    /// <param name="productId">ID of the product</param>
    /// <param name="newProductOwnerId">New owner ID, or null to make orphan</param>
    Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products in the system.
    /// </summary>
    Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all orphaned products (products with no owner).
    /// </summary>
    Task<IEnumerable<ProductDto>> GetOrphanProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets products selectable by a specific Product Owner (owned + orphaned).
    /// </summary>
    Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a product by ID.
    /// </summary>
    Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders products for a Product Owner.
    /// </summary>
    Task<IEnumerable<ProductDto>> ReorderProductsAsync(int productOwnerId, List<int> productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a team to a product.
    /// </summary>
    Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks a team from a product.
    /// </summary>
    Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default);
}
