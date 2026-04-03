# Current default filtering analysis

## 1. Inventory of pages/views

| View | Initial load behavior | Time default | Product default | Project default | Project/team/additional defaults | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Home dashboard (`PoTool.Client/Pages/Home/HomePage.razor`) | Loads immediately after profile load | None | No explicit default; uses `productId` from URL when present, otherwise all products in home metrics/signals | None | No team or sprint scope | Home tiles/signals use all profile products when no product is selected (`HomePage.razor:316-323`, `WorkspaceSignalService.cs:382-391`). |
| Health hub (`PoTool.Client/Pages/Home/HealthWorkspace.razor`) | No analytical data load | None | Context only | Context only | Just propagates query context | Hub does not inject defaults; it only forwards current context (`HealthWorkspace.razor:102-137`). |
| Health overview (`PoTool.Client/Pages/Home/HealthOverviewPage.razor`) | Loads immediately | Explicit rolling last 30 days | Effective all products; `productId` only affects display ordering/label | None | No team filter | Request only sends owner + 30-day window, not `productId` (`HealthOverviewPage.razor:149-205`, `BuildQualityService.cs:23-34`). |
| Backlog health (`PoTool.Client/Pages/Home/BacklogOverviewPage.razor`) | Loads only if a product can be resolved | None | Explicit URL `productId`; else auto-select only when exactly one product exists; otherwise no load | None | No team/sprint filters | Multiple products + no context leaves page empty until user picks a product (`BacklogOverviewPage.razor:372-387`). |
| Validation triage (`PoTool.Client/Pages/Home/ValidationTriagePage.razor`) | Loads immediately | None | Explicit `productId`; otherwise all products in active profile | None | No team/sprint/category default | Page passes every profile product when no product context exists (`ValidationTriagePage.razor:142-156`). |
| Validation queue (`PoTool.Client/Pages/Home/ValidationQueuePage.razor`) | Requires `category` query param, then loads immediately | None | Explicit `productId`; otherwise all products in active profile | None | Category is mandatory; no team/sprint defaults | Missing category blocks the page; product scope falls back to all profile products (`ValidationQueuePage.razor:194-235`). |
| Validation fix (`PoTool.Client/Pages/Home/ValidationFixPage.razor`) | Requires `category` and `ruleId`, then loads immediately | None | Explicit `productId`; otherwise all products in active profile | None | Category + rule required | Same all-products fallback as queue/triage (`ValidationFixPage.razor:316-355`). |
| Portfolio progress (`PoTool.Client/Pages/Home/PortfolioProgressPage.razor`) | Loads immediately after profile/team/sprint setup | Explicit 5-sprint window: current sprint + 4 previous | Explicit URL `productId`; otherwise all products | None | Auto-selects first team when none is provided | Team selection only drives available sprint range; request carries sprint IDs plus optional product (`PortfolioProgressPage.razor:645-665`, `779-805`). |
| Portfolio delivery (`PoTool.Client/Pages/Home/PortfolioDelivery.razor`) | Loads immediately after profile/team/sprint setup | Explicit 5-sprint window: current sprint + 4 previous | Always all products | None | Auto-selects first team when none is provided | No product filter exists; request always sends `productIds: null` (`PortfolioDelivery.razor:531-546`, `593-624`). |
| Sprint delivery (`PoTool.Client/Pages/Home/SprintTrend.razor`) | Loads immediately after merged sprint context load | Implicit current sprint; summary compares current + previous sprint | All products | None | No team filter in UI; drill-down defaults to aggregate view | Current sprint is picked from all team sprints merged together (`SprintTrend.razor:1086-1096`, `1193-1209`, `1234-1245`). |
| Delivery trends (`PoTool.Client/Pages/Home/DeliveryTrends.razor`) | Loads immediately | Explicit last 6 sprints ending at current/most recent sprint | All products unless `productId` query exists | None | Team filter optional; default trend length = 6 | Team only narrows the sprint list locally; backend request uses sprint IDs and optional product (`DeliveryTrends.razor:310-321`, `405-456`). |
| Sprint execution (`PoTool.Client/Pages/Home/SprintExecution.razor`) | Loads immediately after profile/team/sprint setup | Explicit current sprint (or most recent past sprint) | All products unless user picks one | None | Auto-selects first team when none is provided | Product filter is optional; team only determines sprint candidates (`SprintExecution.razor:508-518`, `547-559`, `572-578`). |
| PR overview (`PoTool.Client/Pages/Home/PrOverview.razor`) | Loads immediately | UI shows last 6 months, then current sprint if a team is selected; actual API default is no time filter | No product filter | None | Auto-selects team only when exactly one team exists; repository filter default = all repos | Date controls are computed locally but never sent; `sprintId` is sent to `/insights` even though the controller does not accept it (`PrOverview.razor:661-679`, `705-718`, `795-835`; `PullRequestStateService.cs:16-29`; `PullRequestsController.cs:344-367`). |
| PR delivery insights (`PoTool.Client/Pages/Home/PrDeliveryInsights.razor`) | Loads immediately | UI shows last 6 months, then current sprint if a team is selected; actual API uses sprint only | No product filter | None | Auto-selects team only when exactly one team exists | Date controls are dead; state service only sends team + sprint (`PrDeliveryInsights.razor:777-795`, `826-840`, `894-933`; `PullRequestStateService.cs:31-42`; `PullRequestsController.cs:386-409`). |
| Pipeline insights (`PoTool.Client/Pages/Home/PipelineInsights.razor`) | Loads only after team/sprint can be resolved | Explicit current sprint (or most recent past sprint) once a team is known | All owner products linked to the sprint scope | None | Team from URL or single-team auto-select; `includePartiallySucceeded=true`, `includeCanceled=false` | With multiple teams and no `teamId`, the page stops before loading data (`PipelineInsights.razor:638-661`, `694-710`, `769-775`). |
| Bug overview (`PoTool.Client/Pages/Home/BugOverview.razor`) | Loads immediately | Implicit last 6 months, but only inside client-side metric calculations | All products/teams unless user selects filters | None | Team and product filters are both optional and both applied client-side | API request pulls all cached work items, then filters bugs in the browser by area path (`BugOverview.razor:238-360`, `WorkItemService.cs:62-67`, `WorkItemsController.cs:38-45`). |
| Bug detail (`PoTool.Client/Pages/Home/BugDetail.razor`) | Requires `bugId` query param | None | None | None | `bugId` is mandatory | Not a filter page; it is purely record lookup by ID (`BugDetail.razor:199-267`). |
| Dependency overview (`PoTool.Client/Pages/Home/DependencyOverview.razor`) | Page shell loads, embedded panel does not auto-load data | None | None | None | Embedded panel needs manual filter input | `DependenciesPanel` only loads automatically when `InitialAreaPath` is supplied, but the page passes none (`DependencyOverview.razor:67-69`, `DependenciesPanel.razor:123-140`). |
| Product roadmaps (`PoTool.Client/Pages/Home/ProductRoadmaps.razor`) | Loads immediately | None | All products in selected project; if no project resolves, all profile products | Route `projectAlias`, query `projectAlias`, or project inferred from `productId`; otherwise no project scope | No team/sprint filter | Project scope is explicit when present but otherwise open-ended (`ProductRoadmaps.razor:821-825`, `1132-1160`). |
| Plan board (`PoTool.Client/Pages/Home/PlanBoard.razor`) | Loads only if a product can be resolved | No page-level time filter; board itself only shows next 3 future sprints from the product's first team | URL `productId`; else auto-select only when one product remains in scope | Route/query project alias, or project inferred from `productId`; otherwise no project scope | Candidate tree excludes Done/Removed; board uses only the first team on the product | Multiple products + no `productId` means no board is loaded (`PlanBoard.razor:595-609`, `705-714`, `792-799`). |
| Multi-product planning (`PoTool.Client/Pages/Home/MultiProductPlanning.razor`) | Loads immediately | None | Explicitly selects all products by default | None | Clearing the selection snaps back to all products | Initial scope is always the full product set (`MultiProductPlanning.razor:466-470`, `658-669`). |
| Project planning overview (`PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`) | Loads immediately | None | Project summary only; no product filter | Route alias is mandatory | No team/sprint filter | Explicit route-scoped project page (`ProjectPlanningOverview.razor:153-163`). |

