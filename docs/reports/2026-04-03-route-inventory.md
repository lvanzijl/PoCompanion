# Route inventory

Total routed components found: 38

Scan basis: `@page` directives under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client`.

## WorkItemExplorer
- File: `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
- Route path(s): `/workitems`
- Query parameters used: `validationCategory`, `rootWorkItemId`, `allProducts`, `allTeams`
- Inbound navigation references:
  - BacklogOverview root-work-item navigation (`PoTool.Client/Pages/Home/BacklogOverviewPage.razor`)
  - ReleasePlanningBoard selected-item navigation (`PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## BugsTriage
- File: `PoTool.Client/Pages/BugsTriage.razor`
- Route path(s): `/bugs-triage`
- Query parameters used: none found
- Inbound navigation references:
  - Home quick action (`PoTool.Client/Pages/Home/HomePage.razor`)
  - Bug Overview navigation (`PoTool.Client/Pages/Home/BugOverview.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## BacklogOverviewPage
- File: `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
- Route path(s): `/home/health/backlog-health`, `/home/backlog-overview`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Health workspace navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`)
  - cross-workspace navigation (`PoTool.Client/Pages/Home/DeliveryWorkspace.razor`, `PoTool.Client/Pages/Home/PlanningWorkspace.razor`, `PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
  - Health product summary card (`PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## BugOverview
- File: `PoTool.Client/Pages/Home/BugOverview.razor`
- Route path(s): `/home/bugs`
- Query parameters used: `teamId`, `productId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Trends workspace navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
  - self URL updates in `PoTool.Client/Pages/Home/BugOverview.razor`
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## DeliveryTrends
- File: `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- Route path(s): `/home/trends/delivery`
- Query parameters used: `teamId`, `productId`
- Inbound navigation references:
  - Trends workspace navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
  - self URL updates in `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## DeliveryWorkspace
- File: `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`
- Route path(s): `/home/delivery`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Home workspace tile (`PoTool.Client/Pages/Home/HomePage.razor`)
  - cross-workspace and local navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`, `PoTool.Client/Pages/Home/PlanningWorkspace.razor`, `PoTool.Client/Pages/Home/SprintTrend.razor`, `PoTool.Client/Pages/Home/SprintExecution.razor`, `PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
  - breadcrumb links from delivery detail pages
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: Yes
- Breadcrumbs: Yes
- Local UI navigation: Yes

## HealthOverviewPage
- File: `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- Route path(s): `/home/health/overview`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Health workspace navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`)
  - self navigation in `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## HealthWorkspace
- File: `PoTool.Client/Pages/Home/HealthWorkspace.razor`
- Route path(s): `/home/health`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Home workspace tile (`PoTool.Client/Pages/Home/HomePage.razor`)
  - cross-workspace and local navigation buttons (`PoTool.Client/Pages/Home/HealthWorkspace.razor`, `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`, `PoTool.Client/Pages/Home/PlanningWorkspace.razor`, `PoTool.Client/Pages/Home/TrendsWorkspace.razor`, `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `PoTool.Client/Pages/Home/ValidationTriagePage.razor`)
  - breadcrumb links from health/validation/planning pages
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: Yes
- Breadcrumbs: Yes
- Local UI navigation: Yes

## HomeChanges
- File: `PoTool.Client/Pages/Home/HomeChanges.razor`
- Route path(s): `/home/changes`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - Home page “What's new since sync” link (`PoTool.Client/Pages/Home/HomePage.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## HomePage
- File: `PoTool.Client/Pages/Home/HomePage.razor`
- Route path(s): `/home`
- Query parameters used: `productId`
- Inbound navigation references:
  - valid-profile redirect target from `/` and sync gate (`PoTool.Client/Pages/Index.razor`, `PoTool.Client/Pages/SyncGate.razor`)
  - main layout home button (`PoTool.Client/Layout/MainLayout.razor`)
  - breadcrumb/home actions across Home pages
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## MultiProductPlanning
- File: `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
- Route path(s): `/planning/multi-product`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Planning workspace navigation (`PoTool.Client/Pages/Home/PlanningWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PipelineInsights
- File: `PoTool.Client/Pages/Home/PipelineInsights.razor`
- Route path(s): `/home/pipeline-insights`
- Query parameters used: `teamId`, `sprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Trends workspace pipeline tile navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
  - self URL updates in `PoTool.Client/Pages/Home/PipelineInsights.razor`
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PlanBoard
- File: `PoTool.Client/Pages/Home/PlanBoard.razor`
- Route path(s): `/planning/plan-board`, `/planning/{RouteProjectAlias}/plan-board`
- Query parameters used: `projectAlias`, `productId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Planning workspace navigation (`PoTool.Client/Pages/Home/PlanningWorkspace.razor`)
  - ProjectPlanningOverview local navigation (`PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PlanningWorkspace
- File: `PoTool.Client/Pages/Home/PlanningWorkspace.razor`
- Route path(s): `/home/planning`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Home workspace tile (`PoTool.Client/Pages/Home/HomePage.razor`)
  - cross-workspace and local navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`, `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`, `PoTool.Client/Pages/Home/TrendsWorkspace.razor`, `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `PoTool.Client/Pages/Home/MultiProductPlanning.razor`, `PoTool.Client/Pages/Home/ProductRoadmaps.razor`, `PoTool.Client/Pages/Home/PlanBoard.razor`, `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`, `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: Yes
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PortfolioDelivery
- File: `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- Route path(s): `/home/delivery/portfolio`
- Query parameters used: `teamId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - Delivery workspace navigation (`PoTool.Client/Pages/Home/DeliveryWorkspace.razor`)
  - self breadcrumb/local route building in `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PortfolioProgressPage
- File: `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- Route path(s): `/home/portfolio-progress`
- Query parameters used: `productId`, `teamId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Trends workspace navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PrDeliveryInsights
- File: `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- Route path(s): `/home/pr-delivery-insights`
- Query parameters used: `teamId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Trends workspace PR delivery tile navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## PrOverview
- File: `PoTool.Client/Pages/Home/PrOverview.razor`
- Route path(s): `/home/pull-requests`
- Query parameters used: `teamId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Trends workspace PR tile navigation (`PoTool.Client/Pages/Home/TrendsWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ProductRoadmapEditor
- File: `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
- Route path(s): `/planning/product-roadmaps/{ProductId:int}`
- Query parameters used: `projectAlias`
- Inbound navigation references:
  - ProductRoadmaps editor navigation (`PoTool.Client/Pages/Home/ProductRoadmaps.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ProductRoadmaps
- File: `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- Route path(s): `/planning/product-roadmaps`, `/planning/{RouteProjectAlias}/product-roadmaps`
- Query parameters used: `projectAlias`, `productId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Planning workspace navigation (`PoTool.Client/Pages/Home/PlanningWorkspace.razor`)
  - ProductRoadmapEditor and ProjectPlanningOverview return links (`PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`, `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ProjectPlanningOverview
- File: `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`
- Route path(s): `/planning/{ProjectAliasRoute}/overview`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - Planning workspace project navigation (`PoTool.Client/Pages/Home/PlanningWorkspace.razor`)
  - ProductRoadmaps and PlanBoard breadcrumb/local navigation (`PoTool.Client/Pages/Home/ProductRoadmaps.razor`, `PoTool.Client/Pages/Home/PlanBoard.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## SprintExecution
- File: `PoTool.Client/Pages/Home/SprintExecution.razor`
- Route path(s): `/home/delivery/execution`
- Query parameters used: none found
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Delivery workspace navigation (`PoTool.Client/Pages/Home/DeliveryWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## SprintTrend
- File: `PoTool.Client/Pages/Home/SprintTrend.razor`
- Route path(s): `/home/sprint-trend`, `/home/delivery/sprint`
- Query parameters used: none found
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - HomeChanges navigation (`PoTool.Client/Pages/Home/HomeChanges.razor`)
  - Delivery workspace sprint-delivery navigation (`PoTool.Client/Pages/Home/DeliveryWorkspace.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## SprintTrendActivity
- File: `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- Route path(s): `/home/sprint-trend/activity/{WorkItemId:int}`, `/home/delivery/sprint/activity/{WorkItemId:int}`
- Query parameters used: `productOwnerId`, `periodStartUtc`, `periodEndUtc`, `view`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - drill-down from `PoTool.Client/Pages/Home/SprintTrend.razor`
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## TrendsWorkspace
- File: `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- Route path(s): `/home/trends`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Home workspace tile (`PoTool.Client/Pages/Home/HomePage.razor`)
  - cross-workspace and local navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`, `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`, `PoTool.Client/Pages/Home/PlanningWorkspace.razor`, `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`, `PoTool.Client/Pages/Home/DeliveryTrends.razor`)
  - breadcrumb links from PR/Pipeline/DeliveryTrends pages
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: Yes
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ValidationFixPage
- File: `PoTool.Client/Pages/Home/ValidationFixPage.razor`
- Route path(s): `/home/validation-fix`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`, `category`, `ruleId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Validation Queue rule navigation (`PoTool.Client/Pages/Home/ValidationQueuePage.razor`)
  - self URL refresh in `PoTool.Client/Pages/Home/ValidationFixPage.razor`
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ValidationQueuePage
- File: `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
- Route path(s): `/home/validation-queue`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`, `category`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Validation Triage category navigation (`PoTool.Client/Pages/Home/ValidationTriagePage.razor`)
  - Backlog Overview queue navigation (`PoTool.Client/Pages/Home/BacklogOverviewPage.razor`)
  - Validation Fix back-navigation (`PoTool.Client/Pages/Home/ValidationFixPage.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## ValidationTriagePage
- File: `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
- Route path(s): `/home/validation-triage`
- Query parameters used: `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId`
- Inbound navigation references:
  - `WorkspaceNavigationCatalog` active prefixes (`PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`)
  - Home quick action (`PoTool.Client/Pages/Home/HomePage.razor`)
  - Health workspace navigation (`PoTool.Client/Pages/Home/HealthWorkspace.razor`)
  - return navigation from validation queue/fix pages (`PoTool.Client/Pages/Home/ValidationQueuePage.razor`, `PoTool.Client/Pages/Home/ValidationFixPage.razor`)
- WorkspaceNavigationCatalog: Yes
- Home hub tiles: No
- Breadcrumbs: Yes
- Local UI navigation: Yes

## Index
- File: `PoTool.Client/Pages/Index.razor`
- Route path(s): `/`
- Query parameters used: none found
- Inbound navigation references:
  - direct entry route; routed by the application router (`PoTool.Client/Pages/Index.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: No

## NotFound
- File: `PoTool.Client/Pages/NotFound.razor`
- Route path(s): `/not-found`
- Query parameters used: none found
- Inbound navigation references:
  - router not-found page registration (`PoTool.Client/App.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: No

## Onboarding
- File: `PoTool.Client/Pages/Onboarding.razor`
- Route path(s): `/onboarding`
- Query parameters used: none found
- Inbound navigation references:
  - startup redirect from `/` (`PoTool.Client/Pages/Index.razor`)
  - getting-started settings action (`PoTool.Client/Components/Settings/GettingStartedSection.razor`)
  - startup guard allowed-route list (`PoTool.Client/Components/Common/StartupGuard.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## ProfilesHome
- File: `PoTool.Client/Pages/ProfilesHome.razor`
- Route path(s): `/profiles`
- Query parameters used: `returnUrl`
- Inbound navigation references:
  - startup redirect from `/` (`PoTool.Client/Pages/Index.razor`)
  - sync/profile guard redirects (`PoTool.Client/Pages/SyncGate.razor`, `PoTool.Client/Components/Common/StartupGuard.razor`)
  - profile selector navigation (`PoTool.Client/Components/Settings/ProfileSelector.razor`)
  - return-to-profiles navigation from settings pages (`PoTool.Client/Pages/Settings/EditProductOwner.razor`, `PoTool.Client/Pages/Settings/ManageProductOwner.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## EditProductOwner
- File: `PoTool.Client/Pages/Settings/EditProductOwner.razor`
- Route path(s): `/settings/productowner/edit/{ProfileId:int?}`
- Query parameters used: none found
- Inbound navigation references:
  - Profiles page add-profile action (`PoTool.Client/Pages/ProfilesHome.razor`)
  - ManageProductOwner edit navigation (`PoTool.Client/Pages/Settings/ManageProductOwner.razor`)
  - startup guard allowed-route list (`PoTool.Client/Components/Common/StartupGuard.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## ManageProductOwner
- File: `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
- Route path(s): `/settings/productowner/{ProfileId:int}`
- Query parameters used: none found
- Inbound navigation references:
  - profile tile detail navigation (`PoTool.Client/Components/Settings/ProfileTile.razor`)
  - EditProductOwner post-save / cancel navigation (`PoTool.Client/Pages/Settings/EditProductOwner.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## ManageTeams
- File: `PoTool.Client/Pages/Settings/ManageTeams.razor`
- Route path(s): `/settings/teams`
- Query parameters used: none found
- Inbound navigation references:
  - Profiles page manage-teams action (`PoTool.Client/Pages/ProfilesHome.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## WorkItemStates
- File: `PoTool.Client/Pages/Settings/WorkItemStates.razor`
- Route path(s): `/settings/workitem-states`
- Query parameters used: none found
- Inbound navigation references:
  - settings work-item-state section button (`PoTool.Client/Components/Settings/WorkItemStateMappingSection.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## SettingsPage
- File: `PoTool.Client/Pages/SettingsPage.razor`
- Route path(s): `/settings`, `/settings/{SelectedTopic}`
- Query parameters used: none found
- Inbound navigation references:
  - top-right settings button (`PoTool.Client/Layout/MainLayout.razor`)
  - startup redirect to settings topics (`PoTool.Client/Pages/Index.razor`, `PoTool.Client/Components/Common/StartupGuard.razor`, `PoTool.Client/Pages/SyncGate.razor`)
  - profiles page TFS/settings links (`PoTool.Client/Pages/ProfilesHome.razor`)
  - settings topic navigation inside `PoTool.Client/Pages/SettingsPage.razor`
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes

## SyncGate
- File: `PoTool.Client/Pages/SyncGate.razor`
- Route path(s): `/sync-gate`
- Query parameters used: `returnUrl`
- Inbound navigation references:
  - profile selection flow builds `/sync-gate?returnUrl=...` (`PoTool.Client/Pages/ProfilesHome.razor`)
  - post-onboarding redirect (`PoTool.Client/Pages/Onboarding.razor`)
  - startup redirect from `/` (`PoTool.Client/Pages/Index.razor`)
  - manage-product-owner re-entry (`PoTool.Client/Pages/Settings/ManageProductOwner.razor`)
  - startup guard allowed-route list (`PoTool.Client/Components/Common/StartupGuard.razor`)
- WorkspaceNavigationCatalog: No
- Home hub tiles: No
- Breadcrumbs: No
- Local UI navigation: Yes
