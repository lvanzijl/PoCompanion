# Navigation Discoverability Audit

## Executive summary

The modern, visible navigation model is concentrated in the top workspace bar and the home dashboard: `MainLayout` renders `WorkspaceNavigationBar`, and `WorkspaceNavigationCatalog` exposes only **Health**, **Delivery**, **Trends**, and **Planning** as first-class workspaces (`PoTool.Client/Layout/MainLayout.razor:23-55`, `PoTool.Client/Components/Common/WorkspaceNavigationBar.razor:5-12`, `PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs:19-73`, `PoTool.Client/Pages/Home/HomePage.razor:175-240`).

From that model:

- Most modern workspace pages are discoverable through hub tiles, page-local buttons, or breadcrumbs.
- **Work Item Explorer (`/workitems`) is not part of normal navigation.** It remains reachable only through two surviving local entry points plus manual deep links.
- **Dependency Overview (`/home/dependencies`) is the clearest “existing but not normally discoverable” page.** It is present in the route set and even listed under the Trends workspace active-route catalog, but no visible modern navigation links to it.
- Several pages are residual or inconsistent rather than truly integrated:
  - legacy workspace cluster: `/legacy`, `/workspace/*`
  - legacy aliases: `/home/backlog-overview`, `/home/sprint-trend`, `/home/sprint-trend/activity/{id}`
  - orphan/detail routes: `/home/bugs/detail`, `/settings/products`
  - startup/system routes: `/`, `/onboarding`, `/sync-gate`, `/not-found`
- There are also surviving visible links to **non-routable** pages such as `/dependency-graph`, `/velocity`, `/state-timeline`, `/release-planning`, `/epic-forecast`, `/pr-insights`, `/pipeline-insights`, `/effort-distribution`, and `/help`, mostly in legacy UI and one current component (`DependencyOverview`, `ValidationSummaryPanel`).

## Discoverability matrix for routes

