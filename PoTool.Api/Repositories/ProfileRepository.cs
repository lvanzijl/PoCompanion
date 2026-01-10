using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for profile persistence.
/// </summary>
public class ProfileRepository : IProfileRepository
{
    private readonly PoToolDbContext _context;

    public ProfileRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Profiles
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ProfileDto> CreateProfileAsync(
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        ProfilePictureType pictureType = ProfilePictureType.Default,
        int defaultPictureId = 0,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new ProfileEntity
        {
            Name = name,
            AreaPaths = string.Join(",", areaPaths),
            TeamName = teamName,
            GoalIds = string.Join(",", goalIds),
            PictureType = (int)pictureType,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };

        _context.Profiles.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        ProfilePictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"Profile with ID {id} not found.");
        }

        entity.Name = name;
        entity.AreaPaths = string.Join(",", areaPaths);
        entity.TeamName = teamName;
        entity.GoalIds = string.Join(",", goalIds);

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
    public async Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Profiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        _context.Profiles.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Profiles.AnyAsync(cancellationToken);
    }

    private static ProfileDto MapToDto(ProfileEntity entity)
    {
        var areaPaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(entity.AreaPaths))
        {
            areaPaths = entity.AreaPaths
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(ap => ap.Trim())
                .Where(ap => !string.IsNullOrWhiteSpace(ap))
                .ToList();
        }

        var goalIds = new List<int>();
        if (!string.IsNullOrWhiteSpace(entity.GoalIds))
        {
            foreach (var part in entity.GoalIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id) && id > 0)
                {
                    goalIds.Add(id);
                }
            }
        }

        return new ProfileDto(
            entity.Id,
            entity.Name,
            areaPaths,
            entity.TeamName,
            goalIds,
            (ProfilePictureType)entity.PictureType,
            entity.DefaultPictureId,
            entity.CustomPicturePath,
            entity.CreatedAt,
            entity.LastModified
        );
    }
}
