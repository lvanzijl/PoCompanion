# OData Ingestion Fix Plan

This plan aligns ingestion behavior with validator/client semantics where appropriate, while preserving:
- incremental ingestion safety,
- bounded memory,
- and **multi-product aggregated scope**.

## Guiding invariants
1. Aggregate all configured product roots into one allowed WorkItemId set **before** segmentation.  
   (`PoTool.Api/Services/RevisionIngestionService.cs:1910-1930`)
2. Use stable OData ordering/cursor semantics `(ChangedDate, WorkItemId, Revision)` for deterministic progression.  
   (`PoTool.Integrations.Tfs/Clients/ODataRevisionQueryBuilder.cs:111-114`, `RealODataRevisionTfsClient.cs:461-475`)
3. Avoid uncontrolled memory growth: keep bounded page/window processing.
4. Treat retrieval/persistence deltas as first-class diagnostics.

---

## Phase 0 — Instrumentation & observability hardening

### Code changes
1. Add explicit per-window counters to run summary:
   - `ScopedRetrievedCount`
   - `ScopedInWindowCount`
   - `PersistedCount`
   - `DroppedOutsideWindowCount`
   - `DroppedDuplicateCount`
   - `DroppedMissingRequiredCount`
   - `DroppedDbConstraintCount`
2. Promote drop-accounting from debug-only to always-on summary at window end.
3. Add warning threshold when `ScopedRetrievedCount > 0 && PersistedCount == 0` for N consecutive pages/windows.

### Files likely touched
- `PoTool.Api/Services/RevisionIngestionService.cs` (window summary and diagnostic aggregates)
- (optional) `PoTool.Integrations.Tfs/Diagnostics/*` if summary DTOs are centralized

### Tests
- Update/add unit tests in `PoTool.Tests.Unit/Services/RevisionIngestionServiceTests.cs` validating emitted accounting paths.

### Acceptance criteria
- Logs clearly show why persistence is 0 when retrieval is non-zero.
- No behavior change to success/failure decisions yet.

---

## Phase 1 — Multi-product aggregation invariants (lock in)

### Code changes
1. Keep existing concatenated scope behavior (already present) and make it explicit in tests and docs.
2. Ensure no later refactor performs per-product segmentation in ingestion loop.
3. Add a guard assertion/log that segmentation input equals full union scope count for the run.

### Files likely touched
- `PoTool.Api/Services/RevisionIngestionService.cs` (guard/log only)
- `PoTool.Tests.Unit/Services/RevisionIngestionServiceTests.cs`

### Tests
1. **Existing test to retain/extend**: `IngestRevisionsAsync_WithMultipleProducts_UsesConcatenatedScopeAcrossRoots` (`RevisionIngestionServiceTests.cs:637-678`).
2. Add assertion that first call scope snapshot equals union of all product descendants and remains unchanged across pages.

### Acceptance criteria
- Multi-product scope union is proven in tests and cannot regress silently.
- Segmentation inputs are based on aggregated union, not product-by-product subsets.

---

## Phase 2 — Query/continuation alignment with validator semantics

### Problem
Ingestion currently queries with `ChangedDate ge window.StartUtc` then locally drops `>= window.EndUtc`, which can produce large retrieved-vs-persist mismatch (`RevisionIngestionService.cs:1224-1227`, `1254-1259`).

### Code changes
Choose **one** of these options (recommended: Option A):

#### Option A (recommended): Add server-side upper-bound filter for window mode
- Extend query builder/client API so windowed calls pass both `windowStartUtc` and `windowEndUtc`.
- Build filter: `ChangedDate ge start and ChangedDate lt end` (+ scope + seek cursor clauses).
- Keep current bounded window algorithm.

**Trade-off:** minimal architectural disruption; strongest fix for “retrieved but out-of-window dropped”.

#### Option B: Reduce local window gating, rely on global seek cursor progression
- Remove strict window-end candidate filter and advance via global seek cursor only.
- Requires stronger checkpoint/termination semantics to stay incremental-safe.

**Trade-off:** simpler semantic model but larger behavioral shift/risk.

### Additional continuation alignment work (both options)
1. When `raw=0 && hasMore=true`, attempt deterministic re-seek from last committed tuple before counting toward fatal dead-page threshold.
2. Ensure retry path mutates cursor/query state (not delay-only replay).

### Files likely touched
- `PoTool.Integrations.Tfs/Clients/ODataRevisionQueryBuilder.cs`
- `PoTool.Integrations.Tfs/Clients/RealODataRevisionTfsClient.cs`
- `PoTool.Core/Contracts/*` if call signatures need upper-bound parameter
- `PoTool.Api/Services/RevisionIngestionService.cs`

