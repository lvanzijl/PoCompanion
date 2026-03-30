# Compatibility Cleanup Phase 3

## Removed Legacy Alias Fields

The following legacy `*Effort` transport fields were removed because the transport naming alignment audit classified them as pure StoryPoint aliases only:

- `EpicCompletionForecastDto.TotalEffort`
- `EpicCompletionForecastDto.CompletedEffort`
- `EpicCompletionForecastDto.RemainingEffort`
- `SprintForecast.ExpectedCompletedEffort`
- `SprintForecast.RemainingEffortAfterSprint`
- `FeatureProgressDto.TotalEffort`
- `FeatureProgressDto.DoneEffort`
- `EpicProgressDto.TotalEffort`
- `EpicProgressDto.DoneEffort`
- `FeatureDeliveryDto.TotalEffort`

## DTOs Updated

The active transport contracts now keep only canonical StoryPoint names for the removed alias set:

- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`
  - `TotalStoryPoints`
  - `DoneStoryPoints`
  - `RemainingStoryPoints`
  - `DeliveredStoryPoints`
- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs` (`SprintForecast`)
  - `ExpectedCompletedStoryPoints`
  - `RemainingStoryPointsAfterSprint`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
  - `FeatureProgressDto.TotalStoryPoints`
  - `FeatureProgressDto.DoneStoryPoints`
  - `EpicProgressDto.TotalStoryPoints`
  - `EpicProgressDto.DoneStoryPoints`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
  - `FeatureDeliveryDto.TotalStoryPoints`

## Handlers and Mappers Updated

The following mapping seams were reduced to canonical-only StoryPoint mapping:

- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - maps only canonical forecast properties
  - removed dual assignment of forecast scope values
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
  - maps `FeatureProgressDto` and `EpicProgressDto` using only canonical StoryPoint fields
  - reverse mapping back into CDC models reads only canonical StoryPoint fields
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
  - builds `PortfolioFeatureContributionInput` from `FeatureProgressDto.TotalStoryPoints`
  - populates `FeatureDeliveryDto.TotalStoryPoints` only

## UI Consumers Updated

Remaining live consumers of the removed alias names were updated to canonical StoryPoint naming:

- `PoTool.Client/Pages/Home/SprintTrend.razor`
  - changed epic filtering from `DoneEffort` to `DoneStoryPoints`
- `PoTool.Client/swagger.json`
  - repaired the forecast and progress transport schemas for canonical StoryPoint naming
- `PoTool.Client/ApiClient/ApiClient.g.cs`
  - regenerated so `EpicCompletionForecastDto` and `SprintForecast` expose canonical StoryPoint properties only

## Fields Intentionally Kept as Effort

The following surfaces remain intentionally effort-based or mixed-semantic and were not changed in this phase:

- `SprintExecutionSummaryDto.*Effort`
- `EffortDistributionDto.*`
- `EffortDistributionTrendDto.*`
- `EffortImbalanceDto.*`
- `ProductDeliveryDto.CompletedEffort`
- `PortfolioDeliverySummaryDto.TotalCompletedEffort`
- `FeatureDeliveryDto.SprintCompletedEffort`
- `FeatureProgressDto.SprintCompletedEffort`
- `EpicProgressDto.SprintCompletedEffort`
- portfolio progress compatibility aliases such as `RemainingEffort` and `ThroughputEffort`

## Validation Results

Validated locally in `/home/runner/work/PoCompanion/PoCompanion`:

- `dotnet build PoTool.sln --no-restore -m:1`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build -m:1 --filter "FullyQualifiedName~DtoContractCleanupTests|FullyQualifiedName~DeliveryTrendProgressRollupMapperTests|FullyQualifiedName~GetEpicCompletionForecastQueryHandlerTests|FullyQualifiedName~GetPortfolioDeliveryQueryHandlerTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~RoadmapAnalyticsServiceTests|FullyQualifiedName~CompatibilityCleanupPhase3DocumentTests|FullyQualifiedName~UiSemanticLabelsTests" -v minimal`

Result:

- build succeeded
- focused tests succeeded
- forecast generated client now uses canonical StoryPoint names
- no removed alias bindings remain in the updated forecast and sprint-trend consumers

## Remaining Compatibility Debt

Remaining debt is intentionally outside the pure alias removal set:

- stale historical audit documents from earlier compatibility phases still describe the old additive migration state
- mixed-semantic effort fields in portfolio delivery still need separate review before any rename
- portfolio progress compatibility aliases remain because that DTO already exposes canonical StoryPoint fields and was not part of this breaking cleanup
