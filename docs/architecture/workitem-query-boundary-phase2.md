# Work Item Query Boundary Phase 2

## Summary

Phase 2 tightens the Phase 1 Work Item analytical boundary by changing `IWorkItemQuery` from a small-but-coarse storage-shaped interface into a more intent-driven analytical query surface.

Phase 1 introduced:

- `IWorkItemQuery`
- `EfWorkItemQuery`
- migration of the safest analytical handlers away from `IWorkItemReadProvider`

The remaining problem was that the Phase 1 interface still exposed broad primitives:

- `GetAllAsync`
- `GetByAreaPathsAsync`
- `GetByRootIdsAsync`

Those methods were easy to reuse, but they forced handlers to:

- decide product/root/all fallback behavior themselves
- over-fetch and then filter in memory
- rely on hierarchy expansion as an implicit side effect of a generic method name

Phase 2 fixes that by:

- replacing the broad public query shape with intent-driven analytical methods
- moving more scope/filter selection into `EfWorkItemQuery`
- making hierarchy expansion explicit through use-case-specific methods rather than a generic root-expansion API
- removing remaining analytical repository/provider fallback logic from the migrated handlers

This remains a Work Item slice-only, cache-only refinement. No live/TFS redesign, history abstraction, UI work, or cross-slice abstraction was introduced.

## Phase 1 Usage Audit

### Phase 1 interface shape

Phase 1 `IWorkItemQuery` exposed three broad methods:

1. `GetAllAsync(CancellationToken cancellationToken)`
2. `GetByAreaPathsAsync(IReadOnlyList<string> areaPaths, CancellationToken cancellationToken)`
3. `GetByRootIdsAsync(IReadOnlyList<int> rootWorkItemIds, CancellationToken cancellationToken)`

### How those broad methods were used

| Handler | Phase 1 method usage | Phase 1 issue |
|---|---|---|
| `GetAllWorkItemsQueryHandler` | selected between `GetByRootIdsAsync`, `GetByAreaPathsAsync`, and `GetAllAsync` | handler still owned product-scope fallback logic instead of expressing listing intent |
| `GetAllWorkItemsWithValidationQueryHandler` | selected between all three methods | handler still owned product filtering and fallback logic; `goto`-style flow remained because query boundary was too coarse |
| `GetAllGoalsQueryHandler` | selected between all three methods, then filtered `.Where(wi => wi.Type == Goal)` | goal intent was not represented by the query surface; type filtering happened after broad fetch |
| `GetDependencyGraphQueryHandler` | loaded broad scoped data via `GetByRootIdsAsync` or `GetAllAsync` | query-time area/type/id filtering still happened in handler; hierarchy scope was implicit |
| `GetValidationImpactAnalysisQueryHandler` | loaded broad scoped data via `GetByRootIdsAsync` or `GetAllAsync` | query-time area/iteration filtering still happened in handler; handler rebuilt children lookup from broad data |
| `GetProductBacklogStateQueryHandler` | called `GetByRootIdsAsync` | hierarchy expansion was hidden behind a generic method name |
| `GetHealthWorkspaceProductSummaryQueryHandler` | called `GetByRootIdsAsync` | same implicit hierarchy-expansion issue as backlog state |

### Where handlers still did too much in-memory reshaping

The Phase 1 interface reduced direct persistence leakage, but significant intent still lived in handlers:

- `GetAllGoalsQueryHandler` loaded broad snapshot data and filtered to goals in-memory
- `GetDependencyGraphQueryHandler` still applied area-path, work-item-id, and work-item-type filtering after broad fetch
- `GetValidationImpactAnalysisQueryHandler` still applied area-path and iteration-path filtering after broad fetch and rebuilt a children lookup in-memory
- `GetAllWorkItemsQueryHandler` and `GetAllWorkItemsWithValidationQueryHandler` still decided whether to load by product roots, area paths, or full cache snapshot
- `GetProductBacklogStateQueryHandler` and `GetHealthWorkspaceProductSummaryQueryHandler` still relied on `GetByRootIdsAsync` even though their real need was "product backlog analytics source data"

### Where hierarchy expansion was implicit

The Phase 1 `GetByRootIdsAsync` method expanded a hierarchy, but its name was still too generic. That caused two problems:

- handlers had to know that the method returned root descendants, not just roots
- several analytical handlers used the same method name even though their actual intent was very different:
  - all-work-item listing
  - dependency graph source data
  - validation impact source data
  - product backlog analytics source data

