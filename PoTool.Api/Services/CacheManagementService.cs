using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using System.Text.Json;

namespace PoTool.Api.Services;

/// <summary>
/// Service for cache insights and granular reset operations.
/// </summary>
public class CacheManagementService
{
    private readonly PoToolDbContext _context;
    private readonly ISprintRepository _sprintRepository;
    private readonly ILogger<CacheManagementService> _logger;

    public CacheManagementService(
        PoToolDbContext context,
        ISprintRepository sprintRepository,
        ILogger<CacheManagementService> logger)
    {
        _context = context;
        _sprintRepository = sprintRepository;
        _logger = logger;
    }

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

    public async Task<ActivityLedgerValidationDto> GetActivityLedgerValidationAsync(
        int productOwnerId,
        int workItemId,
        DateTimeOffset? fromChangedDate,
        DateTimeOffset? toChangedDate,
        CancellationToken cancellationToken)
    {
        var baseQuery = _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e => e.ProductOwnerId == productOwnerId && e.WorkItemId == workItemId);

        if (fromChangedDate.HasValue)
        {
            var fromChangedDateUtc = fromChangedDate.Value.UtcDateTime;
            baseQuery = baseQuery.Where(e => e.EventTimestampUtc >= fromChangedDateUtc);
        }

        if (toChangedDate.HasValue)
        {
            var toChangedDateUtc = toChangedDate.Value.UtcDateTime;
            baseQuery = baseQuery.Where(e => e.EventTimestampUtc <= toChangedDateUtc);
        }

        var events = await baseQuery
            .OrderBy(e => e.EventTimestampUtc)
            .ThenBy(e => e.UpdateId)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var changedByByUpdateId = events
            .Where(e => string.Equals(e.FieldRefName, "System.ChangedBy", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.NewValue))
            .GroupBy(e => e.UpdateId)
            .ToDictionary(g => g.Key, g => ExtractDisplayNameOrOriginal(g.Last().NewValue), comparer: EqualityComparer<int>.Default);

        var workItem = await _context.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TfsId == workItemId, cancellationToken);

        var resolvedWorkItem = await _context.ResolvedWorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.WorkItemId == workItemId, cancellationToken);

        var currentParentId = workItem?.ParentTfsId;
        var currentFeatureId = resolvedWorkItem?.ResolvedFeatureId;
        var currentEpicId = resolvedWorkItem?.ResolvedEpicId;

        var titleLookupIds = new[] { currentParentId, currentFeatureId, currentEpicId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var titleLookup = titleLookupIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.WorkItems
                .AsNoTracking()
                .Where(w => titleLookupIds.Contains(w.TfsId))
                .ToDictionaryAsync(w => w.TfsId, w => w.Title, cancellationToken);

        var assignedTo = events
            .Where(e => string.Equals(e.FieldRefName, "System.AssignedTo", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.EventTimestampUtc)
            .ThenByDescending(e => e.UpdateId)
            .Select(e => ExtractDisplayNameOrOriginal(e.NewValue))
            .FirstOrDefault();

        var snapshot = workItem == null
            ? null
            : new CachedWorkItemSnapshotDto
            {
                WorkItemId = workItem.TfsId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                AssignedTo = assignedTo,
                CurrentIterationPath = workItem.IterationPath,
                ParentId = currentParentId,
                ParentTitle = currentParentId.HasValue && titleLookup.TryGetValue(currentParentId.Value, out var parentTitle) ? parentTitle : null,
                FeatureId = currentFeatureId,
                FeatureTitle = currentFeatureId.HasValue && titleLookup.TryGetValue(currentFeatureId.Value, out var featureTitle) ? featureTitle : null,
                EpicId = currentEpicId,
                EpicTitle = currentEpicId.HasValue && titleLookup.TryGetValue(currentEpicId.Value, out var epicTitle) ? epicTitle : null,
                LastChangedDate = workItem.TfsChangedDate
            };

        var sprintsByPath = (await _sprintRepository.GetAllSprintsAsync(cancellationToken))
            .GroupBy(s => s.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var grouped = events
            .Select(e => new
            {
                Event = new ActivityLedgerEventDto
                {
                    Timestamp = e.EventTimestamp,
                    UpdateId = e.UpdateId,
                    ChangedBy = changedByByUpdateId.TryGetValue(e.UpdateId, out var changedBy) ? changedBy : null,
                    FieldRefName = e.FieldRefName,
                    OldValue = NormalizeIdentityFieldValue(e.FieldRefName, e.OldValue),
                    NewValue = NormalizeIdentityFieldValue(e.FieldRefName, e.NewValue),
                    IterationPathAtTime = e.IterationPath,
                    ParentIdAtTime = e.ParentId,
                    FeatureIdAtTime = e.FeatureId,
                    EpicIdAtTime = e.EpicId
                },
                Sprint = !string.IsNullOrWhiteSpace(e.IterationPath) && sprintsByPath.TryGetValue(e.IterationPath, out var sprint) ? sprint : null
            })
            .ToList();

        var sprintGroups = grouped
            .Where(x => x.Sprint != null)
            .GroupBy(x => x.Sprint!.Id)
            .Select(g =>
            {
                var sprint = g.First().Sprint!;
                var eventsInSprint = g.Select(x => x.Event).ToList();
                return new ActivityLedgerSprintGroupDto
                {
                    SprintId = sprint.Id,
                    SprintName = sprint.Name,
                    IterationPath = sprint.Path,
                    SprintStart = sprint.StartUtc,
                    SprintEnd = sprint.EndUtc,
                    TotalEventCount = eventsInSprint.Count,
                    DistinctFieldsTouchedCount = eventsInSprint
                        .Select(e => e.FieldRefName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    DistinctUsersCount = eventsInSprint
                        .Select(e => e.ChangedBy)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Events = eventsInSprint
                };
            })
            .OrderBy(g => g.SprintStart ?? DateTimeOffset.MaxValue)
            .ThenBy(g => g.SprintName)
            .ToList();

        var unknownEvents = grouped
            .Where(x => x.Sprint == null)
            .Select(x => x.Event)
            .ToList();

        return new ActivityLedgerValidationDto
        {
            WorkItemId = workItemId,
            Snapshot = snapshot,
            SprintGroups = sprintGroups,
            UnknownSprintEvents = unknownEvents
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

    private static string? NormalizeIdentityFieldValue(string fieldRefName, string? value)
    {
        if (!string.Equals(fieldRefName, "System.AssignedTo", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fieldRefName, "System.ChangedBy", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return ExtractDisplayNameOrOriginal(value);
    }

    private static string? ExtractDisplayNameOrOriginal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(value);
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object &&
                jsonDocument.RootElement.TryGetProperty("displayName", out var displayNameElement) &&
                displayNameElement.ValueKind == JsonValueKind.String)
            {
                var displayName = displayNameElement.GetString();
                return string.IsNullOrWhiteSpace(displayName) ? value : displayName;
            }
        }
        catch (JsonException)
        {
            // Value is not JSON, keep original.
        }

        return value;
    }
}
