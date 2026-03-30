# Work Item Query Boundary Phase 1

## Summary

This change introduces the first dedicated cache-only analytical persistence boundary for the Work Item slice:

- `PoTool.Api/Services/IWorkItemQuery.cs`
- `PoTool.Api/Services/EfWorkItemQuery.cs`

The boundary is intentionally narrow. It owns only cache-backed analytical reads over persisted Work Items and returns fully materialized `WorkItemDto` collections. It does **not** expose live/TFS behavior, command semantics, revisions/history, `DataSourceMode`, `IQueryable`, or EF entities.

Phase 1 focuses on the safest analytical handlers that were previously coupled to the broader `IWorkItemReadProvider` abstraction, which still hides a live/cache switch. The goal of this phase is to establish a clean analytical read boundary without redesigning the broader Work Item provider surface.

## Migration Inventory

### Candidate handler inventory

| Handler | Present | Previous persistence dependency | Leakage assessment | Phase 1 outcome |
|---|---:|---|---|---|
| `GetAllWorkItemsQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` + `ProfileFilterService` | Broad provider leakage because `IWorkItemReadProvider` can resolve live or cached reads | **Migrated** |
| `GetAllWorkItemsWithValidationQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` + `ProfileFilterService` + validator | Same broad provider leakage; validation is in-memory and safe to keep in handler | **Migrated** |
| `GetAllGoalsQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` + `ProfileFilterService` | Cache-only analytical filtering over work item snapshots | **Migrated** |
| `GetGoalHierarchyQueryHandler` | Yes | `IWorkItemReadProvider` + `TfsRuntimeMode` + optional `BattleshipMockDataFacade` | Not a safe cache-only candidate because it explicitly participates in mock/live runtime behavior | **Deferred** |
| `GetDependencyGraphQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` | Analytical graph construction over current cached relations | **Migrated** |
| `GetValidationImpactAnalysisQueryHandler` | Yes | `IWorkItemReadProvider` + `IWorkItemRepository` fallback + `IProductRepository` | Cache-only analysis, but had a repository fallback that bypassed a dedicated analytical boundary | **Migrated** |
| `GetProductBacklogStateQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` | Product-scoped current-state analytical read | **Migrated** |
| `GetHealthWorkspaceProductSummaryQueryHandler` | Yes | `IWorkItemReadProvider` + `IProductRepository` | Product-scoped current-state analytical summary | **Migrated** |

### Direct `PoToolDbContext` usage inventory

None of the candidate handlers used `PoToolDbContext` directly before this change.

The leakage in this slice was different from Build Quality and Pipeline Insights:

- most candidate handlers depended on `IWorkItemReadProvider`, which is broader than a cache-only analytical boundary because it is designed to switch between live and cached data sources
- `GetValidationImpactAnalysisQueryHandler` also depended on `IWorkItemRepository` for a fallback `GetAllAsync` path
- this meant persistence/query shape was still hidden behind abstractions that are not specific to analytical cache reads

### Why `GetGoalHierarchyQueryHandler` was deferred

`GetGoalHierarchyQueryHandler` is intentionally left outside Phase 1 because it is not a pure cache-only analytical read:

- it depends on `TfsRuntimeMode`
- it can return mock data through `BattleshipMockDataFacade`
- it still uses `IWorkItemReadProvider` to support the goal-ID hierarchy path in the current runtime model

That makes it the wrong target for the first cache-only query boundary. Migrating it in this prompt would blur the distinction between analytical cache reads and the existing mock/live runtime behavior.

## Query Boundary Design

### Interface shape

`IWorkItemQuery` is intentionally minimal and contains only the methods required by the migrated handlers:

1. `GetAllAsync(CancellationToken cancellationToken)`
2. `GetByAreaPathsAsync(IReadOnlyList<string> areaPaths, CancellationToken cancellationToken)`
3. `GetByRootIdsAsync(IReadOnlyList<int> rootWorkItemIds, CancellationToken cancellationToken)`

### Responsibilities

The interface owns only:

- cache-backed snapshot reads from the persisted `WorkItems` table
- hierarchical root expansion for product-scoped analytical reads
- area-path scoped analytical reads
- materialization to `WorkItemDto`

The interface does **not** own:

- live/TFS calls
- `DataSourceMode`
- command/update behavior
- revisions/history/state timeline/event-ledger paths
- `IQueryable`
- EF entities or `PoToolDbContext` leakage outside the persistence layer

### Read model choice

Phase 1 keeps the boundary simple by returning the already-established `WorkItemDto` snapshot DTO. That preserves existing handler behavior while still enforcing a persistence boundary because the EF implementation materializes the data internally and does not leak queryables or entities.

## Implementation

### EF-backed query store

`EfWorkItemQuery` encapsulates cache-only Work Item query composition over `PoToolDbContext.WorkItems`.

Key implementation details:

- all reads use `AsNoTracking()`
- all methods materialize inside the store
- `GetByRootIdsAsync` preserves the existing descendant expansion semantics by loading cached rows and iteratively walking `ParentTfsId`
- `GetByAreaPathsAsync` preserves the existing hierarchical area-path prefix matching behavior
- `GetAllAsync` returns the full cached snapshot set

