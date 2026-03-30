# Application Semantic Audit — Effort vs StoryPoints Usage

_Generated: 2026-03-16_

Reference documents:

- `docs/architecture/domain-model.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/sprint-rules.md`

Files analyzed:

- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
- `PoTool.Api/Controllers/MetricsController.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Client/Components/Forecast/ForecastPanel.razor`
- `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/SprintTrend.razor`
- `PoTool.Client/Services/RoadmapAnalyticsService.cs`

## Scope

This audit checks whether the application layer and UI present canonical story-point semantics where the domain model expects story points, and whether legacy effort terminology still leaks through DTO contracts or labels.

Canonical rule baseline:

- **Story points** are the planning, velocity, delivery, and forecasting unit.
- **Effort** represents implementation hours and remains valid only for effort-hour diagnostics or effort-based proxy models.

The audit therefore separates two problems:

1. **legacy effort names carrying story-point values**
2. **genuine effort-hour fields that the UI currently presents as if they were story points**

## Legacy naming detected

### Canonical story-point values behind legacy effort names

| Contract field | Actual underlying metric | Current display / usage | Expected canonical name | Backward compatibility |
| --- | --- | --- | --- | --- |
| `EpicCompletionForecastDto.TotalEffort` | total canonical story-point scope | `ForecastPanel.razor` shows `Total Effort`; `ProductRoadmaps.razor` renders `TotalEffort` as `SP` | `TotalStoryPoints` | **Must stay for backward compatibility** until the forecast contract is versioned |
| `EpicCompletionForecastDto.CompletedEffort` | completed canonical story-point scope | `ForecastPanel.razor` shows `Completed`; forecast handler maps from `forecast.CompletedScopeStoryPoints` | `DeliveredStoryPoints` | **Must stay for backward compatibility** |
| `EpicCompletionForecastDto.RemainingEffort` | remaining canonical story-point scope | `ForecastPanel.razor` shows `Remaining`; roadmap analytics compare it to estimated velocity | `RemainingStoryPoints` | **Must stay for backward compatibility** |
| `SprintForecast.ExpectedCompletedEffort` | projected completed story points by forecast sprint | forecast transport only | `ExpectedCompletedStoryPoints` | **Must stay for backward compatibility** |
| `SprintForecast.RemainingEffortAfterSprint` | projected remaining story points after forecast sprint | forecast transport only | `RemainingStoryPointsAfterSprint` | **Must stay for backward compatibility** |
| `FeatureProgressDto.TotalEffort` | total canonical story-point scope under the feature | `SprintTrend.razor` and `ProductRoadmaps.razor` treat it as scope/`SP` | `TotalStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `FeatureProgressDto.DoneEffort` | delivered canonical story-point scope under the feature | used through delivery-progress rollups and sprint trend drilldowns | `DeliveredStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `FeatureProgressDto.SprintCompletedEffort` | story points delivered during the selected sprint | `SprintTrend.razor` and `PortfolioDelivery.razor` render it as delivered `pts` | `DeliveredStoryPoints` or `SprintDeliveredStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `EpicProgressDto.TotalEffort` | total canonical story-point scope under the epic | sprint trend drilldowns and roadmap surfaces use it as scope/`SP` | `TotalStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `EpicProgressDto.DoneEffort` | delivered canonical story-point scope under the epic | delivery-progress rollups and drilldowns | `DeliveredStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `EpicProgressDto.SprintCompletedEffort` | story points delivered during the selected sprint under the epic | `SprintTrend.razor` renders it as `Delivered (pts)` | `DeliveredStoryPoints` or `SprintDeliveredStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `CompletedPbiDto.Effort` | canonical story points for the delivered PBI | `SprintTrend.razor` table shows `Effort (pts)` | `StoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `FeatureDeliveryDto.SprintCompletedEffort` | canonical story-point scope delivered for the feature | `PortfolioDelivery.razor` shows `pts` | `DeliveredStoryPoints` or `SprintDeliveredStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |
| `FeatureDeliveryDto.TotalEffort` | total canonical story-point scope of the feature | portfolio-delivery progress denominator | `TotalStoryPoints` | **Must stay for backward compatibility**; XML docs already mark it legacy |

