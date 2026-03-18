# Bug Analysis Report

## Metadata
- Bug: Parent move causes ResolvedWorkItems entry without matching WorkItems row
- Area: Sync / hierarchy resolution / data consistency
- Status: Analysis complete

## ROOT_CAUSE

- The inconsistency is caused by a broken cache contract between the base and derived tables, not by `WorkItemResolutionService` independently discovering extra items.
  Sync discovery starts from `ProductBacklogRoots` in `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`, then `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` and `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` fetch and upsert the discovered hierarchy into the global `WorkItems` cache.
  The resolution pass in `PoTool.Api/Services/WorkItemResolutionService.cs` can only create a `ResolvedWorkItems` row for an id already present in `WorkItems`, because it first materializes `context.WorkItems` and skips any missing `TfsId`.
  The defect is that `WorkItems` are treated as a global disposable cache and can be deleted globally (`PoTool.Api/Repositories/CacheStateRepository.cs`, `PoTool.Api/Services/CacheManagementService.cs`), while `ResolvedWorkItems` are deleted only per selected product and the schema defines no foreign key from `ResolvedWorkItems.WorkItemId` to `WorkItems.TfsId` (`PoTool.Api/Migrations/20260205163141_AddRevisionTrackingTables.cs`).
  Once the parent move makes work item 117917 product-visible under backlog parent 128305, that derived row can survive while the matching base snapshot row is later absent, leaving an inconsistent local database.

## CURRENT_BEHAVIOR
- `PoTool.Api/Services/Sync/SyncPipelineRunner.cs` builds sync scope from the current product owner's configured `ProductBacklogRoots`, so a TFS parent change becomes relevant only when it moves the work item under one of those roots.
- `PoTool.Api/Services/Sync/WorkItemSyncStage.cs` calls `ITfsClient.GetWorkItemsByRootIdsAsync`.
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` performs recursive hierarchy discovery, ancestor completion, and batch field retrieval before upserting the returned ids into `WorkItems`.
- Only ids returned by that fetch are inserted or updated in `WorkItems`.
- `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs` separately snapshots the current hierarchy into `WorkItemRelationshipEdges`.
- `PoTool.Api/Services/WorkItemResolutionService.cs` does not resolve from those edges. It rebuilds `ResolvedWorkItems` from the current `WorkItems` table and explicitly skips any id that is missing from `WorkItems`.
- The base/derived split is introduced by cache lifecycle semantics, not by the resolution walk itself.
- `PoTool.Api/Repositories/CacheStateRepository.cs` and `PoTool.Api/Services/CacheManagementService.cs` delete `WorkItems` globally, while resolved rows are deleted only for selected products.
- `PoTool.Api/Migrations/20260205163141_AddRevisionTrackingTables.cs` creates only a unique index on `ResolvedWorkItems.WorkItemId`; it does not enforce referential integrity back to `WorkItems`.
- Downstream readers assume the base and derived tables stay aligned. `PoTool.Api/Services/SprintTrendProjectionService.cs`, `PoTool.Api/Services/PortfolioFlowProjectionService.cs`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`, and `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` all start from `ResolvedWorkItems` scope and then join or load `WorkItems`, so a missing base row silently drops the item from metrics, execution views, or activity detail output.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the path end to end: TFS parent change -> product-root discovery -> work item fetch/upsert -> relationship snapshot -> resolution rebuild -> projection/query consumers.

The important correction is that the current resolution code does not have an independent path that can freshly insert a `ResolvedWorkItems` row for an id that is absent from `WorkItems`. The inconsistent state comes from the fact that the two tables do not share the same ownership or deletion scope, and the database does not enforce a base-row requirement.

So the verified root cause is a local cache-consistency contract bug. The parent move is the trigger that makes work item 117917 newly relevant under backlog parent 128305, but the durable defect is that `ResolvedWorkItems` can outlive the corresponding `WorkItems` snapshot because `WorkItems` are global, `ResolvedWorkItems` are product-scoped, and no foreign key prevents the split. That is why downstream metrics and projections can observe the item in resolved scope while failing to find its base snapshot row.
</comments>
