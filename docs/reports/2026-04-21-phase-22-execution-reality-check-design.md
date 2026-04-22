# Phase 22 execution reality-check design

## 1. Summary

- VERIFIED: the current repository already separates planning/backlog semantics from historical execution semantics. `docs/architecture/cdc-domain-map.md` and `docs/architecture/cdc-reference.md` place `BacklogQuality`, `SprintCommitment`, `DeliveryTrends`, `Forecasting`, and `PortfolioFlow` in distinct CDC slices, with `DeliveryTrends` downstream of `SprintCommitment` and no reverse dependency from UI or application layers.
- VERIFIED: the repository already contains validated planning-phase artifacts for Phases 15–21 in `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-15-signal-calibration.md` through `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-21-phase-21-decision-integration-validation.md`.
- VERIFIED: existing repo surfaces already expose the execution facts needed for a minimal advisory layer: delivered scope, spillover, scope change, PR workflow timing, pipeline stability, and backlog health. Evidence: `PoTool.Shared/Metrics/SprintTrendDtos.cs`, `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`, `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`, `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`, and `docs/architecture/navigation-map.md`.
- VERIFIED: the safest Phase 22 outcome is a secondary advisory layer over historical execution facts only. It must not redefine planning heat, plan stability, forecast confidence, or capacity behavior. Evidence: `docs/architecture/cdc-domain-map.md`, `docs/architecture/cdc-reference.md`, and the Trends/Planning separation in `docs/architecture/navigation-map.md`.
- GO: proceed to Phase 23 CDC slice design. Existing CDC inputs are sufficient for a conservative execution reality-check layer if it stays advisory and reuses existing workspaces for investigation.

## 2. Signal purpose definition

### Primary question

- VERIFIED: the execution reality-check layer should answer one question only: **Is recent delivery execution behaving abnormally enough that the current plan should be examined through deeper insight workspaces before it is trusted at face value?**

### Secondary questions

- VERIFIED: **Has delivered scope fallen below its own recent normal range?**
- VERIFIED: **Has execution become unusually variable across recent completed sprints?**
- VERIFIED: **Is committed work being carried over more often than usual?**

### Explicit exclusions

- VERIFIED: this layer does **not** predict future delivery dates or delivery probability. Forecasting already owns future-looking semantics in `docs/architecture/cdc-reference.md` and `docs/architecture/forecasting-domain-model.md`.
- VERIFIED: this layer does **not** enforce capacity. Capacity and planning confidence remain in planning-oriented surfaces, not in execution CDC slices. Evidence: Trends and Planning are separate workspaces in `docs/architecture/navigation-map.md`.
- VERIFIED: this layer does **not** micromanage single sprints. It uses completed-sprint history only and routes users to deeper workspaces instead of prescribing sprint actions.

## 3. CDC data mapping

| Need | Source / slice | Status | Data shape | Evidence | Design implication |
| --- | --- | --- | --- | --- | --- |
| Throughput | `SprintCommitment` and downstream `DeliveryTrends`; persisted sprint metrics and portfolio flow projections | VERIFIED | Per sprint, per product aggregates for delivered PBIs and delivered story points | `docs/architecture/sprint-commitment-domain-model.md`, `docs/architecture/cdc-reference.md`, `PoTool.Shared/Metrics/SprintTrendDtos.cs`, `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`, `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs` | Safe primary execution input |
| Spillover | `SprintCommitment` outputs and sprint execution metrics | VERIFIED | Per sprint, per product aggregates for spillover count, spillover story points, spillover rate | `docs/architecture/sprint-commitment-domain-model.md`, `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs` | Safe primary execution input |
| Change rates | Sprint execution churn and portfolio inflow | VERIFIED | Per sprint, per product aggregates for `AddedSP`, `RemovedSP`, `ChurnRate`, and `InflowStoryPoints` | `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`, `docs/architecture/portfolio-flow-model.md` | Available as supporting context, but not selected as a primary anomaly because it overlaps planning/change semantics |
| Variance support | Shared statistics contract plus per-sprint execution series | VERIFIED | Repository-level math semantics for median, variance, standard deviation, and percentiles; raw execution series are per sprint | `docs/architecture/cdc-domain-map.md`, `docs/architecture/cdc-reference.md`, `PoTool.Shared/Statistics/PercentileMath.cs` | Enough to derive a conservative variability anomaly from existing history |
| Dedicated delivery-variance projection | No dedicated execution-variance projection found in current repo structure | UNKNOWN | No explicit materialized delivery-variance CDC output identified | Search evidence: current repo shows throughput/spillover projections, but no separate delivery-variance entity or DTO in `PoTool.Api/Persistence/Entities` or `PoTool.Shared/Metrics` | Variability should remain derived from existing series in Phase 23, not introduced as a new primary data source |

