# Bug Analysis Report

## Metadata
- Bug: Sync architecture is difficult to test in isolation
- Area: Sync / architecture / testability
- Status: Analysis complete

## ROOT_CAUSE

- The current sync design hides the most important incremental-sync decisions inside coarse, side-effect-heavy boundaries instead of exposing them as a small planner contract.
  `SyncPipelineRunner` orchestrates the stages and builds product scope from configured roots.
  But the current implementation bundles multiple responsibilities into `ITfsClient.GetWorkItemsByRootIdsAsync` and its real implementation in `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`:
  - changed-ID detection
  - hierarchy traversal
  - ancestor completion
  - fetch planning
  - TFS HTTP calls
  - DTO hydration
  - part of the scope-closure behavior
  `WorkItemSyncStage` then immediately mixes the returned DTO set with EF upsert work in the same stage.
  There is no dedicated service whose inputs and outputs are “configured roots + prior watermark + current graph facts -> fetch plan / removals / re-resolve set”.
  Because that contract is missing, unit tests cannot target the incremental rules directly; they can only fake the final TFS answer or run a larger integration-style scenario.
- Persistence and reprojection boundaries also lack a strict, testable cache contract.
  `WorkItems` behave as a global disposable snapshot cache, while `ResolvedWorkItems` are rebuilt and deleted only for selected products in `PoTool.Api/Services/WorkItemResolutionService.cs`, `PoTool.Api/Repositories/CacheStateRepository.cs`, and `PoTool.Api/Services/CacheManagementService.cs`.
  The schema in `PoTool.Api/Persistence/PoToolDbContext.cs` gives `ResolvedWorkItems.WorkItemId` only a unique index, not a foreign key back to `WorkItems.TfsId`.
  This allows orphaned resolved items to exist, meaning resolved rows whose `WorkItemId` no longer exists in `WorkItems.TfsId`.
  That means the integrity guarantee cannot be enforced or tested at the schema level.
  As a result, the guarantee that resolved scope, hierarchy projections, and downstream reprojections remain aligned with the base work item snapshot is enforced only by orchestration order and deletion discipline.
  That is hard to assert cleanly in isolated tests and easy to break in moved-into-scope / moved-out-of-scope scenarios.

