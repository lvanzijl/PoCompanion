# Phase 24 interpretation layer

## 1. Summary

- IMPLEMENTED: the Phase 24 interpretation layer now converts the existing Phase 23c execution CDC slice into per-anomaly statuses and an overall execution state.
- IMPLEMENTED: the interpretation remains internal-only and does not modify the Phase 23c slice, planning logic, UI, or routing.
- VERIFIED: insufficient-evidence passthrough is preserved exactly from the CDC slice.
- DEVIATION: none.
- RISK: the median-centered spread interpretation now uses percentile bands derived from the existing spread-reference payload; later phases must keep downstream consumers aligned with that contract.

## 2. Interpretation logic implementation

### 2.1 Internal interpretation contract

- IMPLEMENTED: new internal Phase 24 models in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/ExecutionRealityCheck/ExecutionRealityCheckInterpretation.cs`

- IMPLEMENTED: the internal contract includes:
  - `ExecutionRealityCheckAnomalyStatus`
    - `Inactive`
    - `Weak`
    - `Strong`
  - `ExecutionRealityCheckOverallState`
    - `Stable`
    - `Watch`
    - `Investigate`
    - `InsufficientEvidence`
  - `ExecutionRealityCheckAnomalyInterpretation`
  - `ExecutionRealityCheckInterpretation`
  - `IExecutionRealityCheckInterpretationService`
  - `ExecutionRealityCheckInterpretationService`

### 2.2 Condition evaluation

- IMPLEMENTED: the interpreter evaluates anomaly conditions from:
  - current / ordered window values
  - baseline median
  - baseline spread reference

- IMPLEMENTED: the current implementation uses the repository’s shared percentile semantics to interpret the Phase 23c spread reference:
  - completion below typical: value deviation is at or below the 25th-percentile deviation band
  - spillover increasing: value deviation is at or above the 75th-percentile deviation band
  - completion variability high: absolute deviation is at or above the 75th-percentile absolute-deviation band

- VERIFIED: no new metrics were introduced.
- VERIFIED: no alternative baselines were introduced.
- VERIFIED: all three anomalies still use the Phase 23c median-centered baseline family only.

### 2.3 Persistence tracking

- IMPLEMENTED: trailing persistence is evaluated over the ordered 8-sprint window.
- IMPLEMENTED:
  - `Weak` triggers at 3 consecutive anomaly-positive sprints
  - `Strong` triggers at 4 or more consecutive anomaly-positive sprints
  - first normal sprint after an active anomaly downgrades to `Weak` pending clear
  - second consecutive normal sprint clears the anomaly back to `Inactive`

- IMPLEMENTED: `PersistenceLength` stores:
  - the active anomaly run length when the anomaly is active
  - `0` when the anomaly has fully cleared
  - the current trailing positive streak when the anomaly has not yet activated

### 2.4 Global state mapping

- IMPLEMENTED:
  - `Weak` = severity `1`
  - `Strong` = severity `2`

- IMPLEMENTED:
  - total severity `0` → `Stable`
  - total severity `1` → `Watch`
  - total severity `>= 2` → `Investigate`
  - insufficient evidence → `InsufficientEvidence`

## 3. Code references

### 3.1 Pure interpretation service

- IMPLEMENTED:
  - `ExecutionRealityCheckInterpretationService.Interpret(...)`
  - `InterpretAnomaly(...)`
  - `EvaluateCondition(...)`
  - `CountTrailingValues(...)`
  - File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/ExecutionRealityCheck/ExecutionRealityCheckInterpretation.cs`

### 3.2 API composition layer

