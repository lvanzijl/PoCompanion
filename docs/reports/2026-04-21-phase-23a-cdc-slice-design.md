# Phase 23a CDC slice design

## 1. Summary

- VERIFIED: this phase defines the CDC slice inputs needed for the execution reality-check layer only.
- VERIFIED: this report follows Phase 22c and does not change planning logic, existing CDC semantics, API contracts, or UI.
- VERIFIED: the slice must own only historical execution-input preparation: sourcing, window extraction, baseline preparation, and anomaly input shaping.
- GAP: `CommitmentCompletion` and `SpilloverRate` are exposed in `SprintExecutionSummaryDto`, but they are not persisted as multi-sprint projection fields.
- RISK: if Phase 23 implementation tries to source the anomaly series from `SprintMetricsProjectionEntity` alone, it will lose the strict denominator required by Phase 22c.

## 2. Input data mapping

### 2.1 CommitmentCompletion per sprint

- VERIFIED: the canonical formula already exists in the Sprint Commitment domain: `DeliveredSP / (CommittedSP - RemovedSP)`.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/sprint-commitment-domain-model.md`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs`

- VERIFIED: the canonical numerator and denominator inputs already exist in the CDC output:
  - `CommittedStoryPoints`
  - `RemovedStoryPoints`
  - `DeliveredStoryPoints`
  - Source: `SprintFactResult`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/Sprints/SprintFactResult.cs`

- VERIFIED: an existing transport DTO already exposes the computed result:
  - `SprintExecutionSummaryDto.CommitmentCompletion`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/SprintExecutionDtos.cs`

- GAP: no persistence entity currently stores `CommitmentCompletion` per sprint-product row.
  - `SprintMetricsProjectionEntity` stores delivered and spillover outputs plus estimation-coverage counters, but not `CommittedSP`, `RemovedSP`, or `CommitmentCompletion`.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`

- VERIFIED: Phase 23a design therefore uses **Sprint Commitment CDC facts as the authoritative source** and treats the DTO field as downstream confirmation only.

### 2.2 SpilloverRate per sprint

- VERIFIED: the canonical formula already exists in the Sprint Commitment domain: `SpilloverSP / (CommittedSP - RemovedSP)`.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/sprint-commitment-domain-model.md`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs`

- VERIFIED: the canonical numerator and denominator inputs already exist in the CDC output:
  - `CommittedStoryPoints`
  - `RemovedStoryPoints`
  - `SpilloverStoryPoints`
  - Source: `SprintFactResult`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/Sprints/SprintFactResult.cs`

- VERIFIED: an existing transport DTO already exposes the computed result:
  - `SprintExecutionSummaryDto.SpilloverRate`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/SprintExecutionDtos.cs`

- GAP: no persistence entity currently stores `SpilloverRate` per sprint-product row.
  - `SprintMetricsProjectionEntity` stores `SpilloverStoryPoints`, but not `CommittedSP`, `RemovedSP`, or `SpilloverRate`.
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`

- VERIFIED: Phase 23a design therefore uses **Sprint Commitment CDC facts as the authoritative source** and treats the DTO field as downstream confirmation only.

### 2.3 Sprint ordering

- VERIFIED: sprint ordering metadata already exists in persistence through `SprintEntity`.
  - Required persisted fields:
    - `Id`
    - `TeamId`
    - `Path`
    - `StartDateUtc`
    - `EndDateUtc`
    - `TimeFrame`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`

- VERIFIED: the existing spillover logic already defines the current next-sprint ordering rule:
  - same team
  - later `StartUtc`
  - ascending `StartUtc`
  - tie-break by `SprintId`
  - Evidence: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`

- VERIFIED: Phase 23a uses that same team-local ordering rule and does not redefine sprint sequencing.

## 3. Window extraction design

### 3.1 Retrieval scope

- VERIFIED: the slice window is built from **the latest 8 completed sprints for one team-local ordered stream**.
- VERIFIED: a sprint is eligible only when:
  - `StartDateUtc` exists
  - `EndDateUtc` exists
  - `EndDateUtc` is in the past relative to the analysis timestamp

- ASSUMPTION: completed-sprint resolution should use the sprint date window rather than `TimeFrame == "past"` as the primary rule, because the repository already uses date-based ordering as the stable sort key.

