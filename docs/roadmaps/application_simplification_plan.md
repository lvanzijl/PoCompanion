# Application Simplification Plan

This document is a refactoring plan only. It is based on:

- `docs/audits/application_simplification_audit.md`
- `docs/audits/cdc_usage_coverage.md`
- `docs/domain/cdc_reference.md`

The goal is to simplify the application layer by turning analytics handlers into thin adapters around existing CDC slices.

## Refactor Groups

| Slice | Current handlers / consumers | Current status from audits | Simplification target |
| --- | --- | --- | --- |
| `BacklogQuality` | `GetBacklogHealthQueryHandler`, `GetMultiIterationBacklogHealthQueryHandler` | `legacy compatibility path` | Replace wrapper-plus-heuristic composition with direct mapping over BacklogQuality outputs when direct contracts are available. |
| `SprintCommitment` | `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler` | `CDC compliant` after `SprintFactResult` extraction | Keep the new CDC-backed sprint fact seam stable and route any remaining sprint-total consumers through `ISprintFactService`. |
| `DeliveryTrends` | `GetSprintTrendMetricsQueryHandler`, `GetPortfolioDeliveryQueryHandler`, `SprintTrendProjectionService` | mixed: trend handler already compliant; portfolio delivery still `CDC bypass` | Keep the valid projection seam and move portfolio delivery summaries / contribution rollups behind CDC-backed delivery outputs. |
| `Forecasting` | `GetEpicCompletionForecastQueryHandler`, `GetCapacityCalibrationQueryHandler`, `GetEffortDistributionTrendQueryHandler` | `CDC compliant` | Preserve the thin-adapter pattern and remove any remaining dependency on handler-rebuilt sprint totals. |
| `EffortDiagnostics` | `GetEffortImbalanceQueryHandler`, `GetEffortConcentrationRiskQueryHandler`, `GetEffortDistributionQueryHandler`, `GetEffortEstimationQualityQueryHandler`, `GetEffortEstimationSuggestionsQueryHandler` | mixed: two handlers are acceptable adapters; three remain local calculations | Keep the already-thin diagnostics handlers stable, and separately decide whether the non-CDC effort-planning formulas should move into a slice or remain explicitly outside this roadmap. |
| `PortfolioFlow` | `GetPortfolioProgressTrendQueryHandler`, `PortfolioFlowProjectionService` | `CDC bypass` in the handler; projection service is a valid adapter seam | Move completion, net-flow, remaining-scope change, and trajectory rollups behind CDC-backed portfolio outputs so the handler becomes read-and-map only. |

### Group notes

#### BacklogQuality

- Current path uses `IHierarchicalWorkItemValidator`, but the handlers still compute blocked-item counts, in-progress counts, grouped summaries, placeholder sprint windows, and trend directions locally.
- `docs/domain/cdc_reference.md` already assigns backlog validation findings, readiness scores, and implementation readiness to `BacklogQuality`.
- Migration priority is medium: the handlers are smaller than the sprint and portfolio consumers, but they still carry compatibility logic that should not keep growing.

#### SprintCommitment

- `docs/domain/cdc_reference.md` already assigns these outputs to `SprintCommitment`: `SprintCommitment`, `SprintScopeAdded`, `SprintScopeRemoved`, `SprintCompletion`, `SprintSpillover`, and derived story-point totals for commitment, added scope, removed scope, delivered scope, delivered-from-added scope, and spillover.
- `SprintFactResult` now provides the application-facing seam for those totals.
- The remaining goal is to keep future consumers aligned to that seam without reintroducing handler-level recomputation.

#### DeliveryTrends

- `GetSprintTrendMetricsQueryHandler` is the reference thin-adapter shape in this slice because it already reads projection rows and performs transport assembly over CDC-backed outputs.
- `GetPortfolioDeliveryQueryHandler` is still overscoped because it re-aggregates product delivery totals, effort shares, and feature contribution shares locally.
- `SprintTrendProjectionService` remains a valid materialization seam and should be kept.

#### Forecasting

- Forecasting already has the desired ownership split: forecast math in CDC services, handler work in orchestration and DTO mapping.
- The only migration concern is dependency cleanup around historical sprint totals, because `GetEpicCompletionForecastQueryHandler` currently depends on the current sprint-metrics path.
- This slice should be used as the template for the target handler shape.

#### EffortDiagnostics

- `GetEffortImbalanceQueryHandler` and `GetEffortConcentrationRiskQueryHandler` are already acceptable adapters because the formulas are delegated to `EffortDiagnosticsAnalyzer`.
- `GetEffortDistributionQueryHandler`, `GetEffortEstimationQualityQueryHandler`, and `GetEffortEstimationSuggestionsQueryHandler` remain local calculations; the coverage audit explicitly frames them as a separate migration decision.
- This plan should not silently pull those three handlers into the CDC without an explicit slice decision.

