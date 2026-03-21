# Prompt 2 — BuildQuality Data & Aggregation Contract Report

## 1. Purpose

Define the raw-data and aggregation contract for mapping TFS / Azure DevOps build, test-run, and coverage facts into the `BuildQuality` CDC slice without redefining the locked BuildQuality formulas, Unknown rules, or metric set.

## 2. In scope

- builds
- test runs
- coverage
- raw-field requirements needed to support the five locked `BuildQuality` metrics
- mapping from raw source fields to canonical aggregation inputs
- safe aggregation rules for caller-supplied default-branch data
- missing and incomplete data handling required to preserve the locked formulas

## 3. Out of scope

- UI
- CDC redesign
- storage schema
- ingestion scheduling
- new metrics
- application handler design
- consumer-specific DTOs
- feature-branch, nightly, or multi-branch aggregation

## 4. Locked decisions applied

The following constraints are repeated exactly from the CDC contract report and are confirmed as binding for this data and aggregation contract:

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
- only these canonical metrics are allowed:
  - `SuccessRate`
  - `TestPassRate`
  - `TestVolume`
  - `Coverage`
  - `Confidence`
- `Confidence = BuildThresholdMet + TestThresholdMet`
- `BuildThresholdMet = 1 when EligibleBuilds >= minimum_builds, otherwise 0`
- `TestThresholdMet = 1 when TestVolume >= minimum_tests, otherwise 0`
- percentages MUST NOT be averaged

## 5. Raw data requirements

### Builds

Required raw data for each build record in scope:

- build id
- branch
- result (`succeeded` / `failed` / `partiallySucceeded` / `canceled`)
- completion time

Repository-confirmed build raw field names currently visible in the existing TFS integration path are:

- `id`
- `sourceBranch`
- `result`
- `finishTime`

These are sufficient to support default-branch filtering, eligible-build classification, and caller-provided time scoping for build aggregation.

### Test Runs

Required raw data for each test-run record in scope:

- total tests
- passed
- notApplicable (or equivalent)
- association to build

Current repository evidence does **NOT** expose a test-run retrieval contract or persisted test-run entity, so the raw field names remain **UNCERTAIN** and must not be guessed.

### Coverage

Required raw data for each coverage record in scope:

- covered lines
- total lines
- association to build

Current repository evidence does **NOT** expose a coverage retrieval contract or persisted coverage entity, so the raw field names remain **UNCERTAIN** and must not be guessed.

## 6. Mapping (Raw → Canonical)

| Raw field | Canonical field | Transformation rule |
| --- | --- | --- |
| `id` | `BuildId` | Direct integer mapping. |
| `sourceBranch` | `Branch` | Direct string mapping. Used to restrict scope to the repository default branch only. |
| `result` | `BuildResult` | Map exactly as locked: `succeeded -> succeeded`, `failed -> failed`, `partiallySucceeded -> partiallySucceeded`, `canceled -> canceled`. No alternative interpretation is allowed. |
| `finishTime` | `CompletionTime` | Direct timestamp mapping used for externally provided time scoping. |
| `UNCERTAIN (test total field)` | `TotalTests` | Direct numeric mapping once the source field is identified. Until then, field name remains **UNCERTAIN**. |
| `UNCERTAIN (test passed field)` | `PassedTests` | Direct numeric mapping once the source field is identified. Until then, field name remains **UNCERTAIN**. |
| `UNCERTAIN (test notApplicable field or equivalent)` | `NotApplicableTests` | Direct numeric mapping to the locked `notApplicable` semantic. Equivalent source fields require an explicit adapter mapping and must not be silently guessed. |
| `UNCERTAIN (test build association field)` | `BuildId` | Direct linkage to the owning build. Without this linkage, the test-run record cannot safely participate in build-scoped aggregation. |
| `UNCERTAIN (coverage covered-lines field)` | `CoveredLines` | Direct numeric mapping once the source field is identified. Until then, field name remains **UNCERTAIN**. |
| `UNCERTAIN (coverage total-lines field)` | `TotalLines` | Direct numeric mapping once the source field is identified. Until then, field name remains **UNCERTAIN**. |
| `UNCERTAIN (coverage build association field)` | `BuildId` | Direct linkage to the owning build. Without this linkage, the coverage record cannot safely participate in build-scoped aggregation. |

## 7. Aggregation rules

### Builds

Build aggregation is count-first, then ratio:

1. caller supplies only default-branch build facts inside the desired time scope
2. map raw build results using the locked mapping
3. aggregate counts first:
   - `SucceededBuilds = count(result == succeeded)`
   - `FailedBuilds = count(result == failed)`
   - `PartiallySucceededBuilds = count(result == partiallySucceeded)`
   - `CanceledBuilds = count(result == canceled)` and are excluded
