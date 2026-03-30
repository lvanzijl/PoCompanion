# Filter Performance Audit and Optimization Plan

## Summary

The canonical filter system is functionally coherent, but the heaviest remaining performance risk is no longer filter resolution itself; it is the shape of the downstream data-access paths that execute after scope resolution. The main issues are:

- sprint-scoped work-item acquisition still includes an N+1 product lookup and broad hierarchy materialization (`PoTool.Api/Services/SprintScopedWorkItemLoader.cs:32-79`, `PoTool.Api/Services/CachedWorkItemReadProvider.cs:103-149`)
- pipeline metrics/runs still do repeated per-definition queries and apply some scope constraints too late, after `Take(top)` and materialization (`PoTool.Api/Services/CachedPipelineReadProvider.cs:154-181`, `PoTool.Api/Services/PipelineFiltering.cs:25-45`)
- PR metrics still use per-PR enrichment calls, and PR delivery insights still loads the full work-item cache to resolve hierarchy (`PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs:37-48`, `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:160-168`)
- build-quality still materializes builds before default-branch filtering (`PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:116-158`)
- cross-slice delivery/sprint projection services are already batch-oriented, but they remain heavy for wide product+sprint scopes because they intentionally load large event and hierarchy sets to preserve canonical sprint semantics (`PoTool.Api/Services/SprintTrendProjectionService.cs:117-185`, `PoTool.Api/Services/SprintTrendProjectionService.cs:540-776`)

Existing documentation already flagged several of these risks, especially sprint scoped loading, build-quality branch filtering, and pipeline insights branch filtering (`docs/analysis/filter-validation-report.md:255-319`). The remaining workstream should therefore focus on:

1. removing obvious N+1 and broad-read patterns
2. moving safe filters earlier **only when SQLite can translate and execute them reliably**
3. avoiding “clever” rewrites that would destabilize canonical sprint, delivery, or hierarchy semantics

## Sensitive Paths

| Area | Entry point | Downstream services / loaders | Why this path is performance-sensitive |
| --- | --- | --- | --- |
| PR slice | `PullRequestsController.GetMetrics`, `GetFiltered`, `GetInsights`, `GetDeliveryInsights` (`PoTool.Api/Controllers/PullRequestsController.cs`) | `PullRequestFilterResolutionService` → PR handlers → `IPullRequestReadProvider` / `PullRequestFiltering` | Metrics still fans out per PR for iterations/comments/files; filtered list applies several selections after provider materialization; delivery insights traverses work-item hierarchy for linked PRs (`PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs:37-48`, `PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs:36-42`, `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:149-168`). |
| Pipeline slice | `PipelinesController.GetMetrics`, `GetRunsForProducts`, `GetInsights` (`PoTool.Api/Controllers/PipelinesController.cs`) | `PipelineFilterResolutionService` → pipeline handlers → `CachedPipelineReadProvider` / `LivePipelineReadProvider` / `PipelineFiltering` | Cached path performs one query per pipeline definition, then applies default-branch and end-of-window filtering in memory; insights materializes runs before branch filtering; live definition acquisition still fans out by repository (`PoTool.Api/Services/CachedPipelineReadProvider.cs:154-181`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:243-290`, `PoTool.Api/Services/LivePipelineReadProvider.cs:106-177`). |
| Delivery slice | `MetricsController.GetPortfolioProgressTrend`, `GetCapacityCalibration`, `GetPortfolioDelivery`, `GetHomeProductBarMetrics` (`PoTool.Api/Controllers/MetricsController.cs`) | `DeliveryFilterResolutionService` → projection queries / `SprintTrendProjectionService` / summary services | Most product+sprint filters already hit EF early, but delivery snapshot/trend paths still become heavy for broad multi-sprint ranges because they combine projection reads with feature-progress reconstruction (`PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs:64-95`, `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs:68-99`, `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs:66-96`). |
| Sprint slice | `MetricsController.GetSprintMetrics`, `GetSprintExecution`, `GetSprintTrendMetrics` (`PoTool.Api/Controllers/MetricsController.cs`) | `SprintFilterResolutionService` → `SprintScopedWorkItemLoader` → activity/state/history queries → `SprintTrendProjectionService` | Sprint correctness depends on commitment, first-done, churn, and spillover rules, so these paths intentionally load hierarchy and history. The performance sensitivity comes from how wide the initial work-item scope is and how much history is then pulled for that scope (`PoTool.Api/Services/SprintScopedWorkItemLoader.cs:26-79`, `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs:115-159`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs:107-190`, `docs/rules/sprint-rules.md:5-97`). |
| Build-quality supporting paths | `BuildQualityController.GetRolling`, `GetSprint` (`PoTool.Api/Controllers/BuildQualityController.cs`) | `BuildQualityScopeLoader` → `IBuildQualityProvider` | Candidate builds are selected by product/window in EF, but default-branch filtering still happens after materialization, which can amplify downstream test-run and coverage loads (`PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:111-158`). |
| Cross-slice shared loaders | `SprintScopedWorkItemLoader`, `SprintTrendProjectionService`, data-source-aware lazy providers | `IProductRepository`, `IWorkItemReadProvider`, `DataSourceAwareReadProviderFactory` | Shared loaders shape the real data volume for multiple slices. Their cost compounds when a request spans many products, pipelines, or sprints (`PoTool.Api/Services/SprintScopedWorkItemLoader.cs:26-79`, `PoTool.Api/Services/SprintTrendProjectionService.cs:92-185`, `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:28-71`). |

