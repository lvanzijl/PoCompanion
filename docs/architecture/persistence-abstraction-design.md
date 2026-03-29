# Persistence Abstraction Design for Analytical Query Side

## Summary

The analytical side should move toward **cache-only analytical read abstractions**, not generic CRUD repositories and not provider-aware services.

The current code already has the right architectural preconditions for this split:

- analytical reads are deterministic and cache-backed
- live/TFS reads are already centralized elsewhere
- hidden fallback behavior has been removed

That means the analytical side can now depend on abstractions that expose **cached analytical facts and scoped read models**, while EF Core, SQLite-specific query shape, `PoToolDbContext`, and table-level composition stay below the boundary.

The recommended model is:

- keep **repositories** for narrow entity-oriented configuration/reference access where that is already working well
- introduce **analytical query stores / projection readers** for slice-specific cached analytics
- keep **domain/application logic** focused on aggregation rules, classification rules, rollups, and DTO shaping
- keep **shared loaders/helpers** only when they centralize scope loading for multiple consumers, and make them depend on analytical stores rather than `PoToolDbContext`

This should be migrated incrementally. Do **not** force one pattern across every slice.

## Current Persistence Leakage

The analytical side currently leaks persistence concerns in three recurring ways:

1. **Handlers directly query `PoToolDbContext`**
   - query orchestration, table joins, and projection loading live inside handlers
2. **Large analytical services create their own DbContext scope and query many tables directly**
   - projection logic and persistence logic are mixed in the same class
3. **Some read providers centralize EF access but still expose storage-shaped reads instead of analytical reads**
   - they hide some plumbing, but not the analytical query boundary

### Pull Requests

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs` | `GetPullRequestInsightsQueryHandler` | Loads `TfsConfigs`, `Teams`, `PullRequests`, `PullRequestIterations`, `PullRequestComments`, and `PullRequestFileChanges` directly from `_context`, then groups and computes analytics in the handler. | The handler knows table layout and batch-loading shape, so any persistence change forces handler edits. The analytical use case is not isolated from EF/Core schema details. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs` | `GetPrDeliveryInsightsQueryHandler` | Directly queries `Teams`, `Sprints`, `PullRequests`, `PullRequestIterations`, `PullRequestFileChanges`, `PullRequestWorkItemLinks`, and the entire `WorkItems` table for hierarchy traversal. | Delivery classification logic is mixed with storage traversal. The handler is effectively acting as both query store and analytical service. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs` | `GetPrSprintTrendsQueryHandler` | Direct DbContext access for sprint selection, PR loading, file-change loading, and comment loading. | Sprint trend computation depends on raw table composition instead of an analytical PR reader. Similar query shape is repeated across PR handlers. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestsByWorkItemIdQueryHandler.cs` | `GetPullRequestsByWorkItemIdQueryHandler` | Directly joins `PullRequestWorkItemLinks` and `PullRequests` in the handler. | Even a simple analytical lookup bypasses an abstraction boundary, so handlers remain coupled to link-table shape. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPullRequestReadProvider.cs` | `CachedPullRequestReadProvider` | Centralizes EF access, but still exposes storage-shaped reads (`GetAll`, iterations, comments, file changes) rather than analytical slices such as insights, sprint trends, or delivery mapping. | This is useful infrastructure, but it does not yet give handlers a stable analytical abstraction. Handlers still need to know how to combine multiple cached tables. |

### Pipelines

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `GetPipelineInsightsQueryHandler` | Directly loads `Sprints`, `Products`, `PipelineDefinitions`, and `CachedPipelineRuns`; also contains run-window filtering and default-branch filtering logic in `LoadRunsAsync`. | The handler knows both the entity model and the performance/query-shape details of cached pipeline storage. That makes the analytical logic hard to reuse and hard to migrate to a different provider. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs` | `CachedPipelineReadProvider` | Centralizes EF calls for definitions and runs, but methods are still generic run/definition reads. Per-pipeline branch filtering and per-definition run selection are storage-aware implementation details inside the provider. | Better than direct handler access, but still not a slice-oriented analytical abstraction. Pipeline insight handlers still need to orchestrate raw cached facts themselves. |

