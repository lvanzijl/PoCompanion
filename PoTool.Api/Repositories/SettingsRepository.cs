using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for settings persistence.
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private readonly PoToolDbContext _context;

    public SettingsRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _context.Settings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Settings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            // Create default settings if none exist
            entity = new SettingsEntity
            {
                ActiveProfileId = profileId,
                LastModified = DateTimeOffset.UtcNow
            };
            _context.Settings.Add(entity);
        }
        else
        {
            entity.ActiveProfileId = profileId;
            entity.LastModified = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    private static SettingsDto MapToDto(SettingsEntity entity)
    {
        return new SettingsDto(
            entity.Id,
            entity.ActiveProfileId,
            entity.LastModified
        );
    }
}
