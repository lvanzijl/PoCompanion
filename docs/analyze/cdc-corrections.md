# Corrective CDC Integration Audit

_Reference: `docs/analyze/final-cdc-integration.md` (Prompt 9), `docs/analyze/planning-quality.md`,
`docs/analyze/validation-rules.md`, `docs/analyze/snapshots.md`, `docs/analyze/progress-model.md`,
`docs/analyze/filtering.md`_

---

## Summary

This document resolves six areas that were left weakly defined or incorrectly assessed in the
Prompt 9 final audit. Each correction is grounded in the prior analysis documents. No new domain
semantics are introduced unless required to resolve a proven conflict.

Corrections made in this document:

1. **Planning Quality boundary** — the domain placement and integration pattern are now explicit.
2. **Blocking vs non-blocking model** — a concrete `IsBlocking` contract is recommended.
3. **Snapshot risk recalibration** — Phase D risk is raised from Low to High (was scored 6/10,
   now 8/10).
4. **EstimationMode contract** — inference is rejected; an explicit enum at product-settings level
   is required, with propagation into CDC and read-model DTOs.
5. **Logging semantics** — a concrete event taxonomy and required log fields are defined.
6. **Data maturity feedback loop** — surface-by-surface mapping of actionable quality signals.

---

## 1. Planning Quality Boundary Correction

### 1.1 Is Planning Quality backlog validation?

**No.** Backlog validation (`SI`, `RR`, `RC`) operates on individual work items and their immediate
parent–child relationships. It answers the question: "Is this work item structurally sound and
refinement-ready?" Findings are scoped to a single work item node.

Planning Quality operates on aggregated delivery data — feature rollups, epic progress, product
scope, and forecast signals. It answers the question: "Does the current delivery state represent a
coherent plan?" Findings are scoped to feature, epic, or product aggregates, not individual items.

These two concerns belong to **different domains**. Merging them into `RuleCatalog` /
`BacklogValidationService` would force PQ signals to evaluate at the wrong scope and at the wrong
time in the pipeline.

### 1.2 Is Planning Quality delivery analytics?

**Yes.** `PlanningQualityService` already consumes `FeatureProgress`, `EpicProgress`, and
`ProductAggregationResult` — all outputs of the delivery analytics CDC pipeline. The service is
registered in `PoTool.Core.Domain/Domain/DeliveryTrends/Services`, which is the correct domain
boundary.

PQ signals are analytics-layer signals. Their inputs do not exist until the CDC aggregation
pipeline has run.

### 1.3 Should Planning Quality share DTO shapes with SI / RR / RC?

**Partially.** The UI needs a consistent rendering model for all quality signals, so the
_serialized shape_ sent to the Blazor client should be uniform (signal ID, scope, severity label,
human description). However, the _domain type_ should remain separate because:

- `PQ` findings reference delivery aggregates (`FeatureProgress`, `EpicProgress`).
- `SI / RR / RC` findings reference individual `WorkItemSnapshot` nodes.
- The two sets are produced at different points in the request pipeline and are never part of the
  same evaluation run.

Recommended approach: define a shared read-only transport DTO (`QualitySignalDto`) that carries
`SignalId`, `Scope`, `Severity`, and `Description`. Both the backlog-quality pipeline and the
Planning Quality pipeline produce `QualitySignalDto` rows. The UI consumes one list without caring
about the originating domain.

### 1.4 Recommended architecture

**Architecture: Parallel provider with shared DTO surface, no shared domain types.**

- `BacklogValidationService` remains the execution engine for `SI / RR / RC`.
- `PlanningQualityService` remains the execution engine for `PQ`.
- Both services emit `QualitySignalDto` for transport.
- A composite API handler (e.g., `GetQualitySignalSummaryQueryHandler`) merges outputs from both
  providers for a given product scope before returning to the client.
- `RuleCatalog` is NOT extended with `PlanningQuality` entries. `PQ` rules are catalogued in their
  own `PlanningQualityRuleCatalog` inside the delivery-trends domain.

### 1.5 Rejected alternatives