### Contracts that still use effort naming for effort-hour metrics while nearby UI implies story points

| Contract field | Actual underlying metric | Current display / usage | Expected canonical name | Backward compatibility |
| --- | --- | --- | --- | --- |
| `PortfolioDeliverySummaryDto.TotalCompletedEffort` | **effort-hours**, because `GetPortfolioDeliveryQueryHandler` sums `CompletedPbiEffort` | `PortfolioDelivery.razor` shows `Effort Delivered` with `pts` suffix | target-state `DeliveredStoryPoints` after recalculation, or interim `CompletedEffortHours` if left effort-based | **Must stay for backward compatibility** until portfolio delivery is versioned |
| `ProductDeliveryDto.CompletedEffort` | **effort-hours** from `CompletedPbiEffort` | `PortfolioDelivery.razor` shows `@product.CompletedEffort pts` | target-state `DeliveredStoryPoints` after recalculation, or interim `CompletedEffortHours` | **Must stay for backward compatibility** until portfolio delivery is versioned |
| `ProductDeliveryDto.EffortShare` | share of product **effort-hours** | UI presents it as part of a delivered-points contribution story | `DeliverySharePercent` once numerator/denominator semantics are aligned | **Must stay for backward compatibility** until portfolio delivery is versioned |
| `SprintExecutionSummaryDto.InitialScopeEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` | `InitialScopeEffortHours` if kept, or replace the UI surface with `CommittedSP` | contract can remain, but should not be presented as story points |
| `SprintExecutionSummaryDto.AddedDuringSprintEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` | `AddedDuringSprintEffortHours` if kept, or replace the UI surface with `AddedSP` | contract can remain, but should not be presented as story points |
| `SprintExecutionSummaryDto.RemovedDuringSprintEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` | `RemovedDuringSprintEffortHours` if kept | contract can remain, but should not be presented as story points |
| `SprintExecutionSummaryDto.CompletedEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` even though `DeliveredSP` already exists | `CompletedEffortHours` if kept, or replace the UI surface with `DeliveredStoryPoints`/`DeliveredSP` | contract can remain, but should not be presented as story points |
| `SprintExecutionSummaryDto.UnfinishedEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` | `UnfinishedEffortHours` if kept, or replace the UI surface with `RemainingStoryPoints` once exposed | contract can remain, but should not be presented as story points |
| `SprintExecutionSummaryDto.SpilloverEffort` | **effort-hours** from `WorkItem.Effort` | `SprintExecution.razor` shows `pts` even though `SpilloverSP` already exists | `SpilloverEffortHours` if kept, or replace the UI surface with `SpilloverStoryPoints`/`SpilloverSP` | contract can remain, but should not be presented as story points |
| `SprintExecutionPbiDto.Effort` | **effort-hours** from `WorkItem.Effort` | completion-order table header is `Effort`; UI context implies points | `EffortHours` if kept, or `StoryPoints` only if the source changes | contract can remain, but current label is semantically misleading |
| `SprintTrendMetricsDto.TotalPlannedEffort` | projected **effort-hours** | `DeliveryTrends.razor` table shows `Planned Effort (pts)` | `PlannedEffortHours`, while story-point UI should prefer `TotalPlannedStoryPoints` | contract can remain, but current label is misleading |
| `SprintTrendMetricsDto.TotalCompletedPbiEffort` | projected **effort-hours** | `DeliveryTrends.razor` shows `Completed Effort (pts)` and `Effort Throughput Trend` | `CompletedPbiEffortHours`, while story-point UI should prefer `TotalCompletedPbiStoryPoints` | contract can remain, but current label is misleading |
| `ProductSprintMetricsDto.PlannedEffort` | projected **effort-hours** | trend drilldowns inherit `pts` framing | `PlannedEffortHours`, while story-point UI should prefer `PlannedStoryPoints` | contract can remain |
| `ProductSprintMetricsDto.CompletedPbiEffort` | projected **effort-hours** | sprint trend product summary says `Effort delivered` | `CompletedPbiEffortHours`, while story-point UI should prefer delivered story points | contract can remain |
| `ProductSprintMetricsDto.SpilloverEffort` | projected **effort-hours** | adjacent semantics use spillover story points elsewhere | `SpilloverEffortHours`, while story-point UI should prefer `SpilloverStoryPoints` | contract can remain |