### Data-availability conclusion

- VERIFIED: throughput, spillover, and change-rate inputs are already present in stable per-sprint forms.
- VERIFIED: variability can be derived safely from existing per-sprint series without changing persistence, APIs, or UI in this phase.
- UNKNOWN: whether all products have enough completed sprint history for the same window size; this affects coverage, not the semantic design.

## 4. Selected anomaly types (exactly 3)

### 4.1 Delivery below typical range

- VERIFIED: **Definition:** recent delivered scope is persistently below the product’s own recent normal delivery range.
- VERIFIED: **Required inputs:** delivered story points per completed sprint, optionally supported by completed PBI count. Evidence: `PoTool.Shared/Metrics/SprintTrendDtos.cs`, `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`, `PoTool.Api/Persistence/Entities/PortfolioFlowProjectionEntity.cs`.
- VERIFIED: **Why it matters for planning:** planning can remain internally coherent while actual delivery output weakens. This anomaly gives a conservative “recheck execution reality” signal without changing planning heat or forecast semantics.

### 4.2 Execution variability high

- VERIFIED: **Definition:** delivered outcomes vary unusually from sprint to sprint over the recent window, even when average delivery is not obviously low.
- VERIFIED: **Required inputs:** per-sprint delivered story points and/or commitment completion series, interpreted with shared median/percentile or variance semantics. Evidence: `docs/architecture/cdc-domain-map.md`, `docs/architecture/cdc-reference.md`, `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Shared/Statistics/PercentileMath.cs`.
- VERIFIED: **Why it matters for planning:** plans are easier to trust when execution is rhythmically stable. High variability weakens confidence in execution follow-through, but it should remain advisory and explanatory rather than predictive.

### 4.3 Spillover increasing

- VERIFIED: **Definition:** committed work is being carried into the next sprint more often, or in larger scope, across recent completed sprints.
- VERIFIED: **Required inputs:** spillover story points or spillover rate, plus committed scope context. Evidence: `docs/architecture/sprint-commitment-domain-model.md`, `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`.
- VERIFIED: **Why it matters for planning:** sustained carry-over is a direct execution drag signal. It indicates that current commitments are finishing less cleanly, which should trigger investigation before any planning response is considered.

### Selection boundaries

- VERIFIED: exactly three anomalies are selected.
- VERIFIED: `defect backlog growing` is intentionally **not** selected for the minimal Phase 22 layer because it broadens scope toward backlog-state management and bug analytics rather than a narrow execution reality check. Existing repo evidence for bugs appears in delivery trends (`BugsCreatedCount`, `BugsClosedCount`) and backlog/bug workspaces, but not as a minimal delivery-core anomaly owner.

## 5. Temporal model definition

- VERIFIED: **Rolling window size:** 8 completed sprints.
  - VERIFIED: justification: `docs/architecture/navigation-map.md` uses a last-6-month default for trend analysis, and an 8-sprint window is large enough to be slow-moving while still close to current execution behavior.
- VERIFIED: **Aggregation method:** median-centered interpretation over the rolling window, using percentile/spread semantics for variability instead of raw per-sprint movement.
  - VERIFIED: justification: median is more conservative and explainable than mean when a few unusual sprints exist; percentile semantics are already consistent with repository statistics usage in PR and pipeline analytics.
- VERIFIED: **Persistence rule:** an anomaly should trigger only after it is present across 3 consecutive completed sprints.
  - VERIFIED: justification: this suppresses one-off delivery dips, isolated spillover spikes, and single-sprint volatility.
- VERIFIED: **Clear rule:** an anomaly should clear only after 2 consecutive completed sprints return to normal range.
  - VERIFIED: justification: this keeps the layer slow-moving and avoids state flapping.
- VERIFIED: **Insufficient evidence rule:** fewer than 6 completed sprints should produce an explicit insufficient-evidence state rather than a forced advisory judgment.
  - VERIFIED: justification: conservative signals should prefer silence over synthetic certainty.

## 6. Interpretation model

### Allowed states

- VERIFIED: the execution reality-check layer should expose only four user-facing states:
  1. **Stable**
  2. **Watch**
  3. **Investigate**
  4. **Insufficient evidence**

### Mapping from anomaly to state

