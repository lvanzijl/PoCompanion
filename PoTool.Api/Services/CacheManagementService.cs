using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for cache insights, granular reset, and revision validation.
/// </summary>
public class CacheManagementService
{
    private readonly PoToolDbContext _context;
    private readonly ILogger<CacheManagementService> _logger;

    public CacheManagementService(PoToolDbContext context, ILogger<CacheManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets cache insights with counts per entity type.
    /// </summary>
    public async Task<CacheInsightsDto> GetInsightsAsync(int productOwnerId, CancellationToken cancellationToken)
    {
        var cacheState = await _context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var counts = new List<CacheEntityCountDto>
        {
            new() { EntityType = CacheEntityTypes.WorkItems, TotalCount = await _context.WorkItems.CountAsync(cancellationToken) },
            new() { EntityType = CacheEntityTypes.Revisions, TotalCount = await _context.RevisionHeaders.CountAsync(cancellationToken) },
            new()
            {
                EntityType = CacheEntityTypes.PullRequests,
                TotalCount = await _context.PullRequests
                    .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Pipelines,
                TotalCount = await _context.CachedPipelineRuns
                    .Where(r => r.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Metrics,
                TotalCount = await _context.CachedMetrics
                    .Where(m => m.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            },
            new() { EntityType = CacheEntityTypes.Validations, TotalCount = await _context.CachedValidationResults.CountAsync(cancellationToken) },
            new()
            {
                EntityType = CacheEntityTypes.SprintProjections,
                TotalCount = await _context.SprintMetricsProjections
                    .Where(s => productIds.Contains(s.ProductId))
                    .CountAsync(cancellationToken)
            },
            new()
            {
                EntityType = CacheEntityTypes.Relationships,
                TotalCount = await _context.WorkItemRelationshipEdges
                    .Where(e => e.ProductOwnerId == productOwnerId)
                    .CountAsync(cancellationToken)
            }
        };

        return new CacheInsightsDto
        {
            ProductOwnerId = productOwnerId,
            EntityCounts = counts,
            SyncStatus = cacheState != null ? (CacheSyncStatusDto)cacheState.SyncStatus : CacheSyncStatusDto.Idle,
            LastSuccessfulSync = cacheState?.LastSuccessfulSync,
            LastAttemptSync = cacheState?.LastAttemptSync,
            LastErrorMessage = cacheState?.LastErrorMessage,
            CurrentSyncStage = cacheState?.CurrentSyncStage
        };
    }

    /// <summary>
    /// Resets specific cache entity types.
    /// </summary>
    public async Task<CacheResetResponse> ResetSelectiveAsync(
        int productOwnerId,
        CacheResetRequest request,
        CancellationToken cancellationToken)
    {
        var typesToReset = request.EntityTypes;
        if (typesToReset == null || typesToReset.Count == 0)
        {
            typesToReset = CacheEntityTypes.All.ToList();
        }

        var productIds = await _context.Products
            .Where(p => p.ProductOwnerId == productOwnerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var deletedCounts = new List<CacheEntityCountDto>();

        foreach (var entityType in typesToReset)
        {
            var count = await ResetEntityTypeAsync(productOwnerId, entityType, productIds, cancellationToken);
            deletedCounts.Add(new CacheEntityCountDto { EntityType = entityType, TotalCount = count });
            _logger.LogInformation("Reset {EntityType}: deleted {Count} records for ProductOwner {ProductOwnerId}",
                entityType, count, productOwnerId);
        }

        // If all types are reset, also reset the cache state
        if (typesToReset.Count == CacheEntityTypes.All.Count)
        {
            var cacheState = await _context.ProductOwnerCacheStates
                .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);
            if (cacheState != null)
            {
                cacheState.SyncStatus = CacheSyncStatus.Idle;
                cacheState.LastAttemptSync = null;
                cacheState.LastSuccessfulSync = null;
                cacheState.WorkItemCount = 0;
                cacheState.PullRequestCount = 0;
                cacheState.PipelineCount = 0;
                cacheState.WorkItemWatermark = null;
                cacheState.PullRequestWatermark = null;
                cacheState.PipelineWatermark = null;
                cacheState.LastErrorMessage = null;
                cacheState.CurrentSyncStage = null;
                cacheState.StageProgressPercent = 0;
                cacheState.RelationshipsSnapshotAsOfUtc = null;
                cacheState.RelationshipsSnapshotWorkItemWatermark = null;
                cacheState.ResolutionAsOfUtc = null;
                cacheState.SprintTrendProjectionAsOfUtc = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return new CacheResetResponse
        {
            Success = true,
            Message = $"Reset {typesToReset.Count} entity type(s) for ProductOwner {productOwnerId}",
            DeletedCounts = deletedCounts
        };
    }

    private async Task<int> ResetEntityTypeAsync(
        int productOwnerId,
        string entityType,
        List<int> productIds,
        CancellationToken cancellationToken)
    {
        return entityType switch
        {
            CacheEntityTypes.WorkItems => await DeleteWorkItemsAsync(cancellationToken),
            CacheEntityTypes.Revisions => await DeleteRevisionsAsync(productOwnerId, cancellationToken),
            CacheEntityTypes.PullRequests => await DeletePullRequestsAsync(productIds, cancellationToken),
            CacheEntityTypes.Pipelines => await DeletePipelinesAsync(productOwnerId, cancellationToken),
            CacheEntityTypes.Metrics => await DeleteMetricsAsync(productOwnerId, cancellationToken),
            CacheEntityTypes.Validations => await DeleteValidationsAsync(cancellationToken),
            CacheEntityTypes.SprintProjections => await DeleteSprintProjectionsAsync(productIds, cancellationToken),
            CacheEntityTypes.Relationships => await DeleteRelationshipsAsync(productOwnerId, cancellationToken),
            _ => 0
        };
    }

    private async Task<int> DeleteWorkItemsAsync(CancellationToken ct)
    {
        var count = await _context.WorkItems.CountAsync(ct);
        await _context.WorkItems.ExecuteDeleteAsync(ct);
        await _context.Sprints.ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteRevisionsAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.RevisionHeaders.CountAsync(ct);
        await _context.RevisionFieldDeltas.ExecuteDeleteAsync(ct);
        await _context.RevisionRelationDeltas.ExecuteDeleteAsync(ct);
        await _context.RevisionHeaders.ExecuteDeleteAsync(ct);
        await _context.RevisionIngestionWatermarks
            .Where(w => w.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeletePullRequestsAsync(List<int> productIds, CancellationToken ct)
    {
        var pullRequestIds = await _context.PullRequests
            .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
            .Select(pr => pr.Id)
            .ToListAsync(ct);

        var count = pullRequestIds.Count;
        if (count > 0)
        {
            await _context.PullRequestFileChanges
                .Where(c => pullRequestIds.Contains(c.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequestComments
                .Where(c => pullRequestIds.Contains(c.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequestIterations
                .Where(i => pullRequestIds.Contains(i.PullRequestId))
                .ExecuteDeleteAsync(ct);
            await _context.PullRequests
                .Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value))
                .ExecuteDeleteAsync(ct);
        }
        return count;
    }

    private async Task<int> DeletePipelinesAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.CachedPipelineRuns
            .Where(r => r.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteMetricsAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.CachedMetrics
            .Where(m => m.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.CachedMetrics
            .Where(m => m.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteValidationsAsync(CancellationToken ct)
    {
        var count = await _context.CachedValidationResults.CountAsync(ct);
        await _context.CachedValidationResults.ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteSprintProjectionsAsync(List<int> productIds, CancellationToken ct)
    {
        var count = await _context.SprintMetricsProjections
            .Where(s => productIds.Contains(s.ProductId))
            .CountAsync(ct);
        await _context.SprintMetricsProjections
            .Where(s => productIds.Contains(s.ProductId))
            .ExecuteDeleteAsync(ct);
        await _context.ResolvedWorkItems
            .Where(r => r.ResolvedProductId.HasValue && productIds.Contains(r.ResolvedProductId.Value))
            .ExecuteDeleteAsync(ct);
        return count;
    }

    private async Task<int> DeleteRelationshipsAsync(int productOwnerId, CancellationToken ct)
    {
        var count = await _context.WorkItemRelationshipEdges
            .Where(e => e.ProductOwnerId == productOwnerId)
            .CountAsync(ct);
        await _context.WorkItemRelationshipEdges
            .Where(e => e.ProductOwnerId == productOwnerId)
            .ExecuteDeleteAsync(ct);
        return count;
    }

    /// <summary>
    /// Validates revision cache by replaying revisions and comparing against cached work item state.
    /// </summary>
    public async Task<RevisionValidationReport> ValidateRevisionsAsync(
        int productOwnerId,
        RevisionValidationRequest request,
        CancellationToken cancellationToken)
    {
        var report = new RevisionValidationReport
        {
            ValidatedAt = DateTimeOffset.UtcNow,
            Mode = request.Mode,
            ComparedFields = RevisionFieldWhitelist.Fields.ToList()
        };

        var workItemIds = await GetWorkItemIdsForValidation(request, cancellationToken);
        if (workItemIds.Count == 0)
        {
            return report;
        }

        foreach (var workItemId in workItemIds)
        {
            var result = await ValidateSingleWorkItemAsync(workItemId, request.Mode == "single", cancellationToken);
            report.Results.Add(result);
        }

        report.TotalValidated = report.Results.Count;
        report.Passed = report.Results.Count(r => r.IsValid);
        report.Failed = report.Results.Count(r => !r.IsValid);

        _logger.LogInformation(
            "Revision validation completed: {Total} validated, {Passed} passed, {Failed} failed",
            report.TotalValidated, report.Passed, report.Failed);

        return report;
    }

    private async Task<List<int>> GetWorkItemIdsForValidation(
        RevisionValidationRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Mode)
        {
            case "single":
                if (!request.WorkItemId.HasValue)
                    return new List<int>();
                // Verify the work item has revisions
                var hasRevisions = await _context.RevisionHeaders
                    .AnyAsync(r => r.WorkItemId == request.WorkItemId.Value, cancellationToken);
                return hasRevisions ? new List<int> { request.WorkItemId.Value } : new List<int>();

            case "sample":
                return await _context.RevisionHeaders
                    .Select(r => r.WorkItemId)
                    .Distinct()
                    .OrderBy(id => EF.Functions.Random())
                    .Take(Math.Min(request.SampleSize, 50))
                    .ToListAsync(cancellationToken);

            case "recent":
                // SQLite cannot apply Max() on DateTimeOffset in queries, so we need client-side evaluation
                // Note: This loads all revision headers (WorkItemId, ChangedDate only) into memory.
                // For typical datasets this is acceptable since validation is an administrative operation.
                // ProjectOnly two fields to minimize memory footprint.
                var allRevisions = await _context.RevisionHeaders
                    .Select(r => new { r.WorkItemId, r.ChangedDate })
                    .ToListAsync(cancellationToken);
                
                return allRevisions
                    .GroupBy(r => r.WorkItemId)
                    .Select(g => new { WorkItemId = g.Key, MaxChanged = g.Max(r => r.ChangedDate) })
                    .OrderByDescending(x => x.MaxChanged)
                    .Take(Math.Min(request.SampleSize, 50))
                    .Select(x => x.WorkItemId)
                    .ToList();

            default:
                return new List<int>();
        }
    }

    /// <summary>
    /// Validates a single work item by replaying its revisions and comparing to cached state.
    /// This is a public static method for testability.
    /// </summary>
    internal async Task<WorkItemValidationResult> ValidateSingleWorkItemAsync(
        int workItemId,
        bool includeTimeline,
        CancellationToken cancellationToken)
    {
        var result = new WorkItemValidationResult { WorkItemId = workItemId };

        try
        {
            // Get all revisions for this work item ordered by revision number
            IQueryable<RevisionHeaderEntity> revisionsQuery = _context.RevisionHeaders
                .Where(r => r.WorkItemId == workItemId);

            if (includeTimeline)
            {
                revisionsQuery = revisionsQuery.Include(r => r.FieldDeltas);
            }

            var revisions = await revisionsQuery
                .OrderBy(r => r.RevisionNumber)
                .ToListAsync(cancellationToken);

            if (revisions.Count == 0)
            {
                result.ErrorMessage = "No revisions found in cache";
                result.IsValid = false;
                return result;
            }

            // The last revision represents the final replayed state
            var lastRevision = revisions[^1];
            result.LastRevisionNumber = lastRevision.RevisionNumber;
            result.RevisionCount = revisions.Count;

            // Get the cached work item
            var workItem = await _context.WorkItems
                .FirstOrDefaultAsync(w => w.TfsId == workItemId, cancellationToken);

            if (workItem == null)
            {
                result.ErrorMessage = "Work item not found in cache";
                result.IsValid = false;
                return result;
            }

            // Build replayed state from last revision header
            var replayedState = BuildReplayedState(lastRevision);

            // Build REST/cached state from work item entity
            var cachedState = BuildCachedWorkItemState(workItem);

            // Compare using whitelist fields
            result.Diffs = CompareStates(replayedState, cachedState);
            result.IsValid = result.Diffs.Count == 0;

            if (includeTimeline)
            {
                result.ChangeTimeline = BuildRevisionTimelineEntries(revisions);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Builds a dictionary of field values from the last revision header.
    /// Internal for testability via InternalsVisibleTo.
    /// </summary>
    internal static Dictionary<string, string?> BuildReplayedState(RevisionHeaderEntity revision)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = revision.WorkItemId.ToString(),
            ["System.WorkItemType"] = revision.WorkItemType,
            ["System.Title"] = revision.Title,
            ["System.State"] = revision.State,
            ["System.Reason"] = revision.Reason,
            ["System.IterationPath"] = revision.IterationPath,
            ["System.AreaPath"] = revision.AreaPath,
            ["System.CreatedDate"] = revision.CreatedDate?.ToString("o"),
            ["System.ChangedDate"] = revision.ChangedDate.ToString("o"),
            ["System.ChangedBy"] = revision.ChangedBy,
            ["Microsoft.VSTS.Common.ClosedDate"] = revision.ClosedDate?.ToString("o"),
            ["Microsoft.VSTS.Scheduling.Effort"] = revision.Effort?.ToString(),
            ["System.Tags"] = revision.Tags,
            ["Microsoft.VSTS.Common.Severity"] = revision.Severity
        };
    }

    /// <summary>
    /// Builds a dictionary of field values from a cached work item entity.
    /// Internal for testability via InternalsVisibleTo.
    /// </summary>
    internal static Dictionary<string, string?> BuildCachedWorkItemState(WorkItemEntity workItem)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["System.Id"] = workItem.TfsId.ToString(),
            ["System.WorkItemType"] = workItem.Type,
            ["System.Title"] = workItem.Title,
            ["System.State"] = workItem.State,
            ["System.Reason"] = null, // WorkItemEntity does not store Reason
            ["System.IterationPath"] = workItem.IterationPath,
            ["System.AreaPath"] = workItem.AreaPath,
            ["System.CreatedDate"] = workItem.CreatedDate?.ToString("o"),
            ["System.ChangedDate"] = workItem.TfsChangedDate.ToString("o"),
            ["System.ChangedBy"] = null, // WorkItemEntity does not store ChangedBy
            ["Microsoft.VSTS.Common.ClosedDate"] = workItem.ClosedDate?.ToString("o"),
            ["Microsoft.VSTS.Scheduling.Effort"] = workItem.Effort?.ToString(),
            ["System.Tags"] = workItem.Tags,
            ["Microsoft.VSTS.Common.Severity"] = workItem.Severity
        };
    }

    /// <summary>
    /// Compares two field state dictionaries and returns diffs.
    /// Normalizes null/empty to treat them equivalently.
    /// Internal for testability via InternalsVisibleTo.
    /// </summary>
    internal static List<FieldDiffDto> CompareStates(
        Dictionary<string, string?> replayedState,
        Dictionary<string, string?> cachedState)
    {
        var diffs = new List<FieldDiffDto>();

        // Only compare fields in the whitelist
        foreach (var field in RevisionFieldWhitelist.Fields)
        {
            // Skip fields that aren't stored in WorkItemEntity
            if (field is "System.Reason" or "System.ChangedBy")
                continue;

            var replayed = Normalize(replayedState.GetValueOrDefault(field));
            var cached = Normalize(cachedState.GetValueOrDefault(field));

            if (!string.Equals(replayed, cached, StringComparison.Ordinal))
            {
                diffs.Add(new FieldDiffDto
                {
                    FieldName = field,
                    ReplayedValue = replayed,
                    RestValue = cached
                });
            }
        }

        return diffs;
    }

    internal static List<RevisionTimelineEntryDto> BuildRevisionTimelineEntries(List<RevisionHeaderEntity> revisions)
    {
        var orderedRevisions = revisions
            .OrderBy(r => r.RevisionNumber)
            .ToList();

        var timeline = orderedRevisions
            .SelectMany(r => r.FieldDeltas
                .OrderBy(d => d.Id)
                .Select(d => new RevisionTimelineEntryDto
                {
                    RevisionNumber = r.RevisionNumber,
                    ChangedDate = r.ChangedDate,
                    ChangedBy = r.ChangedBy,
                    FieldName = d.FieldName,
                    OldValue = Normalize(d.OldValue),
                    NewValue = Normalize(d.NewValue)
                }))
            .ToList();

        if (timeline.Count > 0)
        {
            return timeline;
        }

        Dictionary<string, string?>? previousRevisionState = null;
        foreach (var revision in orderedRevisions)
        {
            var currentState = BuildReplayedState(revision);

            foreach (var field in RevisionFieldWhitelist.Fields)
            {
                var previousValue = Normalize(previousRevisionState?.GetValueOrDefault(field));
                var currentValue = Normalize(currentState.GetValueOrDefault(field));

                if (previousRevisionState == null && currentValue == null)
                {
                    continue;
                }
                if (previousRevisionState != null && string.Equals(previousValue, currentValue, StringComparison.Ordinal))
                {
                    continue;
                }

                timeline.Add(new RevisionTimelineEntryDto
                {
                    RevisionNumber = revision.RevisionNumber,
                    ChangedDate = revision.ChangedDate,
                    ChangedBy = revision.ChangedBy,
                    FieldName = field,
                    OldValue = previousValue,
                    NewValue = currentValue
                });
            }

            previousRevisionState = currentState;
        }

        return timeline;
    }

    /// <summary>
    /// Normalizes a field value: null and empty string → null.
    /// </summary>
    internal static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
