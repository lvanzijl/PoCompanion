# Filter Contract Sanity Check

This report validates the currently reachable pages against the global filter contract after recent cleanup. Route aliases were collapsed into one logical page entry. Clearly unreachable pages were excluded (`/not-found`, obsolete `TfsConfig.razor` with commented-out route).

## Summary

- Total logical pages analyzed: **43**
- Pages per default time mode (best inferred):
  - **Snapshot:** 29
  - **Rolling:** 5
  - **Sprint:** 4
  - **Trend:** 3
  - **Unclear / outside contract:** 2
- Pages requiring Team (Yes or Conditional): **8**
- Violations: **11**
- Warnings: **9**

## Key Findings

- Sprint-scoped delivery/trends pages still rely on fallback behavior (`first team`, `current/latest sprint`, merged sprint lists), which directly conflicts with the strict contract.
- Several pages mix time semantics on one surface (`Home`, `PR` pages, `TrendsWorkspace`, `HomeChanges`), so the default time mode is not always cleanly declared by current behavior.
- Planning pages mostly fit a Snapshot model, but some still derive hidden sprint context from `product.TeamIds.First()`.
- Legacy `/legacy` and `/workspace/*` routes are still active and preserve the old intent/context model, which does not map cleanly to the new global filter contract.
- Most startup, settings, health triage, and editor/admin pages cleanly fit **Snapshot** and do not block the contract.

## Contract Fit Verdict

**RED** — active pages still contain fundamental conflicts with the strict filter contract, mainly around sprint resolution and fallback behavior.

## 1. Page classification

### 1.1 Startup and administration

| Page / route(s) | Primary purpose | Supported time modes | Default | Requires Team | Supports Team | Supports Product | Supports Project | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| `Index` `/` | Startup redirector into onboarding / profiles / sync gate | Snapshot | Snapshot | No | No | No | No | GREEN | Pure bootstrap routing. |
| `Onboarding` `/onboarding` | First-run wizard | Snapshot | Snapshot | No | No | No | No | GREEN | No analytic filter semantics. |
| `SyncGate` `/sync-gate` | Cache readiness gate before workspace entry | Snapshot | Snapshot | No | No | No | No | GREEN | Operational gate only. |
| `ProfilesHome` `/profiles` | Pick or create active profile | Snapshot | Snapshot | No | No | No | No | GREEN | No filter model needed. |
| `SettingsPage` `/settings`, `/settings/{SelectedTopic}` | Settings shell for cache/TFS/import/state topics | Snapshot | Snapshot | No | No | No | No | GREEN | Topic navigation only. |
| `ManageProductOwner` `/settings/productowner/{ProfileId:int}` | Manage products for one product owner | Snapshot | Snapshot | No | No | No | No | GREEN | Admin detail page. |
| `EditProductOwner` `/settings/productowner/edit/{ProfileId:int?}` | Create or edit product owner profile | Snapshot | Snapshot | No | No | No | No | GREEN | Admin form. |
| `ManageProducts` `/settings/products` | Global product administration | Snapshot | Snapshot | No | No | No | No | GREEN | Admin list. |
| `ManageTeams` `/settings/teams` | Team administration | Snapshot | Snapshot | No | No | No | No | GREEN | Admin list. |
| `WorkItemStates` `/settings/workitem-states` | Configure canonical state mapping | Snapshot | Snapshot | No | No | No | No | GREEN | Admin mapping page. |

### 1.2 Home, workspace hubs, and health pages

| Page / route(s) | Primary purpose | Supported time modes | Default | Requires Team | Supports Team | Supports Product | Supports Project | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| `Home` `/home` | Product-owner dashboard and workspace entry | Snapshot | Snapshot | No | No | Yes | No | WARNING | Surface is hub-like, but embedded signals mix current sprint, recent-sprint trend, planning capacity, and “today” metrics. |
| `HealthWorkspace` `/home/health` | Health hub for overview / validation / backlog views | Snapshot | Snapshot | No | No | Via propagated context | Via propagated context only | GREEN | Pure navigation hub. |
| `HealthOverviewPage` `/home/health/overview` | Build quality overview for rolling window | Rolling | Rolling | No | No | Yes | No | GREEN | Explicit 30-day rolling default. |
| `BacklogOverviewPage` `/home/health/backlog-health`, `/home/backlog-overview` | Product backlog readiness detail | Snapshot | Snapshot | No | No | Yes | No | GREEN | Explicit unresolved state when no product is selected. |
| `ValidationTriagePage` `/home/validation-triage` | Validation category summary | Snapshot | Snapshot | No | No | Yes | No | GREEN | Product context optional. |
| `ValidationQueuePage` `/home/validation-queue` | Rule-group queue for one validation category | Snapshot | Snapshot | No | No | Yes | No | GREEN | Product context optional. |
| `ValidationFixPage` `/home/validation-fix` | Guided fix session for validation items | Snapshot | Snapshot | No | No | Yes | No | GREEN | Product context optional. |
| `BugOverview` `/home/bugs` | Rolling bug metrics and severity breakdown | Rolling | Rolling | No | Yes | Yes | No | GREEN | Explicit team/product filters with 6-month rolling window. |
| `BugDetail` `/home/bugs/detail` | Read-only detail for one bug | Snapshot | Snapshot | No | No | No | No | GREEN | Entity detail page. |
| `BugsTriage` `/bugs-triage` | Triage and tag bug backlog | Snapshot | Snapshot | No | No | No | No | WARNING | Hidden implicit scope = all products for active profile; no explicit filter declaration on the page. |
| `HomeChanges` `/home/changes` | Show changes since last sync window | Unclear | Unclear | No | No | No | No | VIOLATION | Uses “since last sync” window, which is outside Snapshot / Sprint / Trend / Rolling. |

