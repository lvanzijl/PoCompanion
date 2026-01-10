using PoTool.Client.ApiClient;
using ProductPictureType = PoTool.Shared.Settings.ProductPictureType;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing products via the API.
/// </summary>
public class ProductService
{
    private readonly IProductsClient _productsClient;

    public ProductService(IProductsClient productsClient)
    {
        _productsClient = productsClient;
    }

    /// <summary>
    /// Gets all products for a Product Owner.
    /// </summary>
    public async Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        return await _productsClient.GetProductsByOwnerAsync(productOwnerId, cancellationToken);
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _productsClient.GetProductByIdAsync(id, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    public async Task<ProductDto> CreateProductAsync(
        int productOwnerId,
        string name,
        int backlogRootWorkItemId,
        ProductPictureType pictureType = ProductPictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        // If no picture ID is specified and using default type, randomize it
        var pictureId = defaultPictureId ?? Random.Shared.Next(0, 64);

        var request = new CreateProductRequest
        {
            ProductOwnerId = productOwnerId,
            Name = name,
            BacklogRootWorkItemId = backlogRootWorkItemId,
            PictureType = (ApiClient.ProductPictureType)pictureType,
            DefaultPictureId = pictureId,
            CustomPicturePath = customPicturePath
        };

        return await _productsClient.CreateProductAsync(request, cancellationToken);
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    public async Task<ProductDto> UpdateProductAsync(
        int id,
        string name,
        int backlogRootWorkItemId,
        ProductPictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProductRequest
        {
            Name = name,
            BacklogRootWorkItemId = backlogRootWorkItemId,
            PictureType = pictureType.HasValue ? (ApiClient.ProductPictureType?)pictureType.Value : null,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath
        };

        return await _productsClient.UpdateProductAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _productsClient.DeleteProductAsync(id, cancellationToken);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return false;
        }
    }

    /// <summary>
    /// Reorders products for a Product Owner.
    /// </summary>
    public async Task<IEnumerable<ProductDto>> ReorderProductsAsync(
        int productOwnerId,
        List<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var request = new ReorderProductsRequest
        {
            ProductOwnerId = productOwnerId,
            ProductIds = productIds
        };

        return await _productsClient.ReorderProductsAsync(request, cancellationToken);
    }

    /// <summary>
    /// Links a team to a product.
    /// </summary>
    public async Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _productsClient.LinkTeamToProductAsync(productId, teamId, cancellationToken);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return false;
        }
    }

    /// <summary>
    /// Unlinks a team from a product.
    /// </summary>
    public async Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _productsClient.UnlinkTeamFromProductAsync(productId, teamId, cancellationToken);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return false;
        }
    }
}
