# Final CDC Integration Audit & Implementation Plan

_Reference: Prompts 1–8 analysis documents under `docs/analysis/`_

---

## Summary

This document consolidates the findings from all prior analysis documents (Prompts 1–8) into a
single coherent audit and a phased implementation plan for the CDC integration. All decisions are
grounded in previously analyzed artifacts and domain rules; no new assumptions are introduced.

The system as designed is largely consistent and conflict-free. The main residual risks are:

- `TimeCriticality` / `ProjectNumber` / `WorkPackage` are not yet wired end-to-end.
- Mixed estimation mode (SP vs. Effort) has no explicit flag in the current DTO contract.
- Snapshot lifecycle (creation moment, versioning, comparison) is defined in principle but not yet
  fully enforced.
- Planning Quality signals are implemented for delivery analytics but are not yet connected to the
  broader validation pipeline.

---

## Consistency Audit

### 1. State & Readiness

**Finding: Consistent.**

- `StateClassification` defines exactly four lifecycle states: `New`, `InProgress`, `Done`,
  `Removed`. Source: `PoTool.Core.Domain/Models/StateClassificationModels.cs`.
- `Approved` is a raw TFS state for PBI/Bug that maps to canonical `New`. No overlap with
  refinement-readiness semantics.
- Refinement readiness is a derived, content-driven concept (description, children, effort score)
  implemented in `BacklogReadinessService`. It is not part of `StateClassification`.
- No UI-only reinterpretation of state exists. The only UI-side logic is visibility gating in
  `WorkItemVisibilityService`, which consumes pre-computed classifications.

**Action required:** None. The boundary between lifecycle state and refinement readiness is clean.

---

### 2. Field Contract

**Finding: Partially consistent — three fields are absent end to end.**

| Field | Type | Target level | Status |
|---|---|---|---|
| `ProjectNumber` | `string` | Epic only | Not implemented |
| `WorkPackage` | `string` | Per-project consistency | Not implemented |
| `TimeCriticality` | `double` | Feature only (Epic ignored) | Not implemented |
| `Effort` | `double` | Epic / Feature / PBI | Fully wired |
| `StoryPoints` | `double` | PBI only (authoritative) | Fully wired |

- `ProjectNumber`: not in `RequiredWorkItemFields`, `RevisionFieldWhitelist`, `WorkItemEntity`,
  `WorkItemDto`, or any domain rule. Source: `docs/analysis/field-contract.md §2`.
- `WorkPackage`: same gap as `ProjectNumber`. Source: `docs/analysis/field-contract.md §3`.
- `TimeCriticality`: not retrieved, persisted, or analyzed. Source: `docs/analysis/field-contract.md §5`.
- `Effort`: fully supported from retrieval through analytics. Source: `docs/analysis/field-contract.md §4`.
- No field is used in multiple semantic roles. No ambiguity detected.

**Action required:** Implement `ProjectNumber`, `WorkPackage`, and `TimeCriticality` in Phase A
(field contract enforcement). Type contracts are clear from domain rules; only plumbing is missing.

---

### 3. Hierarchy & Aggregation

**Finding: Consistent.**

- Canonical hierarchy is `Epic ← Feature ← PBI ← Task`. Source: `docs/analysis/hierarchy-aggregation.md §1`.
- SP rollup is the canonical aggregation path. Effort rollup is secondary and optional.
- Override is scoped to Feature level only. `FeatureProgressService` applies the override; all
  downstream rollups consume `EffectiveProgress` without re-computing it. Source:
  `docs/analysis/progress-model.md §3`.
- Aggregation is deterministic: `HierarchyRollupService.RollupCanonicalScope` is the single
  computation point for scope. Source: `docs/analysis/hierarchy-aggregation.md §3`.
- Tasks are excluded from story-point scope. Bugs can drive activity but not authoritative SP
  aggregation. Source: `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`.

**Action required:** None. Hierarchy and aggregation rules are consistently implemented.

---

### 4. Validation System

**Finding: Consistent — categories are distinct and severity mapping is clear.**