- IMPLEMENTED: new composition service in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ExecutionRealityCheckInterpretationLayerService.cs`

- IMPLEMENTED: the API composition service:
  1. calls the existing `ExecutionRealityCheckCdcSliceService`
  2. passes the slice result into `IExecutionRealityCheckInterpretationService`
  3. returns the internal interpretation result

### 3.3 Dependency injection

- IMPLEMENTED: DI registration was added in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

- IMPLEMENTED:
  - `IExecutionRealityCheckInterpretationService` registered as singleton
  - `ExecutionRealityCheckInterpretationLayerService` registered as scoped

## 4. Test coverage

- IMPLEMENTED: new domain tests in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ExecutionRealityCheckInterpretationServiceTests.cs`

- IMPLEMENTED: test coverage includes:
  - `Interpret_WhenSpilloverConditionPersistsForThreeSprints_MarksWeakAndWatch`
  - `Interpret_WhenSpilloverConditionPersistsForFourSprints_MarksStrongAndInvestigate`
  - `Interpret_WhenCompletionVariabilityPersistsForThreeSprints_MarksWeak`
  - `Interpret_WhenFirstNormalSprintFollowsSustainedAnomaly_KeepsWeakPendingClear`
  - `Interpret_WhenTwoNormalSprintsFollowSustainedAnomaly_ClearsToInactive`
  - `Interpret_WhenMultipleAnomaliesAreActive_EscalatesToInvestigate`
  - `Interpret_WhenSliceHasInsufficientEvidence_PassthroughsInsufficientEvidence`

- VERIFIED: executed validation after implementation:
  - `dotnet build PoTool.sln`
  - `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-build`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build`

- VERIFIED: results:
  - `PoTool.Core.Domain.Tests`: 48 passed
  - `PoTool.Api.Tests`: 35 passed
  - `PoTool.Tests.Unit`: 2112 passed

## 5. Edge case handling

- IMPLEMENTED: insufficient evidence from Phase 23c suppresses anomaly output and returns `InsufficientEvidence`.
- IMPLEMENTED: zero-width spread bands do not create false-positive anomalies.
- IMPLEMENTED: the first normal sprint after a sustained anomaly keeps the anomaly active as weak pending clear.
- IMPLEMENTED: two consecutive normal sprints clear the anomaly.
- IMPLEMENTED: multiple weak anomalies combine into `Investigate` through severity summation.

## 6. Known limitations

- RISK: the interpretation layer operates over the 8-sprint window already produced by Phase 23c; it does not yet persist anomaly history beyond that window.
- RISK: the current percentile-band interpretation is internal-only and must stay aligned with future consumers so later phases do not reintroduce alternate spread semantics.
- RISK: the layer intentionally does not expose explanation text, routing, or UI labels in this phase.

## Final section

### IMPLEMENTED

- IMPLEMENTED: per-anomaly inactive/weak/strong interpretation
- IMPLEMENTED: 3-sprint weak trigger
- IMPLEMENTED: 4-sprint strong trigger
- IMPLEMENTED: 2-sprint clear rule
- IMPLEMENTED: severity mapping and overall state mapping
- IMPLEMENTED: insufficient-evidence passthrough
- IMPLEMENTED: API composition service on top of the existing Phase 23c slice
- IMPLEMENTED: focused interpretation-layer unit coverage

### VERIFIED

- VERIFIED: Phase 23c CDC slice files were not modified
- VERIFIED: no planning logic, UI, or routing was introduced
- VERIFIED: all existing test projects passed after the change
- VERIFIED: the implementation stays on the corrected Phase 22c metric definitions and severity/state mapping

### DEVIATIONS

- DEVIATION: none

### RISKS

- RISK: persistence is currently reconstructed from the available 8-sprint window rather than a longer-lived anomaly history store
- RISK: interpretation depends on the quality of the Phase 23c spread-reference payload
- RISK: any future attempt to widen anomaly metrics or add alternate baselines would violate the corrected design

### GO / NO-GO for Phase 25 (routing layer)

- GO: Phase 25 may proceed because the interpretation layer now produces per-anomaly status/persistence and overall state/severity without modifying CDC extraction semantics or introducing UI/routing behavior in this phase.
