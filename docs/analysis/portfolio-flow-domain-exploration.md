# PortfolioFlow Domain Exploration

_Generated: 2026-03-16_

Reference documents:

- `docs/architecture/domain-model.md`
- `docs/analysis/trend-delivery-analytics-exploration.md`
- `docs/analysis/delivery-trend-analytics-cdc-summary.md`
- `docs/analysis/forecasting-cdc-summary.md`

## Summary

A single coherent **PortfolioFlow** CDC slice does **not** currently exist.

The repository has two portfolio-facing families, but they do not share one canonical semantic core:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` owns a portfolio **stock/flow reconstruction** model based on effort history replay, `SprintMetricsProjection.PlannedEffort`, and trajectory classification.
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` owns a **composition and ranking view** over already-derived delivery outputs, mainly `SprintMetricsProjectionEntity` rows plus feature progress returned by `PoTool.Api/Services/SprintTrendProjectionService.cs`.

That means the portfolio surface is split between:

1. an effort-based historical reconstruction family, and
2. a presentation-oriented cross-product delivery aggregation family.

The strongest conclusion from the current code is therefore:

- **there is no monolithic PortfolioFlow slice ready for extraction**
- **there is one promising sub-family**: portfolio stock/flow reconstruction
- **the rest is mostly application aggregation built on top of DeliveryTrends**

## Inventory

| File | Class | Method(s) | Concept | Inputs | Outputs |
| --- | --- | --- | --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | `Handle` | Portfolio progress trend orchestration across selected sprints/products | `ProductOwnerId`, `SprintIds`, optional `ProductIds`, `Products`, `Sprints`, `ResolvedWorkItems`, `WorkItems`, `ActivityEventLedgerEntries`, `SprintMetricsProjectionEntity` | `PortfolioProgressTrendDto` |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | `ComputeHistoricalScopeEffort` | Portfolio flow reconstruction via historical effort replay at sprint end | sprint end timestamp, resolved PBI/Bug IDs, current work-item snapshots, effort/state change events | reconstructed total scope effort per sprint |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | `ComputeSummary` | Portfolio trajectory classification from cumulative net flow and scope deltas | `PortfolioSprintProgressDto` sequence | `PortfolioProgressSummaryDto` |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `GetPortfolioDeliveryQueryHandler` | `Handle` | Cross-product delivery distribution snapshot and contributor ranking | `ProductOwnerId`, `SprintIds`, owner products, `SprintMetricsProjectionEntity`, product names, feature progress from `SprintTrendProjectionService.ComputeFeatureProgressAsync` | `PortfolioDeliveryDto` |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeFeatureProgressAsync`, `ComputeFeatureProgress` | Feature completion summaries used by portfolio delivery ranking | product owner, optional sprint activity window, resolved hierarchy, work items, state lookup, DeliveryTrends rollup service | `FeatureProgressDto[]` |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeEpicProgressAsync`, `ComputeEpicProgress` | Epic completion summaries and product rollups used by sprint trend views | product owner, feature progress, resolved hierarchy, work items, DeliveryTrends rollup service | `EpicProgressDto[]` |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | `Handle` | Product-level completion aggregation adjacent to portfolio views | sprint projections, feature progress, epic progress, `ProductDeliveryProgressSummary` | `GetSprintTrendMetricsResponse` with per-product summaries |
| `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressSummaryCalculator.cs` | `DeliveryProgressSummaryCalculator` | `ComputeProductSummaries` | Canonical product delivery summary aggregation from epic outputs | `EpicProgress[]` | `IReadOnlyDictionary<int, ProductDeliveryProgressSummary>` |
| `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs` | `DeliveryProgressRollupService` | `ComputeFeatureProgress`, `ComputeEpicProgress`, `ComputeProgressionDelta` | Canonical feature/epic completion rollups and progression delta that feed portfolio-adjacent views | prepared delivery-trend requests, hierarchy, state lookup, story-point resolution | `FeatureProgress[]`, `EpicProgress[]`, `ProgressionDelta` |
| `PoTool.Core/Metrics/Queries/GetPortfolioProgressTrendQuery.cs` | `GetPortfolioProgressTrendQuery` | record contract | Portfolio progress API query contract | `ProductOwnerId`, `SprintIds`, optional `ProductIds` | `PortfolioProgressTrendDto` |
| `PoTool.Core/Metrics/Queries/GetPortfolioDeliveryQuery.cs` | `GetPortfolioDeliveryQuery` | record contract | Portfolio delivery API query contract | `ProductOwnerId`, `SprintIds` | `PortfolioDeliveryDto` |
| `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` | DTOs | `PortfolioTrajectory`, `PortfolioSprintProgressDto`, `PortfolioProgressSummaryDto`, `PortfolioProgressTrendDto` | Transport model for portfolio stock/flow trend, including explicit limitation notes | reconstructed scope, remaining effort, throughput, added-effort proxy, net flow | API/client DTOs |
| `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs` | DTOs | `PortfolioDeliveryDto`, `PortfolioDeliverySummaryDto`, `ProductDeliveryDto`, `FeatureDeliveryDto` | Transport model for portfolio delivery distribution and ranking | aggregated completed PBIs/story points, bug counts, feature ranking | API/client DTOs |
| `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` | Razor page | `BuildChartData`, `RebuildFlowSeries`, `ComputeRemainingRatio` | Portfolio progress visualization and terminology projection | `PortfolioProgressTrendDto` | summary cards, flow/stock charts, trend labels |
| `PoTool.Client/Pages/Home/PortfolioDelivery.razor` | Razor page | page rendering and `LoadDeliveryDataAsync` | Portfolio delivery composition view | `PortfolioDeliveryDto` | summary cards, product contribution bars, feature contribution ranking, bug distribution |
| `PoTool.Api/Controllers/MetricsController.cs` | `MetricsController` | `GetPortfolioProgressTrend`, `GetPortfolioDelivery` | API endpoints exposing portfolio metrics | query params from client | HTTP responses for portfolio DTOs |
| `PoTool.Tests.Unit/Handlers/GetPortfolioProgressTrendQueryHandlerTests.cs` | `GetPortfolioProgressTrendQueryHandlerTests` | `ComputeHistoricalScopeEffort_*` tests | Existing evidence that only the historical reconstruction helper has isolated semantics today | synthetic PBI snapshots and scope events | helper-level behavior verification |