### Build Quality

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs` | `BuildQualityScopeLoader` | Directly queries `Products`, `PipelineDefinitions`, `CachedPipelineRuns`, `TestRuns`, and `Coverages`; also constructs internal records (`ProductRecord`, `PipelineDefinitionRecord`, `BuildRecord`) and applies branch/date/product scoping itself. | This is a strong candidate for an analytical store, but today it still knows raw storage shape and EF-specific composition. It is doing both persistence assembly and analytical scope composition. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/BuildQuality/GetBuildQualitySprintQueryHandler.cs` | `GetBuildQualitySprintQueryHandler` | Direct sprint lookup via `_context.Sprints` before delegating to `BuildQualityScopeLoader`. | Even though most build-quality loading is centralized, handlers still depend on DbContext for scope metadata. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/BuildQuality/GetBuildQualityPipelineDetailQueryHandler.cs` | `GetBuildQualityPipelineDetailQueryHandler` | Same direct sprint lookup pattern as above. | The slice is close to a clean abstraction, but the last step is incomplete. |

### Work Items

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` | `GetWorkItemActivityDetailsQueryHandler` | Directly loads `Products`, `ResolvedWorkItems`, `WorkItems`, and `ActivityEventLedgerEntries`, then reconstructs descendant scope and activity history in the handler. | The handler is responsible for storage traversal, scope reconstruction, and analytical mapping, which makes it fragile and hard to test independently of EF. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintScopedWorkItemLoader.cs` | `SprintScopedWorkItemLoader` | Mixes `IWorkItemReadProvider`, `IProductRepository`, mediator queries, and in-memory descendant filtering. It also contains `RepositoryBackedWorkItemReadProvider`, an internal bridge over repository access. | This is a clear smell that the slice lacks a clean analytical read abstraction. The loader is compensating for repository/provider gaps and ends up owning scope semantics. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedWorkItemReadProvider.cs` | `CachedWorkItemReadProvider` | Provides cached reads, but the most analytical method (`GetByRootIdsAsync`) materializes the full work-item set and performs descendant expansion in memory. | The provider hides EF, but it does not yet expose purpose-built analytical reads such as resolved product scope, hierarchy slices, or event-backed analytical inputs. |

### Sprint / Delivery / Trend Analytics

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | `GetSprintMetricsQueryHandler` | Uses repositories and `SprintScopedWorkItemLoader`, but still directly loads `ActivityEventLedgerEntries` from `_context` for iteration/state reconstruction. It also has a secondary constructor that builds a loader composition itself. | This is mixed abstraction usage: repository + mediator + loader + DbContext in one handler. The handler still owns persistence composition for sprint history. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | Directly loads `Sprints`, `Products`, `ResolvedWorkItems`, `WorkItems`, `ActivityEventLedgerEntries`, and additional team sprint data. | Rich domain services are already present, but the handler still performs all analytical fact loading itself, so the boundary between query-side persistence and domain logic is blurred. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | Uses `SprintTrendProjectionService`, but still directly queries `Sprints`, `Products`, and cache-state tables. | The handler mixes projection orchestration with direct persistence reads instead of depending on a cohesive trend/projection reader. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `GetPortfolioDeliveryQueryHandler` | Directly loads `SprintMetricsProjections`, `Products`, and `Sprints`, then combines them with `SprintTrendProjectionService`. | Projection-backed analytics still leak projection-table knowledge into handlers. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | Directly loads `Sprints` and `PortfolioFlowProjections`. | Projection consumption is simple here, but still coupled to EF and projection entity names. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | Creates service scopes, resolves `PoToolDbContext` internally, loads products, sprints, resolved work items, work items, activity ledger entries, projections, and more; also persists projection rows. The same class also computes feature/epic progress and other analytical outputs. | This is the biggest analytical hotspot. Compute logic, orchestration logic, and persistence logic are co-located, which makes the abstraction boundary unclear and future provider migration harder. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs` | `PortfolioFlowProjectionService` | Same pattern: creates its own scope, queries products, sprints, resolved work items, work items, and several ledger-event subsets, then writes projections. | A projection engine should depend on preloaded analytical facts or a dedicated store, not own DbContext acquisition and table composition itself. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioSnapshotSelectionService.cs` | `PortfolioSnapshotSelectionService` | Exposes a useful interface, but the implementation still encodes all snapshot grouping, canonicalization, and query composition directly over `PoToolDbContext`. | This is not wrong, but it shows the pattern to aim for: keep the interface, hide EF completely, and ensure consumers depend only on portfolio snapshot semantics. |

