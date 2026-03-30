# Forecasting Semantic Audit

_Generated: 2026-03-15_

Reference documents:

- `docs/audits/forecasting_domain_exploration.md`
- `docs/domain/domain_model.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`

## Scope

This audit focuses only on forecasting behavior: future-looking formulas, calibration logic that feeds forecasting, and adjacent projection code that could be mistaken for forecasting ownership.

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`
- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`
- `PoTool.Client/Components/Forecast/ForecastPanel.razor`
- `PoTool.Shared/Statistics/PercentileMath.cs`
- `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`

Excluded from this audit:

- purely historical delivery-trend reconstruction
- portfolio progress views that do not predict future delivery
- UI rendering details that do not change forecasting formulas

## Canonical forecasting semantics

### Velocity

Canonical forecasting velocity is story-point delivery, not effort-hours.

- Domain rules define velocity as the sum of story points from PBIs whose first Done transition occurs within the sprint window.
- `GetEpicCompletionForecastQueryHandler` uses historical `CompletedStoryPoints` from sprint metrics and computes `EstimatedVelocity` as the arithmetic mean of those delivered story points.
- `GetCapacityCalibrationQueryHandler` uses persisted `CompletedPbiStoryPoints` as the delivered-story-point basis for velocity bands.

This is semantically aligned with the domain model: velocity is PBI story-point delivery, excluding bugs, tasks, removed items, and derived estimates from the velocity-grade signal.
Story-point delivery is the correct canonical forecasting unit because the domain model treats story points as the planning, velocity, delivery-analytics, and forecasting measure, while effort-hours remain diagnostic calibration data rather than the primary throughput signal.

### Throughput

The system currently uses more than one throughput summary, but the units are mostly consistent.

- **Average velocity** exists in `GetEpicCompletionForecastQueryHandler` as the arithmetic mean of sampled `CompletedStoryPoints`.
- **Median velocity** exists in `GetCapacityCalibrationQueryHandler` as P50 via `PercentileMath.LinearInterpolation`.
- **Percentile throughput bands** exist in `GetCapacityCalibrationQueryHandler` as P25/P50/P75 delivered story points.
- **Rolling velocity** does not exist as a named production formula. The epic forecast samples recent sprints, but it does not implement a separate rolling-window metric beyond its recency filter and max-sprint cap.

The code therefore has one canonical unit for story-point throughput, but not one canonical summary statistic.

### Completion projection

The only explicit completion projection formula lives in `GetEpicCompletionForecastQueryHandler`.

- Remaining scope comes from canonical hierarchy rollup and is expressed in story points, even though the DTO keeps legacy `*Effort` names.
- `SprintsRemaining` is computed as `ceil(RemainingScopeStoryPoints / EstimatedVelocity)`.
- `EstimatedCompletionDate` is derived from the last historical sprint end date plus `14 * SprintsRemaining` days.
- `ForecastByDate` builds deterministic sprint buckets using the same estimated velocity and the same 14-day cadence assumption.

This establishes a recognizable canonical completion-forecast strategy: **mean historical velocity applied to remaining story-point scope on a fixed 14-day sprint cadence**.

### Delivery probability

No single canonical probability model exists yet.

- `GetEpicCompletionForecastQueryHandler` exposes `ForecastConfidence` as low/medium/high based only on the number of historical sprints sampled.
- `GetEffortDistributionTrendQueryHandler` exposes `ConfidenceLevel` as a numeric value derived from coefficient-of-variation buckets and also creates low/high forecast bands using `2 * stdDev`.
- `GetCapacityCalibrationQueryHandler` computes percentile velocity bands and median predictability, which are probabilistic planning signals, but it does not convert them into completion-date probabilities.

The system therefore has forecasting confidence signals, but not one shared delivery-probability semantic.

### Capacity assumptions

The current forecasting behavior relies on several implicit policy assumptions:

- sprint duration is fixed at 14 days in `GetEpicCompletionForecastQueryHandler`
- historical sampling is capped by `MaxSprintsForVelocity`
- sprints older than six months are excluded from epic velocity sampling
- percentile capacity calibration uses linear interpolation through `PoTool.Shared/Statistics/PercentileMath.cs`
- hours per story point is treated as a diagnostic calibration signal, not as the canonical throughput unit

Those assumptions are internally understandable, but they are not centralized as named forecasting policies.

## Formula comparison

| Formula family | Location | Current implementation | Semantic assessment |
| --- | --- | --- | --- |
| average velocity | `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | arithmetic mean of sampled `CompletedStoryPoints` | consistent story-point unit, but only one handler uses this mean-based strategy |
| median velocity | `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | P50 from delivered story points via `PercentileMath.LinearInterpolation` | consistent story-point unit, but different summary statistic from epic completion forecasting |
| rolling velocity | not implemented as a named production formula | recent-sprint sampling exists, but no distinct rolling metric is exposed | semantic gap rather than a contradiction |
| forecast completion date | `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `ceil(remaining scope / average velocity)` plus `14 * sprintsRemaining` days | one stable implementation, but cadence is hardcoded as policy |
| probability distributions | `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`, `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | percentile velocity bands, median predictability, and effort forecast intervals | no shared probability model for story-point completion forecasts |

