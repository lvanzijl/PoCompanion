# Prompt 1 — BuildQuality CDC Contract Report

## 1. Purpose

Define a repository-aware CDC slice named `BuildQuality` that owns canonical build/test/coverage quality semantics for the repository default branch without changing existing CDC slice ownership or dependency direction.

## 2. In scope

- define the `BuildQuality` slice responsibility and boundaries
- position `BuildQuality` within the existing CDC dependency model
- define only these canonical metrics:
  - `SuccessRate`
  - `TestPassRate`
  - `TestVolume`
  - `Coverage`
  - `Confidence`
- apply the locked build/test/coverage formulas exactly as given
- define explicit Unknown handling for insufficient builds, insufficient tests, and missing coverage
- define sprint-based and rolling-window usage semantics while keeping the CDC slice itself time-agnostic
- explain how the slice remains CDC-compliant

## 3. Out of scope

- UI layout, routes, navigation behavior, or page composition
- ingestion design
- storage design
- schema changes
- application handler design
- changes to existing CDC slices
- work-item linkage
- feature-branch, nightly, or multi-branch aggregation
- any metric beyond the five locked metrics

## 4. Locked decisions applied

- scope is the repository default branch only
- no nightly-build aggregation
- no feature-branch aggregation
- build success uses:
  - success = `succeeded`
  - failure = `failed + partiallySucceeded`
  - exclude = `canceled`
- `TestPassRate = passed / (total - notApplicable)`
- `Coverage = covered_lines / total_lines`
- tests are `Unknown` if no test runs exist in scope
- coverage is `Unknown` if no coverage exists in scope or `total_lines == 0`
- `minimum_builds = 3`
- `minimum_tests = 20`
- test types are not distinguished
- no linkage to WorkItems
- the Health area remains a hub with Backlog Health unchanged and Build Quality added as a separate consumer of this slice rather than merged into `BacklogQuality`

## 5. BuildQuality definition

### Name

`BuildQuality`

### Responsibility

Own the canonical repository-quality interpretation of build outcomes, test outcomes, test volume, coverage, and confidence for default-branch pipeline evidence.

### Boundaries

`BuildQuality` owns:

- canonical interpretation of eligible build outcomes for success/failure reporting
- canonical interpretation of test pass rate and applicable test volume
- canonical interpretation of coverage ratio
- canonical confidence derived only from the locked minimum thresholds

`BuildQuality` does not own:

- backlog, sprint-commitment, delivery-trend, forecast, effort, or portfolio semantics
- ingestion timing or retrieval mechanics
- persistence models or projection tables
- handler-specific DTOs or consumer-specific wording
- health navigation or delivery page behavior
- any relationship to WorkItems

## 6. CDC positioning

### Upstream dependencies

`BuildQuality` has no upstream dependency on existing CDC slices.

It depends only on repository-scoped build, test, and coverage facts that have already been materialized outside the CDC and then interpreted canonically inside the slice.

### Downstream consumers

Downstream consumers are application adapters that need:

- sprint-scoped delivery quality views
- rolling-window health quality views

### Independence from other slices

`BuildQuality` is independent from:

- `BacklogQuality`
- `SprintCommitment`
- `DeliveryTrends`
- `Forecasting`
- `PortfolioFlow`
- `EffortDiagnostics`
- `EffortPlanning`

It must not redefine or borrow semantics from those slices, and those slices must not redefine build/test/coverage semantics owned by `BuildQuality`.

## 7. Metrics

Only the following canonical metrics belong to `BuildQuality`:

### `SuccessRate`

Share of eligible default-branch builds that succeeded.

### `TestPassRate`

Share of applicable tests that passed within the selected scope.

### `TestVolume`

Applicable test count within the selected scope.

### `Coverage`

Line coverage ratio within the selected scope.

### `Confidence`

Threshold-satisfaction metric derived only from `minimum_builds` and `minimum_tests`.

No additional metrics are part of this slice.

## 8. Formulas

### `SuccessRate`

Eligible builds:

`EligibleBuilds = SucceededBuilds + FailedBuilds + PartiallySucceededBuilds`

Build result mapping:

- `SucceededBuilds = count(result == succeeded)`
- `FailedBuilds = count(result == failed)`
- `PartiallySucceededBuilds = count(result == partiallySucceeded)`
- `CanceledBuilds = count(result == canceled)` and are excluded

Formula:

`SuccessRate = SucceededBuilds / EligibleBuilds`

### `TestVolume`

`TestVolume = TotalTests - NotApplicableTests`