#### PortfolioFlow

- `GetPortfolioProgressTrendQueryHandler` reads canonical `PortfolioFlowProjectionEntity` rows but still computes `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, scope-change percentages, and `Trajectory` in handler code.
- `PortfolioFlowProjectionService` remains a valid seam and should continue to materialize the canonical projection rows.
- The cleanup target is application-level rollup code, not the projection service itself.

## Handler Migration Plan

### Target handler shape

Every analytics handler in scope should converge on this structure:

Handler  
→ Load required work-item data  
→ Call CDC slice  
→ Map canonical result to DTO  
→ Return response

Handlers must not:

- calculate analytics
- reconstruct sprint history
- compute rollups
- compute velocity or flow metrics

Valid handler responsibilities after migration:

- query composition
- loading current snapshots, history, or projection rows required by the CDC contract
- choosing the correct CDC slice call
- mapping canonical outputs onto existing DTOs or compatibility DTOs
- presentation-only sorting, grouping, or labeling that does not redefine the metric

### Incremental migration steps by group

#### BacklogQuality

Step 1: redirect calculation to the `BacklogQuality` slice by replacing wrapper-plus-local heuristics with direct consumption of canonical backlog validation and readiness outputs.  
Step 2: remove duplicated service logic in `GetBacklogHealthQueryHandler` and `GetMultiIterationBacklogHealthQueryHandler`, especially blocked-item counting, in-progress counting, grouped consequence summaries, and trend heuristics that duplicate slice meaning.  
Step 3: simplify DTO builders so handler output becomes a mapping layer over canonical findings and readiness results.  
Step 4: remove unused helper utilities and compatibility-only code paths once direct `BacklogQuality` contracts are available.

#### SprintCommitment

Step 1: keep `SprintFactResult` as the single application-facing result for committed, added, removed, delivered, delivered-from-added, spillover, and remaining totals.  
Step 2: route any future sprint-total consumer through `ISprintFactService` instead of adding handler-owned summation logic.  
Step 3: keep sprint metrics and sprint execution responses as mapping layers over canonical sprint facts.  
Step 4: prevent transport formulas from reappearing in DTOs, including `RemainingStoryPoints`.

#### DeliveryTrends

Step 1: redirect calculation to the `DeliveryTrends` slice by promoting portfolio delivery summaries and contribution rollups to canonical delivery outputs.  
Step 2: remove duplicated service logic from `GetPortfolioDeliveryQueryHandler` so it no longer recomputes product totals, delivered-effort shares, or feature contribution shares.  
Step 3: simplify DTO builders so portfolio delivery responses become mapping over CDC-backed delivery summaries, while `GetSprintTrendMetricsQueryHandler` remains the reference thin adapter for this slice.  
Step 4: remove unused helper utilities that only exist to support local delivery aggregation, but keep `SprintTrendProjectionService` and `DeliveryTrendProgressRollupMapper` as valid adapter / compatibility seams until DTO versioning changes are approved.

#### Forecasting

Step 1: keep forecast calculation in the `Forecasting` slice and redirect any remaining historical sprint-total dependency to CDC-backed sprint summary outputs instead of handler-rebuilt metrics.  
Step 2: remove duplicated service logic only where forecast consumers still rely on local sprint-metrics reconstruction inherited from upstream handlers.  
Step 3: simplify DTO builders so forecast handlers remain pure orchestration and mapping over canonical completion, calibration, and effort-trend outputs.  
Step 4: remove unused helper utilities that were only needed to compensate for non-canonical upstream sprint totals.

#### EffortDiagnostics

Step 1: preserve the current thin-adapter path for `GetEffortImbalanceQueryHandler` and `GetEffortConcentrationRiskQueryHandler`, and explicitly decide whether the three non-CDC effort-planning handlers should migrate into a dedicated slice or remain outside this roadmap.  
Step 2: remove duplicated service logic only for any effort-planning handlers that are explicitly pulled behind a CDC contract; do not mix that decision into the already-stable imbalance / concentration adapters.  
Step 3: simplify DTO builders for whichever effort handlers move behind a canonical contract, keeping recommendation text and visibility shaping outside the CDC.  
Step 4: remove unused helper utilities and local statistics wrappers only after the slice-boundary decision is made and the migrated handlers no longer depend on them.

#### PortfolioFlow

Step 1: redirect calculation to the `PortfolioFlow` slice by surfacing completion, net-flow, remaining-scope change, and trajectory rollups as canonical portfolio outputs.  
Step 2: remove duplicated service logic from `GetPortfolioProgressTrendQueryHandler`, especially `ComputeSummary`-style completion and net-flow rollups that sit on top of canonical projection rows.  
Step 3: simplify DTO builders so portfolio progress responses map canonical rollups directly from the CDC-backed result.  
Step 4: remove unused helper utilities that were only needed for handler-owned portfolio rollup arithmetic, while keeping `PortfolioFlowProjectionService` as the canonical projection materialization seam.

## Service Removal Candidates

### Redundant services or wrappers to remove when direct CDC contracts exist

- `PoTool.Client/Services/RoadmapAnalyticsService.cs`
  - Remove or reduce `ComputeLocalAnalytics` to pure presentation shaping once the API exposes CDC-backed roadmap scope totals.
- `IHierarchicalWorkItemValidator` compatibility path used by `GetBacklogHealthQueryHandler` and `GetMultiIterationBacklogHealthQueryHandler`
  - Collapse the wrapper once handlers can read direct `BacklogQuality` outputs.

### Duplicated calculators and handler-owned aggregations to remove

- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - Remove local summation of `CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `DeliveredFromAddedSP`, and `SpilloverSP`.
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - Remove handler-owned replay of sprint totals and local story-point summary logic.
- `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs`
  - Remove local calculation of `CompletionPercent`, `NetFlowStoryPoints`, cumulative net flow, scope deltas, and `Trajectory`.
