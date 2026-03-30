# Work Item Query Boundary Phase 3 — Goal Hierarchy

Date: 2026-03-30  
Repository: `lvanzijl/PoCompanion`  
Scope: cached analytical Goal Hierarchy extraction only  
Status: implemented

## Summary

Phase 3 extracts the **cached analytical Goal Hierarchy read** into the existing `IWorkItemQuery` boundary without pulling any runtime-mode, mock, or live-provider behavior into that boundary.

The change is intentionally small:

- `IWorkItemQuery` now exposes one new intent-driven method: `GetGoalHierarchyAsync(...)`
- `EfWorkItemQuery` now owns the cached hierarchy selection/materialization for goal-root reads
- `GetGoalHierarchyQueryHandler` now makes the split explicit:
  - **mock/runtime branch** stays in the handler
  - **cached analytical branch** goes through `IWorkItemQuery`

This preserves the architectural target established in Phase 1 and Phase 2:

- `IWorkItemQuery` remains cache-only
- `IWorkItemQuery` remains analytical
- `IWorkItemQuery` remains free of live/TFS/mock/runtime-mode logic

## Goal Hierarchy Audit

### Previous `GetGoalHierarchyQueryHandler` behavior

Before this phase, `/api/workitems/goals` still flowed through `GetGoalHierarchyQueryHandler`, which mixed two different responsibilities in one handler:

1. **Runtime/mock behavior**
   - depended on `TfsRuntimeMode`
   - validated mock registration through `TfsRuntimeModeGuard.EnsureExpectedMockFacade(...)`
   - returned mock hierarchy data via `BattleshipMockDataFacade.GetMockHierarchyForGoals(...)` when mock mode was active

2. **Cached/live provider-shaped behavior**
   - depended on `IWorkItemReadProvider`
   - called `GetByRootIdsAsync(...)` for the non-mock branch
   - therefore still relied on a provider abstraction that can switch between cached and live implementations outside the analytical boundary

### What was analytical cache read vs runtime behavior

#### Cache-backed analytical portion

The analytical portion was:

- materializing cached work item snapshots
- expanding the hierarchy from the supplied goal IDs
- returning the root goals plus descendants from the local cache

That behavior was already implemented in cache form by the existing EF/provider stack, but it was not yet expressed through `IWorkItemQuery`.

#### Runtime/mode-specific portion

The non-analytical portion was:

- deciding whether the process is running in mock mode
- validating that mock dependencies are present or absent consistently
- serving mock hierarchy data when mock mode is active

Those behaviors are not cache-query responsibilities and do not belong in `IWorkItemQuery`.

### Important boundary observation

The route `/api/workitems/goals` is already classified as a **cache-only analytical read** by `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`.

That means the handler’s real-data route should use the cache boundary directly. The only remaining explicit non-analytical concern for this handler is the mock-runtime branch.

### Previous abstractions in use

Before this phase, `GetGoalHierarchyQueryHandler` depended on:

- `IWorkItemReadProvider`
- `TfsRuntimeMode`
- `BattleshipMockDataFacade?`
- `ILogger<GetGoalHierarchyQueryHandler>`

It also still accepted `IProductRepository`, but that dependency was unused in the handler and did not contribute to the goal hierarchy path.

## Query Boundary Refinement

### New analytical method

`IWorkItemQuery` now includes:

```csharp
Task<IReadOnlyList<WorkItemDto>> GetGoalHierarchyAsync(
    IReadOnlyList<int> goalIds,
    CancellationToken cancellationToken);
```

### Why this shape was chosen

This is the smallest intent-driven method that matches the actual handler need:

- input = goal root IDs
- output = cached materialized hierarchy snapshots for those roots

It avoids reintroducing a broad storage-shaped API such as a generic public “get by root ids” method on `IWorkItemQuery`.

It also avoids speculative design:

- no extra read model was needed
- no additional generalized hierarchy abstraction was introduced
- no runtime/live/mock switching was moved into the boundary

### Why this still fits the analytical boundary

`GetGoalHierarchyAsync(...)` is acceptable inside `IWorkItemQuery` because it only answers a cache-backed analytical question:

- “Given goal IDs, what cached hierarchy snapshot should analytical consumers use?”

It does **not**:

- access TFS
- inspect runtime mode
- choose between live and cache providers
- depend on `BattleshipMockDataFacade`
- leak `IQueryable`

## Implementation Changes

### What changed in `EfWorkItemQuery`

`EfWorkItemQuery` now implements `GetGoalHierarchyAsync(...)` by:

1. accepting the requested goal IDs
2. reusing the existing internal hierarchy loader `LoadHierarchyByRootIdsAsync(...)`
3. materializing `WorkItemEntity` rows inside the store
4. mapping them to `WorkItemDto`

No new EF translation-heavy behavior was introduced.

### Analytical selection/materialization location

After this change, the cached goal hierarchy selection logic is owned by `EfWorkItemQuery`:

- hierarchy scope expansion remains internal
- EF materialization remains internal
- DTO projection remains internal

That keeps the public query boundary aligned with the Phase 2 design:

