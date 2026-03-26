# API Read Models Validation

## DTOs and read models added or changed

- `PoTool.Shared/Metrics/DeliveryTrendAnalyticsDtos.cs`
  - added `ProductDeliveryAnalyticsDto`
  - added `ProductProgressSummaryDto`
  - added `SnapshotComparisonDto`
  - added `PlanningQualityDto`
  - added `PlanningQualitySignalDto`
  - added `InsightDto`
  - added `InsightContextDto`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
  - extended `GetSprintTrendMetricsResponse` with `ProductAnalytics`
  - extended `FeatureProgressDto` with canonical `Effort`
  - changed `ProductSprintMetricsDto.ScopeChangeEffort` to nullable for compatibility
  - changed `ProductSprintMetricsDto.CompletedFeatureCount` to nullable for compatibility

These changes keep the exposure layer transport-only:

- feature DTOs preserve `CalculatedProgress`, `Override`, `EffectiveProgress`, forecast values, `Weight`, `IsExcluded`, and now the canonical `Effort` needed by Planning Quality
- epic DTOs preserve nullable progress plus forecast, included/excluded counts, and total weight
- product analytics expose `ProductProgress`, `ProductForecastConsumed`, `ProductForecastRemaining`, `ExcludedEpicsCount`, `IncludedEpicsCount`, and `TotalWeight`
- comparison DTOs preserve nullable `ProgressDelta`, `ForecastConsumedDelta`, and `ForecastRemainingDelta`
- planning-quality DTOs preserve `PlanningQualityScore` and all `PlanningQualitySignals`
- insight DTOs preserve `Code`, `Severity`, `Message`, and structured `Context`

## Mappers and adapters updated

- `PoTool.Api/Adapters/DeliveryTrendAnalyticsExposureMapper.cs`
  - centralizes all mapping from canonical domain results to exposure DTOs
  - maps `ProductAggregationResult` to `ProductProgressSummaryDto`
  - maps `SnapshotComparisonResult` to `SnapshotComparisonDto`
  - maps `PlanningQualityResult` / `PlanningQualitySignal` to `PlanningQualityDto`
  - maps `InsightResult` / `Insight` / `InsightContext` to `InsightDto`
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
  - preserves canonical `FeatureProgress.Effort` in `FeatureProgressDto`
  - added `ToFeatureProgress(FeatureProgressDto)` so handler orchestration can reuse canonical services without re-running formulas

No handler performs ad hoc DTO shaping for the new product/comparison/planning-quality/insight payloads. The handler only:

- groups existing canonical results by product
- calls canonical services
- delegates transport mapping to the centralized adapters

## Handlers and controllers wired to canonical services

- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - still uses `SprintTrendProjectionService` for current feature and epic outputs
  - now also consumes `IProductAggregationService`
  - now also consumes `ISnapshotComparisonService`
  - now also consumes `IPlanningQualityService`
  - now also consumes `IInsightService`
  - computes current and previous canonical product snapshots from already-computed epic outputs
  - exposes `ProductAnalytics` on `GetSprintTrendMetricsResponse`
  - stops coercing missing `ScopeChangeEffort` / `CompletedFeatureCount` to zero in `ProductSprintMetricsDto`

No new endpoint was introduced. The existing delivery/progress query surface was extended in place so consumers can read the canonical pipeline outputs without conflicting contracts.

## Verification results

### Functional checks

1. Feature DTO preserves:
   - `CalculatedProgress`
   - `Override`
   - `EffectiveProgress`
   - `ForecastConsumedEffort`
   - `ForecastRemainingEffort`
   - `Weight`
   - `IsExcluded`
   - `Effort` for lossless Planning Quality transport
2. Epic DTO preserves:
   - nullable `ProgressPercent`
   - nullable `AggregatedProgress`
   - nullable forecast values
   - `ExcludedFeaturesCount`
   - `IncludedFeaturesCount`
   - `TotalWeight`
3. Product DTO preserves:
   - nullable `ProductProgress`
   - nullable `ProductForecastConsumed`
   - nullable `ProductForecastRemaining`
   - `ExcludedEpicsCount`
   - `IncludedEpicsCount`
   - `TotalWeight`
4. Comparison DTO preserves:
   - nullable deltas
   - negative deltas
   - no normalization or fallback
5. Planning Quality DTO preserves:
   - `PlanningQualityScore`
   - all `PlanningQualitySignals` with `Code`, `Severity`, `Scope`, `Message`, `EntityId`
6. Insight DTO preserves:
   - `Code`
   - `Severity`
   - `Message`
   - structured `Context`

### Structural checks

- `GetSprintTrendMetricsQueryHandler` no longer performs inline product aggregation formulas
- no new null-to-zero coercion was added in the exposure mapping layer
- the existing zero fallback for `ProductSprintMetricsDto` compatibility fields was removed
- mapper logic is centralized in `DeliveryTrendProgressRollupMapper` and `DeliveryTrendAnalyticsExposureMapper`
- exposed product/comparison/planning-quality/insight data comes from canonical services only

### Targeted validation

- `DeliveryTrendProgressRollupMapperTests`
- `DeliveryTrendAnalyticsExposureMapperTests`
- `GetSprintTrendMetricsQueryHandlerTests`
- `DtoContractCleanupTests`
- `dotnet build PoTool.sln --configuration Release --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests|FullyQualifiedName~DeliveryTrendProgressRollupMapperTests|FullyQualifiedName~DeliveryTrendAnalyticsExposureMapperTests|FullyQualifiedName~DtoContractCleanupTests" -v minimal`

All targeted checks passed after the exposure-layer changes.

## Remaining compatibility constraints

- `ProductSprintMetricsDto.ScopeChangeEffort` and `ProductSprintMetricsDto.CompletedFeatureCount` remain legacy compatibility fields for the per-sprint grid, but they are now nullable so “not populated for this sprint row” is not collapsed into zero.
- `FeatureProgressDto.ProgressPercent` and `EpicProgressDto.ProgressPercent` remain compatibility aliases for effective/aggregated progress because existing consumers already use them; the canonical nullable fields remain available alongside them.
- No UI rendering changes were introduced as part of this prompt. Existing UI code can continue consuming the older fields, while downstream consumers can use `ProductAnalytics` for the canonical product/comparison/planning-quality/insight outputs.
