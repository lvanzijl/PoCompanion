using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Builds an authoritative snapshot of current work item relationships using the Work Items API.
/// </summary>
public class WorkItemRelationshipSnapshotService
{
    private const string InMemoryProviderName = "Microsoft.EntityFrameworkCore.InMemory";
    private const string ParentRelationType = "System.LinkTypes.Hierarchy-Reverse";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkItemRelationshipSnapshotService> _logger;

    public WorkItemRelationshipSnapshotService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkItemRelationshipSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<RelationshipSnapshotResult> BuildSnapshotAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();

        var profile = await context.Profiles
            .Include(p => p.Products)
                .ThenInclude(p => p.BacklogRoots)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(p => p.Id == productOwnerId, cancellationToken);

        if (profile == null)
        {
            return RelationshipSnapshotResult.Failed($"ProductOwner {productOwnerId} was not found.");
        }

        var rootIds = profile.Products
            .SelectMany(p => p.BacklogRoots.Select(r => r.WorkItemTfsId))
            .Distinct()
            .ToArray();

        if (rootIds.Length == 0)
        {
            return RelationshipSnapshotResult.Failed($"ProductOwner {productOwnerId} has no configured backlog roots.");
        }

        var workItems = (await tfsClient.GetWorkItemsByRootIdsAsync(
            rootIds,
            since: null,
            progressCallback: null,
            cancellationToken)).ToList();

        if (workItems.Count == 0)
        {
            return RelationshipSnapshotResult.Failed("No work items were returned for configured roots.");
        }

        var snapshotAsOf = DateTimeOffset.UtcNow;
        var edges = BuildEdges(workItems, productOwnerId, snapshotAsOf);

        await DeleteExistingEdgesAsync(context, productOwnerId, cancellationToken);

        if (edges.Count > 0)
        {
            await context.WorkItemRelationshipEdges.AddRangeAsync(edges, cancellationToken);
        }

        var cacheState = await context.ProductOwnerCacheStates
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        if (cacheState == null)
        {
            cacheState = new ProductOwnerCacheStateEntity
            {
                ProductOwnerId = productOwnerId,
                SyncStatus = CacheSyncStatus.Idle
            };
            context.ProductOwnerCacheStates.Add(cacheState);
        }

        cacheState.RelationshipsSnapshotAsOfUtc = snapshotAsOf;
        cacheState.RelationshipsSnapshotWorkItemWatermark = workItems
            .Select(w => w.ChangedDate ?? w.RetrievedAt)
            .Where(d => d != default)
            .DefaultIfEmpty(snapshotAsOf)
            .Max();

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Captured {EdgeCount} relationship edges for ProductOwner {ProductOwnerId} at {SnapshotAsOf}",
            edges.Count,
            productOwnerId,
            snapshotAsOf);

        return RelationshipSnapshotResult.CreateSuccess(edges.Count, snapshotAsOf);
    }

    private static List<WorkItemRelationshipEdgeEntity> BuildEdges(
        IReadOnlyCollection<WorkItemDto> workItems,
        int productOwnerId,
        DateTimeOffset snapshotAsOf)
    {
        var edges = new List<WorkItemRelationshipEdgeEntity>();
        var dedupe = new HashSet<(int Source, int? Target, string RelationType)>();

        foreach (var workItem in workItems)
        {
            if (workItem.ParentTfsId.HasValue)
            {
                TryAddEdge(edges, dedupe, productOwnerId, workItem.TfsId, workItem.ParentTfsId.Value, ParentRelationType, snapshotAsOf);
            }

            if (workItem.Relations is { Count: > 0 })
            {
                foreach (var relation in workItem.Relations)
                {
                    if (relation.TargetWorkItemId.HasValue && !string.IsNullOrWhiteSpace(relation.LinkType))
                    {
                        TryAddEdge(
                            edges,
                            dedupe,
                            productOwnerId,
                            workItem.TfsId,
                            relation.TargetWorkItemId.Value,
                            relation.LinkType,
                            snapshotAsOf);
                    }
                }
            }
        }

        return edges;
    }

    private static async Task DeleteExistingEdgesAsync(
        PoToolDbContext context,
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var existingEdgesQuery = context.WorkItemRelationshipEdges
            .Where(edge => edge.ProductOwnerId == productOwnerId);

        if (context.Database.ProviderName == InMemoryProviderName)
        {
            var existingEdges = await existingEdgesQuery.ToListAsync(cancellationToken);
            context.WorkItemRelationshipEdges.RemoveRange(existingEdges);
            return;
        }

        await existingEdgesQuery.ExecuteDeleteAsync(cancellationToken);
    }

    private static void TryAddEdge(
        ICollection<WorkItemRelationshipEdgeEntity> edges,
        ISet<(int Source, int? Target, string RelationType)> dedupe,
        int productOwnerId,
        int sourceId,
        int targetId,
        string relationType,
        DateTimeOffset snapshotAsOf)
    {
        var key = (Source: sourceId, Target: (int?)targetId, relationType);
        if (!dedupe.Add(key))
        {
            return;
        }

        edges.Add(new WorkItemRelationshipEdgeEntity
        {
            ProductOwnerId = productOwnerId,
            SourceWorkItemId = sourceId,
            TargetWorkItemId = targetId,
            RelationType = relationType,
            SnapshotAsOfUtc = snapshotAsOf
        });
    }
}

public sealed record RelationshipSnapshotResult
{
    public required bool Success { get; init; }
    public int EdgeCount { get; init; }
    public DateTimeOffset? SnapshotAsOfUtc { get; init; }
    public string? ErrorMessage { get; init; }

    public static RelationshipSnapshotResult CreateSuccess(int edgeCount, DateTimeOffset snapshotAsOfUtc) =>
        new()
        {
            Success = true,
            EdgeCount = edgeCount,
            SnapshotAsOfUtc = snapshotAsOfUtc
        };

    public static RelationshipSnapshotResult Failed(string error) =>
        new()
        {
            Success = false,
            ErrorMessage = error
        };
}
