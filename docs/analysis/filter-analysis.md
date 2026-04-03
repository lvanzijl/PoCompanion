# Filter analysis

## 1. Page inventory

### Home / entry / shell

| Route | File | Notes |
| --- | --- | --- |
| `/` | `PoTool.Client/Pages/Index.razor:1-63` | Startup router; no user filter UI. |
| `/profiles` | `PoTool.Client/Pages/ProfilesHome.razor:1-220` | Profile picker; not an analytics page, but it chooses the active user context. |
| `/sync-gate` | `PoTool.Client/Pages/SyncGate.razor:1-220` | Cache/sync gate; no analytics filters. |
| `/onboarding` | `PoTool.Client/Pages/Onboarding.razor:1-37` | Onboarding wizard shell. |
| `/home` | `PoTool.Client/Pages/Home/HomePage.razor:1-220,599-740` | Main workspace hub with product context chips. |
| `/home/changes` | `PoTool.Client/Pages/Home/HomeChanges.razor:1-240` | “What changed since last sync” page. |
| `/not-found` | `PoTool.Client/Pages/NotFound.razor:1` | Error route; no filters. |

### Health workspace

| Route | File | Notes |
| --- | --- | --- |
| `/home/health` | `PoTool.Client/Pages/Home/HealthWorkspace.razor:1-138` | Navigation hub; intentionally no data filters. |
| `/home/health/overview` | `PoTool.Client/Pages/Home/HealthOverviewPage.razor:1-260` | Build Quality overview. |
| `/home/health/backlog-health` | `PoTool.Client/Pages/Home/BacklogOverviewPage.razor:1-257,337-485` | Product backlog readiness. |
| `/home/backlog-overview` | `PoTool.Client/Pages/Home/BacklogOverviewPage.razor:1-2` | Legacy alias of Backlog Health. |
| `/home/validation-triage` | `PoTool.Client/Pages/Home/ValidationTriagePage.razor:1-176` | Validation category summary. |
| `/home/validation-queue` | `PoTool.Client/Pages/Home/ValidationQueuePage.razor:1-244` | Validation rule queue for one category. |
| `/home/validation-fix` | `PoTool.Client/Pages/Home/ValidationFixPage.razor:1-418` | Guided fix session for one validation rule. |

### Delivery workspace

| Route | File | Notes |
| --- | --- | --- |
| `/home/delivery` | `PoTool.Client/Pages/Home/DeliveryWorkspace.razor:1-158` | Navigation hub; no filter bar. |
| `/home/delivery/sprint` | `PoTool.Client/Pages/Home/SprintTrend.razor:1-1818` | Sprint Delivery; alias also lives at `/home/sprint-trend`. |
| `/home/sprint-trend` | `PoTool.Client/Pages/Home/SprintTrend.razor:1-2` | Legacy alias of Sprint Delivery. |
| `/home/delivery/sprint/activity/{WorkItemId:int}` | `PoTool.Client/Pages/Home/SprintTrendActivity.razor:1-220` | Activity detail for one work item. |
| `/home/sprint-trend/activity/{WorkItemId:int}` | `PoTool.Client/Pages/Home/SprintTrendActivity.razor:1-2` | Legacy alias of activity detail. |
| `/home/delivery/execution` | `PoTool.Client/Pages/Home/SprintExecution.razor:1-320,466-638` | Sprint execution diagnostics. |
| `/home/delivery/portfolio` | `PoTool.Client/Pages/Home/PortfolioDelivery.razor:1-320,489-684` | Aggregated delivery over a sprint range. |

### Trends workspace

| Route | File | Notes |
| --- | --- | --- |
| `/home/trends` | `PoTool.Client/Pages/Home/TrendsWorkspace.razor:1-240` | Trends signal hub with team/sprint context. |
| `/home/trends/delivery` | `PoTool.Client/Pages/Home/DeliveryTrends.razor:1-320,347-626` | Historical sprint trend charts. |
| `/home/portfolio-progress` | `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:1-360,600-820` | Portfolio flow trend over selected sprints. |
| `/home/pull-requests` | `PoTool.Client/Pages/Home/PrOverview.razor:1-320,611-920` | PR insights dashboard. |
| `/home/pr-delivery-insights` | `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:1-360,736-950` | PR-to-epic/feature delivery insights. |
| `/home/pipeline-insights` | `PoTool.Client/Pages/Home/PipelineInsights.razor:1-320,560-760` | Pipeline stability dashboard. |
| `/home/bugs` | `PoTool.Client/Pages/Home/BugOverview.razor:1-320,317-470` | Bug insights dashboard. |
| `/home/bugs/detail` | `PoTool.Client/Pages/Home/BugDetail.razor:1-235` | Bug detail/editor shell; currently placeholder-backed. |

### Planning workspace

| Route | File | Notes |
| --- | --- | --- |
| `/home/planning` | `PoTool.Client/Pages/Home/PlanningWorkspace.razor:1-145` | Navigation hub; no filter bar. |
| `/planning/plan-board` | `PoTool.Client/Pages/Home/PlanBoard.razor:1-280,466-969` | Product planning board. |
| `/planning/product-roadmaps` | `PoTool.Client/Pages/Home/ProductRoadmaps.razor:1-320,545-967` | Multi-product roadmap overview. |
| `/planning/product-roadmaps/{ProductId:int}` | `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor:1-220` | Per-product roadmap editor. |
| `/home/dependencies` | `PoTool.Client/Pages/Home/DependencyOverview.razor:1-151` | Read-only dependency overview linked from planning context. |

