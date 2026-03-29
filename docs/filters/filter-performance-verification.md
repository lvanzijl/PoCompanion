# Filter Performance Verification

## Summary

Phase 1 + 2 delivered the intended **correctness fixes** for cached pipeline selection and build-quality branch fallback, and it removed the explicit sprint product-loading N+1.  
However, the performance outcome is mixed:

- **Sprint product loading improved** from an estimated `N + 1` DB-call pattern to a fixed two-step path (1 product batch query + 1 work-item query).
- **Pipeline metrics/runs correctness is verified** for the validated cached scenarios, but query count is still **one run query per pipeline definition**.
- **Build-quality now loads fewer build rows**, but query count increased because build selection is now executed **per definition**.
- **PR metrics still contains a clear N+1/fan-out pattern** and was not improved by Phase 1 + 2.

Net result: the changes are **safe to continue with**, but they are **not a blanket performance win**. The correctness-critical fixes are real; the remaining work is still substantial.

---

## N+1 status

### SprintScopedWorkItemLoader

**Status:** partial improvement; one N+1 removed, one broad-read remains.

#### Product loading

- Current code batch-loads selected products via `GetProductsByIdsAsync(...)` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintScopedWorkItemLoader.cs`.
- Repository implementation uses a single `Contains(...)` query in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductRepository.cs`.

**Measured current shape (temporary local harness):**

- product batch query commands: **1**
- root work-item load commands: **1**

**Before vs after**

- before: approximately **selected product count + 1** DB calls
  - one `GetProductByIdAsync(...)` per selected product
  - one work-item load
- after: **2** DB calls regardless of selected product count

**Conclusion:** the sprint **product-loading N+1 is fixed**.

#### Work-item loading

- `GetWorkItemsByRootIdsQueryHandler` still delegates to `IWorkItemReadProvider.GetByRootIdsAsync(...)`.
- Cached implementation in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedWorkItemReadProvider.cs` still loads **all cached work items** and computes descendants in memory.

**Measured current shape (temporary local harness):**

- rows materialized from DB: **5**
- rows actually used after descendant filtering: **4**

That sample is intentionally tiny; the important point is the shape:

- query count is only **1**
- data volume is still **global table size**, not scoped descendant size

**Conclusion:** the explicit product N+1 is gone, but sprint root loading still has **broad over-fetch**, not a true scoped hierarchy query.

#### Area-path filtering

- `SprintScopedWorkItemLoader` still applies an additional in-memory area-path prefix filter after loading.
- `CachedWorkItemReadProvider.GetByAreaPathsAsync(...)` also performs area-path filtering at provider level.

**Conclusion:** no new regression was introduced, but **duplicate/in-memory filtering still exists**.

---

### PR metrics

**Status:** N+1/fan-out still exists.

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/PullRequests/GetPullRequestMetricsQueryHandler.cs` still does:

1. one scoped PR list load
2. then, per PR:
   - `GetIterationsAsync(...)`
   - `GetCommentsAsync(...)`
   - `GetFileChangesAsync(...)`

So the current request shape is:

- **1 + (3 × PR count)** provider calls

This is still the main PR-side N+1/fan-out pattern noted in the audit.

**Conclusion:** **PR metrics N+1 still exists**.

---

### Pipeline metrics / runs

**Status:** correctness improved; per-definition query pattern remains.

`CachedPipelineReadProvider.GetRunsForPipelinesAsync(...)` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs` still does:

1. one definition lookup query
2. one run query **per definition**

For metrics, `GetPipelineMetricsQueryHandler` then adds:

3. one scoped pipeline-definition lookup via `GetByIdsAsync(...)`

So the current shape is:

- provider only: **1 + definition count**
- metrics handler end-to-end: **2 + definition count**

**Before vs after**

- query count: effectively **unchanged**
- definition data volume: **improved**
  - before: loaded **all** pipeline definitions for mapping
  - after: loads **only pipelines present in scoped results**

**Conclusion:** there is still an **O(definition count)** query pattern, but the wasteful “load all definitions” path is removed.

---

### Build-quality scope loader

**Status:** no row-materialization N+1, but query count now scales with definition count.

`BuildQualityScopeLoader` now loads builds per definition in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/BuildQuality/BuildQualityScopeLoader.cs`.

Measured owner-scoped sample (temporary local harness, 2 definitions):

- total DB commands: **7**
- candidate builds in window before branch filtering: **3**
- selected builds after query-time branch filtering: **2**

**Before vs after (same shape, estimated from code)**

- before:
  - product ids
  - products
  - pipeline definitions
  - **one combined builds query**
  - test runs
  - coverages
  - ≈ **6 DB commands**
- after:
  - product ids
  - products
  - pipeline definitions
  - **one builds query per definition**
  - test runs
  - coverages
  - ≈ **5 + definition count** DB commands

With 2 definitions, that becomes **7** commands, which matches the measurement.

**Conclusion:** build-quality row volume improved, but query count **got worse**. This is acceptable for the correctness-critical phase, but it is a real trade-off and should not be described as a pure performance win.

---

## Pipeline correctness

**Status:** verified for the validated cached scenarios.

### Case A — more than 100 runs, mixed branches, recent runs outside window

