# Planning Projection State Audit

## Summary
- projection implemented: partial
- multi-product planning: no
- UI support: partial

## Findings

### 1. Projection implementation actually present

#### Epic forecast DTO and handler exist
- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`
  - Class: `EpicCompletionForecastDto`
  - Actual behavior: shared read DTO for epic/feature completion forecasting. It carries `SprintsRemaining`, `EstimatedCompletionDate`, `Confidence`, and `ForecastByDate`.
- `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
  - Class: `CompletionForecastService`
  - Actual behavior: computes epic/feature completion forecasts from total/completed story-point scope plus historical sprint velocity. It:
    - uses average completed story points across sampled sprints as velocity
    - computes `SprintsRemaining = ceil(remaining / velocity)`
    - derives `EstimatedCompletionDate` from the last sprint end date plus fixed 14-day sprint cadence
    - builds synthetic future sprint buckets up to 20 sprints
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - Class: `GetEpicCompletionForecastQueryHandler`
  - Actual behavior: loads an epic/feature, rolls up canonical scope from hierarchy, derives sprint metrics from distinct iteration paths, calls `CompletionForecastService`, and returns `EpicCompletionForecastDto`.
- `PoTool.Api/Controllers/MetricsController.cs`
  - Endpoint: `GET /api/Metrics/epic-forecast/{epicId}`
  - Actual behavior: exposes epic/feature forecast only. No product-scoped or project-scoped projection endpoint is declared here.

#### Project planning summary exists, but it is not a timeline projection
- `PoTool.Shared/Planning/ProjectPlanningSummaryDtos.cs`
  - Classes: `ProjectPlanningSummaryDto`, `ProjectPlanningProductSummaryDto`
  - Actual behavior: count/effort/capacity summary DTOs only. No projected start date, projected end date, delay state, or timeline structure.
- `PoTool.Api/Services/ProjectPlanningSummaryService.cs`
  - Class: `ProjectPlanningSummaryService`
  - Actual behavior: builds a read-only project summary by:
    - loading project products and backlog roots
    - counting roadmap epics / PBIs
    - summing `Effort` for total and planned effort
    - calibrating `CapacityPerSprint` from historical `SprintMetricsProjectionEntity` rows per product
    - rolling project capacity as `SUM(product capacityPerSprint)`
  - Important: it does not calculate projected start/end dates or effort-capacity timelines for project planning items.
- `PoTool.Api/Controllers/ProjectsController.cs`
  - Endpoint: `GET /api/projects/{alias}/planning-summary`
  - Actual behavior: returns the read-only project summary above; no projection payload.

#### Endpoints that do not exist
- No `EpicProjectionDto` was found.
- No endpoint like `/api/products/{productId}/epic-projections` was found.
- No endpoint like `/api/projects/{alias}/...projection...` was found for planning projections.

### 2. Planning UI changes actually present

#### Plan Board
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - Actually rendered:
    - single-product plan board with sprint columns
    - sprint date headers for real sprint windows
    - per-sprint capacity usage (`Capacity`, `Assigned`, `Remaining`, `Over by`)
    - project summary bar when a project is selected
  - Missing:
    - no epic timeline bars
    - no projected start/end dates
    - no projected completion dates
    - no delay indicator tied to a forecasted schedule

#### Product Roadmaps
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - Actually rendered:
    - multiple product lanes
    - per-epic cards with delivered/remaining story-point progress
    - `~N sprints` chip when forecast data loads
    - warning text such as `Epic exceeds velocity`, `Has dependencies`, `Refinement blockers`, `Validation issues`
    - project roadmap summary card above lanes
  - Missing:
    - no timeline bars
    - no projected start date
    - no projected end date
    - no estimated completion date shown
    - no explicit delay indicator relative to a plan baseline

