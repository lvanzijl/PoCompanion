# BuildQuality Calculation Validation Report

## 1. Scope validated

Validated BuildQuality calculation and API/query flow against seeded cached data in focused unit validation covering:

- rolling scope
  - Product Owner `1`
  - window `2026-03-01T00:00:00Z` to `2026-03-15T00:00:00Z`
- sprint scope
  - Product Owner `1`
  - Sprint `77` (`Sprint 77`)
- pipeline scope
  - Product Owner `1`
  - Sprint `77`
  - Pipeline definition `2001` (`Pipeline B`)

Sampled cached records:

- Product A / Repo A / Pipeline A
  - included build: `1001` (`Succeeded`, default branch)
  - excluded build: `1002` (`Failed`, feature branch)
  - excluded build: `1003` (`Failed`, outside window)
- Product B / Repo B / Pipeline B
  - included build: `2001` (`PartiallySucceeded`, default branch)

## 2. Provider input validation

Validated through query-handler tests that capture provider inputs after scope loading:

- rolling scope
  - builds present: yes (`1001`, `2001`)
  - test runs present: yes (`1001`, `2001`)
  - coverage present: yes (`1001`, `2001`)
- sprint scope
  - builds present: yes (`1001`, `2001`)
  - test runs present: yes (`1001`, `2001`)
  - coverage present: yes (`1001`, `2001`)
- pipeline scope
  - builds present: yes (`2001`)
  - test runs present: yes (`2001`)
  - coverage present: yes (`2001`)

Conclusion: provider input is populated correctly for all three validated scopes and default-branch/window filtering is applied before provider calculation.

## 3. Metric validation

Validated with the real `BuildQualityProvider` through rolling, sprint, and pipeline query-handler tests.

### Rolling summary

- `SuccessRate = 1 / 2 = 0.5`
- `TestPassRate = 20 / (22 - 1) = 20 / 21 = 0.952381`
- `Coverage = 170 / 200 = 0.85`
- `Confidence = 1`
  - `BuildThresholdMet = false` (`2 < 3`)
  - `TestThresholdMet = true` (`21 >= 20`)
- Unknown behavior
  - `SuccessRateUnknown = false`
  - `TestPassRateUnknown = false`
  - `CoverageUnknown = false`

### Sprint summary

Sprint `77` uses the same date window as the validated rolling sample, so the computed summary values match the rolling summary exactly:

- `SuccessRate = 0.5`
- `TestPassRate = 20 / 21 = 0.952381`
- `Coverage = 0.85`
- `Confidence = 1`
- Unknown flags remain `false`

### Pipeline summary (`Pipeline B`)

- `SuccessRate = 0 / 1 = 0`
- `TestPassRate = 11 / 12 = 0.916667`
- `Coverage = 80 / 100 = 0.8`
- `Confidence = 0`
  - `BuildThresholdMet = false`
  - `TestThresholdMet = false`
- Unknown behavior
  - `SuccessRateUnknown = false`
  - `TestPassRateUnknown = false`
  - `CoverageUnknown = false`

### Product breakdown checks

- Product A
  - `SuccessRate = 1`
  - `TestPassRate = 9 / 9 = 1`
  - `Coverage = 90 / 100 = 0.9`
  - `Confidence = 0`
- Product B
  - `SuccessRate = 0`
  - `TestPassRate = 11 / 12 = 0.916667`
  - `Coverage = 80 / 100 = 0.8`
  - `Confidence = 0`

Conclusion: metrics are calculated correctly, unknown handling is correct for the validated populated-data paths, and no impossible values appeared in the validated scenarios.

## 4. Evidence validation

Validated evidence against the seeded cached records used by the tests.

### Rolling / Sprint summary evidence

- `EligibleBuilds = 2`
- `SucceededBuilds = 1`
- `FailedBuilds = 0`
- `PartiallySucceededBuilds = 1`
- `TotalTests = 22`
- `PassedTests = 20`
- `NotApplicableTests = 1`
- `CoveredLines = 170`
- `TotalLines = 200`

### Pipeline summary evidence (`Pipeline B`)

- `EligibleBuilds = 1`
- `SucceededBuilds = 0`
- `FailedBuilds = 0`
- `PartiallySucceededBuilds = 1`
- `TotalTests = 12`
- `PassedTests = 11`
- `NotApplicableTests = 0`
- `CoveredLines = 80`
- `TotalLines = 100`

Conclusion: evidence values matched the stored data exactly for all validated scopes.

## 5. API validation

Validated the API/query flow in two layers:

1. query handlers with the real `BuildQualityProvider`
2. controller inspection

Findings:

- rolling
  - `GetBuildQualityRollingWindowQueryHandler` returns the computed summary and per-product DTOs directly from scoped provider results
- sprint
  - `GetBuildQualitySprintQueryHandler` returns the computed summary and per-product DTOs directly from scoped provider results
- pipeline
  - `GetBuildQualityPipelineDetailQueryHandler` returns the computed scoped result directly from provider output and correctly projects pipeline metadata for pipeline definition `2001`
- controller
  - `BuildQualityController` is a thin mediator pass-through and does not alter handler DTOs

Conclusion: API output fields are populated correctly for the validated rolling, sprint, and pipeline flows, and there is no remaining projection logic based on the earlier empty-child-data assumption.

## 6. Issues found

No real functional issues were reproduced in:

- provider calculation
- scope loading
- API/query projection

Classification:

- CRITICAL: none
- MAJOR: none
- MINOR: none

## 7. Final verdict

READY

## Reviewer notes

### What changed

- validated BuildQuality calculation end-to-end across rolling, sprint, and pipeline scopes using real scoped build, test-run, and coverage inputs
- expanded focused unit coverage to verify populated provider inputs and combined metric/evidence outputs
- added this audit report

### What was intentionally not changed

- no TFS retrieval logic
- no schema changes
- no UI changes
- no BuildQuality formula redesign

### Known limitations / follow-up

- validation is based on deterministic seeded cached data in unit tests, not a live TFS environment
- no remaining functional issues were identified in the validated flow