### Tests
1. Unit tests for query building include upper-bound ChangedDate filter.
2. Client tests for continuation progression with nextLink + seek fallback remain green.
3. Ingestion tests that simulate out-of-window payloads now persist expected in-window rows without excessive drop.

### Acceptance criteria
- “Scoped retrieved high, persisted 0” no longer occurs when in-window data exists.
- Continuation progresses deterministically under both nextLink and seek fallback.

---

## Phase 3 — Anomaly handling and deterministic recovery

### Code changes
1. Introduce explicit anomaly recovery state machine for:
   - repeated token,
   - token non-advance,
   - raw-empty with hasMore.
2. Recovery order:
   - retry with re-seek cursor (changed query state),
   - bounded attempts,
   - then explicit terminal reason (`WindowStallReason`) with sampled context.
3. Keep retry bounds from options (`MaxPageRetries`, `MaxProgressWithoutDataPages`, retry backoff options).

### Files likely touched
- `PoTool.Api/Services/RevisionIngestionService.cs` (main stall/retry branch around `1472-1567`)
- possibly `RealODataRevisionTfsClient.cs` if new cursor-reset API surface is needed

### Tests
Add deterministic mock-based tests in `RevisionIngestionServiceTests`:
1. **Empty page but has more** anomaly simulation with deterministic recovery path.
2. Repeated token scenario verifies recovery attempts then bounded termination.
3. Non-advancing token scenario verifies same.

### Acceptance criteria
- No silent dead pages.
- Every anomaly ends in either successful deterministic recovery or explicit bounded failure reason.
- No infinite loops, no unbounded retries.

---

## Phase 4 — Tests (unit + integration-ish fixtures)

### Unit tests (must-have)
1. **Multi-product aggregated scope -> segmentation input**
   - Validate union scope across products is used before paging.
2. **Continuation token / nextLink progression**
   - Cover nextLink, seek fallback, repeated-token, non-advancing token.
3. **“Empty page but has more” anomaly + recovery**
   - Simulate raw-empty pages with advancing token and assert recovery or bounded fail reason.
4. **Persistence gating correctness**
   - Assert retrieved in-scope revisions persist when inside scope/window and valid.
   - Assert drops are attributed to correct drop counters when not persisted.

### Integration-ish strategy (no live TFS)
- Use deterministic HTTP fixtures/mocks (existing `HttpMessageHandler` test style in `RealODataRevisionTfsClientTests.cs`) to simulate OData response sequences:
  - normal progression
  - empty pages with nextLink
  - repeated nextLink
  - mixed in-window/out-of-window changed dates
- Keep CI hermetic: no external server calls.

### Acceptance criteria
- New tests fail on old anomaly behavior and pass with fixes.
- Existing relevant tests remain green:
  - `RevisionIngestionServiceTests`
  - `RealODataRevisionTfsClientTests`

---

## Rollout and safety checks
1. Ship Phase 0 first (observability), then Phases 1-3 behind existing option defaults where possible.
2. Verify incremental checkpoint semantics remain monotonic (`LastStableChangedDateUtc`, retry cursor logic).
3. Monitor:
   - anomaly counts,
   - scoped-to-persisted ratio,
   - windows marked unretrievable,
   - retry iteration outcomes.

---

## Definition of done
Done when:
1. Ingestion and validator/client semantics are aligned on ordering/cursor progression.
2. Multi-product union scope remains guaranteed before segmentation.
3. `RawZeroWithHasMore`/dead-page scenarios have deterministic recovery or explicit bounded failure.
4. Retrieved scoped revisions persist as expected (or are explicitly and correctly attributed to drop reasons).
5. Unit and fixture-based tests cover all required scenarios without live TFS dependencies.

---

## Implemented (2026-02-22)
- Enforced canonical continuation query rebuild in `RealODataRevisionTfsClient` + `ODataRevisionQueryBuilder` so continuation pages always re-apply window bounds (`ChangedDate ge start` + `ChangedDate lt end`), stable ordering, and top while extracting only continuation tokens from nextLink.
- Added guardrail failure for out-of-window payloads with high-signal diagnostics (window bounds, page index, min/max changed date, nextLink-follow-up flag, filter/orderby).
- Added deterministic empty-page-with-has-more recovery by re-seeking from the last seen `(ChangedDate, WorkItemId, Revision)` cursor (defaulted by existing seek fallback option), with mocked unit tests covering nextLink constraint preservation, conflicting nextLink filters, invariant enforcement, and re-seek progress.