| Alternative | Reason rejected |
|---|---|
| Extend `RuleCatalog` / `BacklogValidationService` with `RuleFamily.PlanningQuality` | Forces PQ to evaluate at work-item scope; PQ inputs (FeatureProgress, EpicProgress) do not exist at that point in the pipeline |
| Single merged domain service | Collapses different evaluation scopes; breaks SRP; makes independent scaling and testing harder |
| PQ findings surfaced only through `BacklogHealthCalculator` | `BacklogHealthCalculator` uses opaque counters; PQ signals would lose identity and scope |

### 1.6 Exact integration points

| Concern | File |
|---|---|
| PQ engine | `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PlanningQualityService.cs` |
| PQ DI registration | `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` |
| Shared transport DTO | New: `PoTool.Shared/WorkItems/QualitySignalDto.cs` |
| Composite query handler | New: `PoTool.Api/Handlers/WorkItems/GetQualitySignalSummaryQueryHandler.cs` |
| PQ rule catalog | New: `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PlanningQualityRuleCatalog.cs` |

---

## 2. Blocking vs Non-Blocking Rule Model

### 2.1 Current state

The existing `RuleFindingClass` defines:

- `StructuralWarning` — reported, never blocking.
- `RefinementBlocker` — suppresses RC evaluation for the affected scope.
- `ImplementationBlocker` — blocks implementation-readiness derivation.

"Blocking" currently means **suppression of downstream rule evaluation**, not hard rejection of
ingestion or CDC builds. No rule currently prevents data from being stored or queried.

### 2.2 Recommended model

**Enforcement-point-specific blocking via an `IsBlocking` property on rule metadata.**

A single `IsBlocking: bool` flag on `RuleMetadata` is the cleanest extension. The flag is
interpreted differently depending on which enforcement point evaluates it:

| Enforcement point | Blocking behavior |
|---|---|
| Ingestion | No rule currently blocks ingestion; no new rule should do so without explicit product decision |
| CDC build | A blocking rule whose finding is active prevents the current CDC pass from updating the feature/epic progress cache for the affected scope |
| Query time | A blocking finding returns an API error or empty result for the affected scope, not a 500 |
| UI workflow action | A blocking finding disables the relevant action button (e.g., "Mark as ready") |

### 2.3 Which current rules should remain non-blocking

| Rule | Blocking today | Should remain non-blocking |
|---|---|---|
| `SI-1`, `SI-2`, `SI-3` | No (reported only) | Yes — structural integrity is informational |
| `RR-1`, `RR-2`, `RR-3` | Suppresses RC only | Yes — suppression is correct; hard block is not |
| `RC-1`, `RC-2`, `RC-3` | No | Yes |
| `PQ-1` through `PQ-7` | No | Yes — advisory signals |

All current rules should remain non-blocking at ingestion and CDC build. The suppression
semantics for `RR → RC` are retained as-is and are not expressed via `IsBlocking`.

### 2.4 Which future rules may need blocking semantics

| Scenario | Enforcement point | Severity |
|---|---|---|
| Epic without `ProjectNumber` when product settings require it | CDC build — skip progress update for affected epic | Medium |
| WorkPackage inconsistency within a project | Snapshot creation — block snapshot until resolved | Medium |
| Circular parent–child relationship detected | Ingestion — reject the revision | High |
| Override value outside `[0, 100]` | CDC build — reject the override, use calculated value | Medium |

### 2.5 Enforcement points

- **Ingestion**: only structural data corruption (e.g., circular hierarchy) should block; all
  other violations are recorded as findings and processing continues.
- **CDC build**: a blocking finding for a scope causes the CDC to emit a null or stale result
  for that scope; downstream consumers must handle null values without crashing.
- **Query time**: API handlers return the last valid cached result alongside any active blocking
  findings; no hard 404/500 for transient blocking states.
- **UI workflow action**: client-side guard using pre-fetched blocking findings; action button
  disabled with tooltip showing blocking signal ID.

### 2.6 Migration impact

- No existing tests break. `IsBlocking = false` on all current rules preserves existing behavior.
- CDC build blocking requires `PlanningQualityService` to communicate null scope results to the
  aggregation layer, which must propagate null correctly (already supported for `EpicProgress =
  null` per `docs/analyze/epic-aggregation-null-semantics-fix.md`).
- UI workflow blocking requires a new client-side check; no existing UI paths are affected until
  blocking rules are activated.

---

