using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Domain.WorkItems;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Services;

public class ForecastProjectionMaterializationService
{
    private const int MaxSupportedVelocityWindow = 20;
    private const int MaxIterationPathScan = MaxSupportedVelocityWindow * 2;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly PoToolDbContext _context;
    private readonly IHierarchyRollupService _hierarchyRollupService;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly IDeliveryForecastProjector _projector;
    private readonly ILogger<ForecastProjectionMaterializationService> _logger;

    public ForecastProjectionMaterializationService(
        PoToolDbContext context,
        IHierarchyRollupService hierarchyRollupService,
        IWorkItemStateClassificationService stateClassificationService,
        IDeliveryForecastProjector projector,
        ILogger<ForecastProjectionMaterializationService> logger)
    {
        _context = context;
        _hierarchyRollupService = hierarchyRollupService;
        _stateClassificationService = stateClassificationService;
        _projector = projector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ForecastProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        var productIds = await _context.Products
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return Array.Empty<ForecastProjectionEntity>();
        }

        var resolvedItems = await _context.ResolvedWorkItems
            .AsNoTracking()
            .Where(item => item.ResolvedProductId != null && productIds.Contains(item.ResolvedProductId.Value))
            .ToListAsync(cancellationToken);

        var scopedWorkItemIds = resolvedItems
            .Select(item => item.WorkItemId)
            .Distinct()
            .ToList();

        if (scopedWorkItemIds.Count == 0)
        {
            return Array.Empty<ForecastProjectionEntity>();
        }

        var workItems = await _context.WorkItems
            .AsNoTracking()
            .Where(workItem => scopedWorkItemIds.Contains(workItem.TfsId))
            .ToListAsync(cancellationToken);

        if (workItems.Count == 0)
        {
            return Array.Empty<ForecastProjectionEntity>();
        }