### Effort-based portfolio-flow fields that should not be renamed to story points until the computation changes

| Contract field | Actual underlying metric | Current display / usage | Expected canonical name | Backward compatibility |
| --- | --- | --- | --- | --- |
| `PortfolioSprintProgressDto.TotalScopeEffort` | reconstructed effort-hours stock | `PortfolioProgressPage.razor` labels it `Total Scope (pts)` | interim `TotalScopeEffortHours`; future story-point model would be `TotalScopeStoryPoints` | keep until portfolio flow calculation is redesigned |
| `PortfolioSprintProgressDto.RemainingEffort` | effort-hours remaining stock proxy | `PortfolioProgressPage.razor` shows `Remaining Effort Ratio` and `pts` deltas | interim `RemainingEffortHours`; future story-point model would be `RemainingStoryPoints` | keep until portfolio flow calculation is redesigned |
| `PortfolioSprintProgressDto.ThroughputEffort` | effort-hours throughput/outflow | `PortfolioProgressPage.razor` shows `Throughput (pts)` and tooltip `Effort completed per sprint (pts)` | interim `ThroughputEffortHours`; future story-point model would be `DeliveredStoryPoints` | keep until portfolio flow calculation is redesigned |
| `PortfolioSprintProgressDto.AddedEffort` | effort-hours commitment proxy from `PlannedEffort`, not backlog-entry story points | `PortfolioProgressPage.razor` shows `Added` in the flow chart | interim `AddedEffortHours`; future story-point model would be `AddedStoryPoints` | keep until portfolio flow calculation is redesigned |

## Semantic mismatches

| Field / surface | Actual underlying metric | Current display name | Expected canonical name / label | Audit finding |
| --- | --- | --- | --- | --- |
| `ForecastPanel.razor` cards bound to `TotalEffort`, `CompletedEffort`, `RemainingEffort` | story points | `Total Effort`, `Completed`, `Remaining` | `Total Story Points`, `Delivered Story Points`, `Remaining Story Points` | contract is legacy but the UI still reinforces effort terminology |
| `ProductRoadmaps.razor` + `RoadmapAnalyticsService.EpicLocalAnalytics` | story points | `DeliveredEffort / TotalEffort SP`, `Remaining: RemainingEffort` | `DeliveredStoryPoints / TotalStoryPoints`, `RemainingStoryPoints` | local analytics already behave like story points, but names remain legacy |
| `DeliveryTrends.razor` secondary chart | story points in the caption, but effort-hour field names in code | `Effort Throughput Trend` | `Story Point Delivery Trend` | UI label conflicts with its own caption and the canonical meaning |
| `DeliveryTrends.razor` drill-down table | effort-hours | `Completed Effort (pts)` / `Planned Effort (pts)` | `Completed Effort (hours)` / `Planned Effort (hours)`, or switch to story-point fields | UI suffix implies story points even though the data is effort-based |
| `SprintTrend.razor` product summary card | effort-hours | `Effort delivered` | `Delivered Effort (hours)` or replace card with delivered story points | UI presents effort-hours as if they were delivery points |
| `SprintTrend.razor` epic/feature drilldowns | story points for `SprintCompletedEffort`, effort-hours for `SprintEffortDelta` | `Delivered (pts)` plus `Δ Effort (pts)` | `Delivered Story Points` plus `Δ Effort (hours)` | one surface mixes canonical story points and effort-hours correctly in structure, but not consistently in units |
| `SprintExecution.razor` summary cards | effort-hours | `Initial Scope`, `Added During Sprint`, `Completed`, `Unfinished` with `pts` values | either hours labels or story-point cards backed by `CommittedSP`, `AddedSP`, `DeliveredSP`, `SpilloverSP` | UI currently turns effort-hours into pseudo-story-points |
| `PortfolioDelivery.razor` portfolio summary | effort-hours | `Effort Delivered` + `pts` | `Delivered Effort (hours)` or recompute to `Delivered Story Points` | transport XML docs, computation, and UI label do not agree |
| `PortfolioDelivery.razor` product contribution | effort-hours | tooltip says `Share of delivered effort per product`; body shows `pts` | `Delivered Effort (hours)` or recompute to canonical story points | display unit and tooltip disagree |
| `PortfolioDelivery.razor` feature contribution | story points | tooltip says `Top features by delivered effort`; body shows `pts` | `Delivered Story Points` | the feature surface already behaves like story points but still uses effort wording |
| `PortfolioProgressPage.razor` flow charts | effort-hours proxies | `Total Scope (pts)`, `Throughput (pts)`, `Effort completed per sprint (pts)` | `Effort-hours` labels today, or story-point names only after recalculation | current UI suggests canonical points where the calculation is still effort-based |

