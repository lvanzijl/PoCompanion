# Sprint Attribution Strategy Analysis

_Generated: 2026-03-13_

## 1 Introduction

This report audits how PoTool currently decides whether a work item, pull request, pipeline run, or derived metric should be attributed to a sprint.

The current codebase does **not** use a single sprint attribution model. Instead, it mixes:

- current `IterationPath` / resolved sprint membership
- activity-event timestamps derived from work item update ingestion
- artifact timestamps such as `CreatedDateUtc`, `FinishedDateUtc`, and `ClosedDate`
- hybrid combinations where commitment comes from `IterationPath` and activity comes from sprint windows

This document is analysis only. No business logic was changed as part of this task.

## 2 Current Strategies in Codebase

### Strategy A — IterationPath Attribution

The item is considered part of a sprint because its current `IterationPath` matches a sprint path, or because `ResolvedSprintId` was derived from that path.

Typical usage:

- sprint planning / commitment
- current sprint backlog membership
- capacity planning

### Strategy B — Revision / Activity Time Window Attribution

The item is considered active in a sprint because an ingested activity event happened inside the sprint date window.

Typical usage:

- sprint trends
- activity drill-down
- historical progress analysis

### Strategy C — Completion During Sprint

The item or artifact is considered part of a sprint because it completed or finished during the sprint window.

Typical usage:

- pipeline insights (`FinishedDateUtc`)
- some delivery-style metrics

### Strategy D — Hybrid Logic

The feature combines more than one rule, typically:

- `IterationPath` for planned / committed scope
- activity timestamps for work performed during the sprint
- completion transitions for delivered outcomes

This is currently the dominant strategy for delivery-oriented work item metrics.

## 3 Inventory of Implementations