| Route(s) | Component | Primary navigation path | Secondary path(s) | Normal discoverable | Residually reachable only | Reachability category | Notes |
|---|---|---|---|---|---|---|---|
| `/` | `Pages/Index.razor` | none | startup entry only | No | Yes | startup redirect only | Immediately redirects to onboarding, profiles, settings topic, or sync gate (`Pages/Index.razor:12-61`). |
| `/home` | `Pages/Home/HomePage.razor` | top home button; sync-gate return; startup flow | n/a | Yes | No | main workspace/home entry | Central dashboard (`Layout/MainLayout.razor:25-30`, `Pages/SyncGate.razor:371-379`). |
| `/home/health` | `Pages/Home/HealthWorkspace.razor` | top workspace nav; home Health tile | cross-workspace buttons | Yes | No | main workspace/hub navigation | Visible in main nav and home tiles (`WorkspaceNavigationCatalog.cs:21-36`, `HomePage.razor:177-216`). |
| `/home/health/overview` | `Pages/Home/HealthOverviewPage.razor` | Health hub → Overview tile | breadcrumb return within Health | Yes | No | related detail page | Entered from Health hub (`HealthWorkspace.razor:42-67,109-112`). |
| `/home/health/backlog-health` | `Pages/Home/BacklogOverviewPage.razor` | Health hub → Backlog Health tile | Health summary card deep link with `productId` | Yes | No | related detail page | Also linked from `HealthProductSummaryCard` (`HealthWorkspace.razor:59-66,119-122`, `Pages/Home/SubComponents/HealthProductSummaryCard.razor:168-171`). |
| `/home/backlog-overview` | `Pages/Home/BacklogOverviewPage.razor` | none | bookmarks/direct URL only | No | Yes | URL/manual deep link | Kept as legacy alias (`WorkspaceRoutes.cs:204-209`); no visible link found to the alias. |
| `/home/validation-triage` | `Pages/Home/ValidationTriagePage.razor` | Health hub → Validation Triage tile | Home quick action | Yes | No | related detail page | (`HealthWorkspace.razor:51-58,114-117`, `HomePage.razor:224-239,757`). |
| `/home/validation-queue?category=...` | `Pages/Home/ValidationQueuePage.razor` | Validation Triage → “Open queue” cards | Backlog Health structural-integrity CTA | Yes | No | related detail page; query-state required | Bare route is recoverable but not meaningful without `category` (`ValidationTriagePage.razor:75-104,181-185`, `BacklogOverviewPage.razor:467-470`, `ValidationQueuePage.razor:194-203,278-295`). |
| `/home/validation-fix?category=...&ruleId=...` | `Pages/Home/ValidationFixPage.razor` | Validation Queue → “Start fix session” | direct URL with valid query state | Yes | No | related detail page; query-state required | Bare route falls into recovery flow (`ValidationQueuePage.razor:145-152,262-266`, `ValidationFixPage.razor:316-331`). |
| `/home/bugs` | `Pages/Home/BugOverview.razor` | Trends hub → Bug Trend tile | Bug trend chart click | Yes | No | related detail page | (`TrendsWorkspace.razor:147-173,517-535`). |
| `/home/bugs/detail?bugId=...` | `Pages/Home/BugDetail.razor` | none found | direct URL only | No | Yes | URL/manual deep link | Route exists, breadcrumbs/outbound links exist, but no inbound navigation to it was found; page itself requires `bugId` query state (`BugDetail.razor:199-227,269-301`). |
| `/bugs-triage` | `Pages/BugsTriage.razor` | Home quick action | Bug Insights CTA; Bug Detail CTA | Partly | No | related tool page outside workspace model | Visible, but not surfaced in main workspace navigation (`HomePage.razor:224-239`, `BugOverview.razor:180-207,434-437`, `BugDetail.razor:114-117,294-297`). |
| `/home/delivery` | `Pages/Home/DeliveryWorkspace.razor` | top workspace nav; home Delivery tile | cross-workspace buttons | Yes | No | main workspace/hub navigation | (`WorkspaceNavigationCatalog.cs:37-48`, `HomePage.razor:187-216`). |
| `/home/delivery/sprint` | `Pages/Home/SprintTrend.razor` | Delivery hub → Sprint Delivery tile | HomeChanges “View Sprint Trend”; direct canonical link | Yes | No | related detail page | Canonical Delivery page (`DeliveryWorkspace.razor:44-68,120-123`, `HomeChanges.razor:288-295`). |
| `/home/sprint-trend` | `Pages/Home/SprintTrend.razor` | none in modern hubs | bookmark/direct URL; `VelocityPanel` link | No | Yes | legacy alias | Alias retained for backward compatibility (`WorkspaceRoutes.cs:155-163`, `Components/Velocity/VelocityPanel.razor:7-9`). |
| `/home/delivery/sprint/activity/{WorkItemId:int}` | `Pages/Home/SprintTrendActivity.razor` | Sprint Delivery activity-history buttons | direct URL | Yes | No | related detail page | Generated by `BuildWorkItemActivityUrl` (`SprintTrend.razor:661-667,1681-1689`). |
| `/home/sprint-trend/activity/{WorkItemId:int}` | `Pages/Home/SprintTrendActivity.razor` | none in modern hubs | direct URL/bookmark | No | Yes | legacy alias | Alias route only; canonical links point to `/home/delivery/sprint/activity/{id}`. |
| `/home/delivery/execution` | `Pages/Home/SprintExecution.razor` | Delivery hub → Sprint Execution tile | breadcrumb/back to Delivery | Yes | No | related detail page | (`DeliveryWorkspace.razor:61-68,130-133`). |
| `/home/delivery/portfolio` | `Pages/Home/PortfolioDelivery.razor` | Delivery hub → Portfolio Delivery tile | breadcrumb/back to Delivery | Yes | No | related detail page | (`DeliveryWorkspace.razor:53-60,125-128`). |
| `/home/trends` | `Pages/Home/TrendsWorkspace.razor` | top workspace nav; home Trends tile | cross-workspace buttons | Yes | No | main workspace/hub navigation | (`WorkspaceNavigationCatalog.cs:49-61`, `HomePage.razor:197-216`). |
| `/home/pull-requests` | `Pages/Home/PrOverview.razor` | Trends hub → PR Trend tile | direct URL | Yes | No | related detail page | (`TrendsWorkspace.razor:176-201,538-541`). |
| `/home/pr-delivery-insights` | `Pages/Home/PrDeliveryInsights.razor` | Trends hub → PR Delivery Insights tile | direct URL | Yes | No | related detail page | (`TrendsWorkspace.razor:221-231,551-556`). |
| `/home/pipeline-insights` | `Pages/Home/PipelineInsights.razor` | Trends hub → Pipeline Insights tile | direct URL | Yes | No | related detail page | (`TrendsWorkspace.razor:203-219,543-549`). |
| `/home/portfolio-progress` | `Pages/Home/PortfolioProgressPage.razor` | Trends hub → Portfolio Progress tile | direct URL | Yes | No | related detail page | (`TrendsWorkspace.razor:233-241,559-562`). |
| `/home/trends/delivery` | `Pages/Home/DeliveryTrends.razor` | Trends hub → Delivery Trends tile | direct URL | Yes | No | related detail page | (`TrendsWorkspace.razor:243-251,564-567`). |
| `/home/dependencies` | `Pages/Home/DependencyOverview.razor` | none found | direct URL only | No | Yes | URL/manual deep link | Listed under Trends active-route prefixes but omitted from visible Trends hub; breadcrumb instead points to Planning (`WorkspaceNavigationCatalog.cs:54-60`, `DependencyOverview.razor:127-150`). |
| `/home/changes` | `Pages/Home/HomeChanges.razor` | Home dashboard “What’s new since sync” link | direct URL | Yes | No | home-only linked page | Discoverable from Home only, not from workspace model (`HomePage.razor:151-170`). |
| `/home/planning` | `Pages/Home/PlanningWorkspace.razor` | top workspace nav; home Planning tile | cross-workspace buttons | Yes | No | main workspace/hub navigation | (`WorkspaceNavigationCatalog.cs:62-72`, `HomePage.razor:207-216`). |
| `/planning/product-roadmaps` | `Pages/Home/ProductRoadmaps.razor` | Planning hub → Product Roadmaps tile | breadcrumb/back to Planning | Yes | No | related detail page | (`PlanningWorkspace.razor:56-63,131-138`). |
| `/planning/{RouteProjectAlias}/product-roadmaps` | `Pages/Home/ProductRoadmaps.razor` | Planning hub → Product Roadmaps tile when project context exists | Project Overview / Plan Board navigation | Conditional | No | conditional UI state | Project-scoped path depends on `ProjectAlias` context (`PlanningWorkspace.razor:131-138`). |
| `/planning/product-roadmaps/{ProductId:int}` | `Pages/Home/ProductRoadmapEditor.razor` | Product Roadmaps → per-product editor action | direct URL | Yes | No | related detail page | ProductRoadmaps navigates to editor (`ProductRoadmaps.razor:1100-1105`). |
| `/planning/plan-board` | `Pages/Home/PlanBoard.razor` | Planning hub → Plan Board tile | breadcrumb/back to Planning | Yes | No | related detail page | (`PlanningWorkspace.razor:64-71,140-147`). |
| `/planning/{RouteProjectAlias}/plan-board` | `Pages/Home/PlanBoard.razor` | Planning hub → Plan Board tile when project context exists | Project Overview / Product Roadmaps navigation | Conditional | No | conditional UI state | Depends on project context (`PlanningWorkspace.razor:140-147`). |
| `/planning/{ProjectAliasRoute}/overview` | `Pages/Home/ProjectPlanningOverview.razor` | Planning hub → Project Overview tile | ProductRoadmaps/PlanBoard → Overview buttons | Conditional | No | conditional UI state | Only rendered when `ProjectAlias` is set (`PlanningWorkspace.razor:45-54,149-157`, `ProductRoadmaps.razor:1108-1118`, `PlanBoard.razor:1128-1138`). |
| `/planning/multi-product` | `Pages/Home/MultiProductPlanning.razor` | Planning hub → Multi-Product Planning tile | breadcrumb/back to Planning | Yes | No | related detail page | (`PlanningWorkspace.razor:72-79,159-162`). |
| `/workitems` plus query states | `Components/WorkItems/WorkItemExplorer.razor` | none in main nav | Backlog Health ready-epic card; ReleasePlanning epic menu; manual deep link | No | Yes | related detail page / manual deep link | Not present in top nav, hubs, or breadcrumbs. In-app links generate `rootWorkItemId` or `selected`; other query states exist but are not generated in-app. |
| `/settings` | `Pages/SettingsPage.razor` | top settings button | startup redirect to `/settings/tfs` topic | Yes | No | utility/settings | Entry from top-right settings icon (`MainLayout.razor:50-54`). |
| `/settings/{SelectedTopic}` | `Pages/SettingsPage.razor` | Settings sidebar topic nav | direct URL | Yes | No | utility/settings | Visible topics: cache, cache-management, tfs, import-export, workitem-states, triage-tags, getting-started (`SettingsPage.razor:18-70,188-192`). |
| `/settings/workitem-states` | `Pages/Settings/WorkItemStates.razor` | Settings sidebar → Work Item States | `WorkItemStateMappingSection` button | Yes | No | utility/settings | (`SettingsPage.razor:50-55`, `Components/Settings/WorkItemStateMappingSection.razor:4-21`). |
| `/settings/teams` | `Pages/Settings/ManageTeams.razor` | Profiles → Manage Teams | legacy TeamWorkspace prompt | Yes | No | utility/settings | Discoverable from ProfilesHome (`ProfilesHome.razor:89-103,209-212`). |
| `/settings/products` | `Pages/Settings/ManageProducts.razor` | none in modern UI found | legacy ProductWorkspace prompt only | No | Yes | residual via hidden legacy area | Only inbound link found at audit time was inside the undiscoverable legacy ProductWorkspace alert (same alert block now at `Pages/LegacyWorkspaces/ProductWorkspace.razor:103-106`, later changed by Batch 1 cleanup). |
| `/settings/productowner/{ProfileId:int}` | `Pages/Settings/ManageProductOwner.razor` | Profiles → profile tile detail icon | return from edit page | Yes | No | utility/settings | (`Components/Settings/ProfileTile.razor:36-41,121-126`, `EditProductOwner.razor:248-318`). |
| `/settings/productowner/edit/{ProfileId:int?}` | `Pages/Settings/EditProductOwner.razor` | Profiles → Add Profile | Manage Product Owner → Edit | Yes | No | utility/settings | (`ProfilesHome.razor:62-76,203-207`, `ManageProductOwner.razor:394`). |
| `/profiles` | `Pages/ProfilesHome.razor` | profile selector; startup redirects; startup guard buttons | direct URL | Yes | No | setup/utility | (`Components/Settings/ProfileSelector.razor:128`, `Index.razor:47-50`, `StartupGuard.razor:41-55`). |
| `/onboarding` | `Pages/Onboarding.razor` | first-run startup redirect | Settings → Getting Started wizard route | Conditional | No | startup/conditional UI state | (`Index.razor:28-32`, `Components/Settings/GettingStartedSection.razor:19-29`). |
| `/sync-gate` | `Pages/SyncGate.razor` | none | startup/programmatic redirects only | No | Yes | startup redirect only | Used after onboarding/profile selection; validates `returnUrl` and forwards (`Index.razor:52-60`, `Onboarding.razor:35`, `ProfilesHome.razor:164-173`, `SyncGate.razor:371-409`). |
| `/not-found` | `Pages/NotFound.razor` | none | router fallback only | No | Yes | system fallback | Not linked anywhere; used by `Router` as NotFound page (`App.razor:4-9`). |
| `/legacy` | `Pages/Landing.razor` | none in modern UI | direct URL; breadcrumbs from legacy workspaces | No | Yes | residual legacy cluster entry | Legacy landing remains routable but is absent from modern navigation. |
| `/workspace/product`, `/workspace/product/{ProductId:int}` | `Pages/LegacyWorkspaces/ProductWorkspace.razor` | `/legacy` → “Overzien” | internal legacy navigation | No | Yes | residual legacy cluster | Visible only after entering legacy cluster (`Landing.razor:199-210`). |
| `/workspace/team`, `/workspace/team/{TeamId:int}` | `Pages/LegacyWorkspaces/TeamWorkspace.razor` | legacy cluster internal navigation | direct URL | No | Yes | residual legacy cluster | Reached from legacy product/communication flows. |
| `/workspace/analysis`, `/workspace/analysis/{Mode}` | `Pages/LegacyWorkspaces/AnalysisWorkspace.razor` | `/legacy` → “Begrijpen” | internal legacy navigation | No | Yes | residual legacy cluster | Legacy-only. |
| `/workspace/communication` | `Pages/LegacyWorkspaces/CommunicationWorkspace.razor` | `/legacy` → “Delen” | internal legacy navigation | No | Yes | residual legacy cluster | Legacy-only. |