        var canonicalWorkItems = workItems
            .Select(workItem => workItem.ToCanonicalWorkItem())
            .ToList();
        var targetWorkItems = workItems
            .Where(workItem =>
                string.Equals(workItem.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase)
                || string.Equals(workItem.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var stateLookup = await GetStateLookupAsync(cancellationToken);
        var doneByWorkItemId = workItems.ToDictionary(
            workItem => workItem.TfsId,
            workItem => StateClassificationLookup.IsDone(
                stateLookup,
                workItem.Type.ToCanonicalWorkItemType(),
                workItem.State));

        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.Team.ProductTeamLinks.Any(link => productIds.Contains(link.ProductId)))
            .ToListAsync(cancellationToken);

        var sprintByPath = sprints
            .Where(sprint => !string.IsNullOrWhiteSpace(sprint.Path))
            .GroupBy(sprint => sprint.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(sprint => sprint.EndDateUtc ?? DateTime.MinValue)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var projectionTotalsBySprintId = await _context.SprintMetricsProjections
            .AsNoTracking()
            .Where(projection => productIds.Contains(projection.ProductId))
            .GroupBy(projection => projection.SprintId)
            .Select(group => new
            {
                SprintId = group.Key,
                CompletedStoryPoints = group.Sum(projection => projection.CompletedPbiStoryPoints)
            })
            .ToDictionaryAsync(
                group => group.SprintId,
                group => group.CompletedStoryPoints,
                cancellationToken);

        var existingEntities = await _context.ForecastProjections
            .Where(entity => scopedWorkItemIds.Contains(entity.WorkItemId))
            .ToDictionaryAsync(entity => entity.WorkItemId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var results = new List<ForecastProjectionEntity>(targetWorkItems.Count);
        var targetIds = new HashSet<int>();

        foreach (var workItem in targetWorkItems)
        {
            targetIds.Add(workItem.TfsId);

            var scope = _hierarchyRollupService.RollupCanonicalScope(
                workItem.ToCanonicalWorkItem(),
                canonicalWorkItems,
                doneByWorkItemId);

            var historicalSamples = BuildHistoricalVelocitySamples(
                workItems,
                workItem.AreaPath,
                sprintByPath,
                projectionTotalsBySprintId);

            var variants = BuildVariants(
                workItem.TfsId,
                workItem.Type.ToCanonicalWorkItemType(),
                scope.Total,
                scope.Completed,
                historicalSamples,
                now);

            var canonicalProjection = variants[4].Projection;
            var serializedVariants = JsonSerializer.Serialize(
                variants.Select(variant => variant.Stored).ToList(),
                SerializerOptions);

            if (existingEntities.TryGetValue(workItem.TfsId, out var existingEntity))
            {
                existingEntity.WorkItemType = canonicalProjection.WorkItemType;
                existingEntity.SprintsRemaining = canonicalProjection.SprintsRemaining;
                existingEntity.EstimatedCompletionDate = canonicalProjection.EstimatedCompletionDate;
                existingEntity.Confidence = canonicalProjection.Confidence.ToString();
                existingEntity.LastUpdated = now;
                existingEntity.ProjectionVariantsJson = serializedVariants;
                results.Add(existingEntity);
            }
            else
            {
                var entity = new ForecastProjectionEntity
                {
                    WorkItemId = workItem.TfsId,
                    WorkItemType = canonicalProjection.WorkItemType,
                    SprintsRemaining = canonicalProjection.SprintsRemaining,
                    EstimatedCompletionDate = canonicalProjection.EstimatedCompletionDate,
                    Confidence = canonicalProjection.Confidence.ToString(),
                    LastUpdated = now,
                    ProjectionVariantsJson = serializedVariants
                };

                _context.ForecastProjections.Add(entity);
                results.Add(entity);
            }
        }

        foreach (var staleEntity in existingEntities.Values.Where(entity => !targetIds.Contains(entity.WorkItemId)))
        {
            _context.ForecastProjections.Remove(staleEntity);
        }

        var cacheState = await _context.ProductOwnerCacheStates
            .OrderBy(state => state.Id)
            .FirstOrDefaultAsync(state => state.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState != null)
        {
            cacheState.ForecastProjectionAsOfUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Computed {ProjectionCount} forecast projections for ProductOwner {ProductOwnerId}",
            results.Count,
            productOwnerId);

        return results;
    }

    internal static IReadOnlyList<HistoricalVelocitySample> BuildHistoricalVelocitySamples(
        IReadOnlyList<WorkItemEntity> workItems,
        string? areaPath,
        IReadOnlyDictionary<string, SprintEntity> sprintByPath,
        IReadOnlyDictionary<int, double> projectionTotalsBySprintId)
    {
        var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);

        return workItems
            .Where(workItem =>
                string.IsNullOrWhiteSpace(areaPath)
                || workItem.AreaPath.StartsWith(areaPath, StringComparison.OrdinalIgnoreCase))
            .Select(workItem => workItem.IterationPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(MaxIterationPathScan)
            .Select(path => CreateHistoricalVelocitySample(path, sprintByPath, projectionTotalsBySprintId))
            .Where(sample => sample is not null && (!sample.SprintEndDate.HasValue || sample.SprintEndDate >= sixMonthsAgo))
            .Select(sample => sample!)
            .ToList();
    }

    private static HistoricalVelocitySample? CreateHistoricalVelocitySample(
        string iterationPath,
        IReadOnlyDictionary<string, SprintEntity> sprintByPath,
        IReadOnlyDictionary<int, double> projectionTotalsBySprintId)
    {
        if (!sprintByPath.TryGetValue(iterationPath, out var sprint))
        {
            return null;
        }

        projectionTotalsBySprintId.TryGetValue(sprint.Id, out var completedStoryPoints);

        return new HistoricalVelocitySample(
            string.IsNullOrWhiteSpace(sprint.Name) ? iterationPath : sprint.Name,
            sprint.EndDateUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDateUtc.Value, DateTimeKind.Utc), TimeSpan.Zero)
                : null,
            Math.Round(completedStoryPoints, MidpointRounding.AwayFromZero));
    }

    private IReadOnlyList<(ForecastProjection Projection, StoredForecastProjectionVariant Stored)> BuildVariants(
        int workItemId,
        string workItemType,
        double totalScopeStoryPoints,
        double completedScopeStoryPoints,
        IReadOnlyList<HistoricalVelocitySample> historicalSamples,
        DateTimeOffset lastUpdated)
    {
        var variants = new List<(ForecastProjection Projection, StoredForecastProjectionVariant Stored)>(MaxSupportedVelocityWindow);

        for (var maxSprints = 1; maxSprints <= MaxSupportedVelocityWindow; maxSprints++)
        {
            var projection = _projector.Project(
                workItemId,
                workItemType,
                totalScopeStoryPoints,
                completedScopeStoryPoints,
                historicalSamples.Take(maxSprints).ToList(),
                lastUpdated);

            variants.Add((
                projection,
                new StoredForecastProjectionVariant(
                    maxSprints,
                    projection.WorkItemId,
                    projection.WorkItemType,
                    projection.TotalScopeStoryPoints,
                    projection.CompletedScopeStoryPoints,
                    projection.RemainingScopeStoryPoints,
                    projection.EstimatedVelocity,
                    projection.SprintsRemaining,
                    projection.EstimatedCompletionDate,
                    projection.Confidence,
                    projection.LastUpdated,
                    projection.ForecastByDate.ToList())));
        }

        return variants;
    }

    private async Task<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> GetStateLookupAsync(
        CancellationToken cancellationToken)
    {
        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        return StateClassificationLookup.Create(response.Classifications.ToDomainStateClassifications());
    }

}
