# CDC Full Quality Audit

_Generated: 2026-03-26_

Reference documents:

- `docs/domain/domain_model.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/state_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/source_rules.md`
- `docs/implementation/phase-e-snapshots.md`
- `docs/implementation/phase-f-lifecycle.md`
- `docs/implementation/phase-g-consumption.md`
- `docs/implementation/phase-i-finalization.md`

Files analyzed:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotFactory.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/ProductAggregationService.cs`
- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs`
- `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs`
- `PoTool.Api/Services/PortfolioProgressQueryService.cs`
- `PoTool.Api/Services/PortfolioSnapshotQueryService.cs`
- `PoTool.Api/Services/PortfolioComparisonQueryService.cs`
- `PoTool.Api/Services/PortfolioTrendAnalysisService.cs`
- `PoTool.Api/Services/PortfolioTrendQueryService.cs`
- `PoTool.Api/Services/PortfolioDecisionSignalService.cs`
- `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs`
- `PoTool.Api/Services/PortfolioReadModelMapper.cs`
- `PoTool.Api/Services/PortfolioReadModelFiltering.cs`
- `PoTool.Api/Controllers/MetricsController.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- `PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs`
- `PoTool.Tests.Unit/Audits/PhaseIFinalizationDocumentTests.cs`

## 1. Executive summary

- overall correctness: **FAIL**
- confidence level: **high**

The CDC portfolio stack is not audit-clean. Three findings break the stated contract directly:

1. the read/query path persists snapshots on demand, so `GET` endpoints are not truly read-only and trend/comparison/signal outputs do not rely exclusively on pre-existing persisted data
2. an empty portfolio state cannot be represented or persisted, so snapshot history is incomplete at exactly the point where a full-state audit requires fidelity
3. persisted snapshot identity is not concurrency-safe: `SnapshotId` is assigned with `MAX + 1`, and there is no database uniqueness constraint for the logical key `(ProductId, TimestampUtc, Source)`

Beyond those blockers, the implementation is generally deterministic when already-given persisted rows are fed into the comparison/trend/signal services. The domain comparison engine avoids null-to-zero coercion, the trend layer orders explicitly, and the UI consumes DTOs without recomputing metrics. The failure is at the system boundary: hidden write-side behavior, incomplete representability, and persistence semantics that are too weak for a financial-grade audit.

## 2. Critical issues (must fix)

### 2.1 Hidden write-on-read breaks the persisted-only contract

**Exact location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:69-166`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs:210-273`
- transitively reached by:
  - `PoTool.Api/Services/PortfolioProgressQueryService.cs:23-24`
  - `PoTool.Api/Services/PortfolioSnapshotQueryService.cs:23-24`
  - `PoTool.Api/Services/PortfolioComparisonQueryService.cs:28-29`
  - `PoTool.Api/Services/PortfolioTrendQueryService.cs:23-24`
  - `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs:33-35`
- exposed via `GET` endpoints in `PoTool.Api/Controllers/MetricsController.cs:250-452`

**Why it breaks correctness**

`LoadPortfolioContextAsync` always calls `EnsureLatestSourcesPersistedAsync`, and that method reads transient capture sources, builds snapshot inputs, creates snapshots, and writes them through `IPortfolioSnapshotPersistenceService` before the query returns. That means:

- comparison/trend/signal responses are not based only on already persisted snapshots
- identical persisted data is not sufficient to guarantee identical API responses, because current source availability can change what becomes persisted during the request
- the `GET /api/portfolio/comparison`, `/api/portfolio/trends`, and `/api/portfolio/signals` endpoints have hidden mutation side effects

This is the single largest contract violation in the implementation.

### 2.2 Empty portfolio states are impossible to persist

**Exact location**

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:14-21`
- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:97-109`

**Why it breaks correctness**

`PortfolioSnapshot` throws when `items.Count == 0`. Separately, the capture service returns an empty dictionary when no feature/epic progress exists, so no snapshot is created at all for an empty product scope. The result is not merely “no data”; it is loss of state fidelity:

- a portfolio that legitimately becomes empty cannot be represented as a persisted snapshot
- a “everything removed / nothing left active” point disappears from history instead of being recorded
- later comparison/trend logic cannot distinguish “no snapshot captured” from “snapshot captured and the portfolio was empty”

Phase 1 explicitly required the empty-portfolio edge case. The current design fails it.

### 2.3 Snapshot persistence is not concurrency-safe or logically unique

**Exact location**

- `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs:183-189`
- `PoTool.Api/Persistence/PoToolDbContext.cs:707-733`
- downstream consequence in `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:243-274`

**Why it breaks correctness**

`PersistAsync` sets `SnapshotId` as `(MAX(existing SnapshotId) + 1)` in application code. There is also no uniqueness constraint on the logical snapshot key `(ProductId, TimestampUtc, Source)`. Under concurrent reads or multi-instance execution this creates two failure modes:

- two requests can race on `MAX + 1`, producing primary-key conflicts or non-repeatable failure behavior
- two requests can both observe “snapshot does not exist yet” and persist duplicate logical snapshots for the same product/timestamp/source

If duplicate logical snapshots exist, `GetPortfolioSnapshotBySourceAsync` loads all matching entities and merges all rows into one combined `PortfolioSnapshot`. That can duplicate business keys and either fail integrity validation or return a distorted grouped history.

For a deterministic CDC system, identity generation and logical uniqueness must be enforced by the database, not by optimistic read-before-write code in the query path.

## 3. Structural weaknesses

### 3.1 Snapshot groups are not fully self-contained state; aggregate rollups are recomputed later

**Location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:85-103`
- `PoTool.Api/Services/PortfolioTrendAnalysisService.cs:152-165`
- `PoTool.Api/Services/PortfolioTrendAnalysisService.cs:194-208`