### `TestPassRate`

`TestPassRate = PassedTests / (TotalTests - NotApplicableTests)`

### `Coverage`

`Coverage = CoveredLines / TotalLines`

### `Confidence`

Let:

- `BuildThresholdMet = 1 when EligibleBuilds >= minimum_builds, otherwise 0`
- `TestThresholdMet = 1 when TestVolume >= minimum_tests, otherwise 0`

Formula:

`Confidence = BuildThresholdMet + TestThresholdMet`

The canonical confidence range is therefore:

- `0` when neither threshold is met
- `1` when exactly one threshold is met
- `2` when both thresholds are met

## 9. Unknown handling

### Insufficient builds

- if `EligibleBuilds == 0`, `SuccessRate` is `Unknown`
- if `0 < EligibleBuilds < minimum_builds`, `SuccessRate` is still computed from the available eligible builds, but `Confidence` cannot satisfy the build threshold

### Insufficient tests

- if no test runs exist in scope, `TestPassRate` is `Unknown`
- if test runs exist but `TestVolume < minimum_tests`, `TestPassRate` is still computed from the locked formula, but `Confidence` cannot satisfy the test threshold

### Missing coverage

- if no coverage exists in scope, `Coverage` is `Unknown`
- if `TotalLines == 0`, `Coverage` is `Unknown`

## 10. Confidence model

`Confidence` is determined only from the two locked minimum thresholds:

- build sufficiency threshold: `EligibleBuilds >= minimum_builds`
- test sufficiency threshold: `TestVolume >= minimum_tests`

With the locked values applied:

- `minimum_builds = 3`
- `minimum_tests = 20`

Result:

- `Confidence = 0` when `EligibleBuilds < 3` and `TestVolume < 20`
- `Confidence = 1` when exactly one of these is true:
  - `EligibleBuilds >= 3`
  - `TestVolume >= 20`
- `Confidence = 2` when `EligibleBuilds >= 3` and `TestVolume >= 20`

No other heuristics, weights, branch rules, or quality signals participate in confidence.

## 11. Time semantics

`BuildQuality` remains time-agnostic at the CDC level.

The slice accepts a caller-defined scope and computes the five metrics over whatever in-scope repository facts are provided.

### Sprint-based usage

For delivery-oriented consumers, the caller supplies the default-branch build/test/coverage facts that fall within a sprint scope, and `BuildQuality` computes the canonical metrics over that supplied set.

### Rolling-window usage

For health-oriented consumers, the caller supplies the default-branch build/test/coverage facts that fall within a rolling time window, and `BuildQuality` computes the same canonical metrics over that supplied set.

### CDC rule

Sprint windows and rolling windows are consumer selection semantics, not `BuildQuality` semantics.

## 12. CDC compliance explanation

`BuildQuality` respects one-way CDC dependencies because:

- it does not consume application handlers, DTOs, pages, or projection-specific shapes
- it does not depend on downstream consumers
- it has no reverse dependency from application, persistence, or UI layers

`BuildQuality` avoids redefining semantics because:

- it does not reinterpret backlog, sprint, forecast, effort, or portfolio meanings already owned elsewhere
- it owns only build/test/coverage quality semantics for the default branch
- it does not redefine upstream repository facts beyond the locked result mappings and formulas

`BuildQuality` avoids application leakage because:

- the slice defines canonical meanings only
- consumer-specific filtering, transport shapes, and presentation labels remain outside the CDC
- the Health hub decision affects consumer placement, not slice semantics

## 13. Drift check

No drift detected.

## 14. Open questions introduced

None.

## 15. Clarifications and Locks

### Build result interpretation

- `PartiallySucceeded` builds are included in `EligibleBuilds`
- `PartiallySucceeded` builds are treated as failures in `SuccessRate`

### Unknown vs Low Confidence

- `Unknown` strictly means absence of data:
  - no builds → `SuccessRate = Unknown`
  - no test runs → `TestPassRate = Unknown`
  - no coverage or `TotalLines == 0` → `Coverage = Unknown`

- Low confidence means:
  - data exists but does not meet thresholds

### Confidence semantics

- `Confidence = 0` → insufficient builds AND insufficient tests
- `Confidence = 1` → sufficient in exactly one dimension
- `Confidence = 2` → sufficient in both dimensions

### Coverage aggregation rule

- Coverage MUST be calculated using:
  - total `CoveredLines` (summed)
  - total `TotalLines` (summed)

- Coverage MUST NOT be calculated by averaging percentages