### 1.3 Delivery and trends pages

| Page / route(s) | Primary purpose | Supported time modes | Default | Requires Team | Supports Team | Supports Product | Supports Project | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| `DeliveryWorkspace` `/home/delivery` | Delivery hub | Snapshot | Snapshot | No | No | Via propagated context | Via propagated context only | GREEN | Pure navigation hub. |
| `SprintDelivery` `/home/delivery/sprint`, `/home/sprint-trend` | Inspect one sprint’s delivered work | Sprint | Sprint | **No** | No | No | No | VIOLATION | Sprint page loads all team sprints, picks a merged “current sprint,” and falls back to latest/last sprint without explicit team resolution. |
| `SprintExecution` `/home/delivery/execution` | Diagnose churn within one sprint | Sprint | Sprint | Yes | Yes | Yes | No | VIOLATION | Auto-selects the first available team and then auto-selects a current/latest sprint. |
| `PortfolioDelivery` `/home/delivery/portfolio` | Compare delivery across a sprint range | Trend | Trend | Conditional | Yes | Yes | No | VIOLATION | Sprint range is team-derived and page auto-selects first team plus default sprint window. |
| `TrendsWorkspace` `/home/trends` | Trends hub with bug / PR / dependency / flow tiles | Rolling, Trend | Rolling | Conditional | Yes | Yes | No | GREEN | Default is last 6 months; team-specific sprint range is optional. |
| `PrOverview` `/home/pull-requests` | Pull request metrics and charts | Rolling, Sprint | Rolling | Conditional | Yes | Yes (repository optional too) | No | VIOLATION | Auto-selects sole team and current/latest sprint when team context exists; behavior still depends on fallback logic. |
| `PrDeliveryInsights` `/home/pr-delivery-insights` | PR classification against sprint delivery context | Rolling, Sprint | Rolling | Conditional | Yes | No | No | VIOLATION | Same fallback pattern as PR overview; sprint mode is optional but still auto-derived. |
| `PipelineInsights` `/home/pipeline-insights` | Sprint-scoped pipeline stability view | Sprint | Sprint | Yes | Yes | No | No | VIOLATION | Page depends on team+sprint but auto-selects them via fallback instead of unresolved state. |
| `DeliveryTrends` `/home/trends/delivery` | Multi-sprint delivery trend charts | Trend | Trend | No | Yes | Yes | No | VIOLATION | End sprint is inferred from merged/current/latest sprint heuristics; page still depends on fallback logic. |
| `DependencyOverview` `/home/dependencies` | Read-only dependency insight surface | Snapshot | Snapshot | No | No | No | No | WARNING | Contract fit is simple Snapshot, but the page still exposes a standalone dependency feature outside the reduced workspace model. |
| `PortfolioProgressPage` `/home/portfolio-progress` | Portfolio flow/progress over sprint range | Trend | Trend | Conditional | Yes | Yes | No | VIOLATION | Auto-selects first team and default sprint window (`current + past 4`), violating strict no-fallback rules. |

### 1.4 Planning pages

| Page / route(s) | Primary purpose | Supported time modes | Default | Requires Team | Supports Team | Supports Product | Supports Project | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| `PlanningWorkspace` `/home/planning` | Planning hub | Snapshot | Snapshot | No | No | Via propagated context | Yes | GREEN | Pure navigation hub; project-aware tile routing only. |
| `ProductRoadmaps` `/planning/product-roadmaps`, `/planning/{RouteProjectAlias}/product-roadmaps` | Read-only roadmap lanes across products | Snapshot | Snapshot | No | No | No | Yes | WARNING | Page fits Snapshot, but sprint cadence is derived from `product.TeamIds.First()` and explicit fallback states are shown. |
| `ProductRoadmapEditor` `/planning/product-roadmaps/{ProductId:int}` | Edit one product roadmap | Snapshot | Snapshot | No | No | Yes (route required) | No | GREEN | Clear product-detail editor. |
| `MultiProductPlanning` `/planning/multi-product` | Shared-axis planning across selected products | Snapshot | Snapshot | No | No | Yes | No | WARNING | Explicit product multi-select is good, but cadence still derives from first team plus fallback. |
| `PlanBoard` `/planning/plan-board`, `/planning/{RouteProjectAlias}/plan-board` | Assign PBIs/bugs into upcoming sprint columns | Snapshot | Snapshot | No | No | Yes (required) | Yes | WARNING | Page filters are project+product, but sprint columns come from the product’s first team. |
| `ProjectPlanningOverview` `/planning/{ProjectAliasRoute}/overview` | Project-level planning summary | Snapshot | Snapshot | No | No | No | Yes (required) | GREEN | Clean project-scoped snapshot page. |