### Other active routes

| Route | File | Notes |
| --- | --- | --- |
| `/bugs-triage` | `PoTool.Client/Pages/BugsTriage.razor:1-220` | Dedicated bug triage workspace with tag filters. |
| `/workitems` | `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:1-200` | Legacy explorer with validation/text filters. |
| `/settings` and `/settings/{SelectedTopic}` | `PoTool.Client/Pages/SettingsPage.razor:1-203` | Settings shell; topic selection is navigation, not analytics filtering. |
| `/settings/workitem-states` | `PoTool.Client/Pages/Settings/WorkItemStates.razor:1` | Settings subpage; no analytics filters. |
| `/settings/productowner/edit/{ProfileId:int?}` | `PoTool.Client/Pages/Settings/EditProductOwner.razor:1` | Settings editor; no analytics filters. |
| `/settings/teams` | `PoTool.Client/Pages/Settings/ManageTeams.razor:1` | Settings manager; no analytics filters. |
| `/settings/productowner/{ProfileId:int}` | `PoTool.Client/Pages/Settings/ManageProductOwner.razor:1` | Settings manager; no analytics filters. |

### Legacy intent workspaces

| Route | File | Notes |
| --- | --- | --- |
| `/legacy` | `PoTool.Client/Pages/Landing.razor:1-160` | Legacy intent landing; no analytics filters. |
| `/workspace/product` and `/workspace/product/{ProductId:int}` | `PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor:1-2` | Legacy workspace route. |
| `/workspace/team` and `/workspace/team/{TeamId:int}` | `PoTool.Client/Pages/LegacyWorkspaces/TeamWorkspace.razor:1-2` | Legacy workspace route. |
| `/workspace/analysis` and `/workspace/analysis/{Mode}` | `PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor:1-2` | Legacy workspace route. |
| `/workspace/communication` | `PoTool.Client/Pages/LegacyWorkspaces/CommunicationWorkspace.razor:1` | Legacy workspace route. |

## 2. Filter inventory

