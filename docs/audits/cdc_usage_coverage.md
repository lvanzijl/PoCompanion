# CDC Usage Coverage Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/domain/cdc_reference.md`
- `docs/domain/cdc_domain_map.md`
- `docs/audits/cdc_completion_summary.md`
- `docs/audits/backlog_quality_cdc_summary.md`
- `docs/audits/delivery_trend_analytics_cdc_summary.md`
- `docs/audits/forecasting_cdc_summary.md`
- `docs/audits/effort_diagnostics_cdc_extraction_report.md`
- `docs/audits/application_simplification_audit.md`

Files analyzed:

- `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`
- `PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs`
- `PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsAnalyzer.cs`

Scope note:

- This audit covers the requested analytics handler groups only:
  - backlog quality handlers
  - sprint metrics handlers
  - trend analytics handlers
  - forecast handlers
  - portfolio handlers
  - effort diagnostics handlers
- The audit focuses on calculation ownership and usage paths, not UI behavior or persistence design.

Classification key:

- `CDC compliant`
- `CDC bypass`
- `legacy compatibility path`
- `unavoidable adapter logic`

## Handler Inventory

### Backlog quality handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetBacklogHealthQueryHandler` | `IWorkItemReadProvider`, `IProductRepository`, `IMediator`, `IHierarchicalWorkItemValidator` | none | `IHierarchicalWorkItemValidator.ValidateWorkItems(...)` provides validation findings, but the handler still computes blocked-item counts, in-progress-at-end counts, and grouped summaries locally via `CountBlockedItems`, `CountInProgressAtEnd`, and `GroupValidationIssuesByConsequence`. | `legacy compatibility path` |
| `GetMultiIterationBacklogHealthQueryHandler` | `IWorkItemReadProvider`, `IProductRepository`, `ISprintRepository`, `IMediator`, `IHierarchicalWorkItemValidator` | `SprintWindowSelector` | `CalculateIterationHealth(...)` reuses `IHierarchicalWorkItemValidator.ValidateWorkItems(...)`, then computes per-iteration totals and trend directions locally. Placeholder slot generation and trend aggregation stay outside the BacklogQuality slice. | `legacy compatibility path` |

### Sprint metrics handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetSprintMetricsQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `ISprintRepository`, `IWorkItemStateClassificationService`, `ISprintCommitmentService`, `ISprintScopeChangeService`, `ISprintCompletionService`, `ISprintFactService`, `IMediator`, `PoToolDbContext` | none | Commitment membership and first-Done attribution still come from CDC services, and story-point totals now come from `ISprintFactService.BuildSprintFactResult(...)` instead of handler-local summation. The handler remains a thin adapter for counts and DTO mapping. | `CDC compliant` |
| `GetSprintExecutionQueryHandler` | `PoToolDbContext`, `IWorkItemStateClassificationService`, `ISprintCommitmentService`, `ISprintScopeChangeService`, `ISprintCompletionService`, `ISprintSpilloverService`, `ISprintFactService` | `SprintMetrics.ISprintExecutionMetricsCalculator` | Sprint fact reconstruction stays CDC-backed (`BuildCommittedWorkItemIds`, `BuildFirstDoneByWorkItem`, `DetectScopeAdded`, `DetectScopeRemoved`, `BuildSpilloverWorkItemIds`), and the canonical story-point totals now come from `ISprintFactService.BuildSprintFactResult(...)` before the handler maps counts and execution-specific heuristics. | `CDC compliant` |

### Trend analytics handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetSprintTrendMetricsQueryHandler` | `PoToolDbContext`, `SprintTrendProjectionService` | none | The handler reads `SprintMetricsProjectionEntity` rows from `SprintTrendProjectionService.GetProjectionsAsync(...)` / `ComputeProjectionsAsync(...)`, then gets feature and epic progress from `ComputeFeatureProgressAsync(...)` and `ComputeEpicProgressAsync(...)`. Remaining work is transport aggregation over persisted CDC outputs. | `CDC compliant` |

### Forecast handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetEpicCompletionForecastQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator`, `IWorkItemStateClassificationService`, `IHierarchyRollupService` | `ICompletionForecastService` | Total and completed scope come from `IHierarchyRollupService.RollupCanonicalScope(...)`; forecast projection comes from `ICompletionForecastService.Forecast(...)`. Historical velocity is loaded indirectly by chaining `GetSprintMetricsQuery`, which now reads sprint totals from the CDC-owned sprint fact seam. | `CDC compliant` |
| `GetCapacityCalibrationQueryHandler` | `PoToolDbContext` | `IVelocityCalibrationService` | The handler reads `SprintMetricsProjectionEntity` rows and delegates percentile and predictability math to `IVelocityCalibrationService.Calibrate(...)`. | `CDC compliant` |
| `GetEffortDistributionTrendQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `IEffortTrendForecastService` | Forecast slope, volatility, and confidence bands come from `IEffortTrendForecastService.Analyze(...)` in the Forecasting CDC. The handler limits itself to loading, optional filtering, and DTO mapping. | `CDC compliant` |

### Portfolio handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetPortfolioProgressTrendQueryHandler` | `PoToolDbContext` | `IPortfolioFlowSummaryService` | The handler loads canonical `PortfolioFlowProjectionEntity` rows, delegates per-sprint and range rollups to `_portfolioFlowSummaryService.BuildTrend(...)`, and maps the CDC-owned outputs to `PortfolioProgressTrendDto`. | `CDC compliant` |
| `GetPortfolioDeliveryQueryHandler` | `PoToolDbContext`, `SprintTrendProjectionService` | `IPortfolioDeliverySummaryService` | The handler loads `SprintMetricsProjectionEntity` rows plus feature progress, delegates totals and contribution shares to `_portfolioDeliverySummaryService.BuildSummary(...)`, and maps the canonical delivery summary to DTOs. | `CDC compliant` |

