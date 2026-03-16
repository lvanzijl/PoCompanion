# Forecasting Domain Exploration

_Generated: 2026-03-15_

Reference documents:

- `docs/domain/domain_model.md`
- `docs/audits/trend_delivery_analytics_exploration.md`
- `docs/audits/delivery_trend_analytics_cdc_summary.md`

## Summary

This exploration re-audits only the forecasting-related logic: code that predicts future delivery, calibrates future capacity, or extrapolates future effort. The current codebase has one clear completion-forecasting flow, one calibration flow that should feed forecasting, and one effort-trend extrapolation flow that predicts future effort-hours rather than future story-point delivery.

The strongest current Forecasting slice candidates are:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` (only the forward-extrapolation portion)
- shared statistical helpers already centralized in `PoTool.Shared/Statistics/PercentileMath.cs` and `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`

The current delivery-trend CDC already owns the historical projection backbone. Forecasting should depend on that slice, not absorb it.

## Search scope

The search reviewed forecasting-adjacent code under:

- `PoTool.Api/Handlers/Metrics`
- `PoTool.Api/Handlers/PullRequests`
- `PoTool.Api/Handlers/Pipelines`
- `PoTool.Api/Services`
- `PoTool.Client/Services`
- `PoTool.Client/Components`
- `PoTool.Core.Domain/Domain/DeliveryTrends`
- `PoTool.Shared/Metrics`
- `PoTool.Shared/Statistics`

Search terms included:

- forecast / forecasting
- projection / projected completion date
- velocity / capacity calibration
- predictability / confidence
- burn-up / burn-down
- statistical helper / percentile / standard deviation

Result:

- no production code implementing a dedicated burn-up or burn-down predictor was found
- only one production flow computes explicit expected completion dates
- historical sprint projections are already centralized outside the forecasting surface area

## Forecasting inventory and classification

| Location | Semantic purpose | Classification | Why it matters to Forecasting |
| --- | --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | Predict epic/feature completion from remaining canonical story-point scope and average delivered sprint velocity | **Domain forecasting logic** | Primary future-delivery predictor; owns estimated velocity, sprints remaining, completion date, confidence, and sprint-by-sprint forecast buckets |
| `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | Build P25/P50/P75 velocity bands, hours-per-story-point diagnostics, and predictability ratios from historical sprint projections | **Domain forecasting logic** | Provides calibration inputs that a Forecasting slice should own or consume directly |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | Analyze effort distribution by sprint and area path, then extrapolate next three effort buckets using slope and standard deviation | **Statistical helper logic embedded in a handler** | It predicts future effort-hours, but it is not aligned with canonical story-point delivery forecasting |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | Reconstruct historical stock-and-flow scope at each sprint end by replaying ledger changes | **Pure projection of past data (not forecasting)** | Forecasting depends on this historical view conceptually, but the handler does not predict future dates or future delivery |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | Orchestrate recomputation and persistence of historical sprint metrics projections per product and sprint | **Pure projection of past data (not forecasting)** | Delivery-trend dependency, not forecasting ownership |
| `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs` | Canonical planned/worked/completed/spillover computation from sprint history | **Pure projection of past data (not forecasting)** | Core dependency for any future forecasting slice because it produces canonical historical delivery facts |
| `PoTool.Shared/Statistics/PercentileMath.cs` | Shared linear-interpolation percentile calculation | **Statistical helper logic** | Already reused by calibration logic; should remain shared infrastructure for Forecasting |
| `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs` | Shared standard deviation and variance math | **Statistical helper logic** | Used by effort-trend extrapolation; future Forecasting work should reuse it instead of local math |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | Consume epic forecast endpoint and classify an epic as at risk using UI thresholds | **UI visualization/orchestration logic** | Displays forecast signals but should not own forecasting formulas |
| `PoTool.Client/Components/Forecast/ForecastPanel.razor` | UI surface for epic/feature forecast calculation and display | **UI visualization logic** | Renders forecast DTOs and user input only |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | Aggregate existing projections and feature progress into a delivery snapshot | **Pure projection of past data (not forecasting)** | Depends on projections but does not forecast future delivery |

## Detailed findings by location

### 1. `GetEpicCompletionForecastQueryHandler`

Observed logic:

- rolls up total, completed, and remaining canonical story-point scope through `IHierarchyRollupService`
- collects historical sprint metrics through `GetSprintMetricsQuery`
- samples up to `MaxSprintsForVelocity` recent sprints within a six-month window
- calculates `EstimatedVelocity` as the arithmetic mean of `CompletedStoryPoints`
- calculates `SprintsRemaining` as `ceil(RemainingScopeStoryPoints / EstimatedVelocity)`
- derives `EstimatedCompletionDate` by taking the last sprint end date and adding `14 * SprintsRemaining` days
- assigns `ForecastConfidence` from sprint-count thresholds:
  - low: fewer than 3 sprints
  - medium: 3-4 sprints
  - high: 5 or more sprints

