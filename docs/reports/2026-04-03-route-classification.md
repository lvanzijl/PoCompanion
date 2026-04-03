# Route classification

Source inventory: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-03-route-inventory.md`.

Total classified routes: 38

## INVALID

None.

## WorkspaceHub

### HomePage
- Route path(s): `/home`
- Classification: `WorkspaceHub`
- Owner: Home system
- Entry path(s): `/`, `/sync-gate`, home navigation actions from routed pages
- Exit path: `/home/health`, `/home/delivery`, `/home/trends`, `/home/planning`, `/home/validation-triage`, `/bugs-triage`, `/home/changes`
- Context model: `product`

### HealthWorkspace
- Route path(s): `/home/health`
- Classification: `WorkspaceHub`
- Owner: Health workspace
- Entry path(s): `/home`, `WorkspaceNavigationCatalog`, health child breadcrumb links
- Exit path: `/home`, `/home/health/overview`, `/home/validation-triage`, `/home/health/backlog-health`, `/home/delivery`, `/home/trends`, `/home/planning`
- Context model: `product`

### DeliveryWorkspace
- Route path(s): `/home/delivery`
- Classification: `WorkspaceHub`
- Owner: Delivery workspace
- Entry path(s): `/home`, `WorkspaceNavigationCatalog`, delivery child breadcrumb links
- Exit path: `/home`, `/home/delivery/sprint`, `/home/delivery/execution`, `/home/delivery/portfolio`, `/home/health`, `/home/trends`, `/home/planning`, `/home/health/backlog-health`
- Context model: `team`

### TrendsWorkspace
- Route path(s): `/home/trends`
- Classification: `WorkspaceHub`
- Owner: Trends workspace
- Entry path(s): `/home`, `WorkspaceNavigationCatalog`, trends child breadcrumb links
- Exit path: `/home`, `/home/pull-requests`, `/home/pr-delivery-insights`, `/home/pipeline-insights`, `/home/portfolio-progress`, `/home/trends/delivery`, `/home/bugs`, `/home/health`, `/home/planning`, `/home/delivery`, `/home/health/backlog-health`
- Context model: `time`

### PlanningWorkspace
- Route path(s): `/home/planning`
- Classification: `WorkspaceHub`
- Owner: Planning workspace
- Entry path(s): `/home`, `WorkspaceNavigationCatalog`, planning child breadcrumb links
- Exit path: `/home`, `/planning/product-roadmaps`, `/planning/multi-product`, `/planning/plan-board`, `/planning/{ProjectAliasRoute}/overview`, `/home/health`, `/home/trends`, `/home/delivery`, `/home/health/backlog-health`
- Context model: `product`

## WorkspaceChild

### HealthOverviewPage
- Route path(s): `/home/health/overview`
- Classification: `WorkspaceChild`
- Owner: Health workspace
- Entry path(s): `/home/health`
- Exit path: `/home/health`
- Context model: `product`

### BacklogOverviewPage
- Route path(s): `/home/health/backlog-health`, `/home/backlog-overview`
- Classification: `WorkspaceChild`
- Owner: Health workspace
- Entry path(s): `/home/health`, `/home/delivery`, `/home/trends`, `/home/planning`, product summary card deep links
- Exit path: `/home/health`, `/home/validation-queue`, `/workitems`, `/home/trends`, `/home/planning`
- Context model: `product`

### BugOverview
- Route path(s): `/home/bugs`
- Classification: `WorkspaceChild`
- Owner: Health workspace
- Entry path(s): `/home/trends`
- Exit path: `/bugs-triage`, `/home`
- Context model: `product`

### SprintTrend
- Route path(s): `/home/sprint-trend`, `/home/delivery/sprint`
- Classification: `WorkspaceChild`
- Owner: Delivery workspace
- Entry path(s): `/home/delivery`, `/home/changes`
- Exit path: `/home/delivery`, `/home/delivery/sprint/activity/{WorkItemId:int}`, `/home`, `/home/trends`
- Context model: `team`

### SprintExecution
- Route path(s): `/home/delivery/execution`
- Classification: `WorkspaceChild`
- Owner: Delivery workspace
- Entry path(s): `/home/delivery`
- Exit path: `/home/delivery`, `/home`
- Context model: `team`

### PortfolioDelivery
- Route path(s): `/home/delivery/portfolio`
- Classification: `WorkspaceChild`
- Owner: Delivery workspace
- Entry path(s): `/home/delivery`
- Exit path: `/home/delivery`, `/home`
- Context model: `time`

### DeliveryTrends
- Route path(s): `/home/trends/delivery`
- Classification: `WorkspaceChild`
- Owner: Trends workspace
- Entry path(s): `/home/trends`
- Exit path: `/home/trends`, `/home`
- Context model: `time`

### PrOverview
- Route path(s): `/home/pull-requests`
- Classification: `WorkspaceChild`
- Owner: Trends workspace
- Entry path(s): `/home/trends`
- Exit path: `/home/trends`, `/home`
- Context model: `team`

### PrDeliveryInsights
- Route path(s): `/home/pr-delivery-insights`
- Classification: `WorkspaceChild`
- Owner: Trends workspace
- Entry path(s): `/home/trends`
- Exit path: `/home/trends`, `/home`
- Context model: `team`

### PipelineInsights
- Route path(s): `/home/pipeline-insights`
- Classification: `WorkspaceChild`
- Owner: Trends workspace
- Entry path(s): `/home/trends`
- Exit path: `/home/trends`, `/home`
- Context model: `team`

### PortfolioProgressPage
- Route path(s): `/home/portfolio-progress`
- Classification: `WorkspaceChild`
- Owner: Trends workspace
- Entry path(s): `/home/trends`
- Exit path: `/home/trends`, `/home`
- Context model: `time`

### ProductRoadmaps
- Route path(s): `/planning/product-roadmaps`, `/planning/{RouteProjectAlias}/product-roadmaps`
- Classification: `WorkspaceChild`
- Owner: Planning workspace
- Entry path(s): `/home/planning`, `/planning/{ProjectAliasRoute}/overview`, `/planning/product-roadmaps/{ProductId:int}`
- Exit path: `/home/planning`, `/planning/product-roadmaps/{ProductId:int}`, `/planning/{ProjectAliasRoute}/overview`, `/home`
- Context model: `product`

### MultiProductPlanning
- Route path(s): `/planning/multi-product`
- Classification: `WorkspaceChild`
- Owner: Planning workspace
- Entry path(s): `/home/planning`
- Exit path: `/home/planning`, `/home`
- Context model: `product`

### PlanBoard
- Route path(s): `/planning/plan-board`, `/planning/{RouteProjectAlias}/plan-board`
- Classification: `WorkspaceChild`
- Owner: Planning workspace
- Entry path(s): `/home/planning`, `/planning/{ProjectAliasRoute}/overview`
- Exit path: `/home/planning`, `/planning/{ProjectAliasRoute}/overview`, `/home`
- Context model: `product`

## TaskFlow

### ValidationTriagePage
- Route path(s): `/home/validation-triage`
- Classification: `TaskFlow`
- Owner: Health workspace
- Entry path(s): `/home`, `/home/health`, validation return navigation
- Exit path: `/home/validation-queue`, `/home/health`
- Context model: `product`

### ValidationQueuePage
- Route path(s): `/home/validation-queue`
- Classification: `TaskFlow`
- Owner: Health workspace
- Entry path(s): `/home/validation-triage`, `/home/health/backlog-health`, validation return navigation
- Exit path: `/home/validation-fix`, `/home/validation-triage`
- Context model: `product`

### ValidationFixPage
- Route path(s): `/home/validation-fix`
- Classification: `TaskFlow`
- Owner: Health workspace
- Entry path(s): `/home/validation-queue`
- Exit path: `/home/validation-queue`, `/home/validation-triage`
- Context model: `product`

### BugsTriage
- Route path(s): `/bugs-triage`
- Classification: `TaskFlow`
- Owner: Bug triage system
- Entry path(s): `/home`, `/home/bugs`
- Exit path: `/home`
- Context model: `none`

### Onboarding
- Route path(s): `/onboarding`
- Classification: `TaskFlow`
- Owner: Startup system
- Entry path(s): `/`
- Exit path: `/sync-gate?returnUrl=%2Fhome`
- Context model: `none`

### ProfilesHome
- Route path(s): `/profiles`
- Classification: `TaskFlow`
- Owner: Profile system
- Entry path(s): `/`, `/sync-gate`, profile selector navigation, profile-management return navigation
- Exit path: `/sync-gate?returnUrl=...`, `/settings/productowner/edit/{ProfileId:int?}`, `/settings/teams`
- Context model: `none`

## Utility

### SettingsPage
- Route path(s): `/settings`, `/settings/{SelectedTopic}`
- Classification: `Utility`
- Owner: Settings system
- Entry path(s): top-right settings navigation, startup redirects, profiles page settings links
- Exit path: settings topic routes, browser/app navigation away from settings
- Context model: `none`

### ManageTeams
- Route path(s): `/settings/teams`
- Classification: `Utility`
- Owner: Profile system
- Entry path(s): `/profiles`
- Exit path: `/profiles`
- Context model: `none`

### ManageProductOwner
- Route path(s): `/settings/productowner/{ProfileId:int}`
- Classification: `Utility`
- Owner: Profile system
- Entry path(s): `/profiles`, `/settings/productowner/edit/{ProfileId:int?}`
- Exit path: `/profiles`, `/settings/productowner/edit/{ProfileId:int}`, `/sync-gate`
- Context model: `none`

### EditProductOwner
- Route path(s): `/settings/productowner/edit/{ProfileId:int?}`
- Classification: `Utility`
- Owner: Profile system
- Entry path(s): `/profiles`, `/settings/productowner/{ProfileId:int}`
- Exit path: `/settings/productowner/{ProfileId:int}`, `/profiles`
- Context model: `none`

### WorkItemStates
- Route path(s): `/settings/workitem-states`
- Classification: `Utility`
- Owner: Settings system
- Entry path(s): `/settings`
- Exit path: `/settings`
- Context model: `none`

### SyncGate
- Route path(s): `/sync-gate`
- Classification: `Utility`
- Owner: Startup system
- Entry path(s): `/`, `/onboarding`, `/profiles`, `/settings/productowner/{ProfileId:int}`
- Exit path: validated `returnUrl`, `/home`, `/profiles`, `/settings/cache`, `/settings/tfs`
- Context model: `none`

### HomeChanges
- Route path(s): `/home/changes`
- Classification: `Utility`
- Owner: Home system
- Entry path(s): `/home`
- Exit path: `/home/sprint-trend`, `/home/health`, `/home`
- Context model: `product`

### Index
- Route path(s): `/`
- Classification: `Utility`
- Owner: Startup system
- Entry path(s): direct application entry
- Exit path: `/onboarding`, `/settings/tfs`, `/profiles`, `/sync-gate?returnUrl=%2Fhome`
- Context model: `none`

### NotFound
- Route path(s): `/not-found`
- Classification: `Utility`
- Owner: Routing system
- Entry path(s): router not-found handling
- Exit path: none found
- Context model: `none`

## DeepLinkOnly

### WorkItemExplorer
- Route path(s): `/workitems`
- Classification: `DeepLinkOnly`
- Owner: Work item system
- Entry path(s): `/home/health/backlog-health`, release-planning deep links
- Exit path: none found
- Context model: `none`

### ProjectPlanningOverview
- Route path(s): `/planning/{ProjectAliasRoute}/overview`
- Classification: `DeepLinkOnly`
- Owner: Planning workspace
- Entry path(s): `/home/planning`, `/planning/product-roadmaps`, `/planning/plan-board`
- Exit path: `/planning/product-roadmaps`, `/planning/plan-board`, `/home/planning`
- Context model: `product`

### ProductRoadmapEditor
- Route path(s): `/planning/product-roadmaps/{ProductId:int}`
- Classification: `DeepLinkOnly`
- Owner: Planning workspace
- Entry path(s): `/planning/product-roadmaps`, `/planning/{RouteProjectAlias}/product-roadmaps`
- Exit path: `/planning/product-roadmaps`, `/planning/{RouteProjectAlias}/product-roadmaps`, `/home/planning`
- Context model: `product`

### SprintTrendActivity
- Route path(s): `/home/sprint-trend/activity/{WorkItemId:int}`, `/home/delivery/sprint/activity/{WorkItemId:int}`
- Classification: `DeepLinkOnly`
- Owner: Delivery workspace
- Entry path(s): `/home/sprint-trend`, `/home/delivery/sprint`
- Exit path: `/home/delivery/sprint`, `/home/delivery`
- Context model: `time`
