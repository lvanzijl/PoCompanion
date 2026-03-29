using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;

namespace PoTool.Api.Services.BuildQuality;

/// <summary>
/// EF-backed BuildQuality read store.
/// </summary>
public sealed class EfBuildQualityReadStore : IBuildQualityReadStore
{
    private readonly PoToolDbContext _context;

    public EfBuildQualityReadStore(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<BuildQualityScopeSelection> GetScopeSelectionAsync(
        int productOwnerId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        int? repositoryId,
        int? pipelineDefinitionId,
        CancellationToken cancellationToken)
    {
        var productIds = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        return await LoadScopeSelectionCoreAsync(
            productIds,
            windowStartUtc,
            windowEndUtc,
            repositoryId,
            pipelineDefinitionId,
            cancellationToken,
            productOwnerId);
    }

    public async Task<BuildQualityScopeSelection> GetScopeSelectionAsync(
        IReadOnlyList<int> productIds,
        DateTime? windowStartUtc,
        DateTime? windowEndUtc,
        int? repositoryId,
        int? pipelineDefinitionId,
        CancellationToken cancellationToken)
    {
        return await LoadScopeSelectionCoreAsync(
            productIds,
            windowStartUtc ?? default,
            windowEndUtc ?? default,
            repositoryId,
            pipelineDefinitionId,
            cancellationToken,
            productOwnerId: null);
    }

    public async Task<BuildQualitySprintWindow?> GetSprintWindowAsync(
        int sprintId,
        CancellationToken cancellationToken)
    {
        return await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.Id == sprintId)
            .Select(sprint => new BuildQualitySprintWindow(
                sprint.Id,
                sprint.Name,
                sprint.TeamId,
                sprint.StartDateUtc,
                sprint.EndDateUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<BuildQualityScopeSelection> LoadScopeSelectionCoreAsync(
        IReadOnlyList<int> requestedProductIds,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        int? repositoryId,
        int? pipelineDefinitionId,
        CancellationToken cancellationToken,
        int? productOwnerId)
    {
        var normalizedProductIds = requestedProductIds
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        var productQuery = _context.Products
            .AsNoTracking()
            .Where(product => normalizedProductIds.Contains(product.Id));

        if (repositoryId.HasValue)
        {
            productQuery = productQuery.Where(product => product.Repositories.Any(repository => repository.Id == repositoryId.Value));
        }

        var products = await productQuery
            .OrderBy(product => product.Name)
            .Select(product => new ProductRecord(product.Id, product.Name))
            .ToListAsync(cancellationToken);

        var candidateProductIds = products.Select(product => product.Id).ToList();

        var pipelineDefinitions = candidateProductIds.Count == 0
            ? new List<PipelineDefinitionRecord>()
            : await _context.PipelineDefinitions
                .AsNoTracking()
                .Where(definition => candidateProductIds.Contains(definition.ProductId))
                .Where(definition => !repositoryId.HasValue || definition.RepositoryId == repositoryId.Value)
                .Where(definition => !pipelineDefinitionId.HasValue || definition.PipelineDefinitionId == pipelineDefinitionId.Value)
                .Select(definition => new PipelineDefinitionRecord(
                    definition.Id,
                    definition.PipelineDefinitionId,
                    definition.ProductId,
                    definition.RepositoryId,
                    definition.Name,
                    definition.RepoName,
                    definition.DefaultBranch))
                .ToListAsync(cancellationToken);

        var productsInScope = (repositoryId.HasValue || pipelineDefinitionId.HasValue)
            ? products.Where(product => pipelineDefinitions.Any(definition => definition.ProductId == product.Id)).ToList()
            : products;

        var pipelineDefinitionDbIds = pipelineDefinitions
            .Select(definition => definition.Id)
            .Distinct()
            .ToList();
        var builds = pipelineDefinitionDbIds.Count == 0
            ? new List<BuildRecord>()
            : await LoadBuildsAsync(
                pipelineDefinitions,
                windowStartUtc,
                windowEndUtc,
                productOwnerId,
                cancellationToken);

        var buildIds = builds.Select(build => build.Id).ToList();
        var testRuns = buildIds.Count == 0
            ? new List<BuildQualityTestRunFact>()
            : await _context.TestRuns
                .AsNoTracking()
                .Where(testRun => buildIds.Contains(testRun.BuildId))
                .Select(testRun => new BuildQualityTestRunFact(
                    testRun.BuildId,
                    testRun.TotalTests,
                    testRun.PassedTests,
                    testRun.NotApplicableTests))
                .ToListAsync(cancellationToken);

        var coverages = buildIds.Count == 0
            ? new List<BuildQualityCoverageFact>()
            : await _context.Coverages
                .AsNoTracking()
                .Where(coverage => buildIds.Contains(coverage.BuildId))
                .Select(coverage => new BuildQualityCoverageFact(
                    coverage.BuildId,
                    coverage.CoveredLines,
                    coverage.TotalLines))
                .ToListAsync(cancellationToken);

        var definitionsByDbId = pipelineDefinitions.ToDictionary(definition => definition.Id);
        var productSelections = productsInScope
            .Select(product =>
            {
                var productDefinitions = pipelineDefinitions
                    .Where(definition => definition.ProductId == product.Id)
                    .ToList();
                var productBuilds = builds
                    .Where(build => definitionsByDbId.TryGetValue(build.PipelineDefinitionId, out var definition) && definition.ProductId == product.Id)
                    .ToList();
                var productBuildIds = productBuilds.Select(build => build.Id).ToHashSet();

                return new BuildQualityProductSelection(
                    product.Id,
                    product.Name,
                    productDefinitions.Select(definition => definition.ExternalPipelineDefinitionId).Distinct().OrderBy(id => id).ToArray(),
                    productDefinitions.Select(definition => definition.RepositoryId).Distinct().OrderBy(id => id).ToArray(),
                    productBuilds.Select(build => new BuildQualityBuildFact(build.Id, build.Result)).ToArray(),
                    testRuns.Where(testRun => productBuildIds.Contains(testRun.BuildId)).ToArray(),
                    coverages.Where(coverage => productBuildIds.Contains(coverage.BuildId)).ToArray());
            })
            .ToArray();

        return new BuildQualityScopeSelection(
            productOwnerId ?? 0,
            windowStartUtc,
            windowEndUtc,
            productsInScope.Select(product => product.Id).OrderBy(id => id).ToArray(),
            pipelineDefinitions
                .Where(definition => !string.IsNullOrWhiteSpace(definition.DefaultBranch))
                .Select(definition => definition.DefaultBranch!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(branch => branch, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            pipelineDefinitions,
            productSelections,
            builds.Select(build => new BuildQualityBuildFact(build.Id, build.Result)).ToArray(),
            testRuns,
            coverages);
    }

    private sealed record ProductRecord(int Id, string Name);

    public sealed record PipelineDefinitionRecord(
        int Id,
        int ExternalPipelineDefinitionId,
        int ProductId,
        int RepositoryId,
        string PipelineName,
        string RepositoryName,
        string? DefaultBranch);

    private sealed record BuildRecord(int Id, int PipelineDefinitionId, string? SourceBranch, string? Result);

    private async Task<List<BuildRecord>> LoadBuildsAsync(
        IReadOnlyList<PipelineDefinitionRecord> pipelineDefinitions,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        int? productOwnerId,
        CancellationToken cancellationToken)
    {
        var builds = new List<BuildRecord>();

        foreach (var definition in pipelineDefinitions)
        {
            var query = _context.CachedPipelineRuns
                .AsNoTracking()
                .Where(build => build.PipelineDefinitionId == definition.Id)
                .Where(build => !productOwnerId.HasValue || build.ProductOwnerId == productOwnerId.Value)
                .Where(build => build.FinishedDateUtc.HasValue
                    && build.FinishedDateUtc.Value >= windowStartUtc
                    && build.FinishedDateUtc.Value < windowEndUtc);

            if (!string.IsNullOrWhiteSpace(definition.DefaultBranch))
            {
                var normalizedDefaultBranch = definition.DefaultBranch.ToLowerInvariant();
                query = query.Where(build => build.SourceBranch != null && build.SourceBranch.ToLower() == normalizedDefaultBranch);
            }

            var definitionBuilds = await query
                .Select(build => new BuildRecord(
                    build.Id,
                    build.PipelineDefinitionId,
                    build.SourceBranch,
                    build.Result))
                .ToListAsync(cancellationToken);

            builds.AddRange(definitionBuilds);
        }

        return builds;
    }
}

/// <summary>
/// Product-scoped BuildQuality raw fact selection.
/// </summary>
public sealed record BuildQualityProductSelection(
    int ProductId,
    string ProductName,
    IReadOnlyList<int> PipelineDefinitionIds,
    IReadOnlyList<int> RepositoryIds,
    IReadOnlyList<BuildQualityBuildFact> Builds,
    IReadOnlyList<BuildQualityTestRunFact> TestRuns,
    IReadOnlyList<BuildQualityCoverageFact> Coverages);

/// <summary>
/// Selected BuildQuality raw facts for a requested scope.
/// </summary>
public sealed record BuildQualityScopeSelection(
    int ProductOwnerId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<int> ProductIds,
    IReadOnlyList<string> DefaultBranches,
    IReadOnlyList<EfBuildQualityReadStore.PipelineDefinitionRecord> PipelineDefinitions,
    IReadOnlyList<BuildQualityProductSelection> Products,
    IReadOnlyList<BuildQualityBuildFact> Builds,
    IReadOnlyList<BuildQualityTestRunFact> TestRuns,
    IReadOnlyList<BuildQualityCoverageFact> Coverages);
