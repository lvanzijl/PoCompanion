using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for repository configuration persistence.
/// </summary>
public class RepositoryRepository : IRepositoryConfigRepository
{
    private readonly PoToolDbContext _context;

    public RepositoryRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all repositories configured for a specific product.
    /// </summary>
    public async Task<IEnumerable<RepositoryDto>> GetRepositoriesByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Repositories
            .Where(r => r.ProductId == productId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <summary>
    /// Gets all repositories in the system.
    /// </summary>
    public async Task<IEnumerable<RepositoryDto>> GetAllRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Repositories
            .OrderBy(r => r.ProductId)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <summary>
    /// Gets repositories for multiple products in a single query.
    /// </summary>
    public async Task<Dictionary<int, List<RepositoryDto>>> GetRepositoriesByProductIdsAsync(List<int> productIds, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Repositories
            .Where(r => productIds.Contains(r.ProductId))
            .OrderBy(r => r.ProductId)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return entities
            .GroupBy(r => r.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(MapToDto).ToList()
            );
    }

    /// <summary>
    /// Creates a new repository configuration.
    /// </summary>
    public async Task<RepositoryDto> CreateRepositoryAsync(int productId, string name, CancellationToken cancellationToken = default)
    {
        // Check if repository with same name already exists for this product
        var exists = await _context.Repositories
            .AnyAsync(r => r.ProductId == productId && r.Name == name, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Repository '{name}' already exists for product ID {productId}.");
        }

        var entity = new RepositoryEntity
        {
            ProductId = productId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Repositories.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <summary>
    /// Deletes a repository configuration.
    /// </summary>
    public async Task DeleteRepositoryAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Repositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Repository with ID {repositoryId} not found.");
        }

        _context.Repositories.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static RepositoryDto MapToDto(RepositoryEntity entity)
    {
        return new RepositoryDto(
            entity.Id,
            entity.ProductId,
            entity.Name,
            entity.CreatedAt
        );
    }
}
