# PoTool Metrics Domain Audit

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/estimation_rules.md`
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

- `docs/domain/domain_model.md` §§ 2.3-2.4, 3.3-3.12, 5.1-5.10, 7
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/estimation_rules.md`

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
| P0 | `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | `GetCapacityCalibrationQueryHandler` | `Handle` | Capacity calibration still defines committed work as `PlannedEffort`, done work as `CompletedPbiEffort`, and "velocity" as completed PBI effort-hours. This violates `docs/domain/rules/metrics_rules.md` and `docs/domain/rules/estimation_rules.md`, which define velocity from delivered Story Points and keep Effort separate from velocity. |
| P0 | `PoTool.Shared/Metrics/CapacityCalibrationDto.cs` | `SprintCalibrationEntry`, `CapacityCalibrationDto` | record shape | The shared contract encodes the same non-canonical semantics (`Committed`, `Done`, and percentile "velocity" are all effort-based) and does not expose the domain's diagnostic `HoursPerSP = DeliveredEffort / DeliveredStoryPoints` metric. The capacity surface therefore cannot report the canonical capacity-validation metric. |
| P1 | `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | `Handle` | The handler correctly reconstructs added and removed scope from `System.IterationPath` history after the commitment timestamp, but it stops at counts and effort totals. It does not calculate `AddedSP`, `RemovedSP`, `ChurnRate`, `CommitmentCompletion`, or `AddedDeliveryRate`, so the canonical sprint-execution metrics defined in `docs/domain/rules/metrics_rules.md` are incomplete. |
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
   Surface `DeliveredEffort / DeliveredStoryPoints` as the explicit capacity-validation metric described in `docs/domain/domain_model.md` §3.12 and `docs/domain/rules/estimation_rules.md`.

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