| Filter | Meaning | UI type | State scope | Server/client application | Main references |
| --- | --- | --- | --- | --- | --- |
| Product (`productId`) | Scope analytics to one product; product is the primary analytical boundary in the domain model. | Home chips, selects, context chips, popover selects | Shared by query string on `WorkspaceBase` pages; local on several pages | Server-side where API accepts product IDs; client-side on Bug Insights and some roadmap/planning pages | `PoTool.Client/Pages/Home/HomePage.razor:43-93,599-740`; `PoTool.Client/Pages/Home/WorkspaceBase.cs:24-80`; `docs/rules/hierarchy-rules.md:38-46` |
| Team (`teamId`) | Scope to one team or to the sprint list belonging to a team. | Selects, popover selects, context chips | Mixed: query string in Trends hub/detail pages; local state elsewhere | Usually server-side through `teamId`, or indirectly by choosing team-owned sprint IDs; sometimes client-side only for filtering lists | `PoTool.Client/Pages/Home/TrendsWorkspace.razor:41-92,785-795`; `PoTool.Client/Pages/Home/PrOverview.razor:70-126,733-849`; `PoTool.Client/Pages/Home/SprintExecution.razor:40-109,586-617` |
| Sprint (single) | Pick one sprint window. | Select, popover select, implicit auto-selected current sprint | Local state; sometimes query string only for navigation context | Server-side via `sprintId`, `iterationPath`, or derived date range | `PoTool.Client/Pages/Home/SprintExecution.razor:61-83,548-562`; `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:87-109,918-933`; `PoTool.Client/Pages/Home/SprintTrend.razor:1039-1399` |
| Sprint range (`fromSprintId`, `toSprintId`) | Compare/aggregate multiple sprints. | Two selects or popover selects | Local state; URL-persisted in some pages only | Server-side via expanded `sprintIds[]` request; not a single backend range parameter | `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:105-210,694-820`; `PoTool.Client/Pages/Home/PortfolioDelivery.razor:79-165,571-665` |
| End sprint + sprint count | Historical horizon for Delivery Trends. | Select + numeric field | Local state, product/team partly mirrored to URL | Server-side via `GetSprintTrendMetricsAsync(productOwnerId, sprintIds)` | `PoTool.Client/Pages/Home/DeliveryTrends.razor:101-129,423-621`; `PoTool.Api/Controllers/MetricsController.cs:691-722` |
| Repository | Restrict PR insights to one repository. | Dropdown | Local state | Server-side on PR Insights; not present elsewhere | `PoTool.Client/Pages/Home/PrOverview.razor:111-125,838-849`; `PoTool.Client/ApiClient/ApiClient.PullRequestInsights.cs:60-89`; `PoTool.Api/Controllers/PullRequestsController.cs:307-325` |
| Validation category (`category`) | Pick one validation category (SI/RR/RC/EFF). | Implicit query parameter, chosen via cards | URL/query-string state | Server-side | `PoTool.Client/Pages/Home/ValidationQueuePage.razor:164-205`; `PoTool.Api/Controllers/WorkItemsController.cs:151-181` |
| Validation rule (`ruleId`) | Pick one validation rule inside a category. | Implicit query parameter, chosen via queue card | URL/query-string state | Server-side | `PoTool.Client/Pages/Home/ValidationFixPage.razor:285-322`; `PoTool.Api/Controllers/WorkItemsController.cs:184-220` |
| Fixed rolling window | Last 30 days Build Quality on Health Overview. | Informational chip only | Local constant | Server-side | `PoTool.Client/Pages/Home/HealthOverviewPage.razor:25-31,134-193`; `PoTool.Api/Controllers/BuildQualityController.cs:22-34` |
| Fixed last-6-month time window | Default Bug/PR time horizon. | Implicit only on Bug Insights and PR pages | Local state | Server-side for PR pages, client-side for Bug Insights metrics | `PoTool.Client/Pages/Home/BugOverview.razor:257-258,396-402`; `PoTool.Client/Pages/Home/PrOverview.razor:645-706`; `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:762-827` |
| Include partial success | Include partially succeeded pipeline runs. | Toggle/switch | Local state | Server-side | `PoTool.Client/Pages/Home/PipelineInsights.razor:110-118,566-738`; `PoTool.Api/Controllers/PipelinesController.cs:156-170` |
| Include canceled | Include canceled pipeline runs. | Toggle/switch | Local state | Server-side | `PoTool.Client/Pages/Home/PipelineInsights.razor:120-128,566-738`; `PoTool.Api/Controllers/PipelinesController.cs:156-170` |
| SLO duration minutes | Visual threshold line on pipeline scatter. | Numeric field | Local state | Client-side only; not sent to backend | `PoTool.Client/Pages/Home/PipelineInsights.razor:130-152,575` |
| Product filter in Sprint Execution | Narrow execution diagnostics to one product inside the selected sprint. | Popover select | Local state | Server-side | `PoTool.Client/Pages/Home/SprintExecution.razor:86-108,558-561`; `PoTool.Api/Controllers/MetricsController.cs:868-888` |
| Tag filters + match mode | Filter Bugs Triage by triage tags. | Chips + Any/All button group | Local state | Client-side on already loaded bug set | `PoTool.Client/Pages/BugsTriage.razor:81-121` |
| Validation category checkboxes | Toggle categories in Work Item Explorer. | Checkbox list | Local state | Client-side plus helper filtering service | `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:43-59,135-162` |
| Free-text tree filter | Filter Work Item Explorer tree by text. | Text input embedded in tree grid | Local state | Client-side | `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:67-76,129-130` |
| Search available epics | Filter available epics in roadmap editor. | Text field | Local state | Client-side | `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor:145-155` |
| Route-scoped product | Roadmap editor product route parameter. | Route parameter | URL/route state | Server-side through product-specific work item loads | `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor:1-15` |
| Route/query work item IDs | Sprint Activity and Bug Detail route/query context. | Route or query parameter | URL state | Sprint Activity is server-side; Bug Detail is currently placeholder/local only | `PoTool.Client/Pages/Home/SprintTrendActivity.razor:1-220`; `PoTool.Client/Pages/Home/BugDetail.razor:184-206` |

### Shared context mechanism already in the codebase

- `WorkspaceBase` parses `productId` and `teamId` from the URL and rebuilds those parameters when navigating between workspace pages: `PoTool.Client/Pages/Home/WorkspaceBase.cs:24-80`.
- The Home page already uses a product-context bar and passes `productId` into Health, Trends, Delivery, Planning, and Validation navigation: `PoTool.Client/Pages/Home/HomePage.razor:43-93,599-740`.
- Trends hub also persists `teamId` plus optional sprint-range choices and reuses them when navigating to several trends pages: `PoTool.Client/Pages/Home/TrendsWorkspace.razor:41-123,760-778`.

## 3. Applicability matrix

Legend:
- `✓` = implemented now
- `△` = not implemented, but the page purpose/data model makes it a reasonable future candidate
- `—` = not applicable / should stay page-local / not an analytics filter on that page