### Shared Loaders / Helpers / Filtering

| File | Class | Persistence leakage | Why it is a problem |
| --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PullRequestFilterResolutionService.cs` | `PullRequestFilterResolutionService` | Directly queries `ProductTeamLinks`, `Repositories`, `PullRequests`, and `Sprints`. | This is legitimate application-layer scope resolution, but it means scope logic is still tightly bound to EF. Analytical handlers then inherit that coupling. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PipelineFilterResolutionService.cs` | `PipelineFilterResolutionService` | Directly queries `Products`, `Repositories`, `PipelineDefinitions`, and `Sprints`. | Same pattern: filter resolution is useful, but still storage-bound. It should remain separate from analytical stores, not be merged into them. |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DeliveryFilterResolutionService.cs` | `SprintFilterResolutionService`, `DeliveryFilterResolutionService` | Direct EF access for product and sprint scope resolution. | These services are acceptable as application-layer scope resolvers, but they should stop bleeding into analytical persistence choices. |

## Target Abstraction Model

The analytical side should use four distinct concepts.

### 1. Repository abstractions

**Purpose**

Repositories should stay narrow and entity-oriented.

They are a good fit for:

- products
- teams
- sprints
- settings/configuration
- profile/config metadata
- simple cached entity retrieval where the consumer truly needs entity-like records

**They should not become the default abstraction for analytical queries.**

Why:

- repositories tend to mirror tables or aggregates
- analytical use cases often cut across multiple cached tables
- forcing all analytics through repositories usually creates either anemic repositories or duplicated orchestration above them

### 2. Query / read-model abstractions

**Purpose**

These are the recommended primary abstraction for the analytical side.

They should expose:

- cached analytical facts
- scoped read models
- pre-composed projection inputs
- projection outputs already persisted in cache

Good examples:

- pull-request insights facts for a scope
- pipeline run facts for a sprint window
- build-quality scope selections
- sprint execution analytical facts
- portfolio flow projection rows

These abstractions should hide:

- EF query composition
- DbSet names
- joins/subqueries/includes
- SQLite/PostgreSQL tuning
- raw table shape

### 3. Domain / application analytical logic

**Purpose**

This layer should own:

- canonical domain rules
- aggregation
- rollups
- classification
- percentiles
- delivery/sprint/work-item semantics
- DTO mapping when the mapping is analytical rather than storage-driven

It should depend on:

- analytical stores / readers
- repositories only for narrow reference data when needed
- pure domain services

It should **not** depend on:

- `PoToolDbContext`
- EF entities
- provider-specific query tricks

### 4. Shared loaders / helpers

**Purpose**

A loader should exist only when multiple handlers/services need the same multi-step fact-loading behavior.

A loader is appropriate when it:

- loads a reusable analytical scope
- combines multiple analytical stores/readers into one slice-level input
- normalizes filter/scope inputs for a family of handlers

A loader is **not** appropriate when it merely compensates for missing abstractions or mixes persistence composition with domain logic.

### How handlers/services should depend on them

Recommended dependency order:

1. **Handler -> filter/scope resolver**
2. **Handler -> analytical query store / projection reader**
3. **Handler -> pure analytical/domain service**
4. **Handler -> DTO/result mapping**

Handlers should call repositories only for:

- simple reference metadata
- stable configuration data
- entity-like records already modeled as repositories today

Handlers should call analytical query stores when:

- more than one cached table is involved
- analytical scope needs pre-composed facts
- projection tables or ledger tables are part of the read path
- the query is slice-specific rather than entity-specific

A loader should exist when:

- multiple handlers in the same slice need the same scoped analytical facts
- the shared behavior is stable enough to deserve one contract
- keeping it as a handler-local helper would duplicate analytical loading logic

## Slice-by-Slice Recommendation

### Pull Requests

**Recommended style:** `Read model query service / analytical store`

Why:

- PR analytics span multiple cached tables (`PullRequests`, iterations, comments, file changes, work-item links)
- the existing `IPullRequestReadProvider` already centralizes basic reads
- the remaining problem is not entity access, but analytical composition

Recommended direction:

- keep `IPullRequestReadProvider` for generic cached/live read behavior
- add a separate analytical abstraction for cache-backed PR analytics
- move PR-specific batch loading and cross-table assembly into that abstraction

### Pipelines

**Recommended style:** `Hybrid: provider + analytical query store`

Why:

- the slice already has `IPipelineReadProvider` for generic reads
- insight/build-focused handlers need windowed, branch-aware, scoped analytical facts
- pipeline analytics are still mostly read-model oriented, not aggregate-root oriented

Recommended direction:

- keep `IPipelineReadProvider` for general run/definition reads
- introduce a pipeline analytical store for sprint-window or repository-scoped analytical facts

### Build Quality

**Recommended style:** `Analytical projection reader / scoped fact loader`

Why:

- build quality already behaves like a scoped fact selection problem
- `BuildQualityScopeLoader` is very close to the right shape
- the missing step is to make it a true abstraction boundary rather than an EF-aware service

Recommended direction:

- promote the existing concept into an interface-led analytical store
- keep `IBuildQualityProvider` pure and computation-only
- keep sprint lookup out of handlers if possible

### Work Items

**Recommended style:** `Hybrid`

Why:

- simple work-item retrieval still benefits from repository/provider patterns
- analytical reads need resolved membership, hierarchy traversal, and activity ledger joins
- one single repository abstraction will be too weak for the analytical use cases

Recommended direction:

- keep entity-style work-item repository/provider for basic cached reads
- introduce one or more analytical stores for hierarchy/activity-based reads
- let loaders or services consume those stores instead of composing raw persistence access themselves

### Sprint / Delivery / Trends

**Recommended style:** `Projection reader + analytical fact store + pure compute services`

Why:

- this is the most domain-heavy slice
- it uses both snapshots and event history
- several handlers consume either persisted projections or projection computations

Recommended direction:

- split data loading from compute
- expose persisted projections through read-model readers
- expose recompute inputs through analytical fact stores
- keep sprint/delivery domain rules in pure services

## Boundary Rules

These rules should become explicit architectural guidance for the analytical side.

1. **Analytical handlers must not depend directly on `PoToolDbContext`.**
2. **Analytical handlers must not compose EF queries over cached tables.**
3. **Analytical services that implement domain logic must not create DI scopes just to obtain `PoToolDbContext`.**
4. **Repositories must not call TFS or any live gateway.**
5. **Analytical stores must expose cached analytical models only.**
6. **Analytical logic must not know about provider mode, live fallback, or cache-vs-live branching.**
7. **Filter/scope resolvers may depend on persistence, but they must stay separate from analytical stores.**
8. **Projection compute services must accept materialized facts, not `IQueryable` or EF entities.**
9. **No analytical abstraction may return `IQueryable`.**
10. **SQL/EF-specific tuning belongs below the abstraction boundary.**
11. **Branch normalization, date coercion, provider-specific function choices, and include strategies belong in store implementations, not handlers.**
12. **If a query primarily answers a slice-specific analytical question, it belongs in a query store/reader, not a generic repository.**
13. **If a component both loads cached facts and computes analytics, it should be split unless the compute is trivial.**
14. **Shared loaders may orchestrate multiple stores, but they must not reintroduce table knowledge.**
15. **Analytical correctness is more important than abstraction purity; if a boundary would hide important semantics, keep the semantics explicit at the analytical-store contract.**

## Migration Readiness

### Best first candidates

1. **Build quality scoped loading**
   - Best readability payoff
   - Already concentrated in `BuildQualityScopeLoader`
   - Semantic risk is relatively low compared with hierarchy-heavy work-item analytics
   - Existing `IBuildQualityProvider` already separates computation from selection

2. **Pull request analytical reads**
   - Strongly bounded slice
   - Existing provider pattern can coexist with a new analytical store
   - Clear hotspots in `GetPullRequestInsightsQueryHandler`, `GetPrDeliveryInsightsQueryHandler`, and `GetPrSprintTrendsQueryHandler`
   - Good payoff because several handlers repeat the same cached table orchestration

3. **Pipeline analytical reads**
   - Similar readiness to PRs
   - Existing `IPipelineReadProvider` gives a stable starting point
   - Main remaining issue is branch-aware/sprint-window analytical composition

4. **Projection-backed portfolio reads**
   - `GetPortfolioProgressTrendQueryHandler` and parts of `GetPortfolioDeliveryQueryHandler` are relatively straightforward readers over already-computed projections
   - Good later step once slice-specific readers are defined

### Slices that should NOT be abstracted first

1. **`SprintTrendProjectionService`**
   - Highest coupling
   - mixes compute, persistence, projection writes, and feature/epic progress reads
   - large surface area means high regression risk

2. **`PortfolioFlowProjectionService`**
   - event-heavy and membership-history heavy
   - depends on both resolved current state and historical ledger events
   - abstraction work is valuable, but should follow a smaller successful migration first

3. **Hierarchy-heavy work-item flow (`GetSprintExecutionQueryHandler`, `GetSprintMetricsQueryHandler`, `SprintScopedWorkItemLoader`)**
   - depends on hierarchy traversal, resolved membership, sprint semantics, and state/iteration event reconstruction
   - this area still contains mixed abstraction usage, so introducing the wrong boundary too early could freeze a bad design

4. **Any slice still depending on broad “load everything then filter in memory” behavior for correctness**
   - abstraction is premature until the contract can express the real analytical input cleanly

## Candidate Interfaces

These are candidate contracts for the next implementation steps. Names can change, but the roles should stay similar.

### `IBuildQualityAnalyticsStore`

**Purpose**

Expose build-quality facts for an already resolved analytical scope.

**Likely methods**

- `Task<BuildQualityScopeSelection> GetForSprintAsync(int productOwnerId, IReadOnlyList<int> productIds, DateTime windowStartUtc, DateTime windowEndUtc, int? repositoryId, int? pipelineDefinitionId, CancellationToken cancellationToken)`
- `Task<BuildQualityScopeSelection> GetForWindowAsync(IReadOnlyList<int> productIds, DateTime? windowStartUtc, DateTime? windowEndUtc, int? repositoryId, int? pipelineDefinitionId, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `Products`, `PipelineDefinitions`, `CachedPipelineRuns`, `TestRuns`, and `Coverages`

**Boundary shape**

- hides EF
- centralizes analytical query shape
- can reuse most of the current `BuildQualityScopeLoader` logic

### `IPullRequestAnalyticsStore`

**Purpose**

Expose cache-backed PR analytical facts for insights, delivery classification, and sprint trends.

**Likely methods**

- `Task<PullRequestInsightFactSet> GetInsightsFactsAsync(PullRequestEffectiveFilter filter, CancellationToken cancellationToken)`
- `Task<PullRequestDeliveryFactSet> GetDeliveryFactsAsync(PullRequestEffectiveFilter filter, CancellationToken cancellationToken)`
- `Task<PullRequestSprintTrendFactSet> GetSprintTrendFactsAsync(IReadOnlyList<int> sprintIds, PullRequestEffectiveFilter filter, CancellationToken cancellationToken)`
- `Task<IReadOnlyList<PullRequestLinkDto>> GetByWorkItemIdAsync(int workItemId, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over PR tables and work-item link/hierarchy cache

**Boundary shape**

- hides EF
- centralizes multi-table query shape
- should return materialized fact records, not entities

### `IPipelineAnalyticsStore`

**Purpose**

Expose scoped cached pipeline facts for analytical handlers.

**Likely methods**

- `Task<PipelineInsightFactSet> GetInsightFactsAsync(PipelineEffectiveFilter filter, CancellationToken cancellationToken)`
- `Task<IReadOnlyList<PipelineRunFact>> GetRunsForWindowAsync(IReadOnlyList<int> productIds, IReadOnlyList<string> repositoryNames, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken)`
- `Task<IReadOnlyList<PipelineDefinitionFact>> GetDefinitionsForScopeAsync(IReadOnlyList<int> productIds, IReadOnlySet<string> repositoryNames, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `Products`, `PipelineDefinitions`, and `CachedPipelineRuns`

**Boundary shape**

- hides EF
- centralizes branch-aware run selection
- complements, not replaces, `IPipelineReadProvider`

### `IWorkItemAnalyticsStore`

**Purpose**

Provide hierarchy- and activity-aware cached work-item facts for analytical handlers.

**Likely methods**

- `Task<WorkItemActivityFactSet?> GetActivityDetailsAsync(int productOwnerId, IReadOnlyList<int> productIds, int rootWorkItemId, DateTimeOffset? rangeStartUtc, DateTimeOffset? rangeEndUtc, CancellationToken cancellationToken)`
- `Task<SprintWorkItemFactSet> GetSprintFactsAsync(int productOwnerId, SprintEffectiveFilter filter, CancellationToken cancellationToken)`
- `Task<IReadOnlyList<CanonicalWorkItemSnapshot>> GetHierarchyScopeAsync(IReadOnlyList<int> productIds, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `ResolvedWorkItems`, `WorkItems`, and `ActivityEventLedgerEntries`

**Boundary shape**

- should hide EF completely
- should expose analytical facts already aligned to domain semantics where possible

### `ISprintProjectionReader`

**Purpose**

Read persisted sprint metrics projections without leaking projection-table shape.

**Likely methods**

- `Task<IReadOnlyList<SprintMetricsProjectionDto>> GetForProductsAndSprintsAsync(IReadOnlyList<int> productIds, IReadOnlyList<int> sprintIds, CancellationToken cancellationToken)`
- `Task<IReadOnlyDictionary<int, SprintInfoDto>> GetSprintInfoAsync(IReadOnlyList<int> sprintIds, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `SprintMetricsProjections` and `Sprints`

**Boundary shape**

- hides EF
- mostly centralizes query shape rather than deep domain logic

### `IPortfolioFlowProjectionReader`

**Purpose**

Read persisted portfolio flow projections for trend handlers.

**Likely methods**

- `Task<IReadOnlyList<PortfolioFlowProjectionInput>> GetTrendInputsAsync(IReadOnlyList<int> productIds, IReadOnlyList<int> sprintIds, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `PortfolioFlowProjections`

**Boundary shape**

- lightweight reader over projection tables
- hides EF and entity names from handlers

### `ISprintTrendFactStore`

**Purpose**

Provide the raw cached facts required to recompute sprint trend projections.

**Likely methods**

- `Task<SprintTrendFactSnapshot> GetProjectionFactsAsync(int productOwnerId, IReadOnlyList<int> sprintIds, IReadOnlyList<int>? productIds, CancellationToken cancellationToken)`
- `Task<FeatureProgressFactSnapshot> GetFeatureProgressFactsAsync(int productOwnerId, IReadOnlyList<int>? productIds, DateTime? sprintStartUtc, DateTime? sprintEndUtc, int? sprintId, CancellationToken cancellationToken)`

**Likely backing implementation**

- EF Core over `Products`, `Sprints`, `ResolvedWorkItems`, `WorkItems`, and `ActivityEventLedgerEntries`

**Boundary shape**

- should hide EF but keep analytical semantics explicit
- this is a larger-scope interface and should not be implemented first

## Incremental Migration Strategy

### Phase 1 — Establish the rule and one narrow abstraction

- Introduce one analytical-store interface in a single slice
- Register the EF-backed implementation in API DI
- Change only the targeted handlers to depend on the abstraction
- Keep existing DTOs, domain services, and external behavior unchanged

### Phase 2 — Move shared query shape, not business rules

For the selected slice:

- move table composition
- move batch loading
- move branch/date/product scoping that is purely persistence-related
- do **not** move domain formulas unless they were only there because of the persistence coupling

This avoids a big-bang rewrite and limits semantic risk.

### Phase 3 — Collapse temporary duplication quickly

During migration, temporary duplication may appear in the old handler and the new store.

Rule:

- accept duplication only briefly while proving parity
- once the new store-backed path is verified, remove the old direct DbContext path immediately

Do not leave both long-term.

### Phase 4 — Add parity-focused tests at the seam

For each migrated slice:

- keep existing handler tests green
- add tests around the new abstraction only where current coverage is weak
- prefer integration-style tests against the cache-backed implementation for query-shape confidence
- keep domain-service tests focused on analytical rules, not EF composition

### Phase 5 — Tackle larger slices only after a successful small migration

Once one of build quality / PR / pipeline is complete:

- codify the boundary rules
- use the first migration as the pattern library
- then move to the next slice

### Practical migration rules

- no big-bang rewrite
- no cross-slice abstraction introduced “just in case”
- no generic `IAnalyticsRepository` umbrella
- no returning entities or `IQueryable`
- no merging filter-resolution logic into analytical stores
- keep persisted projections readable before attempting to refactor projection recomputation

## PostgreSQL Impact

This abstraction design will help a future SQLite -> PostgreSQL migration, but only in specific ways.

### What becomes easier

1. **Handlers and domain services stop knowing about EF query shape**
   - fewer places need edits for provider-specific translation differences
2. **SQLite/PostgreSQL-specific tuning stays in EF-backed store implementations**
   - date filtering, branch normalization, and query plan adjustments become localized
3. **Projection readers and analytical stores become the migration seam**
   - you can keep contracts stable while changing indexes, query shape, and provider-specific implementation details
4. **Cross-slice testing becomes clearer**
   - parity tests can compare the same analytical contract across different DB providers

### What remains hard

1. **Large event-heavy analytical queries are still structurally complex**
   - PostgreSQL may execute them better, but abstraction alone does not simplify the underlying analytical problem
2. **Hierarchy reconstruction and ledger-event joins remain expensive patterns**
   - especially in sprint/delivery/portfolio flow slices
3. **Any logic that currently depends on loading large cached sets into memory will still need redesign for scale**
   - changing the engine does not remove the need for better analytical fact contracts
4. **Projection recomputation services still need a cleaner split**
   - until `SprintTrendProjectionService` and `PortfolioFlowProjectionService` are separated into fact loading + pure compute, database migration risk remains higher there

### Realistic conclusion

This design does **not** make PostgreSQL migration automatic.
It does make it safer by reducing the number of application-layer classes that know about current persistence shape.

## Final Recommendation

**Prompt 30 should implement build quality first, by turning `BuildQualityScopeLoader` into an interface-led analytical store and updating the two build-quality handlers to depend on that abstraction instead of `PoToolDbContext` plus an EF-aware loader.**

Why this should go first:

- it is the cleanest slice boundary already visible in the code
- it has low semantic risk compared with sprint/work-item/trend logic
- it delivers immediate readability and architectural payoff
- it creates a concrete pattern for later PR and pipeline migrations
- it avoids prematurely freezing a weak abstraction in the harder hierarchy/event-driven slices

If Prompt 30 wants the absolute lowest-risk follow-up after that, the next slice should be **pull requests**, using a dedicated PR analytical store rather than expanding repositories into cross-table analytical orchestrators.