## Pages not discoverable through normal navigation

### Routable modern pages without a normal visible path

1. **Work Item Explorer — `/workitems`**  
   Routable and still used, but not part of the visible workspace/hub model. No main nav item, no hub tile, no breadcrumb entry, and no visible “Explorer” link was found outside two local flows.

2. **Dependency Overview — `/home/dependencies`**  
   Routable modern page with no visible inbound link from Home, workspace hubs, settings, breadcrumbs, or page-local navigation elsewhere. The only references found were the route itself and inclusion in the Trends active-route catalog (`WorkspaceNavigationCatalog.cs:54-60`, `DependencyOverview.razor:1`).

3. **Bug Detail — `/home/bugs/detail`**  
   Routable detail page that expects `bugId` in the query string (`BugDetail.razor:199-227`), but no inbound link to it was found. It has outbound links back to Bug Overview / Bug Triage / Health, but no visible entry path into it.

4. **Manage Products — `/settings/products`**  
   Routable settings page, but the only inbound link found at audit time was inside the undiscoverable legacy Product workspace alert block (`Pages/LegacyWorkspaces/ProductWorkspace.razor:103-106`, later changed by Batch 1 cleanup).

### Residual legacy pages outside the intended modern app flow

- `/legacy`
- `/workspace/product`
- `/workspace/product/{ProductId:int}`
- `/workspace/team`
- `/workspace/team/{TeamId:int}`
- `/workspace/analysis`
- `/workspace/analysis/{Mode}`
- `/workspace/communication`

