# Current Filter State Analysis

## 1. Filter state locations

### 1.1 Shared URL/context state

| Location | Current state | Scope |
|---|---|---|
| `PoTool.Client/Pages/Home/WorkspaceBase.cs:24-80` | `ProductId`, `TeamId`; `ParseContextQueryParameters()` reads `productId` and `teamId` from the URL; `BuildContextQuery()` rebuilds a query string from those two values plus optional extra params. | Shared Home workspace context for pages inheriting `WorkspaceBase`; effectively global only inside the Home workspace flow. |
| `PoTool.Client/Pages/Home/HomePage.razor:599-614,717-740` | Local `_selectedProductId` plus a page-local `BuildContextQuery()` that only emits `productId`. | Home page product context propagated into Health, Trends, Delivery, Planning, and Validation Triage navigation. |
| `PoTool.Client/Pages/Home/TrendsWorkspace.razor:596-779` | `_selectedTeamId`, `_selectedFromSprintId`, `_selectedToSprintId`; `ParseSprintQueryParameters()` and `UpdateSprintUrlParameters()` read/write `teamId`, `fromSprintId`, `toSprintId`. `ProductId` still comes from `WorkspaceBase`. | Trends workspace only. |

### 1.2 Page-local in-memory filter state

| Location | Current fields | Scope |
|---|---|---|
| `PoTool.Client/Pages/Home/DeliveryTrends.razor:300-319` | `_selectedTeamId`, `_selectedProductId`, `_trendSprintCount`, `_teams`, `_products`, `_allSprints`, `_filteredSprints`, `_currentSprint`, `_currentSprintIndex`. | Page-local. |
| `PoTool.Client/Pages/Home/PortfolioProgressPage.razor:477-518` | `_selectedProductId`, `_selectedTeamId`, `_fromSprintId`, `_toSprintId`, `_defaultFromSprintId`, `_defaultToSprintId`, `_products`, `_teams`, `_sprints`. | Page-local. |
| `PoTool.Client/Pages/Home/PortfolioDelivery.razor:438-461` | `_selectedTeamId`, `_fromSprintId`, `_toSprintId`, `_selectedSprintIds`, `_teams`, `_sprints`. | Page-local. |
| `PoTool.Client/Pages/Home/SprintExecution.razor:428-447` | `_selectedTeamId`, `_selectedSprintId`, `_selectedProductId`, `_teams`, `_sprints`, `_products`; popover flags for each filter. | Page-local. |
| `PoTool.Client/Pages/Home/PipelineInsights.razor:556-590` | `_selectedTeamId`, `_selectedSprintId`, `_includePartiallySucceeded`, `_includeCanceled`, `_sloDurationMinutes`, `_teams`, `_sprints`. | Page-local. |
| `PoTool.Client/Pages/Home/PrOverview.razor:598-625` | `_selectedTeamId`, `_selectedSprintId`, `_fromDate`, `_toDate`, `_selectedRepository`, `_teams`, `_sprints`, `_availableRepositories`, `_highlightedPrId`, `_filteredAuthor`. | Page-local. |
| `PoTool.Client/Pages/Home/PrDeliveryInsights.razor:722-740` | `_selectedTeamId`, `_selectedSprintId`, `_fromDate`, `_toDate`, `_teams`, `_sprints`. | Page-local. |
| `PoTool.Client/Pages/Home/HealthOverviewPage.razor:133-145` | `_windowStartUtc`, `_windowEndUtc`, `_selectedProductName`; no interactive filter controls besides `ProductId` coming from URL context. | Page-local with inherited context. |
| `PoTool.Client/Pages/Home/ValidationTriagePage.razor:94-100` | `_productName`, `_summary`; effective product filter comes from inherited `ProductId`. | Page-local with inherited context. |
| `PoTool.Client/Pages/Home/ValidationQueuePage.razor:150-156` | `_productName`, `_categoryKey`, `_queue`; effective product filter comes from inherited `ProductId`, category from query string. | Page-local with inherited context + local query parsing. |
| `PoTool.Client/Pages/Home/ValidationFixPage.razor:261-279` | `_productName`, `_categoryKey`, `_ruleId`, `_productIds`, `_session`; effective product filter comes from inherited `ProductId`, category/rule from query string. | Page-local with inherited context + local query parsing. |
| `PoTool.Client/Pages/Home/BacklogOverviewPage.razor:342-351` | `_selectedProductId`, `_products`, `_state`, `_readyEpics`, `_refinementEpics`, `_siIssueCount`. | Page-local. |
| `PoTool.Client/Pages/Home/PlanBoard.razor:427-453` | `_selectedProductId`, `_products`, `_sprintColumns`, `_candidateTree`, `_sprintItems`, `_sprintCapacity`, `_selectedProductRootIds`. | Page-local. |
| `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor:511-544` | Route parameter `ProductId`; local `_searchText`, `_roadmapEpics`, `_availableEpics`, drawer state. | Page-local. |
| `PoTool.Client/Pages/BugsTriage.razor:156-171` | `_selectedTags`, `_tagMatchMode`, `_enabledTags`, `_selectedBugId`, `_expandedState`. | Page-local. |
| `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:94-120,122-175` | Query-backed `ValidationCategoryId`, `RootWorkItemId`, `AllProducts`, `AllTeams`; local `_validationFilters`, `_filterText`, `_focusRootId`, `_expandedState`, selection state. | Component-local. |
| `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor:456-480` | `_productId`, `_projectNumber`, `_workPackage`, `_lifecycleState`, `_sortBy`, `_groupBy`, `_snapshotCount`, `_includeArchivedSnapshots`, `_compareToSnapshotId`. | Component-local read-only portfolio view embedded under Portfolio Progress. |

