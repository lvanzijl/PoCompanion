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
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        _logger.LogInformation("Computing sprint metrics for ProductOwner {ProductOwnerId}", productOwnerId);

        // Get products for this ProductOwner
        var products = await context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found for ProductOwner {ProductOwnerId}", productOwnerId);
            return Array.Empty<SprintMetricsProjectionEntity>();
        }

        var productIds = products.Select(p => p.Id).ToList();

        // Get sprints
        var sprints = await context.Sprints
            .Where(s => sprintIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        // Get state classifications for activity detection
        var classifications = await GetStateClassificationsAsync(context, cancellationToken);

        // Get resolved work items that belong to these products
        var resolvedItems = await context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId != null && productIds.Contains(r.ResolvedProductId.Value))
            .Where(r => r.ResolutionStatus == ResolutionStatus.Resolved)
            .ToListAsync(cancellationToken);

        var resolvedWorkItemIds = resolvedItems.Select(r => r.WorkItemId).ToHashSet();

        // Get latest revisions for resolved items
        var latestRevisions = await context.RevisionHeaders
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .GroupBy(h => h.WorkItemId)
            .Select(g => g.OrderByDescending(h => h.RevisionNumber).First())
            .ToListAsync(cancellationToken);

        var revisionsByWorkItem = latestRevisions.ToDictionary(r => r.WorkItemId, r => r);

        // Build product mapping
        var productByWorkItem = resolvedItems.ToDictionary(r => r.WorkItemId, r => r.ResolvedProductId!.Value);

        var projections = new List<SprintMetricsProjectionEntity>();

        foreach (var sprint in sprints)
        {
            foreach (var product in products)
            {
                var projection = await ComputeSprintProductMetricsAsync(
                    context,
                    sprint,
                    product,
                    resolvedWorkItemIds,
                    revisionsByWorkItem,
                    productByWorkItem,
                    classifications,
                    cancellationToken);

                projections.Add(projection);

                // Upsert projection
                var existing = await context.SprintMetricsProjections
                    .FirstOrDefaultAsync(p => p.SprintId == sprint.Id && p.ProductId == product.Id, cancellationToken);

                if (existing != null)
                {
                    existing.PlannedCount = projection.PlannedCount;
                    existing.PlannedEffort = projection.PlannedEffort;
                    existing.WorkedCount = projection.WorkedCount;
                    existing.WorkedEffort = projection.WorkedEffort;
                    existing.BugsPlannedCount = projection.BugsPlannedCount;
                    existing.BugsWorkedCount = projection.BugsWorkedCount;
                    existing.LastComputedAt = DateTimeOffset.UtcNow;
                    existing.IncludedUpToRevisionId = projection.IncludedUpToRevisionId;
                }
                else
                {
                    context.SprintMetricsProjections.Add(projection);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Computed {Count} sprint-product projections for ProductOwner {ProductOwnerId}",
            projections.Count, productOwnerId);

        return projections;
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

        return await context.SprintMetricsProjections
            .Where(p => sprintIds.Contains(p.SprintId) && productIds.Contains(p.ProductId))
            .Include(p => p.Sprint)
            .Include(p => p.Product)
            .ToListAsync(cancellationToken);
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
        int plannedCount = 0, plannedEffort = 0;
        int workedCount = 0, workedEffort = 0;
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
                plannedEffort += revision.Effort ?? 0;
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
                workedEffort += revision.Effort ?? 0;
            }
        }

        return new SprintMetricsProjectionEntity
        {
            SprintId = sprint.Id,
            ProductId = product.Id,
            PlannedCount = plannedCount,
            PlannedEffort = plannedEffort,
            WorkedCount = workedCount,
            WorkedEffort = workedEffort,
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

        // Get all revisions in the sprint date range
        var sprintRevisions = await context.RevisionHeaders
            .Include(h => h.FieldDeltas)
            .Where(h => h.ChangedDate >= sprintStart && h.ChangedDate <= sprintEnd)
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .ToListAsync(cancellationToken);

        // Also check for late completions: Done while IterationPath == Sprint
        // (attributed to sprint even if change happens later)
        var lateCompletions = await context.RevisionHeaders
            .Include(h => h.FieldDeltas)
            .Where(h => h.IterationPath == sprint.Path)
            .Where(h => resolvedWorkItemIds.Contains(h.WorkItemId))
            .Where(h => h.FieldDeltas.Any(fd => fd.FieldName == "System.State"))
            .ToListAsync(cancellationToken);

        var allRevisionsToCheck = sprintRevisions.Union(lateCompletions).ToList();

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
    /// Made internal for testability.
    /// </summary>
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