### 3.2 Retrieval order

- VERIFIED: eligible sprints are ordered by:
  1. `StartDateUtc` ascending
  2. `SprintId` ascending as a tie-break

- VERIFIED: after ordering, the slice selects the latest contiguous block of 8 completed sprints.

### 3.3 Continuity validation

- VERIFIED: continuity is valid only when all of the following hold for the selected 8-sprint block:
  - exactly 8 completed sprints are present
  - every sprint has non-null `StartDateUtc` and `EndDateUtc`
  - the sprints form one ordered team-local sequence under the existing next-sprint lookup rule
  - every sprint in the block has a resolvable next sprint path for strict spillover interpretation

- GAP: the current repository does not expose a dedicated persisted “continuous completed-sprint window” artifact. Continuity must therefore be validated from `SprintEntity` ordering plus next-sprint resolution.

### 3.4 Missing-data handling

- VERIFIED: if fewer than 8 valid sprints remain after ordering and continuity checks, the slice returns **Insufficient evidence input state**.
- VERIFIED: if any sprint inside the candidate 8-sprint block is missing the required execution facts to compute strict rates, the entire block is rejected.
- VERIFIED: no partial window, fallback window, or mixed-quality window is allowed.

## 4. Exact anomaly input series

### 4.1 CommitmentCompletion series

- VERIFIED: the completion series is exactly:
  - one ordered value per sprint
  - value = `CommitmentCompletion`
  - source = `SprintFactResult` + `SprintExecutionMetricsCalculator`
  - window = latest valid 8 completed sprints

- VERIFIED: no alternative series is allowed.
- VERIFIED: delivered story points, completed PBI count, and mixed completion/throughput series are not part of this slice input.

### 4.2 SpilloverRate series

- VERIFIED: the spillover series is exactly:
  - one ordered value per sprint
  - value = `SpilloverRate`
  - source = `SprintFactResult` + `SprintExecutionMetricsCalculator`
  - window = latest valid 8 completed sprints

- VERIFIED: no alternative spillover series is allowed.
- VERIFIED: raw spillover story points without denominator normalization are not the anomaly series.

## 5. Baseline model

### 5.1 Typical range derivation

- VERIFIED: the baseline is derived only from the same 8-sprint anomaly input series that the slice evaluates.
- VERIFIED: there is one baseline per series:
  - completion baseline from the 8-value `CommitmentCompletion` series
  - spillover baseline from the 8-value `SpilloverRate` series

- VERIFIED: the baseline center is the **median** of the 8 ordered values.
- VERIFIED: spread is interpreted as the series’ **median-centered normal band**.
- VERIFIED: spread interpretation is conceptual only in this phase:
  - it describes how tightly or loosely the 8 values cluster around the median
  - it is used consistently for both anomaly comparison and variability preparation
  - it does not introduce a second metric

### 5.2 Median usage

- VERIFIED: the median is the only center reference used for:
  - “below typical completion”
  - “completion variability”
  - “spillover increase”

- VERIFIED: mean-based baselines are not part of this design.

### 5.3 Baseline reuse rule

- VERIFIED: `completion variability` uses the **same `CommitmentCompletion` series and the same median-centered baseline family** as `completion below typical`.
- VERIFIED: the slice must not create a separate variability-only baseline model.

## 6. Anomaly detection input requirements

### 6.1 Below typical completion

- VERIFIED: required values are:
  - current sprint `CommitmentCompletion`
  - ordered 8-sprint `CommitmentCompletion` series
  - completion-series median
  - completion-series spread reference
  - ordered sprint identities for persistence tracking

- VERIFIED: no throughput, count, or added-work substitute values are allowed.

### 6.2 Completion variability

- VERIFIED: required values are:
  - ordered 8-sprint `CommitmentCompletion` series
  - completion-series median
  - completion-series spread reference
  - per-sprint deviation context relative to that same median-centered spread
  - ordered sprint identities for persistence tracking

- VERIFIED: this anomaly uses one metric only: `CommitmentCompletion`.
- VERIFIED: no second variability metric is allowed.

### 6.3 Spillover increase