### 1.3 Services and client helpers that build or carry filters

| Location | Current behavior |
|---|---|
| `PoTool.Client/Services/WorkItemService.cs:162-266` | Converts `int[]? productIds` into comma-separated `productIds=` query strings for work item, validation triage, validation queue, and validation fix endpoints. |
| `PoTool.Client/ApiClient/ApiClient.PullRequestInsights.cs:44-89` | Builds `teamId`, `fromDate`, `toDate`, `repositoryName` query parameters for `GET api/PullRequests/insights`. |
| `PoTool.Client/ApiClient/ApiClient.PrDeliveryInsights.cs:43-88` | Builds `teamId`, `sprintId`, `fromDate`, `toDate` query parameters for `GET api/PullRequests/delivery-insights`. |
| `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs:233-282` | Shared query builder for read-only portfolio endpoints: `productId`, `projectNumber`, `workPackage`, `lifecycleState`, `sortBy`, `sortDirection`, `groupBy`, `snapshotCount`, `rangeStartUtc`, `rangeEndUtc`, `includeArchivedSnapshots`, `compareToSnapshotId`. |
| `PoTool.Client/Services/BuildQualityService.cs:17-47` | Passes `productOwnerId`, `windowStartUtc`, `windowEndUtc`, `sprintId`, `pipelineDefinitionId`, `repositoryId` through typed generated clients. |
| `PoTool.Client/Services/SprintService.cs:23-44` | Team-scoped sprint lookup service; many pages depend on it to derive time filters before calling metrics APIs. |
| `PoTool.Client/Services/WorkItemFilteringService.cs:27-116` | Applies text and validation filters for Work Item Explorer by sending `FilterId` and invalid work item IDs to the filtering API. |

### 1.4 Persisted UI state related to filter flows

| Location | Persistence |
|---|---|
| `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor:448-489` | Saves `_expandedState` to `localStorage` key `workitem-expanded-state`; filter selections themselves are not persisted. |
| `PoTool.Client/Pages/BugsTriage.razor:567-601` | Saves `_expandedState` to `localStorage` key `bugs-triage-expanded-state`; tag filters are not persisted. |
| `PoTool.Client/Components/Common/ResizableSplitter.razor:29-80` | Saves splitter widths to `localStorage` using `potool-<StorageKey>`; this persists panel layout, not actual filters. |
| `PoTool.Client/Storage/BrowserPreferencesService.cs:18-59` | Generic `localStorage` wrapper for bool/int preferences; not wired into current filter pages. |
| `PoTool.Client/Storage/DraftStorageService.cs:20-99` | Generic `localStorage` draft persistence; used for drafts, not current filter state. |
| `PoTool.Client/Storage/BrowserSecureStorageService.cs:20-53` | Generic `sessionStorage`; not used for current filters. |

## 2. Current filter types