So hierarchy expansion was technically encapsulated, but semantically still implicit.

## Interface Refinement

### Old shape

```csharp
public interface IWorkItemQuery
{
    Task<IReadOnlyList<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemDto>> GetByAreaPathsAsync(
        IReadOnlyList<string> areaPaths,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItemDto>> GetByRootIdsAsync(
        IReadOnlyList<int> rootWorkItemIds,
        CancellationToken cancellationToken);
}
```

### New shape

```csharp
public interface IWorkItemQuery
{
    Task<IReadOnlyList<WorkItemDto>> GetWorkItemsForListingAsync(
        IReadOnlyList<int>? productIds,
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemDto>> GetGoalsForListingAsync(
        IReadOnlyList<string>? fallbackAreaPaths,
        CancellationToken cancellationToken);

    Task<DependencyGraphQuerySource> GetDependencyGraphSourceAsync(
        string? areaPathFilter,
        IReadOnlyList<int>? workItemIds,
        IReadOnlyList<string>? workItemTypes,
        CancellationToken cancellationToken);

    Task<ValidationImpactQuerySource> GetValidationImpactSourceAsync(
        string? areaPathFilter,
        string? iterationPathFilter,
        CancellationToken cancellationToken);

    Task<ProductBacklogAnalyticsSource?> GetProductBacklogAnalyticsSourceAsync(
        int productId,
        CancellationToken cancellationToken);
}
```

### New query-source read models

Phase 2 also adds narrow read models where a plain flat list was not enough to express intent:

- `DependencyGraphQuerySource`
  - `ScopedWorkItems`
  - `RelevantWorkItems`
- `ValidationImpactQuerySource`
  - `WorkItems`
  - `ChildrenByParentId`
- `ProductBacklogAnalyticsSource`
  - `ProductId`
  - `WorkItems`

### Why this is more intent-driven

Each public method now answers a specific analytical question instead of exposing generic storage operations:

- `GetWorkItemsForListingAsync` = cache-backed analytical listing scope
- `GetGoalsForListingAsync` = goals listing source data
- `GetDependencyGraphSourceAsync` = dependency graph source selection
- `GetValidationImpactSourceAsync` = validation-impact source selection with explicit hierarchy lookup support
- `GetProductBacklogAnalyticsSourceAsync` = product backlog / health summary source data

This keeps the interface minimal while still reflecting actual migrated handler needs.

## Implementation Changes

### What changed in `EfWorkItemQuery`

`EfWorkItemQuery` now owns more of the analytical selection intent that previously lived in handlers.

Key changes:

1. **Listing scope resolution moved into the store**
   - `GetWorkItemsForListingAsync` now resolves:
     - selected product roots when product IDs are specified
     - all configured product roots when product IDs are not specified
     - area-path fallback when no roots exist and fallback area paths are available
     - full-cache fallback only when neither roots nor fallback area paths exist

2. **Goal filtering moved into the store**
   - `GetGoalsForListingAsync` now filters to `WorkItemType.Goal` inside the query store instead of in the handler

3. **Dependency graph selection moved into the store**
   - `GetDependencyGraphSourceAsync` now applies area-path, work-item-id, and work-item-type filtering inside the store and returns both:
     - the broader scoped set needed for link lookup parity
     - the filtered relevant set used for node generation

4. **Validation-impact filtering moved into the store**
   - `GetValidationImpactSourceAsync` now applies area-path and iteration-path filtering inside the store and returns a prebuilt `ChildrenByParentId` lookup for the filtered analytical scope

5. **Product backlog source resolution moved into the store**
   - `GetProductBacklogAnalyticsSourceAsync` now owns:
     - product existence check
     - backlog-root lookup
     - empty-source handling when a product exists but has no configured backlog roots
     - hierarchy-scoped item loading for backlog-state and health-summary analytics

### Internal helpers added

Phase 2 keeps the public boundary intent-driven but still reuses small internal helpers:

- `WorkItemHierarchySelection`
  - internal helper for descendant expansion over cached `WorkItemEntity` rows
- internal scope-loading helpers inside `EfWorkItemQuery`
  - `LoadListingScopeEntitiesAsync`
  - `LoadAllProductScopedOrAllEntitiesAsync`
  - `LoadConfiguredRootIdsAsync`
  - `LoadHierarchyByRootIdsAsync`
  - `LoadAllEntitiesAsync`

