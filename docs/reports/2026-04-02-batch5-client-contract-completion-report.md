# Batch 5 — Client-Side Contract Completion Report

## 1. Violations fixed
The Batch 4 target violation list has been completed for the requested scope only:

- `PoTool.Client/Components/Forecast/ForecastPanel.razor`
- `PoTool.Client/Components/BacklogHealth/BacklogHealthPanel.razor`
- `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor`
- `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Pages/Home/BugOverview.razor`
- `PoTool.Client/Components/ReleasePlanning/AddLaneDialog.razor`

For all eight targets:

- raw cache-backed `IMetricsClient` usage was removed
- raw cache-backed `IPullRequestsClient` usage was removed where applicable
- raw cache-backed `WorkItemService` calls were replaced with DataState-aware calls
- render decisions now flow from explicit DataState state instead of raw DTO assumptions

## 2. Components updated
### Shared guardrails and service layer
- added `PoTool.Client/Components/Common/DataStateView.razor`
  - reusable wrapper that standardizes:
    - Loading
    - NotReady
    - Available
    - Empty
    - Failed

- extended `PoTool.Client/Services/WorkItemService.cs`
  - `GetAllStateAsync`
  - `GetByRootIdsStateAsync`

- extended `PoTool.Client/Services/PullRequestStateService.cs`
  - `GetSprintTrendsStateAsync`

### Target components/pages
- `ForecastPanel`
  - now uses `MetricsStateService.GetEpicForecastStateAsync`
  - renders via `DataStateView`

- `BacklogHealthPanel`
  - now uses `MetricsStateService.GetMultiIterationBacklogHealthStateAsync`
  - renders via `DataStateView`

- `EffortDistributionPanel`
  - now uses `MetricsStateService.GetEffortDistributionStateAsync`
  - renders via `DataStateView`

- `CapacityCalibrationPanel`
  - now uses `MetricsStateService.GetCapacityCalibrationStateAsync`
  - renders via `DataStateView`

- `PlanBoard`
  - board hierarchy now uses `WorkItemService.GetByRootIdsStateAsync`
  - calibration now uses `MetricsStateService.GetCapacityCalibrationStateAsync`
  - main board render now gates on explicit DataState

- `TrendsWorkspace`
  - bug trend now uses `WorkItemService.GetAllWithValidationStateAsync`
  - PR trend now uses `PullRequestStateService.GetSprintTrendsStateAsync`
  - tile fallback behavior is now DataState-driven

- `BugOverview`
  - now uses `WorkItemService.GetAllStateAsync`
  - page render now gates on explicit DataState

- `AddLaneDialog`
  - now uses `WorkItemService.GetAllStateAsync`
  - dialog content now gates on explicit DataState

## 3. Removed patterns
Removed from the target scope:

- direct raw cache-backed client calls:
  - `MetricsClient.*`
  - `PullRequestsClient.*`
  - `WorkItemService.GetAllAsync()`
  - `WorkItemService.GetByRootIdsAsync()`
  - `WorkItemService.GetAllWithValidationAsync()`

- exception-message driven UI for normal cache-backed reads:
  - `ex.Message` display paths removed from the target violation set
  - cache-backed render flow no longer depends on thrown transport exceptions

- raw DTO-first render assumptions:
  - targets no longer render straight from cache-backed DTO results without first resolving DataState

## 4. Guardrails added
### UI guardrail
`PoTool.Client/Components/Common/DataStateView.razor`

This wrapper makes it materially harder to render cache-backed UI without an explicit DataState model because the intended rendering path is now:

1. obtain `DataStateViewModel<T>`
2. pass it into `DataStateView`
3. render content only from the available-state child content

### Regression audit
`PoTool.Tests.Unit/Audits/ClientDataStateContractBatch5AuditTests.cs`

It fails when any Batch 5 target file:

- injects raw cache-backed clients
- calls forbidden raw cache-backed methods directly
- lacks explicit DataState usage markers

## 5. Before vs After
### Before
- target UIs mixed raw DTO loading with exception-driven fallbacks
- cache-backed rendering behavior was inconsistent across analytical surfaces
- multiple targets still assumed cache-backed calls would either:
  - return raw DTO data
  - throw and be converted into ad hoc UI error handling

### After
- all eight Batch 5 targets consume DataState-aware service calls only
- target rendering is state-first and consistent:
  - Loading
  - NotReady
  - Available
  - Empty
  - Failed
- the target scope no longer relies on:
  - raw cache-backed DTO assumptions
  - exception-driven normal rendering
  - ambiguous client contract behavior

## 6. Remaining violations (must be empty)
None.

All requested Batch 5 target violations were converted to explicit DataState consumption, and the Batch 5 audit now locks that scope against regression.

## 7. Confidence (must be High)
High.

Reasoning:

- the change is intentionally scoped to the exact Batch 4 carry-over target list
- build passes
- targeted Batch 4 and Batch 5 contract audits pass
- an explicit regression audit now checks the exact target files for raw cache-backed call regressions
- manual repository search confirms the forbidden raw cache-backed call patterns are gone from the requested target scope

Validation note:
- the full unit test project still has unrelated pre-existing `CachedPipelineReadProviderSqliteTests` failures outside this change scope