| Filter | Where it appears today | Representation | How it is passed |
|---|---|---|---|
| Product | Home product bar (`HomePage.razor:268-269,599-625`), Health/Validation context (`WorkspaceBase.cs:27-32`, `ValidationTriagePage.razor:120-137`), Backlog Health (`BacklogOverviewPage.razor:342-449`), Delivery Trends (`DeliveryTrends.razor:309-318,551-571`), Portfolio Progress (`PortfolioProgressPage.razor:510-518,774-782`), Sprint Execution (`SprintExecution.razor:433-437,558-562`), Plan Board (`PlanBoard.razor:428-429,490-496`), CDC panel (`PortfolioCdcReadOnlyPanel.razor:459-470,516-573`). | Usually `int?`; validation/work item APIs use `int[]?`; home product bar uses one selected product at a time. | URL query `productId`; comma-separated `productIds` in work item endpoints; `productId` in read-only portfolio endpoints; optional `productId` in sprint execution; not sent at all in Delivery Trends metrics call. |
| Team | Workspace context (`WorkspaceBase.cs:30-32`), Trends workspace (`TrendsWorkspace.razor:318-323,596-624`), Delivery Trends, Portfolio Progress, Portfolio Delivery, Sprint Execution, Pipeline Insights, PR Insights, PR Delivery Insights. | `int?`. | URL query `teamId` on workspace/trends pages; direct query `teamId` only on PR endpoints; in many delivery pages it is used only to fetch sprints and is not sent to the final API. |
| Time / sprint single | Pipeline Insights (`PipelineInsights.razor:563-565,642-703`), PR Insights (`PrOverview.razor:615-617,776-835`), PR Delivery Insights (`PrDeliveryInsights.razor:735-739,876-933`), Sprint Execution (`SprintExecution.razor:435-436,533-562`), Build Quality sprint detail (`BuildQualityService.cs:26-47`). | `int? sprintId`; related pages also keep derived `DateTime` / `DateTimeOffset` range fields. | Query `sprintId` to pipeline, PR delivery, sprint execution, and build-quality detail; PR Insights does not send `sprintId`, it converts the selected sprint into `fromDate` / `toDate`. |
| Time / sprint range | Trends workspace (`TrendsWorkspace.razor:319-325,611-779`), Delivery Trends (`DeliveryTrends.razor:312-318,435-449`), Portfolio Progress (`PortfolioProgressPage.razor:512-515,803-828`), Portfolio Delivery (`PortfolioDelivery.razor:450-461,603-615`). | Pair of `int?` sprint IDs, or derived `List<int>` / `int[]` sprint IDs. | URL query `fromSprintId` / `toSprintId` only in Trends workspace; downstream metrics APIs take expanded `sprintIds[]`. |
| Date range | PR Insights (`PrOverview.razor:609-610,693-705`), PR Delivery Insights (`PrDeliveryInsights.razor:736-737,815-828`), Health Overview window (`HealthOverviewPage.razor:143-150,189-193`), Trends workspace bug/pipeline signal window (`TrendsWorkspace.razor:324-325,732-751`). | `DateTime?` or `DateTimeOffset`. | Query `fromDate` / `toDate` on PR endpoints; build-quality rolling window uses `windowStartUtc` / `windowEndUtc`; most pages derive these internally instead of exposing a reusable time filter model. |
| Repository | PR Insights page (`PrOverview.razor:611-613,701-713`) and API surface for build-quality detail (`BuildQualityController.cs:49-60`). | `string?` repository name on PR page; `int? repositoryId` for build-quality detail endpoint. | PR Insights sends `repositoryName`; pipeline pages do not surface repository filters even though build-quality detail supports `repositoryId`. |
| Validation category | Validation Queue / Fix query strings (`ValidationQueuePage.razor:165-171,229-243`; `ValidationFixPage.razor:287-300,422-432`), Work Item Explorer query (`WorkItemExplorer.razor:94-120,203-225`). | Category key string (`SI`, `RR`, `RC`, `EFF`) in validation pages; numeric `validationCategory` in Work Item Explorer deep links. | Query `category` or `validationCategory`. |
| Validation rule | Validation Fix page (`ValidationFixPage.razor:287-322`). | `string _ruleId`. | Query `ruleId`. |
| Validation issue filters | Work Item Explorer (`ValidationFilter.cs:8-19`; `WorkItemExplorer.razor:135-162,343-369`). | `ValidationFilter` objects with `Id`, `Label`, `IsEnabled`, `Count`, `Category`. | `FilterId` in filtering API DTOs (`WorkItemFilteringService.cs:46-69`). |
| Text search | Work Item Explorer (`WorkItemExplorer.razor:129,306-362`) and Product Roadmap Editor (`ProductRoadmapEditor.razor:523-550,639-643`). | `string _filterText` / `string _searchText`. | In Work Item Explorer it is sent to `TreeBuilderService.FilterWithAncestors(...)` client-side after validation filtering; in Product Roadmap Editor it only filters the in-memory available-epics list. |
| Tag filters | Bugs Triage (`BugsTriage.razor:169-170,260-272,518-540`). | `List<string> _selectedTags`, `TagMatchMode _tagMatchMode`. | In-memory only, applied via `BugTreeBuilderService.ApplyTagFilters(...)`; not sent to the API. |
| Pipeline toggles | Pipeline Insights (`PipelineInsights.razor:566-573,730-739`). | `bool _includePartiallySucceeded`, `bool _includeCanceled`. | Query params `includePartiallySucceeded` and `includeCanceled` to `GET /api/pipelines/insights`. |
| SLO duration | Pipeline Insights (`PipelineInsights.razor:575,325-332`). | `double? _sloDurationMinutes`. | Not sent to any API; only used as a chart overlay. |
| Portfolio CDC filters | `PortfolioCdcReadOnlyPanel.razor:38-147,456-645`. | `int? _productId`, `string? _projectNumber`, `string? _workPackage`, `PortfolioLifecycleState? _lifecycleState`, `PortfolioReadSortBy`, `PortfolioReadGroupBy`, `int _snapshotCount`, `bool _includeArchivedSnapshots`, `long? _compareToSnapshotId`. | Typed query params through `ApiClient.PortfolioConsumption.cs:233-282` into `/api/portfolio/*` endpoints. |
| Root work item / all-products / all-teams deep-link flags | Work Item Explorer (`WorkItemExplorer.razor:98-120,227-235,264-281`). | `int? RootWorkItemId`, `bool AllProducts`, `bool AllTeams`. | URL query params; only `RootWorkItemId` and `AllProducts` alter loading behavior. `AllTeams` is explicitly “display/context only” and not implemented as real filtering (`WorkItemExplorer.razor:115-120`). |