### 1.5 Legacy routes still active

| Page / route(s) | Primary purpose | Supported time modes | Default | Requires Team | Supports Team | Supports Product | Supports Project | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| `Landing` `/legacy` | Legacy intent-based landing shell | Snapshot | Snapshot | No | No | No | No | WARNING | Still routeable, but built around the removed intent/navigation model. |
| `ProductWorkspace` `/workspace/product`, `/workspace/product/{ProductId:int}` | Legacy product/portfolio navigation shell | Snapshot | Snapshot | No | No | Yes | No | WARNING | Still active and scope-driven, but not aligned to the new explicit global filter contract. |
| `TeamWorkspace` `/workspace/team`, `/workspace/team/{TeamId:int}` | Legacy team workspace with sprint horizon controls | Sprint | Sprint | Yes | Yes | Indirectly | No | VIOLATION | Uses legacy scope stack plus team/sprint horizon behavior outside the new model. |
| `AnalysisWorkspace` `/workspace/analysis`, `/workspace/analysis/{Mode}` | Legacy multi-mode analysis shell | Unclear | Unclear | No | No | No | No | VIOLATION | One page mixes health, effort, flow, forecast, dependencies, and timeline modes with no single contract time model. |
| `CommunicationWorkspace` `/workspace/communication` | Legacy report/export shell | Snapshot | Snapshot | No | No | No | No | WARNING | Still active legacy route with old context model. |

## 2. Contract violations

### VIOLATION

- **`SprintDelivery`** — Sprint page, but team is not required or selectable; sprint is inferred from merged team data with fallback.
- **`SprintExecution`** — Sprint page, but it auto-selects first team and then auto-selects sprint.
- **`PortfolioDelivery`** — Team-derived multi-sprint page, but team/sprint defaults are filled by fallback.
- **`PrOverview`** — Supports sprint behavior, but auto-selects sole team and current/latest sprint.
- **`PrDeliveryInsights`** — Same fallback pattern as `PrOverview`.
- **`PipelineInsights`** — Sprint mode depends on team+sprint, but page auto-resolves both.
- **`DeliveryTrends`** — Trend page depends on implicit current/latest sprint resolution instead of explicit time input.
- **`PortfolioProgressPage`** — Auto-selects first team and a derived 5-sprint window.
- **`HomeChanges`** — Sync-window time model is outside the contract.
- **`TeamWorkspace`** — Legacy active route with team/sprint horizon behavior outside the new contract.
- **`AnalysisWorkspace`** — No clear single time/filter contract applies.

### WARNING

- **`Home`** — Snapshot shell with mixed hidden signal horizons.
- **`BugsTriage`** — Implicit all-products scope; filter dimensions are not explicit on the page.
- **`DependencyOverview`** — Snapshot page, but still preserves standalone dependency feature complexity.
- **`ProductRoadmaps`** — Snapshot page with hidden first-team cadence assumption.
- **`MultiProductPlanning`** — Snapshot page with hidden first-team cadence assumption.
- **`PlanBoard`** — Snapshot page, but future sprint columns come from first-team sprint data.
- **`Landing`** — Legacy route still active.
- **`ProductWorkspace`** — Legacy route still active.
- **`CommunicationWorkspace`** — Legacy route still active.

## 3. Orphaned complexity

Pages that no longer justify complex filtering or still imply removed/legacy features:

- **`DependencyOverview`** — read-only standalone dependency page remains active even though it now behaves like a narrow snapshot insight page.
- **`Landing`**, **`ProductWorkspace`**, **`TeamWorkspace`**, **`AnalysisWorkspace`**, **`CommunicationWorkspace`** — still-active legacy route family preserving the old intent/context model.
- **`HomeChanges`** — operational sync-diff page uses a bespoke time window outside the new filter modes.
- **`PlanBoard`**, **`ProductRoadmaps`**, **`MultiProductPlanning`** — page-level filters are mostly simple, but hidden sprint/team-derived logic still adds legacy complexity behind the scenes.

## 4. Minimal verdict

The remaining application is **not yet cleanly aligned** to the strict global filter contract.

What fits well:

- startup pages
- settings/admin pages
- health triage/detail pages
- bug detail/admin-style pages
- project-level planning summary/editor pages

What blocks clean alignment:

- sprint pages that still infer team/sprint implicitly
- trend pages that still default to latest/current sprint windows
- legacy routes that preserve the old workspace model
- a few pages whose time semantics are outside the contract entirely
