# Phase 2 — State Unification

## Canonical model

- `FilterState`
  - `ProductIds: IReadOnlyList<int>` — empty means ALL
  - `ProjectIds: IReadOnlyList<string>` — canonical internal project identifiers; empty means ALL
  - `TeamId: int?`
  - `Time: FilterTimeSelection`
- `FilterTimeSelection`
  - `Mode: Snapshot | Sprint | Range | Rolling`
  - `SprintId: int?`
  - `StartSprintId: int?`
  - `EndSprintId: int?`
  - `RollingWindow: int?`
  - `RollingUnit: Sprint | Days`
- `FilterStateResolution`
  - page metadata
  - canonical `FilterState`
  - `LastUpdateSource`
  - normalization decisions
  - missing-team / missing-sprint flags

## Normalization rules

- Precedence is `route > query > local bridge > defaults`.
- Project scoping is stored only as canonical `ProjectId`.
  - route project aliases are resolved through `ProjectService.GetProjectAsync(aliasOrId)`
  - query `projectId` is accepted directly
  - query or local `projectAlias` is resolved to `ProjectId`
- Time normalization:
  - sprint pages → `Time.Mode = Sprint`
  - trend/range pages → `Time.Mode = Range`
  - rolling pages → `Time.Mode = Rolling`
  - pages without explicit time → `Time.Mode = Snapshot`
- Invalid range ordering is normalized by swapping `fromSprintId` and `toSprintId`.
- Legacy query sprint input on rolling pages is normalized to a one-sprint rolling window.
- Missing required filters are flagged but not blocked.

## Pages migrated

These pages now resolve canonical filter state through `GlobalFilterStore` and use store-backed state for page/local filter synchronization:

- `WorkspaceBase` descendants
  - `HealthWorkspace`
  - `HealthOverviewPage`
  - `BacklogOverviewPage`
  - `HomeChanges`
  - `DeliveryWorkspace`
  - `PlanningWorkspace`
  - `ValidationTriagePage`
  - `ValidationQueuePage`
  - `ValidationFixPage`
  - `MultiProductPlanning`
  - `ProjectPlanningOverview`
  - `TrendsWorkspace`
- Direct filter-driven pages
  - `SprintExecution`
  - `SprintTrend`
  - `PortfolioDelivery`
  - `PortfolioProgressPage`
  - `PipelineInsights`
  - `PrOverview`
  - `DeliveryTrends`
  - `BugOverview`
- Shared shell/components
  - `MainLayout`
  - `FilterSummaryBar`

## Local state removed/bridged

- Bridged to store with local write-through plus store subscription:
  - `SprintExecution` — team / sprint / product
  - `PortfolioDelivery` — team / range
  - `PortfolioProgressPage` — product / team / range
  - `PipelineInsights` — team / sprint
  - `PrOverview` — team / sprint / rolling default
  - `DeliveryTrends` — product / team
  - `BugOverview` — product / team
  - `TrendsWorkspace` — product / team / range
  - `SprintTrend` — team / sprint initialization now reads canonical store state
- Remaining technical debt:
  - some pages still keep local UI fields as temporary mirrors of store state rather than removing those fields entirely
  - project-scoped planning pages still navigate with route aliases even though the store keeps only `ProjectId`

## Remaining inconsistencies

- Planning navigation still requires project aliases in route segments, so route generation is not yet fully canonical even though store state is.
- Some pages still refresh data from their own local mirrored fields after syncing instead of reading directly from `FilterState` for every downstream service call.
- Rolling time is normalized structurally, but page-specific rolling semantics are still approximate until Phase 3 enforcement.

## Risks for Phase 3

- Enforcing required filters will break direct links that currently rely on unresolved or partially normalized state.
- Project alias routes will need a deterministic reverse mapping strategy if route generation must become canonical as well.
- Pages that still use mirrored local fields may loop if enforcement adds automatic redirects without preserving current change-detection guards.
- Rolling pages may need a stricter contract for window semantics before backend/client enforcement can align safely.