This keeps hierarchy and scope logic internal without introducing a broader new domain abstraction.

### SQLite safety

The refinement remains SQLite-safe because:

- it still uses `AsNoTracking()`
- it still materializes inside the store
- it does not introduce `IQueryable` leakage
- it does not add problematic `DateTimeOffset` translation patterns
- the hierarchy-expansion step still happens over materialized cache rows, not translated recursive SQL

## Handler Refactors

### Updated handlers

The following handlers were updated to use the refined intent-driven interface:

- `GetAllWorkItemsQueryHandler`
- `GetAllWorkItemsWithValidationQueryHandler`
- `GetAllGoalsQueryHandler`
- `GetDependencyGraphQueryHandler`
- `GetValidationImpactAnalysisQueryHandler`
- `GetProductBacklogStateQueryHandler`
- `GetHealthWorkspaceProductSummaryQueryHandler`

### How handler intent became clearer

#### `GetAllWorkItemsQueryHandler`

Before:

- loaded products
- chose between roots / area paths / all cache

Now:

- asks for `GetWorkItemsForListingAsync(...)`
- still supplies profile fallback area paths, but no longer owns product-root selection logic

#### `GetAllWorkItemsWithValidationQueryHandler`

Before:

- loaded all or selected products
- chose between roots / area paths / all cache
- used `goto` flow to skip fallback logic

Now:

- asks for `GetWorkItemsForListingAsync(query.ProductIds, profileAreaPaths, ...)`
- only keeps validation orchestration and DTO projection

#### `GetAllGoalsQueryHandler`

Before:

- loaded broad scoped data
- filtered goals in-memory

Now:

- asks for `GetGoalsForListingAsync(profileAreaPaths, ...)`
- no longer expresses goal filtering as a handler-side `.Where(...)`

#### `GetDependencyGraphQueryHandler`

Before:

- loaded broad scoped data
- filtered by area/type/id in-memory
- built a work-item map over a broader set than the filtered nodes

Now:

- asks for `GetDependencyGraphSourceAsync(...)`
- still performs graph orchestration, but no longer owns the analytical selection logic

#### `GetValidationImpactAnalysisQueryHandler`

Before:

- loaded broad scoped data
- filtered area/iteration in-memory
- rebuilt children lookup after filtering

Now:

- asks for `GetValidationImpactSourceAsync(...)`
- still runs validation and recommendation logic, but receives already-filtered analytical source data plus explicit child lookup support

#### `GetProductBacklogStateQueryHandler`

Before:

- fetched product via repository
- used generic `GetByRootIdsAsync`

Now:

- asks for `GetProductBacklogAnalyticsSourceAsync(productId, ...)`
- no longer needs `IProductRepository`
- handler logic is now strictly backlog-state orchestration and score computation

#### `GetHealthWorkspaceProductSummaryQueryHandler`

Before:

- fetched product via repository
- used generic `GetByRootIdsAsync`

Now:

- asks for the same `GetProductBacklogAnalyticsSourceAsync(productId, ...)`
- no longer needs `IProductRepository`
- handler logic is now strictly summary computation over the returned analytical product scope

## Hierarchy Decision

Phase 2 keeps hierarchy expansion **inside the query store**, but no longer exposes it through an overly generic public method name.

### Decision

- hierarchy expansion remains in `EfWorkItemQuery`
- the descendant-expansion algorithm is factored into the narrow internal helper `WorkItemHierarchySelection`
- public callers no longer ask for generic `GetByRootIdsAsync`
- public callers now ask for explicit use-case-oriented methods that happen to rely on hierarchy expansion internally

### Why this approach was chosen

This satisfies the Phase 2 requirement without introducing a large new abstraction:

- hierarchy behavior remains encapsulated
- the method names now express analytical intent
- no new cross-slice or domain-level hierarchy framework was introduced
- existing cache-backed behavior remains stable and testable

## Fallback Elimination

### Removed from migrated handlers

Phase 2 removes remaining analytical fallback behavior from the migrated handlers themselves.

Removed from handlers:

- repository fallback
- provider fallback
- product-root selection logic in handlers
- broad `GetAllAsync` / `GetByAreaPathsAsync` / `GetByRootIdsAsync` branching in handlers

Specific removals:

