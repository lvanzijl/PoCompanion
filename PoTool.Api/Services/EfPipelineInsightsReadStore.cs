using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Pipelines.Filters;

namespace PoTool.Api.Services;

/// <summary>
/// EF-backed Pipeline Insights read store.
/// </summary>
public sealed class EfPipelineInsightsReadStore : IPipelineInsightsReadStore
{
    private readonly PoToolDbContext _context;

    public EfPipelineInsightsReadStore(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<PipelineInsightsSprintWindow?> GetSprintWindowAsync(
        int sprintId,
        CancellationToken cancellationToken)
    {
        return await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.Id == sprintId)
            .Select(sprint => new PipelineInsightsSprintWindow(
                sprint.Id,
                sprint.Name,
                sprint.TeamId,
                sprint.StartDateUtc,
                sprint.EndDateUtc,
                sprint.StartUtc,
                sprint.EndUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PipelineInsightsSprintWindow?> GetPreviousSprintWindowAsync(
        int teamId,
        DateTime sprintStartUtc,
        CancellationToken cancellationToken)
    {
        return await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.TeamId == teamId
                && sprint.StartDateUtc.HasValue
                && sprint.StartDateUtc.Value < sprintStartUtc)
            .OrderByDescending(sprint => sprint.StartDateUtc)
            .Select(sprint => new PipelineInsightsSprintWindow(
                sprint.Id,
                sprint.Name,
                sprint.TeamId,
                sprint.StartDateUtc,
                sprint.EndDateUtc,
                sprint.StartUtc,
                sprint.EndUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PipelineInsightsProductSelection>> GetProductsAsync(
        PipelineEffectiveFilter filter,
        CancellationToken cancellationToken)
    {
        IQueryable<ProductEntity> productsQuery = _context.Products
            .AsNoTracking();

        if (!filter.Context.ProductIds.IsAll)
        {
            var productIds = filter.Context.ProductIds.Values.ToArray();
            productsQuery = productsQuery.Where(product => productIds.Contains(product.Id));
        }

        return await productsQuery
            .OrderBy(product => product.Name)
            .Select(product => new PipelineInsightsProductSelection(
                product.Id,
                product.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PipelineInsightsDefinitionSelection>> GetPipelineDefinitionsAsync(
        IReadOnlyList<int> productIds,
        IReadOnlyCollection<int> repositoryIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Count == 0 || repositoryIds.Count == 0)
        {
            return Array.Empty<PipelineInsightsDefinitionSelection>();
        }

        return await _context.PipelineDefinitions
            .AsNoTracking()
            .Where(definition => productIds.Contains(definition.ProductId)
                && repositoryIds.Contains(definition.RepositoryId))
            .Select(definition => new PipelineInsightsDefinitionSelection(
                definition.Id,
                definition.PipelineDefinitionId,
                definition.ProductId,
                definition.RepositoryId,
                definition.Name,
                definition.DefaultBranch))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PipelineInsightsRun>> GetRunsAsync(
        IReadOnlyList<PipelineInsightsDefinitionSelection> pipelineDefinitions,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        CancellationToken cancellationToken)
    {
        if (pipelineDefinitions.Count == 0)
        {
            return Array.Empty<PipelineInsightsRun>();
        }

        var definitionIds = pipelineDefinitions
            .Select(definition => definition.Id)
            .ToList();

        var runs = await _context.CachedPipelineRuns
            .AsNoTracking()
            .Where(run => definitionIds.Contains(run.PipelineDefinitionId)
                && run.FinishedDateUtc.HasValue
                && run.FinishedDateUtc.Value >= rangeStartUtc
                && run.FinishedDateUtc.Value < rangeEndUtc)
            .Select(run => new PipelineInsightsRun(
                run.Id,
                run.TfsRunId,
                run.PipelineDefinitionId,
                run.Result,
                run.RunName,
                run.CreatedDateUtc,
                run.FinishedDateUtc,
                run.CreatedDate,
                run.FinishedDate,
                run.SourceBranch,
                run.Url))
            .ToListAsync(cancellationToken);

        var defaultBranchByDefId = pipelineDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.DefaultBranch))
            .ToDictionary(definition => definition.Id, definition => definition.DefaultBranch);

        if (defaultBranchByDefId.Count == 0)
        {
            return runs;
        }

        return runs
            .Where(run =>
            {
                if (!defaultBranchByDefId.TryGetValue(run.DefId, out var branch)
                    || string.IsNullOrEmpty(branch))
                {
                    return true;
                }

                return string.Equals(run.SourceBranch, branch, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }
}