| File | Method / Member | Strategy Used | Description |
|---|---|---|---|
| `PoTool.Api/Services/WorkItemResolutionService.cs` | `ResolveAllAsync()` | A — IterationPath Attribution | Resolves each work item's current `IterationPath` to `SprintEntity.Path` and stores the result in `ResolvedWorkItemEntity.ResolvedSprintId`. |
| `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` | `ResolvedSprintId` | A — IterationPath Attribution | Persisted sprint membership cache; explicitly documented as based on the current iteration path. |
| `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | `Handle()` | A — IterationPath Attribution | Computes sprint metrics by filtering current work items whose `IterationPath` equals the requested sprint path. Completion is evaluated from current state, not a historical transition timestamp. |
| `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs` | `Handle()` | A — IterationPath Attribution | Calculates planned sprint effort and capacity by grouping current work items on exact `IterationPath`. |
| `PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs` | `Handle()` / `AnalyzeSprintTrends()` | A — IterationPath Attribution | Analyzes effort per sprint by exact `IterationPath`. Sprint order is lexicographic, not date-based. |
| `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs` | `Handle()` / `CalculateIterationHealth()` | D — Hybrid Logic | Selects which sprint slots to show by sprint dates, but calculates each slot from current work items grouped by current `IterationPath`. |
| `PoTool.Core/Metrics/Services/SprintWindowSelector.cs` | `GetBacklogHealthWindow()` / `GetIssueComparisonWindow()` | B — Revision / Activity Time Window (supporting window selection) | Does not attribute work items directly, but chooses current / past / future sprint windows from sprint dates. |
| `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `Handle()` | D — Hybrid Logic | Uses current `IterationPath == sprint.Path` for current membership, activity ledger `System.IterationPath` changes for add/remove churn, and current done-state / `ClosedDate` for completion presentation. |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `ComputeProjectionsAsync()` / `ComputeProductSprintProjection()` | D — Hybrid Logic | Uses activity events inside sprint windows for worked/completed/bug metrics, while planned PBIs and bugs still come from `ResolvedSprintId == sprint.Id`. |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `Handle()` | D — Hybrid Logic | Returns cached or recomputed sprint trend projections; inherits the projection service's hybrid strategy. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `Handle()` | D — Hybrid Logic | Aggregates delivery from sprint projections and feature progress across the requested sprint range. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `Handle()` / `ComputeHistoricalScopeEffort()` | D — Hybrid Logic | Reconstructs historical scope with activity events and combines it with projection-based throughput / planned effort proxies. |
| `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` | `Handle()` | B — Revision / Activity Time Window | Returns activity rows for a work item tree inside a supplied period window using `ActivityEventLedgerEntries.EventTimestampUtc`. |
| `PoTool.Api/Handlers/PullRequests/GetPrSprintTrendsQueryHandler.cs` | `Handle()` | B — Revision / Activity Time Window | Maps PRs to sprints when `CreatedDateUtc` falls inside `[StartDateUtc, EndDateUtc)`. |
| `PoTool.Api/Handlers/PullRequests/GetPrDeliveryInsightsQueryHandler.cs` | `Handle()` | B — Revision / Activity Time Window | When a sprint is selected, converts sprint dates into a PR date range and filters by `CreatedDateUtc`. |
| `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs` | `Handle()` / `LoadRunsAsync()` | C — Completion During Sprint | Maps pipeline runs to a sprint when `FinishedDateUtc` falls inside `[SprintStartDateUtc, SprintEndDateUtc)`. |
| `PoTool.Api/Services/ActivityEventIngestionService.cs` | `IngestAsync()` | B — Revision / Activity Time Window (data source) | Ingests field-change events with timestamps and current hierarchy context; this is the main temporal data source for activity-based sprint logic. |
| `PoTool.Client/Pages/Home/PlanBoard.razor` | `HandleDropOnSprint(...)` call path via `UpdateIterationPathAsync()` | A — IterationPath Attribution | Planning board persists sprint assignment by writing `System.IterationPath`, so sprint membership here is explicitly commitment-based. |
| `PoTool.Client/Services/WorkItemService.cs` | `UpdateIterationPathAsync()` | A — IterationPath Attribution | Client entry point for changing sprint assignment. |
| `PoTool.Api/Controllers/WorkItemsController.cs` | `UpdateIterationPath(...)` | A — IterationPath Attribution | API endpoint for changing sprint assignment from planning features. |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs` | `UpdateWorkItemIterationPathAsync(...)` | A — IterationPath Attribution | TFS mutation that writes `/fields/System.IterationPath`. |

## 4 Strategy Classification

### Category A — IterationPath Attribution

Current members:

- `WorkItemResolutionService`
- `ResolvedWorkItemEntity.ResolvedSprintId`
- `GetSprintMetricsQueryHandler`
- `GetSprintCapacityPlanQueryHandler`
- `GetEffortDistributionTrendQueryHandler`
- Plan Board / iteration-path update flow

Semantics:

- best fit for sprint planning, sprint commitment, and current backlog assignment
- weak fit for historical "what happened during the sprint" questions

### Category B — Revision / Activity Time Window

Current members:

- `GetWorkItemActivityDetailsQueryHandler`
- `GetPrSprintTrendsQueryHandler`
- `GetPrDeliveryInsightsQueryHandler`
- `SprintWindowSelector` (window selection only)
- `ActivityEventIngestionService` as the supporting event source

Semantics:

- best fit for historical analytics and "what changed during the sprint"
- depends on accurate window boundaries and complete activity ingestion

### Category C — Completion During Sprint

Current members:

- `GetPipelineInsightsQueryHandler`

Semantics:

- best fit when the key business question is "what completed in this sprint"
- independent from `IterationPath`

### Category D — Hybrid Logic

Current members:

- `GetSprintExecutionQueryHandler`
- `SprintTrendProjectionService`
- `GetSprintTrendMetricsQueryHandler`
- `GetPortfolioDeliveryQueryHandler`
- `GetPortfolioProgressTrendQueryHandler`
- `GetMultiIterationBacklogHealthQueryHandler`

Semantics:

- appropriate when a feature mixes commitment, churn, and delivery questions
- highest risk of drift because different parts of the same page can mean different things

## 5 Feature Mapping

| Feature / Page | Strategy Used | Why |
|---|---|---|
| `/home/delivery` | None directly | Delivery hub only; routes users to downstream features that each apply their own attribution logic. |
| `/home/delivery/sprint` (Sprint Delivery) | D — Hybrid Logic | Uses sprint trend projections: planned scope comes from resolved sprint membership, while delivered / worked / bug metrics come from sprint-window activity. |
| `/home/delivery/sprint/activity/{id}` | B — Revision / Activity Time Window | Shows ledger events inside the selected sprint-period window for the selected work item tree. |
| `/home/delivery/execution` (Sprint Execution) | D — Hybrid Logic | Reconstructs sprint backlog evolution using current sprint membership plus `System.IterationPath` add/remove events and done-state completion. |
| `/home/portfolio-progress` | D — Hybrid Logic | Historical trend page uses sprint windows plus event replay to reconstruct scope and throughput across sprints. |
| `/home/pipeline-insights` | C — Completion During Sprint | Pipeline runs are attributed by `FinishedDateUtc`, which matches the question "what completed in this sprint". |
| `/home/pr-delivery-insights` | B — Revision / Activity Time Window | When a sprint is selected, PRs are scoped by sprint start/end dates and filtered by PR creation time. |
| PR Sprint trends API (`GetPrSprintTrendsQueryHandler`) | B — Revision / Activity Time Window | Explicitly maps PRs into sprint buckets by `CreatedDateUtc`. |
| Portfolio Delivery API (`GetPortfolioDeliveryQueryHandler`) | D — Hybrid Logic | Aggregates hybrid sprint projections across products and across the selected sprint range. |
| `/home/health` (Health workspace shell) | None directly | Current-state hub. It does not directly decide sprint membership. |
| Backlog Health Analysis / Issue Comparison (`GetMultiIterationBacklogHealthQueryHandler`) | D — Hybrid Logic | Uses date-based sprint slot selection, but iteration membership still comes from current `IterationPath` on current work item snapshots. |
| `/home/backlog-overview` | None directly | Product backlog readiness / refinement view; no sprint attribution logic in the page itself. |
| Validation triage / validation queue / validation fix | None directly | Current validation state; not sprint-scoped in the inspected code paths. |
| Bug triage / bug detail | None directly | Current bug state; not sprint-scoped in the inspected code paths. |
| `/home/planning` | None directly | Planning hub only. |
| `/planning/plan-board` | A — IterationPath Attribution | This is explicit sprint planning: moving work changes `System.IterationPath`, so the correct membership concept is sprint assignment. |
| `/planning/sprint-planning` | No implementation found | No route or page was found in the current repository. |
| Product Roadmaps | None directly | Roadmap sequencing, not sprint attribution. |
| `/home/changes` | None directly (sync window, not sprint window) | Uses sync-to-sync time windows rather than sprint attribution. |

## 6 Recommended Strategy Per Feature

| Feature | Recommended Strategy | Rationale |
|---|---|---|
| Sprint planning / Plan Board | IterationPath Attribution | Planning is about assigning work to a sprint, so current `IterationPath` is the correct source of truth. |
| Sprint commitment / capacity planning | IterationPath Attribution | Capacity and commitment should reflect what is assigned to the sprint, not just what changed during it. |
| Sprint execution diagnostics | Hybrid Logic | Execution needs both commitment (`IterationPath`) and churn / activity inside the sprint window. |
| Sprint delivery stakeholder report | Hybrid Logic, weighted toward completion/activity | Stakeholders care about what was delivered during the sprint, but comparing delivery to commitment still needs planned-scope membership. |
| Portfolio delivery snapshot | Hybrid Logic | Delivery snapshots should keep commitment vs delivery distinction, but completed outcomes should remain time-window based. |
| Historical sprint trends | Revision / Activity Time Window | Trend analysis should answer what happened during the sprint, not what items happen to be assigned there now. |
| Portfolio progress trend | Revision / Activity Time Window with explicit stock reconstruction | Historical stock-and-flow metrics need event-based reconstruction to avoid present-state distortion. |
| PR sprint trends | Revision / Activity Time Window | PR trend pages are about development activity in the sprint window, not work-item iteration assignment. |
| PR delivery insights | Revision / Activity Time Window | A sprint filter should continue to mean "PRs active in that sprint period", though the precise anchor (`CreatedDateUtc`) remains a product decision. |
| Pipeline insights | Completion During Sprint | Pipeline health is naturally completion-oriented; `FinishedDateUtc` is the right anchor. |
| Backlog health across iterations | IterationPath Attribution + date-based sprint selection | The feature is evaluating backlog quality of sprint-assigned work, but sprint ordering should remain date-based. Historical snapshots would be a future enhancement. |
| Validation / bug triage / backlog overview | No sprint attribution unless explicitly introduced | These pages are currently current-state workflows; adding sprint semantics would change their meaning. |

## 7 Identified Inconsistencies

| Feature | Current Strategy | Recommended Strategy | Problem |
|---|---|---|---|
| `GetEffortDistributionTrendQueryHandler` | IterationPath + lexicographic path ordering | IterationPath with sprint-date ordering | Sprint order is determined by string sort, which can misorder custom sprint names. |
| `GetMultiIterationBacklogHealthQueryHandler` | Date-based sprint selection + current `IterationPath` membership | Keep date-based selection, but consider historical membership if the page is meant to describe past sprints | Past sprint rows are built from today's work item snapshots, so moved items can distort history. |
| `GetSprintExecutionQueryHandler` | Hybrid, but completion order uses current `ClosedDate` | Hybrid with completion transition timestamps | The page reconstructs sprint churn from activity events, but completion order is sorted by current `ClosedDate` instead of the actual transition event time. |
| `GetSprintExecutionQueryHandler` vs `SprintTrendProjectionService` | Mixed inclusive end-boundary handling | Consistent half-open sprint windows | Both use `<= sprintEnd` for activity windows, while PR and pipeline handlers mostly use `[start, end)`. |
| `GetPrDeliveryInsightsQueryHandler` | Sprint filter uses `CreatedDateUtc >= from && <= to` | Consistent half-open sprint windows | Uses an inclusive end boundary while `GetPrSprintTrendsQueryHandler` and `GetPipelineInsightsQueryHandler` use `< end`. |
| `GetSprintMetricsQueryHandler` | Current IterationPath + current done state | IterationPath is fine only for commitment metrics | The name reads like a historical sprint metric, but the implementation is a present-state snapshot of current sprint assignment. |
| `GetSprintCapacityPlanQueryHandler` | Current IterationPath only | IterationPath | The strategy is logically correct for planning, but the handler leaves `StartDate` / `EndDate` null and can be mistaken for a historical analysis API. |
| `SprintTrendProjectionService` | Planned from `ResolvedSprintId`, delivered from activity events | Hybrid logic | The page mixes "assigned to sprint" and "changed during sprint" in one DTO, which is powerful but easy to misread if the labels are not explicit. |
| `GetPortfolioProgressTrendQueryHandler` | Added effort proxied from planned effort | Event-based inflow when available | The handler documents that `AddedEffort` is only a proxy, so backlog inflow is not yet a pure time-window measurement. |

## 8 Future Refactoring Opportunities

High-level only; no implementation is proposed in this task.

1. **Standardize sprint window boundaries**
   - Choose a single convention, preferably half-open `[start, end)`.
   - Apply it consistently across work item, PR, and pipeline features.

2. **Separate commitment metrics from activity metrics in DTO naming**
   - Pages such as Sprint Delivery and Sprint Trends currently mix both concepts.
   - Explicit naming would reduce user confusion and code drift.

3. **Introduce historical sprint-membership reconstruction where needed**
   - Backlog-health-style history currently depends on current `IterationPath`.
   - Activity / revision history could reconstruct past assignment snapshots if that becomes a product requirement.

4. **Move legacy iteration ordering off string sort**
   - Effort distribution should use `SprintEntity` ordering instead of lexicographic path ordering.

5. **Unify completion semantics**
   - Decide whether "completed in sprint" is defined by state transition timestamp, `ClosedDate`, or feature-specific rules.
   - Use one canonical rule per feature family.

6. **Document feature intent next to each metric API**
   - Several handlers are logically correct only if the page is understood as commitment-oriented, activity-oriented, or completion-oriented.
   - Short intent comments would make future refactors safer.

## Appendix — Existing Related Documentation

The following existing documents already discuss parts of sprint attribution and were useful for this audit:

- `docs/reports/sprint-trends-current-state-analysis.md`
- `docs/reports/sprint-trends-vs-revisions-report.md`
- `docs/revisions/REVISION_PIPELINE_INVENTORY.md`
- `docs/revision-window-backfill.md`
- `docs/sprint-scoping-limitations.md`
- `docs/sprintmetrics_iteration_migration_plan.md`
- `docs/iteration_path_sorting_audit.md`
