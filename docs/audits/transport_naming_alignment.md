# Transport Naming Alignment Audit

## Scope

This audit captures the Phase 1 transport-level cleanup for analytics DTO naming at the application boundary.

The cleanup is intentionally non-breaking:

- CDC logic is unchanged
- existing `*Effort` fields remain available
- canonical story-point aliases are added only where the backing value already comes from CDC story-point outputs
- true effort-hour fields remain named as effort and are documented as such

The CDC remains the semantic authority. This audit only records transport naming alignment and migration readiness.

## Detection inventory

The following transport surfaces were reviewed for `TotalEffort`, `DoneEffort`, `RemainingEffort`, and related analytics response fields:

| File | Relevant fields | Detection summary |
| --- | --- | --- |
| `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs` | `TotalEffort`, `CompletedEffort`, `RemainingEffort`, `ExpectedCompletedEffort`, `RemainingEffortAfterSprint` | Forecast transport uses legacy effort names for canonical story-point scope and forecast projections |
| `PoTool.Shared/Metrics/SprintTrendDtos.cs` | `TotalEffort`, `DoneEffort`, `SprintCompletedEffort` | Feature and epic progress DTOs expose story-point rollups through legacy effort names |
| `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs` | `TotalEffort`, `SprintCompletedEffort`, `CompletedEffort`, `TotalCompletedEffort` | Feature delivery scope is story-point based; product and portfolio summary fields are already mapped from delivered story points but keep legacy labels |
| `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` | `TotalScopeEffort`, `RemainingEffort`, `ThroughputEffort`, `AddedEffort`, `NetFlow` | DTO already exposes canonical story-point fields and keeps these names as compatibility aliases |
| `PoTool.Shared/Metrics/SprintExecutionDtos.cs` | `InitialScopeEffort`, `AddedDuringSprintEffort`, `CompletedEffort`, `UnfinishedEffort`, `SpilloverEffort` | These remain effort-hour diagnostics; canonical story-point fields are already separate |
| `PoTool.Shared/Metrics/EffortDistributionDto.cs` | `TotalEffort` and nested effort fields | Intentional effort-hour planning surface; no story-point alias should be added |
| `PoTool.Shared/Metrics/EffortDistributionTrendDto.cs` | `TotalEffort`, `EffortBySprint`, `AverageEffort` | Intentional effort-hour trend surface; no story-point alias should be added |
| `PoTool.Shared/Metrics/EffortImbalanceDto.cs` | `TotalEffort` and related effort metrics | Intentional effort-hour diagnostics surface; no story-point alias should be added |
| `PoTool.Client/Pages/Home/SprintTrend.razor` | `DoneEffort` | UI still binds some legacy progress fields for compatibility |
| `PoTool.Client/Components/Forecast/ForecastPanel.razor` | `TotalEffort`, `RemainingEffort` | UI still binds forecast legacy transport names |

## Classification

### 1. Pure transport alias

These fields map directly from CDC story-point values with no additional handler-side calculation:

- `EpicCompletionForecastDto.TotalEffort` → `TotalStoryPoints`
- `EpicCompletionForecastDto.CompletedEffort` → `DoneStoryPoints`
- `EpicCompletionForecastDto.RemainingEffort` → `RemainingStoryPoints`
- `SprintForecast.ExpectedCompletedEffort` → `ExpectedCompletedStoryPoints`
- `SprintForecast.RemainingEffortAfterSprint` → `RemainingStoryPointsAfterSprint`
- `FeatureProgressDto.TotalEffort` → `TotalStoryPoints`
- `FeatureProgressDto.DoneEffort` → `DoneStoryPoints`
- `EpicProgressDto.TotalEffort` → `TotalStoryPoints`
- `EpicProgressDto.DoneEffort` → `DoneStoryPoints`
- `FeatureDeliveryDto.TotalEffort` → `TotalStoryPoints`

### 2. Mixed semantic usage

These responses contain both effort-hour and story-point values, so only the pure alias fields were updated:

- `PortfolioDeliverySummaryDto.TotalCompletedEffort`
- `ProductDeliveryDto.CompletedEffort`
- `FeatureDeliveryDto.SprintCompletedEffort`
- `SprintTrendMetricsDto.TotalCompletedPbiEffort`
- `ProductSprintMetricsDto.PlannedEffort`

