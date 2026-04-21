# Phase 23c CDC slice implementation

## 1. Summary

- IMPLEMENTED: the Phase 23c execution reality-check CDC slice now reconstructs a strict 8-sprint team-local window, batch rebuilds `SprintFactResult`, computes canonical series, builds median-centered baselines, and emits raw anomaly inputs.
- IMPLEMENTED: evidence gating now rejects insufficient history, non-continuous ordering, and non-authoritative denominators before returning a slice.
- VERIFIED: the implementation reuses existing sprint CDC services and batched history-loading patterns instead of calling the single-sprint handler 8 times.
- DEVIATION: none.
- RISK: the slice remains an internal service and is not yet wired into an interpretation/query layer.

## 2. Implemented slice structure

### 2.1 Internal domain contract

- IMPLEMENTED: new internal Phase 23c models and projector in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Cdc/ExecutionRealityCheck/ExecutionRealityCheckCdcSlice.cs`

- IMPLEMENTED: the internal contract includes:
  - `ExecutionRealityCheckCdcSliceResult`
  - `ExecutionRealityCheckCdcSlice`
  - `ExecutionRealityCheckWindowRow`
  - `ExecutionRealityCheckBaseline`
  - `ExecutionRealityCheckSpreadReference`
  - `ExecutionRealityCheckAnomalyInput`
  - `IExecutionRealityCheckCdcSliceProjector`
  - `ExecutionRealityCheckCdcSliceProjector`

- IMPLEMENTED: canonical internal keys were added for:
  - `commitment-completion`
  - `spillover-rate`
  - `completion-below-typical`
  - `completion-variability`
  - `spillover-increase`

### 2.2 Batched reconstruction service

- IMPLEMENTED: new API orchestration service in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ExecutionRealityCheckCdcSliceService.cs`

- IMPLEMENTED: the service:
  1. resolves the anchor sprint
  2. resolves effective product scope
  3. loads all team sprints once
  4. selects the latest 8 completed sprints by team-local ordering
  5. validates continuity using `ISprintSpilloverService.GetNextSprintPath(...)`
  6. batch-loads resolved work items, work items, state history, and iteration history
  7. rebuilds `SprintFactResult` for each sprint
  8. computes `CommitmentCompletion` and `SpilloverRate`
  9. sets `HasAuthoritativeDenominator`
  10. returns either a full slice or insufficient evidence

### 2.3 Dependency injection

- IMPLEMENTED: service registration was added in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

- IMPLEMENTED:
  - `IExecutionRealityCheckCdcSliceProjector` registered as singleton
  - `ExecutionRealityCheckCdcSliceService` registered as scoped

## 3. Code references

### 3.1 Window reconstruction

- IMPLEMENTED: `SelectCompletedWindow(...)`
  - filters to dated completed sprints using `EndDateUtc < DateTime.UtcNow`
  - orders by `StartDateUtc`, then `SprintId`
  - takes the latest 8
  - File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ExecutionRealityCheckCdcSliceService.cs`

- IMPLEMENTED: `HasContinuousOrdering(...)`
  - validates the selected window against `GetNextSprintPath(...)`
  - requires exact next-window linkage for the first 7 rows
  - requires a resolvable next sprint for the latest completed sprint
  - File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ExecutionRealityCheckCdcSliceService.cs`

### 3.2 Batch fact reconstruction

- IMPLEMENTED: one batch load of:
  - `ResolvedWorkItems`
  - `WorkItems`
  - `ActivityEventLedgerEntries` for `System.State`
  - `ActivityEventLedgerEntries` for `System.IterationPath`