These pages form a self-contained legacy navigation cluster. They are not exposed by the modern top navigation or home dashboard, but once entered manually they can navigate to one another.

### Alias routes kept for backward compatibility rather than discovery

- `/home/backlog-overview`
- `/home/sprint-trend`
- `/home/sprint-trend/activity/{WorkItemId:int}`

These aliases are still routable, but visible modern navigation uses the canonical routes instead.

## Residual access paths still present

### Residual access to routed pages

| Residual path | Destination | Evidence | Assessment |
|---|---|---|---|
| Manual URL or bookmark | `/workitems` | `Components/WorkItems/WorkItemExplorer.razor:1` | Intentional deep-link capability, but not part of normal navigation. |
| Manual URL or bookmark | `/home/dependencies` | `Pages/Home/DependencyOverview.razor:1` | Residual modern page with no visible inbound path. |
| Manual URL or bookmark | `/home/bugs/detail?bugId=...` | `Pages/Home/BugDetail.razor:199-227` | Deep-link-only/orphaned detail page. |
| Manual URL or bookmark | `/legacy`, `/workspace/*` | `Pages/Landing.razor:1`, `Pages/LegacyWorkspaces/*.razor` | Residual legacy architecture. |
| Legacy alias bookmark | `/home/backlog-overview`, `/home/sprint-trend`, `/home/sprint-trend/activity/{id}` | `WorkspaceRoutes.cs:155-209` | Intentional compatibility aliases. |
| Startup/programmatic redirect | `/onboarding`, `/sync-gate` | `Index.razor:28-60`, `Onboarding.razor:35`, `ProfilesHome.razor:164-173` | Intentional system flow, not user-browsed navigation. |

