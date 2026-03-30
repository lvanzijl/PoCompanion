# Application Handler Cleanup

_Generated: 2026-03-16_

Reference documents:

- `docs/analysis/cdc-usage-coverage.md`
- `docs/analysis/application-simplification-audit.md`

## Handlers Scanned

The final pass scanned all handlers under `PoTool.Api/Handlers/` for helper methods and inline rollup arithmetic matching the cleanup criteria:

- story point summations
- completion percentages
- delivery totals
- scope-change calculations
- flow calculations
- distribution arithmetic

Handlers confirmed as already CDC-backed thin adapters for those semantics:

- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`

Handlers with helper methods or inline arithmetic that were re-evaluated during this pass:

- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`

Other handler groups under `PoTool.Api/Handlers/PullRequests`, `PoTool.Api/Handlers/ReleasePlanning`, and `PoTool.Api/Handlers/WorkItems` were also scanned. They did not contain remaining helpers that duplicate an existing CDC rollup seam for the targeted story point, completion, delivery, scope, flow, or distribution semantics.

## Helpers Removed

- none in this pass
- the previously-identified redundant helpers were already removed by the earlier sprint commitment, portfolio, effort planning, and backlog quality cleanup work
- this final scan did not find any additional handler-owned rollup helper whose semantics are already owned by an existing CDC service

## Helpers Retained (UI-only)

- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
  - `CalculateTrend(...)`, `CalculateTrendDirection(...)`, and `GenerateTrendSummary(...)` remain handler-owned because they summarize already-mapped `BacklogHealthDto` results for the multi-iteration dashboard view
- `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs`
  - area and iteration bucket shaping remains local before `EffortDiagnosticsAnalyzer.AnalyzeImbalance(...)`
  - `MapTeamImbalances(...)`, `MapSprintImbalances(...)`, and recommendation/description helpers remain UI-specific presentation mapping over CDC-owned imbalance facts
- `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs`
  - area and iteration bucket shaping remains local before `EffortDiagnosticsAnalyzer.AnalyzeConcentration(...)`
  - risk mapping, top-item formatting, and mitigation recommendation helpers remain UI-specific presentation mapping over CDC-owned concentration facts
- `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs`
  - `CalculateTeamCapacities(...)`, `DetermineCapacityStatus(...)`, and `GenerateWarnings(...)` remain page-specific capacity-plan presentation logic
  - no equivalent CDC seam currently exists for this endpoint, so removing these helpers would require new domain ownership beyond the scope of this cleanup
- `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`
  - `LoadSprintProgressPercentageAsync(...)` and `CalculateSprintProgressPercentage(...)` remain a compact home-bar display metric rather than a reusable CDC rollup
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - per-sprint totals are still transport aggregation over `SprintMetricsProjectionEntity` rows and are not duplicated helper methods
  - no additional local helper extraction was required in this pass

## Lines of Code Removed

- `0` additional handler lines were removed in this follow-up scan
- the current codebase already reflects the earlier removals documented in:
  - `docs/analysis/sprint-commitment-handler-simplification.md`
  - `docs/analysis/portfolio-handler-simplification.md`
  - `docs/analysis/backlog-health-simplification.md`
  - `docs/analysis/effort-planning-boundary-cleanup.md`

## Final Handler Responsibilities

Handlers in the audited scope now follow this boundary:

- load data from repositories, EF projections, or existing query seams
- invoke CDC/domain services for canonical calculations:
  - `ISprintFactService`
  - `IBacklogQualityAnalysisService`
  - `IPortfolioFlowSummaryService`
  - `IPortfolioDeliverySummaryService`
  - `IEffortDistributionService`
  - `IEffortEstimationQualityService`
  - `IEffortEstimationSuggestionService`
  - `EffortDiagnosticsAnalyzer`
- map canonical outputs into transport DTOs
- keep only UI-specific shaping, compatibility mapping, and recommendation text where the logic is not a reusable CDC semantic

Net result:

- no remaining handler-owned rollup helpers were found that duplicate an existing CDC ownership boundary
- no additional handler test assertions needed to move in this pass because no new calculation extraction was performed
