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
| `GetPortfolioProgressTrendQueryHandler` | `PoToolDbContext` | none | Stock, remaining scope, inflow, and throughput come from `PortfolioFlowProjectionEntity`, but `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, scope-change percentages, and `Trajectory` are recomputed locally in `Handle(...)` and `ComputeSummary(...)`. | `CDC bypass` |
| `GetPortfolioDeliveryQueryHandler` | `PoToolDbContext`, `SprintTrendProjectionService` | none | Product rows are built from `SprintMetricsProjectionEntity`, and feature progress comes from `SprintTrendProjectionService.ComputeFeatureProgressAsync(...)`, but summary totals, product effort shares, and top-feature contribution shares are re-aggregated in the handler. | `CDC bypass` |

### Effort diagnostics handlers

| Handler | Service dependencies | Calculator dependencies | Calculation origin | Classification |
| --- | --- | --- | --- | --- |
| `GetEffortImbalanceQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `EffortDiagnosticsAnalyzer` | Imbalance formulas are delegated to `Analyzer.AnalyzeImbalance(...)`, which now bridges into the canonical effort-diagnostics rules and statistics. The handler still owns grouping, filtering, and recommendation text. | `unavoidable adapter logic` |
| `GetEffortConcentrationRiskQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | `EffortDiagnosticsAnalyzer` | Concentration formulas are delegated to `Analyzer.AnalyzeConcentration(...)`, with local responsibility limited to visibility filtering, top-item lists, and mitigation text. | `unavoidable adapter logic` |
| `GetEffortDistributionQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator` | none | The handler computes area totals, iteration totals, utilization percentages, and heat-map cells locally through `CalculateEffortByAreaPath`, `CalculateEffortByIteration`, and `CalculateHeatMapCells`. | `CDC bypass` |
| `GetEffortEstimationQualityQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator`, `IWorkItemStateClassificationService` | `StatisticsMath` | Effort-quality scores are derived locally through variance and coefficient-of-variation calculations in `CalculateQualityByType`, `CalculateTrendOverTime`, and `CalculateOverallAccuracy`. | `CDC bypass` |
| `GetEffortEstimationSuggestionsQueryHandler` | `IWorkItemRepository`, `IProductRepository`, `IMediator`, `IWorkItemStateClassificationService` | `StatisticsMath` | Similarity scoring, median estimation, confidence heuristics, and rationale generation all run locally in `GenerateSuggestion`, `CalculateSimilarity`, `CalculateMedian`, and `CalculateConfidence`. | `CDC bypass` |

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

### Portfolio progress rollups

- `GetPortfolioProgressTrendQueryHandler`
  - consumes canonical `PortfolioFlowProjectionEntity` rows
  - still computes `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, total scope change, remaining-scope change, and `Trajectory` in application code
  - bypass type: local stock / inflow / throughput rollups on top of CDC projections
- `GetPortfolioDeliveryQueryHandler`
  - consumes `SprintMetricsProjectionEntity` rows and feature progress outputs
  - still computes product totals, delivery shares, and feature contribution shares locally
  - bypass type: local delivery-summary aggregation on top of DeliveryTrends projections

### Non-CDC effort calculations still living in handlers

- `GetEffortDistributionQueryHandler`
  - local effort heat-map and utilization arithmetic
- `GetEffortEstimationQualityQueryHandler`
  - local variance, coefficient-of-variation, and weighted-accuracy calculations
- `GetEffortEstimationSuggestionsQueryHandler`
  - local similarity heuristics, medians, and confidence scoring

These three handlers do not currently consume CDC slice outputs for their formulas. They remain local analytics paths.

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
  - target consumers: `GetPortfolioProgressTrendQueryHandler` and `GetPortfolioDeliveryQueryHandler`
  - expected simplification: map `CompletionPercent`, `NetFlowStoryPoints`, trajectory, product delivery totals, and contribution shares directly from canonical outputs instead of re-aggregating projection rows
- **Decide whether effort distribution / quality / suggestion analytics belong in the CDC**
  - current status: `GetEffortDistributionQueryHandler`, `GetEffortEstimationQualityQueryHandler`, and `GetEffortEstimationSuggestionsQueryHandler` remain local calculations
  - migration option: either leave them explicitly outside the CDC or extract a new CDC-backed effort-planning slice so those formulas stop drifting in handlers
- **Collapse backlog-health compatibility wrappers when direct CDC contracts are available**
  - target consumers: `GetBacklogHealthQueryHandler` and `GetMultiIterationBacklogHealthQueryHandler`
  - expected simplification: replace wrapper-plus-heuristic composition with thinner mapping over direct BacklogQuality outputs

Net assessment:

- CDC adoption is strong for trend projections, forecasting services, and effort-diagnostics formulas.
- The main remaining non-CDC paths are not raw helper lookups anymore; they are handler-level rollups layered on top of CDC facts.
- The highest-value cleanup targets are sprint totals and portfolio summaries, because those handlers already have the right CDC inputs but still rebuild the derived outputs locally.