## 2. Explicit vs implicit defaults

### Explicit defaults

- **30-day window** on Health Overview (`HealthOverviewPage.razor:136-153`).
- **5-sprint window** on Portfolio Progress and Portfolio Delivery (`PortfolioProgressPage.razor:645-665`, `678-713`; `PortfolioDelivery.razor:531-546`, `575-591`).
- **Current sprint fallback** on Sprint Execution, Pipeline Insights, PR pages with team scope, and Sprint Delivery (`SprintExecution.razor:547-559`; `PipelineInsights.razor:980-998`; `PrOverview.razor:805-826`; `PrDeliveryInsights.razor:904-924`; `SprintTrend.razor:1193-1209`).
- **6-sprint trend length** on Delivery Trends (`DeliveryTrends.razor:310-321`, `445-450`).
- **All products selected** on Multi-Product Planning (`MultiProductPlanning.razor:466-470`).
- **All profile products** on Validation Triage/Queue/Fix whenever `productId` is absent (`ValidationTriagePage.razor:142-156`; `ValidationQueuePage.razor:223-235`; `ValidationFixPage.razor:345-355`).
- **Pipeline result toggles** default to include partially succeeded builds and exclude canceled builds (`PipelineInsights.razor:579-580`).

### Implicit defaults

- **No filter submitted means `ALL`** in the backend filter-resolution services. For delivery, sprint, pipeline, PR, and portfolio filter contexts, omitted selections map to `FilterSelection.All()` / `FilterTimeSelection.None()` (`DeliveryFilterResolutionService.cs:103-126`, `370-373`; `SprintFilterResolutionService.cs:124-152`; `PipelineFilterResolutionService.cs:127-147`, `419-422`; `PullRequestFilterResolutionService.cs:132-162`, `415-423`; `PortfolioFilterResolutionService.cs:101-118`).
- **Owner scoping silently becomes the effective product universe** on delivery/sprint/pipeline boundaries when `productOwnerId` is present and `productIds` is omitted (`DeliveryFilterResolutionService.cs:167-227`; `SprintFilterResolutionService.cs:196-252`; `PipelineFilterResolutionService.cs:180-242`).
- **PR team scope can silently derive product scope**: if PR filters specify a team but not products, the backend resolves products from `ProductTeamLinks` (`PullRequestFilterResolutionService.cs:52-65`).
- **Home dashboard defaults to all products** unless a valid `productId` is in the URL (`HomePage.razor:316-323`; `WorkspaceSignalService.cs:382-391`).
- **Bug Overview defaults to all cached work items**, then filters in-memory by area-path mappings only if team/product filters are chosen (`BugOverview.razor:304-334`).
- **Plan Board implicitly limits visible sprint columns to the next 3 future sprints** from the product's first team, even though the page has no time filter (`PlanBoard.razor:705-726`).
- **Dependency Overview implicitly defaults to no graph data**, because the embedded panel needs an initial area path or user-entered filters before it will call the API (`DependenciesPanel.razor:134-156`).

