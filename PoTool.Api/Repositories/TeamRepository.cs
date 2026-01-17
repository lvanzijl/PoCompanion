using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for team persistence.
/// </summary>
public class TeamRepository : ITeamRepository
{
    private readonly PoToolDbContext _context;

    public TeamRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TeamDto>> GetAllTeamsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Teams.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(t => !t.IsArchived);
        }

        var entities = await query
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<TeamDto?> GetTeamByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<TeamDto> CreateTeamAsync(
        string name,
        string teamAreaPath,
        TeamPictureType pictureType,
        int defaultPictureId,
        string? customPicturePath,
        string? projectName = null,
        string? tfsTeamId = null,
        string? tfsTeamName = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new TeamEntity
        {
            Name = name,
            TeamAreaPath = teamAreaPath,
            IsArchived = false,
            PictureType = (int)pictureType,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath,
            ProjectName = projectName,
            TfsTeamId = tfsTeamId,
            TfsTeamName = tfsTeamName,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };

        _context.Teams.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<TeamDto> UpdateTeamAsync(
        int id,
        string name,
        string teamAreaPath,
        TeamPictureType? pictureType,
        int? defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Team with ID {id} not found.");
        }

        entity.Name = name;
        entity.TeamAreaPath = teamAreaPath;

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
    public async Task<TeamDto> ArchiveTeamAsync(int id, bool isArchived, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Team with ID {id} not found.");
        }

        entity.IsArchived = isArchived;
        entity.LastModified = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTeamAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        // Remove all product-team links first
        var productLinks = await _context.ProductTeamLinks
            .Where(ptl => ptl.TeamId == id)
            .ToListAsync(cancellationToken);
        
        _context.ProductTeamLinks.RemoveRange(productLinks);

        // Then remove the team entity
        _context.Teams.Remove(entity);
        
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static TeamDto MapToDto(TeamEntity entity)
    {
        return new TeamDto(
            entity.Id,
            entity.Name,
            entity.TeamAreaPath,
            entity.IsArchived,
            (TeamPictureType)entity.PictureType,
            entity.DefaultPictureId,
            entity.CustomPicturePath,
            entity.CreatedAt,
            entity.LastModified,
            entity.ProjectName,
            entity.TfsTeamId,
            entity.TfsTeamName,
            entity.LastSyncedIterationsUtc
        );
    }
}