## 3. Page-by-page analysis

### Home
- **Current filters:** product only via `_selectedProductId` (`HomePage.razor:267-269,616-625`).
- **Definition:** local component state.
- **Application:** not sent through a central filter service; it is pushed into navigation links with `BuildContextQuery()` (`HomePage.razor:599-614,717-740`) and into dashboard signal services via method parameters (`HomePage.razor:479-538`).
- **Centralization:** partially centralized for navigation, but duplicated because `HomePage` has its own `BuildContextQuery()` instead of reusing `WorkspaceBase`.

### Health workspace / overview
- **Health hub filters:** no local selectors; only inherited `productId` / `teamId` URL context (`HealthWorkspace.razor:102-137`).
- **Overview page filters:** fixed rolling 30-day window plus optional `ProductId` from `WorkspaceBase` (`HealthOverviewPage.razor:134-150,189-200`).
- **Application:** Build Quality overview calls `GetRollingWindowAsync(activeProfile.Id, _windowStartUtc, _windowEndUtc)` and only uses `ProductId` client-side for chip/highlight/order (`HealthOverviewPage.razor:189-200,226-236`).
- **Centralization:** context propagation is centralized in `WorkspaceBase`; product filtering of the actual dataset is not sent to the Build Quality endpoint.

### Delivery / Sprint pages

#### Sprint Execution
- **Current filters:** team, sprint, optional product (`SprintExecution.razor:433-447`).
- **Definition:** page-local state with popovers.
- **Application:** calls `GetSprintExecutionAsync(productOwnerId, sprintId, productId)` (`SprintExecution.razor:548-563`), so product does reach the backend here.
- **Centralization:** local and self-contained; default team/sprint selection logic is hardcoded on the page (`SprintExecution.razor:494-545`).

#### Portfolio Delivery
- **Current filters:** team, from sprint, to sprint (`PortfolioDelivery.razor:447-461`).
- **Definition:** page-local state.
- **Application:** team is only used to load the sprint list; backend call is `GetPortfolioDeliveryAsync(productOwnerId, _selectedSprintIds)` (`PortfolioDelivery.razor:571-590`).
- **Centralization:** duplicated range-building logic (`BuildSprintRange`) lives on the page (`PortfolioDelivery.razor:603-615`).

#### Delivery Trends
- **Current filters:** team, product, end sprint, sprint-count window (`DeliveryTrends.razor:309-318`).
- **Definition:** page-local state plus `teamId` / `productId` URL persistence (`DeliveryTrends.razor:551-571`).
- **Application:** final API call is only `GetSprintTrendMetricsAsync(productOwnerId, sprintIds, recompute:false)` (`DeliveryTrends.razor:423-449`). Team only narrows `_filteredSprints`; product is applied client-side in `GetMetricValue(...)` by selecting from `metric.ProductMetrics` (`DeliveryTrends.razor:395-405,527-541`).
- **Centralization:** duplicated and partly hardcoded; filter semantics differ from the visible UI because product is not a backend filter here.

### Trends / Portfolio

#### Trends workspace
- **Current filters:** optional product chip via `WorkspaceBase.ProductId`; team and sprint-range selectors (`TrendsWorkspace.razor:318-325,596-829`).
- **Application:** team and sprint range change the computed `_fromDate` / `_toDate` and tile signal loads (`TrendsWorkspace.razor:646-751,785-829`). Navigation forwards some context to detail pages, but not consistently: Product/Team are forwarded to backlog/trends pages with `BuildContextQuery()`, while Pipeline Insights and PR Delivery Insights are opened without that context (`TrendsWorkspace.razor:546-589`).
- **Centralization:** mixed; `WorkspaceBase` handles product/team context, while sprint range is bespoke to this page.

#### Portfolio Progress page
- **Current filters:** product, team, from sprint, to sprint, plus the embedded CDC panel’s project/work-package/lifecycle/history filters (`PortfolioProgressPage.razor:510-518`; `PortfolioCdcReadOnlyPanel.razor:38-147,456-645`).
- **Application:** main chart page sends only `productIds` and `sprintIds` (`PortfolioProgressPage.razor:758-782`). Team again only drives available sprints. The embedded CDC panel independently calls `/api/portfolio/progress`, `/snapshots`, `/comparison`, `/trends`, and `/signals` with its own filter model (`PortfolioCdcReadOnlyPanel.razor:516-573`).
- **Centralization:** fragmented: one page hosts two different portfolio filter systems side-by-side.

### Planning

