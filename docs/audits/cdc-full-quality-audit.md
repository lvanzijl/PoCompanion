# CDC Full Quality Audit

_Generated: 2026-03-27_

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
- `docs/implementation/cdc-critical-fixes.md`

Files analyzed:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotFactory.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/ProductAggregationService.cs`
- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs`
- `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs`
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
- `PoTool.Api/Controllers/PortfolioSnapshotsController.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Api/Migrations/20260327061554_CdcCriticalFixes.cs`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotCaptureOrchestratorTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- `PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs`
- `PoTool.Tests.Unit/Audits/CdcCriticalFixesDocumentTests.cs`

## 1. Executive summary

- overall correctness: **FAIL**
- confidence level: **high**

The CDC stack is materially better than the earlier implementation that this audit originally targeted. The major defects around write-on-read persistence and missing logical uniqueness were fixed: portfolio GET endpoints now read persisted snapshots only, snapshot capture sits behind an explicit `POST /api/portfolio/snapshots/capture` boundary, and the database now enforces uniqueness on `(ProductId, TimestampUtc, Source)`.

The system still does not pass a full financial-grade quality audit. Two issues remain correctness defects rather than style concerns:

1. a truly empty portfolio owner with no resolved-work-item-derived source cannot produce a persisted empty snapshot, so snapshot history is still not guaranteed to represent the complete state timeline
2. historical selection rewrites the caller contract by forcing `snapshotCount < 2` up to `2`, so latest-`N` retrieval is not exact for `N = 1`

Outside those failures, the comparison engine is mathematically careful, trend and signal logic are deterministic for fixed persisted inputs, ordering is explicit, null-to-zero coercion was not found, DTOs are projection-only, and the UI does not recompute domain math.

## 2. Critical issues (must fix)

### 2.1 Truly empty portfolio owners still cannot be captured as persisted history

**Exact location**

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:42-81`
- `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs:47-63`
- `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs:68-118`

**Why it breaks correctness**

The snapshot model and persistence layer now allow empty snapshots. That fix is real and verified by tests (`PortfolioSnapshotPersistenceServiceTests` and `PortfolioSnapshotCaptureOrchestratorTests`). The remaining failure is one layer earlier: capture source discovery depends on existing resolved work items with a resolved sprint.

`GetLatestSourcesAsync` derives candidate snapshot sources only from `ResolvedWorkItems`. If a product owner is genuinely empty and has no qualifying resolved work items at all, the method returns no sources. `CaptureLatestAsync` then exits with `SourceCount = 0` and creates no snapshot header. That means the system still cannot guarantee a persisted “portfolio is empty at this point in time” record for the exact case the audit asked to challenge.

This is not a representational detail. It means historical completeness still depends on the presence of upstream source rows, not solely on the business state being audited.

### 2.2 Latest-`N` history retrieval is not exact because `N = 1` is silently rewritten to `2`

**Exact location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:97-133`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs:218-219`
- indirectly exposed through:
  - `PoTool.Api/Controllers/MetricsController.cs:361-452`
  - `PoTool.Api/Services/PortfolioTrendQueryService.cs:18-62`
  - `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs:28-68`

**Why it breaks correctness**

`NormalizeSnapshotCount` upgrades any request below `2` to `2`. That means the read side does not honor the caller’s explicit historical bound for trend and signal queries.

For a system that claims deterministic historical selection, silently changing `N = 1` into `N = 2` is not benign. It changes what data is selected, can change direction and delta semantics, and makes it impossible to audit “latest persisted snapshot only” through the published read contract.

The implementation is deterministic, but it is not semantically faithful to the request.

## 3. Structural weaknesses

### 3.1 Snapshot groups are still not fully frozen analytical artifacts

**Location**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:76-94`
- `PoTool.Api/Services/PortfolioTrendAnalysisService.cs:154-165`
- `PoTool.Api/Services/PortfolioTrendAnalysisService.cs:194-208`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/ProductAggregationService.cs:22-59`

Persisted rows freeze item-level progress, weight, and lifecycle state, but portfolio-level and project-level rollups are recomputed at read time from those rows. The logic is centralized and deterministic, which avoids formula duplication, but the snapshot is still not a fully self-contained “all derived values frozen” artifact.

If aggregation semantics change later, historical portfolio/project trend responses can change without any persisted snapshot row changing. That is a structural audit risk, not a current arithmetic bug.

### 3.2 Decision signals do not read from a single transactional historical view

**Location**

- `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs:33-48`

Signals are assembled from two separate reads: one history state load for trends and one comparison state load for current-vs-baseline comparison. With static persisted data this is deterministic. Under concurrent snapshot capture, however, a signal request can observe history and comparison from different points in time because no explicit transaction or persisted-history version ties the two reads together.

This is not a defect for the fixed-input determinism requirement, but it is a realism gap under active capture.

### 3.3 Historical selection remains explicit but scales with an N+1 reload pattern

**Location**

- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:154-179`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:226-298`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs:390-420`

