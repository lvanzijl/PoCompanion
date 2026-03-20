# Bug Analysis Report

## Metadata
- Bug: ResolvedWorkItems rows survive after item leaves analytical scope
- Area: Sync / resolution / cleanup
- Status: Analysis complete

## ROOT_CAUSE

- The single most likely root cause is not that `WorkItemResolutionService` forgets to run its delete; it is that incremental sync never removes stale base `WorkItems` rows for ids that leave current closure, so resolution immediately recreates the supposedly removed `ResolvedWorkItems` row from stale cached hierarchy data.
  In the repro, planner input is built from the freshly fetched TFS graph, so work item 117917 correctly appears in `LeftAnalyticalScopeIds`, `HierarchyChangedIds`, and the validation warning because the current closure no longer contains it.
  However, `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` only upserts fetched ids and never deletes previously persisted `WorkItems` rows that are no longer in `CurrentClosureScopeIds`.
  Because 117917 is outside current closure, `IdsToHydrate` is `0`, so its cached `WorkItems` row is neither refreshed to the new parent 118761 nor removed; it keeps the stale parent 128305.
  Later `PoTool.Api/Services/WorkItemResolutionService.cs` does a product-scoped full replace of `ResolvedWorkItems`, but it rebuilds from the entire persisted `WorkItems` table, not from planner scope or the freshly fetched closure graph.
  Its traversal starts from current `BacklogRoots`, expands children by cached `ParentTfsId`, and therefore still reaches 117917 through the stale cached link to 128305.
  The old resolved row is deleted, then a new one is inserted in the same rebuild, which is why the row survives and why final resolved count can exceed the current analytical scope count.

## CURRENT_BEHAVIOR
- Planner output: `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` builds previous scope from persisted `ResolvedWorkItems` plus ancestor closure from persisted `WorkItems`, and current scope from the work items returned by the current TFS fetch. With the repro parent move from 128305 to 118761, the planner correctly models 117917 as having left analytical scope. That matches the observed `PlanningMode=Incremental`, `LeftAnalyticalScopeIds includes 117917`, `HierarchyChangedIds includes 117917`, and `IdsToHydrate count = 0`.
- Validation warning: after planning, `LogResolvedOutsideClosureWarning` compares previously resolved ids with the current closure scope and logs `INCREMENTAL_SYNC_PLAN_VALIDATION` when a previously resolved id is now outside closure. That warning is expected in this repro because 117917 is still present in prior persisted `ResolvedWorkItems` while the current fetched closure excludes it.
- Persisted base snapshot after incremental sync: `UpsertWorkItemsAsync` only inserts or updates ids returned by the current fetch. It does not delete rows for ids that are no longer in current closure. So if 117917 was previously cached under parent 128305 and is no longer fetched after moving to 118761 outside scope, the stale `WorkItems` row remains unchanged. That matches the post-sync database evidence that `WorkItems count for TfsId 117917 = 1`.
- Resolution rebuild delete scope: `WorkItemResolutionService.DeleteExistingResolvedItemsAsync` is a full replace only for rows whose `ResolvedProductId` belongs to the current product owner's products. It is product-scoped cleanup, not global cleanup, and not targeted by analytical-scope ids. So the service does perform deletion for the affected product, but only inside `ResolvedWorkItems`.
- Resolution rebuild traversal: after the delete, `WorkItemResolutionService.ResolveAllAsync` loads all persisted `WorkItems` into `workItemsByTfsId`, builds `childrenByParent` from cached `ParentTfsId`, then traverses downward from each current `Product.BacklogRoots` entry. It does not use planner scope, `LeftAnalyticalScopeIds`, `CurrentClosureScopeIds`, or relationship snapshot edges to constrain the rebuild.
- Why out-of-scope rows survive: because 117917 remains in persisted `WorkItems` with stale parent 128305, the rebuild still reaches it from the current root traversal and inserts a fresh `ResolvedWorkItems` row for it immediately after deleting the previous one. The cleanup therefore appears to fail, but the real failure is stale base-snapshot retention plus rebuild-from-cache behavior.
- Why resolved count exceeds current analytical scope count: planner counts only the current fetched analytical scope (`905`), while resolution counts every persisted `WorkItems` row still reachable from backlog roots using cached parent links. Any stale descendants like 117917 that should have left scope but were not removed from `WorkItems` are counted again during rebuild, which explains how resolution can log `Resolved 908 work items` while planner reports only `905 analytical / 905 closure`.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the requested path end to end:

1. Planner computes scope from the freshly fetched graph and correctly identifies 117917 as having left scope.
2. Validation warns because the previous resolved snapshot still contains an id now outside current closure.
3. Incremental persistence does not remove the stale base `WorkItems` row for that id.
4. Resolution then performs a product-scoped delete-and-rebuild, but the rebuild reads the stale cached hierarchy and recreates the row it just deleted.

So the most likely root cause is incomplete cleanup of the base `WorkItems` snapshot for ids that leave current closure during incremental sync. The `ResolvedWorkItems` delete is real, but it is defeated by immediate reinsertion from stale cached parentage. That is also the simplest explanation for the count mismatch: the planner describes current scope, while resolution rebuilds from a larger stale persisted graph.
</comments>