Semantic purpose:

- explicit completion forecasting for a single epic or feature

Notable dependency chain:

- `GetSprintMetricsQuery`
- canonical state classification
- hierarchy rollup / canonical story-point resolution

### 2. `GetCapacityCalibrationQueryHandler`

Observed logic:

- reads persisted `SprintMetricsProjectionEntity` rows
- computes committed story points as `PlannedStoryPoints - DerivedStoryPoints`
- computes delivered story points from `CompletedPbiStoryPoints`
- computes `HoursPerSP` from delivered effort divided by delivered story points
- computes `PredictabilityRatio` as delivered divided by committed scope when commitment is non-zero
- computes velocity percentiles with `PercentileMath.LinearInterpolation`
- uses:
  - P25 / P50 / P75 for planning bands
  - P10 / P90 for outlier detection

Semantic purpose:

- velocity and predictability calibration for planning

Why it belongs near Forecasting:

- it does not predict dates by itself
- it produces the probabilistic planning bands that a Forecasting slice should own or consume

### 3. `GetEffortDistributionTrendQueryHandler`

Observed logic:

- groups work items with effort by iteration path and area path
- derives per-sprint effort trends and area-path trend slopes
- computes standard deviation with `StatisticsMath.StandardDeviation`
- computes slope with an in-handler linear regression helper
- forecasts the next three sprints as `avgEffort + slope * i`
- builds confidence intervals using `2 * stdDev`
- derives confidence level from coefficient-of-variation buckets

Semantic purpose:

- future effort distribution extrapolation rather than canonical delivery forecasting

Why it is only a partial Forecasting candidate:

- it predicts effort-hours, not canonical delivered story points
- it uses a separate confidence model from epic completion forecasting
- it is useful as a forecasting-adjacent statistical pattern, but not the core completion-forecast formula

### 4. `SprintTrendProjectionService` and `SprintDeliveryProjectionService`

Observed logic:

- reconstruct committed scope, first-Done delivery, spillover, bug activity, and progress deltas from historical events
- persist or expose canonical historical sprint facts

Classification:

- not forecasting
- foundational historical projection / delivery-trend logic

Why it matters:

- Forecasting should consume these outputs as stable inputs
- moving this logic into Forecasting would blur the already-clean boundary between past-fact reconstruction and future prediction

### 5. Client-only consumers

`RoadmapAnalyticsService` and `ForecastPanel.razor` only:

- call the forecast endpoint
- map DTOs to risk signals or UI presentation
- do not duplicate forecasting formulas locally

This is the correct UI boundary.

## Slice dependency map

Forecasting currently depends on the following slices or helper families:

### SprintAnalytics

- `GetSprintMetricsQuery` is used by `GetEpicCompletionForecastQueryHandler`
- sprint windows, first-Done delivery, and committed-scope semantics originate here

### DeliveryTrends

- `SprintTrendProjectionService`
- `SprintDeliveryProjectionService`
- persisted `SprintMetricsProjectionEntity`
- story-point and spillover facts used by `GetCapacityCalibrationQueryHandler`

This is the strongest direct dependency. Forecasting should treat DeliveryTrends as the canonical source of historical delivery facts.

### EffortDiagnostics

- indirect dependency only
- `HoursPerSP` and effort-based variation signals are diagnostic calibration signals, not core forecast outputs
- `GetEffortDistributionTrendQueryHandler` reuses statistical helpers similar to EffortDiagnostics patterns

This should remain an adjacent dependency, not a required ownership merge.

### Statistical helpers

- `PoTool.Shared/Statistics/PercentileMath.cs`
- `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`

These helpers are already shared and should stay shared rather than being copied into a Forecasting slice.

## Duplicated logic

### Resolved duplication

- percentile calculation is already consolidated in `PoTool.Shared/Statistics/PercentileMath.cs`
- standard deviation is already consolidated in `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`
- canonical sprint delivery reconstruction is already centralized in `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`

### Remaining duplication or near-duplication

1. **Multiple confidence models**
   - `GetEpicCompletionForecastQueryHandler` uses sprint-count thresholds
   - `GetEffortDistributionTrendQueryHandler` uses coefficient-of-variation buckets
   - both are reasonable locally, but they are semantically different confidence schemes for future prediction

2. **Multiple forecasting heuristics**
   - epic completion forecast uses mean historical velocity and a fixed 14-day sprint cadence
   - effort distribution forecast uses linear regression on effort-hours plus `2 * stdDev` bands
   - these are different by design, but a dedicated Forecasting slice should own and document both as separate named strategies

3. **Local linear regression helper remains in `GetEffortDistributionTrendQueryHandler`**
   - slope calculation is still local instead of shared
   - this is acceptable today because only one production consumer uses it, but it becomes a likely extraction target if more forecasting code starts using regression-based prediction