- VERIFIED: this follows the same batch pattern used in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`

- IMPLEMENTED: per-sprint fact rebuilding uses:
  - `ISprintFactService.BuildSprintFactResult(...)`
  - `ISprintExecutionMetricsCalculator.Calculate(...)`

### 3.3 Baseline and anomaly input projection

- IMPLEMENTED: `ExecutionRealityCheckCdcSliceProjector.TryProject(...)`
  - requires exactly 8 rows
  - rejects any row with `HasAuthoritativeDenominator == false`
  - rejects any row with `HasContinuousOrdering == false`
  - computes baselines for both canonical metrics
  - emits the 3 raw anomaly inputs required by Phase 23a

- IMPLEMENTED: baseline center uses:
  - `StatisticsMath.Median(...)`
  - File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`

- IMPLEMENTED: `SpreadReference` remains conceptual only and currently stores:
  - minimum observed value
  - maximum observed value
  - ordered deviation from median

## 4. Test coverage

- IMPLEMENTED: new SQLite-backed unit tests in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/ExecutionRealityCheckCdcSliceServiceTests.cs`

- IMPLEMENTED: test coverage includes:
  - `BuildAsync_SelectsLatestEightCompletedSprintsInTeamOrder`
  - `BuildAsync_ComputesCanonicalSeriesBaselinesAndAnomalyInputs`
  - `BuildAsync_ReturnsInsufficientEvidence_WhenFewerThanEightCompletedSprintsExist`
  - `BuildAsync_ReturnsInsufficientEvidence_WhenAnyWindowSprintLacksAuthoritativeDenominator`

- VERIFIED: executed validation after implementation:
  - `dotnet build PoTool.sln`
  - `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-build`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build`

- VERIFIED: results:
  - `PoTool.Core.Domain.Tests`: 41 passed
  - `PoTool.Api.Tests`: 35 passed
  - `PoTool.Tests.Unit`: 2112 passed

## 5. Evidence gating behavior

- IMPLEMENTED: the slice returns insufficient evidence when:
  - fewer than 8 completed sprints are available
  - any selected sprint cannot be converted into a dated sprint definition
  - the selected window is not continuous under the existing next-sprint lookup rule
  - no effective product scope exists
  - no resolved scoped work items exist
  - no scoped work-item snapshots exist
  - any reconstructed sprint has `CommittedStoryPoints - RemovedStoryPoints <= 0`

- VERIFIED: no interpretation thresholds or anomaly states were added.
- VERIFIED: no UI models, routes, or planning logic were changed.

## 6. Known limitations

- RISK: the slice is currently internal-only and not yet consumed by a Phase 24 interpretation/query layer.
- RISK: evidence gating is intentionally strict; sparse historical product/team data will return insufficient evidence rather than partial output.
- RISK: `SpreadReference` is still intentionally non-interpretive; Phase 24 must decide how to consume it without changing the Phase 23 raw contract.
- RISK: the service anchors team selection through the supplied sprint context; callers must pass the intended team stream.

## Final section

### IMPLEMENTED

- IMPLEMENTED: 8-sprint window reconstruction
- IMPLEMENTED: batched `SprintFactResult` reconstruction
- IMPLEMENTED: canonical `CommitmentCompletion` and `SpilloverRate` series extraction
- IMPLEMENTED: denominator authority flagging
- IMPLEMENTED: median-centered baseline projection
- IMPLEMENTED: raw per-anomaly input projection
- IMPLEMENTED: insufficient-evidence gating
- IMPLEMENTED: SQLite-backed unit coverage for success and failure paths

### VERIFIED

- VERIFIED: implementation follows Phase 22c/23a constraints without adding planning behavior, UI, routing, thresholds, or anomaly states
- VERIFIED: batch reconstruction reuses existing repository CDC services and historical data structures
- VERIFIED: all existing test projects passed after the change

### DEVIATIONS

- DEVIATION: none

### RISKS

- RISK: no external query/handler consumes the slice yet
- RISK: historical completeness still depends on current repository activity-ledger quality
- RISK: strict denominator gating may reduce coverage in sparse environments, by design

### GO / NO-GO for Phase 24 (interpretation layer)

- GO: Phase 24 may proceed on top of this slice because the implementation now provides the required ordered window rows, baselines, and raw anomaly inputs while preserving strict evidence gating and canonical metric semantics.
