# PoTool Metrics Domain Audit

## Summary

### Files analyzed

- `docs/architecture/domain-model.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`
- `docs/rules/estimation-rules.md`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Shared/Metrics/CapacityCalibrationDto.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

### Verdict

**Needs fixes**

The repository is mostly aligned on the core delivery semantics: velocity in sprint metrics and sprint trend projections is now based on resolved PBI story points, first Done transitions, and canonical story-point resolution; spillover detection uses committed scope plus next-sprint movement; and activity metrics exclude trivial metadata fields while propagating descendant activity. The remaining gaps are concentrated in the capacity-calibration surface and in sprint execution summaries: the capacity handler still treats effort-hours as "velocity", no hours-per-story-point diagnostic is exposed, and the sprint execution surface reconstructs churn inputs without producing the canonical story-point formulas defined by the domain model.

## Domain Rules Reviewed

- `docs/architecture/domain-model.md` §§ 2.3-2.4, 3.3-3.12, 5.1-5.10, 7
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`
- `docs/rules/estimation-rules.md`

## Compliant Areas

- **Velocity in sprint metrics is canonical.**  
  `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` reconstructs committed scope via `SprintCommitmentLookup`, attributes delivery from `FirstDoneDeliveryLookup`, and sums only authoritative PBI estimates resolved through `ICanonicalStoryPointResolutionService`. Missing and derived estimates are excluded from delivered story points, while `BusinessValue` fallback remains available when `StoryPoints` is absent.

- **Sprint trend projections preserve canonical story-point delivery and spillover semantics.**  
  `PoTool.Api/Services/SprintTrendProjectionService.cs` computes `CompletedPbiStoryPoints`, `PlannedStoryPoints`, and `SpilloverStoryPoints` separately from effort-hours. `ComputeProductSprintProjection()` counts delivered story points only when a PBI's first Done transition falls inside the sprint window and excludes derived estimates from velocity totals.

- **Spillover detection is aligned with the domain model.**  
  `PoTool.Api/Services/SprintSpilloverLookup.cs` requires all three canonical conditions: the item was committed, the item was not Done at sprint end, and the first post-sprint iteration move was a direct move from Sprint N to Sprint N+1. The spillover tests in `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` and `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` cover direct carry-over, backlog round-trip exclusion, and unfinished-still-on-sprint exclusion.

- **Activity metrics follow the "meaningful change" rule.**  
  `PoTool.Api/Services/SprintTrendProjectionService.cs` excludes `System.ChangedBy` and `System.ChangedDate` from worked/activity classification and bubbles descendant activity upward to parent backlog items. `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` likewise filters those trivial fields and includes descendants in the returned activity feed, which matches the domain rule that activity includes descendant updates and ignores metadata-only noise.

- **Forecasting uses canonical story-point scope rather than effort-based velocity.**  
  `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` rolls up feature and epic scope through `CanonicalStoryPointResolutionService`, then derives estimated velocity from `CompletedStoryPoints` returned by `GetSprintMetricsQueryHandler`. This keeps forecasting tied to Story Points while leaving Effort available as a separate diagnostic concept.

## Violations Found

| Priority | File | Class | Method | Rule violation |
| --- | --- | --- | --- | --- |
| P0 | `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | `GetCapacityCalibrationQueryHandler` | `Handle` | Capacity calibration still defines committed work as `PlannedEffort`, done work as `CompletedPbiEffort`, and "velocity" as completed PBI effort-hours. This violates `docs/rules/metrics-rules.md` and `docs/rules/estimation-rules.md`, which define velocity from delivered Story Points and keep Effort separate from velocity. |
| P0 | `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` | `SprintCalibrationEntry`, `CapacityCalibrationDto` | record shape | The shared contract encodes the same non-canonical semantics (`Committed`, `Done`, and percentile "velocity" are all effort-based) and does not expose the domain's diagnostic `HoursPerSP = DeliveredEffort / DeliveredStoryPoints` metric. The capacity surface therefore cannot report the canonical capacity-validation metric. |
| P1 | `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | `Handle` | The handler correctly reconstructs added and removed scope from `System.IterationPath` history after the commitment timestamp, but it stops at counts and effort totals. It does not calculate `AddedSP`, `RemovedSP`, `ChurnRate`, `CommitmentCompletion`, or `AddedDeliveryRate`, so the canonical sprint-execution metrics defined in `docs/rules/metrics-rules.md` are incomplete. |
| P1 | `PoTool.Shared/Metrics/SprintExecutionDtos.cs` | `SprintExecutionSummaryDto` | record shape | The summary DTO exposes only counts and effort-hours for initial scope, added scope, removed scope, completed scope, unfinished scope, and spillover. Without Story Point numerators/denominators, the API cannot surface the canonical churn, commitment-completion, spillover-rate, or added-delivery formulas required by the domain model. |

## Architectural Risks

- **The capacity endpoint currently codifies the wrong semantic contract.**  
  Because both the handler and DTO call effort-based throughput "velocity", downstream consumers and tests (`PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`) reinforce a non-canonical definition. Fixing this later will require coordinated changes across the handler, DTOs, tests, and any client visualizations that consume the calibration payload.

- **Sprint execution already has the hard historical inputs, but not the canonical outputs.**  
  `GetSprintExecutionQueryHandler` reconstructs commitment, additions, removals, first-Done delivery, and spillover from ledger events. That is the difficult historical work. The remaining gap is now mostly an application-contract problem: the DTO and summary logic need Story Point-based aggregates and rate formulas, otherwise different consumers may re-derive them inconsistently.

- **Portfolio stock/flow trend metrics still rely on effort proxies rather than canonical churn semantics.**  
  `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` explicitly documents that `AddedEffort` is proxied from `SprintMetricsProjection.PlannedEffort`, not from iteration-change events after commitment. That is acceptable for a coarse effort trend, but it is not a canonical churn metric and should not be reused as one.

## Recommended Fixes

1. **Correct capacity calibration semantics first.**  
   Update `GetCapacityCalibrationQueryHandler` and `CapacityCalibrationDto` so Story Points remain the velocity denominator/numerator, and expose effort-hours as a separate diagnostic input rather than calling it velocity.

2. **Add the missing hours-per-story-point capacity metric.**  
   Surface `DeliveredEffort / DeliveredStoryPoints` as the explicit capacity-validation metric described in `docs/architecture/domain-model.md` §3.12 and `docs/rules/estimation-rules.md`.

3. **Extend sprint execution summaries with Story Point-based formulas.**  
   Build `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, and `SpilloverSP` from the already reconstructed work-item sets, then publish `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate`.

