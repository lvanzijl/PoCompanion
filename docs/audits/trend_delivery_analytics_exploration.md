# Trend / Delivery Analytics Exploration

_Generated: 2026-03-15_

## Summary

A coherent **delivery trend analytics** slice does exist, but it is narrower than the repository's full "trend / forecast / delivery analytics" surface.

The strongest nucleus is the sprint-projection stack around `PoTool.Api/Services/SprintTrendProjectionService.cs` and `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`. That stack already applies canonical product-scoped sprint rules, reconstructs commitment and first-Done delivery from ledger history, keeps story-point delivery separate from effort diagnostics, and feeds downstream consumers such as capacity calibration, epic forecasting, and portfolio delivery views.

Beyond that nucleus, the repository contains several adjacent but semantically different families:

- **Forecasting** on top of sprint delivery history (`GetEpicCompletionForecastQueryHandler`, `GetEffortDistributionTrendQueryHandler`)
- **Portfolio stock/flow analytics** using effort-based proxies (`GetPortfolioProgressTrendQueryHandler`)
- **Operational delivery insights** for PRs and pipelines (`GetPrSprintTrendsQueryHandler`, `GetPrDeliveryInsightsQueryHandler`, `GetPullRequestInsightsQueryHandler`, `GetPipelineInsightsQueryHandler`)
- **Estimation confidence heuristics** that reuse some of the same math words but not the same domain meaning (`GetEffortEstimationQualityQueryHandler`, `GetEffortEstimationSuggestionsQueryHandler`)

Conclusion: there is a good CDC candidate, but it is **not** one monolithic "trend analytics" slice. The next extraction should target the **delivery trend analytics core** first, while the forecasting / volatility / operational analytics families need separate semantic decisions.

## Inventory