## Contradictions and semantic drift

### Confirmed contradictions

No direct contradictions were found in the current completion-forecasting formulas around velocity semantics:

- epic forecasting consumes canonical `CompletedStoryPoints`
- capacity calibration consumes canonical `CompletedPbiStoryPoints`
- both exclude derived delivery from velocity-grade story-point delivery

### Semantic tensions to resolve in a Forecasting slice

1. **Forecasting vs calibration split**
   - `GetEpicCompletionForecastQueryHandler` forecasts completion dates
   - `GetCapacityCalibrationQueryHandler` produces percentile capacity bands
   - the forecaster does not currently consume the calibration bands, so the two planning signals remain adjacent rather than unified

2. **Story-point forecasting vs effort forecasting**
   - epic forecasting is canonical story-point based
   - effort distribution forecasting is effort-hour based
   - both are future-looking, but they answer different planning questions and should not share a single unlabeled “forecast” concept

3. **Expected completion cadence assumption**
   - epic forecasting hardcodes a 14-day sprint length when projecting completion dates
   - calibration logic does not carry sprint-duration modeling at all
   - this is not a bug, but it is a policy choice that should live in the Forecasting slice rather than remain an inline handler detail

4. **No burn-up / burn-down prediction implementation**
   - production code contains no dedicated burn-up or burn-down predictor
   - documentation mentions a long-horizon burndown-style chart in `docs/NAVIGATION_MAP.md`
   - if that feature is implemented later, it should be added to Forecasting rather than DeliveryTrends

## Recommended boundaries for a Forecasting slice

### Should belong in a dedicated Forecasting CDC slice

- epic or feature completion forecasting formulas and DTO shaping
- velocity and predictability calibration formulas
- future-oriented statistical projection helpers used by forecasting handlers
- named forecasting policies such as:
  - average-velocity completion forecast
  - percentile capacity banding
  - effort-trend extrapolation
  - confidence scoring policies for future predictions

Concrete starting ownership candidates:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- forward-extrapolation portions of `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`

### Should remain outside the Forecasting slice

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- client renderers such as `PoTool.Client/Components/Forecast/ForecastPanel.razor`

These areas either reconstruct historical facts or render outputs and should remain dependencies, not ownership targets.

## Recommended extraction order

1. **Start with completion forecasting + calibration**
   - extract shared future-prediction policies behind domain interfaces
   - keep API handlers as orchestration only

2. **Document forecasting strategies explicitly**
   - mean-velocity date forecast
   - percentile capacity calibration
   - effort-trend extrapolation
   - confidence models

3. **Extract statistical helpers only when reused**
   - keep `PercentileMath` and `StatisticsMath` shared
   - extract linear regression only if another forecasting consumer appears

4. **Do not pull historical projection services into Forecasting**
   - DeliveryTrends should remain the source of past-fact reconstruction
   - Forecasting should consume those facts to predict the future

## Test anchors

Existing focused tests that protect the audited behavior:

- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Services/SprintDeliveryProjectionServiceTests.cs`
- `PoTool.Tests.Unit/Audits/TrendDeliveryAnalyticsExplorationDocumentTests.cs`
- `PoTool.Tests.Unit/Audits/DeliveryTrendAnalyticsCdcSummaryDocumentTests.cs`

## Final recommendation

**Forecasting should be a separate CDC slice layered on top of DeliveryTrends, not a renaming of the existing delivery-trend CDC.**

The clean boundary is:

- **DeliveryTrends** owns historical sprint facts and progress rollups
- **Forecasting** owns future prediction policies that consume those facts
- **Statistical helpers** remain shared
- **UI** only renders or labels forecast outputs

That boundary captures the true forecasting logic already present, avoids absorbing non-forecast historical projection code, and leaves room for future burn-up/burn-down prediction without diluting the delivery-trend CDC.

## Extraction outcome

The forecasting extraction now lives under `PoTool.Core.Domain/Domain/Forecasting` with dedicated `Models` and `Services` folders.

Extracted domain models:

- `PoTool.Core.Domain/Domain/Forecasting/Models/DeliveryForecast.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Models/CompletionProjection.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Models/VelocityCalibration.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Models/EffortDistributionAnalysis.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Models/ForecastingModelValidation.cs`

Extracted domain services:

- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs`

Behavior-preserving handler orchestration remains in:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`

The handlers now:

- load repository and EF data
- scope the requested dataset
- delegate forecasting formulas to the Forecasting CDC services
- map domain outputs back to the existing shared DTO contracts

Compatibility notes:

- `EpicCompletionForecastDto` still keeps the legacy `*Effort` property names for API compatibility
- capacity calibration still exposes the same `CapacityCalibrationDto` fields and percentile semantics
- effort distribution trend still exposes the same `EffortDistributionTrendDto` shape and confidence buckets

Focused verification added for the extracted slice:

- `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs`
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