## Semantic conflicts and contradictions

### Stable semantics

The audit did confirm several stable semantics:

- velocity is consistently treated as delivered PBI story points rather than effort-hours
- percentile math is centralized in `PoTool.Shared/Statistics/PercentileMath.cs`
- the UI in `PoTool.Client/Components/Forecast/ForecastPanel.razor` renders returned forecast DTOs and does not recompute projections independently
- historical sprint projection ownership remains separate in `PoTool.Api/Services/SprintTrendProjectionService.cs` and `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`

### Contradiction 1 — multiple confidence models

Forecast confidence does not mean the same thing across forecasting-adjacent handlers.

- epic completion forecasting uses sprint-count thresholds
- effort distribution forecasting uses variance-derived numeric confidence plus interval bands
- capacity calibration exposes percentile and predictability signals, but no shared completion-probability model

This is the strongest semantic conflict for CDC extraction: the codebase uses the language of confidence and prediction, but not one canonical probability meaning.

### Contradiction 2 — story points vs effort naming

`PoTool.Shared/Metrics/EpicCompletionForecastDto.cs` documents that `TotalEffort`, `CompletedEffort`, and `RemainingEffort` are legacy contract names for story-point scope.

That preserves API compatibility, but it also means the main completion forecast surface still uses effort-shaped names for story-point semantics. This is a documentation-backed mismatch rather than a hidden bug, but it remains semantic debt.
The recommended reconciliation path is to keep the legacy names only at the current transport boundary, explicitly document them as story-point fields in the DTO, and rename them to story-point terminology if the forecast contract is versioned or otherwise revised in a future API cleanup.

### Contradiction 3 — derived estimates in scope vs velocity

The epic completion forecast uses canonical rollup scope, which may include derived story-point estimates, while capacity calibration explicitly subtracts derived story points from committed scope and velocity-oriented planning signals.

That split is understandable:

- derived estimates are allowed for aggregation and forecasting scope
- derived estimates are not allowed for velocity-grade delivery

However, the rule is implicit across multiple files rather than expressed as one named forecasting policy, which increases semantic drift risk.

### Contradiction 4 — hardcoded cadence policy

`GetEpicCompletionForecastQueryHandler` hardcodes a 14-day sprint cadence in both completion-date projection and sprint-bucket generation.

This is not contradicted elsewhere because no second completion-date formula exists, but it is still an embedded policy choice rather than an explicit forecasting boundary.

### Contradiction 5 — effort extrapolation is forecasting-adjacent, not the same forecast

`GetEffortDistributionTrendQueryHandler` forecasts future effort-hours using slope and standard deviation, not future delivered story points or completion dates.

That means the codebase currently has at least two future-looking models:

- story-point completion forecasting
- effort-hour trend extrapolation

Both are valid, but they should not be treated as one unlabeled forecasting semantic.

## Recommended CDC boundaries

### Should belong in a Forecasting CDC

- mean-velocity completion forecasting from `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- velocity-band and predictability calibration from `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- future-oriented effort extrapolation policies from `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`
- shared named forecasting policies for:
  - average-velocity completion projection
  - percentile capacity banding
  - confidence/probability semantics
  - cadence assumptions for date projection

### Should remain outside the Forecasting CDC

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- historical delivery-trend reconstruction handlers
- UI renderers such as `PoTool.Client/Components/Forecast/ForecastPanel.razor`
- generic shared math helpers such as `PoTool.Shared/Statistics/PercentileMath.cs` and `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`

The clean boundary is still: **DeliveryTrends reconstructs historical delivery facts; Forecasting consumes those facts to predict future delivery.**

## Readiness classification

Classification: **Partially ready**

### Why it is not fully ready

Forecasting is not yet fully ready for CDC extraction because the semantics are not unified enough in three areas:

1. confidence/probability has multiple meanings
2. cadence assumptions remain inline handler policy
3. story-point scope still appears behind legacy effort names on the main forecast contract

### Why it is more than "needs clarification"

The area is still partially ready because the core delivery semantics are already stable enough to extract around:

- velocity uses canonical delivered story points
- completion projection has one primary formula
- UI components do not re-implement forecasting formulas
- shared statistical helpers are already centralized
- historical delivery projections already sit in a separate dependency slice

## Final recommendation

The forecasting domain is **Partially ready** for CDC extraction.

Recommended next step:

1. preserve the current boundary where Forecasting is layered on top of DeliveryTrends
2. explicitly name the existing strategies:
   - average velocity
   - percentile calibration
   - effort extrapolation
3. promote sprint cadence and confidence semantics into explicit forecasting policies
4. keep legacy DTO names only at the transport boundary and document them as story-point semantics

Once those policy choices are made explicit, Forecasting can be extracted without semantic drift.