## 3. Snapshot Risk Recalibration

### 3.1 Previous score

The Prompt 9 risk table assessed Phase D (Snapshots) as: Impact 7 / Risk 6.

### 3.2 Corrected assessment

**Corrected: Impact 8 / Risk 8.**

The previous score underestimated risk because it did not account for:

1. `WorkPackage` field is not yet in the data model. Snapshot scoping by WorkPackage depends on
   a field that does not yet exist in retrieval, persistence, or DTOs. This is a hard prerequisite.
2. Historical truth requirement. A snapshot must capture and freeze work item values at the moment
   of creation. The current `WorkItemEntity` is a latest-state cache. Any snapshot that joins
   live rows at query time will silently drift as estimates change. The entity model must persist
   its own row data at capture time.
3. Deletion / correction semantics conflict with historical truth. The existing
   `RoadmapSnapshotService.DeleteSnapshotAsync()` allows permanent deletion. If delivery-trend
   snapshots are used as comparison baselines, deletion of a prior snapshot breaks the comparison
   chain. A correction or archival model is needed instead of hard delete.
4. Filtering impact. Snapshot comparison results are filtered by `Project + WorkPackage`. If
   `WorkPackage` is optional globally but required within a project once used, the filter contract
   must validate consistency before a comparison is allowed. No enforcement point exists today.

### 3.3 Top 3 failure modes

| Rank | Failure mode | Impact |
|---|---|---|
| 1 | Snapshot rows reference live `WorkItemEntity` at query time → historical baselines drift as live estimates change | Data integrity loss; comparison results are silently wrong |
| 2 | `WorkPackage` field absent at snapshot creation → scoping rule cannot be enforced → all snapshots fall into the unscoped bucket | Comparison returns wrong population; WorkPackage-based trending is impossible |
| 3 | Prior snapshot deleted by user → `SnapshotComparisonService` called with null previous snapshot → all deltas return null → trend lines break | UI shows incomplete delivery trend; no error surfaced to user |

### 3.4 Mitigations

| Failure mode | Mitigation |
|---|---|
| Baseline drift | Snapshot creation must capture field values from `WorkItemEntity` at the moment of creation and persist them in `BudgetSnapshotItemEntity` rows; no live joins at query time |
| Missing WorkPackage | Phase A must complete `WorkPackage` plumbing before Phase D snapshot creation is allowed for projects that use WorkPackage; validate at snapshot creation |
| Deletion breaks chain | Replace hard delete with soft-archive: mark snapshot as archived; `SnapshotComparisonService` skips archived snapshots; only allow hard delete when snapshot is not referenced by any active comparison |

---

## 4. EstimationMode Contract

### 4.1 Should EstimationMode be explicit?

**Yes.** Inference from null/non-null values is not acceptable because:

- A feature can have `Effort = null` either because effort was never entered (no estimation mode
  decision) or because the product is deliberately SP-only. These two cases require different UI
  treatment and different `PQ-1` signal behavior.
- A mixed-mode product (some features using Effort, others using SP) cannot be represented
  reliably without an explicit mode discriminator at product level.
- Future forecast logic (`FeatureForecastService`) already branches on whether Effort is present.
  Adding an explicit mode flag makes the branch condition self-documenting and testable in
  isolation.

### 4.2 At which levels

| Level | Explicit flag required | Rationale |
|---|---|---|
| Product settings | **Yes** — `EstimationMode` per product | This is where the mode decision lives; stored in `EffortEstimationSettingsEntity` or a new settings column |
| CDC / domain model | **Yes** — `FeatureProgress.EstimationMode` and `EpicProgress.EstimationMode` | The CDC pipeline must know the mode to apply the correct rollup path in `HierarchyRollupService` |
| API DTO | **Yes** — `FeatureProgressDto.EstimationMode` and `EpicProgressDto.EstimationMode` | UI must show the mode in context (e.g., "SP" vs "hours" labels) |
| UI | **Derived** — rendered from DTO; no local computation | UI reads the DTO value; no separate client-side enum |

### 4.3 Required enum values

```
StoryPoints   — canonical SP-based aggregation (default)
EffortHours   — effort-hours-based aggregation
Mixed         — both modes present within the same product; surfaced as a PQ signal
NoSPMode      — product settings indicate SP mode is explicitly disabled
```

