using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

/// <summary>
/// Cached pipeline read provider that reads from the local database.
/// Used when DataSourceMode is Cache (after sync).
/// </summary>
public sealed class CachedPipelineReadProvider : IPipelineReadProvider
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CachedPipelineReadProvider> _logger;

    public CachedPipelineReadProvider(
        PoToolDbContext dbContext,
        ILogger<CachedPipelineReadProvider> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all pipelines from the cache (via pipeline definitions).
    /// </summary>
    public async Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching all pipelines from cache");

        var definitions = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return definitions.Select(d => new PipelineDto(
            Id: d.PipelineDefinitionId,
            Name: d.Name,
            Type: PipelineType.Build, // Cached pipelines are build pipelines
            Path: d.Folder,
            RetrievedAt: d.LastSyncedUtc
        ));
    }

    /// <summary>
    /// Retrieves a specific pipeline by ID from the cache.
    /// </summary>
    public async Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching pipeline by ID: {PipelineId}", pipelineId);

        var definition = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .OrderBy(d => d.PipelineDefinitionId)
            .FirstOrDefaultAsync(d => d.PipelineDefinitionId == pipelineId, cancellationToken);

        if (definition == null)
        {
            return null;
        }

        return new PipelineDto(
            Id: definition.PipelineDefinitionId,
            Name: definition.Name,
            Type: PipelineType.Build,
            Path: definition.Folder,
            RetrievedAt: definition.LastSyncedUtc
        );
    }

    public async Task<IEnumerable<PipelineDto>> GetByIdsAsync(IEnumerable<int> pipelineIds, CancellationToken cancellationToken = default)
    {
        var normalizedIds = pipelineIds
            .Distinct()
            .ToArray();

        if (normalizedIds.Length == 0)
        {
            return [];
        }

        var definitions = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .Where(d => normalizedIds.Contains(d.PipelineDefinitionId))
            .ToListAsync(cancellationToken);

        return definitions
            .Select(d => new PipelineDto(
                Id: d.PipelineDefinitionId,
                Name: d.Name,
                Type: PipelineType.Build,
                Path: d.Folder,
                RetrievedAt: d.LastSyncedUtc))
            .ToList();
    }

    /// <summary>
    /// Retrieves pipeline runs for a specific pipeline from the cache.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching runs for pipeline {PipelineId}", pipelineId);

        // Get the internal definition ID first
        var definition = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .OrderBy(d => d.PipelineDefinitionId)
            .FirstOrDefaultAsync(d => d.PipelineDefinitionId == pipelineId, cancellationToken);

        if (definition == null)
        {
            _logger.LogWarning("Pipeline definition {PipelineId} not found in cache", pipelineId);
            return Enumerable.Empty<PipelineRunDto>();
        }

        var runs = await _dbContext.CachedPipelineRuns
            .AsNoTracking()
            .Where(r => r.PipelineDefinitionId == definition.Id)
            .OrderByDescending(r => r.CreatedDateUtc)
            .Take(top)
            .ToListAsync(cancellationToken);

        return runs.Select(r => MapRunToDto(r, definition.Name));
    }

    /// <summary>
    /// Retrieves all pipeline runs from the cache.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching all pipeline runs from cache");

        var runs = await _dbContext.CachedPipelineRuns
            .AsNoTracking()
            .Include(r => r.PipelineDefinition)
            .OrderByDescending(r => r.CreatedDateUtc)
            .ToListAsync(cancellationToken);

        return runs.Select(r => MapRunToDto(r, r.PipelineDefinition.Name));
    }

    /// <summary>
    /// Retrieves pipeline runs for multiple pipelines with optional filtering.
    /// </summary>
    public async Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minFinishTime = null,
        DateTimeOffset? maxFinishTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "CachedPipelineReadProvider: Fetching runs for {Count} pipelines with filters",
            pipelineIds.Count());

        var pipelineIdList = pipelineIds.ToList();

        // Get definition IDs for the TFS pipeline IDs
        var definitions = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .Where(d => pipelineIdList.Contains(d.PipelineDefinitionId))
            .ToDictionaryAsync(d => d.Id, d => d, cancellationToken);

        if (definitions.Count == 0)
        {
            return Enumerable.Empty<PipelineRunDto>();
        }

        var minFinishTimeUtc = minFinishTime?.UtcDateTime;
        var maxFinishTimeUtc = maxFinishTime?.UtcDateTime;
        var branchScopeByPipelineId = branchScope?
            .GroupBy(scope => scope.PipelineId)
            .ToDictionary(group => group.Key, group => group.Last().DefaultBranch);
        var runs = new List<PipelineRunDto>();

        _logger.LogDebug(
            "CachedPipelineReadProvider: Selecting up to {Top} cached runs per pipeline for {DefinitionCount} pipeline definitions before combining results",
            top,
            definitions.Count);

        foreach (var (definitionId, definition) in definitions.OrderBy(entry => entry.Value.PipelineDefinitionId))
        {
            var query = _dbContext.CachedPipelineRuns
                .AsNoTracking()
                .Where(r => r.PipelineDefinitionId == definitionId);

            if (!string.IsNullOrEmpty(branchName))
            {
                query = query.Where(r => r.SourceBranch == branchName);
            }

            if (branchScopeByPipelineId?.TryGetValue(definition.PipelineDefinitionId, out var defaultBranch) == true
                && !string.IsNullOrWhiteSpace(defaultBranch))
            {
                var normalizedDefaultBranch = defaultBranch.ToLowerInvariant();
                query = query.Where(r => r.SourceBranch != null && r.SourceBranch.ToLower() == normalizedDefaultBranch);
            }

            if (minFinishTimeUtc.HasValue)
            {
                query = query.Where(r => r.FinishedDateUtc.HasValue && r.FinishedDateUtc.Value >= minFinishTimeUtc.Value);
            }

            if (maxFinishTimeUtc.HasValue)
            {
                query = query.Where(r => r.FinishedDateUtc.HasValue && r.FinishedDateUtc.Value <= maxFinishTimeUtc.Value);
            }

            var pipelineRuns = await query
                .OrderByDescending(r => r.FinishedDateUtc)
                .ThenByDescending(r => r.CreatedDateUtc)
                .Take(top)
                .ToListAsync(cancellationToken);

            runs.AddRange(pipelineRuns.Select(r => MapRunToDto(r, definition.Name)));
        }

        runs = runs
            .OrderByDescending(run => run.FinishTime)
            .ThenByDescending(run => run.StartTime)
            .ThenByDescending(run => run.RunId)
            .ToList();

        if (runs.Count == 0)
        {
            await LogEmptyRunsDiagnosticsAsync(pipelineIdList, branchName, minFinishTime, cancellationToken);
        }

        _logger.LogDebug(
            "CachedPipelineReadProvider: Returning {RunCount} cached runs after per-pipeline selection for {PipelineCount} pipelines",
            runs.Count,
            definitions.Count);

        return runs;
    }

    /// <summary>
    /// Retrieves pipeline definitions for a specific product from the cache.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching definitions for product {ProductId}", productId);

        var definitions = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .Where(d => d.ProductId == productId)
            .ToListAsync(cancellationToken);

        return definitions.Select(MapDefinitionToDto);
    }

    /// <summary>
    /// Retrieves pipeline definitions for a specific repository from the cache.
    /// </summary>
    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPipelineReadProvider: Fetching definitions for repository {RepositoryId}", repositoryId);

        var definitions = await _dbContext.PipelineDefinitions
            .AsNoTracking()
            .Where(d => d.RepositoryId == repositoryId)
            .ToListAsync(cancellationToken);

        return definitions.Select(MapDefinitionToDto);
    }

    private static PipelineRunDto MapRunToDto(CachedPipelineRunEntity entity, string pipelineName)
    {
        return new PipelineRunDto(
            RunId: entity.TfsRunId,
            PipelineId: entity.PipelineDefinitionId,
            PipelineName: pipelineName,
            StartTime: entity.CreatedDate,
            FinishTime: entity.FinishedDate,
            Duration: entity.FinishedDate.HasValue && entity.CreatedDate.HasValue 
                ? entity.FinishedDate.Value - entity.CreatedDate.Value 
                : null,
            Result: ParseResult(entity.Result),
            Trigger: PipelineRunTrigger.Unknown, // Not stored in cache
            TriggerInfo: null,
            Branch: entity.SourceBranch,
            RequestedFor: null, // Not stored in cache
            RetrievedAt: entity.CachedAt
        );
    }

    private static PipelineRunResult ParseResult(string? result)
    {
        return result?.ToLowerInvariant() switch
        {
            "succeeded" => PipelineRunResult.Succeeded,
            "failed" => PipelineRunResult.Failed,
            "partiallysucceeded" => PipelineRunResult.PartiallySucceeded,
            "canceled" => PipelineRunResult.Canceled,
            _ => PipelineRunResult.Unknown
        };
    }

    private static PipelineDefinitionDto MapDefinitionToDto(PipelineDefinitionEntity entity)
    {
        return new PipelineDefinitionDto
        {
            PipelineDefinitionId = entity.PipelineDefinitionId,
            ProductId = entity.ProductId,
            RepositoryId = entity.RepositoryId,
            RepoId = entity.RepoId,
            RepoName = entity.RepoName,
            Name = entity.Name,
            YamlPath = entity.YamlPath,
            Folder = entity.Folder,
            Url = entity.Url,
            LastSyncedUtc = entity.LastSyncedUtc
        };
    }

    /// <summary>
    /// Logs debug-level diagnostics when a pipeline runs query returns an empty result set.
    /// Helps diagnose whether the DB has runs that were excluded by filters.
    /// </summary>
    private async Task LogEmptyRunsDiagnosticsAsync(
        List<int> pipelineIds,
        string? branchName,
        DateTimeOffset? minFinishTime,
        CancellationToken cancellationToken)
    {
        try
        {
            var totalRuns = await _dbContext.CachedPipelineRuns.CountAsync(cancellationToken);
            DateTime? minDate = null;
            DateTime? maxDate = null;

            if (totalRuns > 0)
            {
                var datedRuns = _dbContext.CachedPipelineRuns
                    .Where(r => r.FinishedDateUtc != null);
                minDate = await datedRuns.MinAsync(r => r.FinishedDateUtc, cancellationToken);
                maxDate = await datedRuns.MaxAsync(r => r.FinishedDateUtc, cancellationToken);
            }

            _logger.LogDebug(
                "PIPELINE_EMPTY_RESULT_DIAG: totalDbRows={TotalRows}, " +
                "dbDateRange=[{MinDate} .. {MaxDate}], " +
                "appliedFilters: pipelineIds=[{PipelineIds}], branch={Branch}, minFinishTime={MinFinishTime}",
                totalRuns,
                minDate?.ToString("O") ?? "n/a",
                maxDate?.ToString("O") ?? "n/a",
                string.Join(",", pipelineIds),
                branchName ?? "any",
                minFinishTime?.ToString("O") ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to compute empty-result diagnostics for pipeline runs");
        }
    }
}