## Findings

### Verified findings

| ID | Location | Issue | Why it is expensive or risky | Correctness-neutral to optimize? | Severity | Optimization safety | SQLite feasibility |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F1 | `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:32-42` | Selected products are loaded one-by-one through `GetProductByIdAsync(...)`. | This is a classic N+1 lookup pattern. Cost grows linearly with selected product count before any work-item loading even begins. | Yes. Batch-loading product metadata preserves semantics. | High | Safe without semantic risk | Safe on SQLite |
| F2 | `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:44-51` → `PoTool.Api/Handlers/WorkItems/GetWorkItemsByRootIdsQueryHandler.cs:44-46` → `PoTool.Api/Services/CachedWorkItemReadProvider.cs:103-149` | Root-based sprint loading reaches `GetByRootIdsAsync`, which loads **all** cached work items and finds descendants in memory. | The request is scoped by selected root IDs, but the cache path still materializes the whole work-item table. Cost therefore grows with total cache size, not just selected roots. | Not easily. The obvious server-side alternative is recursive hierarchy traversal. | High | Needs careful validation | Poor fit for SQLite |
| F3 | `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:53-79`, `PoTool.Api/Services/CachedWorkItemReadProvider.cs:64-83` | Area-path filtering is partly duplicated and partly in-memory. | When only area paths are selected, the provider already filters by area path, yet the loader still runs a second in-memory prefix pass. When products are selected, the area-path filter is delayed until after work-item materialization. | Partially. The duplication is safe to question, but provider implementations do not currently share identical semantics. | Medium | Needs careful validation | Possibly feasible on SQLite |
| F4 | `PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs:37-48` | PR metrics loads base PRs, then loads iterations, comments, and file changes separately for each PR. | That is 3 extra provider calls per PR. In cache mode this becomes repeated DB queries; in live mode it becomes repeated TFS calls. | Yes for cache-mode batching; mixed-mode optimization needs care. | High | Needs careful validation | Safe on SQLite for cache mode |
| F5 | `PoTool.Api/Handlers/PullRequests/GetFilteredPullRequestsQueryHandler.cs:36-42`, `PoTool.Api/Services/PullRequestFiltering.cs:43-92` | The filtered PR list only scopes by repository/date at provider level; iteration path, author, and status filters are applied after materialization. | Broad repository/date windows can materialize far more PRs than the final list requires. | Usually yes, but current provider abstraction mixes cache and live mode. | Medium | Needs careful validation | Safe on SQLite for cache mode |
| F6 | `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:149-168` | PR delivery insights batch-loads PR links, but then loads the full `WorkItems` cache into a dictionary to resolve ancestors. | Work-item hierarchy resolution is correct, but the current approach scales with the entire work-item cache even if only a few linked items are relevant. | Not safely with a simple EF rewrite; ancestor closure is the hard part. | High | High semantic risk | Poor fit for SQLite |
| F7 | `PoTool.Api/Services/CachedPipelineReadProvider.cs:154-181`, `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:34-40`, `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:33-40`, `PoTool.Api/Services/PipelineFiltering.cs:25-45` | Cached pipeline metrics/runs do one query per definition, `Take(top)` per definition, then apply end-of-window and default-branch filters in memory. | It is both a performance and correctness risk. Runs that are outside the effective end time or on non-default branches can consume the per-definition top-N budget before in-memory filtering removes them. | Yes in principle, but only if branch-null behavior and case-insensitive matching are preserved. | Critical | Needs careful validation | Possibly feasible on SQLite |
| F8 | `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:243-290` | Pipeline insights pushes time-window filtering into EF, but still applies default-branch filtering after materialization. | Safer than F7 because the time window is already server-side, but it still reads more runs than needed whenever feature-branch traffic is high. | Yes, if default-branch semantics stay identical. | Medium | Needs careful validation | Possibly feasible on SQLite |
| F9 | `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:45-47`, `PoTool.Api/Services/CachedPipelineReadProvider.cs:31-46`, `PoTool.Api/Services/LivePipelineReadProvider.cs:34-41` | Pipeline metrics loads **all** pipeline definitions just to map scoped run results back to names/types. | This is an unnecessary broad read on the cache path and an unnecessary all-pipeline TFS call on the live path. | Yes. Only scoped definitions are needed. | Medium | Safe without semantic risk | Safe on SQLite |
| F10 | `PoTool.Api/Services/LivePipelineReadProvider.cs:106-177` | Live pipeline definition acquisition still fans out by repository and loads all repositories to resolve a single repository ID. | This is not a SQLite issue, but it is still part of the filtering execution path when live mode is used. Repository count directly drives remote call count. | Yes. | Medium | Safe without semantic risk | Not applicable to SQLite |
| F11 | `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:111-158` | Build-quality loads builds inside the window, then applies default-branch filtering in memory before loading test runs and coverage by build ID. | More build rows than necessary can cascade into larger dependent test-run and coverage reads. | Yes in principle, but only if “no stored default branch means include all builds” remains intact. | High | Needs careful validation | Possibly feasible on SQLite |
| F12 | `PoTool.Api/Services/SprintTrendProjectionService.cs:117-185`, `PoTool.Api/Services/SprintTrendProjectionService.cs:540-776`, `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:62-120`, `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs:83-96` | Cross-slice projection/feature-progress paths intentionally batch-load large resolved-item and history sets, then recompute per sprint and product. | These paths are already better than N+1, but they are inherently heavy for broad scopes. Over-optimizing them risks breaking canonical sprint, delivery, and estimation behavior. | Only partially. Some small read-shape improvements are safe; deep rewrites are not. | Medium | High semantic risk | Poor fit for SQLite for aggressive server-side rewrites |

