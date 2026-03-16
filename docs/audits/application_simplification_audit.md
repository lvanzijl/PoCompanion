# Application Simplification Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/domain/cdc_reference.md`
- `docs/domain/cdc_domain_map.md`
- `docs/audits/cdc_completion_summary.md`
- `docs/domain/domain_model.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/state_rules.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/source_rules.md`

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`
- `PoTool.Api/Adapters/HistoricalSprintInputMapper.cs`
- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `PoTool.Client/Services/RoadmapAnalyticsService.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
- `PoTool.Shared/Metrics/PortfolioDeliveryDtos.cs`

Scope note:

- No `PoTool.Application` project exists in this repository snapshot. The audit therefore covered the active application-layer logic in `PoTool.Api`, CDC-adjacent helpers in `PoTool.Core`/`PoTool.Core.Domain` where they are consumed by handlers, and client-side calculator/service code in `PoTool.Client`.

Classification key:

1. `CDC duplication`
2. `adapter logic (valid)`
3. `presentation logic (valid)`
4. `transport compatibility`

## CDC Duplication Findings

| File | Function | CDC slice that should own it | Classification | Reason it is duplicated |
| --- | --- | --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `Handle`, `ComputeSummary` | `PortfolioFlow` | `CDC duplication` | The handler reads canonical `PortfolioFlowProjectionEntity` rows, but then recomputes `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, total scope change, remaining-scope change, and trajectory classification in application code. Those stock / inflow / throughput / completion semantics now belong to the `PortfolioFlow` slice and should be surfaced as CDC-backed rollups rather than re-derived in the handler. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `Handle` | `DeliveryTrends` | `CDC duplication` | Product delivery totals, delivered-effort shares, and feature contribution shares are re-aggregated in the handler from `SprintMetricsProjectionEntity` plus feature progress rows. `docs/domain/cdc_reference.md` already describes product delivery progress summaries as `DeliveryTrends` outputs, so this handler is still assembling slice-level delivery summaries that could be mapped instead. |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | `ComputeLocalAnalytics` | `Core Concepts` / `DeliveryTrends` | `CDC duplication` | The client traverses descendants, hardcodes terminal-state semantics (`Closed`, `Done`, `Removed`), and recomputes delivered vs remaining scope totals from cached work items. That duplicates hierarchy, state, and scope semantics on the client instead of consuming CDC-backed outputs. |

## Redundant Services

The audit found one clear service-layer simplification candidate and several handler-owned aggregations that should eventually become thin adapters.

| File | Current role | Suggested outcome | Why it is redundant or overscoped |
| --- | --- | --- | --- |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | Client-side analytics helper for roadmap cards | `candidate for thin adapter conversion` | The service mixes legitimate endpoint fan-out (`LoadForecastAsync`, `LoadBacklogHealthAsync`) with local hierarchy traversal and scope calculations in `ComputeLocalAnalytics`. The fan-out remains valid, but the local analytics branch is now CDC duplication and can be removed once an API surface exposes the needed rollups. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | Delivery composition handler over projection tables | `candidate for thin adapter conversion` | Although this is a handler rather than a reusable service, it currently acts like a calculation service: it aggregates delivery totals, derives percentage shares, and ranks features. Once `DeliveryTrends` exposes portfolio-level delivery summaries directly, this layer can collapse to loading and mapping only. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | Portfolio flow consumer over persisted projections | `candidate for thin adapter conversion` | The handler already reads canonical projection rows. The remaining work is structural: move the portfolio rollups it still computes (`CompletionPercent`, `NetFlowStoryPoints`, trajectory summary) behind CDC-backed outputs so the handler becomes a reader + mapper. |

Services and helpers reviewed but currently valid:

- `PoTool.Api/Services/SprintTrendProjectionService.cs` — `adapter logic (valid)`; it materializes CDC delivery-trend outputs and persists projections.
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs` — `adapter logic (valid)`; it is the canonical producer for persisted `PortfolioFlow` rows.
- `PoTool.Api/Adapters/HistoricalSprintInputMapper.cs` — `adapter logic (valid)`; entity-to-domain input mapping only.
- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs` — `adapter logic (valid)`; transport-to-domain input mapping only.
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs` — `transport compatibility`; it maps canonical fields onto legacy DTO names without recalculating the underlying progress metrics.
- `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs` — `presentation logic (valid)`; grouping, filtering, and recommendation text remain outside the CDC even though the formulas themselves already delegate to the CDC-backed analyzer.
- `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs` — `presentation logic (valid)` for the same reason.
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs` — `adapter logic (valid)`; effort totals, utilization, and heat-map math now delegate to the EffortPlanning CDC slice.
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs` — `adapter logic (valid)`; effort-quality rollups now delegate to the EffortPlanning CDC slice.
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs` — `adapter logic (valid)`; suggestion similarity, medians, and confidence now delegate to the EffortPlanning CDC slice.
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs` — `adapter logic (valid)`; percentile and predictability math are already delegated to `IVelocityCalibrationService`.
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` — mixed but mostly `adapter logic (valid)`; the forecast calculation itself is delegated to `ICompletionForecastService`, although the handler still owns input selection heuristics for which sprints to sample.

## DTO Calculation Leakage

| File | DTO or builder location | Classification | Leakage |
| --- | --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `Handle`, `ComputeSummary` | `CDC duplication` | Builds `PortfolioSprintProgressDto` and `PortfolioProgressSummaryDto` with new arithmetic for `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, scope deltas, and trajectory. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `Handle` | `CDC duplication` | Builds `PortfolioDeliverySummaryDto`, `ProductDeliveryDto`, and `FeatureDeliveryDto` with derived totals and `EffortShare` calculations instead of mapping CDC summary outputs. |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `Handle` | `adapter logic (valid)` but still DTO calculation leakage | The handler is mostly consuming projection rows, but the DTO-building phase recomputes many cross-product totals (`TotalPlannedEffort`, `TotalCompletedPbiEffort`, `TotalSpilloverStoryPoints`, etc.). These are safe to keep temporarily, yet they are structural aggregation in the DTO assembly layer rather than pure mapping. |
| `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `Handle` when building `SprintExecutionSummaryDto` | `presentation logic (valid)` | Count and effort-hour surfaces remain presentation-oriented, while the story-point summary fields are now mapped from the CDC-owned `SprintFactResult`. |

## Safe Simplification Opportunities

| Location | Current classification | Safe simplification |
| --- | --- | --- |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `CDC duplication` | Push portfolio-level completion / net-flow / trajectory rollups behind `PortfolioFlow` outputs, then map them directly into `PortfolioProgressTrendDto`. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `CDC duplication` | Promote portfolio delivery summaries and contribution rollups to a CDC-backed `DeliveryTrends` output so this handler becomes sorting/filtering plus transport mapping. |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | `CDC duplication` | Remove `ComputeLocalAnalytics` or reduce it to pure presentation shaping once the API exposes CDC-backed scope totals for roadmap cards. |
| `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs` | `transport compatibility` | Keep as-is until DTO versioning is approved; it is already a thin mapping seam and is not a simplification target yet. |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` and `PoTool.Api/Services/PortfolioFlowProjectionService.cs` | `adapter logic (valid)` | Keep as canonical materialization seams. The opportunity here is not removal, but making downstream consumers read their CDC-backed outputs more directly. |