#### Plan Board
- **Current filters:** single product selector (`PlanBoard.razor:67-79,427-429`).
- **Application:** loads one product’s roots, candidate tree, sprint columns, and capacity calibration (`PlanBoard.razor:498-565,969`). Product implicitly scopes sprints by using `product.TeamIds.First()` (`PlanBoard.razor:536-547`).
- **Centralization:** page-local and hardcoded; no URL persistence.

#### Product Roadmaps / Product Roadmap Editor
- **Product Roadmaps:** no interactive page filter; it loads all products for the active profile and renders one lane per product (`ProductRoadmaps.razor:107-120`).
- **Roadmap Editor:** route parameter `ProductId` plus local search text `_searchText` for available epics (`ProductRoadmapEditor.razor:511-523,544-550`).
- **Application:** search stays client-side only (`ProductRoadmapEditor.razor:639-643`).
- **Centralization:** none; search is isolated to this page.

### Pipeline Insights
- **Current filters:** team, sprint, include partial success, include canceled, SLO duration (`PipelineInsights.razor:563-577`).
- **Application:** team loads sprints and auto-selects a sprint (`PipelineInsights.razor:642-689`); backend call is `PipelinesClient.GetInsightsAsync(productOwnerId, sprintId, includePartiallySucceeded, includeCanceled)` (`PipelineInsights.razor:710-759`, `PipelinesController.cs:156-168`). Build Quality overlay/detail then calls `GetPipelineAsync(productOwnerId, sprintId, pipelineDefinitionId, repositoryId)` but the page only supplies `pipelineDefinitionId`, not `repositoryId` (`PipelineInsights.razor:920-999`; `BuildQualityController.cs:49-60`).
- **Centralization:** local; team is not a backend filter on the final insights call.

### PR Insights
- **Current filters:** team, sprint quick-select, repository, derived date range, plus local author/highlight UI filters (`PrOverview.razor:608-625`).
- **Application:** team selection loads sprints and auto-sets `_fromDate` / `_toDate` from the chosen sprint (`PrOverview.razor:776-817`). Backend call is `GetInsightsAsync(teamId, fromDate, toDate, repositoryName)` (`PrOverview.razor:689-719`; `PullRequestsController.cs:307-325`). Sprint is never sent directly; it only becomes a date range.
- **Centralization:** duplicated sprint-to-date mapping logic on the page; repository is a proper backend filter; author filtering is client-only on the scatter/table view (`PrOverview.razor:895-905`).

### PR Delivery Insights
- **Current filters:** team, sprint quick-select, derived date range (`PrDeliveryInsights.razor:733-739`).
- **Application:** same team/sprint defaulting pattern as PR Insights (`PrDeliveryInsights.razor:847-916`), then `GetDeliveryInsightsAsync(teamId, sprintId, fromDate, toDate)` (`PrDeliveryInsights.razor:811-843`; `PullRequestsController.cs:343-360`).
- **Centralization:** duplicated with PR Insights but slightly different because sprintId is also sent to the backend here.

### Backlog / Refinement

#### Backlog Health
- **Current filters:** product (`BacklogOverviewPage.razor:342-349`).
- **Application:** loads `GetBacklogStateAsync(productId)` and separately `GetValidationTriageSummaryAsync(new[] { productId })` for the SI maintenance count (`BacklogOverviewPage.razor:427-449`). Navigating into the explorer passes only `rootWorkItemId`, not the current product (`BacklogOverviewPage.razor:461-465`).
- **Centralization:** product scoping is local; deep-link propagation loses the selected product.

#### Validation Triage / Queue / Fix
- **Current filters:** product context plus category and rule drill-down (`ValidationTriagePage.razor:120-137`; `ValidationQueuePage.razor:165-203`; `ValidationFixPage.razor:287-322`).
- **Application:** all three pages rebuild `productIds` from either `ProductId` or all profile products, then call the corresponding validation API (`ValidationTriagePage.razor:125-137`; `ValidationQueuePage.razor:193-203`; `ValidationFixPage.razor:314-323`).
- **Centralization:** heavily duplicated; the same product-resolution and productIds-building pattern appears in all three pages.

#### Work Item Explorer / Bugs Triage
- **Work Item Explorer:** query-backed validation filters, root-focus deep links, `AllProducts`, non-functional `AllTeams`, plus text search (`WorkItemExplorer.razor:94-120,186-235,335-369`). Filtering uses the dedicated filtering service, then tree builders.
- **Bugs Triage:** tag chips and Any/All tag-match mode, all client-side (`BugsTriage.razor:260-282,518-540`).

## 4. Query flow and mapping patterns

