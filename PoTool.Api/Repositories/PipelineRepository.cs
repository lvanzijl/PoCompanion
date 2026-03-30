using PoTool.Core.Contracts;
using PoTool.Shared.Pipelines;

using PoTool.Core.Pipelines;
using PoTool.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Repositories;

/// <summary>
/// Pipeline repository implementation with hybrid storage:
/// - In-memory for pipeline runs (exploratory, volatile data)
/// - Database for pipeline definitions (persistent, per-product data)
/// </summary>
public class PipelineRepository : IPipelineRepository
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<PipelineRepository> _logger;

    // In-memory storage for pipeline runs (volatile data)
    private readonly object _lock = new();
    private List<PipelineDto> _pipelines = new();
    private List<PipelineRunDto> _runs = new();

    public PipelineRepository(PoToolDbContext context, ILogger<PipelineRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<PipelineDto>>(_pipelines.ToList());
        }
    }

    public Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var pipeline = _pipelines.FirstOrDefault(p => p.Id == pipelineId);
            return Task.FromResult(pipeline);
        }
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var runs = _runs
                .Where(r => r.PipelineId == pipelineId)
                .OrderByDescending(r => r.StartTime)
                .Take(top)
                .ToList();
            return Task.FromResult<IEnumerable<PipelineRunDto>>(runs);
        }
    }

    public Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IEnumerable<PipelineRunDto>>(_runs.ToList());
        }
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minFinishTime = null,
        DateTimeOffset? maxFinishTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var pipelineIdSet = new HashSet<int>(pipelineIds);
            var filteredRuns = _runs
                .Where(r => pipelineIdSet.Contains(r.PipelineId));

            if (!string.IsNullOrEmpty(branchName))
            {
                filteredRuns = filteredRuns.Where(r => r.Branch == branchName);
            }

            if (minFinishTime.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.FinishTime.HasValue && r.FinishTime.Value >= minFinishTime.Value);
            }

            if (maxFinishTime.HasValue)
            {
                filteredRuns = filteredRuns.Where(r => r.FinishTime.HasValue && r.FinishTime.Value <= maxFinishTime.Value);
            }

            var result = filteredRuns
                .GroupBy(r => r.PipelineId)
                .SelectMany(g => g.OrderByDescending(r => r.FinishTime).ThenByDescending(r => r.StartTime).Take(top))
                .ToList();

            return Task.FromResult<IEnumerable<PipelineRunDto>>(result);
        }
    }


    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pipelines.Clear();
            _runs.Clear();
            return Task.CompletedTask;
        }
    }

    // ============================================
    // PIPELINE DEFINITION METHODS (DATABASE)
    // ============================================

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.PipelineDefinitions
            .Where(pd => pd.ProductId == productId)
            .OrderBy(pd => pd.RepoName)
            .ThenBy(pd => pd.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.PipelineDefinitions
            .Where(pd => pd.RepositoryId == repositoryId)
            .OrderBy(pd => pd.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task SaveDefinitionsAsync(
        IEnumerable<PipelineDefinitionDto> definitions,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var definitionsList = definitions.ToList();
        var productIdsList = productIds.ToList();

        _logger.LogInformation(
            "Saving {Count} pipeline definitions for products: {ProductIds}",
            definitionsList.Count, string.Join(", ", productIdsList));

        // Fetch existing definitions for the affected products
        var existingDefinitions = await _context.PipelineDefinitions
            .Where(pd => productIdsList.Contains(pd.ProductId))
            .ToListAsync(cancellationToken);

        var existingByKey = existingDefinitions
            .ToDictionary(pd => (pd.ProductId, pd.PipelineDefinitionId));

        var updated = 0;
        var inserted = 0;

        // Upsert definitions from TFS
        foreach (var dto in definitionsList)
        {
            if (!dto.ProductId.HasValue || !dto.RepositoryId.HasValue)
            {
                _logger.LogWarning(
                    "Skipping pipeline definition {DefId} - missing ProductId or RepositoryId",
                    dto.PipelineDefinitionId);
                continue;
            }

            var key = (dto.ProductId.Value, dto.PipelineDefinitionId);

            if (existingByKey.TryGetValue(key, out var existing))
            {
                // Update existing
                existing.Name = dto.Name;
                existing.RepoId = dto.RepoId;
                existing.RepoName = dto.RepoName;
                existing.YamlPath = dto.YamlPath;
                existing.Folder = dto.Folder;
                existing.Url = dto.Url;
                existing.LastSyncedUtc = dto.LastSyncedUtc;
                existing.RepositoryId = dto.RepositoryId.Value;
                updated++;
            }
            else
            {
                // Insert new
                var entity = new PipelineDefinitionEntity
                {
                    PipelineDefinitionId = dto.PipelineDefinitionId,
                    ProductId = dto.ProductId.Value,
                    RepositoryId = dto.RepositoryId.Value,
                    RepoId = dto.RepoId,
                    RepoName = dto.RepoName,
                    Name = dto.Name,
                    YamlPath = dto.YamlPath,
                    Folder = dto.Folder,
                    Url = dto.Url,
                    LastSyncedUtc = dto.LastSyncedUtc
                };

                _context.PipelineDefinitions.Add(entity);
                inserted++;
            }
        }

        // Remove stale definitions (exist in DB but not in current sync for the affected products)
        var currentKeys = definitionsList
            .Where(d => d.ProductId.HasValue)
            .Select(d => (d.ProductId!.Value, d.PipelineDefinitionId))
            .ToHashSet();

        var staleDefinitions = existingDefinitions
            .Where(e => !currentKeys.Contains((e.ProductId, e.PipelineDefinitionId)))
            .ToList();

        if (staleDefinitions.Any())
        {
            _context.PipelineDefinitions.RemoveRange(staleDefinitions);
            _logger.LogInformation(
                "Removing {Count} stale pipeline definitions for products: {ProductIds}",
                staleDefinitions.Count, string.Join(", ", productIdsList));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Pipeline definitions saved: {Inserted} inserted, {Updated} updated, {Removed} removed",
            inserted, updated, staleDefinitions.Count);
    }

    private static PipelineDefinitionDto MapToDto(PipelineDefinitionEntity entity)
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
}
