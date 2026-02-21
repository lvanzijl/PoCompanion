using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

public sealed class TfsConfig
{
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string DefaultAreaPath { get; set; } = string.Empty;
    public bool UseDefaultCredentials { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
    public string AnalyticsODataBaseUrl { get; set; } = string.Empty;
    public string AnalyticsODataEntitySetPath { get; set; } = "WorkItemRevisions";
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
    private const string DefaultAnalyticsEntitySetPath = "WorkItemRevisions";
    private const string DefaultAnalyticsODataVersionPath = "_odata/v3.0-preview";

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
            // Use ToListAsync then LINQ to Objects for DateTimeOffset ordering (SQLite compatibility)
            // Note: TfsConfig typically contains only one record (updated in place), so loading all is acceptable
            var entities = await _db.TfsConfigs.ToListAsync(cancellationToken);
            var entity = entities.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
            if (entity == null) return null;

            return new TfsConfig
            {
                Url = entity.Url,
                Project = entity.Project,
                DefaultAreaPath = entity.DefaultAreaPath,
                UseDefaultCredentials = entity.UseDefaultCredentials,
                TimeoutSeconds = entity.TimeoutSeconds,
                ApiVersion = entity.ApiVersion,
                AnalyticsODataBaseUrl = entity.AnalyticsODataBaseUrl,
                AnalyticsODataEntitySetPath = entity.AnalyticsODataEntitySetPath,
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
            // Use ToListAsync then LINQ to Objects for DateTimeOffset ordering (SQLite compatibility)
            var entities = await _db.TfsConfigs.ToListAsync(cancellationToken);
            var existing = entities.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();

            // Derive DefaultAreaPath from Project name (canonical root area path)
            var derivedDefaultAreaPath = project ?? string.Empty;
            var resolvedAnalyticsODataBaseUrl = ResolveAnalyticsODataBaseUrl(url, project, analyticsODataBaseUrl);
            var resolvedAnalyticsODataEntitySetPath = ResolveAnalyticsODataEntitySetPath(analyticsODataEntitySetPath);

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
                    AnalyticsODataBaseUrl = resolvedAnalyticsODataBaseUrl,
                    AnalyticsODataEntitySetPath = resolvedAnalyticsODataEntitySetPath,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
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
                existing.AnalyticsODataBaseUrl = resolvedAnalyticsODataBaseUrl;
                existing.AnalyticsODataEntitySetPath = resolvedAnalyticsODataEntitySetPath;

                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _db.TfsConfigs.Update(existing);
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("TFS configuration saved/updated for Url={Url}, Project={Project}, DefaultAreaPath={DefaultAreaPath} (derived from project), using NTLM authentication", 
                url, project, derivedDefaultAreaPath);
        }, cancellationToken);
    }

    private static string ResolveAnalyticsODataBaseUrl(string? url, string? project, string? analyticsODataBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(analyticsODataBaseUrl))
        {
            return analyticsODataBaseUrl.Trim();
        }

        var trimmedUrl = (url ?? string.Empty).Trim().TrimEnd('/');
        var trimmedProject = (project ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl) || string.IsNullOrWhiteSpace(trimmedProject))
        {
            return string.Empty;
        }

        return $"{trimmedUrl}/{Uri.EscapeDataString(trimmedProject)}/{DefaultAnalyticsODataVersionPath}";
    }

    private static string ResolveAnalyticsODataEntitySetPath(string? analyticsODataEntitySetPath)
        => string.IsNullOrWhiteSpace(analyticsODataEntitySetPath)
            ? DefaultAnalyticsEntitySetPath
            : analyticsODataEntitySetPath.Trim();

    public async Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default)
    {
        return await _efGate.ExecuteAsync(async () =>
        {
            // Use ToListAsync then LINQ to Objects for DateTimeOffset ordering (SQLite compatibility)
            // Note: TfsConfig typically contains only one record (updated in place), so loading all is acceptable
            var entities = await _db.TfsConfigs.ToListAsync(cancellationToken);
            return entities.OrderByDescending(c => c.UpdatedAt).FirstOrDefault();
        }, cancellationToken);
    }

    public async Task SaveConfigEntityAsync(TfsConfigEntity entity, CancellationToken cancellationToken = default)
    {
        await _efGate.ExecuteAsync(async () =>
        {
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            _db.TfsConfigs.Update(entity);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("TFS configuration entity updated");
        }, cancellationToken);
    }
}