### Existing documented warnings that still matter

The previous validation report already captured three of the most important issues and they remain valid starting points for this workstream:

- sprint scoped work-item loading N+1 + in-memory filtering (`docs/analysis/filter-validation-report.md:257-273`)
- build-quality branch filtering after materialization (`docs/analysis/filter-validation-report.md:275-289`)
- pipeline insights branch filtering after materialization (`docs/analysis/filter-validation-report.md:291-304`)

## SQLite Constraints

SQLite must be treated as a first-class constraint here, not as an implementation detail.

### What is already safe in this codebase

The repository rules already require UTC `DateTime` columns for queryable timestamps and explicitly forbid server-side `DateTimeOffset` predicates/sorts on SQLite (`docs/rules/ef-rules.md:130-149`). The filtering paths generally follow that rule:

- pipeline queries use `CreatedDateUtc` / `FinishedDateUtc` and compute UTC bounds outside LINQ (`PoTool.Api/Services/CachedPipelineReadProvider.cs:146-173`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:250-270`)
- build-quality queries use `FinishedDateUtc` (`PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:118-124`)
- PR cache queries use `CreatedDateUtc` (`PoTool.Api/Services/CachedPullRequestReadProvider.cs:36-40`, `PoTool.Api/Services/PullRequestFiltering.cs:28-38`)

That means the safest optimization space is:

- simple `IN (...)` filters on IDs
- equality filters on repository / branch / status when semantics are known
- UTC date range predicates
- `AsNoTracking()` on read-only paths
- reducing repeated materialization or repeated round-trips without changing semantics

### What SQLite can likely handle safely

1. **Batch ID lookups**
   - Example: replacing repeated product-by-id calls with one `Products.Where(p => ids.Contains(p.Id))`
   - Why safe: simple primary-key/foreign-key filtering; no advanced translation required

2. **UTC range predicates before materialization**
   - Example: pushing `RangeEndUtc` into pipeline cached queries before `Take(top)`
   - Why safe: this uses existing UTC columns and simple comparison operators

3. **Scoped definition lookups instead of “load all then dictionary”**
   - Example: pipeline metrics loading only scoped definitions instead of `GetAllAsync()`
   - Why safe: no semantic change, no complex SQL shape

4. **Read-only no-tracking improvements**
   - Example: delivery/sprint projection reads that do not mutate returned entities
   - Why safe: purely EF tracking overhead reduction

### What is risky on SQLite even if it looks attractive in theory

1. **Hierarchy closure / recursive ancestor-descendant loading**
   - Relevant to sprint root loading and PR delivery insights hierarchy resolution (`PoTool.Api/Services/CachedWorkItemReadProvider.cs:114-149`, `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:160-168`)
   - Why risky: the natural SQL answer is a recursive CTE or precomputed closure table. EF Core does not make recursive CTE work a comfortable or low-risk path here, and SQLite execution quality is highly shape-dependent.

2. **Per-pipeline default-branch filtering in one “smart” query**
   - Relevant to pipeline metrics/runs and insights (`PoTool.Api/Services/PipelineFiltering.cs:35-43`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:278-287`)
   - Why risky: each pipeline definition can have a different default branch, and null/empty default branches currently mean “include all runs.” A single large SQL rewrite would need either joins, OR chains, or shape-specific unions, all of which are harder to validate on SQLite.