- public interface = intent-driven analytical reads
- internal helpers = scope shaping and materialization

### SQLite safety

The implementation remains SQLite-safe because it:

- uses the existing `AsNoTracking()` and materialize-then-expand pattern
- does not introduce recursive SQL translation
- does not introduce `DateTimeOffset` predicates or sorts
- does not leak `IQueryable`

## Handler Refactor

### New `GetGoalHierarchyQueryHandler` split

`GetGoalHierarchyQueryHandler` now depends on:

- `IWorkItemQuery`
- `TfsRuntimeMode`
- `BattleshipMockDataFacade?`
- `ILogger<GetGoalHierarchyQueryHandler>`

The handler logic is now explicit:

1. validate mock/runtime consistency with `TfsRuntimeModeGuard`
2. if mock mode is active:
   - return `BattleshipMockDataFacade.GetMockHierarchyForGoals(...)`
3. otherwise:
   - log that the cached analytical goal hierarchy is being loaded
   - call `IWorkItemQuery.GetGoalHierarchyAsync(...)`

### What became clearer

The separation is now obvious in the handler itself:

- **mock/runtime behavior** is a top-level branch
- **cached analytical reads** are delegated to the query boundary

The handler no longer hides the cached analytical path behind `IWorkItemReadProvider`.

### Compile-time compatibility notes

The handler constructor changed because:

- `IWorkItemReadProvider` was replaced with `IWorkItemQuery`
- unused `IProductRepository` was removed

No DI registration changes were required because `IWorkItemQuery` was already registered as `EfWorkItemQuery`.

## Boundary Preservation

### What intentionally remains outside the query boundary

The following still remain outside `IWorkItemQuery` by design:

- `TfsRuntimeMode`
- `TfsRuntimeModeGuard`
- `BattleshipMockDataFacade`
- any live/TFS provider behavior
- broader `IWorkItemReadProvider` runtime switching

### What was intentionally not redesigned

This phase does **not** redesign or migrate:

- revisions/history/state timeline
- refresh/update commands
- area paths from TFS
- goals from TFS live fetch
- provider-system redesign
- sprint execution / sprint metrics / event-ledger paths
- any cross-slice shared abstraction
- UI behavior

### Behavior preservation

For the cached analytical path, behavior remains equivalent:

- goal IDs still act as hierarchy roots
- descendants are still expanded from cached snapshot data
- return shape remains `IEnumerable<WorkItemDto>`

For the mock path, behavior also remains unchanged:

- mock mode still returns `BattleshipMockDataFacade.GetMockHierarchyForGoals(...)`
- guard behavior still fails fast when mock registration is inconsistent

## Validation

### Focused tests added/updated

Updated/added tests:

- `PoTool.Tests.Unit/Services/EfWorkItemQueryTests.cs`
  - `GetGoalHierarchyAsync_ReturnsHierarchyScopedToSpecifiedGoalIds`
- `PoTool.Tests.Unit/Handlers/GetGoalHierarchyQueryHandlerTests.cs`
  - `Handle_InRealMode_UsesCachedGoalHierarchyQuery`
  - `Handle_InMockMode_UsesMockFacadeHierarchy`
  - `Handle_InMockModeWithoutFacade_ThrowsInvalidOperationException`

### Commands run

Baseline before changes:

```bash
dotnet restore PoTool.sln --nologo
dotnet build PoTool.sln --configuration Release --no-restore --nologo
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal
dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal
```

Result:

- restore succeeded
- Release build succeeded
- `PoTool.Tests.Unit`: 1707 passed
- `PoTool.Core.Domain.Tests`: 1 passed

Focused validation after the Goal Hierarchy change:

```bash
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --nologo -v minimal --filter "FullyQualifiedName~GetGoalHierarchyQueryHandlerTests|FullyQualifiedName~EfWorkItemQueryTests"
```

Result:

- 9 focused tests passed

Final full validation was then run after implementation and documentation updates.

## Risks / Deferred Areas

### Remaining deferred area

This phase deliberately does **not** remove `IWorkItemReadProvider` from unrelated work item handlers. That broader provider/runtime split remains outside the scope of Goal Hierarchy Phase 3.

### Small remaining coupling

`GetGoalHierarchyQueryHandler` still contains explicit runtime branching because mock mode is intentionally not part of the analytical query boundary. That is acceptable and required by the prompt.

### No new ambiguity introduced

The resulting split is clearer than before:

- `IWorkItemQuery` = cached analytical goal hierarchy read
- handler = runtime/mock orchestration

There is no longer ambiguity about whether the cached goal hierarchy path belongs to the general provider system.

## Final Status

The cached analytical Goal Hierarchy path is now abstracted correctly.

Specifically:

- cached goal hierarchy reads now go through `IWorkItemQuery`
- `EfWorkItemQuery` owns the cached hierarchy selection/materialization
- runtime/mock behavior remains outside the query boundary
- no live/TFS/mock logic was moved into `IWorkItemQuery`
- the change stayed small, SQLite-safe, and consistent with Phase 1 and Phase 2