- `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs`
  - Remove local product delivery totals, effort-share calculations, and feature contribution rollups.
- `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
  - Remove local blocked-item and in-progress heuristic calculations once canonical backlog outputs are mapped directly.
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
  - Remove local placeholder-slot shaping and trend-direction logic that exists only because the current path stops at the compatibility wrapper.

### Obsolete helper classes or DTO formulas to remove

- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
  - Remove the transport-level `RemainingStoryPoints` formula and map the field from canonical sprint facts instead.

### Explicit keep-list during migration

These components should stay in place during this simplification because the audits classify them as valid seams:

- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
- `PoTool.Api/Adapters/HistoricalSprintInputMapper.cs`
- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `PoTool.Api/Adapters/DeliveryTrendProgressRollupMapper.cs`

## Calculator Consolidation

The migration should consolidate calculation ownership into the existing CDC slices as follows:

| Calculation family | Canonical owner after migration | Current leakage to remove |
| --- | --- | --- |
| backlog validation findings, readiness, implementation readiness | `BacklogQuality` | blocked / in-progress heuristics and grouped health summaries in backlog handlers |
| committed, added, removed, delivered, spillover, remaining sprint totals | `SprintCommitment` | local sprint-total reconstruction in sprint metrics and sprint execution handlers, plus DTO-level remaining-scope formula leakage |
| product delivery summaries and contribution rollups | `DeliveryTrends` | portfolio delivery totals, effort shares, and feature contribution math in `GetPortfolioDeliveryQueryHandler` |
| forecast projection, calibration, trend forecasting | `Forecasting` | indirect dependence on handler-owned sprint totals rather than direct CDC-backed summaries |
| effort imbalance and concentration formulas | `EffortDiagnostics` | none for the two compliant handlers; keep outside-slice effort-planning formulas as a separate scope decision |
| stock, inflow, throughput, completion, net flow, trajectory | `PortfolioFlow` | progress-summary rollups in `GetPortfolioProgressTrendQueryHandler` |

## Expected Codebase Impact

Expected impact from the audits if this roadmap is executed incrementally:

- remove or centralize approximately 6 semantic hotspots covering sprint totals, remaining scope, net flow, completion percent, delivery-share math, and client-side roadmap scope replay
- reduce one client analytics service plus several heavy aggregation handlers to thin CDC adapters
- likely remove roughly 150–250 lines of calculation-heavy code across `GetSprintExecutionQueryHandler`, `GetSprintMetricsQueryHandler`, `GetPortfolioProgressTrendQueryHandler`, and `GetPortfolioDeliveryQueryHandler`
- improve consistency by making handlers consume the same canonical outputs already defined in `docs/domain/cdc_reference.md`
- lower regression risk by keeping valid adapter seams and moving only duplicated calculation logic

## Risk Notes

- **Contract sequencing risk**
  - Freeze the CDC result shapes before removing handler logic so DTO mappings can switch without metric drift.
- **Compatibility-path risk**
  - Backlog health currently depends on a legacy wrapper; collapse it only when direct `BacklogQuality` contracts are available.
- **Projection-boundary risk**
  - Keep `SprintTrendProjectionService` and `PortfolioFlowProjectionService` intact; the target is downstream handler rollups, not the projection materialization seam.
- **DTO compatibility risk**
  - Compatibility adapters such as `DeliveryTrendProgressRollupMapper` should remain until transport versioning decisions are made.
- **Client dependency risk**
  - `RoadmapAnalyticsService` should not be simplified until the API exposes the CDC-backed totals the client currently recomputes.
- **Scope-boundary risk**
  - The non-CDC effort-planning handlers (`GetEffortDistributionQueryHandler`, `GetEffortEstimationQualityQueryHandler`, `GetEffortEstimationSuggestionsQueryHandler`) require an explicit slice decision before migration work starts.