| File | Class | Method(s) | Concept | Inputs | Outputs |
| --- | --- | --- | --- | --- | --- |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeProjectionsAsync` | Historical sprint projection orchestration | `productOwnerId`, `sprintIds`, `Products`, `Sprints`, `ResolvedWorkItems`, `WorkItems`, `ActivityEventLedgerEntries` | Reads/upserts `SprintMetricsProjectionEntity` rows per sprint/product |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeProductSprintProjection` | Canonical sprint delivery projection | sprint definition, product-resolved hierarchy, state/iteration change events, commitment set, first-Done lookup | `Planned*`, `Worked*`, `CompletedPbi*`, `Spillover*`, derived-estimate diagnostics, `UnestimatedDeliveryCount`, `ProgressionDelta` |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeProgressionDelta` | Feature-level progress change for active sprint work | product hierarchy, work item snapshots, sprint activity, rollup services | Average feature progression delta |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | `ComputeFeatureProgressAsync`, `ComputeFeatureProgress`, `ComputeEpicProgressAsync` | Feature/epic delivery progress rollups | resolved hierarchy, work item snapshots, optional sprint activity window, story-point resolver, hierarchy rollup service | `FeatureProgressDto[]`, `EpicProgressDto[]` |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | `Handle` | Cached/recomputed sprint trend retrieval | `productOwnerId`, `sprintIds`, `recompute`, stored projections, sprint/product metadata | `GetSprintTrendMetricsResponse` with grouped `SprintTrendMetricsDto`, progress detail, staleness flag |
| `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `GetEpicCompletionForecastQueryHandler` | `Handle`, `GetVelocitySprintsAsync`, `BuildSprintForecast`, `DetermineConfidence` | Epic/feature completion forecasting from historical sprint delivery | target epic/feature, hierarchy rollup, sprint metrics history, `MaxSprintsForVelocity` | `EpicCompletionForecastDto` with story-point scope, average velocity, sprint-by-sprint forecast, completion date, confidence |
| `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` | `GetCapacityCalibrationQueryHandler` | `Handle` | Velocity calibration and predictability bands | sprint projections for requested sprints/products | `CapacityCalibrationDto` with committed/delivered story points, P25/P50/P75 velocity, hours-per-SP, predictability, outliers |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `GetEffortDistributionTrendQueryHandler` | `AnalyzeSprintTrends`, `AnalyzeAreaPathTrends`, `CalculateOverallTrend`, `GenerateForecasts`, `CalculateLinearRegressionSlope`, `DetermineEffortTrendDirectionFromSlope` | Effort distribution trend, slope analysis, volatility detection, future effort forecasts | work items with effort, iteration paths, area paths, optional default capacity | `EffortDistributionTrendDto` with per-sprint trends, per-area trends, slope, volatility/stability direction, 3-sprint forecasts |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | `Handle`, `ComputeHistoricalScopeEffort`, `ComputeSummary` | Portfolio stock/flow trend analysis | selected sprints, resolved PBIs/Bugs, work item snapshots, effort/state history, sprint projections | `PortfolioProgressTrendDto` with reconstructed scope, remaining effort, throughput, added-effort proxy, net flow, trajectory |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `GetPortfolioDeliveryQueryHandler` | `Handle` | Portfolio delivery distribution snapshot | sprint projections, product names, feature progress over sprint range | `PortfolioDeliveryDto` with per-product delivery shares, summary totals, top feature contributors |
| `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs` | `GetPrSprintTrendsQueryHandler` | `Handle` | Sprint-scoped PR operational trend metrics | requested sprint ids, PRs by created date, file changes, comments | `GetPrSprintTrendsResponse` with median PR size, median first-review time, median merge time, P90 merge time per sprint |
| `PoTool.Api/Handlers/PullRequests/GetPullRequestInsightsQueryHandler.cs` | `GetPullRequestInsightsQueryHandler` | `Handle`, `BuildProblematicEntry`, `Median` | PR health and ranking analytics | PRs in date window, iterations, comments, file changes, optional team/repository scope | `PullRequestInsightsDto` with lifetime medians/P90s, author/repository breakdowns, scatter points, top problematic PRs |
| `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs` | `GetPrDeliveryInsightsQueryHandler` | `Handle`, `ComputeLifetimeHours`, `Median` | PR delivery classification against work-item hierarchy | PRs, cached PR→work-item links, cached work item hierarchy, optional sprint/team scope | `PrDeliveryInsightsDto` with category counts, epic/feature breakdowns, lifetime medians/P90s |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `GetPipelineInsightsQueryHandler` | `Handle`, `BuildTop3`, `ClassifyRuns`, `Median` | Pipeline stability/duration analytics per sprint | selected sprint + previous sprint, pipeline definitions, cached pipeline runs | `PipelineInsightsDto` with failure/warning rates, median/P90 durations, top pipelines in trouble |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs` | `GetEffortEstimationQualityQueryHandler` | `CalculateQualityByType`, `CalculateTrendOverTime`, `CalculateOverallAccuracy` | Effort consistency trend mislabeled as estimation accuracy | completed work items with effort grouped by type/iteration | `EffortEstimationQualityDto` with average "accuracy" and time trend |
| `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` | `GetEffortEstimationSuggestionsQueryHandler` | `GenerateSuggestion`, `CalculateSimilarity`, `CalculateConfidence` | Historical estimation suggestion confidence heuristic | unestimated items, historical completed items, settings, variance, title/area/type similarity | `EffortEstimationSuggestionDto[]` with suggested effort, confidence, rationale, examples |
| `PoTool.Client/Services/PullRequestInsightsCalculator.cs` | `PullRequestInsightsCalculator` | `CalculateLeadTimeToMerge`, `CalculateCycleTime`, `CalculateTimeToFirstReview`, `CalculateReviewDuration`, `CalculatePRSize`, `CalculateReworkRate` | Client-side PR metric calculations | API PR DTOs, comments, iterations, metrics | `MetricResult` values using local median wrappers plus shared percentiles |
| `PoTool.Client/Services/PipelineInsightsCalculator.cs` / `PoTool.Client/Services/BugInsightsCalculator.cs` | `PipelineInsightsCalculator` / `BugInsightsCalculator` | metric calculation methods | Client-side percentile/median consumers for operational insights | API pipeline/bug DTOs | `MetricResult` values with local median wrappers and shared percentiles |
| `PoTool.Api/Services/Sync/MetricsComputeStage.cs` | `MetricsComputeStage` | `CalculateWorkItemMetricsAsync` | Internal cached work-item metric with conflicting velocity label | cached work items closed in last 7 days | internal metric tuple containing `Velocity7d` = sum of `Effort` with unit `"points"` |

## Statistical Primitives Used

| Primitive | Current usage | Shared helper? | Notes |
| --- | --- | --- | --- |
| Percentile | `GetCapacityCalibrationQueryHandler`, `GetPrSprintTrendsQueryHandler`, `GetPullRequestInsightsQueryHandler`, `GetPrDeliveryInsightsQueryHandler`, `GetPipelineInsightsQueryHandler`, `PoTool.Client/Services/PullRequestInsightsCalculator.cs`, `PoTool.Client/Services/PipelineInsightsCalculator.cs`, `PoTool.Client/Services/BugInsightsCalculator.cs` | **Yes** — `PoTool.Shared/Statistics/PercentileMath.cs` | Current repository-default percentile semantics are aligned on `PercentileMath.LinearInterpolation(...)`. |
| Variance | `GetEffortEstimationQualityQueryHandler`, `GetEffortEstimationSuggestionsQueryHandler` | **Yes** — `PoTool.Core.Domain/Domain/Statistics/StatisticsMath.cs` | Used as a pure spread primitive, then reinterpreted as "accuracy" or suggestion confidence. |
| Standard deviation | `GetEffortDistributionTrendQueryHandler.CalculateStandardDeviation` | **Yes**, via wrapper to `StatisticsMath.StandardDeviation(...)` | Shared pure math is used, but the semantic meaning remains slice-local. |
| Median | PR handlers, pipeline handler, client calculators | **Partially** | Local median wrappers remain because nullable/empty-sample contracts differ from `StatisticsMath.Median(...)`. |
| Linear regression / slope | `GetEffortDistributionTrendQueryHandler.CalculateLinearRegressionSlope` | **No** | Still a local forecasting helper. |
| Coefficient of variation | Inline in `GetEffortDistributionTrendQueryHandler` and `GetEffortEstimationQualityQueryHandler` | **No active shared consumer** | `CanonicalEffortDiagnosticsStatistics.CoefficientOfVariation(...)` exists, but these slices compute CV inline. |
| Volatility detection | `GetEffortDistributionTrendQueryHandler.DetermineEffortTrendDirectionFromSlope` | **No** | Local `coefficientOfVariation > 0.5` heuristic. |
| Confidence interval | `GetEffortDistributionTrendQueryHandler.GenerateForecasts` | **No** | Local `± 2 * stdDev` heuristic. |
| Moving averages | none found | n/a | No moving-average implementation was found in the current search scope. |

## Domain Families

### 1. Delivery trend analytics core

Files centered on `SprintTrendProjectionService` and `GetSprintTrendMetricsQueryHandler` form the clearest family.

This family owns:

- commitment reconstruction
- first-Done delivery attribution
- spillover detection
- story-point vs effort separation
- per-product sprint projections
- feature/epic progress rollups
- staleness-aware trend retrieval

This is the strongest current CDC candidate because its semantics are already anchored in the canonical sprint and metrics rules.

### 2. Forecasting on top of delivery history

`GetEpicCompletionForecastQueryHandler` and part of `GetCapacityCalibrationQueryHandler` sit on top of the delivery trend core.

They reuse stable sprint outputs, but then add their own planning heuristics:

- average historical velocity sampling
- P25/P50/P75 capacity banding
- sprint-count-based forecast confidence
- synthetic future sprint buckets

This looks like a second family rather than part of the base projection slice.

### 3. Effort distribution / volatility analytics

`GetEffortDistributionTrendQueryHandler` is a separate effort-oriented family.

It is coherent internally, but its semantics are different from the story-point-based delivery core:

- input unit is effort-hours
- iteration ordering is based on iteration-path strings
- trend direction uses regression slope plus coefficient-of-variation thresholds
- forecast ranges are heuristic confidence intervals

This is promising, but it still reads as a specialized heuristic analytics slice.

### 4. Portfolio flow analytics

`GetPortfolioProgressTrendQueryHandler` and `GetPortfolioDeliveryQueryHandler` together describe product/portfolio-level delivery views.

The first is an effort-based stock/flow model with historical scope reconstruction; the second is a delivery distribution snapshot fed by stored sprint projections. They are related, but only loosely: one is historical scope-flow reconstruction, the other is a composition view over already-derived delivery outputs.

### 5. Operational software-delivery insights

PR and pipeline handlers form a coherent operations-oriented family:

- `GetPrSprintTrendsQueryHandler`
- `GetPullRequestInsightsQueryHandler`
- `GetPrDeliveryInsightsQueryHandler`
- `GetPipelineInsightsQueryHandler`
- matching client calculators

This family uses trend words and delivery words, but its subject is operational software delivery (PR review cycles, PR lifetime, pipeline stability), not canonical backlog/sprint delivery.

### 6. Estimation volatility / confidence heuristics

`GetEffortEstimationQualityQueryHandler` and `GetEffortEstimationSuggestionsQueryHandler` are adjacent because both reuse variance-style spread logic.

However, they are not really part of the delivery trend core. They are estimation-diagnostics heuristics that overlap statistically with forecasting terminology without sharing a stable domain meaning.

## Semantic Overlaps

| Term | Current meanings |
| --- | --- |
| `delivery` | In sprint projection code it means **first Done transition of PBIs inside a sprint window**. In `GetPortfolioDeliveryQueryHandler` it means **aggregated output from sprint projections plus feature contribution**. In `GetPrDeliveryInsightsQueryHandler` it means **whether a PR can be mapped into Feature/Epic delivery hierarchy categories**. |
| `forecast` | In `GetEpicCompletionForecastQueryHandler` it means **future sprint completion prediction from average historical velocity**. In `GetEffortDistributionTrendQueryHandler` it means **future effort buckets extrapolated from slope + standard deviation**. |
| `projection` | In `SprintTrendProjectionService` and `SprintTrendMetricsDto` it means **stored derived historical sprint facts** rather than future prediction. |
| `trend` | In `GetSprintTrendMetricsQueryHandler` it is a **historical multi-sprint delivery view**. In `GetEffortDistributionTrendQueryHandler` it is **direction + slope + volatility classification**. In `GetPrSprintTrendsQueryHandler` it is **per-sprint medians and percentiles for PR operational metrics**. |
| `velocity` | In the core sprint stack, epic forecast, and capacity calibration it means **delivered story points**. In `MetricsComputeStage.CalculateWorkItemMetricsAsync` it currently means **sum of Effort across recently closed items**, even though the metric is labeled `Velocity7d` and unit `"points"`. |
| `confidence` | `GetEpicCompletionForecastQueryHandler` uses **sample-depth buckets**. `GetEffortDistributionTrendQueryHandler` uses **coefficient-of-variation bands**. `GetEffortEstimationSuggestionsQueryHandler` uses **sample size + variance damping**. `CapacityCalibrationDto` reuses the word informally when describing P25 as a "high-confidence" planning number. |
| `accuracy` | `EffortEstimationQualityDto` says estimate-vs-actual quality, but `GetEffortEstimationQualityQueryHandler` actually computes **inverse coefficient of variation** (consistency/stability). `PortfolioProgressTrendDtos.cs` uses "accuracy" only as **historical reconstruction fidelity** in documentation comments. |
| `stability` / `volatile` | `GetEffortDistributionTrendQueryHandler` makes this an explicit statistical classification. Pipeline docs use "stability" informally for build health, but no shared stability score exists across the repository. |

## Contradictions

1. **A non-canonical velocity label still exists outside the main sprint analytics flow.**  
   `PoTool.Api/Services/Sync/MetricsComputeStage.cs` computes `Velocity7d` as `closedItems.Sum(w => w.Effort ?? 0)` and labels the unit as `"points"`. That conflicts with the story-point velocity semantics used by sprint projections, epic forecasting, and capacity calibration.

2. **`GetEffortDistributionTrendQueryHandler` uses two different trend-direction models at the same time.**  
   `AnalyzeSprintTrends(...)` assigns each sprint a direction from simple sprint-to-sprint percentage change, while `CalculateOverallTrend(...)` and `AnalyzeAreaPathTrends(...)` use regression slope plus coefficient-of-variation thresholds. The same DTO family therefore mixes local delta-based direction and regression-based direction.

3. **Iteration ordering for the effort/forecast handlers is heuristic rather than canonical.**  
   `GetEffortDistributionTrendQueryHandler` orders iteration paths lexicographically, and `GetEpicCompletionForecastQueryHandler.GetVelocitySprintsAsync(...)` also orders iteration paths lexicographically before sampling history. Both approaches assume sortable sprint naming instead of authoritative sprint dates.

4. **Legacy `*Effort` property names hide different units across slices.**  
   `EpicCompletionForecastDto` intentionally keeps legacy `TotalEffort`, `CompletedEffort`, and `RemainingEffort` names even though the handler maps them from canonical story-point rollups. `PortfolioProgressTrendDtos.cs` uses `*Effort` literally for effort-based stock/flow values. The same field vocabulary therefore does not imply the same unit.

5. **Portfolio inflow is still a proxy, not true backlog inflow.**  
   `GetPortfolioProgressTrendQueryHandler` documents `AddedEffort` as a proxy taken from `SprintMetricsProjection.PlannedEffort`, not from actual backlog-addition events. The family is coherent as an effort-flow heuristic, but it should not be mistaken for canonical churn analytics.

6. **"Accuracy" remains semantically misleading in the estimation-quality slice.**  
   `GetEffortEstimationQualityQueryHandler` never compares an estimate with an actual. It converts spread (variance / coefficient of variation) into an `AverageAccuracy` score, so the DTO name and the executed rule are still mismatched.

## CDC Candidates

| Family | Semantic stability | Rule determinism | Duplication level | Coupling with application logic | Classification | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Delivery trend analytics core (`SprintTrendProjectionService`, `GetSprintTrendMetricsQueryHandler`, feature/epic progress rollups) | High | High | Low | Medium | **A — ready candidate for CDC slice** | Canonical sprint semantics are already concentrated here; the remaining work is ownership extraction away from API/EF orchestration. |
| Forecasting from delivery history (`GetEpicCompletionForecastQueryHandler`, downstream capacity calibration usage) | Medium | High | Low | Medium | **B — promising but needs semantic clarification** | Inputs are stable, but confidence semantics, date assumptions, and legacy effort naming still need a contract decision. |
| Velocity calibration / capacity calibration (`GetCapacityCalibrationQueryHandler`) | Medium | High | Low | Medium | **B — promising but needs semantic clarification** | Story-point semantics are now aligned, but the family still uses slice-specific planning language such as "high-confidence capacity" for P25. |
| Effort distribution / volatility analytics (`GetEffortDistributionTrendQueryHandler`) | Medium | Medium | Low | Medium | **B — promising but needs semantic clarification** | Clear internal math, but slope thresholds, volatility bands, and iteration-order heuristics are still local heuristics. |
| Portfolio flow analytics (`GetPortfolioProgressTrendQueryHandler`, `GetPortfolioDeliveryQueryHandler`) | Medium | Medium | Low | High | **B — promising but needs semantic clarification** | The family is real, but it mixes historical event replay, effort proxies, and projection composition, so extraction should wait for a sharper semantic boundary. |
| Operational PR/pipeline delivery insights (`GetPrSprintTrendsQueryHandler`, `GetPullRequestInsightsQueryHandler`, `GetPrDeliveryInsightsQueryHandler`, `GetPipelineInsightsQueryHandler`) | Medium | High | Medium | High | **C — still application heuristics** | The family is coherent, but it is operational engineering analytics rather than the canonical backlog/sprint delivery domain. |
| Estimation confidence heuristics (`GetEffortEstimationQualityQueryHandler`, `GetEffortEstimationSuggestionsQueryHandler`) | Low | Medium | Medium | Medium | **C — still application heuristics** | Shared math words exist, but the semantics of accuracy/confidence are still unstable. |

## Recommendation

**Recommended next step: splitting into multiple slices.**

The repository already contains one extraction-ready slice:

1. **Extract the delivery trend analytics core** around `SprintTrendProjectionService`, `GetSprintTrendMetricsQueryHandler`, and the feature/epic progress rollups.

Then treat the nearby families separately:

2. **Do semantic clarification for forecasting and volatility** before extracting `GetEpicCompletionForecastQueryHandler`, `GetCapacityCalibrationQueryHandler`, and `GetEffortDistributionTrendQueryHandler` into one shared CDC surface.
3. **Keep PR/pipeline operational insights separate** from backlog/sprint delivery analytics; they use similar statistics but belong to a different domain family.
4. **Keep portfolio flow analytics as a later follow-up** after deciding whether the target slice should stay effort-oriented or be re-grounded in canonical story-point churn semantics.

In short: a coherent slice exists, but it is the **delivery trend analytics core**, not the entire current trend/forecast/reporting surface.

## Delivery Trend Analytics CDC Progress — Domain Models Added

The canonical delivery-trend domain slice now includes the following domain-only models under `PoTool.Core.Domain/Domain/DeliveryTrends/Models`:

- `SprintDeliveryProjection`
- `SprintTrendMetrics`
- `FeatureProgress`
- `EpicProgress`
- `ProgressionDelta`

The scope was kept intentionally narrow:

- only sprint delivery projections
- only sprint trend metrics
- only feature progress rollups
- only epic progress rollups
- no epic completion forecasting
- no capacity calibration
- no effort distribution trend
- no portfolio flow analytics
- no PR/pipeline analytics

The models preserve canonical units by naming story-point fields as story points while keeping actual effort deltas explicitly effort-based.

Focused MSTest coverage was added for:

- canonical construction of the new delivery-trend domain models
- bounded progression delta semantics
- aggregation of sprint totals from per-product projections
- construction-time invariant validation only

## Delivery Trend Analytics CDC Progress — Projection Core Extracted

The canonical sprint projection formulas have now been moved into `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`.

Formulas moved into the CDC service:

- planned/worked/completed counts
- planned/completed/spillover story-point delivery values
- spillover counts and effort values
- derived-estimate diagnostics
- unestimated delivery counts
- progression delta integration

`PoTool.Api/Services/SprintTrendProjectionService.cs` is now reduced to orchestration responsibilities:

- loading sprints, work items, and activity history
- preparing canonical domain inputs
- reconstructing commitment / first-Done / spillover context
- persisting `SprintMetricsProjectionEntity` rows

Focused tests were added for the extracted projection core to verify:

- derived-story-point and unestimated-delivery semantics
- commitment plus spillover handling
- progression delta averaging based on sprint progress activity