### Effort diagnostics handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetEffortImbalanceQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `EffortDiagnosticsAnalyzer` | Imbalance formulas are delegated to `Analyzer.AnalyzeImbalance(...)`, which now bridges into the canonical effort-diagnostics rules and statistics. The handler still owns grouping, filtering, and recommendation text. | `unavoidable adapter logic` |
| `GetEffortConcentrationRiskQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `EffortDiagnosticsAnalyzer` | Concentration formulas are delegated to `Analyzer.AnalyzeConcentration(...)`, with local responsibility limited to visibility filtering, top-item lists, and mitigation text. | `unavoidable adapter logic` |
| `GetEffortDistributionQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `IEffortDistributionService` | The handler filters to scoped work items with effort, delegates area/iteration totals plus heat-map utilization to `_effortDistributionService.Analyze(...)`, and maps the canonical result to `EffortDistributionDto`. | `CDC compliant` |
| `GetEffortEstimationQualityQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator`, `IWorkItemStateClassificationService` | `IEffortEstimationQualityService` | The handler filters to completed work items with effort, delegates quality rollups to `_effortEstimationQualityService.Analyze(...)`, and maps the canonical result to `EffortEstimationQualityDto`. | `CDC compliant` |
| `GetEffortEstimationSuggestionsQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator`, `IWorkItemStateClassificationService` | `IEffortEstimationSuggestionService` | The handler filters suggestion candidates and historical completed samples, delegates similarity / median / confidence formulas to `_effortEstimationSuggestionService.GenerateSuggestion(...)`, and maps the canonical result to `EffortEstimationSuggestionDto`. | `CDC compliant` |

## CDC-Compliant Handlers

Handlers that are fully powered by CDC outputs or CDC-owned domain services:

- `GetSprintMetricsQueryHandler`
  - reconstructs membership and first-Done attribution through CDC services
  - reads committed and delivered story-point totals from `ISprintFactService.BuildSprintFactResult(...)`
  - handler work is retrieval, scope-count shaping, and DTO mapping
- `GetSprintExecutionQueryHandler`
  - reconstructs membership, churn, completion, and spillover through CDC services
  - reads committed, added, removed, delivered, delivered-from-added, spillover, and remaining story points from `ISprintFactService.BuildSprintFactResult(...)`
  - delegates rates to `SprintMetrics.ISprintExecutionMetricsCalculator` and keeps only presentation-oriented counts / heuristics locally
- `GetSprintTrendMetricsQueryHandler`
  - reads `SprintMetricsProjectionEntity` rows produced by `SprintTrendProjectionService`
  - feature and epic rollups come from `ComputeFeatureProgressAsync(...)` and `ComputeEpicProgressAsync(...)`
  - handler work is retrieval, staleness detection, and DTO assembly
- `GetEpicCompletionForecastQueryHandler`
  - scope rollups come from `IHierarchyRollupService.RollupCanonicalScope(...)`
  - forecast projection comes from `ICompletionForecastService.Forecast(...)`
  - no local forecast math is implemented in the handler
- `GetCapacityCalibrationQueryHandler`
  - reads `SprintMetricsProjectionEntity` rows
  - delegates quartiles, median velocity, and predictability to `IVelocityCalibrationService`
- `GetEffortDistributionTrendQueryHandler`
  - delegates trend slope, volatility, and forecast bands to `IEffortTrendForecastService`
  - handler remains a thin adapter over a Forecasting CDC service
- `GetPortfolioProgressTrendQueryHandler`
  - reads `PortfolioFlowProjectionEntity` rows
  - delegates completion percent, net flow, cumulative net flow, scope deltas, and trajectory to `IPortfolioFlowSummaryService`
  - handler work is product filtering, sprint loading, and DTO mapping
- `GetPortfolioDeliveryQueryHandler`
  - reads `SprintMetricsProjectionEntity` rows plus `ComputeFeatureProgressAsync(...)` results
  - delegates delivery totals, product shares, and feature contribution shares to `IPortfolioDeliverySummaryService`
  - handler work is sprint-range loading, compatibility mapping, and DTO shaping
- `GetEffortDistributionQueryHandler`
  - filters scoped work items with effort
  - delegates area totals, iteration totals, utilization percentages, and heat-map cells to `IEffortDistributionService`
  - handler work is retrieval, filtering, and DTO mapping