4. **Keep effort-only trend proxies clearly separated from canonical churn analytics.**  
   If `GetPortfolioProgressTrendQueryHandler` remains effort-based, document it as a non-canonical stock/flow helper and avoid reusing it as churn or sprint-scope truth.

## Final Compliance Classification

**Needs fixes**

### Prioritized fix list

1. Replace effort-based "velocity" in `GetCapacityCalibrationQueryHandler` with Story Point-based velocity semantics.
2. Add explicit `HoursPerSP` capacity diagnostics to the capacity calibration contract.
3. Extend sprint execution summaries to calculate and return the canonical Story Point churn and completion formulas.
4. Keep portfolio effort-trend proxies isolated from canonical churn reporting to prevent future metric drift.

## Fix Progress — Capacity Calibration Semantics

- Updated `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` so capacity calibration now uses canonical delivered PBI story points for velocity percentiles and predictability, while keeping delivered effort only as a diagnostic metric. Committed story points now exclude derived estimates by subtracting `DerivedStoryPoints` from the projection's aggregate planned story points.
- Updated `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` so per-sprint entries use `CommittedStoryPoints`, `DeliveredStoryPoints`, `DeliveredEffort`, and `HoursPerSP` naming instead of effort-based `Committed` / `Done` semantics.
- Expanded `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs` to verify velocity is story-point based, derived commitment is excluded, effort-only delivery does not become velocity, and `HoursPerSP` is calculated safely without dividing by zero.

