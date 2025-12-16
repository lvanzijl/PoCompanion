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
    // ProtectedPat is intentionally not returned to callers
}

/// <summary>
/// Service to persist TFS configuration securely using EF Core and IDataProtection.
/// </summary>
public class TfsConfigurationService
{
    private readonly PoToolDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<TfsConfigurationService> _logger;

    public TfsConfigurationService(
        PoToolDbContext db,
        IDataProtectionProvider provider,
        ILogger<TfsConfigurationService> logger)
    {
        _db = db;
        _protector = provider.CreateProtector("PoTool.TfsConfigProtector");
        _logger = logger;
    }

    public async Task<TfsConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _db.TfsConfigs.OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (entity == null) return null;

        return new TfsConfig
        {
            Url = entity.Url,
            Project = entity.Project
        };
    }

    public async Task SaveConfigAsync(string url, string project, string pat, CancellationToken cancellationToken = default)
    {
        var protectedPat = _protector.Protect(pat ?? string.Empty);

        var existing = await _db.TfsConfigs.FirstOrDefaultAsync(cancellationToken);
        if (existing == null)
        {
            existing = new TfsConfigEntity
            {
                Url = url ?? string.Empty,
                Project = project ?? string.Empty,
                ProtectedPat = protectedPat,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _db.TfsConfigs.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Url = url ?? string.Empty;
            existing.Project = project ?? string.Empty;
            existing.ProtectedPat = protectedPat;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            _db.TfsConfigs.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("TFS configuration saved/updated for Url={Url}", url);
    }

    public string? UnprotectPatEntity(TfsConfigEntity? entity)
    {
        if (entity == null || string.IsNullOrEmpty(entity.ProtectedPat))
            return null;

        try
        {
            return _protector.Unprotect(entity.ProtectedPat);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect PAT");
            return null;
        }
    }

    public async Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default)
    {
        return await _db.TfsConfigs.OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
    }
}