#### Project overview
- `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`
  - Actually rendered:
    - project alias
    - summary cards for epics, PBIs, effort, and `Capacity / sprint`
    - risk chips for product imbalance, planning gap, and overcommit
    - per-product distribution table
  - Missing:
    - no timeline visualization
    - no projected dates
    - no forecasted completion view
    - no delay indicators

### 3. Multi-product planning verification

- No page was found that aggregates time-based projections across multiple products.
- `ProductRoadmaps.razor` does show multiple products together, but as separate product lanes with roadmap cards, not as timeline projections.
- `ProjectPlanningOverview.razor` aggregates counts, effort, and capacity, but not forecast timelines.
- `PlanBoard.razor` remains a single selected product board even when entered from a project-scoped route.

### 4. Data-flow verification

#### Where effort is read
- `PoTool.Api/Services/ProjectPlanningSummaryService.cs`
  - reads `workItem.Effort ?? 0` for `TotalEffort` and `PlannedEffort`
- `PoTool.Client/Services/RoadmapAnalyticsService.cs`
  - computes local epic delivered/remaining values from `wi.Effort ?? 0` on PBIs/Bugs

#### Where capacity is calculated
- `PoTool.Api/Services/ProjectPlanningSummaryService.cs`
  - derives product `CapacityPerSprint` via `VelocityCalibrationService` using historical `SprintMetricsProjectionEntity` rows
  - then sums product capacities at project level
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - loads product capacity calibration from `GetCapacityCalibrationEnvelopeAsync`
  - uses that single product median velocity to show sprint utilization

#### Where effort and capacity are combined into time-based projections
- Epic/feature forecasting exists only in the forecast path:
  - `GetEpicCompletionForecastQueryHandler`
  - `CompletionForecastService`
- Project planning pages do not combine project effort and project capacity into projected dates or timeline outputs.
- `ProjectPlanningSummaryService` combines effort and capacity only for overcommit-style advisory comparison, not for time-based projection.

### 5. Warnings vs projections

- Warnings exist without projections:
  - `PlanBoard.razor`: overcommitted sprint warning and project overcommit/planning-gap/imbalance warnings
  - `ProjectPlanningOverview.razor`: risk chips only
- Projections are partially implemented but only lightly surfaced:
  - `ProductRoadmaps.razor` loads epic forecast data but renders only `SprintsRemaining`, `ExceedsVelocity`, and `Confidence`
  - `EpicCompletionForecastDto.EstimatedCompletionDate` and `ForecastByDate` are not used in the planning pages
- Legacy/isolated UI exists:
  - `PoTool.Client/Components/Forecast/ForecastPanel.razor` renders estimated completion date
  - `PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor` hosts `ForecastPanel` and `TimelinePanel`
  - This is not wired into Plan Board, Product Roadmaps, or Project overview

## Gaps

- No epic projection DTO dedicated to planning pages
- No product-level projection endpoint
- No project-level projection endpoint
- No projected start/end dates in planning UI
- No timeline bars for roadmap forecasting
- No delay indicators backed by forecasted schedule data
- No multi-product projection aggregation
- Project planning summary still uses aggregate capacity summary rather than time-based planning projection

## Broken or misleading signals

- `PoTool.Api/Services/ProjectPlanningSummaryService.cs` says it builds summaries from project, sprint, and projection data, but the result is still a capacity/effort summary, not a project planning projection.
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor` surfaces forecast-driven warning signals (`~N sprints`, `Epic exceeds velocity`) without surfacing the forecast date or forecast timeline that produced them.
- `PoTool.Client/Components/Forecast/ForecastPanel.razor` proves estimated completion dates exist in the system, but that UI is isolated in legacy analysis screens rather than the planning pages under audit.
- `docs/analysis/2026-04-02-project-planning-awareness.md` exists and accurately describes counts/effort/capacity summary behavior; it does not document a completed planning projection feature.
- No projection-specific project/product planning report was found beyond the existing planning-awareness report.

## Next safe step

Define a single read-only projection contract for planning pages first, then wire one planning surface to that contract before adding any multi-product rollup.