| Page | Product | Project | Package | State | Time | Other filters |
| --- | --- | --- | --- | --- | --- | --- |
| Home | ✓ | — | — | — | — | Product Owner profile context |
| Health hub | ✓ (propagated) | — | — | — | — | none |
| Health Overview | ✓ | — | — | — | ✓ (fixed rolling window, implicit) | none |
| Backlog Health | ✓ | — | — | — | — | none |
| Validation Triage | ✓ | — | — | — | — | Validation categories are destination cards, not filters on-page |
| Validation Queue | ✓ | — | — | — | — | Category ✓ |
| Validation Fix | ✓ | — | — | ✓ (display only, not filter) | — | Category ✓, Rule ✓ |
| Delivery hub | — | — | — | — | — | none |
| Sprint Delivery | — | — | — | — | ✓ | drill-down level is page-local |
| Sprint Activity | — | — | — | — | ✓ (implicit period from parent page/query) | Work item route param |
| Sprint Execution | ✓ | — | — | △ | ✓ | Team ✓ |
| Portfolio Delivery | — | — | — | △ | ✓ | Team ✓ |
| Trends hub | ✓ | — | — | — | ✓ | Team ✓ |
| Delivery Trends | ✓ | — | — | — | ✓ | Team ✓, end sprint ✓, sprint count ✓ |
| Portfolio Progress | ✓ | — | — | △ | ✓ | Team ✓ |
| PR Overview | — | — | — | — | ✓ (implicit date range) | Team ✓, Sprint ✓, Repository ✓, author highlight client-side |
| PR Delivery Insights | — | — | — | — | ✓ (explicit through sprint-derived date range) | Team ✓, Sprint ✓ |
| Pipeline Insights | — | — | — | — | ✓ | Team ✓, Sprint ✓, partial-success ✓, canceled ✓, SLO ✓ |
| Bug Insights | ✓ | — | — | △ | ✓ (fixed last 6 months, implicit) | Team ✓ |
| Bug Detail | — | — | — | ✓ (editable field, not shared filter) | — | bugId query param |
| Planning hub | — | — | — | — | — | none |
| Plan Board | ✓ | — | — | — | △ | none |
| Product Roadmaps | — | — | — | — | — | none; read-only reporting/snapshot actions |
| Product Roadmap Editor | ✓ (route-scoped) | — | — | — | — | search text ✓ |
| Dependency Overview | — | — | — | — | — | none |
| Home Changes | — | — | — | ✓ (reported change type, not filter) | ✓ (implicit sync window) | none |
| Bugs Triage | — | — | — | — | — | Tag filters ✓ |
| Work Item Explorer | — | — | — | — | — | Validation checkboxes ✓, text search ✓, query-scoped root/category flags |
| Profiles / SyncGate / Onboarding / Settings / legacy routes | — | — | — | — | — | navigation/setup state only |

## 4. Classification

### Global filters that already behave like cross-page context

1. **Product**
   - Evidence: Home product bar writes product context into navigation (`PoTool.Client/Pages/Home/HomePage.razor:43-93,599-740`), and many Health/Trends pages read `productId` via `WorkspaceBase` (`PoTool.Client/Pages/Home/WorkspaceBase.cs:24-80`).
   - Why global: it is the domain’s primary analytical boundary (`docs/rules/hierarchy-rules.md:38-46`) and is reused across Home, Health, Trends, backlog, validation, bugs, portfolio flow, and planning.
   - Backend status: already supported on several pages (`/api/workitems/validated`, `/api/workitems/validation-*`, `/api/metrics/portfolio-progress-trend`, `/api/buildquality/rolling` indirectly by product-owner scope).

2. **Team**
   - Evidence: repeated on Trends hub, Delivery Trends, Sprint Execution, Portfolio Delivery, PR Overview, PR Delivery Insights, Pipeline Insights, and Bug Insights.
   - Why shared: even though it is not yet app-global, it is the second-most repeated scope dimension in the UI.
   - Caveat: current implementation is inconsistent. Some pages persist it in query string (`TrendsWorkspace`, `DeliveryTrends`), while others keep it local only.

### Workspace filters

1. **Time / Sprint**
   - Delivery workspace pages depend heavily on sprint or sprint range (`SprintTrend`, `SprintExecution`, `PortfolioDelivery`).
   - Trends workspace pages also depend heavily on historical sprint/date horizons (`TrendsWorkspace`, `DeliveryTrends`, `PortfolioProgress`, PR pages, Pipeline Insights).
   - It should be **workspace-shared**, not truly app-global, because Health and Planning hubs are intentionally lightweight navigation hubs and most of their pages are not time-driven.

2. **Validation category / rule**
   - These are Health-workspace specific drill-down filters, not global filters.
   - They are already encoded in the navigation flow: Triage → Queue (`category`) → Fix (`category`, `ruleId`).

### Page-specific filters that should remain local

- Repository on PR Overview.
- Include partial success / include canceled / SLO duration on Pipeline Insights.
- Bug triage tag filters and tag match mode.
- Work Item Explorer validation checkboxes and text search.
- Roadmap editor search text.
- Placeholder Bug Detail editing fields.

### Filters that exist in backend contracts but are not used by current UI pages

The portfolio read endpoints already support **project number**, **work package**, and **lifecycle state** filtering through `BuildPortfolioReadOptions(...)` in `PoTool.Api/Controllers/MetricsController.cs:28-53`, and those parameters are exposed on `/api/portfolio/progress`, `/api/portfolio/snapshots`, `/api/portfolio/comparison`, `/api/portfolio/trends`, and `/api/portfolio/signals` (`PoTool.Api/Controllers/MetricsController.cs:247-445`).

Those filters are **not currently implemented in any page covered above**, because the current UI pages use the sprint-oriented endpoints (`portfolio-progress-trend`, `portfolio-delivery`) rather than the read-model endpoints.

## 5. Per-page analysis

### Home
- Current filters: product chip bar only (`HomePage.razor:43-93`).
- Shared/local: shared within navigation through `BuildContextQuery()`.
- Missing filters: none for a hub page.
- Inconsistency: Home propagates only `productId`, not `teamId`, while Trends hub also carries team context.
- Change impact: **frontend-only** if product/team context propagation is unified.