`NoSPMode` is distinct from `EffortHours`: `NoSPMode` means the product owner has explicitly
opted out of SP tracking; `EffortHours` means effort-hours are used as the primary delivery
metric.

### 4.4 Mixed mode representation

A product whose features have inconsistent mode signals receives `EstimationMode = Mixed` in
the `ProductAggregationResult` DTO. This triggers `PQ-2` (feature missing progress basis) for
affected features and is surfaced as a `PQ` signal in the Planning Quality panel.

### 4.5 Why inference is not acceptable

Inference silently produces wrong results in two confirmed scenarios:

1. A feature with `Effort = null` in a `StoryPoints` product is treated as "SP-only" by the CDC —
   correct. But the same null `Effort` in a `EffortHours` product is an error state — incorrect
   to treat as the same case.
2. A product with `StoryPoints = null` across all PBIs is indistinguishable from a `NoSPMode`
   product unless the mode is stored explicitly. `PQ-2` would fire for every feature, masking
   the root cause.

---

## 5. Logging Semantics

### 5.1 Event taxonomy

| Category | Event name | Trigger | Value |
|---|---|---|---|
| CDC | `CdcAggregationRun` | End of each CDC pipeline pass | High |
| CDC | `CdcScopeSkipped` | Blocking finding causes scope to emit null/stale | High |
| Validation | `ValidationFindingCreated` | New finding appears that was not present in previous run | High |
| Validation | `ValidationFindingResolved` | Finding present in previous run is absent in current run | High |
| Validation | `ValidationFindingChanged` | Severity or description changed for existing finding | Medium |
| Planning Quality | `PqSignalEmitted` | `PlanningQualityService` emits a new signal | Medium |
| Planning Quality | `PqSignalResolved` | Signal present in previous evaluation is no longer emitted | Medium |
| Snapshot | `SnapshotCreated` | User or system creates a new delivery snapshot | High |
| Snapshot | `SnapshotArchived` | Snapshot is soft-archived | Medium |
| Snapshot | `SnapshotDeleted` | Hard delete of an archived snapshot | Low |
| Progress override | `OverrideApplied` | `TimeCriticality`-derived override changes `EffectiveProgress` | High |
| Progress override | `OverrideRemoved` | Override cleared; `EffectiveProgress` reverts to calculated | High |
| Health state | `HealthStateChanged` | Readiness score crosses a threshold (0/25/75/100) | Medium |

### 5.2 Required fields on every log entry

```
Timestamp       (UTC ISO-8601)
Event           (string — event name from taxonomy above)
ProductId       (int — owning product)
EpicId          (int? — relevant epic; null if product-scope event)
FeatureId       (int? — relevant feature; null if epic/product-scope)
WorkItemId      (int? — individual work item; null if aggregate event)
CorrelationId   (string — CDC pass ID or request ID)
PreviousValue   (string? — serialized previous state; null for creation events)
NewValue        (string? — serialized new state; null for deletion events)
Source          (string — service class name)
```

### 5.3 What counts as a "change" event

A change event is any event where `PreviousValue != NewValue`. For structured values (scores,
enums) this is a direct equality check. For finding lists this is a set-difference check.

Events where no change occurs MUST NOT be logged. Logging identical-state events is the primary
noise source in validation and health systems.

### 5.4 High-value vs noisy events

| Event | Classification |
|---|---|
| `CdcAggregationRun` | High-value — one entry per pass |
| `ValidationFindingCreated` / `Resolved` | High-value — direct quality signal |
| `OverrideApplied` / `Removed` | High-value — explicit human decision |
| `SnapshotCreated` | High-value |
| `PqSignalEmitted` | Medium — log at `Debug` by default; promote to `Information` when score drops below threshold |
| `ValidationFindingChanged` | Medium — log only when severity changes |
| `HealthStateChanged` | Medium — log only on threshold crossings |
| `CdcScopeSkipped` | High-value when blocking; `Debug` otherwise |
| `SnapshotDeleted` | Low — archived-only path |

### 5.5 Emission points