- `GetEffortEstimationQualityQueryHandler`
  - filters completed work items through `IWorkItemStateClassificationService`
  - delegates quality-by-type, trend, and overall-accuracy rollups to `IEffortEstimationQualityService`
  - handler work is retrieval, completed-state filtering, and DTO mapping
- `GetEffortEstimationSuggestionsQueryHandler`
  - filters unestimated candidates and completed historical samples
  - delegates similarity scoring, medians, confidence, and rationale factors to `IEffortEstimationSuggestionService`
  - handler work is candidate selection, settings lookup, and DTO mapping

Supporting CDC-backed materialization seams confirmed during the audit:

- `SprintTrendProjectionService`
  - reconstructs committed scope via `_sprintCommitmentService.BuildCommittedWorkItemIds(...)`
  - reconstructs first-Done attribution via `_sprintCompletionService.BuildFirstDoneByWorkItem(...)`
  - resolves next sprint via `_sprintSpilloverService.GetNextSprintPath(...)`
  - passes those CDC outputs into `_deliveryTrendProjectionService.Compute(...)`
- `PortfolioFlowProjectionService`
  - reconstructs first-Done attribution via `_sprintCompletionService.BuildFirstDoneByWorkItem(...)`
  - materializes canonical `StockStoryPoints`, `RemainingScopeStoryPoints`, `InflowStoryPoints`, and `ThroughputStoryPoints`

## CDC Bypass Findings

Handlers that still compute delivery analytics locally instead of consuming already-owned CDC outputs:

- none in the audited effort-planning or portfolio scope

## Compatibility Paths

### Legacy compatibility path

- `GetBacklogHealthQueryHandler`
  - depends on `IHierarchicalWorkItemValidator`, which remains a legacy-facing wrapper over analyzer-backed backlog-quality results
  - still computes dashboard-specific blocked and in-progress heuristics locally
- `GetMultiIterationBacklogHealthQueryHandler`
  - uses the same validator seam
  - still owns placeholder sprint-window shaping and trend summary logic for the current health UI

These handlers consume a compatibility wrapper rather than direct BacklogQuality CDC outputs. Their remaining local logic exists to preserve current health-dashboard behavior.

### Unavoidable adapter logic

- `GetEffortImbalanceQueryHandler`
  - canonical imbalance math lives behind `EffortDiagnosticsAnalyzer.AnalyzeImbalance(...)`
  - handler-local work is bucket preparation, DTO mapping, and recommendation wording
- `GetEffortConcentrationRiskQueryHandler`
  - canonical concentration math lives behind `EffortDiagnosticsAnalyzer.AnalyzeConcentration(...)`
  - handler-local work is visibility filtering, top-item explanation, and mitigation wording

These seams are acceptable adapters because the formulas themselves are no longer duplicated in the handlers.

## Migration Opportunities

- **Adopt the CDC sprint fact result as the canonical sprint-total seam**
  - completed for: `GetSprintMetricsQueryHandler` and `GetSprintExecutionQueryHandler`
  - new seam: `ISprintFactService.BuildSprintFactResult(...)` returning `SprintFactResult`
  - follow-up consumer: keep the velocity-sampling path used by `GetEpicCompletionForecastQueryHandler` aligned to this seam
- **Promote portfolio summary rollups into CDC-backed outputs**
  - completed for: `GetPortfolioProgressTrendQueryHandler` and `GetPortfolioDeliveryQueryHandler`
  - new seams: `IPortfolioFlowSummaryService.BuildTrend(...)` and `IPortfolioDeliverySummaryService.BuildSummary(...)`
  - result: handlers now map `CompletionPercent`, `NetFlowStoryPoints`, trajectory, product delivery totals, and contribution shares from canonical outputs instead of re-aggregating projection rows
- **Adopt the EffortPlanning CDC seam for effort distribution, quality, and suggestions**
  - completed for: `GetEffortDistributionQueryHandler`, `GetEffortEstimationQualityQueryHandler`, and `GetEffortEstimationSuggestionsQueryHandler`
  - new seams: `IEffortDistributionService.Analyze(...)`, `IEffortEstimationQualityService.Analyze(...)`, and `IEffortEstimationSuggestionService.GenerateSuggestion(...)`
  - result: handlers now load/filter data, call the EffortPlanning CDC slice, and map canonical results instead of owning formulas
- **Collapse backlog-health compatibility wrappers when direct CDC contracts are available**
  - target consumers: `GetBacklogHealthQueryHandler` and `GetMultiIterationBacklogHealthQueryHandler`
  - expected simplification: replace wrapper-plus-heuristic composition with thinner mapping over direct BacklogQuality outputs

Net assessment:

- CDC adoption is strong for trend projections, forecasting services, effort diagnostics, and effort planning.
- The main remaining non-CDC paths in the audited scope are backlog-health compatibility wrappers and client-side roadmap scope replay.
- The highest-value remaining cleanup targets are backlog-health compatibility wrappers and client-side duplication; portfolio and effort-planning summary rollups are now CDC-backed.