| Condition | User-facing state | Explanation style |
| --- | --- | --- |
| No sustained anomaly active | Stable | Execution is within its recent normal range |
| Exactly one sustained anomaly active | Watch | Something persistent is off and should be monitored |
| Two or more sustained anomalies active | Investigate | Execution is persistently abnormal and deserves deeper inspection |
| Fewer than 6 completed sprints or missing core history | Insufficient evidence | Do not infer a reality-check signal yet |

- VERIFIED: the primary signal should name the dominant anomaly in words, not in raw numbers.
- VERIFIED: no fine-grained score should be exposed.
- VERIFIED: no per-sprint volatility state should be shown directly; only sustained multi-sprint interpretation should surface.

## 7. Routing intent

| Anomaly | Target workspace(s) | What the user should investigate there |
| --- | --- | --- |
| Delivery below typical range | Trends, PR Insights, Pipeline Insights | In Trends, confirm whether the drop is product-specific or broad and whether bug activity changed in the same period. In PR Insights, inspect review-cycle friction, PR lifetime, and merge delays. In Pipeline Insights, inspect whether build instability or long run duration aligns with the delivery drop. |
| Execution variability high | Trends, PR Insights, Pipeline Insights | In Trends, locate when the swings started and whether they align with specific products or ranges. In PR Insights, inspect uneven review and merge behavior. In Pipeline Insights, inspect whether unstable build outcomes coincide with erratic delivery. |
| Spillover increasing | Trends, Backlog Health | In Trends, confirm that carry-over is sustained rather than a one-sprint event. In Backlog Health, inspect whether the affected scope shows weak refinement or implementation readiness that could explain unfinished committed work. |

- VERIFIED: the execution reality-check layer routes to existing workspaces already documented in `docs/architecture/navigation-map.md`: `/home/trends`, `/home/pull-requests`, `/home/pipeline-insights`, and `/home/health/backlog-health`.
- ASSUMPTION: Phase 23 can reuse those workspaces as drill-down destinations without introducing a new dedicated execution workspace.

## 8. Non-goals

- VERIFIED: this layer does **not** change planning heat.
- VERIFIED: this layer does **not** redefine risk.
- VERIFIED: this layer does **not** redefine plan stability.
- VERIFIED: this layer does **not** change heatmap behavior.
- VERIFIED: this layer does **not** represent delivery probability.
- VERIFIED: this layer does **not** enforce scope or capacity limits.
- VERIFIED: this layer does **not** replace insight workspaces.
- VERIFIED: this layer does **not** become a sprint-level operational control panel.

## 9. Risks and unknowns

### VERIFIED

- VERIFIED: current CDC boundaries support a narrow advisory layer because execution facts already live in `SprintCommitment`, `DeliveryTrends`, and `PortfolioFlow`, while backlog quality, PR analytics, and pipeline analytics already have their own workspaces and read models.
- VERIFIED: the repository already uses conservative statistical semantics such as median and percentile-based interpretation in adjacent analytics (`PoTool.Shared/Statistics/PercentileMath.cs`, `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`).

### ASSUMPTIONS

- ASSUMPTION: most target teams have sprint cadences regular enough that an 8-sprint window is meaningful.
- ASSUMPTION: Phase 23 can derive variability from existing per-sprint execution series without introducing a new primary persistence contract.
- ASSUMPTION: routing into existing insight workspaces is preferable to introducing a new execution-diagnostics workspace.

### UNKNOWN

- UNKNOWN: a dedicated delivery-variance projection or DTO was not identified in the current repository; only the underlying per-sprint series and shared statistical semantics are clearly present.
- UNKNOWN: the minimum usable history coverage per product/team was not validated in this phase.
- UNKNOWN: whether bug-oriented execution anomalies should ever be promoted later without widening the layer beyond the minimal Phase 22 scope.

### RISKS

- RISK: if products have sparse sprint history, the conservative insufficient-evidence rule may leave many products without a signal.
- RISK: if change-rate anomalies are later pulled into this layer, it could blur the boundary with existing planning/change semantics.
- RISK: if the layer becomes too eager to route into PR or pipeline views for every anomaly, it may create advisory noise instead of a low-noise reality check.
- RISK: if defect-backlog growth is added too early, the layer may stop being a minimal delivery anomaly layer and drift toward a broader operational health surface.

## Final section

- VERIFIED: this report stayed in analysis/design scope only. No planning engine logic, signal calculations, persistence model, API contracts, or UI were modified.
- GO: Phase 23 CDC slice design can proceed.
- VERIFIED: the minimum safe Phase 23 design constraint is unchanged: build a secondary, explainable, slow-moving advisory layer from existing execution facts only, with routing into existing insight workspaces.