### Surviving visible links to pages that are not routable in the current client

| Visible link source | Target | Evidence | Assessment |
|---|---|---|---|
| `DependencyOverview` current page | `/dependency-graph` | `Pages/Home/DependencyOverview.razor:45-64` | Current visible link to a non-routable page; likely stale/legacy. |
| `ValidationSummaryPanel` on Work Item Explorer | `/help` | `Components/WorkItems/SubComponents/ValidationSummaryPanel.razor:37-41` | Current visible link to a non-routable page; inconsistent. |
| Legacy Product workspace | `/backlog-health`, `/velocity`, `/release-planning`, `/epic-forecast`, `/pr-insights` | `Pages/LegacyWorkspaces/ProductWorkspace.razor:170-194` | Legacy visible links surviving inside hidden cluster; targets not routable. |
| Legacy Team workspace | `/velocity`, `/state-timeline`, `/backlog-health` | `Pages/LegacyWorkspaces/TeamWorkspace.razor:217-243,436` | Legacy visible links surviving inside hidden cluster; targets not routable. |
| Legacy Analysis workspace | `/backlog-health`, `/effort-distribution`, `/pr-insights`, `/pipeline-insights`, `/epic-forecast`, `/dependency-graph`, `/state-timeline` | `Pages/LegacyWorkspaces/AnalysisWorkspace.razor:98-188` | Legacy visible links surviving inside hidden cluster; targets not routable. |