4. compute:
   - `EligibleBuilds = SucceededBuilds + FailedBuilds + PartiallySucceededBuilds`
   - `SuccessRate = SucceededBuilds / EligibleBuilds`

`SuccessRate` MUST be computed only after counts are aggregated. Per-build success percentages MUST NOT be averaged.

### Tests

Test aggregation is sum-first, then formula:

1. caller supplies only default-branch, in-scope test-run facts linked to in-scope builds
2. aggregate totals first:
   - `TotalTests = sum(total)`
   - `PassedTests = sum(passed)`
   - `NotApplicableTests = sum(notApplicable)`
3. compute:
   - `TestVolume = TotalTests - NotApplicableTests`
   - `TestPassRate = PassedTests / (TotalTests - NotApplicableTests)`

`TestPassRate` MUST be computed only after totals are summed. Test-run percentages MUST NOT be averaged.

### Coverage

Coverage aggregation is sum-first, then formula:

1. caller supplies only default-branch, in-scope coverage facts linked to in-scope builds
2. sum:
   - `CoveredLines = sum(covered_lines)`
   - `TotalLines = sum(total_lines)`
3. compute:
   - `Coverage = CoveredLines / TotalLines`

Coverage percentages MUST NOT be averaged. Coverage MUST be calculated from total `CoveredLines` and total `TotalLines` only.

### Confidence

Confidence uses only the locked thresholds after build and test aggregation is complete:

- `BuildThresholdMet = 1 when EligibleBuilds >= minimum_builds, otherwise 0`
- `TestThresholdMet = 1 when TestVolume >= minimum_tests, otherwise 0`
- `Confidence = BuildThresholdMet + TestThresholdMet`

No other heuristic, weight, or quality signal participates in `Confidence`.

## 8. Time scoping

- default branch only
- time window is provided externally
- `BuildQuality` remains time-agnostic at the CDC level

Caller responsibilities:

- select the repository default branch facts
- select the desired sprint window or rolling window externally
- pass only the in-scope build/test/coverage facts into the canonical aggregation step

CDC responsibility:

- compute the same five locked metrics over the supplied in-scope facts
- avoid embedding sprint selection or rolling-window rules into `BuildQuality` semantics

## 9. Missing data handling

### No builds

- if `EligibleBuilds == 0`, `SuccessRate` is `Unknown`
- `Confidence` cannot satisfy the build threshold when `EligibleBuilds < minimum_builds`

### No test runs

- if no test runs exist in scope, `TestPassRate` is `Unknown`
- `Confidence` cannot satisfy the test threshold when test sufficiency is not met

### No coverage

- if no coverage exists in scope, `Coverage` is `Unknown`
- if `TotalLines == 0`, `Coverage` is `Unknown`

### Partial data per build

- if a build record has no branch value, it cannot be proven to be on the repository default branch and must not be included in default-branch-scoped aggregation
- if a build record has no result value, it cannot be mapped to `succeeded`, `failed`, `partiallySucceeded`, or `canceled` and must not be counted in eligible build totals
- if a build record has no completion time, it cannot be safely included in externally time-scoped aggregation
- if a test-run record lacks build linkage, it cannot safely participate in the selected build scope
- if a coverage record lacks build linkage, it cannot safely participate in the selected build scope
- if a test-run or coverage record is missing numerator or denominator inputs required by the locked formulas, the missing value must not be silently converted to zero
- if incomplete test or coverage records leave no usable in-scope evidence, the corresponding `Unknown` rule applies

## 10. Data integrity risks

- missing linkage between build and tests
- missing coverage in some pipelines
- duplicate runs
- inconsistent per-build data

## 11. Consistency with CDC Report

- formulas match the CDC report exactly:
  - `SuccessRate = SucceededBuilds / EligibleBuilds`
  - `TestVolume = TotalTests - NotApplicableTests`
  - `TestPassRate = PassedTests / (TotalTests - NotApplicableTests)`
  - `Coverage = CoveredLines / TotalLines`
  - `Confidence = BuildThresholdMet + TestThresholdMet`
- Unknown rules match the CDC report exactly:
  - no eligible builds -> `SuccessRate = Unknown`
  - no test runs -> `TestPassRate = Unknown`
  - no coverage or `TotalLines == 0` -> `Coverage = Unknown`
- no additional metrics are introduced
- no deviations detected

## 12. Drift check

- assumptions made: none
- inferred fields: none; unverified test-run and coverage raw field names remain explicitly marked **UNCERTAIN**
- deviation from locked rules: none

No drift detected.

## 13. Open questions introduced

None.