## Canonical replacements

### Canonical names for story-point contracts

| Current field | Canonical replacement |
| --- | --- |
| `TotalEffort` | `TotalStoryPoints` |
| `CompletedEffort` / `DoneEffort` | `DeliveredStoryPoints` |
| `RemainingEffort` | `RemainingStoryPoints` |
| `SprintCompletedEffort` | `DeliveredStoryPoints` or `SprintDeliveredStoryPoints` |
| `ExpectedCompletedEffort` | `ExpectedCompletedStoryPoints` |
| `RemainingEffortAfterSprint` | `RemainingStoryPointsAfterSprint` |
| `Effort` on delivered/completed PBI story-point DTOs | `StoryPoints` |

### Canonical replacements requested for sprint-scope semantics

| Current field / surface | Canonical replacement |
| --- | --- |
| sprint commitment scope shown through effort aliases | `CommittedStoryPoints` |
| completed/delivered scope shown through effort aliases | `DeliveredStoryPoints` |
| unfinished / remaining scope shown through effort aliases | `RemainingStoryPoints` |
| added sprint scope shown through effort aliases | `AddedStoryPoints` |
| spillover scope shown through effort aliases | `SpilloverStoryPoints` |

### Fields that should stay effort-based, but become explicit

| Current field | Explicit effort-based name |
| --- | --- |
| `InitialScopeEffort` | `InitialScopeEffortHours` |
| `AddedDuringSprintEffort` | `AddedDuringSprintEffortHours` |
| `RemovedDuringSprintEffort` | `RemovedDuringSprintEffortHours` |
| `CompletedEffort` when sourced from `WorkItem.Effort` | `CompletedEffortHours` |
| `UnfinishedEffort` | `UnfinishedEffortHours` |
| `SpilloverEffort` | `SpilloverEffortHours` |
| `TotalPlannedEffort` / `PlannedEffort` | `PlannedEffortHours` |
| `CompletedPbiEffort` | `CompletedPbiEffortHours` |
| `SpilloverEffort` in trend projections | `SpilloverEffortHours` |
| `TotalScopeEffort` / `RemainingEffort` / `ThroughputEffort` / `AddedEffort` in portfolio flow | `*EffortHours` until the model becomes story-point based |

## Backward compatibility requirements

The following fields are already documented or effectively relied on as stable transport contracts and should remain available until a deliberate API versioning step is approved:

- `EpicCompletionForecastDto.TotalEffort`
- `EpicCompletionForecastDto.CompletedEffort`
- `EpicCompletionForecastDto.RemainingEffort`
- `SprintForecast.ExpectedCompletedEffort`
- `SprintForecast.RemainingEffortAfterSprint`
- `FeatureProgressDto.TotalEffort`
- `FeatureProgressDto.DoneEffort`
- `FeatureProgressDto.SprintCompletedEffort`
- `EpicProgressDto.TotalEffort`
- `EpicProgressDto.DoneEffort`
- `EpicProgressDto.SprintCompletedEffort`
- `CompletedPbiDto.Effort`
- `FeatureDeliveryDto.SprintCompletedEffort`
- `FeatureDeliveryDto.TotalEffort`
- `PortfolioDeliverySummaryDto.TotalCompletedEffort`
- `ProductDeliveryDto.CompletedEffort`

Compatibility note:

- Fields explicitly documented as legacy names should keep their current wire names until consumers move.
- Fields that are not explicitly documented as legacy, but already flow through Swagger and the generated client, should also be treated as backward-compatible transport debt.
- UI-only local records such as `RoadmapAnalyticsService.EpicLocalAnalytics` and the `RoadmapEpic` view model are not public API contracts and can be renamed earlier when UI cleanup begins.

## UI usage evaluation

### Delivery trend charts

- `DeliveryTrends.razor` is not semantically clean today.
- The chart title says **`Effort Throughput Trend`** while the caption says **`Story points delivered per sprint`**.
- The drill-down table labels **`Completed Effort (pts)`** and **`Planned Effort (pts)`**, but the underlying fields are the effort-hour projection fields `TotalCompletedPbiEffort` and `TotalPlannedEffort`.
- Result: the UI does not currently present canonical story-point semantics consistently.

### Sprint metrics / sprint execution pages

- `SprintExecution.razor` shows `InitialScopeEffort`, `AddedDuringSprintEffort`, `RemovedDuringSprintEffort`, `CompletedEffort`, and `UnfinishedEffort` with a **`pts`** suffix even though `GetSprintExecutionQueryHandler` sources them from `WorkItem.Effort`.
- The same DTO already contains canonical story-point fields: `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, and `SpilloverSP`.
- `SprintTrend.razor` mixes a product summary card labeled **`Effort delivered`** with drilldowns that use story-point-valued `SprintCompletedEffort` and correctly effort-based `SprintEffortDelta`.
- Result: the sprint surfaces mix effort-hours and story points on one page and do not make the boundary explicit.

### Portfolio flow charts

- `PortfolioProgressPage.razor` still renders `TotalScopeEffort`, `ThroughputEffort`, `RemainingEffort`, and `AddedEffort` as **`pts`**.
- `PortfolioProgressTrendDtos.cs` and `MetricsController.cs` explicitly document that this surface is still effort-based and compatibility-driven.
- Result: the portfolio-flow UI labels do **not** match the domain meaning yet. This is not only naming debt; it is also a computation-model debt.

### Additional story-point surfaces outside the requested triad

- `ForecastPanel.razor` and `ProductRoadmaps.razor` still use legacy effort labels for story-point scope.
- These surfaces are semantically closer to canonical story points than the portfolio-flow and sprint-execution pages, because their underlying values already map to story-point scope.

## Migration strategy

1. **UI-first cleanup without contract breaks**
   - Update labels and tooltips so story-point surfaces say **Story Points** and effort-hour surfaces say **Effort (hours)**.
   - Stop appending `pts` to effort-hour fields.

2. **Prefer canonical story-point fields where they already exist**
   - Delivery and sprint surfaces should prefer `TotalPlannedStoryPoints`, `TotalCompletedPbiStoryPoints`, `CommittedSP`, `AddedSP`, `DeliveredSP`, `RemainingStoryPoints`-style values, and `SpilloverSP` instead of nearby effort-hour aliases.
   - This step can improve semantics before any transport rename.

3. **Add canonical transport properties additively**
   - Introduce canonical names such as `CommittedStoryPoints`, `DeliveredStoryPoints`, `RemainingStoryPoints`, `AddedStoryPoints`, and `SpilloverStoryPoints`.
   - Keep legacy `*Effort` properties as compatibility aliases for one migration window.

4. **Version or retire ambiguous contracts**
   - Forecast, delivery-progress, and portfolio-delivery DTOs should eventually move to story-point property names.
   - Portfolio flow should not be renamed to story-point semantics until the underlying model stops using effort-hour proxies.

5. **Remove legacy names only after consumer migration**
   - When all generated-client and UI consumers have switched, remove the legacy aliases in the next contract-breaking revision.

## Final assessment

The application layer does **not** yet present one clean semantic model.

- Some contracts already carry **canonical story-point values behind legacy effort names**.
- Some pages still present **effort-hour fields as if they were story points**.
- Portfolio flow remains an **effort-based proxy model** and should not be relabeled as story points until the computation changes.

Recommended direction:

- treat forecast, delivery-trend hierarchy, and roadmap progress as **story-point-first surfaces**
- treat current portfolio flow and raw effort diagnostics as **explicit effort-hour surfaces**
- use additive canonical names and UI relabeling before removing backward-compatible legacy fields