## Dedicated section: Work Item Explorer

### Route and query-state surface

- Primary route: `/workitems` (`Components/WorkItems/WorkItemExplorer.razor:1`)
- In-app generated query states:
  - `?rootWorkItemId={id}` from Backlog Health (`BacklogOverviewPage.razor:461-464`)
  - `?selected={id}` from Release Planning board (`Components/ReleasePlanning/ReleasePlanningBoard.razor:1066-1069`)
- Additional supported query states in the component, with **no in-app generators found**:
  - `validationCategory`
  - `filter=issues`
  - `focusRoot`
  - `allProducts`
  - `allTeams`
  (`WorkItemExplorer.razor:88-120,186-235`)

### Discoverability assessment

Work Item Explorer is **not discoverable through normal navigation**. A typical user following the visible information architecture can reach it only if they happen to use:

- a ready-epic card in **Backlog Health**, or
- the **View Details** menu item on an epic in the **Release Planning Board**.

It has **no**:

- top-level workspace entry,
- hub tile,
- breadcrumb,
- “back to hub” button,
- page-local visible link from a modern hub,
- settings/profile/home entry.

This makes it **residually reachable but not integrated**.

### Work Item Explorer access situations

| Access path | Exact source location in code | User action required | Condition/state required | Category of reachability | Appears intentional |
|---|---|---|---|---|---|
| Backlog Health ready-epic card → `/workitems?rootWorkItemId={epicId}` | `Pages/Home/BacklogOverviewPage.razor:81-123`, `Pages/Home/BacklogOverviewPage.razor:461-464` | Click a ready epic card in the “Ready for Implementation” section | At least one ready epic must exist for the selected product | reachable only from a related detail page; conditional UI state | Yes — code explicitly says the card opens the explorer scoped to the root epic |
| Product Roadmap Editor → Release Planning Board → epic menu “View Details” → `/workitems?selected={epicId}` | `Components/ReleasePlanning/EpicCard.razor:49-67,189-196`, `Components/ReleasePlanning/ReleasePlanningBoard.razor:104-117,1066-1069` | Open the epic context menu and choose **View Details** | User must be in Product Roadmap Editor / Release Planning flow with visible epics | reachable only from a related detail page; conditional UI state | Yes — dedicated event name and handler route to the explorer |
| Manual deep link to `/workitems` | `Components/WorkItems/WorkItemExplorer.razor:1` | Manually enter URL/bookmark | None beyond app readiness | reachable only through URL/manual deep link | Ambiguous — supported by routing, but not exposed |
| Manual deep link to `/workitems?filter=issues&validationCategory=...` | `Components/WorkItems/WorkItemExplorer.razor:88-120,203-225` | Manually enter URL/bookmark or external link | Correct query parameters | reachable only through URL/manual deep link | Ambiguous — supported in code, but no caller found |
| Manual deep link to `/workitems?focusRoot=...`, `?allProducts=true`, `?allTeams=true` | `Components/WorkItems/WorkItemExplorer.razor:106-120,227-235,264-267` | Manually enter URL/bookmark or external link | Correct query parameters | reachable only through URL/manual deep link | Ambiguous/possibly leftover — code supports it, but no caller found |