## 3. Inconsistencies

1. **Time defaults diverge sharply by page.**
   - Health Overview uses **30 days**.
   - Portfolio Progress / Portfolio Delivery use **5 sprints**.
   - Delivery Trends uses **6 sprints**.
   - Sprint Execution / Pipeline Insights / Sprint Delivery use **current sprint**.
   - PR pages show **6 months in the UI**, but actual backend scope is either **none** or **single sprint** depending on the page.

2. **Auto-selection behavior is inconsistent.**
   - Portfolio Progress, Portfolio Delivery, and Sprint Execution auto-pick the **first team** (`PortfolioProgressPage.razor:647-650`; `PortfolioDelivery.razor:531-535`; `SprintExecution.razor:508-512`).
   - PR Overview, PR Delivery Insights, and Pipeline Insights only auto-pick when there is **exactly one team** (`PrOverview.razor:667-670`; `PrDeliveryInsights.razor:782-785`; `PipelineInsights.razor:658-661`).
   - Backlog Health and Plan Board do not auto-pick when multiple products exist.

3. **Similar PR views do not share the same real defaults.**
   - PR Overview presents sprint/date controls but actually defaults to **all-time** data because neither dates nor a valid sprint parameter reach the controller.
   - PR Delivery Insights uses the same visual pattern but actually becomes **current-sprint scoped** when a team is selected, because `sprintId` is wired for that endpoint.

4. **Context propagation is inconsistent with query execution.**
   - Health Overview preserves `productId` context but does not apply it to the Build Quality request.
   - Delivery Trends preserves `teamId` in the URL, but the backend endpoint only understands sprint IDs and product IDs; team scope is enforced only indirectly by the locally chosen sprint list.

5. **Trend views and snapshot-style views do not align.**
   - Validation pages default to **all profile products**.
   - Delivery pages often default to **all owner products inside a sprint window**.
   - Planning pages are often **project- or product-routed** instead of using a shared global filter pattern.

## 4. Missing defaults

- **Backlog Health** has no fallback for multiple products; it simply does not load data until a product is chosen (`BacklogOverviewPage.razor:372-387`).
- **Pipeline Insights** has no fallback for multiple teams; without `teamId` and without a single-team profile, the page renders but does not load insights (`PipelineInsights.razor:647-663`).
- **Dependency Overview** has no initial area/work-item scope and therefore shows an uninitialized dependency panel (`DependencyOverview.razor:67-69`; `DependenciesPanel.razor:134-156`).
- **Plan Board** has no fallback product when more than one product is in scope, so the board itself remains empty until a product is chosen (`PlanBoard.razor:600-609`).
- **Product Roadmaps** has no fallback project scope; absent route/query context, it broadens to all products visible to the profile (`ProductRoadmaps.razor:1132-1153`).