## Domain Families

### 1. Portfolio progress / stock-flow reconstruction

Primary ownership today:

- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`

What it does:

- reconstructs total scope effort at each sprint end using activity ledger history
- derives remaining effort from reconstructed scope minus cumulative throughput
- uses `CompletedPbiEffort` as throughput and `PlannedEffort` as an added-scope proxy
- classifies the range as `Contracting`, `Stable`, or `Expanding`

This is the closest thing to a PortfolioFlow domain family, but it is still implemented directly inside an API handler and is explicitly documented as an approximation rather than canonical churn.

### 2. Portfolio delivery distribution

Primary ownership today:

- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`

What it does:

- aggregates persisted sprint projections across products
- calculates product delivery shares
- ranks top features by delivered scope
- exposes bug-activity totals as portfolio summary context

This family is not time-series flow logic. It is a composition view over already-derived delivery metrics.

### 3. Cross-product completion aggregation

Primary ownership today:

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressSummaryCalculator.cs`

What it does:

- computes feature and epic completion rollups
- derives product summaries from epic outputs
- feeds both sprint trend drilldowns and portfolio-facing feature ranking

This family is semantically stronger than the portfolio handlers themselves, but it belongs to **DeliveryTrends**, not to a separate portfolio slice.

### 4. Portfolio forecast aggregation

Search result:

- no dedicated portfolio forecast aggregation handler or domain service was found
- forecasting is implemented for epic/feature completion and calibration in:
  - `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
  - `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`

This means “portfolio forecast aggregation” is currently absent as a coherent production slice.

### 5. Product contribution ranking

Primary ownership today:

- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`

What it does:

- ranks products by delivered scope share
- ranks top features by delivered scope share
- presents bug distribution by product

This is presentation-oriented aggregation rather than standalone domain logic.

## Dependencies

| Portfolio family | Main dependencies | Notes |
| --- | --- | --- |
| Portfolio progress / stock-flow reconstruction | `SprintMetricsProjectionEntity`, `ActivityEventLedgerEntries`, current work-item snapshots, state history | Depends on sprint analytics persistence and ledger history, but **not** on a portfolio CDC service |
| Portfolio delivery distribution | `SprintMetricsProjectionEntity`, `SprintTrendProjectionService.ComputeFeatureProgressAsync` | Uses persisted sprint outputs plus DeliveryTrends-backed feature progress |
| Cross-product completion aggregation | `DeliveryProgressRollupService`, `DeliveryProgressSummaryCalculator`, `ICanonicalStoryPointResolutionService`, `IHierarchyRollupService` | This is clearly downstream of **DeliveryTrends** |
| Portfolio forecast aggregation | `CompletionForecastService`, `VelocityCalibrationService`, `EffortTrendForecastService` | No portfolio aggregator found; forecasting remains epic/feature-scoped |

Slice dependency assessment against the requested map:

- **SprintAnalytics**  
  Yes. Both portfolio handlers depend on `SprintMetricsProjectionEntity` produced by `SprintTrendProjectionService`.

- **DeliveryTrends**  
  Yes, but mainly through upstream completion rollups and feature progress. `GetPortfolioDeliveryQueryHandler` is the clearest consumer.

- **Forecasting**  
  No direct portfolio consumer was found. Forecasting exists nearby but stops at epic/feature/calibration scope.

- **BacklogQuality**  
  No direct portfolio dependency found in the inspected portfolio logic.

- **EffortDiagnostics**  
  No direct portfolio dependency found. The closest overlap is shared effort vocabulary, not shared services.

- **Shared statistics helpers**  
  No direct use was found in the portfolio handlers or portfolio DTO mapping. Shared math is used upstream in Forecasting and other analytics, not in the portfolio-specific code itself.

## Semantic Overlaps

| Term | Meanings currently in use |
| --- | --- |
| `delivery` | In DeliveryTrends it means canonical first-Done delivery of PBIs; in `GetPortfolioDeliveryQueryHandler` it means aggregated delivered scope plus contributor ranking; in client pages it also includes bug totals in the same summary surface |
| `progress` | `ProgressPercent` from completion rollups, `ProgressionDelta` from sprint activity, and `PercentDone` in portfolio progress are different measures |
| `flow` | Explicit only in portfolio progress trend (`ThroughputEffort`, `AddedEffort`, `NetFlow`); no shared portfolio flow model exists elsewhere |
| `remaining scope` / `remaining effort` | `PortfolioProgressTrendDtos.cs` uses remaining effort as an effort-based stock proxy, while the controller comment says the legacy name is kept for compatibility |
| `throughput` | In portfolio progress it is completed effort per sprint; in canonical sprint metrics the closer stable concept is delivered story points |
| `portfolio summary` | In `PortfolioDelivery.razor` it mixes completed PBIs, delivered scope, average progress, and bug counts; in `RoadmapReportingService` the phrase `Portfolio Summary` means only counts of products and roadmap epics |

## Contradictions

1. **Portfolio progress uses an effort-based stock/flow model while DeliveryTrends and Forecasting are increasingly story-point-based.**  
   `GetPortfolioProgressTrendQueryHandler.cs` reconstructs effort and uses `CompletedPbiEffort` / `PlannedEffort`, while DeliveryTrends and Forecasting treat canonical delivery scope as story points.

2. **`AddedEffort` is not true backlog inflow.**  
   Both `GetPortfolioProgressTrendQueryHandler.cs` and `PortfolioProgressTrendDtos.cs` explicitly document that `AddedEffort` is only a proxy from `SprintMetricsProjection.PlannedEffort`.

3. **`CompletedEffort` in portfolio delivery DTOs is semantically story-point delivery, despite the retained effort name.**  
   `PortfolioDeliveryDtos.cs` says `TotalCompletedEffort` and `ProductDeliveryDto.CompletedEffort` are total story points delivered, while the property names still imply effort.

4. **Client terminology amplifies the mixed units.**  
   `PortfolioProgressPage.razor` renders “pts” for effort-based stock/flow values, while `PortfolioDelivery.razor` also renders “pts” for story-point-derived delivery values.

5. **Portfolio delivery is called a portfolio view, but much of its semantic content is already owned upstream by DeliveryTrends.**  
   The handler mainly aggregates `SprintMetricsProjectionEntity` and `FeatureProgressDto` instead of defining new portfolio rules.

6. **No portfolio forecast family exists even though adjacent CDCs might suggest one should.**  
   Forecasting is already a dedicated CDC, but there is no portfolio-level forecast aggregation counterpart in the inspected portfolio surface.

## CDC Candidates

| Family | Semantic stability | Rule determinism | Duplication level | Coupling with application logic | Classification | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Portfolio progress / stock-flow reconstruction | Medium | Medium | Low | High | **B — promising but needs semantic clarification** | The reconstruction algorithm and summary rules are concentrated, but they are effort-based, approximation-heavy, and still handler-owned |
| Portfolio delivery distribution | Medium | High | Low | High | **C — still application aggregation / presentation logic** | Mostly groups, ranks, and maps upstream delivery outputs |
| Cross-product completion aggregation | High | High | Low | Medium | **A — ready candidate for CDC slice, but as DeliveryTrends rather than PortfolioFlow** | Already substantially owned by `PoTool.Core.Domain/Domain/DeliveryTrends` |
| Product contribution ranking | Medium | High | Low | High | **C — still application aggregation / presentation logic** | Ranking logic is useful, but not a domain kernel on its own |
| Portfolio forecast aggregation | Low | n/a | n/a | n/a | **C — not present as a coherent slice today** | No dedicated implementation found |

## Recommendation

The next step should **not** be extraction of one monolithic `PortfolioFlow` CDC slice.

Recommended direction:

1. **semantic clarification first**
   - decide whether portfolio scope/flow should remain effort-based or align with canonical story-point delivery semantics
   - decide whether “added flow” means backlog inflow, sprint commitment, or estimation delta

2. **then split into multiple portfolio-related slices rather than one**
   - a possible **Portfolio stock/flow** slice for the reconstruction algorithm in `GetPortfolioProgressTrendQueryHandler.cs`
   - keep **portfolio delivery distribution** as application aggregation unless stronger domain rules emerge
   - continue treating cross-product completion rollups as part of **DeliveryTrends**

Final recommendation:

- **splitting into multiple portfolio-related slices** is the correct target
- but the immediate prerequisite is **semantic clarification** of portfolio flow units and inflow meaning
- until that clarification happens, the only near-ready candidate is the narrow stock/flow reconstruction logic, not the entire portfolio surface