| Event | Service responsible |
|---|---|
| `CdcAggregationRun`, `CdcScopeSkipped` | `DeliveryProgressRollupService` |
| `ValidationFindingCreated/Resolved/Changed` | `ValidationComputeStage` (compares current vs previous cached findings) |
| `PqSignalEmitted/Resolved` | `PlanningQualityService` (or the composite handler that compares runs) |
| `SnapshotCreated/Archived/Deleted` | `RoadmapSnapshotService` and future `DeliverySnapshotService` |
| `OverrideApplied/Removed` | `FeatureProgressService` |
| `HealthStateChanged` | `BacklogStateComputationService` |

### 5.6 Noise-control strategy

1. Emit `ValidationFinding*` and `PqSignal*` events only when the set of active findings
   changes between runs. Store a hash of the current finding set alongside the cached validation
   result.
2. Rate-limit `CdcAggregationRun` entries to one per product per CDC pass; do not emit per
   feature or per epic.
3. Use structured log levels: `Debug` for low-value events; `Information` for high-value events.
   `Warning` only when a blocking condition is encountered. `Error` only for unrecoverable states.
4. Log fields must be structured (not interpolated strings) so log aggregators can filter by
   `ProductId`, `FeatureId`, `Event`, and `CorrelationId` without text parsing.

---

## 6. Data Maturity Feedback Loop

### 6.1 Design goal

The feedback loop must convert passive quality reporting into actionable guidance. A signal that
is visible but does not route users to the affected item and a fix path is passive reporting.
An actionable signal names the item, explains the gap, and links to the place where the gap can
be closed.

### 6.2 Surface-by-surface mapping

#### Validation Queue

**Signals shown**: `PQ-1` (missing effort on Feature), `PQ-2` (excluded feature), `RC-2`
(missing PBI effort), `RR-1/RR-2` (missing descriptions).

Each row must include a deep link to the affected work item in TFS so users can act immediately.
The queue must support filtering by `PQ` as a category key alongside `SI`, `RR`, `RC`, and `EFF`.

**Per-item**: yes. One row per affected work item or aggregate scope.

**Feedback loop**: Fix → next sync → finding resolved → row disappears from queue. The absence
of the row is the confirmation signal.

---

#### Health Workspace

**Signals shown**: Readiness score (0/25/75/100 threshold crossings), `HealthStateChanged`
events, current SI/RR/RC finding counts.

Add a `PlanningQualityScore` gauge alongside the existing health score so POs can distinguish
structural issues from planning maturity problems.

**Per-item**: no. Health shows aggregated counts and scores. Per-item detail lives in Validation
Queue.

**Feedback loop**: Trend line for `PlanningQualityScore` over sprints. Score rising = maturity
improving. Score flat = no progress. Score dropping = new planning gaps introduced.

---

#### Delivery Workspace

**Signals shown**: `PQ-5` (epic contains excluded features), `PQ-6` (product contains excluded
epics), `PQ-7` (missing forecast data), `EstimationMode = Mixed` badge.

Show inline badges on Feature and Epic cards that carry active PQ signals. Badge color follows
the severity scheme: orange for `Warning`, red for `Critical`, blue for `Info`.

**Per-item**: yes for feature/epic-level signals; aggregated for product-level signals.

**Feedback loop**: Inline badge links to the Validation Queue filtered by that signal ID. User
fixes the issue, next CDC pass clears the badge.

---

#### Planning Workspace

**Signals shown**: Missing `StoryPoints` on PBIs when product is in `StoryPoints` mode.
Missing `Effort` on Features when product is in `EffortHours` mode. `EstimationMode = Mixed`
warning banner.

Planning workspace is the primary fix surface. Inline edit or link to TFS work item must be
available from every PBI that carries a missing-estimation signal.

**Per-item**: yes. Planning workspace operates at PBI and Feature level.

**Feedback loop**: PO fills in missing estimate → sync → signal cleared → item disappears from
"needs estimation" list. The list length over time is the maturity metric.

---

#### Budget Workspace

**Signals shown**: Missing `ProjectNumber` on Epics (blocks budget grouping), `WorkPackage`
inconsistency within a project.

Budget workspace cannot function correctly when field contract violations exist. Show a
prominent warning banner when any required field (`ProjectNumber`, `WorkPackage`) is absent
from more than a configurable threshold of in-scope Epics.

**Per-item**: aggregated count + drill-down list. Not individual inline badges.