3. **Area-path prefix matching with multiple prefixes**
   - Relevant to sprint scoped work-item filtering (`PoTool.Api/Services/SprintScopedWorkItemLoader.cs:70-76`, `PoTool.Api/Services/CachedWorkItemReadProvider.cs:74-80`)
   - Why risky: prefix matching can translate, but exact boundary semantics, case handling, and multi-prefix selectivity are easy to get wrong. The repository-backed implementation also uses different matching semantics from the cached read provider (`PoTool.Api/Repositories/WorkItemRepository.cs:60-70`).

4. **Forcing complex grouping/aggregation into SQL only because it “should be faster”**
   - Relevant to PR insights and calibration/trend aggregation
   - Current code explicitly materializes then groups in memory in places where that is safer for EF compatibility (`PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:95-126`)
   - Why risky: SQLite is fine for basic grouping, but complex projected grouping can become fragile or harder to reason about than the current batched in-memory summaries

5. **Window-function or per-group top-N rewrites**
   - Relevant to pipeline run selection
   - Why risky: SQLite supports window functions, but pushing this through EF Core in a maintainable, reviewable shape is significantly riskier than preserving the current “one definition at a time” model and tightening predicates before `Take(top)`

### Client-side / in-memory work that is still the right choice

Some current in-memory work is justified and should not be optimized away prematurely:

- PR insights grouping after batch loads, because it avoids fragile grouping translation and remains bounded by the already-scoped PR set (`PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs:95-149`)
- sprint/domain recomputation that needs first-done, commitment, spillover, and hierarchy propagation to match canonical rules (`docs/rules/sprint-rules.md:27-97`, `docs/rules/propagation-rules.md:46-90`, `docs/rules/metrics-rules.md:21-66`)

The workstream should therefore prefer **narrower inputs to correct in-memory logic** over rewriting canonical logic into fragile SQLite SQL.

## Quick Wins