## Fix Progress — Sprint Execution StoryPoint Metrics

- Updated `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` to resolve canonical story-point aggregates from the already reconstructed committed, added, removed, delivered, and spillover work-item sets.
- Updated `PoTool.Shared/Metrics/SprintExecutionDtos.cs` so sprint execution summaries now expose `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, `SpilloverSP`, `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate` alongside the existing count/effort diagnostics.
- Implemented canonical formulas from `docs/rules/metrics-rules.md`, including safe zero-denominator handling and explicit bug/task exclusion via canonical story-point resolution.
- Expanded `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` to verify story-point aggregation, bug/task exclusion, derived-estimate handling, and the churn / commitment / spillover / added-delivery rate formulas.

## Re-Audit Results — Post Metrics Fix

### Canonical metric definitions revalidated

- **Velocity** = delivered PBI Story Points whose **first Done transition** occurs within the sprint window. Bugs, tasks, removed items, and missing/derived estimates do not contribute.
- **Spillover** = committed scope that is **not Done at sprint end** and then moves **directly** into the next sprint.
- **Churn** = added scope + removed scope after the canonical commitment timestamp.
- **Activity** = meaningful updates during the sprint, excluding trivial metadata-only fields.
- **Capacity** = Story Point-based velocity and predictability, with **HoursPerSP = DeliveredEffort / DeliveredStoryPoints** exposed only as a diagnostic.

### Improvements implemented

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` continues to compute sprint velocity from canonical PBI Story Points resolved through `CanonicalStoryPointResolutionService`, using commitment reconstruction plus first-Done delivery semantics.
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` now uses committed and delivered Story Points for velocity and predictability, while exposing `DeliveredEffort` and `HoursPerSP` as diagnostics through `PoTool.Shared/Metrics/CapacityCalibrationDto.cs`.
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` now publishes canonical Story Point aggregates and formulas through `PoTool.Shared/Metrics/SprintExecutionDtos.cs`: `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, `SpilloverSP`, `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate`.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` and `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` preserve the same Story Point semantics in stored projections and surfaced trend DTOs, including `CompletedPbiStoryPoints`, `PlannedStoryPoints`, `SpilloverStoryPoints`, derived-estimate diagnostics, and unestimated-delivery diagnostics.
- `PoTool.Api/Services/SprintSpilloverLookup.cs` still enforces the canonical spillover rule: committed item, not Done at sprint end, first post-sprint move goes directly from Sprint N to Sprint N+1.

### DTO contract validation

- `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` exposes `DeliveredStoryPoints` and `HoursPerSP` and no longer labels effort throughput as velocity.
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs` exposes the canonical Story Point aggregates and rates required by `docs/rules/metrics-rules.md`.
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` returns Story Point trend fields alongside effort diagnostics, keeping effort and velocity semantics explicitly separated.

### Test coverage validation

The current unit suite covers the required post-fix behaviors:

- **Velocity using Story Points** — `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` and `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- **HoursPerSP capacity metric** — `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- **Churn formulas and commitment completion** — `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- **Spillover rate and canonical spillover detection** — `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` and `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
- **Added-delivery rate** — `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
- **Trend projection Story Point handling** — `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` and `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerTests.cs`

Local re-validation for this re-audit:

- `dotnet restore PoTool.sln`
- `dotnet build PoTool.sln --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~GetCapacityCalibrationQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests" -v minimal`

### Remaining violations

- **No remaining violations were found in the audited metrics layer.**

### Architectural risks

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` still represents an effort-oriented stock/flow helper rather than a canonical churn implementation. This is not a violation in the audited metrics layer, but it should remain clearly documented and isolated so it is not reused as sprint-scope truth.
- The audited handlers now rely on shared Story Point semantics across `CanonicalStoryPointResolutionService`, sprint projections, and execution summaries. Future changes must preserve that single interpretation to avoid reintroducing metric drift.

### Final verdict

**Fully compliant**

The metrics layer reviewed in this re-audit now matches the canonical domain model for velocity, spillover, churn, activity, sprint execution formulas, and capacity diagnostics.