## CURRENT_BEHAVIOR
- Sync orchestration lives in `PoTool.Api/Services/Sync/SyncPipelineRunner.cs`. It builds one `SyncContext` from `Products`, `ProductBacklogRoots`, `Repositories`, discovered `PipelineDefinitions`, and cache watermarks, then runs staged sync work in sequence: work item sync, activity ingestion, team sprint sync, relationship snapshot, work item resolution, sprint trend projection, PR sync, pipeline sync, validation, metrics, and finalize.
- Scope discovery currently lives in `SyncPipelineRunner.BuildSyncContextAsync`. The effective work item scope starts from configured `ProductBacklogRoots`, not from a reusable domain/service contract. Repository scope and pipeline-definition discovery are also mixed into the same context-building method.
- Changed-ID detection for work item snapshot sync is not a first-class service today. `WorkItemSyncStage` passes `context.WorkItemWatermark` into `ITfsClient.GetWorkItemsByRootIdsAsync`, but the real hierarchy implementation in `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` still performs whole-graph recursive discovery and whole-result field hydration for the discovered ids. There is no separate planner abstraction that can be tested with cases like “new item under root”, “existing item moved under root”, or “item moved out of root” independent of HTTP and EF.
- Hierarchy traversal and work item fetch planning currently live in the real TFS client. `RealTfsClient.WorkItemsHierarchy.cs` performs recursive WIQL link discovery, ancestor completion, relation-to-parent mapping, and `workitemsbatch` field fetches before returning `WorkItemDto` results. This makes the client itself testable only through fake HTTP responses, not through small pure logic inputs.
- TFS access is isolated behind `ITfsClient`, which is a good boundary for consumer tests, but it is too coarse to validate sync-planning behavior. Above the client boundary, tests can fake “TFS already returned the exact right set of work items”; they cannot verify how the system should discover that set.
- Persistence/upsert for current snapshots lives in `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`. The stage directly maps DTOs to `WorkItemEntity`, queries existing ids through EF, updates/inserts entities, and saves per batch. This stage already has a narrow unit-test seam when the client is mocked and EF runs in memory, but only for watermark/mapping behavior, not for discovery rules.
- Relationship snapshot persistence lives in `PoTool.Api/Services/WorkItemRelationshipSnapshotService.cs`. It re-fetches the full root-scoped hierarchy from TFS, derives edge rows, deletes all existing `WorkItemRelationshipEdges` for the product owner, and inserts a replacement snapshot. The graph-snapshot contract is therefore side-effect-heavy and duplicated from the work item fetch flow instead of being reused from a shared planner/result.
- Resolution currently lives in `PoTool.Api/Services/WorkItemResolutionService.cs`. The service is partly testable because `ResolveAncestry` is pure once a `workItemsByTfsId` map exists, and there are existing in-memory tests for ancestry resolution and synthetic product-transition ledger entries in `PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs`. But `ResolveAllAsync` still loads products, work items, sprints, prior resolved rows, and cache state directly from EF, traverses current roots from the base snapshot, deletes product-scoped resolved rows, inserts replacements, appends synthetic activity entries, and updates cache state in one method.
- Resolution and reprojection triggers are orchestration-driven rather than contract-driven. `SyncPipelineRunner` simply runs relationship snapshot, then resolution, then projection stages. There is no explicit service that answers “does this sync require re-resolution?” or “which products/items require reprojection because parent or scope changed?” That makes hierarchy-change scenarios harder to specify and test independently.
- Some pieces can already be tested in isolation with fake TFS clients or in-memory persistence. `PoTool.Tests.Unit/Services/WorkItemSyncStageTests.cs` shows `WorkItemSyncStage` can be tested with a mocked `ITfsClient` and in-memory `PoToolDbContext`; `PoTool.Tests.Unit/Services/ActivityEventIngestionServiceTests.cs` shows `ActivityEventIngestionService` can be tested the same way; `PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs` shows the resolution service can be exercised with in-memory EF; and `PoTool.Tests.Unit/Services/WorkItemAncestorCompletionTests.cs` / `WorkItemHierarchyBacklogPriorityTests.cs` show the real hierarchy client can be tested with fake HTTP.
- The hard part is that the most business-critical incremental cases still cross multiple services and persistence boundaries at once.
  To verify “newly created item inside scope”, “existing item moved into scope via parent change”, “item moved out of scope”, or “hierarchy change causing re-resolution”, a test currently has to rely on a large integration path through:
  - TFS graph discovery
  - DTO hydration
  - EF upsert
  - edge snapshot rebuild
  - resolved-row rebuild
  - cache semantics
  Because no smaller contract exists that captures these rules, tests must exercise all of those layers together.
- The smallest architecture shift that would materially improve testability is to introduce a dedicated sync planner abstraction for work items.
  That planner should not call TFS or EF directly.
  It should consume configured roots, prior watermarks, cached graph facts (for example prior parent-child relationships, prior in-scope membership, or other cached hierarchy facts already known locally), and changed-item / relation facts.
  It should return an explicit fetch-and-apply plan:
  - ids to fetch
  - ids that newly entered scope
  - ids that left scope
  - whether hierarchy/resolution/projection invalidation is required
  With that seam, fake TFS clients and in-memory persistence would be sufficient for most incremental tests, while the planner itself could be unit-tested as pure logic.
- A second small but important change would be to separate snapshot persistence from reprojection invariants. Even without a large redesign, the architecture would become much easier to test if one boundary owned the base-snapshot contract (`WorkItems` membership by `TfsId`) and another boundary owned derived projection rebuild from that contract. Right now those concerns are spread across work item sync, relationship snapshot, resolution, and cache reset/delete services.
- The desired invariant “all `ResolvedWorkItems` must map to `WorkItems.TfsId`” is not cleanly enforceable today. It can be observed indirectly in tests, but not guaranteed by schema or by a single service contract. The smallest strict contract would be either: (1) add referential integrity from `ResolvedWorkItems.WorkItemId` to `WorkItems.TfsId` and make deletes/rebuilds obey that ownership; or, if schema coupling must stay looser, (2) introduce a single invariant-enforcement step that deletes or rejects orphaned resolved rows transactionally whenever work item membership changes. Either approach would make the guarantee testable without requiring real TFS.

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the current sync path specifically from the perspective of “what would we need in order to write strict incremental-sync tests without calling real TFS?”

