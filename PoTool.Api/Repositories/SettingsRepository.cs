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
    public async Task<SettingsDto> SaveSettingsAsync(
        DataMode dataMode,
        List<int> configuredGoalIds,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Settings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            entity = new SettingsEntity
            {
                DataMode = dataMode,
                ConfiguredGoalIds = string.Join(",", configuredGoalIds),
                ActiveProfileId = null, // Will be set when first profile is created
                LastModified = DateTimeOffset.UtcNow
            };
            _context.Settings.Add(entity);
        }
        else
        {
            entity.DataMode = dataMode;
            entity.ConfiguredGoalIds = string.Join(",", configuredGoalIds);
            entity.LastModified = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

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
                DataMode = DataMode.Mock,
                ConfiguredGoalIds = string.Empty,
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
        var goalIds = new List<int>();
        
        if (!string.IsNullOrWhiteSpace(entity.ConfiguredGoalIds))
        {
            foreach (var part in entity.ConfiguredGoalIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id) && id > 0)
                {
                    goalIds.Add(id);
                }
            }
        }

        return new SettingsDto(
            entity.Id,
            entity.DataMode,
            goalIds,
            entity.ActiveProfileId,
            entity.LastModified
        );
    }
}
