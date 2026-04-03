# Phase 1 — Observe

## Pages instrumented

Passive reporting is now driven by the central `GlobalFilterStore` through route-aware observation in `MainLayout` and shared `WorkspaceBase` context parsing.

Instrumented pages:

- `HomePage` (`/home`)
- `HealthWorkspace` (`/home/health`)
- `HealthOverviewPage` (`/home/health/overview`)
- `BacklogOverviewPage` (`/home/health/backlog-health`, `/home/backlog-overview`)
- `HomeChanges` (`/home/changes`)
- `DeliveryWorkspace` (`/home/delivery`)
- `PortfolioDelivery` (`/home/delivery/portfolio`)
- `SprintExecution` (`/home/delivery/execution`)
- `SprintTrend` (`/home/delivery/sprint`, `/home/sprint-trend`)
- `SprintTrendActivity` (`/home/delivery/sprint/activity/{WorkItemId:int}`, `/home/sprint-trend/activity/{WorkItemId:int}`)
- `TrendsWorkspace` (`/home/trends`)
- `DeliveryTrends` (`/home/trends/delivery`)
- `PortfolioProgressPage` (`/home/portfolio-progress`)
- `PipelineInsights` (`/home/pipeline-insights`)
- `PrOverview` (`/home/pull-requests`)
- `PrDeliveryInsights` (`/home/pr-delivery-insights`)
- `BugOverview` (`/home/bugs`)
- `BugDetail` (`/home/bugs/detail`)
- `ValidationTriagePage` (`/home/validation-triage`)
- `ValidationQueuePage` (`/home/validation-queue`)
- `ValidationFixPage` (`/home/validation-fix`)
- `PlanningWorkspace` (`/home/planning`)
- `MultiProductPlanning` (`/planning/multi-product`)
- `ProductRoadmaps` (`/planning/product-roadmaps`, `/planning/{projectAlias}/product-roadmaps`)
- `ProductRoadmapEditor` (`/planning/product-roadmaps/{productId}`)
- `PlanBoard` (`/planning/plan-board`, `/planning/{projectAlias}/plan-board`)
- `ProjectPlanningOverview` (`/planning/{projectAlias}/overview`)

## Filter usage patterns observed

Most common dimensions:

- **Product**
  - common on health, backlog, roadmap, validation, bug, and portfolio-progress surfaces
- **Project**
  - concentrated on planning routes and project-scoped health/planning carry-over via `projectAlias`
- **Team**
  - concentrated on sprint, delivery-trend, PR, pipeline, and execution surfaces
- **Time**
  - concentrated on sprint, trend, rolling, and delivery-analysis surfaces

Commonly ignored dimensions:

- hub pages (`HealthWorkspace`, `DeliveryWorkspace`, `PlanningWorkspace`, `HomeChanges`) currently observe mostly neutral defaults
- read-only planning overviews (`ProductRoadmaps`, `ProjectPlanningOverview`) do not actively use team filters
- bug detail currently exposes no meaningful shared filter dimensions

## Unresolved state occurrences

Passive unresolved detection now flags:

- **missing team**
  - sprint and delivery-analysis pages when they declare team usage but no team is present in route/query context
- **missing sprint**
  - sprint/trend pages when sprint or sprint-range context is absent

Expected unresolved hotspots for later phases:

- `SprintTrend`
- `SprintExecution`
- `TrendsWorkspace`
- `DeliveryTrends`
- `PortfolioDelivery`
- `PortfolioProgressPage`
- `PipelineInsights`
- `PrDeliveryInsights`

These are now logged passively only; no behavior changes were introduced.

## Inconsistencies detected

- Some pages express filter state in query parameters, while others still keep effective selections in local component state.
- Project scope is mixed:
  - query-string based on shared home/workspace routes
  - route-segment based on planning routes
- Time semantics are inconsistent across pages:
  - single sprint
  - sprint range
  - rolling/implicit time
  - snapshot with no explicit value
- Some pages use product scope plus team/time indirectly, but only part of that state is currently visible through the passive store.

## Risks for Phase 2

- Pages that keep effective team/sprint selection only in local component state will need an explicit bridge before the global filter store becomes authoritative.
- Route-segment project scope and query-string project scope will need one canonical contract.
- Rolling/snapshot/sprint/trend time modes do not yet share a canonical serialized value shape.
- The new summary bar is intentionally read-only; turning it into an authoritative control too early would change behavior.
- Passive observation currently reflects route/query state first; it does not yet normalize all page-local selections into one canonical source of truth.
