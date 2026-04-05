# UX Interaction Model

Scope note: this report covers routed user-facing pages in `PoTool.Client`. Legacy redirect shims (`/home/sprint-trend`, `/home/sprint-trend/activity/{id}`, `/home/backlog-overview`) are excluded from page analysis because they normalize routes and do not define stable interaction behavior.

## Canonical definitions

### Team
- **Definition**
  - Team is the execution lens. It selects the delivery system whose sprint cadence, delivery metrics, PR context, and pipeline context are being inspected.
- **When it is required**
  - Any page whose primary question is about sprint execution, sprint delivery, multi-sprint delivery trends, team-scoped PR delivery behavior, or pipeline health tied to a sprint cadence.
- **When it is optional**
  - Cross-product portfolio pages, bug overview pages, change-log pages, and landing/hub pages where team only narrows exploration rather than defining the page meaning.
- **Route-owned vs user-selectable**
  - **Route-owned:** never by default in the current app model.
  - **User-selectable:** all team use should come from shared/global filter state or page-local exploratory controls.

### Sprint
- **Definition**
  - Sprint is the time-box lens. It selects either one sprint, a sprint range, or an equivalent time horizon used to interpret delivery and execution signals.
- **When it is required**
  - Pages whose main question is explicitly sprint-based: Sprint Delivery, Sprint Execution, Pipeline Insights, PR Delivery Insights, and trend pages that compute over sprint windows.
- **When it is optional**
  - Home/dashboard pages, bug pages, settings/admin pages, roadmap pages, and change-log pages where time scope is informative but not the defining page contract.
- **Route-owned vs user-selectable**
  - **Route-owned:** never as a path segment in the current app; it is part of filter/query context.
  - **User-selectable:** shared/global filter state for common analytics pages; page-local advanced controls only on exploration pages that need richer horizon selection.

### Product
- **Definition**
  - Product is the ownership and backlog scope lens. It answers which owned backlog slice, roadmap, or product contribution the page is about.
- **When it is required**
  - Product-specific planning pages, roadmap editor, plan board, backlog health when operating on a single product, and any page whose main question is about one product’s backlog/roadmap/work.
- **When it is optional**
  - Portfolio or all-products overview pages, workspace hubs, and exploratory analytics where cross-product view is valid.
- **Route-owned vs user-selectable**
  - **Route-owned:** pages whose path already encodes scope, such as `/planning/product-roadmaps/{productId}` and project-scoped planning routes where product/project scope is structurally owned by the route.
  - **User-selectable:** overview and exploration pages where users may switch product without changing the core page type.

## Per-page alignment analysis