### Work Item Explorer consistency findings

1. **Inbound without outbound IA support**  
   The page has inbound navigation, but no breadcrumb, hub-return control, or workspace placement. The component starts immediately with toolbar/tree/detail UI and no higher-level orientation (`WorkItemExplorer.razor:27-85`).

2. **Not represented in the visible workspace model**  
   It is absent from `WorkspaceNavigationCatalog`, so no top-level workspace item can ever highlight it (`WorkspaceNavigationCatalog.cs:19-73`).

3. **Mixed intent signal**  
   Comments in the component still describe “Home navigation deep links” and “task-driven entry points”, but the current codebase exposes only two local callers and no explicit Home hub link (`WorkItemExplorer.razor:88-120`).

4. **Potential leftover deep-link surface**  
   `validationCategory`, `filter=issues`, `focusRoot`, `allProducts`, and `allTeams` are implemented in the component, but no inbound caller for those query states was found in the client.

## Risks and UX implications

1. **Hidden-but-live pages create uneven discoverability**  
   Users can reach some pages only if they already know the URL, already know a local affordance, or happen to land on a specific data state.

2. **Work Item Explorer is functionally alive but architecturally invisible**  
   This is the highest-risk inconsistency: users can still enter it, but once there the app provides little orientation and no explicit return path into the intended workspace model.

3. **Dependency Overview contradicts the workspace model**  
   The page is classified under Trends for active-nav purposes, but its breadcrumb points to Planning and no Trends tile exposes it (`WorkspaceNavigationCatalog.cs:54-60`, `DependencyOverview.razor:127-150`).

4. **HomeChanges and Bug Triage sit outside the workspace model**  
   Both are discoverable, but only through local/home links. They are not represented in the top workspace model, so the active top navigation does not clearly explain where the user is.

5. **Legacy cluster can still expose broken links**  
   Entering `/legacy` or `/workspace/*` exposes multiple links to pages that no longer have routes, increasing confusion if those paths are still used by bookmarks or old documentation.

6. **Orphan detail routes can produce dead-end experiences**  
   `/home/bugs/detail` is routable and has recovery behavior, but no inbound path was found. This is a classic “page exists but the product no longer leads to it” condition.

## Recommended cleanup actions

### Remove

1. Remove or fully retire the legacy workspace cluster if it is no longer part of the intended architecture:
   - `/legacy`
   - `/workspace/product`
   - `/workspace/team`
   - `/workspace/analysis`
   - `/workspace/communication`
2. Remove visible links to non-routable pages from legacy pages and current pages (`/dependency-graph`, `/help`, `/velocity`, `/state-timeline`, etc.) if those targets are not coming back.
3. Remove dormant Work Item Explorer query-state support if it is no longer used (`focusRoot`, `allProducts`, `allTeams`, and possibly `filter=issues` / `validationCategory` if no external caller depends on them).

### Hide

1. If legacy pages must remain for compatibility, hide them behind explicit compatibility handling rather than leaving them as routable user pages.
2. If `/settings/products` should not be used, stop exposing it indirectly via the hidden legacy Product workspace.

### Reroute