The persisted snapshot rows contain item-level progress, weight, and lifecycle state, but portfolio-level and project-level aggregate metrics are recomputed during read queries through `ProductAggregationService`. The logic is centralized rather than duplicated, which is good, but it still means snapshots are not a fully frozen “all derived values included” artifact. If aggregation semantics ever change, historical responses can change without any snapshot rows changing.

### 3.2 Same-timestamp capture lineage is only partially modeled

**Location**

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs:133-145`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs:38-41`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:116-121`

Selection uses `TimestampUtc` and `SnapshotId` as an explicit ordering tie-break, which is correct for retrieval. Creation/validation does not carry the same total ordering end-to-end. `ValidateCreation` considers only snapshots with `Timestamp < candidate.Timestamp`, and `GetLatestBeforeAsync` also uses strict `< timestamp`. If same-timestamp captures are possible, retrieval is totally ordered but creation lineage is not. That is not a proven day-one bug, but it is an unresolved gap in the stated ordering contract.

### 3.3 Historical selection is explicit but inefficient under long history

**Location**

- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:152-176`

The service first queries ordered history groups, then performs a separate full reload per group via `GetPortfolioSnapshotBySourceAsync`. This is logically explicit and deterministic, but it is an N+1 selection pattern. Under long history chains or large portfolios it raises latency and increases the window for race behavior around the hidden write-on-read pipeline.

### 3.4 The latest-N contract is not exact for `N = 1`

**Location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:118-124`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs:275-276`

`NormalizeSnapshotCount` silently upgrades any requested count below 2 to 2. That means trend and signal queries do not implement a true “latest N” contract for `N = 1`; they impose a minimum instead of honoring the caller’s explicit bound.

### 3.5 Capture assumes a one-row-per-business-key mapping

**Location**

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:143-161`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:23-31`

Capture maps each epic-progress row directly to `(ProductId, ProjectNumber, WorkPackage)` and the snapshot model rejects duplicate business keys. If real data ever produces multiple epic rows that collapse onto the same business key, snapshot creation fails rather than aggregating or flagging a recoverable structural anomaly.

## 4. Determinism violations

### 4.1 Query-time persistence makes API results depend on more than persisted data

Given the same already-persisted rows, API results are not guaranteed to remain identical if current capture sources change between requests. The read model can create missing snapshots during the request. This violates the stated determinism requirement even if the downstream trend and comparison services are themselves deterministic.

### 4.2 Persistence identity and uniqueness are nondeterministic under concurrency

`MAX + 1` identity assignment plus missing logical uniqueness means the same workload can produce either:

- a successful write
- a primary-key collision
- a duplicate logical snapshot

which outcome occurs depends on timing, not on business data.

### 4.3 Equal-timestamp capture lineage does not preserve the same total ordering used for retrieval

Selection is explicit about `TimestampUtc` then `SnapshotId`, but creation and “latest before” semantics stop at timestamp-only ordering. The system therefore lacks one consistent total ordering rule across capture, persistence, selection, and comparison.

## 5. Duplication or leakage findings

### 5.1 Major semantic leakage: read-side query orchestration owns write-side persistence

**Location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:44-67`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs:210-273`

This service is a read-model state loader by name, but it also captures source data, invokes the snapshot factory, and persists new snapshots. That is a write concern leaking into query orchestration. It is the architectural reason the API cannot truthfully claim “GET only, no mutation.”

### 5.2 No meaningful formula duplication found across comparison/trend/query/UI layers

Positive result:

- domain comparison logic stays in `PortfolioSnapshotComparisonService`
- portfolio/project aggregation stays in `ProductAggregationService`
- query services mostly orchestrate mapping and filtering
- the UI panel consumes DTOs and formats values only

This part of the implementation is cleaner than the read/write boundary.

### 5.3 UI does not recompute domain values

**Location**

- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:421-478`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:550-586`

The panel issues typed client calls, displays DTO values, formats them, and does point lookup by `SnapshotId`. It does not sum, average, regroup, or derive trend/signal logic. Filtering remains server-side. This is compliant.

## 6. Edge case failures

### 6.1 Empty portfolio

**Result: fail**

No empty snapshot can be created or persisted. The system drops the state entirely.

### 6.2 Single project / single work package

**Result: pass**

The snapshot, comparison, and trend logic handle a single business key deterministically. Existing tests cover simple one-row flows.

### 6.3 Zero-weight items

**Result: mostly pass**

`ProductAggregationService` excludes weight `<= 0` from aggregate progress and returns `null` progress when no positive-weight active baseline exists. This avoids null-to-zero coercion. The risk is representational, not arithmetic: a zero-weight-only snapshot is persisted, but overall aggregate progress becomes absent rather than numerically zero.

### 6.4 All items done

**Result: pass**

No direct time dependence was found in snapshot progress computation or later selection/comparison/trend code. If persisted item progress is `1.0`, downstream comparison and trend logic preserve it deterministically.

### 6.5 All items new

**Result: pass**

Comparison preserves `null` previous values and does not coerce them to zero. New work packages are detected via `PreviousLifecycleState is null && CurrentLifecycleState == Active`.

### 6.6 Comparing identical snapshots

**Result: pass**

Comparison yields zero deltas only when both values exist and are equal. No smoothing or invented change is introduced.

### 6.7 Reordered entities

**Result: pass**

Comparison and trend projection both apply explicit ordering by business key or snapshot order. Natural database ordering is not used.

### 6.8 Partial overlap / missing entities

**Result: pass with caveat**

Comparison handles missing keys with null previous/current values and null deltas. That is correct. The caveat is that duplicate logical persisted snapshots would break this by merging conflicting rows into one group.

### 6.9 Rapid add/remove cycles with key reuse

**Result: fail**

**Location**

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotFactory.cs:59-66`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs:77-87`

Once a work package business key becomes retired, reappearance of the same key throws an exception. If the business permits reintroducing a previously retired work package identifier, the system cannot represent the cycle and fails instead of producing deterministic signals.

### 6.10 Long stable periods

**Result: pass**

Repeated-no-change is deterministic and based on the first three persisted points only. No smoothing or interpolation is present.

## 7. Recommendations (prioritized)

1. **Remove persistence from query execution immediately.**  
   `PortfolioReadModelStateService` must stop calling capture/factory/persistence logic from read paths. Snapshot capture should happen in an explicit command, background sync, or scheduled pipeline. Queries must consume persisted data only.

2. **Make empty portfolio snapshots first-class.**  
   Allow persisted zero-row snapshots or add an explicit header-level “captured empty” representation so history can record “nothing in scope” as a real state.

3. **Move snapshot identity and logical uniqueness into the database.**  
   Replace `MAX + 1` with database-generated identity/sequence behavior and add a unique constraint for `(ProductId, TimestampUtc, Source)`. If grouped multi-product snapshots are intended, define that contract explicitly and enforce it with schema-level guarantees.

4. **Unify total ordering semantics across capture, selection, and comparison.**  
   Either forbid same-timestamp captures per product or model them consistently with a persisted tie-break that creation/validation also understands.

5. **Decide whether work-package reactivation is valid domain behavior.**  
   If it is valid, model it explicitly instead of throwing. If it is invalid, document the invariant and add tests proving why identical-key reintroduction must be rejected.

6. **If “full snapshot state” is a hard requirement, persist aggregate rollups too.**  
   Today portfolio/project rollups are recomputed from row data at query time. That is centralized and deterministic, but it is still recomputation. Either accept that design formally or persist versioned aggregates with the snapshot.

7. **Reduce history selection round-trips once correctness is fixed.**  
   The current N+1 group reload pattern is a secondary issue, but it will become more visible after the write-on-read problem is removed and history length grows.

## Audit conclusion

The implementation contains solid localized pieces: exact-key comparison is mathematically careful, null semantics are mostly preserved, trend direction logic is deterministic, and the UI does not leak business logic. The system still fails the requested full-quality audit because the read path is not truly read-only, empty historical states cannot be represented, and persisted identity semantics are too weak to guarantee deterministic behavior under concurrent or scaled usage.
