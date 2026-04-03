# Filtering model design

## 1. Current filter inventory (per page)

### Shared shell and context plumbing
| Surface | Current filters | Source | Defaults / fallback | Notes |
|---|---|---|---|---|
| `/home` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor`) | Product | Local page state + query (`WorkspaceQueryContext`) | No product = all products for tile signals; if query `productId` matches available products it is preselected | Acts as the practical hub-level product selector today. |
| Main layout (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Layout/MainLayout.razor`) | Project, product, team, sprint, sprint range (pass-through only) | Current URL parsed through `WorkspaceQueryContextHelper` | Preserves current context when moving between top-level workspaces | Shell preserves context but does not own validation or normalization. |
| `WorkspaceBase` + `WorkspaceQueryContextHelper` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/WorkspaceBase.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/WorkspaceQueryContextHelper.cs`) | `projectAlias`, `productId`, `teamId`, `sprintId`, `fromSprintId`, `toSprintId` | Query string | Missing values remain null | This is the closest thing to a shared filter contract, but many pages bypass or partially use it. |

### Trends workspace
| Page | Filters | Source | Defaults / fallback | Current issues |
|---|---|---|---|---|
| `/home/trends` (`TrendsWorkspace.razor`) | Product, team, sprint range, bug period drill-in | Base query context + local `_selectedTeamId`, `_selectedFromSprintId`, `_selectedToSprintId` + manual query parsing for sprint values | No product = inherited/all; no team = none selected; no sprint range = derived date range defaults to 6 months | Mixes inherited context with extra local time state; downstream navigation sometimes passes only team/range and drops product. |
| `/home/trends/delivery` (`DeliveryTrends.razor`) | Team, product, end sprint, sprint count | Local state + manual query parsing/update | Team/product nullable = all; sprint count defaults to 6; current sprint inferred from filtered sprint list | Uses custom query parsing instead of shared context; time model is end sprint + count, unique in app. |
| `/home/pipeline-insights` (`PipelineInsights.razor`) | Team, sprint, include partially succeeded, include canceled, SLO duration | Local state + manual query parsing + backend filter metadata | Team and sprint initially unset; page asks user to select team and sprint; toggles default false; SLO optional | Product is absent even though navigation can pass it; page purpose is sprint-scoped but defaults to empty state. |
| `/home/pull-requests` (`PrOverview.razor`) | Team, optional sprint, optional repository, optional author drill-in | Local state + manual query parsing + backend filter metadata | Team nullable = all teams; sprint optional; repository optional | Product is not directly selectable even though surrounding workspace carries product context. |
| `/home/portfolio-progress` (`PortfolioProgressPage.razor`) | Product, team, from sprint, to sprint | Local state + manual query parsing + backend filter metadata | If no team selected and teams exist, first team is auto-selected; if no sprint range is chosen, page derives a default range ending at current/latest sprint | Strongest explicit scope controls, but only within this one page. |
| `/home/bugs` (`BugOverview.razor`) | Team, product, implicit fixed 6-month window | Local state + manual query parsing | Team/product nullable = all; time hardcoded to last 6 months | Time is implicit and cannot be changed despite page being entered from period-based trend clicks. |
| `/home/changes` (`HomeChanges.razor`) | Context is inherited, but no explicit UI filters | Uses `WorkspaceBase` context propagation only | Uses current inherited context if present | Mostly a report surface, not an owned filter surface. |
| `/home/pr-delivery-insights` (`PrDeliveryInsights.razor`) | Team / sprint delivery context | Local state + backend metadata (by pattern from Trends navigation) | Current workspace context drives defaults | Another task/report surface that depends on caller-provided context. |

### Delivery workspace
| Page | Filters | Source | Defaults / fallback | Current issues |
|---|---|---|---|---|
| `/home/delivery` (`DeliveryWorkspace.razor`) | Pass-through project/product/team/sprint/range only | `WorkspaceBase` | Simply forwards existing context | Hub does not define delivery defaults itself. |
| `/home/delivery/sprint` (`SprintTrend.razor`) | Single sprint via previous/next navigation; product/epic/feature drilldown | Local state; profile-scoped products and all team sprints loaded on first render; backend filter metadata returned but not surfaced | Current sprint inferred from all known sprints; drilldown starts at portfolio | Ignores shared query context entirely for team/product/sprint; no explicit selector for product or team. |
| `/home/delivery/portfolio` (`PortfolioDelivery.razor`) | Team, from sprint, to sprint | Local state + manual query parsing + backend filter metadata | Team nullable = all; sprint range optional but page builds a selected sprint list from current choices | Uses chip/popover scope model; product is not available even though delivery is often product-contextual. |
| `/home/delivery/execution` (`SprintExecution.razor`) | Team/product/sprint style execution filters | Local state + backend filter metadata | Defaults to inferred or current delivery scope | Similar family to portfolio/PR pages but separate contract. |
| `/home/delivery/sprint/activity/{WorkItemId:int}` (`SprintTrendActivity.razor`) | Fixed work item id + optional inherited delivery context in query | Route parameter + query for return context | Requires caller-provided work item id; context is advisory | This is a task/detail page and should not own independent filtering. |

### Planning workspace
| Page | Filters | Source | Defaults / fallback | Current issues |
|---|---|---|---|---|
| `/home/planning` (`PlanningWorkspace.razor`) | Pass-through project/product/team/sprint/range only | `WorkspaceBase` | Forwards current context | Hub does not set planning defaults on its own. |
| `/planning/plan-board` + project route (`PlanBoard.razor`) | Project, product | Route parameter `RouteProjectAlias` + parsed query context product | Project defaults to all projects; if selected project yields exactly one product, product auto-selects | Ignores team/time even though capacity calculations depend on team sprint history. |
| `/planning/product-roadmaps` + project route (`ProductRoadmaps.razor`) | Project | Route parameter + local state | Project defaults to all projects | Product is represented as lanes, not a filter; time is implicit in forecast data. |
| `/planning/multi-product` (`MultiProductPlanning.razor`) | Multi-select products; view toggles for clusters/capacity collisions | Local state; inherits `WorkspaceBase` but does not expose shared context in UI | Defaults to preselected/all available product lanes after load | Planning-specific product set is fully local and not aligned with hub/shared product context. |
| `/planning/{projectAlias}/overview` (`ProjectPlanningOverview.razor`) | Project only | Route parameter | Required route project | Task/report surface with fixed context. |
| `/planning/product-roadmaps/{ProductId:int}` (`ProductRoadmapEditor.razor`) | Fixed product; optional project alias for return route | Route parameter + parsed project alias from query | Product is required by route | Task/editor surface; no independent filtering. |

### Health workspace
| Page | Filters | Source | Defaults / fallback | Current issues |
|---|---|---|---|---|
| `/home/health` (`HealthWorkspace.razor`) | Pass-through project/product/team/sprint/range only | `WorkspaceBase` | Forwards current context | Hub does not set health defaults itself. |
| `/home/health/overview` (`HealthOverviewPage.razor`) | Product; implicit fixed 30-day rolling window | Shared context product + local fixed dates | No product = show overall PO summary; time always last 30 days | Time is hidden and immutable. |
| `/home/health/backlog-health` (`BacklogOverviewPage.razor`) | Product | Shared context product + local selector | If query product valid, use it; else if one product exists auto-select it; else wait for manual selection | Product is required for useful output, unlike health overview. |
| `/home/validation-triage` (`ValidationTriagePage.razor`) | Product + category drill-in | Shared context product + queue category passed as extra query to next page | No product = all profile products | Good example of task flow receiving fixed context from parent. |
| `/home/validation-queue` (`ValidationQueuePage.razor`) | Product + required category | Shared context product + manual query `category` | Missing/invalid category triggers guided recovery; no product = all profile products | Category is page-specific input, product is inherited scope. |
| `/home/validation-fix` (`ValidationFixPage.razor`) | Product + required category + required rule id | Shared context product + manual query parsing | Missing/invalid task context triggers guided recovery | Good task-flow page: context comes from caller, not user filtering. |

### Work Item Explorer and current drill-in dependency
| Surface | Filters | Source | Defaults / fallback | Current issues |
|---|---|---|---|---|
| `/workitems` (`WorkItemExplorer.razor`) | Root work item id, text filter, validation category toggles | Query parameter `rootWorkItemId` + local text/toggle state + active-profile product scope fallback | No profile = all products; no root = full profile-scoped hierarchy; validation toggles off by default | Local filters are not URL-persisted; page still depends on implicit profile scope instead of explicit caller contract. |
| Current entry points into explorer | `rootWorkItemId` only | Parent pages such as Backlog Health and release-planning board | Caller decides fixed root item | This already behaves like a task-flow contract and is the best basis for Explorer replacement. |

## 2. Identified inconsistencies
1. **Two competing filter systems exist**: `WorkspaceQueryContext` is the nominal shared model, but many pages manually parse query strings and ignore part of the shared contract.
2. **Time semantics are inconsistent**: pages use fixed rolling windows, single sprint, sprint range, end sprint + count, or no explicit time at all.
3. **Workspace hubs preserve context but do not define it**: delivery, planning, health, and trends hubs mostly forward current query state instead of establishing workspace-specific defaults.
4. **Product handling varies by page purpose**: sometimes required (`BacklogOverviewPage`, `PlanBoard` in practice), sometimes optional (`HealthOverviewPage`, `PortfolioProgressPage`), and sometimes silently ignored (`PipelineInsights`, `PrOverview`).
5. **Team handling is inconsistent**: some pages default to “all teams,” some auto-select the first team, and some hide team selection entirely while still depending on team-scoped sprint data.
6. **Silent fallback is common**: pages often infer current sprint, first team, or single product without making the normalization explicit to the user.
7. **Task/detail pages are mixed with workspace filter pages**: validation queue/fix and sprint activity already behave like fixed-context flows, but the broader app does not treat them as a separate contract class.
8. **Collapsed summaries are inconsistent**: some pages use chips/popovers, some always-open selectors, and some have no visible scope summary at all.
9. **Canonical filter feedback is uneven**: some pages surface `CanonicalFilterMetadataNotice`, others receive or could receive normalized backend scope but never show it.
10. **Project is only partially integrated**: planning routes treat project as a route-level scope, while the shared query context also carries `projectAlias`; other workspaces largely ignore it.
11. **Navigation drops context selectively**: some links preserve product+team, others preserve team+sprint range only, and some reset everything by routing to a bare canonical path.
12. **Work Item Explorer still relies on implicit profile scope** even when entered from explicit parent context.

## 3. Canonical filter model definition

### Canonical filters
1. **Product** — primary scope filter
2. **Team** — secondary operational scope filter
3. **Time** — exactly one active mode at a time

### Time modes (mutually exclusive)
- **Single sprint**: one `SprintId`
- **Sprint range**: `FromSprintId` + `ToSprintId`
- **Date range**: `FromDateUtc` + `ToDateUtc`

### Canonical rules
- Exactly **one** time mode may be active.
- A page must declare time as **required**, **optional**, or **not applicable**.
- Invalid combinations are normalized in this order:
  1. If `SprintId` is present, discard sprint-range and date-range values.
  2. Else if both `FromSprintId` and `ToSprintId` are present, discard date-range values and normalize the sprint order.
  3. Else if both `FromDateUtc` and `ToDateUtc` are present, normalize date order.
  4. Else fall back to the workspace default time mode.
- `Team` is only valid when the page meaningfully supports team scoping. If not applicable, it must be ignored visibly, not silently.
- `Product` is required for task flows or detail pages whose parent page already fixed the product context.

### Allowed values and missing-value behavior
| Filter | Status in canonical model | Allowed values | Missing behavior |
|---|---|---|---|
| Product | Required on Planning and Health detail/task flows; optional on Trends and Delivery aggregate pages; not applicable only on true cross-product summaries | Valid product owned/visible to active profile | If required and missing, derive from current hub selection; if that is unavailable, force explicit selection before loading page data |
| Team | Optional on Trends/Delivery pages that use sprint or delivery cadence; not applicable on roadmap/editor/task flows without team meaning | Valid team visible to active profile or page dataset | If missing, use workspace default team behavior; if not applicable, clear it on navigation |
| Single sprint | Required for sprint-detail pages | Valid sprint belonging to the effective team scope | If missing, use workspace current sprint default |
| Sprint range | Optional where trend/aggregate pages compare multiple sprints | Valid ordered sprint pair within same effective team scope | If missing, use workspace default sprint-range window |
| Date range | Optional where time is not sprint-based or where cross-team comparisons need calendar windows | Valid ordered UTC dates | If missing, use workspace default calendar window |
| Page-specific task inputs (`category`, `ruleId`, `workItemId`, `rootWorkItemId`) | Required only on task/detail flows | Route/query values validated by caller contract | Missing values must block load with guided recovery |

### Recommended canonical state object
- **Hub / page context**: `ProjectAlias?`, `ProductId?`, `TeamId?`, `TimeMode`, `SprintId?`, `FromSprintId?`, `ToSprintId?`, `FromDateUtc?`, `ToDateUtc?`
- **Task-flow context**: canonical hub context + fixed page-specific inputs (for example `CategoryKey`, `RuleId`, `WorkItemId`, `RootWorkItemId`)

## 4. Filter ownership and propagation rules

### Ownership tiers
1. **Global / hub-level defaults**
   - Owned by Home hub selection and the destination workspace hub.
   - Purpose: establish the starting product/team/time context when entering a workspace.
2. **Workspace-level filters**
   - Owned by the workspace family (Trends, Delivery, Planning, Health).
   - Purpose: preserve a stable context across sibling pages in the same workspace.
3. **Page-level overrides**
   - Allowed only when a page needs a stricter contract than the workspace default.
   - Example: `PlanBoard` may require product even if Planning workspace itself does not.
4. **Task-flow fixed context**
   - Validation queue/fix, sprint activity, roadmap editor, and future Explorer replacements must receive fixed caller context and must not own independent filtering.

### Propagation rules
- **Workspace hub sets defaults** when entered from Home or another workspace.
- **Workspace child inherits** workspace product/team/time unless it explicitly declares a narrower requirement.
- **Page override must be visible** in the UI summary and URL.
- **Task-flow pages do not invent defaults** beyond validating caller-supplied context.
- **Cross-workspace navigation preserves product**, preserves team only when the target page supports team, and converts time to the nearest valid target mode.

### Reset / preserve policy
| Navigation event | Product | Team | Time |
|---|---|---|---|
| Home → workspace hub | Preserve selected home product if valid | Reset to workspace default unless the workspace meaningfully carries team | Reset to workspace default time mode |
| Workspace hub → sibling page | Preserve current workspace product/team/time | Preserve if applicable | Preserve if target supports same mode; otherwise normalize to target default |
| Workspace → different workspace | Preserve product if valid | Preserve only if target supports team scope | Normalize to target default time mode |
| Parent page → task flow | Freeze product/team/time from caller + append fixed task inputs | Freeze | Freeze |
| Clear action on page | Clear only page-allowed optional filters | Preserve required filters | Revert to workspace default time mode |

### URL contract rule
- Shared navigable pages should use one canonical context model in the URL.
- Page-local presentation-only state (expanded panels, toggle visibility, transient search text) should not be treated as workspace context.
- Task-flow parameters must be explicit and validated separately from shared workspace filters.

## 5. Default states per workspace

### Trends
- **Product**: optional; default from Home-selected product if available, otherwise all products.
- **Team**: optional; default none selected.
- **Time mode**: **Sprint range**.
- **Default value**: last completed/current 6-sprint window for the selected team; if no team is selected, use a calendar fallback only on pages that cannot operate without time.
- **Reason**: Trends is comparative by nature; a multi-sprint window is the least surprising default.

### Delivery
- **Product**: optional at workspace level, but required for product-specific drilldown/task flows.
- **Team**: optional; default none selected unless the page is inherently team-scoped.
- **Time mode**: **Single sprint** by default.
- **Default value**: current sprint for the effective team context; if no team is selected, use the profile’s current sprint resolution logic and show that normalization.
- **Reason**: Delivery answers “what landed now,” so a current sprint default is clearest.

### Planning
- **Product**: required on Plan Board; optional on roadmap hubs; multi-select on multi-product planning.
- **Team**: generally not applicable at workspace level.
- **Time mode**: **Not applicable** for roadmap/editor pages; **Single sprint** only where planning boards require sprint placement context.
- **Default value**: if a planning page needs product and only one valid product exists, auto-select it; otherwise require explicit product selection.
- **Reason**: Planning is mostly scope/sequence oriented, not general time-filter driven.

### Health
- **Product**: optional at workspace hub and overview; required on backlog health and inherited task flows.
- **Team**: not applicable for the current health pages.
- **Time mode**: **Date range** for overview-type health metrics; **not applicable** for backlog readiness/validation flows.
- **Default value**: default rolling 30-day date range for overview pages; no time control on structural backlog pages.
- **Reason**: Health splits between rolling quality signals and timeless structural readiness views.

## 6. UI behavior definition (collapsed vs expanded, invalid states)

### Collapsed summary behavior
- Every filterable page must show a **collapsed summary row** near the top.
- The summary must include:
  - Product label (or “All products”)
  - Team label (or “All teams” / “No team selected” only when allowed)
  - Time summary in canonical wording:
    - Single sprint: sprint name
    - Sprint range: `Sprint A → Sprint B`
    - Date range: `MMM d, yyyy → MMM d, yyyy`
- Non-default selections must be visually marked using chips or a `Custom scope` indicator.

### Expanded control panel behavior
- Expanding the panel reveals only the filters applicable to that page.
- Controls should appear in canonical order: **Product → Team → Time**.
- Time mode control must appear before specific time value controls when a page supports multiple time modes.
- When a filter is not applicable, it should not appear at all.

### Invalid / normalized state behavior
- Invalid filter combinations must not silently persist.
- When normalization occurs, the page must show a visible notice explaining:
  - requested values
  - effective values
  - why normalization happened
- Required-but-missing page context must show guided recovery, not partial content.
- Disabled controls should explain why they are unavailable (for example sprint picker disabled until team is chosen).

### Minimal visual rules
- Collapsed summary stays visible after data loads.
- Expanded panel is a secondary control surface and should not dominate the page.
- Task-flow pages should show **context badges**, not full filter panels.

## 7. Problem areas and risks

### Problem areas
1. **Delivery pages are the most inconsistent family**: `SprintTrend.razor` ignores shared context, while `PortfolioDelivery.razor` owns a richer local filter model.
2. **Trends pages fragment time handling**: `DeliveryTrends`, `PortfolioProgressPage`, `PipelineInsights`, `PrOverview`, and `BugOverview` all use different time assumptions.
3. **Health overview hides time** while backlog and validation flows ignore time entirely.
4. **Planning pages do not share one clear product contract**: `PlanBoard` requires one product, `ProductRoadmaps` uses project selection and product lanes, and `MultiProductPlanning` uses its own independent multi-select model.
5. **Manual query parsing is widespread**, making enforcement difficult.
6. **Some pages receive filters they cannot honor** (especially product/team context passed through hubs into pages that do not expose or use them clearly).
7. **Current hub preservation can carry stale or ambiguous state** because hubs do not reassert their own defaults.

### Risks if left unchanged
- Users will continue to see different meanings for the same filter labels across pages.
- Future contextual drilldowns will inherit unclear scope rules from their parent pages.
- Replacing Work Item Explorer will be harder because callers do not yet expose one consistent parent context contract.
- Backend canonical filter metadata will remain underused, so silent normalization will continue to feel broken.

## 8. Work Item Explorer replacement requirements (context contracts)

### Principle
Future replacements must be **task-flow pages** that receive explicit context from the parent surface. They must not depend on hidden global scope.

### Current entry points and required replacement contracts
| Current entry point | Current context | Required replacement contract |
|---|---|---|
| Backlog Health epic card → explorer | `rootWorkItemId` + inherited product context | Required: `ProductId`, `RootWorkItemId`; optional: read-only parent breadcrumb context; no team/time ownership |
| Release planning board “view details” | `rootWorkItemId` for selected epic + planning product context | Required: `ProductId`, `RootWorkItemId`, optional `ProjectAlias`; no independent filtering |
| Direct `/workitems` access | Implicit profile-wide product scope | Replace with no standalone global explorer dependency; direct-entry replacement must require an explicit parent contract or become a dedicated admin/report route with its own declared scope |

### Replacement contract rules
- Parent page must provide all required scope explicitly.
- Replacement page may add page-local toggles or search, but those are **not** workspace filters.
- No replacement page may infer product/team/time from profile alone.
- If parent context is missing or invalid, the page must show guided recovery back to the parent workspace.

## 9. Open decisions (if any)
1. **Should Home own the canonical product selection for the whole session, or should each workspace hub immediately normalize its own product default?**
2. **Should Trends and Delivery both support team = all-teams by default, or should team become required whenever sprint-based time is used?**
3. **Should project remain planning-only route context, or should it become a first-class shared filter available to all workspaces?**
4. **Should date-range mode exist only for Health overview and bug-style history pages, or should it be available generically across Trends?**
5. **Should sprint-derived pages normalize missing team to the repository’s current-sprint team resolution, or require explicit team selection whenever multiple teams are possible?**