### 3. Legacy-only or intentional effort-hour usage

These remain effort-based because the underlying value is not a CDC story-point alias:

- `SprintExecutionSummaryDto.InitialScopeEffort`
- `SprintExecutionSummaryDto.AddedDuringSprintEffort`
- `SprintExecutionSummaryDto.CompletedEffort`
- `SprintExecutionSummaryDto.UnfinishedEffort`
- `SprintExecutionSummaryDto.SpilloverEffort`
- all `EffortDistributionDto` effort fields
- all `EffortDistributionTrendDto` effort fields
- all `EffortImbalanceDto` effort fields

## Updated DTOs

Phase 1 adds canonical aliases without removing legacy fields:

| DTO | Legacy field | Added canonical field | Mapping path |
| --- | --- | --- | --- |
| `EpicCompletionForecastDto` | `TotalEffort` | `TotalStoryPoints` | `GetEpicCompletionForecastQueryHandler.cs` maps both from `forecast.TotalScopeStoryPoints` |
| `EpicCompletionForecastDto` | `CompletedEffort` | `DoneStoryPoints` | `GetEpicCompletionForecastQueryHandler.cs` maps both from `forecast.CompletedScopeStoryPoints` |
| `EpicCompletionForecastDto` | `RemainingEffort` | `RemainingStoryPoints` | `GetEpicCompletionForecastQueryHandler.cs` maps both from `forecast.RemainingScopeStoryPoints` |
| `SprintForecast` | `ExpectedCompletedEffort` | `ExpectedCompletedStoryPoints` | transport alias only; value stays identical |
| `SprintForecast` | `RemainingEffortAfterSprint` | `RemainingStoryPointsAfterSprint` | transport alias only; value stays identical |
| `FeatureProgressDto` | `TotalEffort` | `TotalStoryPoints` | `DeliveryTrendProgressRollupMapper.cs` maps both from `featureProgress.TotalScopeStoryPoints` |
| `FeatureProgressDto` | `DoneEffort` | `DoneStoryPoints` | `DeliveryTrendProgressRollupMapper.cs` maps both from `featureProgress.DeliveredStoryPoints` |
| `EpicProgressDto` | `TotalEffort` | `TotalStoryPoints` | `DeliveryTrendProgressRollupMapper.cs` maps both from `epicProgress.TotalScopeStoryPoints` |
| `EpicProgressDto` | `DoneEffort` | `DoneStoryPoints` | `DeliveryTrendProgressRollupMapper.cs` maps both from `epicProgress.DeliveredStoryPoints` |
| `FeatureDeliveryDto` | `TotalEffort` | `TotalStoryPoints` | `GetPortfolioDeliveryQueryHandler.cs` maps both from `summary.TotalScopeStoryPoints` |

No handler computes replacement values locally. Each canonical property receives the same CDC-derived value already assigned to the legacy field.

## Remaining legacy-only fields

The following transport names remain in place after Phase 1:

- forecast and progress UI bindings that still read `TotalEffort`, `RemainingEffort`, or `DoneEffort`
- `ProductDeliveryDto.CompletedEffort` and `PortfolioDeliverySummaryDto.TotalCompletedEffort`, which should be reconsidered only when the surrounding labels and consumer assumptions are migrated together
- sprint execution effort-hour diagnostics, which should stay separate from story-point delivery metrics
- effort planning and effort diagnostics DTOs, which intentionally report effort hours
- portfolio progress compatibility aliases such as `RemainingEffort` and `ThroughputEffort`, because the DTO already exposes canonical story-point fields and clients still depend on the older names

## Migration readiness assessment

Phase 1 readiness is good:

- DTO transport aliases now expose exact canonical story-point names where the old `*Effort` fields were pure aliases
- handlers and mappers continue to source values directly from CDC/domain story-point outputs
- no legacy field was removed or renamed
- existing consumers can continue using legacy names while newer consumers adopt canonical names incrementally

Follow-up work can safely focus on UI adoption and eventual client migration because the transport surface now makes the canonical semantics explicit.

Reference update:

- `docs/domain/cdc_reference.md` now explicitly states that effort-named fields can be legacy transport aliases for story points when they expose CDC-derived delivery, forecasting, or progress values