### Delivery Trends
1. Reads `teamId` / `productId` from query string (`DeliveryTrends.razor:551-563`).
2. Loads all teams, all profile products, and all sprints across teams (`DeliveryTrends.razor:366-378`).
3. Uses team to derive `_filteredSprints` (`DeliveryTrends.razor:395-405`).
4. Expands the selected end sprint and sprint-count window into `sprintIds` (`DeliveryTrends.razor:435-440`).
5. Sends only `productOwnerId` and `sprintIds` to `/api/metrics/sprint-trend` (`DeliveryTrends.razor:445-449`; `MetricsController.cs:691-716`).
6. Applies the visible product filter after the response by selecting `metric.ProductMetrics` (`DeliveryTrends.razor:530-541`).
- **Duplication:** sprint-window expansion logic is page-local.
- **Missing/inconsistent filters:** visible product filter is not part of the API call; team is only an indirect sprint-list filter.

### Portfolio Progress
1. Reads `productId` and `teamId` from query string (`PortfolioProgressPage.razor:694-708`).
2. Loads team sprints and defaults a five-sprint window (`PortfolioProgressPage.razor:624-691`).
3. Converts the from/to selection into an ordered sprint ID list (`PortfolioProgressPage.razor:803-828`).
4. Sends `productOwnerId`, `sprintIds`, and optional `productIds[]` to `/api/metrics/portfolio-progress-trend` (`PortfolioProgressPage.razor:774-782`; `MetricsController.cs:735-759`).
5. Separately, the embedded CDC panel sends an unrelated filter set to `/api/portfolio/*` (`PortfolioCdcReadOnlyPanel.razor:516-573`; `MetricsController.cs:247-445`).
- **Duplication:** another custom sprint-range mapper.
- **Missing/inconsistent filters:** team is not sent to the backend; project/work-package/lifecycle exist only in the embedded CDC panel, not in the main page query.

### Pipeline Insights
1. Reads team selection from UI; no query-string persistence (`PipelineInsights.razor:71-138`).
2. Uses team to load sprints and auto-select current/most recent sprint (`PipelineInsights.razor:642-689`).
3. Sends `productOwnerId`, `sprintId`, `includePartiallySucceeded`, `includeCanceled` to `/api/pipelines/insights` (`PipelineInsights.razor:730-735`; `PipelinesController.cs:156-168`).
4. Uses `_sloDurationMinutes` only on the scatter component (`PipelineInsights.razor:325-332`).
- **Duplication:** current-sprint auto-selection logic is duplicated with PR pages.
- **Missing/inconsistent filters:** team never reaches the final API; repository filtering exists in build-quality detail API but not in the page.

### PR Insights
1. Parses only `teamId` from the URL (`PrOverview.razor:678-685`).
2. Team selection loads sprints and maps the chosen sprint to `_fromDate` / `_toDate` (`PrOverview.razor:776-817,819-835`).
3. Repository dropdown is populated from returned scatter points after loading (`PrOverview.razor:708-713`).
4. Sends `teamId`, derived `fromDate`, derived `toDate`, and `repositoryName` to `/api/PullRequests/insights` (`PrOverview.razor:701-706`; `ApiClient.PullRequestInsights.cs:60-89`).
- **Duplication:** sprint-to-date mapping duplicated with PR Delivery Insights.
- **Missing/inconsistent filters:** selected sprint is not a first-class API parameter on this endpoint; author filter is UI-only.

### PR Delivery Insights
1. Parses only `teamId` from the URL (`PrDeliveryInsights.razor:794-801`).
2. Team selection loads sprints and also derives `_fromDate` / `_toDate` from the selected sprint (`PrDeliveryInsights.razor:876-929`).
3. Sends `teamId`, `sprintId`, `fromDate`, `toDate` to `/api/PullRequests/delivery-insights` (`PrDeliveryInsights.razor:823-828`; `ApiClient.PrDeliveryInsights.cs:59-86`).
- **Duplication:** same team/sprint initialization logic as PR Insights.
- **Inconsistency:** this endpoint receives both `sprintId` and dates; PR Insights only receives dates.

### Validation flow
1. `ValidationTriagePage`, `ValidationQueuePage`, and `ValidationFixPage` all read `ProductId` from `WorkspaceBase` and rebuild `productIds` arrays from profile products (`ValidationTriagePage.razor:112-137`; `ValidationQueuePage.razor:180-203`; `ValidationFixPage.razor:306-323`).
2. The pages send `productIds` as comma-separated query strings via `WorkItemService` (`WorkItemService.cs:194-266`).
3. The backend parses `productIds` with `TryParseProductIds(...)` and rejects malformed formats (`WorkItemsController.cs:131-214,927-949`).
- **Duplication:** identical product-resolution and `productIds` array construction repeated on every validation page.
- **Inconsistent parameter naming:** frontend usually holds `ProductId` / `productIds`, while validation category/rule use `category` and `ruleId`; Work Item Explorer deep links use `validationCategory` instead.

### Backlog Health -> Work Item Explorer
1. Backlog Health loads one product (`BacklogOverviewPage.razor:427-449`).
2. Clicking an epic navigates to `/workitems?rootWorkItemId=...` without passing the selected product (`BacklogOverviewPage.razor:461-465`).
3. Work Item Explorer then reloads profile-scoped products or all products based on `AllProducts`, not the backlog page’s current product (`WorkItemExplorer.razor:238-281`).
- **Missing filter:** product context is dropped during this navigation.