### Health hub
- Current filters: none on the hub itself; it only forwards context (`HealthWorkspace.razor:102-137`).
- Missing filters: none; hub should stay lightweight per UI rules.
- Change impact: **frontend-only** if it adopts any shared global filter shell.

### Health Overview
- Current filters: product chip from query string, implicit fixed last-30-days window (`HealthOverviewPage.razor:25-42,146-200`).
- Backend: `BuildQualityService.GetRollingWindowAsync(...)` → `GET /api/buildquality/rolling` → `BuildQualityPageDto` / `GetBuildQualityRollingWindowQuery` (`BuildQualityController.cs:22-34`).
- Server/client: server-side by product-owner/time window; product only affects ordering/highlighting client-side today.
- Missing filters: no interactive team/state/time control.
- Inconsistency: product is only a context chip here, unlike selectable product filters on Backlog Health.
- Change impact: making product selection shared/global is **frontend-only**; adding more server-side filters would require backend changes.

### Backlog Health
- Current filters: required product select (`BacklogOverviewPage.razor:26-42`).
- Backend: `IWorkItemsClient.GetBacklogStateAsync(productId)` → `GET /api/workitems/backlog-state/{productId}` (`WorkItemsController.cs:863-891`) plus `WorkItemService.GetValidationTriageSummaryAsync(new[] { productId })` for SI counts.
- Server/client: server-side by product.
- Missing filters: none clearly required beyond product.
- Inconsistency: product is local page state rather than shared query-string context after selection.
- Change impact: **frontend-only** to align with global product context.

### Validation Triage
- Current filters: product context chip only (`ValidationTriagePage.razor:24-34,125-138`).
- Backend: `WorkItemService.GetValidationTriageSummaryAsync(productIds)` → `GET /api/workitems/validation-triage` → `ValidationTriageSummaryDto` / `GetValidationTriageSummaryQuery` (`WorkItemsController.cs:125-148`).
- Server/client: server-side by product IDs.
- Missing filters: none on the page itself; category selection is the drill-down action.
- Inconsistency: no explicit product picker despite product scope being important.
- Change impact: **frontend-only** to move product control into shared global state.

### Validation Queue
- Current filters: product context chip plus required `category` query parameter (`ValidationQueuePage.razor:24-47,164-205`).
- Backend: `GET /api/workitems/validation-queue?category=...&productIds=...` → `ValidationQueueDto` / `GetValidationQueueQuery` (`WorkItemsController.cs:151-181`).
- Server/client: server-side.
- Missing filters: none; rule grouping is the page’s purpose.
- Change impact: **frontend-only** for global product-state adoption; category should remain page/workspace-specific.

### Validation Fix
- Current filters: product context chip, required `category`, required `ruleId`; item cards also display state/type/effort but do not filter on them (`ValidationFixPage.razor:25-35,285-322`).
- Backend: `GET /api/workitems/validation-fix?ruleId=...&category=...&productIds=...` → `ValidationFixSessionDto` / `GetValidationFixSessionQuery` (`WorkItemsController.cs:184-220`).
- Server/client: server-side.
- Missing filters: none for the current task flow.
- Inconsistency: no way to widen/narrow within a rule session without leaving the page.
- Change impact: product globalization is **frontend-only**; any new state/type filters would need backend support.

### Sprint Delivery
- Current filters: implicit sprint context only; user navigates previous/next sprint rather than using a shared selector (`SprintTrend.razor:58-125,1039-1399`).
- Backend: `SprintDeliveryMetricsService.GetSprintTrendMetricsAsync(productOwnerId, sprintIds, includeDetails)` → `GET /api/metrics/sprint-trend`; build-quality panel uses `GET /api/buildquality/sprint`.
- DTOs/queries: `GetSprintTrendMetricsResponse`, `SprintTrendMetricsDto`, `DeliveryBuildQualityDto`, `GetSprintTrendMetricsQuery`, `GetBuildQualitySprintQuery`.
- Server/client: server-side.
- Missing filters: no shared product/team control even though downstream detail pages use them.
- Inconsistency: Delivery workspace uses local sprint navigation here, while other Delivery pages use team/sprint popovers.
- Change impact: **frontend-only** to standardize context UI; adding extra backend filters is not necessary for the current sprint-centric model.

### Sprint Activity
- Current filters: route param `WorkItemId`; implicit period/team/sprint return context parsed from query string (`SprintTrendActivity.razor:197-220,242-475`).
- Backend: metrics detail endpoint through `IMetricsClient` returning `WorkItemActivityDetailsDto`.
- Server/client: server-side.
- Missing filters: none; this is a drill-down detail page.
- Change impact: **frontend-only** if navigation context persistence is standardized.

### Sprint Execution
- Current filters: Team, Sprint, Product (`SprintExecution.razor:40-109,548-603`).
- Backend: `MetricsClient.GetSprintExecutionAsync(productOwnerId, sprintId, productId)` → `GET /api/metrics/sprint-execution` → `SprintExecutionDto` / `GetSprintExecutionQuery` (`MetricsController.cs:857-888`).
- Server/client: server-side.
- Missing filters: state-based views are reasonable but not implemented.
- Inconsistency: product filter exists here but not on Sprint Delivery.
- Change impact: existing filter unification is **frontend-only**; adding state filters would require backend changes because the endpoint only accepts `productId` today.

