using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

public sealed class TfsConfig
{
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    // NOTE: PAT is not included in server-side config
    // PAT is stored client-side using MAUI SecureStorage
    // See docs/PAT_STORAGE_BEST_PRACTICES.md
}

/// <summary>
/// Service to persist TFS configuration (non-sensitive data only).
/// PAT is stored client-side using MAUI SecureStorage, not on the server.
/// See docs/PAT_STORAGE_BEST_PRACTICES.md for details.
/// </summary>
public class TfsConfigurationService
{
    private readonly PoToolDbContext _db;
    private readonly ILogger<TfsConfigurationService> _logger;

    public TfsConfigurationService(
        PoToolDbContext db,
        ILogger<TfsConfigurationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TfsConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        // Use ToListAsync then LINQ to Objects for DateTimeOffset ordering (SQLite compatibility)
        // Note: TfsConfig typically contains only one record (updated in place), so loading all is acceptable
        var entities = await _db.TfsConfigs.ToListAsync(cancellationToken);
        var entity = entities.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
        if (entity == null) return null;

        return new TfsConfig
        {
            Url = entity.Url,
            Project = entity.Project
        };
    }

    /// <summary>
    /// Saves non-sensitive TFS configuration (URL, Project).
    /// PAT is NOT stored by this service - it should be stored client-side.
    /// </summary>
    public async Task SaveConfigAsync(string url, string project, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TfsConfigs.FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            existing = new TfsConfigEntity
            {
                Url = url ?? string.Empty,
                Project = project ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _db.TfsConfigs.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Url = url ?? string.Empty;
            existing.Project = project ?? string.Empty;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            _db.TfsConfigs.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("TFS configuration saved/updated for Url={Url}", url);
    }

    public async Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default)
    {
        // Use ToListAsync then LINQ to Objects for DateTimeOffset ordering (SQLite compatibility)
        // Note: TfsConfig typically contains only one record (updated in place), so loading all is acceptable
        var entities = await _db.TfsConfigs.ToListAsync(cancellationToken);
        return entities.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
    }

    public async Task SaveConfigEntityAsync(TfsConfigEntity entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _db.TfsConfigs.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("TFS configuration entity updated");
    }
}