### Invalid/unsupported filter handling
- `WorkItemsController.TryParseProductIds(...)` rejects malformed `productIds` strings (`WorkItemsController.cs:927-949`).
- Validation queue/fix reject missing `category` / `ruleId` (`WorkItemsController.cs:164-205`).
- Metrics endpoints reject empty `sprintIds` (`MetricsController.cs:701-704,744-747,780-783,815-818`).
- `PipelineService.GetRunsForProductsAsync(...)` validates the first product ID and ignores the rest because of a temporary client limitation (`PipelineService.cs:43-97`).
- `WorkItemExplorer.AllTeams` is declared but explicitly not implemented as team filtering (`WorkItemExplorer.razor:115-120`).

## 5. Existing validation / constraints

### Product ↔ Project constraints
- **Implemented:** only in the CDC read-only panel as independent filters: `_productId`, `_projectNumber`, `_workPackage`, `_lifecycleState` are all sent independently to `/api/portfolio/*` (`PortfolioCdcReadOnlyPanel.razor:456-573`; `MetricsController.cs:247-445`).
- **Enforcement:** none in the client; project/work-package entries are free text, with no product-based narrowing.
- **Gap:** no current code enforces a Product-to-Project relationship or validates that a project belongs to the selected product.

### Time handling
- **Trends workspace:** derives `_fromDate` / `_toDate` from selected sprint range or defaults to the last 6 months (`TrendsWorkspace.razor:732-751`).
- **PR Insights / PR Delivery Insights:** when a sprint is picked, both pages overwrite the date range with the sprint window (`PrOverview.razor:802-807,827-832`; `PrDeliveryInsights.razor:901-906,924-929`).
- **Pipeline Insights:** requires a sprint ID; team only exists to select that sprint (`PipelineInsights.razor:642-703`).
- **Delivery Trends / Portfolio Progress / Portfolio Delivery:** convert a selected end sprint or from/to pair into explicit sprint ID lists (`DeliveryTrends.razor:435-449`; `PortfolioProgressPage.razor:803-828`; `PortfolioDelivery.razor:603-615`).
- **Gap:** time is represented differently per page (sprint ID, sprint range, or explicit dates), with no single model.

### Team scoping
- **Direct backend team filter:** PR Insights and PR Delivery Insights send `teamId` to the backend (`PrOverview.razor:701-706`; `PrDeliveryInsights.razor:823-828`).
- **Indirect team filter:** Delivery Trends, Portfolio Progress, Portfolio Delivery, and Pipeline Insights use team only to choose sprints; the final metrics query is still driven by `productOwnerId` + sprint IDs (`DeliveryTrends.razor:395-405,445-449`; `PortfolioProgressPage.razor:744-782`; `PortfolioDelivery.razor:538-590`; `PipelineInsights.razor:642-735`).
- **Gap:** the meaning of team filter is inconsistent across pages.

## 6. Persistence behavior

| Filter / state | Persistence today |
|---|---|
| `productId`, `teamId` in Home workspace pages | Persisted in the URL only where pages inherit `WorkspaceBase` or manually rebuild URL parameters (`WorkspaceBase.cs:38-80`; `DeliveryTrends.razor:565-571`; `TrendsWorkspace.razor:756-779`). |
| Trends workspace sprint range | Persisted in URL as `fromSprintId` and `toSprintId` (`TrendsWorkspace.razor:756-779`). |
| Delivery Trends selected end sprint and sprint count | In-memory only; URL keeps only `teamId` and `productId` (`DeliveryTrends.razor:565-571`). |
| Portfolio Progress selected sprint range | In-memory only; no URL writeback for `fromSprintId` / `toSprintId` in this page (`PortfolioProgressPage.razor:694-708,803-828`). |
| PR pages team filter | Read from URL if present, but not written back after changes (`PrOverview.razor:678-685`; `PrDeliveryInsights.razor:794-801`). |
| PR repository filter | In-memory only (`PrOverview.razor:611-613,838-849`). |
| Pipeline toggles and SLO | In-memory only (`PipelineInsights.razor:566-577`). |
| Backlog Health product | In-memory only after initial context resolution; not pushed back to URL when changed (`BacklogOverviewPage.razor:372-425`). |
| Validation product/category/rule flow | Product persists through `BuildContextQuery()`; category and rule persist in the URL between drill-down pages (`ValidationTriagePage.razor:161-165`; `ValidationQueuePage.razor:229-243`; `ValidationFixPage.razor:337-349,422-432`). |
| Work Item Explorer validation deep links | Query params persist on reload (`WorkItemExplorer.razor:94-120,186-235`). |
| Bugs Triage tag filters | In-memory only; only expanded tree state persists in `localStorage` (`BugsTriage.razor:518-540,567-601`). |
| Work Item Explorer expanded tree state | `localStorage` only; actual filter selections do not persist (`WorkItemExplorer.razor:448-489`). |