### Shared mapping

A new internal helper, `PoTool.Api/Services/WorkItemQueryMapping.cs`, centralizes `WorkItemEntity -> WorkItemDto` mapping.

That helper is now shared by:

- `EfWorkItemQuery`
- `CachedWorkItemReadProvider`

This avoids duplication and keeps cache-backed Work Item snapshot mapping consistent between the legacy provider surface and the new analytical boundary.

### SQLite-aware behavior

The implementation stays SQLite-safe because it does not introduce any `DateTimeOffset` predicates, ordering, or aggregation in translated query paths. The store performs only simple table reads and in-memory hierarchy expansion, and it does not leak `IQueryable` into handlers.

## Refactored Components

### Migrated handlers

The following handlers now depend on `IWorkItemQuery` instead of `IWorkItemReadProvider` / repository fallback for their analytical Work Item reads:

- `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsWithValidationQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetAllGoalsQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`

### Supporting changes

Additional supporting changes in this phase:

- `PoTool.Api/Services/CachedWorkItemReadProvider.cs` now delegates its shared cache-only analytical methods to `IWorkItemQuery`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` now registers `IWorkItemQuery` to `EfWorkItemQuery`
- focused tests were updated or added for the migrated handlers, the new query store, and DI registration

### Behavior preservation

No intentional behavior changes were introduced.

Observed preserved behavior includes:

- product-root scoped hierarchy loading remains unchanged
- fallback-to-all-cache behavior remains unchanged when no product roots are available
- goal filtering remains `WorkItemType.Goal`
- dependency graph filtering, link projection, blocked-item detection, and critical path analysis remain unchanged
- validation impact filtering and recommendation generation remain unchanged
- backlog-state and health-summary score computation remain in the existing computation services

## Boundary Preservation

Phase 1 preserves the separation between analytical cache reads and live/mock/provider-shaped routes.

### Intentionally left outside the new boundary

The following areas remain outside `IWorkItemQuery` by design:

- `GetGoalHierarchyQueryHandler` because it still participates in mock/live runtime behavior
- direct TFS routes in `WorkItemsController`, including:
  - `GET api/workitems/area-paths/from-tfs`
  - `GET api/workitems/goals/from-tfs`
- revisions/history paths such as state timeline and validation history
- refresh/update/fix commands
- broad live/provider redesign
- sprint execution / sprint metrics / event-ledger paths

### No new ambiguity introduced

This phase keeps the split explicit:

- `IWorkItemQuery` = cache-only analytical snapshots
- `IWorkItemReadProvider` = broader provider abstraction that still exists for live/mock/runtime-dependent paths

That keeps Phase 1 focused and avoids prematurely forcing non-analytical routes into a boundary they do not yet fit.

## Validation

### Focused validation

Succeeded:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --nologo --filter "FullyQualifiedName~GetAllWorkItemsQueryHandlerTests|FullyQualifiedName~GetAllGoalsQueryHandlerTests|FullyQualifiedName~GetAllWorkItemsWithValidationQueryHandlerTests|FullyQualifiedName~GetDependencyGraphQueryHandlerTests|FullyQualifiedName~GetValidationImpactAnalysisQueryHandlerTests|FullyQualifiedName~GetProductBacklogStateQueryHandlerTests|FullyQualifiedName~GetHealthWorkspaceProductSummaryQueryHandlerTests|FullyQualifiedName~EfWorkItemQueryTests|FullyQualifiedName~ServiceCollectionTests"`

Result:

- `38 / 38` focused tests passed

### Full validation

Succeeded:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Results:

- solution build passed with `0` warnings and `0` errors
- unit test suite passed: `1706 / 1706`
- domain test suite passed: `1 / 1`
- total validated tests passed: `1707 / 1707`

## Risks / Deferred Areas

### Deferred for a later phase

The following remain intentionally postponed:

- goal hierarchy runtime unification
- live work-item discovery abstractions
- revisions/history/state timeline abstractions
- refresh/update command abstractions
- TFS validation/discovery/provider redesign
- sprint/event-ledger/history-driven Work Item analytics

### Residual risks

Residual risk is limited to normal refactor parity concerns:

- future changes could drift if `IWorkItemReadProvider` and `IWorkItemQuery` shared semantics are changed independently
- there is still no golden-data parity harness for pre/post-refactor comparison
- `GetGoalHierarchyQueryHandler` remains on the older provider-shaped path until a later phase narrows its runtime responsibilities

## Final Status

Work Item Query Phase 1 is **complete and stable** for the intended scope.

Delivered in this phase:

- a minimal cache-only analytical boundary: `IWorkItemQuery`
- an EF-backed implementation: `EfWorkItemQuery`
- migration of the safe analytical handlers listed above
- DI registration, focused tests, and full validation
- explicit preservation of live/mock/history/update routes outside the abstraction

Not delivered in this phase:

- a unified Work Item abstraction for every route
- live/TFS/provider redesign
- history/revisions/state timeline abstraction
- goal hierarchy migration

Phase 1 is therefore complete as a **safe cache-only analytical boundary**, not as a full Work Item persistence redesign.
