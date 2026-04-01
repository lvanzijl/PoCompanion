using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for project persistence.
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private readonly PoToolDbContext _context;

    public ProjectRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _context.Projects
            .AsNoTracking()
            .Include(project => project.Products)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);

        return projects.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<ProjectDto?> GetProjectByAliasOrIdAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return null;
        }

        var normalizedRouteKey = aliasOrId.Trim();

        var project = await _context.Projects
            .AsNoTracking()
            .Include(entity => entity.Products)
            .OrderBy(entity => entity.Name)
            .FirstOrDefaultAsync(
                entity => entity.Alias == normalizedRouteKey || entity.Id == normalizedRouteKey,
                cancellationToken);

        return project == null ? null : MapToDto(project);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductDto>> GetProjectProductsAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return [];
        }

        var normalizedRouteKey = aliasOrId.Trim();

        var products = await _context.Products
            .AsNoTracking()
            .Include(product => product.Project)
            .Include(product => product.ProductTeamLinks)
            .Include(product => product.Repositories)
            .Include(product => product.BacklogRoots)
            .Where(product => product.Project != null &&
                              (product.Project.Alias == normalizedRouteKey || product.Project.Id == normalizedRouteKey))
            .OrderBy(product => product.Order)
            .ThenBy(product => product.Name)
            .ToListAsync(cancellationToken);

        return products.Select(ProductRepository.MapToDto);
    }

    private static ProjectDto MapToDto(ProjectEntity entity)
    {
        var productIds = entity.Products
            .OrderBy(product => product.Order)
            .ThenBy(product => product.Name)
            .Select(product => product.Id)
            .ToList();

        return new ProjectDto(entity.Id, entity.Alias, entity.Name, productIds);
    }
}