## 7. Duplication and fragmentation

- **Duplicate product-to-`productIds` mapping:** Validation Triage, Queue, and Fix each fetch profile products, resolve `ProductId`, and build `int[]? productIds` in-page (`ValidationTriagePage.razor:112-137`; `ValidationQueuePage.razor:180-203`; `ValidationFixPage.razor:306-323`).
- **Duplicate team→sprints→auto-selected sprint logic:** Pipeline Insights, PR Insights, and PR Delivery Insights each implement the same current-sprint / most-recent-past-sprint fallback pattern (`PipelineInsights.razor:653-676`; `PrOverview.razor:786-808`; `PrDeliveryInsights.razor:886-907`).
- **Duplicate sprint-range expansion:** Delivery Trends, Portfolio Progress, Portfolio Delivery, and Trends workspace each turn UI sprint state into a concrete date or ID range with separate page logic (`DeliveryTrends.razor:435-440`; `PortfolioProgressPage.razor:803-828`; `PortfolioDelivery.razor:603-615`; `TrendsWorkspace.razor:732-751`).
- **Multiple query builders:** `WorkspaceBase.BuildContextQuery()`, `HomePage.BuildContextQuery()`, `DeliveryTrends.UpdateUrlParameters()`, and `TrendsWorkspace.UpdateSprintUrlParameters()` all hand-build query strings independently.
- **Inconsistent naming:** `productId` vs `productIds`; `category` vs `validationCategory`; `repositoryName` on PR Insights vs `repositoryId` on Build Quality; `windowStartUtc` / `windowEndUtc` vs `fromDate` / `toDate`; `projectNumber` and `workPackage` exist only in the CDC view.
- **Parallel portfolio filter systems:** Portfolio Progress has one sprint-driven filter model and one embedded snapshot/read-model filter model (`PortfolioProgressPage.razor:758-782`; `PortfolioCdcReadOnlyPanel.razor:516-573`).

## 8. Gap vs canonical filter model

Comparing only current behavior to the intended canonical concepts:

- **Global Product exists, but inconsistently.** It is propagated through Home/Workspace URLs (`WorkspaceBase.cs:38-80`; `HomePage.razor:599-614`) and used by some APIs, but several pages either keep it page-local (Backlog Health, Plan Board) or apply it only client-side (Delivery Trends).
- **Global Project does not exist in the main flow.** Project/work-package/lifecycle filters only appear inside `PortfolioCdcReadOnlyPanel` (`PortfolioCdcReadOnlyPanel.razor:38-147,456-645`) and are absent from the main Home/Health/Delivery/Planning pages.
- **Time is not canonical.** Current pages mix single sprint IDs, sprint ranges, and raw date ranges; different pages convert between them differently (`DeliveryTrends.razor:435-449`; `PrOverview.razor:693-705`; `TrendsWorkspace.razor:732-751`).
- **Page filters are mixed with global context.** Team behaves like a quasi-global selector on some pages but is only local sprint scaffolding on others.
- **No selected vs effective separation.** Examples: Delivery Trends shows a selected product, but the effective server query is still “all products for those sprint IDs” (`DeliveryTrends.razor:445-449,535-541`). PR Insights shows a selected sprint, but the API only receives derived dates (`PrOverview.razor:701-706,819-835`).
- **No valid vs invalid separation for most filters.** Validation pages check required `category` / `ruleId`, and work item product IDs are parsed server-side (`WorkItemsController.cs:164-205,927-949`), but most other pages assume selected values are valid if they parse or appear in loaded lists.
- **No applicable vs not-applicable layer.** Pages simply hide or ignore unsupported filters rather than tracking applicability. Example: `AllTeams` exists in Work Item Explorer but is documented as not implemented (`WorkItemExplorer.razor:115-120`).
- **Missing canonical page filters in places where APIs already support them.** The read-only portfolio APIs support `projectNumber`, `workPackage`, and `lifecycleState` (`MetricsController.cs:247-445`; `ApiClient.PortfolioConsumption.cs:233-282`), but the main portfolio trends/delivery pages do not use them.

## 9. Summary

- The current filter system is **fragmented and page-owned**. Most pages keep their own filter fields, defaulting rules, sprint selection logic, and query builders.
- The biggest migration risks are **semantic mismatches** rather than missing controls: the visible filter on a page is not always the filter sent to the backend. Product and team are the clearest examples.
- The most reusable pieces are the **typed API client helpers** (`WorkItemService`, PR client extensions, portfolio read-model client extension) and the **shared URL context parsing** in `WorkspaceBase`.
- The parts most likely to need replacement are the **duplicated page-local mapping layers**: repeated team→sprint selection, repeated sprint-range expansion, repeated product→`productIds` construction, and the coexistence of separate sprint-driven and snapshot-driven portfolio filter models on the same surface.
