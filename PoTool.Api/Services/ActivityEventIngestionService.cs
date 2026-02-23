using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

public sealed class ActivityEventIngestionService
{
    private static readonly HashSet<string> WhitelistedFields = new(
        RevisionFieldWhitelist.Fields,
        StringComparer.OrdinalIgnoreCase);

    private readonly PoToolDbContext _context;
    private readonly ITfsClient _tfsClient;
    private readonly ActivityIngestionOptions _options;
    private readonly ILogger<ActivityEventIngestionService> _logger;

    public ActivityEventIngestionService(
        PoToolDbContext context,
        ITfsClient tfsClient,
        IOptions<ActivityIngestionOptions> options,
        ILogger<ActivityEventIngestionService> logger)
    {
        _context = context;
        _tfsClient = tfsClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActivityIngestionResult> IngestAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        var cacheState = await _context.ProductOwnerCacheStates
            .FirstOrDefaultAsync(x => x.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState == null)
        {
            cacheState = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            _context.ProductOwnerCacheStates.Add(cacheState);
        }

        var persistedWatermark = cacheState.ActivityEventWatermark;
        var backfillStart = persistedWatermark == null && _options.ActivityBackfillDays > 0
            ? DateTimeOffset.UtcNow.Date.AddDays(-_options.ActivityBackfillDays)
            : (DateTimeOffset?)null;
        var filterStart = persistedWatermark ?? backfillStart;

        var workItemIds = persistedWatermark.HasValue
            ? await _context.WorkItems
                .Where(w => w.TfsChangedDate > persistedWatermark.Value)
                .Select(w => w.TfsId)
                .ToListAsync(cancellationToken)
            : await _context.WorkItems
                .Select(w => w.TfsId)
                .ToListAsync(cancellationToken);

        if (workItemIds.Count == 0)
        {
            return new ActivityIngestionResult(0, persistedWatermark);
        }

        var workItemSnapshots = await _context.WorkItems
            .Select(w => new { w.TfsId, w.Type, w.ParentTfsId, w.IterationPath })
            .ToListAsync(cancellationToken);

        var typeById = workItemSnapshots.ToDictionary(x => x.TfsId, x => x.Type);
        var parentById = workItemSnapshots.ToDictionary(x => x.TfsId, x => x.ParentTfsId);
        var iterationById = workItemSnapshots.ToDictionary(x => x.TfsId, x => x.IterationPath);

        var stagedEntries = new List<ActivityEventLedgerEntryEntity>();
        DateTimeOffset? maxProcessedTimestamp = persistedWatermark;

        foreach (var workItemId in workItemIds)
        {
            var updates = await _tfsClient.GetWorkItemUpdatesAsync(workItemId, cancellationToken);
            if (updates.Count == 0)
            {
                continue;
            }

            var orderedUpdates = updates
                .OrderBy(u => u.UpdateId)
                .ThenBy(u => u.RevisedDate)
                .Where(u => !filterStart.HasValue || u.RevisedDate > filterStart.Value)
                .ToList();

            if (orderedUpdates.Count == 0)
            {
                continue;
            }

            var candidateUpdateIds = orderedUpdates.Select(u => u.UpdateId).Distinct().ToList();
            var existingKeys = await _context.ActivityEventLedgerEntries
                .Where(e => e.ProductOwnerId == productOwnerId
                            && e.WorkItemId == workItemId
                            && candidateUpdateIds.Contains(e.UpdateId))
                .Select(e => new { e.UpdateId, e.FieldRefName })
                .ToListAsync(cancellationToken);
            var existingKeySet = existingKeys
                .Select(x => $"{x.UpdateId}|{x.FieldRefName}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var currentIteration = iterationById.TryGetValue(workItemId, out var existingIteration) ? existingIteration : null;
            var currentParent = parentById.TryGetValue(workItemId, out var existingParent) ? existingParent : null;

            foreach (var update in orderedUpdates)
            {
                if (!maxProcessedTimestamp.HasValue || update.RevisedDate > maxProcessedTimestamp.Value)
                {
                    maxProcessedTimestamp = update.RevisedDate;
                }

                if (update.FieldChanges.TryGetValue("System.IterationPath", out var iterationChange))
                {
                    currentIteration = iterationChange.NewValue;
                }

                if (update.FieldChanges.TryGetValue("System.Parent", out var parentChange))
                {
                    currentParent = TryParseNullableInt(parentChange.NewValue);
                    parentById[workItemId] = currentParent;
                }

                var hierarchyContext = ResolveHierarchyContext(workItemId, currentParent, typeById, parentById);

                foreach (var fieldChange in update.FieldChanges.Values)
                {
                    if (!WhitelistedFields.Contains(fieldChange.FieldRefName) ||
                        string.Equals(fieldChange.OldValue, fieldChange.NewValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var dedupeKey = $"{update.UpdateId}|{fieldChange.FieldRefName}";
                    if (!existingKeySet.Add(dedupeKey))
                    {
                        continue;
                    }

                    stagedEntries.Add(new ActivityEventLedgerEntryEntity
                    {
                        ProductOwnerId = productOwnerId,
                        WorkItemId = workItemId,
                        UpdateId = update.UpdateId,
                        FieldRefName = fieldChange.FieldRefName,
                        EventTimestamp = update.RevisedDate,
                        IterationPath = currentIteration,
                        ParentId = hierarchyContext.ParentId,
                        FeatureId = hierarchyContext.FeatureId,
                        EpicId = hierarchyContext.EpicId,
                        OldValue = fieldChange.OldValue,
                        NewValue = fieldChange.NewValue
                    });
                }
            }
        }

        if (stagedEntries.Count > 0)
        {
            await _context.ActivityEventLedgerEntries.AddRangeAsync(stagedEntries, cancellationToken);
        }

        if (!maxProcessedTimestamp.HasValue && !persistedWatermark.HasValue)
        {
            maxProcessedTimestamp = DateTimeOffset.UtcNow;
        }

        cacheState.ActivityEventWatermark = maxProcessedTimestamp;

        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Activity ingestion completed for ProductOwner {ProductOwnerId}. Events persisted: {EventCount}. Watermark: {Watermark}",
            productOwnerId,
            stagedEntries.Count,
            cacheState.ActivityEventWatermark?.ToString("O") ?? "null");

        return new ActivityIngestionResult(stagedEntries.Count, cacheState.ActivityEventWatermark);
    }

    private static (int? ParentId, int? FeatureId, int? EpicId) ResolveHierarchyContext(
        int workItemId,
        int? parentId,
        IReadOnlyDictionary<int, string> typeById,
        IReadOnlyDictionary<int, int?> parentById)
    {
        var featureId = typeById.TryGetValue(workItemId, out var selfType) && string.Equals(selfType, "Feature", StringComparison.OrdinalIgnoreCase)
            ? workItemId
            : (int?)null;
        var epicId = typeById.TryGetValue(workItemId, out selfType) && string.Equals(selfType, "Epic", StringComparison.OrdinalIgnoreCase)
            ? workItemId
            : (int?)null;

        var current = parentId;
        var hop = 0;
        while (current.HasValue && hop < 50)
        {
            if (typeById.TryGetValue(current.Value, out var ancestorType))
            {
                if (!featureId.HasValue && string.Equals(ancestorType, "Feature", StringComparison.OrdinalIgnoreCase))
                {
                    featureId = current.Value;
                }

                if (!epicId.HasValue && string.Equals(ancestorType, "Epic", StringComparison.OrdinalIgnoreCase))
                {
                    epicId = current.Value;
                }
            }

            if (featureId.HasValue && epicId.HasValue)
            {
                break;
            }

            current = parentById.TryGetValue(current.Value, out var nextParent) ? nextParent : null;
            hop++;
        }

        return (parentId, featureId, epicId);
    }

    private static int? TryParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}

public readonly record struct ActivityIngestionResult(int PersistedEventCount, DateTimeOffset? Watermark);
