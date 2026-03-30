# Planning Quality Validation

## What was added

- Added `IPlanningQualityService` and `PlanningQualityService` under `PoTool.Core.Domain/Domain/DeliveryTrends/Services`.
- Added `PlanningQualityRequest`, `PlanningQualityResult`, `PlanningQualitySignal`, and the `PlanningQualitySeverity` / `PlanningQualityScope` enums under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`.
- Registered the Planning Quality engine in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` so the canonical diagnostic service resolves from DI as a single source.
- Extended `FeatureProgress` to carry read-only feature `Effort` so Planning Quality can detect `PQ-1` from already-computed feature rollups without changing progress or forecast logic.
- Added focused tests in:
  - `PoTool.Tests.Unit/Services/PlanningQualityServiceTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - `PoTool.Tests.Unit/Audits/PlanningQualityValidationDocumentTests.cs`

## Signals implemented

- `PQ-1` — Feature missing effort (`Warning`)
- `PQ-2` — Feature missing progress basis / excluded from aggregation (`Critical`)
- `PQ-3` — Feature using override (`Info`)
- `PQ-4` — Suspicious override range between `0.01` and `1.0` (`Warning`)
- `PQ-5` — Epic contains excluded features (`Warning`)
- `PQ-6` — Product contains excluded epics (`Warning`)
- `PQ-7` — Missing forecast data at feature / epic / product scope (`Warning`)

The engine is read-only, deterministic, and consumes only already-computed `FeatureProgress`, `EpicProgress`, and `ProductAggregationResult` inputs.

## Scoring logic

- Score starts at `100`
- Each `Warning` deducts `5`
- Each `Critical` deducts `15`
- `Info` signals do not deduct points
- Final score is clamped to `0..100`

This score is diagnostic only. It is not fed back into feature progress, forecast, epic aggregation, or product aggregation.

## Verification results

- Missing feature effort emits `PQ-1`
- Missing feature weight / excluded feature emits `PQ-2`
- Manual override presence emits `PQ-3`
- Override values in the `0.01..1.0` range emit `PQ-4`
- Epic `ExcludedFeaturesCount > 0` emits `PQ-5`
- Product `ExcludedEpicsCount > 0` emits `PQ-6`
- Null forecast values at feature, epic, or product scope emit `PQ-7`
- DI registration resolves `IPlanningQualityService`
- Score clamping prevents negative results

## Examples

- A feature with `Effort = null`, `Weight = 0`, `Override = 0.5`, and null forecast values produces:
  - `PQ-1`
  - `PQ-2`
  - `PQ-3`
  - `PQ-4`
  - `PQ-7`
- An epic with `ExcludedFeaturesCount = 2` and null forecast values produces:
  - `PQ-5`
  - `PQ-7`
- A product with `ExcludedEpicsCount = 3` and null forecast values produces:
  - `PQ-6`
  - `PQ-7`