The selector first retrieves ordered group headers, then reloads every selected group individually through `GetPortfolioSnapshotBySourceAsync`. That preserves explicit ordering and avoids natural-order dependence, but it creates an N+1 pattern that will become more visible under long history chains and large portfolios.

### 3.4 Work-package key reactivation is still modeled as invalid rather than explicitly supported

**Location**

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs:133-181`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotValidationService.cs:77-87`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotFactory.cs` (reactivation path enforced through validation)

A previously retired work-package business key cannot return as active; validation throws instead. If the business truly treats work-package identifiers as permanently retired, that is acceptable. If add/remove/re-add cycles with key reuse are valid, the current model cannot represent them.

The implementation is deterministic, but the domain assumption is stronger than the audit brief and is not documented in the canonical domain rules.

### 3.5 UI composition is read-only but not snapshot-consistent across calls

**Location**

- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:421-478`

The panel loads progress, snapshot, comparison, trends, and signals through five separate GET calls. It does not recompute business logic, which is correct, but it also means the page can temporarily present mixed snapshot labels if a new persisted snapshot appears mid-refresh.

That is a consumption consistency risk, not a UI-math defect.

## 4. Determinism violations (if any)

### 4.1 No direct determinism violation was found for fixed persisted data

For identical persisted snapshot rows:

- comparison output is deterministic because business keys are explicitly unified and ordered in `PortfolioSnapshotComparisonService`
- trend output is deterministic because snapshots are ordered by `Timestamp` then `SnapshotId` in `PortfolioTrendAnalysisService`
- signal output is deterministic because it is derived from deterministic trend/comparison DTOs and ordered explicitly in `PortfolioDecisionSignalService`
- API endpoints `/api/portfolio/comparison`, `/api/portfolio/trends`, and `/api/portfolio/signals` are GET-only and now consume persisted data only

The earlier determinism break caused by write-on-read capture is fixed.

### 4.2 Request-level consistency can still drift under concurrent capture

**Location**

- `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs:33-48`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:421-478`

This is a conditional determinism risk rather than a fixed-data violation. Separate reads inside the signals query, and separate endpoint calls inside the UI, can observe different persisted histories if capture happens concurrently.

## 5. Duplication or leakage findings

### 5.1 No meaningful formula duplication found across domain, query, controller, or UI layers

Positive result:

- comparison math remains in `PortfolioSnapshotComparisonService`
- aggregate portfolio/project rollups remain in `ProductAggregationService`
- query services orchestrate persisted selection, mapping, and filtering rather than reimplementing formulas
- `PortfolioReadModelMapper` performs projection only
- the UI formats values and looks up precomputed points by `SnapshotId`; it does not recompute trends, deltas, or signals

### 5.2 The previous write-side leakage into read orchestration is resolved

**Location validated**

- `PoTool.Api/Services/PortfolioReadModelStateService.cs:60-216`
- `PoTool.Api/Controllers/PortfolioSnapshotsController.cs:23-38`
- `PoTool.Api/Persistence/PoToolDbContext.cs:707-727`
- `PoTool.Api/Migrations/20260327061554_CdcCriticalFixes.cs:11-72`

The earlier architecture defect is no longer present. Read-model state loading no longer captures or persists snapshots. Capture now sits behind an explicit command/controller boundary, and persistence uniqueness is enforced in the schema.

### 5.3 UI consumption is projection-only

**Location**

- `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs:85-231`
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:421-586`

The client calls typed API abstractions, binds DTOs, formats values, and performs point lookup by `SnapshotId`. It does not aggregate, smooth, interpolate, or derive signals locally.

## 6. Edge case failures

### 6.1 Empty portfolio owner with no source rows

