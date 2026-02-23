using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for computing Sprint Trend projections from cached revision data.
/// Computes planned work, worked work, and progress metrics per sprint and product.
/// </summary>
public class SprintTrendProjectionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SprintTrendProjectionService> _logger;

    // Work item type constants to avoid magic strings
    private const string WorkItemTypeTask = "Task";
    private const string WorkItemTypePbi = "Product Backlog Item";
    private const string WorkItemTypePbiShort = "PBI";
    private const string WorkItemTypeBug = "Bug";
    private const string WorkItemTypeEpic = "Epic";
    private const string WorkItemTypeFeature = "Feature";

    public SprintTrendProjectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SprintTrendProjectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Computes sprint metrics projections for all sprints in a given range.
    /// </summary>
    /// <param name="productOwnerId">The ProductOwner ID.</param>
    /// <param name="sprintIds">The sprint IDs to compute metrics for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed projections.</returns>
    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        _ = productOwnerId;
        _ = sprintIds;
        _ = cancellationToken;
        // REPLACE_WITH_ACTIVITY_SOURCE: compute sprint trend metrics from activity events.
        return Array.Empty<SprintMetricsProjectionEntity>();
    }

    /// <summary>
    /// Gets pre-computed sprint metrics projections for a sprint range.
    /// </summary>
    public virtual async Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        // Get products for this ProductOwner
        var productIds = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Looking up cached sprint trend projections for ProductOwner {ProductOwnerId}. ProductCount={ProductCount}.",
            productOwnerId, productIds.Count);

        var sprintIdList = sprintIds.Distinct().ToList();

        var projections = await context.SprintMetricsProjections
            .Where(p => sprintIdList.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .Include(p => p.Sprint)
            .Include(p => p.Product)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {ProjectionCount} cached sprint trend projections for ProductOwner {ProductOwnerId} across {SprintCount} requested sprints.",
            projections.Count, productOwnerId, sprintIdList.Count);

        return projections;
    }

    private async Task<SprintMetricsProjectionEntity> ComputeSprintProductMetricsAsync(
        PoToolDbContext context,
        SprintEntity sprint,
        ProductEntity product,
        HashSet<int> resolvedWorkItemIds,
        Dictionary<int, RevisionHeaderEntity> revisionsByWorkItem,
        Dictionary<int, int> productByWorkItem,
        Dictionary<string, StateClassification> classifications,
        CancellationToken cancellationToken)
    {
        // Get work items that were ever assigned to this sprint (via iteration path)
        // A work item is "planned" if it ever had IterationPath == Sprint.Path
        var plannedWorkItems = await GetPlannedWorkItemsAsync(context, sprint, product.Id, resolvedWorkItemIds, productByWorkItem, cancellationToken);

        // Get work items that had qualifying activity in this sprint
        var workedWorkItems = await GetWorkedWorkItemsAsync(context, sprint, product.Id, resolvedWorkItemIds, productByWorkItem, classifications, cancellationToken);

        // Calculate metrics
        int plannedCount = 0;
        double plannedEffort = 0;
        int workedCount = 0;
        double workedEffort = 0;
        int bugsPlannedCount = 0, bugsWorkedCount = 0;
        int maxRevisionId = 0;

        foreach (var workItemId in plannedWorkItems)
        {
            if (!revisionsByWorkItem.TryGetValue(workItemId, out var revision))
            {
                continue;
            }

            maxRevisionId = Math.Max(maxRevisionId, revision.Id);

            if (revision.WorkItemType.Equals(WorkItemTypeBug, StringComparison.OrdinalIgnoreCase))
            {
                bugsPlannedCount++;
            }
            else
            {
                plannedCount++;
                plannedEffort += revision.Effort ?? 0d;
            }
        }

        foreach (var workItemId in workedWorkItems)
        {
            if (!revisionsByWorkItem.TryGetValue(workItemId, out var revision))
            {
                continue;
            }

            if (revision.WorkItemType.Equals(WorkItemTypeBug, StringComparison.OrdinalIgnoreCase))
            {
                bugsWorkedCount++;
            }
            else
            {
                workedCount++;
                workedEffort += revision.Effort ?? 0d;
            }
        }

        return new SprintMetricsProjectionEntity
        {
            SprintId = sprint.Id,
            ProductId = product.Id,
            PlannedCount = plannedCount,
            PlannedEffort = (int)Math.Round(plannedEffort),
            WorkedCount = workedCount,
            WorkedEffort = (int)Math.Round(workedEffort),
            BugsPlannedCount = bugsPlannedCount,
            BugsWorkedCount = bugsWorkedCount,
            LastComputedAt = DateTimeOffset.UtcNow,
            IncludedUpToRevisionId = maxRevisionId
        };
    }

    private async Task<HashSet<int>> GetPlannedWorkItemsAsync(
        PoToolDbContext context,
        SprintEntity sprint,
        int productId,
        HashSet<int> resolvedWorkItemIds,
        Dictionary<int, int> productByWorkItem,
        CancellationToken cancellationToken)
    {
        // A work item is planned for sprint S if it ever had IterationPath == Sprint.Path
        // We need to check all revisions to find items that were ever in this sprint
        var plannedItemIds = await context.RevisionHeaders
            .Where(h => h.IterationPath == sprint.Path)
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .Select(h => h.WorkItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Filter to items belonging to this product
        return plannedItemIds
            .Where(id => productByWorkItem.TryGetValue(id, out var pid) && pid == productId)
            .ToHashSet();
    }

    private async Task<HashSet<int>> GetWorkedWorkItemsAsync(
        PoToolDbContext context,
        SprintEntity sprint,
        int productId,
        HashSet<int> resolvedWorkItemIds,
        Dictionary<int, int> productByWorkItem,
        Dictionary<string, StateClassification> classifications,
        CancellationToken cancellationToken)
    {
        var workedItems = new HashSet<int>();

        // Get state change deltas that occurred during this sprint
        var sprintStart = sprint.StartUtc ?? DateTimeOffset.MinValue;
        var sprintEnd = sprint.EndUtc ?? DateTimeOffset.MaxValue;

        // SQLite may fail to translate DateTimeOffset range predicates in this query shape.
        // Materialize candidate revisions first, then apply the date filter in memory.
        // This intentionally trades some query selectivity for provider compatibility.
        var sprintRevisionCandidates = await context.RevisionHeaders
            .Include(h => h.FieldDeltas)
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .ToListAsync(cancellationToken);
        var sprintRevisions = sprintRevisionCandidates
            .Where(h => h.ChangedDate >= sprintStart && h.ChangedDate <= sprintEnd)
            .ToList();

        // Also check for late completions: Done while IterationPath == Sprint
        // (attributed to sprint even if change happens later)
        var lateCompletions = await context.RevisionHeaders
            .Include(h => h.FieldDeltas)
            .Where(h => h.IterationPath == sprint.Path)
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .Where(h => h.FieldDeltas.Any(fd => fd.FieldName == "System.State"))
            .ToListAsync(cancellationToken);

        var allRevisionsToCheck = sprintRevisions.Union(lateCompletions).ToList();

        _logger.LogDebug(
            "Worked item scan for SprintId {SprintId}, ProductId {ProductId}: SprintRevisionCandidates={CandidateCount}, SprintRevisionsInRange={InRangeCount}, LateCompletions={LateCompletionCount}, RevisionsChecked={CheckedCount}.",
            sprint.Id,
            productId,
            sprintRevisionCandidates.Count,
            sprintRevisions.Count,
            lateCompletions.Count,
            allRevisionsToCheck.Count);

        foreach (var revision in allRevisionsToCheck)
        {
            if (!productByWorkItem.TryGetValue(revision.WorkItemId, out var pid) || pid != productId)
            {
                continue;
            }

            var stateChange = revision.FieldDeltas?.FirstOrDefault(fd => fd.FieldName == "System.State");
            if (stateChange == null)
            {
                continue;
            }

            var oldClass = GetClassification(stateChange.OldValue, revision.WorkItemType, classifications);
            var newClass = GetClassification(stateChange.NewValue, revision.WorkItemType, classifications);

            // Apply activity detection rules per work item type
            if (IsQualifyingActivity(revision.WorkItemType, oldClass, newClass))
            {
                workedItems.Add(revision.WorkItemId);
            }
        }

        return workedItems;
    }

    /// <summary>
    /// Determines if a state change qualifies as work activity for sprint trend metrics.
    /// Made internal for testability. Hidden from IntelliSense in production code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal bool IsQualifyingActivity(
        string workItemType,
        StateClassification? oldClass,
        StateClassification? newClass)
    {
        if (oldClass == null || newClass == null || oldClass == newClass)
        {
            return false;
        }

        // Rules from specification:
        // - Tasks: Any state-category change counts as activity
        // - PBIs: New->InProgress does NOT count (commit), but InProgress->Done does
        // - Bugs: InProgress->Done counts, Done->InProgress (reopened) counts

        if (workItemType.Equals(WorkItemTypeTask, StringComparison.OrdinalIgnoreCase))
        {
            // Any state change counts
            return true;
        }

        if (workItemType.Equals(WorkItemTypePbi, StringComparison.OrdinalIgnoreCase) ||
            workItemType.Equals(WorkItemTypePbiShort, StringComparison.OrdinalIgnoreCase))
        {
            // New -> InProgress does NOT count (commit without activity)
            if (oldClass == StateClassification.New && newClass == StateClassification.InProgress)
            {
                return false;
            }

            // InProgress -> Done counts
            if (oldClass == StateClassification.InProgress && newClass == StateClassification.Done)
            {
                return true;
            }

            // Child task activity counts for PBI - handled separately by checking child tasks
            return false;
        }

        if (workItemType.Equals(WorkItemTypeBug, StringComparison.OrdinalIgnoreCase))
        {
            // InProgress -> Done counts
            if (oldClass == StateClassification.InProgress && newClass == StateClassification.Done)
            {
                return true;
            }

            // Done -> InProgress (reopened) counts
            if (oldClass == StateClassification.Done && newClass == StateClassification.InProgress)
            {
                return true;
            }

            return false;
        }

        // For other types (Feature, Epic), no direct activity tracking
        return false;
    }

    private StateClassification? GetClassification(
        string? state,
        string workItemType,
        Dictionary<string, StateClassification> classifications)
    {
        if (string.IsNullOrEmpty(state))
        {
            return null;
        }

        // Build key as "WorkItemType:State"
        var key = $"{workItemType}:{state}";
        if (classifications.TryGetValue(key, out var classification))
        {
            return classification;
        }

        // Try with just the state name (fallback for generic classification)
        if (classifications.TryGetValue(state, out classification))
        {
            return classification;
        }

        // Default mapping based on common Azure DevOps state names
        // These are fallbacks used when state classification data is not synced
        // Log a debug message to help identify missing database entries
        var fallback = state.ToLowerInvariant() switch
        {
            "new" or "proposed" => StateClassification.New,
            "active" or "committed" or "in progress" => StateClassification.InProgress,
            "done" or "closed" or "resolved" => StateClassification.Done,
            "removed" or "cancelled" => StateClassification.Removed,
            _ => (StateClassification?)null
        };

        if (fallback.HasValue)
        {
            _logger.LogDebug(
                "Using fallback state classification for '{State}' on type '{WorkItemType}' -> {Classification}. Consider syncing state classifications.",
                state, workItemType, fallback.Value);
        }

        return fallback;
    }

    private async Task<Dictionary<string, StateClassification>> GetStateClassificationsAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        var classifications = await context.WorkItemStateClassifications
            .ToListAsync(cancellationToken);

        // Build lookup with "WorkItemType:State" keys
        var lookup = new Dictionary<string, StateClassification>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in classifications)
        {
            var key = $"{c.WorkItemType}:{c.StateName}";
            lookup[key] = (StateClassification)c.Classification;
        }

        return lookup;
    }
}