- `GetValidationImpactAnalysisQueryHandler` no longer falls back through broad analytical load logic in-handler
- `GetAllWorkItemsQueryHandler` no longer branches across three generic query methods
- `GetAllWorkItemsWithValidationQueryHandler` no longer implements its own product/root/all fallback flow
- `GetAllGoalsQueryHandler` no longer owns product/root/all fallback flow
- `GetProductBacklogStateQueryHandler` and `GetHealthWorkspaceProductSummaryQueryHandler` no longer use `IProductRepository` for analytical source resolution

### Fallback retained, but internalized

A full-cache fallback still exists **inside `EfWorkItemQuery`** for some analytical methods.

That fallback remains only to preserve current behavior when:

- no configured product roots exist
- and no fallback area paths are available

This is still necessary for behavior parity in the current slice. The important Phase 2 change is that this fallback is no longer implemented separately in multiple handlers.

### Compile-time compatibility change outside the refined boundary

`CachedWorkItemReadProvider` was adjusted for compile-time and runtime compatibility because the public `IWorkItemQuery` surface no longer exposes the old broad methods.

That change is intentionally limited:

- the legacy provider contract still needs broad methods for out-of-scope provider-shaped paths
- `CachedWorkItemReadProvider` therefore resumed owning those broad provider operations directly
- hierarchy expansion duplication was avoided by sharing the new internal `WorkItemHierarchySelection` helper

This does **not** change the architectural direction of Phase 2; it only preserves the separate legacy provider contract while the analytical boundary becomes more intent-driven.

## Validation

### Focused validation

Succeeded:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-restore --nologo --filter "FullyQualifiedName~GetAllWorkItemsQueryHandlerTests|FullyQualifiedName~GetAllGoalsQueryHandlerTests|FullyQualifiedName~GetAllWorkItemsWithValidationQueryHandlerTests|FullyQualifiedName~GetDependencyGraphQueryHandlerTests|FullyQualifiedName~GetValidationImpactAnalysisQueryHandlerTests|FullyQualifiedName~GetProductBacklogStateQueryHandlerTests|FullyQualifiedName~GetHealthWorkspaceProductSummaryQueryHandlerTests|FullyQualifiedName~EfWorkItemQueryTests|FullyQualifiedName~ServiceCollectionTests"`

Result:

- `39 / 39` focused tests passed

### Full validation

Succeeded:

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

Results:

- solution build passed with `0` warnings and `0` errors
- unit test suite passed: `1707 / 1707`
- domain test suite passed: `1 / 1`
- total validated tests passed: `1708 / 1708`

## Risks / Deferred Areas

The following areas remain intentionally out of scope after Phase 2:

- `GetGoalHierarchyQueryHandler`
- live/TFS/provider redesign
- revisions/history/state timeline abstraction
- refresh/update command abstraction
- area paths from TFS and goals from TFS live fetch
- sprint execution / sprint metrics / event-ledger paths
- any global/shared abstraction across slices

Residual risks:

- the older provider-shaped cache/live abstraction still exists for non-analytical paths, so there is still some long-term drift risk between the analytical boundary and legacy provider behavior
- `CachedWorkItemReadProvider` now again owns broad provider operations for compatibility, though the analytical boundary itself is more explicit
- backlog-state and health-summary handlers still perform substantial in-memory score/classification orchestration, which is appropriate for now but still leaves room for future slice-local cleanup if explicitly requested

## Security Summary

- `code_review` was run for this change and returned no review comments.
- `codeql_checker` reported `0` alerts, but C# analysis was skipped in this environment because the analysis database is too large.
- No new dependencies were introduced.
- No persistence write paths, live TFS access, UI behavior, or command/update behavior were changed.

## Final Status

Work Item Query Phase 2 is **complete and stable** for the requested scope.

Delivered in Phase 2:

- a refined, intent-driven `IWorkItemQuery` surface
- more analytical scope/filter selection moved into `EfWorkItemQuery`
- explicit hierarchy usage by use-case-specific method names instead of a generic public root-expansion method
- removal of remaining analytical fallback orchestration from the migrated handlers
- focused and full validation on a green deterministic baseline

Not delivered in Phase 2:

- live/provider redesign
- history/timeline abstraction
- goal hierarchy migration
- any cross-slice shared analytical abstraction

Phase 2 therefore completes the intended refinement: the Work Item analytical query boundary is now not only cache-only, but also materially more intent-driven and less reliant on broad fetch primitives.
