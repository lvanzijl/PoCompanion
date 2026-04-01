# Forecasting CDC Summary

_Generated: 2026-03-16_

Reference documents:

- `docs/architecture/forecasting-domain-model.md`
- `docs/analysis/forecasting-domain-exploration.md`
- `docs/architecture/domain-model.md`

## Scope re-audited

This re-audit covers only forecasting logic:

- epic and feature completion forecasting
- capacity calibration for planning bands and predictability
- effort-trend forecasting
- forecasting-owned domain models and policies

Excluded from this audit:

- delivery-trend historical reconstruction
- sprint analytics ownership
- portfolio progress trend reconstruction
- UI rendering and risk-label presentation

## Expected CDC ownership

Forecasting formulas are expected to live only in:

- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Models`

The Forecasting CDC owns:

- completion-date projection policies
- velocity calibration and percentile banding
- predictability calculation
- effort-trend extrapolation and confidence banding
- canonical forecasting models returned by those policies

## What moved into the CDC

The audited forecasting logic now executes inside `PoTool.Core.Domain/Domain/Forecasting`.

Confirmed CDC-owned implementations:

- completion forecast formulas in `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- delivery-forecast projection formulas in `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/DeliveryForecastProjector.cs`
- velocity percentile and predictability calibration in `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`
- effort-trend slope, volatility, and forward extrapolation in `PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs`
- canonical forecasting models in:
  - `PoTool.Core.Domain/Domain/Forecasting/Models/DeliveryForecast.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/Models/CompletionProjection.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/DeliveryForecast/ForecastProjection.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/Models/VelocityCalibration.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/Models/EffortDistributionAnalysis.cs`

Observed ownership after extraction:

- forecast calculations execute in the CDC services
- projection materialization loads canonical inputs, calls the Forecasting CDC, and persists read models
- handlers read persisted forecast projections and map them to DTOs
- statistical primitives remain shared through `PoTool.Shared/Statistics/PercentileMath.cs` and `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs`
- no forecast calculations remain in handlers, UI calculators, or API services

## What remains outside the CDC

The following stay outside the Forecasting CDC by design:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - reads persisted `ForecastProjectionEntity` rows
  - selects the requested velocity-window variant
  - maps the stored projection to `EpicCompletionForecastDto`
- `PoTool.Api/Services/ForecastProjectionMaterializationService.cs`
  - loads canonical work item snapshots, hierarchy state, and sprint history inputs
  - calls the `DeliveryForecastProjector` inside the Forecasting CDC
  - persists `ForecastProjectionEntity` rows for Epic and Feature work items
- `PoTool.Api/Services/Sync/ForecastProjectionSyncStage.cs`
  - refreshes persisted forecast projections during the sync pipeline
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
  - loads persisted sprint projections
  - calls `IVelocityCalibrationService`
  - maps the result to `CapacityCalibrationDto`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`
  - loads work items
  - calls `IEffortTrendForecastService`
  - maps the result to `EffortDistributionTrendDto`
- `PoTool.Client/Services/RoadmapAnalyticsService.cs`
  - consumes forecast DTOs
  - applies UI-facing risk labelling only
- `PoTool.Client/Components/Forecast/ForecastPanel.razor`
  - renders forecast DTOs only

These remaining responsibilities are orchestration or presentation, not ownership of forecasting formulas.

## Implementation audit

### Phase 1 — ownership verification

Confirmed:

- forecasting formulas live in `PoTool.Core.Domain/Domain/Forecasting`
- `CompletionForecastService` remains a compatibility adapter over the delivery-forecast projector
- `DeliveryForecastProjector` owns average-velocity completion forecasting and projected sprint outputs
- `VelocityCalibrationService` owns percentile velocity bands and predictability ratios
- `EffortTrendForecastService` owns slope calculation, standard deviation usage, and forward effort forecasts

### Phase 2 — orchestration boundary verification

Confirmed in `PoTool.Api`:

- materialization loads canonical inputs from EF, calls the Forecasting CDC, and persists the resulting read models
- handlers read persisted forecast projections and map stored variants to transport DTOs
- no handler re-implements percentile, completion, confidence, or extrapolation formulas inline

### Phase 3 — duplication removal verification

Confirmed:

- no forecast calculations remain in handlers
- no forecast calculations remain in UI calculators
- no forecast calculations remain in API services
- shared math stays centralized in `PercentileMath` and `StatisticsMath`

## Remaining issues

Classification: **none blocking**

Observed notes:

1. `ForecastProjectionMaterializationService` depends on persisted sprint trend projections for historical velocity input, so stale sprint projections can still make forecast projections stale until the sync pipeline refreshes both stages.
2. UI code still contains risk-threshold labeling based on returned forecast DTOs. This is presentation logic and does not duplicate CDC formulas.

No remaining issue blocks exclusive CDC ownership of forecasting formulas.

## Test validation

Existing focused tests cover the audited slice:

- CDC service semantics
  - `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs`
- handler orchestration
  - `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs`
- audit and domain reference documents
  - `PoTool.Tests.Unit/Audits/ForecastingDomainExplorationDocumentTests.cs`
  - `PoTool.Tests.Unit/Audits/ForecastingDomainModelDocumentTests.cs`
  - `PoTool.Tests.Unit/Audits/ForecastingSemanticAuditDocumentTests.cs`

Validated locally during this re-audit:

- `dotnet build PoTool.sln`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --filter "FullyQualifiedName~ForecastingDomainServicesTests|FullyQualifiedName~GetEpicCompletionForecastQueryHandlerTests|FullyQualifiedName~GetCapacityCalibrationQueryHandlerTests|FullyQualifiedName~GetEffortDistributionTrendQueryHandlerTests|FullyQualifiedName~ForecastingDomainExplorationDocumentTests|FullyQualifiedName~ForecastingDomainModelDocumentTests|FullyQualifiedName~ForecastingSemanticAuditDocumentTests" -v minimal`

These tests protect:

- CDC formula ownership
- handler orchestration boundaries
- forecasting document anchors

## Final verdict

**Forecasting CDC ready**

The forecasting formulas are now owned exclusively by the Forecasting CDC under `PoTool.Core.Domain/Domain/Forecasting`. API materialization services load canonical inputs, persist `ForecastProjectionEntity` read models, and API handlers now read those persisted projections without recomputing forecasts, while UI code only consumes the resulting DTOs for presentation.