| Category | Code key | Domain family | Severity |
|---|---|---|---|
| Structural Integrity | `SI` | `StructuralIntegrity` | Error |
| Refinement Readiness | `RR` | `RefinementReadiness` | Warning |
| Refinement Completeness | `RC` | `ImplementationReadiness` | Warning |
| Planning Quality | `PQ` | (new, delivery-trend domain only) | Warning / Critical / Info |

- `SI`, `RR`, and `RC` rules are registered in `RuleCatalog` and executed in fixed order by
  `BacklogValidationService`. Source: `docs/rules/validation-rules.md §2`.
- `PQ` signals are currently isolated to the delivery-trend domain and do not overlap with the
  backlog-quality rules. Source: `docs/analysis/planning-quality.md §1`.
- No duplicate signals exist across categories. Each rule carries a unique stable ID (`SI-*`,
  `RR-*`, `RC-*`, `PQ-*`).
- Severity mapping is enforced in `ValidationCategoryMeta.GetAlertSeverity`.

**Action required:** Connect `PQ` signals to the wider validation pipeline in Phase C.

---

### 5. Progress Model

**Finding: Consistent — all computation is in the backend.**

- `CalculatedProgress` = raw story-point ratio (capped at 90 % for non-done items).
- `Override` = manual override field on Feature, sourced from `TimeCriticality` (once wired).
- `EffectiveProgress` = `Override` when present; otherwise `CalculatedProgress`.
- No frontend recomputation occurs. `ProgressPercent` is a pre-computed DTO value consumed
  directly by the UI. Source: `docs/analysis/progress-model.md §1–§4`.
- Null semantics are preserved: a null `Override` propagates null `EffectiveProgress` only when
  `TimeCriticality` is absent — not when it is zero. Source:
  `docs/analysis/epic-aggregation-null-semantics-fix.md`.

**Action required:** None once `TimeCriticality` is wired (Phase A). The model is correct.

---

### 6. Snapshots

**Finding: Consistent in principle — lifecycle rules are not yet enforced in code.**

- A snapshot is scoped to `Project + WorkPackage`. `WorkPackage` is optional globally but
  required for consistency within a project that uses it. Source: `docs/analysis/snapshots.md §3`.
- `RoadmapSnapshotEntity` is a separate, existing entity for roadmap-specific snapshots and is
  not mixed with delivery-trend snapshots. Source: `docs/analysis/snapshots.md §1`.
- `ProductSnapshot` and `SnapshotComparisonService` are implemented for delivery-trend snapshots.
  Source: `docs/analysis/snapshot-comparison-validation.md`.

**Action required:** Implement snapshot lifecycle rules (creation trigger, versioning, comparison
enforcement) in Phase D.

---

### 7. Filtering

**Finding: Consistent — no hidden filtering detected.**

- Default filter: all products, all projects, current sprint (when applicable). Source:
  `docs/analysis/filtering.md §3`.
- `WorkspaceBase` propagates `productId` and `teamId` via URL query strings. No implicit
  pre-filtering occurs at the component level.
- Legacy workspace uses `INavigationContextService` with explicit context serialization. No
  hidden filtering in that path either.
- Backend handlers do not apply undocumented scoping beyond what the query DTO specifies.

**Action required:** None. Filtering is transparent at both layers.

---

## Conflicts

### C-1 — `TimeCriticality` defined in contract but absent from stack

**Severity: High**

`TimeCriticality` (double, Feature-only override source) is fully specified in the domain rules
and the progress model but is not retrieved, persisted, exposed in DTOs, or consumed by
`FeatureProgressService`. Until it is wired, the override mechanism can never activate.

**Required action:** Add `TimeCriticality` to `RequiredWorkItemFields`, `RevisionFieldWhitelist`,
`WorkItemEntity`, DTOs, and `FeatureProgressService` in Phase A.

---

### C-2 — `ProjectNumber` and `WorkPackage` defined in contract but absent from stack

**Severity: Medium**

Both fields are referenced in domain rules and analysis documents but have no retrieval,
persistence, or DTO support. Snapshot scoping by `WorkPackage` cannot be enforced until the
field exists in the data model.

