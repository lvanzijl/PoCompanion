# Bug Analysis Report

## Metadata
- Bug: Parent move causes ResolvedWorkItems entry without matching WorkItems row
- Area: Sync / hierarchy resolution / data consistency
- Status: Analysis complete

## ROOT_CAUSE

- `ResolvedWorkItems` is populated only by `PoTool.Api/Services/WorkItemResolutionService.cs`. That service loads the current `context.WorkItems`, builds a dictionary keyed by `WorkItemEntity.TfsId`, walks from each configured `ProductBacklogRoot`, and only adds a `ResolvedWorkItemEntity` when `workItemsByTfsId.TryGetValue(tfsId, out var wi)` succeeds. In other words: the resolution pass itself cannot freshly create a resolved row for a work item that is absent from `WorkItems` at resolution time.
- `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` does build `WorkItemRelationshipEdges`, but that path is separate. It snapshots the hierarchy directly from TFS and stores edges per product owner. `PoTool.Api/Services/WorkItemResolutionService.cs` does not read `WorkItemRelationshipEdges`, projections, or any cached graph structure when populating `ResolvedWorkItems`.
- The key mapping is also now clear: `PoTool.Api/Persistence/Entities/WorkItemEntity.cs` defines `Id` as the local database primary key and `TfsId` as the unique TFS work item identifier. `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` defines `WorkItemId` as the TFS work item identifier. So the intended relation is `ResolvedWorkItems.WorkItemId` -> `WorkItems.TfsId`, not `WorkItems.Id`. I did not find a code path in the resolution pipeline or the inspected consumers that intentionally maps `ResolvedWorkItems.WorkItemId` to `WorkItems.Id`.
- The real inconsistency comes from lifecycle and scope, not from derivation logic. `WorkItems` are treated as a global disposable cache and can be deleted globally (`PoTool.Api/Repositories/CacheStateRepository.cs`, `PoTool.Api/Services/CacheManagementService.cs`). `ResolvedWorkItems` are deleted only for the selected product ids during resolution and sprint-projection cleanup (`PoTool.Api/Services/WorkItemResolutionService.cs`, `PoTool.Api/Services/CacheManagementService.cs`). `PoTool.Api/Migrations/20260205163141_AddRevisionTrackingTables.cs` and `PoTool.Api/Persistence/PoToolDbContext.cs` define a unique index on `ResolvedWorkItems.WorkItemId`, but no foreign key back to `WorkItems.TfsId`.
- That means a row can exist in `ResolvedWorkItems` while no corresponding row exists in `WorkItems` only as a stale derived row: the resolved row must have been created earlier, at a time when the work item did exist in `WorkItems` and was reachable from the configured backlog roots, and the base `WorkItems` row was removed later by a global cache deletion/replacement path that did not also remove that resolved row. This is the only explanation that is consistent with the current code and with the observed state for work item `117917`.

## CURRENT_BEHAVIOR
- `PoTool.Api/Services/Sync/SyncPipelineRunner.cs` builds sync scope from the current product owner's configured `ProductBacklogRoots`. Moving work item `117917` under parent `128305` matters because it changes whether that item is reachable from those roots.
- `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` calls `ITfsClient.GetWorkItemsByRootIdsAsync`, and `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` performs recursive descendant discovery plus ancestor completion before returning DTOs. The sync stage then upserts those DTOs into `WorkItems` by `TfsId`.
- `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` separately snapshots current edges into `WorkItemRelationshipEdges`, but that snapshot is not an alternate source for `ResolvedWorkItems`.
- `PoTool.Api/Services/WorkItemResolutionService.cs` loads **all** rows from `WorkItems` without product filtering, then applies product scope only when it walks from each product's backlog roots. So the derived table is product-scoped, but the base table it reads from is global.
- Because the existence check is against the unfiltered global `WorkItems` cache keyed by `TfsId`, a table-level lookup such as `WorkItems where TfsId = 117917` is not subject to product filtering. If that lookup returns no row, the item is truly absent from `WorkItems`. The only key-related caveat is that `WorkItems.Id` is the wrong column to compare against `ResolvedWorkItems.WorkItemId`.
- The inspected readers (`PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs`, `PoTool.Api/Services/SprintTrendProjectionService.cs`, and `PoTool.Api/Services/PortfolioFlowProjectionService.cs`) all treat `ResolvedWorkItems.WorkItemId` as a TFS id and then load `WorkItems` via `TfsId`. So there is no verified consumer-side key mismatch explaining the contradiction.
- Those readers do, however, show the filtering difference clearly: they first scope by `ResolvedWorkItems.ResolvedProductId`, then load `WorkItems` for the matching `WorkItemId`/`TfsId` set. If a stale resolved row survives while the base snapshot row is gone, the item remains product-visible in resolved scope but silently disappears from any output that still needs fields from `WorkItems`.
- Therefore the observed runtime state for `117917` should be interpreted as follows: the row in `ResolvedWorkItems` is not proof that the current resolution pass can resolve from outside `WorkItems`; it is evidence that the local cache has already diverged, with a stale resolved row surviving after the matching global `WorkItems` row was removed.

## Comments on the Issue (you are @copilot in this section)

<comments>
I re-checked the contradiction in the original report against the actual code paths.

The correction is: `ResolvedWorkItems` is still strictly derived from `WorkItems` at creation time, and `WorkItemRelationshipEdges` are not used to populate it. So a `ResolvedWorkItems` row without a `WorkItems` row is not created by an alternate resolution source.

The only internally consistent explanation is a cache split after creation. `ResolvedWorkItems.WorkItemId` stores the TFS id and should match `WorkItems.TfsId`, not `WorkItems.Id`. Because `WorkItems` are global and disposable while `ResolvedWorkItems` are product-scoped and not protected by a foreign key, a previously valid resolved row for `117917` can survive after the base snapshot row has been globally deleted. That is how `117917` can be present in `ResolvedWorkItems` while absent from `WorkItems`.

So the parent move under `128305` is the trigger that made `117917` resolvable for that product, but it is not the mechanism that explains the later contradiction. The contradiction is explained by cache-lifecycle divergence between the global `WorkItems` cache and the product-scoped `ResolvedWorkItems` cache.
</comments>
