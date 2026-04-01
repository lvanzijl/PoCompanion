# Forecast Feasibility Analysis

Date: 2026-04-01

## Scope

This report describes the current implemented capability for forecasting delivery duration from existing work-item, estimation, sprint, and velocity-related code. It is limited to behavior that is explicitly present in runtime code, DTOs, persistence, and tests.

## 1. Summary

The current system has an implemented **epic/feature completion forecast** capability.

That capability is based on:

- canonical **story-point scope** rollup for an epic or feature
- historical **completed story points** from sprint metrics
- average delivered story points used as forecast velocity
- conversion of remaining story points into:
  - `SprintsRemaining`
  - `EstimatedCompletionDate`

The current system also contains effort aggregation and effort-trend analysis, but the implemented completion forecast path is **story-point based**, not effort-duration based.

Based on the current implementation, forecasting delivery duration is **partially possible**:

- **possible** for epic/feature forecast in story-point terms when scope and usable historical sprint delivery exist
- **not fully possible** as a general, reliable duration forecast for all cases because the implementation explicitly models missing estimates, derived estimates, approximation, and cases with no usable historical velocity

## 2. Where velocity is calculated or stored

### 2.1 Velocity used by epic completion forecasting

The implemented forecast path calculates velocity from historical sprint delivery.

Files:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs:18-248`
- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs:13-126`
- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs:22-47`

Current flow:

1. `GetEpicCompletionForecastQueryHandler` loads the target epic/feature and its work-item scope.
2. It loads recent sprint metrics through `GetSprintMetricsQuery`.
3. It converts each sprint metric into `HistoricalVelocitySample(..., CompletedStoryPoints)`.
4. `CompletionForecastService` computes:
   - `EstimatedVelocity = historicalSprints.Average(sprint => sprint.CompletedStoryPoints)`
   - `SprintsRemaining = ceil(RemainingScopeStoryPoints / EstimatedVelocity)` when velocity is greater than zero
   - `EstimatedCompletionDate` from the last sprint end date plus `14 * SprintsRemaining` days

Explicit implementation:

- `GetEpicCompletionForecastQueryHandler` says velocity is derived from `CompletedStoryPoints` and capped by `MaxSprintsForVelocity`
- `CompletionForecastService.Forecast` uses only completed story points from historical sprints to calculate `EstimatedVelocity`

### 2.2 Where historical velocity comes from

Historical velocity comes from `SprintMetricsDto.CompletedStoryPoints`.

File:

- `PoTool.Shared/Metrics/SprintMetricsDto.cs:1-21`

Fields:

| Field | Type | Notes |
| --- | --- | --- |
| `CompletedStoryPoints` | `int` | Delivered story points in the sprint |
| `PlannedStoryPoints` | `int` | Committed story-point scope |

`GetSprintMetricsQueryHandler` constructs those values from sprint fact services:

- `completedStoryPoints = round(sprintFact.DeliveredStoryPoints)`
- `plannedStoryPoints = round(sprintFact.CommittedStoryPoints)`

File:

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs:176-204`

### 2.3 Persisted sprint metrics/projections related to velocity

There is also persisted sprint projection storage that keeps sprint delivery facts at product/sprint level.

File:

- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs:9-173`

Relevant persisted fields:

| Field | Type | Notes |
| --- | --- | --- |
| `CompletedPbiStoryPoints` | `double` | Delivered story points for PBIs whose first Done transition occurred during the sprint |
| `CompletedPbiEffort` | `int` | Effort of completed PBIs |
| `PlannedStoryPoints` | `double` | Planned scope story points |
| `PlannedEffort` | `int` | Planned effort |
| `MissingStoryPointCount` | `int` | Missing estimate count |
| `MissingEffortCount` | `int` | Missing effort count |
| `DerivedStoryPointCount` | `int` | Derived estimate count |
| `DerivedStoryPoints` | `double` | Derived estimate amount |
| `UnestimatedDeliveryCount` | `int` | Delivered PBIs without authoritative story-point estimate |
| `IsApproximate` | `bool` | Approximation flag |

### 2.4 Other stored "velocity" metric

There is a separate cached metric called `Velocity7d`, but it is not the same as the epic completion forecast velocity.

Files:

- `PoTool.Api/Persistence/Entities/CachedMetricsEntity.cs:8-50`
- `PoTool.Api/Services/Sync/MetricsComputeStage.cs:112-123`

Current implementation:

- `MetricsComputeStage` sums `Effort` of recently closed items
- stores it under metric name `Velocity7d`
- stores unit `"points"`

That cached metric is not used by `GetEpicCompletionForecastQueryHandler`, which instead uses historical sprint `CompletedStoryPoints`.

## 3. Whether story points can be aggregated per epic

Yes. Story points can be aggregated per epic in the current code.

Primary implementation:

- `PoTool.Core.Domain/Domain/Hierarchy/HierarchyRollupService.cs:7-166`

The hierarchy rollup service explicitly supports:

- Feature rollup from child PBIs
- Epic rollup from child Features
- direct PBI rollup under an Epic when present
- parent fallback when no descendant scope exists

Current rollup result:

- `HierarchyScopeRollup(Total, Completed)`

This rollup is used directly in epic completion forecasting:

- `GetEpicCompletionForecastQueryHandler.cs:105-130`

The forecast handler calls:

- `scope = _hierarchyRollupService.RollupCanonicalScope(...)`
- `totalScopeStoryPoints = scope.Total`
- `completedScopeStoryPoints = scope.Completed`

### 3.1 Story-point resolution rules used in epic rollup

Canonical story-point resolution is implemented in:

- `PoTool.Core.Domain/Domain/Estimation/CanonicalStoryPointResolutionService.cs:6-167`

Resolution order:

1. `StoryPoints`
2. `BusinessValue` fallback
3. sibling-average `Derived`
4. `Missing`

The code explicitly classifies estimate sources as:

- `Real`
- `Fallback`
- `Derived`
- `Missing`

This means epic story-point aggregation exists even when the original work items are not perfectly estimated, but the code also explicitly records when that aggregation had to rely on fallback or derived estimates.

## 4. Whether effort can be aggregated per epic

Yes. Effort can be aggregated per epic in the current code, but not through the same duration forecast path as story points.

### 4.1 Raw effort storage

Raw effort is stored on work items.

Files:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-76`
- `PoTool.Shared/WorkItems/WorkItemDto.cs:7-30`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs:9-52`

Relevant fields:

| Location | Field | Type |
| --- | --- | --- |
| `WorkItemEntity` | `Effort` | `int?` |
| `WorkItemDto` | `Effort` | `int?` |
| `CanonicalWorkItem` | `Effort` | `double?` |

### 4.2 Feature and epic effort aggregation

Feature forecast effort is computed in:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs:5-45`

Current behavior:

- if `Effort` or `EffectiveProgress` is missing, forecast consumed/remaining effort is `null`
- otherwise:
  - `ForecastConsumedEffort = Effort * EffectiveProgress`
  - `ForecastRemainingEffort = Effort - ForecastConsumedEffort`

Epic effort aggregation is implemented in:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs:6-65`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs:6-64`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs:6-122`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs:6-136`

Current epic effort-related outputs include:

- weighted epic progress based on feature `TotalEffort`
- `ForecastConsumedEffort`
- `ForecastRemainingEffort`
- `TotalWeight`

So the system can aggregate effort-related epic information, but the explicit duration forecast endpoint does not use those effort values to derive sprint count or completion date.

## 5. If duration can be derived

### 5.1 Duration in sprints

Yes, in current code, duration can be derived in **sprints remaining** for an epic or feature.

Implemented output:

- `EpicCompletionForecastDto.SprintsRemaining`

Files:

- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs:22-47`
- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs:25-41`

Formula currently implemented:

- `remaining = totalScopeStoryPoints - completedScopeStoryPoints`
- `estimatedVelocity = average historical completed story points`
- `sprintsRemaining = ceil(remaining / estimatedVelocity)` when velocity is greater than zero

### 5.2 Duration in time

Yes, in current code, duration can also be derived as an estimated completion date.

Implemented output:

- `EpicCompletionForecastDto.EstimatedCompletionDate`

Current implementation:

- uses the latest historical sprint end date with a non-null end date
- adds `SprintCadenceDays * sprintsRemaining`
- `SprintCadenceDays` is hard-coded to `14`

Files:

- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs:15-16`, `44-59`

### 5.3 Scope of the current duration capability

The explicit implemented duration forecast is scoped to:

- an individual epic or feature requested through:
  - `GET api/Metrics/epic-forecast/{epicId}`
  - `PoTool.Api/Controllers/MetricsController.cs:509-544`

The current code does not expose a general-purpose duration forecast API based on effort alone.

## 6. Blocking gaps explicitly visible in the model

The code explicitly models several conditions that can block or weaken forecast quality.

### 6.1 Missing estimates

Story-point and effort gaps are explicitly tracked in sprint projection models.

Files:

- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs:120-149`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjection.cs:27-35`, `122-132`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintTrendMetrics.cs:44-87`

Explicit fields:

- `MissingEffortCount`
- `MissingStoryPointCount`
- `UnestimatedDeliveryCount`

These indicate that the current system recognizes cases where required estimates are absent.

### 6.2 Inconsistent estimation / approximation

The current implementation explicitly records when story-point data was derived or approximate.

Files:

- `PoTool.Core.Domain/Domain/Estimation/CanonicalStoryPointResolutionService.cs:26-35`, `137-166`
- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs:126-149`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjection.cs:31-35`, `124-132`

Explicit signals:

- `DerivedStoryPointCount`
- `DerivedStoryPoints`
- `IsApproximate`
- estimate source classification of `Derived`
- estimate source classification of `Fallback`

This means the system can still produce scope and velocity inputs when estimates are imperfect, but it also explicitly marks that the result may be approximate.

### 6.3 Lack of usable velocity

The forecast service explicitly handles the case where no historical sprint velocity is available.

File:

- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs:25-41`, `44-50`, `76-84`

Current behavior when historical sprint samples are absent or unusable:

- `EstimatedVelocity = 0`
- `SprintsRemaining = 0`
- `EstimatedCompletionDate = null`
- `Projections = []`

So the current code does not infer duration without usable historical sprint delivery.

### 6.4 Missing dated sprint windows

Sprint metrics only exist when a dated sprint window can be resolved.

File:

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs:78-88`

If the matched sprint has no start/end dates:

- the handler returns `null`

This is an explicit dependency for historical velocity sampling.

## 7. Conclusion

## Current-state capability conclusion

Forecasting delivery duration is **partially possible**.

### What is currently possible

- The system can forecast completion for an individual **epic or feature**
- It can derive:
  - total scope story points
  - completed story points
  - remaining story points
  - average historical velocity
  - `SprintsRemaining`
  - `EstimatedCompletionDate`
- It exposes this through:
  - `EpicCompletionForecastDto`
  - `GET api/Metrics/epic-forecast/{epicId}`

### Why the result is only partial

The implemented forecast depends on conditions that are explicitly modeled as gaps:

- missing story-point estimates
- missing effort values
- derived/fallback estimates
- approximation flags
- missing dated sprint windows
- no usable historical sprint velocity

### What the current code does not show

- a duration forecast derived from **effort-hours** instead of story-point velocity
- a forecast that works independently of historical sprint delivery
- a guarantee that every epic has authoritative story-point inputs or enough recent sprint history for a usable forecast

Therefore, based on the current implementation, delivery duration forecasting is **partially possible**, with the implemented path centered on **story-point scope plus historical sprint velocity**.