**Required action:** Add both fields to the data model in Phase A. Snapshot enforcement follows
in Phase D once the field is available.

---

### C-3 — Planning Quality not connected to the validation pipeline

**Severity: Medium**

`PlanningQualityService` emits `PQ-*` signals in the delivery-trend domain layer, but those
signals are not routed through `RuleCatalog`, `BacklogValidationService`, or the shared
`ValidationCategory` model. They are also not exposed by the API or consumed by the UI.

**Required action:** Integrate `PQ` signals into the validation pipeline and UI in Phases C and E.

---

### C-4 — No explicit mixed-estimation flag in DTO contract

**Severity: Low**

The domain rules acknowledge that a product can mix SP- and Effort-based estimation. There is
currently no flag in `FeatureProgress`, `EpicProgress`, or API DTOs to indicate which mode
applies for a given scope. Consumers must infer mode from null/non-null Effort and SP values.

**Required action:** Decide whether to add an explicit `EstimationMode` enum to the DTO contract
(Phase A) or defer to Phase E as a UI enhancement only.

---

### C-5 — Snapshot creation moment and versioning undefined in code

**Severity: Low**

The snapshot model specifies `Project + WorkPackage` scope and comparison rules, but no code
enforces when a snapshot is created or how versions are numbered. `RoadmapSnapshotService`
has a user-triggered creation pattern, but no equivalent trigger exists for delivery-trend
snapshots.

**Required action:** Define and implement creation trigger and versioning in Phase D.

---

## Missing Pieces

### M-1 — Mixed estimation mode flag

The system has no explicit `EstimationMode` (SP vs. Effort) flag in the DTO contract. Whether
this requires a dedicated enum or can be inferred from null fields must be decided before Phase B
aggregation logic is locked.

### M-2 — Planning Quality completeness

The seven `PQ` signals cover the main delivery-trend quality cases, but the following inputs are
not yet available at Planning Quality evaluation time:

- `ProjectNumber` presence on Epics (depends on M-1 / C-2 resolution)
- `WorkPackage` consistency (depends on C-2 resolution)
- Sprint-level quality signals (not yet modeled)

### M-3 — Snapshot lifecycle

Missing explicit decisions on:
- **Creation moment**: user-triggered vs. automated (e.g., end of sprint / sync run)
- **Versioning**: monotonic integer, timestamp, or user label
- **Comparison rules**: whether comparison is always latest-vs-previous or user-selectable

### M-4 — Validation enforcement points

The issue mandates clarity on where each validation category runs:

| Category | Current enforcement point | Blocks or warns |
|---|---|---|
| SI | `BacklogValidationService` (sync + query time) | Warns (no hard block) |
| RR | `BacklogValidationService` (sync + query time) | Warns |
| RC | `BacklogValidationService` (sync + query time) | Warns |
| PQ | `PlanningQualityService` (query time only) | Warns / Critical |

No category currently hard-blocks ingestion. Error-severity findings are surfaced as UI alerts
only. This is by design but must be confirmed for the final contract.

---

## Implementation Plan

### Phase A — Foundation

1. Add `ProjectNumber` (`string`) to `RequiredWorkItemFields`, `RevisionFieldWhitelist`,
   `WorkItemEntity`, `WorkItemDto`, `WorkItemWithValidationDto`, and `WorkItemRepository`.
   Scope: Epic only.
2. Add `WorkPackage` (`string`) following the same path. Enforce per-project consistency in
   the CDC layer.
3. Add `TimeCriticality` (`double?`) to the same stack. Scope: Feature only. Wire into
   `FeatureProgressService` as the override source.
4. Finalize decision on `EstimationMode` flag (see M-1).
5. Verify `StateClassification` contract; no changes anticipated.

**Acceptance criteria:** All five fields round-trip through ingestion, persistence, DTOs, and
domain services without data loss.

---

### Phase B — CDC Core

1. Implement / verify aggregation logic:
   - `HierarchyRollupService.RollupCanonicalScope` (SP rollup — already implemented)
   - Effort rollup as secondary path alongside SP rollup
