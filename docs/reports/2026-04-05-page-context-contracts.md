# Page context contracts — 2026-04-05

## Per-page contract table

Source basis:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterPageCatalog.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/FilterStateResolver.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PageFilterExecutionGate.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterRouteService.cs`

Mode legend:
- **Single** = exactly one product
- **Multi** = multiple products, but only when the route or page semantics explicitly create a product set
- **All** = no product filter

| Page | Route(s) | Required context | Supported modes | No selection | Single selection | Multi selection |
|---|---|---|---|---|---|---|
| HomePage | `/home` | none | Single, All | valid | valid | invalid |
| HealthWorkspace | `/home/health` | none | All | valid | invalid | invalid |
| HealthOverviewPage | `/home/health/overview` | Product | Single, All | valid | valid | invalid |
| BacklogOverviewPage | `/home/health/backlog-health`, `/home/backlog-overview` | Product | Single, All | valid | valid | invalid |
| HomeChanges | `/home/changes` | none | All | valid | invalid | invalid |
| DeliveryWorkspace | `/home/delivery` | none | All | valid | invalid | invalid |
| PortfolioDelivery | `/home/delivery/portfolio` | Team + Sprint range | Single, All | unresolved until team and sprint range exist | valid narrowing contract | invalid |
| SprintExecution | `/home/delivery/execution` | Team + Sprint | Single, All | unresolved until team and sprint exist | valid narrowing contract | invalid |
| SprintTrend | `/home/delivery/sprint`, `/home/sprint-trend` | Team + Sprint | Single, All | unresolved until team and sprint exist | valid narrowing contract | invalid |
| SprintTrendActivity | `/home/delivery/sprint/activity/{workItemId}`, `/home/sprint-trend/activity/{workItemId}` | Team + Sprint | Single, All | unresolved until team and sprint exist | valid narrowing contract | invalid |
| TrendsWorkspace | `/home/trends` | Team + Sprint range | Single, All | unresolved until team and sprint range exist | valid narrowing contract | invalid |
| DeliveryTrends | `/home/trends/delivery` | Team + Sprint range | Single, All | unresolved until team and sprint range exist | valid narrowing contract | invalid |
| PortfolioProgressPage | `/home/portfolio-progress` | Team + Sprint range | Single, All | unresolved until team and sprint range exist | valid narrowing contract | invalid |
| PipelineInsights | `/home/pipeline-insights` | Team + Sprint | Single, All | unresolved until team and sprint exist | valid narrowing contract | invalid |
| PrOverview | `/home/pull-requests` | Team | All | valid rolling/all-team mode | invalid | invalid |
| PrDeliveryInsights | `/home/pr-delivery-insights` | Team + Sprint | All | unresolved until team and sprint exist | invalid | invalid |
| BugOverview | `/home/bugs` | none | Single, All | valid | valid | invalid |
| BugDetail | `/home/bugs/detail` | none | All | valid | invalid | invalid |
| ValidationTriagePage | `/home/validation-triage` | Product | Single, All | valid | valid | invalid |
| ValidationQueuePage | `/home/validation-queue` | Product | Single, All | valid | valid | invalid |
| ValidationFixPage | `/home/validation-fix` | Product | Single, All | valid | valid | invalid |
| PlanningWorkspace | `/home/planning` | Project | Multi, All | valid | invalid | valid via project scope |
| MultiProductPlanning | `/planning/multi-product` | Product | Single, All | valid | valid | invalid |
| ProductRoadmaps | `/planning/product-roadmaps`, `/planning/{projectAlias}/product-roadmaps` | Project optional, Product optional | Single, Multi, All | valid | valid | valid via project route scope |
| PlanBoard | `/planning/plan-board`, `/planning/{projectAlias}/plan-board` | Project optional | Single, Multi, All | valid | valid | valid via project route scope |
| ProductRoadmapEditor | `/planning/product-roadmaps/{productId}` | Product | Single | invalid | valid | invalid |
| ProjectPlanningOverview | `/planning/{projectAlias}/overview` | Project | Multi | invalid | invalid | valid via project route scope |
| BugsTriage | `/bugs-triage` | Product optional | Single, All | valid | valid | invalid |
| WorkItemExplorer | `/workitems` | Product optional | Single, All | valid | valid | invalid |

## Context requirements

### Pages with no page-specific context requirement
- `HomePage`
- `HealthWorkspace`
- `HomeChanges`
- `DeliveryWorkspace`
- `BugOverview`
- `BugDetail`

These pages must never block on Product, Team, or Sprint. If global filters are populated, those filters are either optional narrowing inputs or not applied at all.

### Pages requiring Product context only
- `HealthOverviewPage`
- `BacklogOverviewPage`
- `ValidationTriagePage`
- `ValidationQueuePage`
- `ValidationFixPage`
- `MultiProductPlanning`
- `BugsTriage`
- `WorkItemExplorer`

Contract:
- no product = allowed only when the page explicitly supports all-products aggregation
- one product = valid
- more than one product = invalid unless the page is route-scoped to a project and the route defines the product set

### Pages requiring Team + Sprint context
- `SprintExecution`
- `SprintTrend`
- `SprintTrendActivity`
- `PipelineInsights`
- `PrDeliveryInsights`

Contract:
- no team = unresolved
- team without sprint = unresolved
- sprint without team = invalid
- product is optional only as a narrowing scope when the page supports product narrowing

### Pages requiring Team + Sprint range context
- `PortfolioDelivery`
- `TrendsWorkspace`
- `DeliveryTrends`
- `PortfolioProgressPage`

Contract:
- no team = unresolved
- team without complete range = unresolved
- partial range = invalid
- product is optional only as a narrowing scope when the page supports product narrowing

### Pages requiring Project context
- `PlanningWorkspace`
- `ProjectPlanningOverview`
- project-scoped `ProductRoadmaps`
- project-scoped `PlanBoard`

Contract:
- project route authority creates an explicit multi-product scope
- query or shared product state may narrow within that project scope only when the page contract allows it

### Single-product editor
- `ProductRoadmapEditor`

Contract:
- exactly one product is required
- no product = invalid
- route product must be authoritative

## Supported modes (single / multi / all)

### Single-product capable pages
- `HomePage`
- `HealthOverviewPage`
- `BacklogOverviewPage`
- `PortfolioDelivery`
- `SprintExecution`
- `SprintTrend`
- `SprintTrendActivity`
- `TrendsWorkspace`
- `DeliveryTrends`
- `PortfolioProgressPage`
- `PipelineInsights`
- `BugOverview`
- `ValidationTriagePage`
- `ValidationQueuePage`
- `ValidationFixPage`
- `MultiProductPlanning`
- `ProductRoadmaps`
- `PlanBoard`
- `ProductRoadmapEditor`
- `BugsTriage`
- `WorkItemExplorer`

### Multi-product capable pages
- `PlanningWorkspace` through project semantics
- `ProductRoadmaps` through project route semantics
- `PlanBoard` through project route semantics
- `ProjectPlanningOverview` through project route semantics

### All-products capable pages
- `HomePage`
- `HealthWorkspace`
- `HealthOverviewPage`
- `BacklogOverviewPage`
- `HomeChanges`
- `DeliveryWorkspace`
- `PortfolioDelivery`
- `SprintExecution`
- `SprintTrend`
- `SprintTrendActivity`
- `TrendsWorkspace`
- `DeliveryTrends`
- `PortfolioProgressPage`
- `PipelineInsights`
- `PrOverview`
- `BugOverview`
- `ValidationTriagePage`
- `ValidationQueuePage`
- `ValidationFixPage`
- `PlanningWorkspace`
- `MultiProductPlanning`
- `ProductRoadmaps`
- `PlanBoard`
- `BugsTriage`
- `WorkItemExplorer`

## Invalid combinations

### Structural invalid states
- Product set with more than one product on any page whose contract is Single or All-only
- Team without Product on a page whose product scope is explicit and restrictive
- Sprint without Team
- Sprint range with only one bound
- Team or Sprint state on pages that do not apply Team or Time filters
- Missing Product on `ProductRoadmapEditor`
- Product outside route-defined project scope on project-scoped planning pages

### Contract-specific invalid states
- `PrOverview`: any explicit product selection
- `PrDeliveryInsights`: any explicit product selection
- `HealthWorkspace`, `HomeChanges`, `DeliveryWorkspace`, `BugDetail`: any Team or Time dependence
- `ProjectPlanningOverview`: all-products or single-product-only access without project scope

## Enforcement rules

### UI constraints
- Shared product filter stays single-select.
- Team options must be constrained by the selected product when product narrowing is active.
- Sprint options must be constrained by the selected team.
- Pages with `UsesProduct = false`, `UsesTeam = false`, or `UsesTime = false` must not present those filters as page-effective requirements.
- Project-scoped planning pages may express multi-product context only through route authority, not through ad hoc multi-select controls.

### API expectations
- Pages with required Team + Sprint must send both values, never Sprint alone.
- Pages with required Team + Sprint range must send Team plus both range bounds.
- Pages advertising product narrowing must pass either no product filter (All) or one explicit product unless the page contract explicitly allows route-defined multi-product scope.
- `ProductRoadmapEditor` must send one authoritative product.

### Fallback behavior
- Only explicit fallback is allowed.
- `No selection` is valid only on pages whose contract includes All.
- Missing required Team or Sprint should remain unresolved, not silently guessed.
- Project routes may define scope explicitly; route authority is the only valid implicit source of multi-product context.

## Identified inconsistencies

### 1. ProductRoadmapEditor route is single-product, but route product is still treated as a lookup hint
- Contract implication: editor must be `Single` with required authoritative Product.
- Current behavior:
  - route exists at `/planning/product-roadmaps/{ProductId:int}`
  - `FilterStateResolver` explicitly says route `productId` is treated as a lookup hint only
- Result: the page shape implies required single-product context, but the shared resolver does not currently model route product as authoritative.

### 2. PortfolioDelivery is cataloged as product-aware, but client calls ignore product scope
- Catalog contract advertises `UsesProduct = true`.
- Current page request sends `productIds: null` to the API.
- Result: explicit single-product narrowing is not honored even though the page contract says product narrowing exists.

### 3. PipelineInsights is cataloged as product-aware, but client requests only use sprint and profile scope
- Catalog contract advertises `UsesProduct = true`, `UsesTeam = true`, `UsesTime = true`.
- Current page request sends only `productOwnerId` and `sprintId`.
- Result: Team is used only to drive sprint selection locally, and Product is not forwarded as API scope despite the page contract implying both filters matter.

### 4. Project-scoped planning pages are the only real multi-product mode, but that rule is implicit instead of stated
- `ProductRoadmaps`, `PlanBoard`, and `ProjectPlanningOverview` derive multi-product scope from route project authority.
- Shared global product controls remain single-select.
- Result: “multi-product” currently means “project-scoped subset,” not “user-selected arbitrary product set,” but that distinction is not explicit in the current page contract model.

### 5. PR pages split into two different product semantics without an explicit contract boundary
- `PrOverview` is effectively all-products/team-scoped.
- `PrDeliveryInsights` is also effectively all-products/team+sprint-scoped.
- Other delivery pages allow optional product narrowing.
- Result: similar analytical pages do not share one stated rule for whether Product is allowed, ignored, or forbidden.

## Recommended contract baseline

To remove ambiguity without changing implementation yet:
- define **All**, **Single**, and **Project-scoped Multi** as the only valid product modes
- treat **Project-scoped Multi** as route-owned only
- require every page definition to state:
  - whether Product is optional, required, forbidden, or route-owned
  - whether Team is optional or required
  - whether Time is forbidden, optional, Sprint-required, or Range-required
- treat any page behavior outside its declared contract as a defect, not a fallback