**Feedback loop**: Field gap count drives urgency. 0 gaps = budget grouping is reliable.
> 0 gaps = trust warning on all budget aggregates.

---

### 6.3 Per-item vs aggregated signals

| Signal | Per-item | Aggregated |
|---|---|---|
| `RC-2` (PBI missing effort) | Yes | Count in Health |
| `RR-1/RR-2` (missing description) | Yes | Count in Health |
| `PQ-1` (Feature missing effort) | Yes | Score in Delivery |
| `PQ-2` (excluded feature) | Yes | Count in Delivery |
| `PQ-5/PQ-6` (excluded features/epics) | Yes (epic/product) | Count in Health |
| `PQ-7` (missing forecast) | Yes (feature/epic) | Score in Delivery |
| `EstimationMode = Mixed` | No | Banner in Planning + Budget |
| `ProjectNumber` absent | Count | Banner in Budget |

### 6.4 Prioritization of signals

| Priority | Signal | Reason |
|---|---|---|
| P0 | `PQ-2` (excluded feature) | Directly suppresses progress visibility; most urgent for PO |
| P0 | `EstimationMode = Mixed` | Undermines all aggregate metrics |
| P1 | `PQ-1` (Feature missing effort) | Blocks forecast accuracy |
| P1 | `RC-2` (PBI missing effort) | Primary health signal; already surfaced |
| P2 | `PQ-5/PQ-6` (excluded scope) | Reduces confidence in delivery aggregates |
| P2 | `ProjectNumber` absent | Blocks budget grouping |
| P3 | `PQ-7` (missing forecast) | Advisory; data may still arrive |
| P3 | `PQ-3/PQ-4` (override signals) | Informational; PO is aware of override |

---

## 7. Corrections to Previous Audit

### C-3 correction — Planning Quality integration pattern

**Previous text (Prompt 9 Conflicts §C-3):**
> "PQ signals are implemented for delivery analytics but are not yet connected to the broader
> validation pipeline."

**Corrected text:**
PQ signals must NOT be connected to the existing `BacklogValidationService` / `RuleCatalog`
pipeline. They belong to the delivery analytics domain and must be exposed via a parallel
provider (`PlanningQualityService`) with a shared DTO surface (`QualitySignalDto`). The
integration point is a composite query handler, not a rule family extension.

---

### C-4 correction — EstimationMode

**Previous text (Prompt 9 Conflicts §C-4):**
> "Decide whether to add an explicit EstimationMode enum to the DTO contract (Phase A) or defer
> to Phase E as a UI enhancement only."

**Corrected text:**
Deferring `EstimationMode` to Phase E is not acceptable. The mode is required by the CDC
pipeline (`HierarchyRollupService`) in Phase B to select the correct rollup path. It must be
added to product settings and propagated into the CDC domain model and API DTOs in Phase A.
Inference from null/non-null values is rejected.

---

### Phase D risk correction

**Previous score:** Impact 7 / Risk 6.

**Corrected score:** Impact 8 / Risk 8.

Rationale: `WorkPackage` field absence, historical-truth requirements, and deletion/correction
semantics were under-weighted. See Section 3 above.

---

## 8. Final Recommendations

1. **Separate PQ from backlog validation** — keep `PlanningQualityService` in the delivery-trends
   domain; expose via `QualitySignalDto` and a composite handler; never extend `RuleCatalog`.

2. **Add `IsBlocking` to `RuleMetadata`** — default `false` on all current rules; activate
   selectively for future circular-hierarchy and override-range violations.

3. **Raise Phase D priority** — `WorkPackage` and snapshot freeze semantics are prerequisites
   for Phase D; schedule Phase A (WorkPackage plumbing) before Phase D begins.

4. **Make `EstimationMode` explicit at product settings level** — store in
   `EffortEstimationSettingsEntity` or an equivalent; propagate into CDC domain model and DTO
   in Phase A / Phase B.

5. **Implement structured logging at CDC and validation change boundaries** — use the event
   taxonomy from Section 5; suppress no-change events to control noise.

6. **Design feedback loop into Validation Queue and Delivery Workspace** — `PQ-2` and
   `EstimationMode = Mixed` are P0 signals; all PQ badges must carry a deep-link to the fix
   surface.