## 5. Dead or ineffective filters

1. **PR Overview date range is dead.**
   - `_fromDate` and `_toDate` are computed and updated in the UI (`PrOverview.razor:661-663`, `824-850`).
   - `PullRequestStateService.GetInsightsStateAsync` does not send either date (`PullRequestStateService.cs:16-29`).
   - The controller accepts `fromDate`/`toDate` but never receives them from this page (`PullRequestsController.cs:339-367`).

2. **PR Overview sprint selection is effectively dead.**
   - The page sends `sprintId` to `/api/pullrequests/insights` (`PullRequestStateService.cs:16-29`).
   - `GetInsights` does not accept `sprintId`, so the selected sprint does not affect the backend query (`PullRequestsController.cs:344-350`).

3. **PR Delivery Insights date range is dead.**
   - The page tracks `_fromDate` / `_toDate` and updates them from the selected sprint (`PrDeliveryInsights.razor:818-839`, `920-946`).
   - The state service only sends `productOwnerId`, `teamId`, and `sprintId` (`PullRequestStateService.cs:31-42`).

4. **Health Overview product context is decorative, not filtering.**
   - `ProductId` is parsed and used to label/order cards (`HealthOverviewPage.razor:200-205`, `232-240`).
   - `BuildQualityService.GetRollingWindowAsync` only sends owner + window (`BuildQualityService.cs:23-34`).

5. **Dependency Overview's embedded graph has no active initial filter wiring.**
   - The page renders `<DependenciesPanel />` without an `InitialAreaPath` (`DependencyOverview.razor:67-69`).
   - The panel only auto-loads when `InitialAreaPath` is non-empty (`DependenciesPanel.razor:134-140`).

6. **Bug Overview filters are not pushed to the backend.**
   - The page always calls `GetAllStateAsync()` with no product/team scope (`BugOverview.razor:304-334`; `WorkItemService.cs:62-67`).
   - Product/team filtering happens only after the full cached payload is in memory.

## 6. UX impact summary

| View | UX impact on initial load |
| --- | --- |
| Home dashboard | Usually meaningful; scope is understandable only if the user notices the optional product context. |
| Health overview | Meaningful summary, but product-specific navigation context is misleading because it does not filter the data. |
| Backlog health | Good when a product is preselected; otherwise blank for multi-product profiles. |
| Validation triage / queue / fix | Usually meaningful because they default to all profile products; category/rule requirements are explicit on queue/fix. |
| Portfolio progress | Usually meaningful because it auto-selects a team and a 5-sprint window, but the “first team” fallback is arbitrary. |
| Portfolio delivery | Meaningful on first load for the same reason, but always broad across all products. |
| Sprint delivery | Meaningful, but the scope is implicit because the page silently chooses the current sprint from the merged sprint list. |
| Delivery trends | Meaningful and bounded by six sprints, though the scope can still be broad when no product/team is set. |
| Sprint execution | Meaningful, but again depends on an arbitrary first-team default. |
| PR overview | Potentially overloaded and misleading: UI suggests current-sprint or 6-month scoping, but the backend defaults to all-time data unless repository/team filters are applied. |
| PR delivery insights | More understandable than PR Overview when a team exists because sprint scoping is real; without team context it broadens to all-time/all-team data even though the UI still shows a date range. |
| Pipeline insights | Good when team context exists; otherwise the page can appear inert for multi-team profiles. |
| Bug overview | Meaningful, but broad by default because it loads every cached bug and only narrows after optional client-side filters. |
| Dependency overview | Weak initial UX: the page loads, but the embedded graph does not because no starting filter is supplied. |
| Product roadmaps | Meaningful, but potentially broad when no project scope is resolved. |
| Plan board | Meaningful only when product scope is already known; otherwise users land on a mostly empty board shell. |
| Multi-product planning | Meaningful, but potentially dense because it defaults to every product. |
| Project planning overview | Clear and predictable because the route itself defines the scope. |

## 7. Overall assessment

There is **not** a coherent default filtering strategy today.

What exists is a mix of:

- explicit per-page defaults (30 days, 5 sprints, 6 sprints, current sprint, all products),
- implicit backend `ALL` behavior when filters are omitted,
- route/context propagation that is sometimes honored and sometimes ignored,
- and a few views where the UI exposes filter state that never reaches the query boundary.

The current behavior is therefore predictable only within individual pages, not across the application. The largest gaps are:

- inconsistent auto-selection rules,
- PR pages whose visible defaults do not match effective query filters,
- pages that render without any usable initial scope (Backlog Health, Pipeline Insights, Dependency Overview, Plan Board),
- and context values such as `productId` that are propagated across navigation but not consistently enforced by backend requests.