Validated using:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CachedPipelineReadProviderSqliteTests.cs`
- a temporary local harness against the current repository code

Scenario:

- one pipeline definition with:
  - **110 newer disqualifying runs**
    - mixed wrong-branch and outside-window rows
  - **20 valid default-branch runs** inside the time window

Measured result:

- commands: **2**
  - one definition lookup
  - one filtered run query
- returned rows: **20**
- expected valid rows: **20**

This confirms:

1. `CreatedDateUtc <= rangeEnd` is applied before truncation
2. default-branch filtering is applied before truncation
3. `Take(top)` no longer drops valid in-window/default-branch runs because invalid rows consumed the budget first

### Case B — default branch missing

Validated using:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
- temporary local harness for cached pipeline runs

Scenario:

- pipeline definition with `DefaultBranch == null`
- mixed-branch runs inside window

Measured result:

- commands: **2**
- returned rows: **3** with mixed branches present

This confirms:

- **missing default branch still means “include all branches”**

### Edge cases

- Case-insensitive branch matching is still implemented via `ToLower()` comparison on both pipeline and build-quality paths.
- Pipeline metrics and runs handlers still apply `PipelineFiltering.ApplyRunScope(...)` after provider selection, so correctness is reinforced even though some filtering is now pushed earlier.

**Conclusion:** pipeline correctness fix is **verified** for the cached-provider scenarios covered here.

---

## Build-quality correctness

**Status:** verified, with an important query-count trade-off.

Validated using:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
- temporary local harness against the current repository code

### Default-branch filtering

Observed sample:

- builds in window before branch filter: **3**
- selected builds after query-time branch filter: **2**
- selected build IDs: `1001, 2001`

This confirms the old feature-branch build (`1002`) is now excluded **before** dependent test-run and coverage queries.

### Missing-default fallback

`ScopeLoader_WithMissingDefaultBranch_IncludesAllBuildsForThatPipeline` verifies:

- when `DefaultBranch` is missing, builds are **not** excluded by branch

### Trade-off

- correctness is improved
- downstream child-fact loading is narrower
- but DB command count grows with pipeline-definition count

**Conclusion:** build-quality correctness fix is **verified**, but the performance outcome is **mixed rather than universally better**.

---

## Data volume analysis

### Sprint work items

- Product lookup volume is now scoped correctly.
- Root hierarchy loading still materializes **all cached work items** before descendant pruning.

**Reality check:** query count is fine; **row volume is still excessive** for broad caches.

### Pipelines

- Definition loading for metrics is now scoped to pipelines that actually have scoped runs.
- Cached run queries apply branch and time-window filtering before `Take(top)`.

**Reality check:** row volume is improved in the corrected cached scenarios; the main remaining cost is **per-definition query count**.

### Build-quality

- Build rows are reduced before test-run and coverage loads.
- Child-fact loads now receive only filtered build IDs.

**Reality check:** data volume improved, but it was traded for **more DB commands**.

### PR metrics

- No data-volume improvement was made here.
- Per-PR enrichment still causes repeated small reads/calls.

**Conclusion:** the biggest remaining over-fetch problems are still:

1. sprint root hierarchy loading
2. PR metrics enrichment fan-out
3. build-quality query-count growth with many definitions

---

## SQLite safety

**Status:** no immediate SQLite failure observed in the modified paths.

Evidence:

- targeted Release build passed
- focused validation tests passed on SQLite-backed unit tests
- temporary local harness executed the modified provider/loader queries successfully on SQLite in-memory
- no client-eval warnings were surfaced in the observed build/test output

### Safe points verified

- product batch load uses simple `Contains(...)`
- pipeline filters use `CreatedDateUtc` and simple branch equality normalization
- build-quality filters use `FinishedDateUtc` and simple branch equality normalization
- `AsNoTracking()` additions are read-only only

### Remaining cautions

- `ToLower()`-based branch filtering translated and ran, but it may be less index-friendly than a normalized-column approach
- sprint area-path matching and root hierarchy traversal still rely on a combination of translated prefix checks and in-memory filtering
- `CachedWorkItemReadProvider.GetByRootIdsAsync(...)` remains SQLite-safe only because it does **less in SQL**, not because it is efficient

**Conclusion:** no new SQLite-translation breakage was detected, but the sprint hierarchy path remains intentionally in-memory and expensive.

---

## Regressions

**Status:** no regression was detected in the focused validation performed here.

Validated test evidence:

- targeted Prompt 15 baseline:
  - Release build passed
  - **36 focused tests passed**
- focused regression suite:
  - sprint metrics / execution / trend
  - delivery calibration / delivery metrics
  - PR metrics
  - **37 focused tests passed**

### Explicit checks

- **Sprint metrics unchanged:** no failure observed in focused sprint regression tests
- **Delivery metrics unchanged:** no failure observed in focused delivery regression tests
- **PR metrics unchanged:** no failure observed in focused PR metrics tests

### Important nuance

Pipeline/build-quality output is expected to differ in the corrected edge cases:

- runs formerly lost due to post-`Take(top)` filtering are now retained
- off-branch builds formerly loaded then discarded are now filtered earlier

Those are intended correctness changes, not regressions.

---

## Conclusion

**Safe to continue**, but with caveats.

What is genuinely better now:

- sprint product-loading N+1 is removed
- pipeline top-N correctness fix is verified
- build-quality branch fallback/correctness fix is verified
- no regression was detected in focused sprint/delivery/PR tests

What is still not good enough:

- sprint root hierarchy loading still over-fetches the entire work-item cache
- PR metrics still has a `1 + (3 × PR count)` fan-out
- pipeline still uses one run query per definition
- build-quality now uses **more queries** than before, even though it loads fewer build rows

This means the Phase 1 + 2 work is **safe and correctness-positive**, but the repository should **not** treat it as “performance solved.” The next phase should continue only with the understanding that the largest remaining costs are still present.