## Resolved Sprint Commitment Simplifications

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - now reads committed and delivered story-point totals from `ISprintFactService.BuildSprintFactResult(...)`
  - no longer sums sprint story points through `ResolveSprintStoryPoints(...)`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - now maps `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, `SpilloverSP`, and `RemainingStoryPoints` from `SprintFactResult`
  - no longer reconstructs those totals through `SumStoryPoints(...)` or `SumDeliveredStoryPoints(...)`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
  - `RemainingStoryPoints` is now a mapped field instead of a transport-level formula

## Resolved EffortPlanning Simplifications

- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs`
  - now maps `EffortDistributionResult` from `IEffortDistributionService.Analyze(...)`
  - no longer calculates area totals, iteration totals, utilization percentages, or heat-map cells locally
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
  - now maps `EffortEstimationQualityResult` from `IEffortEstimationQualityService.Analyze(...)`
  - no longer calculates local variance, coefficient-of-variation accuracy, or iteration trends
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
  - now maps `EffortEstimationSuggestionResult` from `IEffortEstimationSuggestionService.GenerateSuggestion(...)`
  - no longer calculates local similarity scores, medians, or confidence heuristics

## Estimated Impact

Estimated reduction if the above simplifications are implemented in a later refactor:

- **duplicated calculations:** remove or centralize approximately 6 semantic hotspots covering sprint totals, remaining scope, net flow, completion percent, delivery-share math, and client-side hierarchy/state replay
- **service complexity:** reduce one client analytics service plus two heavy aggregation handlers to thin adapters
- **handler size:** likely remove roughly 150–250 lines of calculation-heavy code across `GetSprintExecutionQueryHandler`, `GetSprintMetricsQueryHandler`, `GetPortfolioProgressTrendQueryHandler`, and `GetPortfolioDeliveryQueryHandler`

Net assessment:

- CDC semantic ownership is broadly stable.
- Most remaining work is **structural cleanup around already-finished slices**, not new CDC extraction.
- The safest next simplifications are to eliminate calculation leakage from handlers, DTOs, and the client-side roadmap helper while preserving the existing valid adapter and compatibility seams.