1. Replace stale links to non-routable pages with canonical modern routes where equivalents exist.
2. If Work Item Explorer is intended to remain only as a deep-link page, add an explicit return route or redirect logic back to an owning workspace/hub context.
3. If legacy aliases remain, consider redirecting them to canonical routes instead of serving duplicate route templates directly.

### Expose properly in navigation

1. Decide whether **Work Item Explorer** is still a supported product surface:
   - if yes, add a visible navigation contract and a return path;
   - if no, remove the surviving local links.
2. Decide whether **Dependency Overview** belongs in Trends or Planning:
   - then make inbound links, breadcrumbs, and workspace classification agree.
3. Decide whether **HomeChanges** and **Bug Triage** should be modeled as workspace destinations or clearly documented utility pages outside the workspace model.

### Keep as intentional deep-link-only page

1. `/sync-gate` should remain programmatic only.
2. `/onboarding` can reasonably remain conditional/system-driven, with the current Settings “Run Getting Started Wizard” path as the sanctioned re-entry.
3. Legacy aliases such as `/home/backlog-overview` and `/home/sprint-trend` may remain deep-link compatible if bookmarks must be preserved.

## Validation

### Routing inventory evidence

- Main route inventory comes from `@page` directives under `PoTool.Client/Pages` and `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`.
- `Pages/TfsConfig.razor` is **not** counted as routable because its `@page` directive is commented out (`Pages/TfsConfig.razor:1-7`).
- Router configuration uses `NotFoundPage="@typeof(Pages.NotFound)"` (`App.razor:4-9`).

### Primary navigation evidence

- Top-level visible navigation is rendered by `MainLayout` + `WorkspaceNavigationBar` (`Layout/MainLayout.razor:23-55`, `Components/Common/WorkspaceNavigationBar.razor:5-12`).
- The authoritative top-level workspace set is defined in `WorkspaceNavigationCatalog` and includes only Health, Delivery, Trends, Planning (`Components/Common/WorkspaceNavigationCatalog.cs:19-73`).
- Home dashboard tiles expose the same four workspaces plus quick actions (`Pages/Home/HomePage.razor:175-240`).

### Key page evidence

- **Dependency Overview is not linked from Trends hub**: Trends hub defines Bug Trend, PR Trend, Pipeline Insights, PR Delivery Insights, Portfolio Progress, Delivery Trends, but no Dependency tile (`Pages/Home/TrendsWorkspace.razor:147-251`).
- **Dependency Overview is still classified under Trends active routes** (`Components/Common/WorkspaceNavigationCatalog.cs:54-60`).
- **Dependency Overview breadcrumb points to Planning, not Trends** (`Pages/Home/DependencyOverview.razor:127-150`).
- **HomeChanges is discoverable from Home only** via the “What’s new since sync” link (`Pages/Home/HomePage.razor:151-170`).
- **Work Item Explorer inbound links**:
  - Backlog Health epic card (`Pages/Home/BacklogOverviewPage.razor:81-123,461-464`)
  - Release Planning epic menu (`Components/ReleasePlanning/EpicCard.razor:49-67,189-196`, `Components/ReleasePlanning/ReleasePlanningBoard.razor:104-117,1066-1069`)
- **Work Item Explorer lacks orientation/return navigation** (`Components/WorkItems/WorkItemExplorer.razor:27-85`).
- **Bug Detail requires query state and has no inbound link found** (`Pages/Home/BugDetail.razor:199-227`, search for `/home/bugs/detail` found only route declaration, route constant, and active-route catalog inclusion).
- **Settings/products inbound link** found only in legacy Product workspace (`Pages/LegacyWorkspaces/ProductWorkspace.razor:105`).
- **Legacy cluster entry** remains at `/legacy` and routes users into `/workspace/*` pages (`Pages/Landing.razor:187-210`).

### Conclusion

The intended modern information architecture is workspace-based and visible. The main residual deviations are:

- an invisible but live **Work Item Explorer**,
- a **Dependency Overview** page that exists without discoverability and contradicts its own workspace framing,
- an isolated **legacy workspace cluster**,
- and a small set of orphan or compatibility routes that remain routable without clear modern ownership.