| Quick win | Likely files | Expected benefit | Why it should be safe | Why it is safe specifically on SQLite + EF Core |
| --- | --- | --- | --- | --- |
| Batch-load selected sprint products instead of looping `GetProductByIdAsync(...)` | `PoTool.Api/Services/SprintScopedWorkItemLoader.cs`, likely `PoTool.Core/Contracts/IProductRepository.cs` and its implementation if a batch repository method is preferred | Removes N+1 repository lookups from every multi-product sprint request | Product identity and backlog roots are already resolved before work-item loading; batching does not change scope semantics | SQLite handles `Contains` on integer IDs well; no complex translation is required |
| Add `AsNoTracking()` consistently to read-only delivery/sprint projection reads that do not update entities | `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | Small but pervasive reduction in EF tracking overhead on repeated filter-driven reads | These handlers only read data and project it; they do not mutate tracked entities | `AsNoTracking()` is provider-agnostic and does not depend on SQLite query-shape behavior |
| Stop loading all pipeline definitions in pipeline metrics when only scoped definitions are needed | `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs`, `PoTool.Api/Services/CachedPipelineReadProvider.cs`, possibly `PoTool.Api/Services/LivePipelineReadProvider.cs` | Avoids broad cache reads / all-pipelines live calls just to map scoped run results | Only metadata for pipelines that already have scoped runs is needed | A scoped `IN` query over pipeline definition IDs is simple and index-friendly on SQLite |
| Pre-index build-quality in-memory selections by product/build once after loading | `PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs` | Reduces repeated per-product scans over builds, test runs, and coverages after materialization | This is purely an in-memory reshaping improvement after the authoritative dataset has already been selected | No SQLite translation involved; it reduces CPU work without affecting SQL |
| Trim duplicate distinct/order work where scope is already normalized | `PoTool.Api/Services/DeliveryFilterResolutionService.cs`, `PoTool.Api/Services/SprintFilterResolutionService.cs`, call sites that repeatedly normalize product/sprint ID lists | Small reduction in repeated list normalization and easier reasoning about hot paths | Normalization logic is already semantically stable; deduplicating repeated work should not change results | SQLite is irrelevant here because the gain is pre-query CPU reduction |

## High-Risk Areas

### 1. Pipeline top-N correctness with late branch/end filtering

**Why it is risky**

`GetPipelineMetricsQueryHandler` and `GetPipelineRunsForProductsQueryHandler` currently request `top: 100` from the provider, then `PipelineFiltering.ApplyRunScope(...)` removes out-of-window and non-default-branch runs in memory (`PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:34-40`, `PoTool.Api/Services/PipelineFiltering.cs:25-45`). Any optimization here must preserve:

- per-definition top-N behavior
- default-branch matching that is case-insensitive
- current fallback behavior where missing/blank default branch means “include all runs”

**What must be preserved**

- exact effective filter semantics
- no undercounting due to branch/window filtering happening after truncation
- no change in inclusion for definitions without stored default branches

**How it should be validated later**

- explicit regression tests with a mix of:
  - default-branch and feature-branch runs
  - more than 100 recent runs per definition
  - past sprint windows where many newer runs exist outside the target window
- SQLite-backed tests, not only in-memory tests

**Why SQLite makes it harder**

A single “smart” SQL query that handles per-definition default branches and per-definition top-N is much riskier on SQLite/EF Core than tightening each per-definition query before `Take(top)`.

### 2. Sprint hierarchy loading by root IDs

**Why it is risky**

Current sprint root-based loading is broad, but it is semantically simple: load everything, then include descendants in memory (`PoTool.Api/Services/CachedWorkItemReadProvider.cs:114-149`). Replacing that with server-side recursion would be a substantial behavioral change in a hierarchy-sensitive area.

**What must be preserved**

- descendant closure from selected roots
- resilience to missing/skipped hierarchy levels
- compatibility with current cached and repository-backed paths

**How it should be validated later**

- hierarchy-focused tests with deep chains, skipped levels, and mixed products
- SQLite-backed tests if any recursive SQL is attempted
- comparison against current output for representative product scopes

**Why SQLite makes it harder**

Recursive CTE support exists in SQLite, but EF Core does not make this a low-risk refactor. This is a poor candidate for “just push it server-side.”

### 3. PR delivery insights hierarchy resolution

**Why it is risky**

The handler currently loads the entire work-item cache so it can resolve ancestor chains reliably (`PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs:160-168`). Narrowing that load requires a reliable way to fetch linked items plus ancestors without breaking classification into DeliveryMapped / Bug / Disturbance / Unmapped.

**What must be preserved**

- exact hierarchy classification rules
- unmapped diagnostics behavior
- correct resolution of feature/epic ancestry

**How it should be validated later**

- PR link fixtures that cover missing cache entries, direct PBI links, bug links, and multiple ancestor chains
- before/after comparisons of category counts and top offenders

**Why SQLite makes it harder**

Ancestor closure is the hard part, not the initial PR link lookup. Again, this tends to push toward recursive SQL or a denormalized closure structure.

### 4. Sprint and delivery projection recomputation

**Why it is risky**

`SprintTrendProjectionService` loads resolved items, snapshots, state history, iteration history, activity events, and existing projections in batches, then recomputes per sprint/product (`PoTool.Api/Services/SprintTrendProjectionService.cs:117-185`). That shape is heavy, but it also mirrors canonical sprint semantics from the domain rules.

**What must be preserved**

- commitment timestamp behavior
- first-done logic
- spillover rules
- product-level hierarchy/estimation semantics

**How it should be validated later**

- existing SQLite sprint tests
- canonical sprint and delivery regression suites
- domain-level fixture comparisons for commitment, churn, and spillover

**Why SQLite makes it harder**

This logic depends on event history and cross-entity aggregation. Forcing it into more SQL would raise both translation risk and semantic risk.

### 5. Build-quality default-branch filtering

**Why it is risky**

The current logic intentionally includes all builds for definitions that do not yet have a stored default branch (`PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs:111-134`). A rewrite that simply joins and filters on `SourceBranch == DefaultBranch` could silently exclude those definitions.

**What must be preserved**

- “no default branch stored” fallback
- product/repository/pipeline scope semantics
- downstream test run and coverage aggregation

**How it should be validated later**

- fixtures with null/empty default branches
- mixed feature-branch and default-branch runs
- SQLite-backed tests over realistic build volumes

**Why SQLite makes it harder**

The difficulty is not the date range; it is the per-definition branch rule plus fallback behavior.

## Recommended Priority Order

### 1. First optimizations

1. **Sprint product lookup batching**
   - Highest confidence quick win
   - High request-frequency impact
   - No semantic ambiguity
   - Safe on SQLite

2. **Pipeline metrics/runs: move end-range and default-branch filtering earlier in the cached path before `Take(top)`**
   - Highest user-visible risk because it affects both cost and possible result quality
   - Must be validated carefully, but the underlying predicates are SQLite-friendly if kept per definition
   - Do **not** start with a giant single-query rewrite

3. **Pipeline metrics: stop loading all definitions when only scoped definitions are required**
   - Small change, low risk
   - Useful immediately after item 2

### 2. Next optimizations

4. **PR metrics: batch cached enrichment data**
   - High value for cache mode
   - Requires careful handling so live mode is not accidentally made worse

5. **Build-quality: reduce late branch filtering and dependent build fan-out**
   - Worth doing after pipeline fixes because the shape is similar
   - Must preserve null-default-branch fallback

6. **Small read-shape improvements in delivery/sprint projection readers**
   - `AsNoTracking()`
   - avoid repeated normalization and avoidable in-memory rescans
   - useful hygiene, but not the main bottleneck

### 3. Later optimizations

7. **PR filtered list: push more local selections into cache-mode SQL where possible**
   - Useful, but less urgent than the metrics and pipeline hotspots
   - Requires care because current provider abstraction spans cache and live mode

8. **Build-quality in-memory grouping/indexing improvements**
   - Low risk
   - Lower user impact than earlier items

### 4. Not worth doing yet

9. **Large server-side rewrites of sprint/delivery projection logic**
   - The current batch model is heavy but semantically safe
   - Optimize inputs first, not the core canonical computation

10. **Aggressive SQL-only rewrites of PR insights grouping**
   - Current batched materialize-then-group pattern is deliberate and SQLite-friendly enough

### 5. Avoid entirely unless storage/query strategy changes

11. **Recursive-SQL rewrites for root/ancestor closure**
   - sprint root hierarchy loading
   - PR delivery insights ancestor resolution

12. **Window-function-heavy, single-query pipeline per-definition top-N rewrites**
   - attractive in theory
   - fragile in EF Core + SQLite
   - harder to review and validate than a simpler per-definition tightening approach

## Success Criteria

The performance workstream should be considered successful when all of the following are true:

1. **No obvious N+1 remains in migrated hot paths**
   - no per-product lookup loops in sprint scoped loading
   - no per-PR cache enrichment loops in the main PR metrics path unless explicitly unavoidable for live mode

2. **No high-severity broad materialization remains where a narrower SQLite-safe query is available**
   - especially pipeline metrics/runs and build-quality build selection

3. **No filter is still applied too late when that lateness can change results**
   - especially default-branch and end-of-window filtering that currently happens after top-N truncation

4. **Canonical semantics remain unchanged**
   - sprint commitment, first-done, churn, spillover, hierarchy propagation, and delivery classification all remain intact
   - output parity is confirmed by targeted regression tests

5. **SQLite-safe query discipline is maintained**
   - no new reliance on fragile `DateTimeOffset` predicates/sorts
   - no forced client-eval escape hatches
   - no optimizations that depend on EF Core/SQLite query shapes known to be brittle

6. **Cross-slice heavy loaders are bounded, even if still partially in-memory**
   - broad inputs are narrowed as early as safely possible
   - unavoidable in-memory stages operate on already-scoped datasets

7. **The remaining unresolved items are only the ones that are intentionally deferred**
   - recursive hierarchy optimizations
   - large SQL rewrites with poor SQLite fit
   - other work explicitly judged to have worse risk than value in the current stack

If those conditions are met, the filtering system will still prioritize correctness, but the remaining performance debt will be limited to areas that are either intentionally deferred or structurally poor fits for EF Core + SQLite.