### Portfolio Delivery
- Current filters: Team, From Sprint, To Sprint (`PortfolioDelivery.razor:51-165,571-665`).
- Backend: `MetricsClient.GetPortfolioDeliveryAsync(productOwnerId, sprintIds)` → `GET /api/metrics/portfolio-delivery` → `PortfolioDeliveryDto` / `GetPortfolioDeliveryQuery` (`MetricsController.cs:798-831`).
- Server/client: server-side by selected sprint IDs.
- Missing filters: no product filter even though the result shows per-product composition.
- Inconsistency: team is used only to choose the sprint list; the API itself has no product/team parameter beyond sprint IDs.
- Change impact: selector UI unification is **frontend-only**; adding a true product/state filter would require backend changes to `GetPortfolioDeliveryQuery` and `PortfolioDeliveryDto` consumption.

### Trends hub
- Current filters: Team, From Sprint, To Sprint, optional product context chip (`TrendsWorkspace.razor:41-123`).
- Backend: used to compute tile signals via PR/build-quality services, but the hub mostly passes context into child pages (`TrendsWorkspace.razor:528-778`).
- Server/client: mixed; mostly navigation context.
- Missing filters: none beyond making team/time context more consistently reusable by child pages.
- Inconsistency: it propagates product in query string but does not pass team/time into every child route (for example PR Delivery Insights and Pipeline Insights are navigated without the full context).
- Change impact: **frontend-only** to propagate shared context more consistently.

### Delivery Trends
- Current filters: Team, Product, End Sprint, Sprints to show (`DeliveryTrends.razor:68-129,423-621`).
- Backend: `MetricsClient.GetSprintTrendMetricsAsync(productOwnerId, sprintIds, recompute:false)` → `GET /api/metrics/sprint-trend`.
- Server/client: team/time server-side through sprint IDs; product is client-side projection from per-product metrics (`GetMetricValue(...)`, `DeliveryTrends.razor:527-540`).
- Missing filters: none obvious beyond sharing/persisting the time context better.
- Inconsistency: team/product are mirrored to URL, but end sprint and sprint count are not.
- Change impact: URL/state unification is **frontend-only**; any desire to offload product filtering fully to the server would require backend changes.

### Portfolio Progress
- Current filters: Product, Team, From Sprint, To Sprint (`PortfolioProgressPage.razor:53-210,694-820`).
- Backend: `MetricsClient.GetPortfolioProgressTrendAsync(productOwnerId, sprintIds, productIds)` → `GET /api/metrics/portfolio-progress-trend` → `PortfolioProgressTrendDto` / `GetPortfolioProgressTrendQuery` (`MetricsController.cs:724-759`).
- Server/client: product and time are server-side; team only controls available sprint list on the client.
- Missing filters: no project/work-package/state filters despite those concepts existing in the portfolio read-model endpoints.
- Inconsistency: this page is called “Portfolio Flow Trend” but does not use the richer `/api/portfolio/*` endpoints that already support `projectNumber`, `workPackage`, and `lifecycleState`.
- Change impact: shared context/persistence is **frontend-only**; adding project/package/state would require backend endpoint changes here or a switch to the read-model endpoints and corresponding DTO/view-model changes.

### PR Overview
- Current filters: Team, optional Sprint, optional Repository; implicit date range stored in `_fromDate/_toDate`; author filter is client-side only from the already loaded data (`PrOverview.razor:70-126,689-905`).
- Backend: `PullRequestsClient.GetInsightsAsync(teamId, fromDate, toDate, repositoryName)` → `GET /api/pullrequests/insights` → `PullRequestInsightsDto` / `GetPullRequestInsightsQuery` (`PullRequestsController.cs:298-331`; `ApiClient.PullRequestInsights.cs:12-140`).
- Server/client: team/date/repository server-side; author highlight/filter client-side.
- Missing filters: no editable date-range UI even though the endpoint supports it.
- Inconsistency: repository is page-local and not shared with PR Delivery Insights.
- Change impact: exposing date inputs or persisting current filters is **frontend-only**; adding author filtering to the server would require backend changes.

### PR Delivery Insights
- Current filters: Team, optional Sprint; implicit date range derived from sprint or last six months (`PrDeliveryInsights.razor:71-109,811-942`).
- Backend: `PullRequestsClient.GetDeliveryInsightsAsync(teamId, sprintId, fromDate, toDate)` → `GET /api/pullrequests/delivery-insights` → `PrDeliveryInsightsDto` / `GetPrDeliveryInsightsQuery` (`PullRequestsController.cs:333-360`; `ApiClient.PrDeliveryInsights.cs:11-140`).
- Server/client: server-side.
- Missing filters: no visible date-range controls even though the API accepts them.
- Inconsistency: less filterable than PR Overview despite using the same team/sprint/time concepts.
- Change impact: adding visible time controls is **frontend-only**; any new repository/author/state filters would require backend additions.

