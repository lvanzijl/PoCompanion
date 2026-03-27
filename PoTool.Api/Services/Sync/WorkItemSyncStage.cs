using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts work items from TFS.
/// </summary>
public class WorkItemSyncStage : ISyncStage
{
    private const int LoggedIdSampleSize = 20;
    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly IIncrementalSyncPlanner _incrementalSyncPlanner;
    private readonly ILogger<WorkItemSyncStage> _logger;

    public string StageName => "SyncWorkItems";
    public int StageNumber => 1;

    public WorkItemSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        IIncrementalSyncPlanner incrementalSyncPlanner,
        ILogger<WorkItemSyncStage> logger)
    {
        _tfsClient = tfsClient;
        _context = context;
        _incrementalSyncPlanner = incrementalSyncPlanner;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.RootWorkItemIds.Length == 0)
            {
                LogIncrementalSyncPlanSkipped(context, "NoRootWorkItemIds");
                return SyncStageResult.CreateSuccess(0);
            }

            progressCallback(0);

            _logger.LogInformation(
                "Starting work item sync for ProductOwner {ProductOwnerId} with {RootCount} roots, watermark: {Watermark}",
                context.ProductOwnerId,
                context.RootWorkItemIds.Length,
                context.WorkItemWatermark?.ToString("O") ?? "null (full sync)");

            // Fetch work items from TFS
            var workItems = await _tfsClient.GetWorkItemsByRootIdsAsync(
                context.RootWorkItemIds,
                context.WorkItemWatermark,
                (fetched, total, message) =>
                {
                    // Map TFS progress to 0-80% (leave 20% for database upsert)
                    var percent = total > 0 ? (int)((fetched / (double)total) * 80) : 50;
                    progressCallback(percent);
                },
                cancellationToken);

            var workItemList = workItems.ToList();

            _logger.LogInformation(
                "Fetched {Count} work items from TFS for ProductOwner {ProductOwnerId}",
                workItemList.Count,
                context.ProductOwnerId);

            progressCallback(80);

            if (workItemList.Count == 0)
            {
                LogIncrementalSyncPlanSkipped(context, "NoWorkItemsFetched");
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0, context.WorkItemWatermark);
            }

            var incrementalPlanExecution = await BuildIncrementalSyncPlanAsync(context, workItemList, cancellationToken);
            LogIncrementalSyncPlan(context, incrementalPlanExecution);

            // Upsert work items to database in batches
            var maxChangedDate = await UpsertWorkItemsAsync(workItemList, context.ProductOwnerId, progressCallback, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {Count} work items for ProductOwner {ProductOwnerId}, new watermark: {Watermark}",
                workItemList.Count,
                context.ProductOwnerId,
                maxChangedDate?.ToString("O") ?? "none");

            return SyncStageResult.CreateSuccess(workItemList.Count, maxChangedDate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Work item sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work item sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTimeOffset?> UpsertWorkItemsAsync(
        List<WorkItemDto> workItems,
        int productOwnerId,
        Action<int> progressCallback,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        DateTimeOffset? maxChangedDate = null;

        var tfsIds = workItems.Select(w => w.TfsId).ToList();
        var existingIds = await _context.WorkItems
            .Where(w => tfsIds.Contains(w.TfsId))
            .Select(w => w.TfsId)
            .ToListAsync(cancellationToken);

        var existingSet = existingIds.ToHashSet();
        var totalBatches = (int)Math.Ceiling(workItems.Count / (double)batchSize);
        var processedBatches = 0;

        foreach (var batch in workItems.Chunk(batchSize))
        {
            foreach (var dto in batch)
            {
                var changedDate = dto.ChangedDate ?? dto.RetrievedAt;
                if (maxChangedDate == null || changedDate > maxChangedDate)
                {
                    maxChangedDate = changedDate;
                }

                if (existingSet.Contains(dto.TfsId))
                {
                    // Update existing
                    var entity = await _context.WorkItems
                        .OrderBy(w => w.TfsId)
                        .FirstAsync(w => w.TfsId == dto.TfsId, cancellationToken);
                    UpdateEntity(entity, dto);
                }
                else
                {
                    // Insert new
                    var entity = MapToEntity(dto);
                    await _context.WorkItems.AddAsync(entity, cancellationToken);
                    existingSet.Add(dto.TfsId);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            processedBatches++;
            // Map batch progress to 80-100%
            var percent = 80 + (int)((processedBatches / (double)totalBatches) * 20);
            progressCallback(Math.Min(percent, 99));
        }

        return maxChangedDate;
    }

    private static void UpdateEntity(WorkItemEntity entity, WorkItemDto dto)
    {
        entity.ParentTfsId = dto.ParentTfsId;
        entity.Type = dto.Type;
        entity.Title = dto.Title;
        entity.AreaPath = dto.AreaPath;
        entity.IterationPath = dto.IterationPath;
        entity.State = dto.State;
        entity.RetrievedAt = dto.RetrievedAt;
        entity.Effort = dto.Effort;
        entity.BusinessValue = dto.BusinessValue;
        entity.TimeCriticality = dto.TimeCriticality;
        entity.ProjectNumber = dto.ProjectNumber;
        entity.ProjectElement = dto.ProjectElement;
        entity.Description = dto.Description;
        entity.CreatedDate = dto.CreatedDate;
        entity.CreatedDateUtc = dto.CreatedDate?.UtcDateTime;
        entity.ClosedDate = dto.ClosedDate;
        entity.Severity = dto.Severity;
        entity.Tags = dto.Tags;
        entity.IsBlocked = dto.IsBlocked;
        entity.Relations = dto.Relations != null ? System.Text.Json.JsonSerializer.Serialize(dto.Relations) : null;
        var changedDate = dto.ChangedDate ?? dto.RetrievedAt;
        entity.TfsChangedDate = changedDate;
        entity.TfsChangedDateUtc = changedDate.UtcDateTime;
        entity.BacklogPriority = dto.BacklogPriority;
    }

    private static WorkItemEntity MapToEntity(WorkItemDto dto)
    {
        return new WorkItemEntity
        {
            TfsId = dto.TfsId,
            ParentTfsId = dto.ParentTfsId,
            Type = dto.Type,
            Title = dto.Title,
            AreaPath = dto.AreaPath,
            IterationPath = dto.IterationPath,
            State = dto.State,
            RetrievedAt = dto.RetrievedAt,
            Effort = dto.Effort,
            BusinessValue = dto.BusinessValue,
            TimeCriticality = dto.TimeCriticality,
            ProjectNumber = dto.ProjectNumber,
            ProjectElement = dto.ProjectElement,
            Description = dto.Description,
            CreatedDate = dto.CreatedDate,
            CreatedDateUtc = dto.CreatedDate?.UtcDateTime,
            ClosedDate = dto.ClosedDate,
            Severity = dto.Severity,
            Tags = dto.Tags,
            IsBlocked = dto.IsBlocked,
            Relations = dto.Relations != null ? System.Text.Json.JsonSerializer.Serialize(dto.Relations) : null,
            TfsChangedDate = dto.ChangedDate ?? dto.RetrievedAt,
            TfsChangedDateUtc = (dto.ChangedDate ?? dto.RetrievedAt).UtcDateTime,
            BacklogPriority = dto.BacklogPriority
        };
    }

    private async Task<IncrementalPlanExecution> BuildIncrementalSyncPlanAsync(
        SyncContext context,
        IReadOnlyList<WorkItemDto> workItems,
        CancellationToken cancellationToken)
    {
        var previousFacts = await LoadPreviousGraphFactsAsync(context.ProductOwnerId, cancellationToken);
        var currentFacts = BuildCurrentGraphFacts(context.RootWorkItemIds, context.WorkItemWatermark, workItems);

        var request = new IncrementalSyncPlannerRequest
        {
            RootIds = context.RootWorkItemIds.OrderBy(id => id).ToArray(),
            PreviousAnalyticalScopeIds = previousFacts.PreviousAnalyticalScopeIds,
            PreviousClosureScopeIds = previousFacts.PreviousClosureScopeIds,
            PreviousParentById = previousFacts.PreviousParentById,
            CurrentAnalyticalScopeIds = currentFacts.CurrentAnalyticalScopeIds,
            CurrentClosureScopeIds = currentFacts.CurrentClosureScopeIds,
            CurrentParentById = currentFacts.CurrentParentById,
            ChangedIdsSinceWatermark = currentFacts.ChangedIdsSinceWatermark,
            ForceFullHydration = !context.WorkItemWatermark.HasValue
        };

        var plan = _incrementalSyncPlanner.Plan(request);
        LogResolvedOutsideClosureWarning(context, previousFacts, plan);
        return new IncrementalPlanExecution(plan, previousFacts.ProductIds);
    }

    private void LogIncrementalSyncPlan(SyncContext context, IncrementalPlanExecution execution)
    {
        var plan = execution.Plan;

        _logger.LogInformation(
            "INCREMENTAL_SYNC_PLAN: ProductOwnerId={ProductOwnerId}, ProductIds=[{ProductIds}], SyncRunId={SyncRunId}, PlanningMode={PlanningMode}, AnalyticalScopeIds={AnalyticalScopeCount}, ClosureScopeIds={ClosureScopeCount}, EnteredAnalyticalScopeIds={EnteredAnalyticalScopeSummary}, LeftAnalyticalScopeIds={LeftAnalyticalScopeSummary}, IdsToHydrate={IdsToHydrateSummary}, HierarchyChangedIds={HierarchyChangedSummary}, RequiresRelationshipSnapshotRebuild={RequiresRelationshipSnapshotRebuild}, RequiresResolutionRebuild={RequiresResolutionRebuild}, RequiresProjectionRefresh={RequiresProjectionRefresh}, ReasonCodes=[{ReasonCodes}]",
            context.ProductOwnerId,
            string.Join(", ", execution.ProductIds),
            (string?)null,
            plan.PlanningMode,
            plan.AnalyticalScopeIds.Count,
            plan.ClosureScopeIds.Count,
            BuildIdSetSummary(plan.EnteredAnalyticalScopeIds),
            BuildIdSetSummary(plan.LeftAnalyticalScopeIds),
            BuildIdSetSummary(plan.IdsToHydrate),
            BuildIdSetSummary(plan.HierarchyChangedIds),
            plan.RequiresRelationshipSnapshotRebuild,
            plan.RequiresResolutionRebuild,
            plan.RequiresProjectionRefresh,
            string.Join(", ", plan.ReasonCodes));

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "INCREMENTAL_SYNC_PLAN_DEBUG: ProductOwnerId={ProductOwnerId}, ProductIds=[{ProductIds}], SyncRunId={SyncRunId}, IdsToHydrate=[{IdsToHydrate}], HierarchyChangedIds=[{HierarchyChangedIds}]",
                context.ProductOwnerId,
                string.Join(", ", execution.ProductIds),
                (string?)null,
                BuildFullIdList(plan.IdsToHydrate),
                BuildFullIdList(plan.HierarchyChangedIds));
        }
    }

    private void LogResolvedOutsideClosureWarning(
        SyncContext context,
        PreviousGraphFacts previousFacts,
        IncrementalSyncPlan plan)
    {
        var closureScope = plan.ClosureScopeIds.ToHashSet();
        var resolvedOutsideClosure = previousFacts.ResolvedWorkItemIds
            .Where(id => !closureScope.Contains(id))
            .OrderBy(id => id)
            .ToArray();

        if (resolvedOutsideClosure.Length == 0)
        {
            return;
        }

        _logger.LogWarning(
            "INCREMENTAL_SYNC_PLAN_VALIDATION: ProductOwnerId={ProductOwnerId}, ProductIds=[{ProductIds}], SyncRunId={SyncRunId}, ResolvedWorkItemsOutsideClosureScope={ResolvedOutsideClosureSummary}",
            context.ProductOwnerId,
            string.Join(", ", previousFacts.ProductIds),
            (string?)null,
            BuildIdSetSummary(resolvedOutsideClosure));
    }

    private void LogIncrementalSyncPlanSkipped(
        SyncContext context,
        string reason)
    {
        _logger.LogWarning(
            "INCREMENTAL_SYNC_PLAN_SKIPPED: ProductOwnerId={ProductOwnerId}, SyncRunId={SyncRunId}, Reason={Reason}",
            context.ProductOwnerId,
            (string?)null,
            reason);
    }

    private async Task<PreviousGraphFacts> LoadPreviousGraphFactsAsync(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var productIds = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .OrderBy(id => id)
            .ToArrayAsync(cancellationToken);

        var resolvedWorkItemIds = productIds.Length == 0
            ? []
            : await _context.ResolvedWorkItems
                .AsNoTracking()
                .Where(item => item.ResolvedProductId.HasValue && productIds.Contains(item.ResolvedProductId.Value))
                .Select(item => item.WorkItemId)
                .Distinct()
                .OrderBy(id => id)
                .ToArrayAsync(cancellationToken);

        var persistedParentRows = await _context.WorkItems
            .AsNoTracking()
            .Select(item => new { item.TfsId, item.ParentTfsId })
            .OrderBy(item => item.TfsId)
            .ToListAsync(cancellationToken);

        var persistedParents = persistedParentRows
            .Select(item => new PersistedParentNode(item.TfsId, item.ParentTfsId))
            .ToArray();

        var parentById = persistedParents.ToDictionary(item => item.TfsId, item => item.ParentTfsId);
        var previousClosureScopeIds = ExpandClosureScope(resolvedWorkItemIds, parentById);
        var previousParentById = previousClosureScopeIds.ToDictionary(id => id, id => parentById.GetValueOrDefault(id));

        return new PreviousGraphFacts(
            productIds,
            resolvedWorkItemIds,
            previousClosureScopeIds,
            previousParentById,
            resolvedWorkItemIds);
    }

    private static CurrentGraphFacts BuildCurrentGraphFacts(
        IReadOnlyList<int> rootWorkItemIds,
        DateTimeOffset? workItemWatermark,
        IReadOnlyList<WorkItemDto> workItems)
    {
        var currentParentById = workItems
            .GroupBy(item => item.TfsId)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Last().ParentTfsId);

        var currentClosureScopeIds = currentParentById.Keys.OrderBy(id => id).ToArray();
        var currentAnalyticalScopeIds = TraverseAnalyticalScope(rootWorkItemIds, currentParentById);
        var changedIdsSinceWatermark = workItemWatermark.HasValue
            ? workItems
                .Where(item => (item.ChangedDate ?? item.RetrievedAt) > workItemWatermark.Value)
                .Select(item => item.TfsId)
                .Distinct()
                .OrderBy(id => id)
                .ToArray()
            : [];

        return new CurrentGraphFacts(
            currentAnalyticalScopeIds,
            currentClosureScopeIds,
            currentParentById,
            changedIdsSinceWatermark);
    }

    private static int[] TraverseAnalyticalScope(
        IEnumerable<int> rootWorkItemIds,
        IReadOnlyDictionary<int, int?> parentById)
    {
        var childrenByParent = parentById
            .Where(entry => entry.Value.HasValue)
            .GroupBy(entry => entry.Value!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Key).OrderBy(id => id).ToArray());

        var visited = new SortedSet<int>();
        var stack = new Stack<int>(rootWorkItemIds.OrderByDescending(id => id));

        while (stack.Count > 0)
        {
            var currentId = stack.Pop();
            if (!parentById.ContainsKey(currentId) || !visited.Add(currentId))
            {
                continue;
            }

            if (childrenByParent.TryGetValue(currentId, out var children))
            {
                for (var index = children.Length - 1; index >= 0; index--)
                {
                    stack.Push(children[index]);
                }
            }
        }

        return visited.ToArray();
    }

    private static int[] ExpandClosureScope(
        IEnumerable<int> analyticalScopeIds,
        IReadOnlyDictionary<int, int?> parentById)
    {
        var closure = new SortedSet<int>(analyticalScopeIds);

        foreach (var workItemId in analyticalScopeIds)
        {
            var visited = new HashSet<int> { workItemId };
            var currentId = workItemId;

            while (parentById.TryGetValue(currentId, out var parentId) && parentId.HasValue && visited.Add(parentId.Value))
            {
                closure.Add(parentId.Value);
                currentId = parentId.Value;
            }
        }

        return closure.ToArray();
    }

    private static string BuildIdSetSummary(IReadOnlyCollection<int> ids)
    {
        var sample = ids.Take(LoggedIdSampleSize).ToArray();
        var truncatedSuffix = ids.Count > sample.Length ? ", truncated=true" : string.Empty;
        return $"count={ids.Count}, sample=[{string.Join(", ", sample)}]{truncatedSuffix}";
    }

    private static string BuildFullIdList(IReadOnlyCollection<int> ids)
    {
        return string.Join(", ", ids);
    }

    private sealed record PersistedParentNode(int TfsId, int? ParentTfsId);

    private sealed record PreviousGraphFacts(
        int[] ProductIds,
        int[] PreviousAnalyticalScopeIds,
        int[] PreviousClosureScopeIds,
        IReadOnlyDictionary<int, int?> PreviousParentById,
        int[] ResolvedWorkItemIds);

    private sealed record CurrentGraphFacts(
        int[] CurrentAnalyticalScopeIds,
        int[] CurrentClosureScopeIds,
        IReadOnlyDictionary<int, int?> CurrentParentById,
        int[] ChangedIdsSinceWatermark);

    private sealed record IncrementalPlanExecution(
        IncrementalSyncPlan Plan,
        int[] ProductIds);
}