The good news is that the codebase already has some usable seams. `ITfsClient` is the mandatory TFS boundary, and several services already have in-memory or mocked tests. That means the repository is not starting from zero. The problem is that the seam exists at the wrong level for the scenarios we care about most. The current abstraction lets a test substitute “what TFS returned”, but it does not let a test substitute or assert “how the system decided what should be fetched, removed, or re-resolved.” Those decisions are where incremental-sync correctness actually lives.

Concretely, the responsibilities are split like this today:

- scope discovery: `SyncPipelineRunner.BuildSyncContextAsync`
- changed/work-item selection for sync: implicitly passed as a watermark into `ITfsClient.GetWorkItemsByRootIdsAsync`, but not expressed as a standalone contract
- hierarchy traversal and ancestor completion: `RealTfsClient.WorkItemsHierarchy.cs`
- work item fetch planning and hydration: also `RealTfsClient.WorkItemsHierarchy.cs`
- TFS access: `ITfsClient` implementations
- persistence/upsert: `WorkItemSyncStage`, `WorkItemRelationshipSnapshotService`, `ActivityEventIngestionService`, and `WorkItemResolutionService`
- resolution/reprojection trigger behavior: stage ordering in `SyncPipelineRunner`, not a dedicated invalidation policy

From a testability standpoint, the pure or mostly pure logic is limited but identifiable. `WorkItemResolutionService.ResolveAncestry` is the clearest example. Some other logic is isolated enough to test with in-memory EF and a fake client, such as watermark persistence in `WorkItemSyncStage` and activity-ledger deduplication in `ActivityEventIngestionService`. The rest of the flow is dominated by orchestration, EF, and TFS/HTTP concerns.

That is why the smallest high-value architectural change is not “add more integration tests”; it is “extract the missing contract.”
The codebase needs a dedicated planner/service abstraction that describes incremental sync behavior in domain-local terms inside the backend, without making TFS or EF calls itself.
For example, a planner result should be able to say:

- which work item ids are now in effective scope
- which ids were newly introduced into scope
- which ids left scope
- which ids require a field refresh
- whether hierarchy changes require resolution rebuild
- whether downstream projections must be recomputed

If such a planner existed, the important scenarios from the issue could be covered without real TFS:

- newly created item inside scope -> planner returns newly in-scope id + fetch required
- existing item moved into scope via parent change -> planner returns in-scope transition + hierarchy invalidation
- item moved out of scope -> planner returns out-of-scope removal + derived-row cleanup requirement
- hierarchy change causing re-resolution -> planner returns same-membership but changed ancestry + re-resolution required

The same principle applies to the `ResolvedWorkItems` contract. Right now the guarantee that resolved rows cannot diverge from `WorkItems` is mostly a convention. It depends on stage order, delete scope, and consumers behaving as if the base cache and derived cache are always aligned. That is exactly the kind of rule that becomes brittle unless one boundary owns it explicitly. A strict contract should state that resolved rows are a pure projection of a known `WorkItems` membership set keyed by `TfsId`, and that any base-membership removal must also remove or invalidate the derived row in the same persistence boundary. Once that contract exists, it becomes straightforward to test with in-memory persistence.

So my conclusion is: the architecture is already close enough that a full rewrite is unnecessary, but it is missing one crucial seam. The smallest effective improvement is to split “decide what changed and what must be refreshed/re-resolved” away from “call TFS” and “write EF rows.” Without that split, incremental sync remains mostly integration-testable. With that split, the repository could define strict unit tests for sync behavior using fake TFS inputs and in-memory persistence only.
</comments>