- VERIFIED: required values are:
  - current sprint `SpilloverRate`
  - ordered 8-sprint `SpilloverRate` series
  - spillover-series median
  - spillover-series spread reference
  - ordered sprint identities for persistence tracking

- VERIFIED: raw spillover story points may exist as supporting reconstruction facts, but they are not anomaly inputs.

## 7. Minimal internal output contract

### 7.1 Window row contract

- VERIFIED: the slice should materialize one internal row per valid sprint in the 8-sprint window with:
  - `SprintId`
  - `SprintPath`
  - `TeamId`
  - `StartDateUtc`
  - `EndDateUtc`
  - `CommitmentCompletion`
  - `SpilloverRate`
  - `HasAuthoritativeDenominator`
  - `HasContinuousOrdering`

- VERIFIED: this row is internal CDC preparation output only. It is not a DTO and not a UI model.

### 7.2 Baseline contract

- VERIFIED: the slice should produce one internal baseline object per metric with:
  - `MetricKey`
  - `WindowSprintIds`
  - `WindowValues`
  - `Median`
  - `SpreadReference`

- VERIFIED: `SpreadReference` is conceptual in this phase and represents the normal band derived from the 8-value window.

### 7.3 Per-anomaly contract

- VERIFIED: each anomaly should expose only the minimum internal fields needed for later verification and persistence logic:
  - `AnomalyKey`
  - `MetricKey`
  - `CurrentSprintId`
  - `CurrentValue`
  - `BaselineMedian`
  - `BaselineSpreadReference`
  - `OrderedWindowSprintIds`
  - `OrderedWindowValues`

- VERIFIED: this contract intentionally omits:
  - thresholds
  - state labels
  - explanation text
  - routes
  - UI wording

### 7.4 Persistence-tracking input rule

- VERIFIED: persistence tracking in later phases must be derived from the ordered raw series plus sprint IDs, not from pre-labeled anomaly booleans in this phase.
- VERIFIED: Phase 23a therefore outputs the ordered history needed for future 3-sprint / 4-sprint persistence evaluation, but it does not define thresholds yet.

## Final section

### VERIFIED

- VERIFIED: the authoritative anomaly metrics remain `CommitmentCompletion` and `SpilloverRate`.
- VERIFIED: those rates are already canonically defined and computable from `SprintFactResult` plus `SprintExecutionMetricsCalculator`.
- VERIFIED: sprint ordering already exists in `SprintEntity` and `SprintSpilloverLookup`.
- VERIFIED: the window is exactly the latest valid 8 completed sprints.
- VERIFIED: the baseline is median-centered and derived from the same exact anomaly series.
- VERIFIED: the minimal output contract is internal only and excludes UI, routing, and wording.

### ASSUMPTIONS

- ASSUMPTION: completed sprint selection should use past date windows as the primary rule rather than trusting `TimeFrame` alone.
- ASSUMPTION: the same team-local sprint ordering contract used for spillover lookup is the correct continuity contract for the reality-check slice.
- ASSUMPTION: the future implementation can request or reconstruct per-product sprint facts without changing existing transport contracts in this phase.

### GAPS

- GAP: no existing persistence entity stores `CommitmentCompletion` per sprint-product row.
- GAP: no existing persistence entity stores `SpilloverRate` per sprint-product row.
- GAP: no dedicated persisted continuity-validation artifact exists for “latest 8 valid completed sprints.”
- GAP: estimation-authority gating still depends on combining Sprint Commitment facts with existing projection counters such as `UnestimatedDeliveryCount`, not on one existing prebuilt CDC output.

### RISKS

- RISK: implementing against `SprintMetricsProjectionEntity` alone would violate the Phase 22c denominator rules.
- RISK: if sprint ordering metadata is incomplete for the latest completed sprint, strict spillover series construction will fail and correctly degrade to insufficient evidence.
- RISK: if estimation coverage is weak in recent sprints, the strict 8-sprint window may collapse and reduce product coverage.
- RISK: if later phases add thresholds directly into the slice contract, the CDC design could drift beyond the narrow input-preparation role defined here.

### GO / NO-GO for Phase 23b verification

- GO: Phase 23b verification may proceed, provided it verifies that the slice uses Sprint Commitment facts plus canonical rate calculation as the source of truth, not projection-only shortcuts.