2. Implement / verify override behavior in `FeatureProgressService`:
   - `EffectiveProgress = TimeCriticality` when present and in-range; `CalculatedProgress`
     otherwise
3. Implement / verify `DeliveryProgressRollupService` consuming `EffectiveProgress`
4. Implement / verify `PlanningQualityService` consuming phase-B outputs

**Acceptance criteria:** Deterministic aggregation for all hierarchy levels; override activates
only when `TimeCriticality` is non-null and in-range.

---

### Phase C — Validation

1. Register `PQ-*` signals in the shared validation model (or produce a parallel `PQ` surface
   that the UI can consume alongside `SI / RR / RC`).
2. Enforce severity levels: `Critical` PQ signals deduct from delivery health score;
   `Warning` signals are advisory only.
3. Log validation changes: when a finding is created, resolved, or changes severity, emit a
   structured log entry for diagnostics.
4. Confirm enforcement points (see M-4): no hard block at ingestion; hard block only where
   explicitly mandated by domain rules.

**Acceptance criteria:** All four categories surface consistently through a single API endpoint
and a single UI entry point.

---

### Phase D — Snapshots

1. Implement delivery-trend `SnapshotEntity` scoped to `Project + WorkPackage`.
2. Enforce `WorkPackage` consistency rule: if any snapshot in a project uses `WorkPackage`, all
   subsequent snapshots in that project must also carry it.
3. Implement creation trigger (decision required: user-triggered or automated at sync end).
4. Implement versioning (decision required: timestamp or monotonic counter).
5. Wire `SnapshotComparisonService` to the snapshot store so comparisons are always
   computed against canonical stored values.
6. Keep `RoadmapSnapshotEntity` separate; no data sharing between the two snapshot families.

**Acceptance criteria:** Two consecutive snapshots for the same `Project + WorkPackage` produce
a deterministic `SnapshotComparisonResult` via `SnapshotComparisonService`.

---

### Phase E — UI Integration

1. Ensure all delivery-trend pages consume canonical `FeatureProgress`, `EpicProgress`,
   and `ProductAggregationResult` DTOs from the API. No local recomputation.
2. Remove any legacy progress or forecast computation from Blazor components.
3. Integrate `PQ` signals into the Validation Queue or a dedicated Planning Quality panel.
4. Integrate `SnapshotComparisonResult` into the delivery dashboard (progress delta, forecast
   consumed delta, forecast remaining delta).
5. Validate default filter behavior: all products, all projects, current sprint. Ensure no
   hidden filter is introduced in new components.

**Acceptance criteria:** UI passes canonical values end to end; no DTO mismatch or orphaned
client-side calculation.

---

### Phase F — Hardening

1. Structured logging for CDC aggregation, validation, and snapshot creation events.
2. Diagnostics endpoint for in-process inspection of CDC state.
3. Test coverage targets:
   - Unit: all aggregation paths, all PQ signals, all snapshot delta cases
   - Integration: full CDC pipeline replay (existing fixture pattern)
   - Audit: all new documents enforced by MSTest audit tests
4. Performance validation: CDC aggregation time under load for large work item trees.

**Acceptance criteria:** Full test suite green; CDC aggregation benchmarked against baseline.

---

## Risk Table

| Phase | Impact (1–10) | Risk (1–10) | Main failure mode |
|---|---|---|---|
| A — Foundation | 8 | 5 | Missing field breaks ingestion; DTO schema change affects existing consumers |
| B — CDC Core | 9 | 4 | Aggregation non-determinism or override applied to wrong scope |
| C — Validation | 6 | 4 | PQ signals duplicate existing SI/RR/RC findings; severity misclassification |
| D — Snapshots | 7 | 6 | WorkPackage consistency not enforced; comparison against wrong baseline |
| E — UI Integration | 7 | 5 | Legacy computation survives refactor; UI shows stale or duplicated values |
| F — Hardening | 5 | 3 | Coverage gap leaves aggregation edge case untested |
