# Phase 3 — Stabilization before filter enforcement

## Canonical identity decisions

- `ProjectId` remains the only canonical project identity in `FilterState`.
- `projectAlias` remains an external route/input form only.
- Alias → `ProjectId` normalization now resolves through `ProjectIdentityMapper`.
- Reverse lookup support (`ProjectId` → alias) now exists in the same mapper so route generation can remain deterministic when alias-based planning routes are required.
- Shared context propagation now carries canonical `projectId` alongside any route alias.

## Pages fully de-mirrored

The following pages no longer keep team/sprint/product/range values as local authoritative state. They now read canonical filter values directly from `GlobalFilterStore` and write changes only through store updates:

- `SprintExecution`
- `PortfolioDelivery`
- `PortfolioProgressPage`
- `PipelineInsights`
- `PrOverview`
- `DeliveryTrends`
- `BugOverview`
- `TrendsWorkspace`
- `SprintTrend`
- `WorkspaceBase` descendants now read propagated context from store-backed state instead of query-local copies

## Time semantics contract

- `Snapshot`
  - no time value
  - current-state only
  - always resolved
- `Sprint`
  - exactly one `SprintId`
  - unresolved when missing
  - no local reinterpretation
- `Range`
  - explicit inclusive `StartSprintId` + `EndSprintId`
  - ordering is normalized centrally once
  - resolved only when both endpoints are present
- `Rolling`
  - explicit `RollingWindow` plus explicit unit
  - supported units: `Days`, `Sprint`
  - invalid when window is non-positive or unit is missing
  - current default normalization is a deterministic 180-day window when no rolling input is supplied

## Route/store guard strategy

- Added canonical route signatures that normalize path plus sorted query parameters.
- `GlobalFilterStore` now keeps:
  - current applied route signature
  - pending route signature
- Pages use `TryPrepareNavigation(...)` before route writes to block:
  - equivalent-route rewrites
  - duplicate pending navigations
  - route/store ping-pong loops
- Store observations now include:
  - route signature
  - explicit resolution status
  - explicit state issues

## Deep-link behavior

Validated with focused build/test coverage and targeted route-state scenarios:

- direct route-only navigation to project-scoped planning routes resolves alias → `ProjectId`
- direct query navigation with reversed sprint range normalizes deterministically
- equivalent route/query permutations do not trigger duplicate navigation writes
- missing required sprint/team inputs classify as `Unresolved` instead of collapsing into generic state
- unknown project aliases classify as `Invalid`

Remaining manual edge cases observed during code review:

- planning pages still rely on alias route segments because existing canonical planning routes are alias-shaped
- unsupported non-canonical local UI dimensions (for example page-specific drawers or highlight state) still remain local by design, but no longer own filter truth

## Remaining blockers before enforcement

- `DeliveryTrends` still exposes an end-sprint + sprint-count UI, but it now projects that UI onto canonical inclusive range state. Enforcement should decide whether that projection remains acceptable or the UI should later surface explicit start/end sprint controls.
- Planning route generation still depends on alias-shaped route templates. The identity mapping is now deterministic, but enforcement should centralize alias emission for all planning navigation call sites.

READY for Phase 4 once planning-route alias emission is centralized.