### Pipeline Insights
- Current filters: Team, Sprint, Include partial success, Include canceled, SLO duration (`PipelineInsights.razor:40-152,642-758`).
- Backend: `PipelinesClient.GetInsightsAsync(productOwnerId, sprintId, includePartiallySucceeded, includeCanceled)` → `GET /api/pipelines/insights` → `PipelineInsightsDto` / `GetPipelineInsightsQuery` (`PipelinesController.cs:151-176`). Build-quality drawer uses `GET /api/buildquality/pipeline` (`BuildQualityController.cs:49-62`).
- Server/client: toggles are server-side; SLO line is client-side only.
- Missing filters: no product/repository filter at page level, even though build-quality detail supports `pipelineDefinitionId` and `repositoryId`.
- Inconsistency: team is only used to select a sprint; it is not sent to the pipeline endpoint directly.
- Change impact: UI consolidation is **frontend-only**; adding product/repository filters to the main page would require backend changes to `GetPipelineInsightsQuery` or extra endpoint usage.

### Bug Insights
- Current filters: Team and Product dropdowns; implicit last-6-month time horizon (`BugOverview.razor:65-105,317-437`).
- Backend/data source: `WorkItemService.GetAllAsync()` → `GET /api/workitems`; all bug/type/team/product filtering and metrics are then computed client-side (`BugOverview.razor:325-402`).
- Server/client: client-side.
- Missing filters: no explicit state or editable time range; the page text already implies time range.
- Inconsistency: unlike PR and pipeline pages, bug scoping is not pushed to the backend at all.
- Change impact: using existing local filters globally is **frontend-only**; moving bug/product/team/time/state filtering server-side requires backend additions (likely new work-item query parameters or a dedicated bug insights endpoint).

### Bug Detail
- Current filters: `bugId` query parameter only (`BugDetail.razor:184-206`).
- Backend: none today; the page loads sample data locally.
- Missing filters: all real bug-detail data integration.
- Inconsistency: page looks editable but has no API-backed data or save flow.
- Change impact: any real filter/global-context support here requires **backend work** plus frontend implementation.

### Planning hub
- Current filters: none.
- Missing filters: none; it is a pure navigation hub.
- Change impact: **frontend-only** if a shared global scope shell is added.

### Plan Board
- Current filters: Product select only (`PlanBoard.razor:52-82,490-541`).
- Backend/data sources: product lookup via `ProductService`; board data via `WorkItemService.GetByRootIdsAsync(...)`, `StateClassificationService.GetClassificationsAsync()`, sprint list via `SprintService.GetSprintsForTeamAsync(...)`, capacity calibration via `MetricsClient.GetCapacityCalibrationAsync(...)`.
- Server/client: mostly client-side shaping after product-scoped loads.
- Missing filters: no explicit team/sprint selector even though sprint columns and capacity calibration are team-driven.
- Inconsistency: planning is product-scoped, but sprint choice is implicit from the product’s first team.
- Change impact: adding a visible sprint/team scope would likely be **frontend-only** initially because the page already has the necessary data, but any server-side narrowing of root hierarchy would require backend work.

### Product Roadmaps
- Current filters: none; data is implicitly profile-scoped and rendered across all products owned by the active profile (`ProductRoadmaps.razor:545-654`).
- Backend/data sources: `ProductService.GetProductsByOwnerAsync`, `WorkItemService.GetByRootIdsAsync`, `RoadmapAnalyticsService`, `IRoadmapSnapshotsClient` (`RoadmapSnapshotsController.cs:22-76`).
- Server/client: mixed; heavy shaping is client/service side.
- Missing filters: no product selector because the page deliberately shows all products at once.
- Change impact: any “global product filter” here is mostly **frontend-only**, but it may conflict with the page’s multi-product purpose.

### Product Roadmap Editor
- Current filters: route-scoped product, local search text for available epics (`ProductRoadmapEditor.razor:1-15,145-155`).
- Backend/data sources: product lookup + work-item hierarchy updates through `WorkItemService`.
- Missing filters: none beyond search.
- Change impact: **frontend-only** for any shared shell; route product should remain page-specific.

### Dependency Overview
- Current filters: none visible (`DependencyOverview.razor:38-92`).
- Data source: embedded `DependenciesPanel` decides its own loads; the page itself only establishes profile context.
- Missing filters: not enough evidence in this file to justify a shared analytics filter requirement.
- Change impact: likely **frontend-only** if a shared global product context shell is added around the embedded panel.

### Home Changes
- Current filters: none visible; implicit time window = previous successful sync → latest sync (`HomeChanges.razor:65-71`).
- Backend: `CacheSyncService` summary endpoint for change windows and counts.
- Missing filters: no product/team scope even though the summary could theoretically be sliced.
- Change impact: any such slicing would require **backend work** because the current API is sync-window oriented, not scoped by product/team.

### Bugs Triage
- Current filters: tag chips and Any/All match mode (`BugsTriage.razor:81-121`); bug set is profile-product scoped because the page loads all bugs for active profile products (`BugsTriage.razor:193-220`).
- Backend/data source: `WorkItemService.GetAllWithValidationAsync(productIds)` plus `IBugTriageClient.GetUntriagedBugIdsAsync(...)`.
- Server/client: tag filtering is client-side.
- Missing filters: no team/product selector, though underlying data could support them.
- Change impact: tag filter globalization is not appropriate; adding team/product controls could start as **frontend-only**.

