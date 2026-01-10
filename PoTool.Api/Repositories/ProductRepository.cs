using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for product persistence.
/// </summary>
public class ProductRepository : IProductRepository
{
    private readonly PoToolDbContext _context;

    public ProductRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .Where(p => p.ProductOwnerId == productOwnerId)
            .OrderBy(p => p.Order)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ProductDto> CreateProductAsync(
        int? productOwnerId,
        string name,
        int backlogRootWorkItemId,
        ProductPictureType pictureType,
        int defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default)
    {
        // Get max order for this product owner (or 0 if no owner)
        var maxOrder = -1;
        if (productOwnerId.HasValue)
        {
            maxOrder = await _context.Products
                .Where(p => p.ProductOwnerId == productOwnerId.Value)
                .MaxAsync(p => (int?)p.Order, cancellationToken) ?? -1;
        }

        var entity = new ProductEntity
        {
            ProductOwnerId = productOwnerId,
            Name = name,
            BacklogRootWorkItemId = backlogRootWorkItemId,
            Order = maxOrder + 1,
            PictureType = (int)pictureType,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };

        _context.Products.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ProductDto> UpdateProductAsync(
        int id,
        string name,
        int backlogRootWorkItemId,
        ProductPictureType? pictureType,
        int? defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Product with ID {id} not found.");
        }

        entity.Name = name;
        entity.BacklogRootWorkItemId = backlogRootWorkItemId;

        if (pictureType.HasValue)
        {
            entity.PictureType = (int)pictureType.Value;
        }
        if (defaultPictureId.HasValue)
        {
            entity.DefaultPictureId = defaultPictureId.Value;
        }
        if (customPicturePath != null)
        {
            entity.CustomPicturePath = customPicturePath;
        }

        entity.LastModified = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        _context.Products.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> ReorderProductsAsync(int productOwnerId, List<int> productIds, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .Where(p => p.ProductOwnerId == productOwnerId && productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        for (int i = 0; i < productIds.Count; i++)
        {
            var entity = entities.FirstOrDefault(e => e.Id == productIds[i]);
            if (entity != null)
            {
                entity.Order = i;
                entity.LastModified = DateTimeOffset.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return entities.OrderBy(e => e.Order).Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default)
    {
        // Check if already linked
        var existingLink = await _context.ProductTeamLinks
            .FirstOrDefaultAsync(l => l.ProductId == productId && l.TeamId == teamId, cancellationToken);

        if (existingLink != null)
        {
            return true; // Already linked
        }

        var link = new ProductTeamLinkEntity
        {
            ProductId = productId,
            TeamId = teamId
        };

        _context.ProductTeamLinks.Add(link);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default)
    {
        var link = await _context.ProductTeamLinks
            .FirstOrDefaultAsync(l => l.ProductId == productId && l.TeamId == teamId, cancellationToken);

        if (link == null)
        {
            return false;
        }

        _context.ProductTeamLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found.");
        }

        entity.ProductOwnerId = newProductOwnerId;
        entity.LastModified = DateTimeOffset.UtcNow;

        // If assigning to a new owner, update order to be at the end
        if (newProductOwnerId.HasValue)
        {
            var maxOrder = await _context.Products
                .Where(p => p.ProductOwnerId == newProductOwnerId.Value)
                .MaxAsync(p => (int?)p.Order, cancellationToken) ?? -1;
            entity.Order = maxOrder + 1;
        }
        else
        {
            entity.Order = 0; // Orphans don't need ordering
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> GetOrphanProductsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .Where(p => p.ProductOwnerId == null)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Products
            .Include(p => p.ProductTeamLinks)
            .Where(p => p.ProductOwnerId == productOwnerId || p.ProductOwnerId == null)
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    private static ProductDto MapToDto(ProductEntity entity)
    {
        var teamIds = entity.ProductTeamLinks?.Select(l => l.TeamId).ToList() ?? new List<int>();

        return new ProductDto(
            entity.Id,
            entity.ProductOwnerId,
            entity.Name,
            entity.BacklogRootWorkItemId,
            entity.Order,
            (ProductPictureType)entity.PictureType,
            entity.DefaultPictureId,
            entity.CustomPicturePath,
            entity.CreatedAt,
            entity.LastModified,
            teamIds
        );
    }
}