| Page | Mode | Current Team behavior vs canonical | Current Sprint behavior vs canonical | Current Product behavior vs canonical | Alignment summary |
|---|---|---|---|---|---|
| Index (`/`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Startup router should stay scope-free. |
| Onboarding (`/onboarding`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Setup launcher should stay scope-free. |
| Profiles (`/profiles`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Context entry page correctly avoids analytic scope. |
| Sync Gate (`/sync-gate`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Operational gate should remain scope-free. |
| Startup Blocked (`/startup-blocked`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Recovery page correctly ignores analytic filters. |
| Home (`/home`) | Decision | Implicit in summary metrics, not directly selectable; acceptable | Implicit in summary metrics, not directly selectable; acceptable | User-selectable dashboard context; aligned | Good decision page, but mixed summary metrics can blur whether product or workspace is primary. |
| Home Changes (`/home/changes`) | Exploration | Team intentionally absent; aligned | Uses sync window instead of sprint; aligned | Product intentionally absent; aligned | Correctly outside canonical shared filter contract. |
| Health workspace (`/home/health`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Good hub behavior. |
| Health Overview (`/home/health/overview`) | Exploration | Team absent; aligned | Rolling window implicit, sprint not required; aligned | Optional product chip/filter; aligned | Mostly aligned. |
| Validation Triage (`/home/validation-triage`) | Decision | Team absent; aligned | Sprint absent; aligned | Optional product scoping; aligned | Good use of minimal scope. |
| Validation Queue (`/home/validation-queue`) | Decision | Team absent; aligned | Sprint absent; aligned | Optional product scoping; aligned | Good use of minimal scope. |
| Validation Fix (`/home/validation-fix`) | Decision | Team absent; aligned | Sprint absent; aligned | Optional product scoping; aligned | Guided workflow stays correctly narrow. |
| Backlog Health (`/home/health/backlog-health`) | Exploration | Team absent; aligned | Sprint absent; aligned | Single-product behavior via selector/chip; aligned | Product is the real scope; aligned. |
| Delivery workspace (`/home/delivery`) | Decision | Not directly selected here; aligned | Not directly selected here; aligned | Not directly selected here; aligned | Good hub, but delivery subpages use different time semantics. |
| Sprint Delivery (`/home/delivery/sprint`) | Exploration | Team required implicitly through filter/defaults; aligned | Single sprint required; aligned | Product optional through drill-down/global context; aligned | Core semantics align. |
| Sprint Execution (`/home/delivery/execution`) | Exploration | Team required; aligned | Single sprint required; aligned | Product optional/secondary; aligned | Core semantics align. |
| Portfolio Delivery (`/home/delivery/portfolio`) | Exploration | Team currently required through shared context even though page is portfolio-wide; mismatch | Sprint range required; aligned | Product optional/all-products; aligned | Team is over-applied for a portfolio composition page. |
| Trends workspace (`/home/trends`) | Decision | Team not directly exposed; aligned | Time horizon implied by downstream signals; acceptable | Product not directly exposed; aligned | Good hub behavior. |
| Delivery Trends (`/home/trends/delivery`) | Exploration | Team required; aligned | Sprint range required; aligned | Product optional via shared scope; aligned | Strong canonical fit. |
| Portfolio Flow Trend (`/home/portfolio-progress`) | Exploration | Team currently required to unlock sprint range; partial mismatch | Sprint range required; aligned | Product optional filter inside portfolio view; aligned | Team behaves like a prerequisite even though the page question is portfolio-level. |
| Pull Request Insights (`/home/pull-requests`) | Exploration | Team context can exist, but repository filter is primary; partial mismatch | Rolling horizon rather than sprint; aligned | Product not primary; aligned | Team meaning is weak and inconsistent versus nearby engineering pages. |
| Pipeline Insights (`/home/pipeline-insights`) | Exploration | Team required; aligned | Single sprint required; aligned | Product shown as per-product breakdown rather than selector; aligned | Canonical fit. |
| PR Delivery Insights (`/home/pr-delivery-insights`) | Exploration | Team required; aligned | Sprint effectively required/optional but page is built around sprint context; aligned | Product not primary; aligned | Canonical fit, though filters are page-local instead of shared. |
| Bug Insights (`/home/bugs`) | Exploration | Team can influence data through shared store, but page question is not team-defined; mismatch | Uses rolling 6-month view rather than sprint; aligned | Product optional; aligned | Team should not be a primary semantic on this page. |
| Bugs Triage (`/bugs-triage`) | Exploration | Team absent; aligned | Sprint absent; aligned | Product inherited from profile/product ownership, not explicit; acceptable | Aligned as a product-owner operational workbench. |
| Planning workspace (`/home/planning`) | Decision | Team absent; aligned | Sprint absent; aligned | Project/product decisions deferred to destination page; aligned | Good hub behavior. |
| Project Planning Overview (`/planning/{projectAlias}/overview`) | Decision | Team absent; aligned | Sprint absent; aligned | Project route-owned, product summarized not selected; aligned | Strong canonical fit. |
| Product Roadmaps (`/planning/product-roadmaps`, `/planning/{projectAlias}/product-roadmaps`) | Exploration | Team absent; aligned | Sprint absent; aligned | Project may be route-owned; products shown as lanes not selector; aligned | Good route-owned planning semantics. |
| Product Roadmap Editor (`/planning/product-roadmaps/{productId}`) | Decision | Team absent; aligned | Sprint absent; aligned | Product route-owned and fixed; aligned | Strong canonical fit. |
| Plan Board (`/planning/plan-board`, `/planning/{projectAlias}/plan-board`) | Decision | Team not selected directly; capacity comes from planning data; acceptable | Sprint is future destination columns, not historical filter; aligned | Product required and either route-owned or default-selected; aligned | Good planning-specific reinterpretation of sprint. |
| Multi-Product Planning (`/planning/multi-product`) | Exploration | Team shown only as capacity-collision metadata; aligned | Sprint absent; aligned | Multi-product selectable scope; aligned | Good exploration semantics. |
| Settings hub (`/settings`, `/settings/{topic}`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Correctly scope-free. |
| Manage Teams (`/settings/teams`) | Decision | Team is managed entity, not filter; aligned | Not used; aligned | Not used; aligned | Correct admin semantics. |
| Manage Product Owner (`/settings/productowner/{id}`) | Decision | Team only as linked metadata on products; aligned | Not used; aligned | Product is managed entity; aligned | Correct admin semantics. |
| Edit Product Owner (`/settings/productowner/edit/{id?}`) | Decision | Team absent; aligned | Sprint absent; aligned | Product absent; aligned | Correct admin semantics. |
| Work Item States (`/settings/workitem-states`) | Decision | Team absent; aligned | Sprint absent; aligned | Product absent; aligned | Correct config semantics. |
| Work Item Explorer (`/workitems`) | Exploration | Team absent; aligned | Sprint absent; aligned | Product implicitly derived from active profile products; acceptable | Broader explorer intentionally avoids explicit team/sprint semantics. |
| Not Found (`/not-found`) | Decision | Not used; aligned | Not used; aligned | Not used; aligned | Correctly scope-free. |

## Mismatch table

| Page | Dimension | Current behavior | Mismatch type | Proposed resolution |
|---|---|---|---|---|
| Portfolio Delivery | Team | Page is portfolio-wide but still depends on team-scoped shared defaults and sprint source | Incorrect usage | Align to canonical behavior |
| Portfolio Flow Trend | Team | Team acts as prerequisite to unlock sprint-range data even though page question is portfolio-level | Incorrect usage | Remove from page scope |
| Pull Request Insights | Team | Team appears in summary context, but repository becomes the actual primary scope lens | Conflicting behavior | Move to advanced mode |
| Bug Insights | Team | Shared team context can shape results even though the page question is bug burden/severity, not team execution | Incorrect usage | Remove from page scope |
| PR Delivery Insights | Team/Sprint control model | Uses page-local context accordion instead of consistent shared filter entry for required execution scope | Conflicting behavior | Align to canonical behavior |
| Pull Request Insights | Sprint | Uses rolling horizon while sitting adjacent to sprint-scoped engineering pages, making time semantics feel inconsistent | Conflicting behavior | Move to advanced mode |
| Plan Board | Sprint | Sprint is a future planning destination, not an analysis filter, which conflicts with delivery/trend interpretations | Conflicting behavior | Convert to route-owned context |
| Home | Product | Product selector is both dashboard context and quasi-filter bar, which can make workspace choice feel secondary | Conflicting behavior | Move to advanced mode |
| Backlog Health | Product | Product is effectively required for meaningful use, but UI can still present the page before strong product commitment | Missing usage | Align to canonical behavior |
| Product Roadmaps | Product | Project-scoped routes own context, but non-project route remains broad and mixes read-only portfolio scan with product-specific drill-in actions | Conflicting behavior | Move to advanced mode |
| Work Item Explorer | Product | Product scope is hidden in active-profile loading rather than explicit page semantics | Missing usage | Align to canonical behavior |

## Proposed resolutions

### Align to canonical behavior
- **Portfolio Delivery / Backlog Health / Work Item Explorer / PR Delivery Insights**
  - Make the required scope explicit in page behavior and language.
  - Keep the same features, but ensure the page contract clearly states whether team, sprint, or product is mandatory.
  - Prefer the shared filter contract when a page depends on canonical execution scope.

### Convert to route-owned context
- **Plan Board**
  - Treat sprint columns as structural planning context owned by the page itself, not as the same user-selected time filter used by trend/delivery pages.
  - Preserve product/project route ownership where already present.

### Remove from page scope
- **Portfolio Flow Trend / Portfolio Delivery / Bug Insights**
  - Stop treating team as a first-class page requirement when the page question is portfolio- or bug-level.
  - Keep team-derived analytics only as supporting metadata where needed, not as the semantic gate to meaning.

### Move to advanced mode
- **Pull Request Insights / Home / Product Roadmaps**
  - Preserve powerful secondary scope controls, but keep them subordinate to the page’s primary question.
  - Repository, product-context switching, and cross-cutting filters should remain available without dominating first-view meaning.

## Decision vs Exploration classification

| Page | Classification | Enforcement implication |
|---|---|---|
| Index | Decision | No filters; immediate routing outcome |
| Onboarding | Decision | No filters; single guided next action |
| Profiles | Decision | Minimal actions; select or create profile |
| Sync Gate | Decision | No filters; wait/retry/escape |
| Startup Blocked | Decision | No filters; recover or open settings |
| Home | Decision | Minimal visible context; choose workspace |
| Home Changes | Exploration | Rich operational evidence; sync-window drill-down |
| Health workspace | Decision | Minimal options; choose health destination |
| Health Overview | Exploration | Supporting context and product narrowing are acceptable |
| Validation Triage | Decision | Minimal scope; choose a category |
| Validation Queue | Decision | Minimal scope; choose a rule group |
| Validation Fix | Decision | Guided next-item workflow |
| Backlog Health | Exploration | Product-focused drill-down and nested detail |
| Delivery workspace | Decision | Minimal options; choose delivery lens |
| Sprint Delivery | Exploration | Delivery details and drill-down dominate |
| Sprint Execution | Exploration | Diagnostics and multiple supporting signals |
| Portfolio Delivery | Exploration | Aggregated metrics and comparative panels |
| Trends workspace | Decision | Choose a trend lens |
| Delivery Trends | Exploration | Multiple signals and horizon controls |
| Portfolio Flow Trend | Exploration | Multiple charts and strategic signals |
| Pull Request Insights | Exploration | Rich filters and multiple breakdowns |
| Pipeline Insights | Exploration | Rich breakdown and tuning options |
| PR Delivery Insights | Exploration | Context + summary + friction drill-down |
| Bug Insights | Exploration | Metrics, distributions, and action CTA |
| Bugs Triage | Exploration | Full workbench with filtering/detail panes |
| Planning workspace | Decision | Choose planning surface |
| Project Planning Overview | Decision | Read summary, then choose next planning action |
| Product Roadmaps | Exploration | Comparative lanes, reporting, snapshots |
| Product Roadmap Editor | Decision | Focused ordering/editing workflow |
| Plan Board | Decision | Minimal scope; assign work to sprint destination |
| Multi-Product Planning | Exploration | Rich multi-product comparison controls |
| Settings hub | Decision | Choose a settings topic |
| Manage Teams | Decision | Focused admin actions |
| Manage Product Owner | Decision | Focused ownership/product admin actions |
| Edit Product Owner | Decision | Single record editing flow |
| Work Item States | Decision | Focused configuration task |
| Work Item Explorer | Exploration | Rich filtering and two-pane inspection |
| Not Found | Decision | No filters; recover from missing route |

## System-wide risks
- **Team overreach risk:** team is currently the most overloaded concept; it sometimes means execution scope, sometimes just a prerequisite for finding sprints, and sometimes background context.
- **Sprint dual-meaning risk:** sprint is retrospective on delivery/trend pages but prospective on planning pages, which is valid structurally but needs explicit semantic separation.
- **Product visibility risk:** product is sometimes explicit and sometimes hidden behind profile-owned defaults, which can make scope feel inconsistent even when behavior is technically correct.
- **Control-location risk:** some pages rely on shared filter semantics while others use page-local controls for similar concepts, which weakens consistency even without changing backend behavior.
- **Mode leakage risk:** several decision pages still carry secondary context or actions high in the view, while some exploration pages under-expose the scope they truly depend on.