### Work Item Explorer
- Current filters: validation category checkboxes, free-text tree filter, query parameters `validationCategory`, `rootWorkItemId`, `allProducts`, `allTeams` (`WorkItemExplorer.razor:43-59,88-120,135-162`).
- Data source: `WorkItemService`, `WorkItemFilteringService`, tree/visibility/selection services.
- Server/client: mostly client-side filtering after load.
- Missing filters: team-based filtering is explicitly “not yet implemented” even though `allTeams` exists as a query flag (`WorkItemExplorer.razor:114-120`).
- Change impact: checkbox/text filter unification is **frontend-only**; true team filtering would require backend or substantial client scope logic.

### Settings / Profiles / SyncGate / Onboarding / legacy routes
- Current filters: none that belong in a unified analytics filter system.
- Change impact: not applicable.

## 6. Impact analysis

### Frontend-only changes

These changes can be made without new API/query parameters because the backend already supports the scope, or because the state is purely UI state today.

- **Unify Product context** across Home, Health Overview, Backlog Health, Validation pages, Trends hub, Delivery Trends, Portfolio Progress, and Planning routes using the existing `productId` query-string pattern (`HomePage.razor:599-740`, `WorkspaceBase.cs:24-80`).
- **Standardize Team/Sprint filter UI** across Delivery/Trends/PR/Pipeline pages. Most pages already have the necessary state and backend calls; the inconsistency is mainly local-vs-query-string persistence.
- **Persist additional time state to URL** for Delivery Trends, Portfolio Progress, and Delivery workspace pages. The APIs already accept `sprintIds[]` or `sprintId`; the missing piece is consistent client persistence.
- **Expose existing implicit date ranges in UI** for PR Overview and PR Delivery Insights, because the backend already accepts `fromDate`/`toDate`.
- **Move page-local filter controls into reusable shared components** (team selector, sprint range popover, product scope chip) without changing API contracts.

### Backend-required changes

These changes need API/controller/query/DTO updates because the current backend contract does not expose the necessary filter dimension for the relevant page.

- **Bug Insights server-side scoping**
  - Current page loads all work items via `GET /api/workitems` and filters bugs/product/team/time locally (`BugOverview.razor:325-402`).
  - Backend work needed for any scalable/global Product/Team/State/Time filter model.
  - Likely touch points: `PoTool.Api/Controllers/WorkItemsController.cs`, `PoTool.Core.WorkItems.Queries.*`, and bug-specific DTO/query additions.

- **Portfolio Delivery product/state filtering**
  - Current endpoint `GET /api/metrics/portfolio-delivery` only accepts `productOwnerId` and `sprintIds` (`MetricsController.cs:807-831`).
  - Adding explicit Product / State / Project / Package filters requires changes to `GetPortfolioDeliveryQuery`, controller signature, and `PortfolioDeliveryDto` consumption.

- **Sprint Execution state-specific filtering**
  - Current endpoint only accepts `productOwnerId`, `sprintId`, `productId` (`MetricsController.cs:868-888`).
  - Any reusable State filter would require API/query changes.

- **Portfolio Progress project/work-package/lifecycle-state filtering on the current page**
  - The page uses `portfolio-progress-trend`, which only accepts `productOwnerId`, `sprintIds`, and `productIds` (`MetricsController.cs:735-759`).
  - The richer portfolio read-model filters (`projectNumber`, `workPackage`, `lifecycleState`) exist only on the `/api/portfolio/*` endpoints (`MetricsController.cs:247-445`).
  - To expose those dimensions on this page, either the current trend endpoint must grow, or the page must be redesigned to consume the read-model endpoints.

- **Pipeline Insights product/repository filters at dashboard level**
  - Main insights endpoint only accepts `productOwnerId`, `sprintId`, `includePartiallySucceeded`, `includeCanceled` (`PipelinesController.cs:156-170`).
  - The build-quality detail endpoint can filter by `pipelineDefinitionId` / `repositoryId`, but that is not the same as page-level dashboard scoping (`BuildQualityController.cs:49-62`).

- **Home Changes product/team filtering**
  - Current change-summary API is sync-window based, not scope-based (`HomeChanges.razor:65-71`).
  - Product/team slices require new API/query support and probably DTO changes in `CacheSyncService`/controller responses.

- **Bug Detail real data/edit flow**
  - The page currently loads placeholder data only (`BugDetail.razor:196-206`).
  - Any real filtering/editing/stateful behavior requires backend endpoints and DTO wiring.

## Concrete conclusions

1. **Product is the only clearly established global filter today.** It already has domain backing and a working cross-page propagation mechanism.
2. **Team is the strongest additional shared candidate.** It is widespread, but implemented inconsistently.
3. **Time/Sprint should be shared within Delivery and Trends, not across the entire app.** Health and Planning hubs are intentionally not time-driven.
4. **Validation category/rule, repository, pipeline toggles, tag filters, and search boxes are page/workspace-specific.** They should not become app-global.
5. **Project / Work Package / lifecycle State exist in backend portfolio read APIs but not in the current UI pages.** They are not current global filters; introducing them would require backend-backed page work, not just a frontend shell change.