**Result: fail**

Empty snapshots are representable and persistable once a source exists, but a truly empty owner with no qualifying resolved-work-item source produces no snapshot at all. The state remains historically invisible.

### 6.2 Empty product within an otherwise non-empty owner

**Result: pass**

`PortfolioSnapshotCaptureOrchestrator` persists header-only snapshots when a source exists but a product has no inputs. This is covered by `PortfolioSnapshotCaptureOrchestratorTests.CaptureLatestAsync_PersistsEmptySnapshotForProductWithoutInputs`.

### 6.3 Single project / single work package

**Result: pass**

The snapshot, comparison, and trend logic handle single-key cases deterministically. Existing tests cover simple one-row flows.

### 6.4 Zero-weight items

**Result: pass with explicit null semantics**

`ProductAggregationService` excludes weight `<= 0` from aggregate progress and returns `null` progress when no positive-weight active baseline exists. No null-to-zero coercion was found.

### 6.5 All items done

**Result: pass**

No hidden current-time dependency was found in snapshot persistence, selection, comparison, or trend projection. Persisted `1.0` progress remains `1.0` downstream.

### 6.6 All items new

**Result: pass**

Comparison preserves `null` previous values, computes no invented delta, and identifies new work packages only when the previous lifecycle is absent and the current lifecycle is active.

### 6.7 Comparing identical snapshots

**Result: pass**

Comparison yields zero deltas only when both prior and current values exist and are equal. No smoothing or invented change is introduced.

### 6.8 Comparing snapshots with missing entities / partial overlap

**Result: pass**

Missing keys remain represented with `null` prior or current values. Deltas remain `null` instead of being coerced to zero.

### 6.9 Reordered entities

**Result: pass**

Comparison and trend projection both apply explicit ordering. No reliance on database natural ordering was found.

### 6.10 Identical timestamps

**Result: pass**

Selection and trend analysis order by `TimestampUtc` and use `SnapshotId` as an explicit tie-break. The earlier tie-break gap was addressed.

### 6.11 Latest `N` where `N > available`

**Result: pass**

The selector returns the available persisted history without fabricating missing snapshots.

### 6.12 Latest `N` where `N = 1`

**Result: fail**

The public read path silently upgrades the request to `2`, so the bounded historical contract is not exact.

### 6.13 Archived snapshots mixed into selection

**Result: pass**

Archived inclusion is explicit, ordering remains explicit, and the system surfaces an archived-history notice when archived snapshots exist but are excluded by default.

### 6.14 Rapid add/remove cycles with key reuse

**Result: unresolved domain risk**

If key reuse is valid, current validation fails the scenario. If key reuse is invalid, the invariant should be documented in the canonical domain rules instead of remaining implicit in validation code.

## 7. Recommendations (prioritized)

1. **Add a capture strategy for truly empty owners.**  
   Snapshot capture needs a source/timestamp strategy that does not depend on existing resolved work items when the audited business state is “nothing in scope.” Without that, full-state history remains incomplete.

2. **Honor `snapshotCount` exactly, including `N = 1`.**  
   If trends/signals require at least two points for some outputs, return null deltas/directions explicitly instead of silently changing the caller’s requested bound.

3. **Decide and document whether work-package key reactivation is valid domain behavior.**  
   Keep the current validation only if the business invariant is intentional and canonical. Otherwise model reactivation explicitly.

4. **Consider freezing aggregate rollups if historical analytics must remain invariant under future formula changes.**  
   The current centralized recomputation is clean, but it is still recomputation.

5. **Reduce history selection round-trips for long histories.**  
   Correctness is currently acceptable, but the N+1 group reload pattern will be an avoidable stress point for large portfolios.

6. **If request-level consistency under concurrent capture matters, introduce a shared persisted-history version or transactional read boundary for signals and UI consumption.**

## Audit conclusion

The current CDC implementation is much closer to audit-clean than the pre-fix system: read-side mutation was removed, persisted uniqueness is enforced, selection is explicitly ordered, comparison preserves null semantics, and the UI consumes DTOs without recalculating business logic.

It still fails a full-system quality audit because the system cannot guarantee a persisted snapshot for a truly empty owner state, and because the read contract for latest-`N` history is not semantically exact. Those are smaller defects than the earlier architecture failures, but they remain correctness issues rather than cosmetic risks.
