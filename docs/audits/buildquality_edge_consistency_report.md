# BuildQuality Edge Case & Cross-Workspace Consistency Report

## 1. Scenarios validated

Validation used the existing seeded Battleship mock dataset for scenario presence and focused unit/query-handler coverage for calculation outputs. No BuildQuality formulas, retrieval contracts, UI structure, or seed sources were redesigned.

### Scenario A — Full data

- Seeded build: `910001`
- Build result: `Succeeded`
- Tests: `180 total`, `176 passed`, `2 not applicable`
- Coverage: `6800 / 8000 = 85.00%`
- Observed output with the canonical provider:
  - `SuccessRate > 0`
  - `TestPassRate = 176 / 178 = 98.88%`
  - `Coverage = 85.00%`
  - `Confidence = 2` in the focused full-data provider validation

### Scenario B — Tests only

- Seeded build: `910003`
- Build result: `PartiallySucceeded`
- Tests: `145 total`, `136 passed`, `3 not applicable`
- Coverage: none
- Observed output with the canonical provider:
  - `TestPassRate = 136 / 142 = 95.77%`
  - `Coverage = Unknown`
  - `Confidence = 1`
  - Unknown reason: `NoCoverage`

### Scenario C — Coverage only

- Seeded build: `910004`
- Build result: `Succeeded`
- Tests: none
- Coverage: `5300 / 7600 = 69.74%`
- Observed output with the canonical provider:
  - `Coverage = 69.74%`
  - `TestPassRate = Unknown`
  - `Confidence = 1`
  - Unknown reason: `NoTestRuns`

### Scenario D — No data

- Seeded build: `910005`
- Build result: `Failed`
- Tests: none
- Coverage: none
- Observed output with the canonical provider:
  - `SuccessRate = 0.00%`
  - `TestPassRate = Unknown`
  - `Coverage = Unknown`
  - `Confidence = 0`
  - Unknown reasons:
    - `NoTestRuns`
    - `NoCoverage`

Notes:

- The current BuildQuality model does not expose a separate composite “BuildQuality” scalar beyond the individual dimensions and `Confidence`.
- Unknown is therefore validated per metric dimension, not via a new aggregate status.

## 2. Cross-page comparison

Validated the same Product Owner and time window across the three existing workspace paths:

- Health rolling window
  - `/home/health`
  - rolling query: `2026-03-01T00:00:00Z` → `2026-03-15T00:00:00Z`
- Delivery / Sprint
  - sprint query for `Sprint 77`
- Pipeline Insights
  - pipeline detail for Product B / `Pipeline B` / pipeline definition `2001`

Observed values:

- Health rolling summary == Delivery sprint summary
  - `SuccessRate = 50.00%`
  - `TestPassRate = 95.24%`
  - `Coverage = 85.00%`
  - `Confidence = 1`
- Health Product B == Delivery Product B == Pipeline Insights Pipeline B
  - `SuccessRate = 0.00%`
  - `TestPassRate = 91.67%`
  - `Coverage = 80.00%`
  - `Confidence = 0`
  - evidence counts also matched exactly:
    - `EligibleBuilds = 1`
    - `TotalTests = 12`
    - `CoveredLines = 80`

Result:

- identical numeric values for the same scope
- no divergence in calculation
- no page-specific overrides detected

## 3. Binding validation

Confirmed current UI bindings stay passive:

- `HealthWorkspace.razor` calls `BuildQualityService.GetRollingWindowAsync(...)`
- `SprintTrend.razor` calls `BuildQualityService.GetSprintAsync(...)`
- `PipelineInsights.razor` calls `BuildQualityService.GetPipelineAsync(...)`

Confirmed client-side rendering uses backend DTO values directly:

- `BuildQualitySummaryComponent.razor`
- `BuildQualityCompactComponent.razor`
- `BuildQualityTooltipComponent.razor`
- `BuildQualityPresentation.cs`

Findings:

- no local metric recomputation in the client
- `Unknown` is rendered only from explicit backend unknown flags
- no local default fallback masks backend values

## 4. Logging validation

Validated sync logging in `PipelineSyncStage` for BuildQuality child ingestion.

Confirmed logs clearly show:

- number of builds requested for test-run retrieval
- number of builds requested for coverage retrieval
- number of returned test-run DTOs
- number of returned coverage DTOs
- number of persisted test-run rows
- number of persisted coverage rows

Validation also confirmed explicit missing-data reason logs now exist for scoped builds missing child facts:

- `BUILDQUALITY_TESTRUN_MISSING_DATA`
- `BUILDQUALITY_COVERAGE_MISSING_DATA`

These logs include:

- affected build count
- reason text explaining whether test-run or coverage rows are missing for scoped builds

## 5. Issues found

### FIXED

- Logging did not explicitly state the reason when scoped builds were missing persisted test-run or coverage data. Summary counts existed, but the missing-data cause was not called out directly.

### No other real issues found

- No BuildQuality formula drift
- No UI-side recomputation
- No cross-workspace numeric inconsistency in the validated scope

## 6. Final verdict

READY WITH FIXES

## Reviewer notes

### What changed

- extended focused automated validation for scenarios A-D
- added cross-workspace consistency assertions across rolling, sprint, and pipeline query paths
- tightened BuildQuality child-ingestion logging so missing-data reasons are explicit
- added this audit report

### What was intentionally not changed

- BuildQuality calculation logic
- TFS integration contracts
- UI structure
- seeded data sources

### Known limitations / follow-up

- validation is based on deterministic seeded mock data and focused unit/query-handler scope, not full production variability
- Pipeline Insights is compared using a single-pipeline product scope so the pipeline detail and product-level values are expected to match exactly
