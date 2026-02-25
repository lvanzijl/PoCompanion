using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

public sealed class TfsConfig
{
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string DefaultAreaPath { get; set; } = string.Empty;
    public bool UseDefaultCredentials { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
    public DateTimeOffset? LastValidated { get; set; }
}

/// <summary>
/// Service to persist TFS configuration.
/// Authentication uses Windows credentials (NTLM) - no PAT needed.
/// 
/// All EF operations are protected by IEfConcurrencyGate to prevent concurrent DbContext access
/// when this service is called from parallel code paths (e.g., Task.WhenAll in RealTfsClient).
/// </summary>
public class TfsConfigurationService : ITfsConfigurationService
{
    private readonly PoToolDbContext _db;
    private readonly ILogger<TfsConfigurationService> _logger;
    private readonly IEfConcurrencyGate _efGate;

    public TfsConfigurationService(
        PoToolDbContext db,
        ILogger<TfsConfigurationService> logger,
        IEfConcurrencyGate efGate)
    {
        _db = db;
        _logger = logger;
        _efGate = efGate;
    }

    public async Task<TfsConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return await _efGate.ExecuteAsync(async () =>
        {
            var entity = await _db.TfsConfigs
                .OrderByDescending(c => c.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null) return null;

            return new TfsConfig
            {
                Url = entity.Url,
                Project = entity.Project,
                DefaultAreaPath = entity.DefaultAreaPath,
                UseDefaultCredentials = entity.UseDefaultCredentials,
                TimeoutSeconds = entity.TimeoutSeconds,
                ApiVersion = entity.ApiVersion,
                LastValidated = entity.LastValidated
            };
        }, cancellationToken);
    }

    /// <summary>
    /// Saves TFS configuration.
    /// Authentication uses Windows credentials (NTLM) - always enabled.
    /// DefaultAreaPath is derived from Project name (canonical root area path).
    /// </summary>
    public async Task SaveConfigAsync(
        string url,
        string project,
        string defaultAreaPath,
        bool useDefaultCredentials = true,
        int timeoutSeconds = 30,
        string apiVersion = "7.0",
        string? analyticsODataBaseUrl = null,
        string? analyticsODataEntitySetPath = null,
        CancellationToken cancellationToken = default)
    {
        await _efGate.ExecuteAsync(async () =>
        {
            var existing = await _db.TfsConfigs
                .OrderByDescending(c => c.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            // Derive DefaultAreaPath from Project name (canonical root area path)
            var derivedDefaultAreaPath = project ?? string.Empty;

            if (existing == null)
            {
                existing = new TfsConfigEntity
                {
                    Url = url ?? string.Empty,
                    Project = project ?? string.Empty,
                    DefaultAreaPath = derivedDefaultAreaPath,
                    UseDefaultCredentials = useDefaultCredentials,
                    TimeoutSeconds = timeoutSeconds,
                    ApiVersion = apiVersion ?? "7.0",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _db.TfsConfigs.AddAsync(existing, cancellationToken);
            }
            else
            {
                existing.Url = url ?? string.Empty;
                existing.Project = project ?? string.Empty;
                existing.DefaultAreaPath = derivedDefaultAreaPath;
                existing.UseDefaultCredentials = useDefaultCredentials;
                existing.TimeoutSeconds = timeoutSeconds;
                existing.ApiVersion = apiVersion ?? "7.0";

                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                _db.TfsConfigs.Update(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("TFS configuration saved/updated for Url={Url}, Project={Project}, DefaultAreaPath={DefaultAreaPath} (derived from project), using NTLM authentication", 
                url, project, derivedDefaultAreaPath);
        }, cancellationToken);
    }

    public async Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default)
    {
        return await _efGate.ExecuteAsync(async () =>
        {
            return await _db.TfsConfigs
                .OrderByDescending(c => c.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task SaveConfigEntityAsync(TfsConfigEntity entity, CancellationToken cancellationToken = default)
    {
        await _efGate.ExecuteAsync(async () =>
        {
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            _db.TfsConfigs.Update(entity);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("TFS configuration entity updated");
        }, cancellationToken);
    }
}
