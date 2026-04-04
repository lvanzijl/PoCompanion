# Battleship mockdata screenshot validation report

## Doel en scope
Deze validatie controleert de user-facing, Battleship-mockdata-gedreven applicatiepages end-to-end op stabiele rendering, zichtbare data en bruikbare fout/lege-states.

**In scope**
- Home / Health / Delivery / Trends / Planning workspaces en hun data-pages
- Bugs Triage
- Work Item Explorer
- Product Roadmap Editor

**Buiten scope voor deze mockdata-run**
- Onboarding / SyncGate / StartupBlocked
- NotFound / legacy redirects
- Settings- en profielbeheerpages zonder Battleship-datavisualisatie

## Gebruikte Battleship mockdata context
- Base URL client: `http://localhost:5292`
- Base URL API: `http://localhost:5291`
- Actieve mock profile: `Commander Elena Marquez` (`productOwnerId=1`)
- Producten:
  - `1` — `Incident Response Control`
  - `2` — `Crew Safety Operations`
- Project route alias: `battleship-systems`
- Team-context gebruikt voor sprint/trend pages: `teamId=4` — `Crew Safety`
- Sprint-context gebruikt voor sprint pages: `sprintId=2` — `Sprint 11`
- Range-context gebruikt voor trend pages: `fromSprintId=1`, `toSprintId=3` (`Sprint 10` t/m `Sprint 12`)
- Rolling-context gebruikt voor PR-overzicht: `30 days`

## Lijst van bezochte pages
1. `/home`
2. `/home/health`
3. `/home/health/overview?productId=1`
4. `/home/health/backlog-health?productId=1`
5. `/home/validation-triage?productId=1`
6. `/home/validation-queue?category=SI&productId=1`
7. `/home/validation-fix?category=SI&ruleId=SI-1&productId=1`
8. `/home/delivery`
9. `/home/delivery/sprint?productId=1&teamId=4&sprintId=2&timeMode=Sprint`
10. `/home/delivery/execution?productId=1&teamId=4&sprintId=2&timeMode=Sprint`
11. `/home/trends?teamId=4&fromSprintId=1&toSprintId=3&timeMode=Range`
12. `/home/trends/delivery?productId=1&teamId=4&fromSprintId=1&toSprintId=3&timeMode=Range`
13. `/home/pull-requests?teamId=4&timeMode=Rolling&rollingWindow=30&rollingUnit=Days`
14. `/home/bugs?productId=1`
15. `/bugs-triage?productId=1`
16. `/workitems?productId=1`
17. `/home/changes`
18. `/home/planning`
19. `/planning/plan-board?productId=1`
20. `/planning/product-roadmaps`
21. `/planning/battleship-systems/overview?productId=1`
22. `/planning/product-roadmaps/1?productId=1`
23. `/planning/product-roadmaps/1`
24. `/planning/multi-product`
25. `/home/pipeline-insights?productId=1&teamId=4&sprintId=2&timeMode=Sprint`

## Samenvatting

| Page | Loaded | Data Visible | Screenshot Count | Errors | Notes |
| --- | --- | --- | --- | --- | --- |
| Home | Yes | Yes | 1 | 0 | All-products dashboard with workspace signals and quick actions. |
| Health workspace | Yes | Yes | 1 | 0 | Hub tiles rendered correctly. |
| Health overview | Yes | Yes | 1 | 0 | Build Quality cards visible for both products. |
| Backlog Health | Yes | Yes | 1 | 0 | Product 1 refinement data rendered. |
| Validation Triage | Yes | Yes | 1 | 0 | Category cards loaded with counts. |
| Validation Queue | Yes | Yes | 1 | 0 | SI queue loaded with rule cards. |
| Validation Fix | Yes | Yes | 1 | 0 | Fix-session item details rendered. |
| Delivery workspace | Yes | Yes | 1 | 0 | Hub tiles rendered correctly. |
| Sprint Delivery | Yes | Yes | 1 | 0 | Build Quality and delivery summary visible. |
| Sprint Execution | Yes | No | 1 | 1 | Empty execution result for selected current sprint. |
| Trends workspace | Yes | Yes | 1 | 0 | Trend tiles plus bug trend chart visible. |
| Delivery Trends | Yes | Yes | 1 | 0 | Trend charts and sprint table rendered, though throughput stayed zero. |
| PR Overview | Yes | Yes | 1 | 0 | Rolling-window PR insights rendered. |
| Bug Insights | Yes | Yes | 1 | 0 | Bug metrics and severity distribution visible. |
| Bugs Triage | Yes | Yes | 1 | 0 | Bug tree + detail split view rendered with mock content. |
| Work Item Explorer | Yes | Yes | 1 | 0 | Large hierarchy and validation summary rendered. |
| What's New Since Last Sync | Yes | No (expected) | 1 | 0 | Explicit expected no-change state because only one successful sync exists. |
| Planning workspace | Yes | Yes | 1 | 0 | Planning hub tiles rendered correctly. |
| Plan Board | Yes | Yes | 1 | 1 | Backlog tree visible; sprint columns unavailable message shown. |
| Product Roadmaps | Yes | Yes | 1 | 0 | 14 roadmap epics visible on shared axis. |
| Project Planning Overview | Yes | No | 1 | 1 | Route loaded but summary showed 0 products / 0 data. |
| Product Roadmap Editor | Yes | Yes | 2 | 0 | Product 1 editor rendered; extra controlled no-selection state captured. |
| Multi-Product Planning | Yes | No | 1 | 1 | Page loaded but reported no planning projections / 0 selected products. |
| Pipeline Insights | Yes | No | 1 | 1 | Explicit `Data unavailable` panel with retry action. |

## Per page

### Home
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: all products
- Screenshots:
  - `./2026-04-04-validation-report/home/all-products.png`
- Notities: dashboard rendered with both product chips, sync info, workspace tiles, and quick actions.

### Health workspace
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: default workspace context
- Screenshots:
  - `./2026-04-04-validation-report/health-workspace/default.png`
- Notities: health hub tiles and breadcrumbing rendered correctly.

### Health overview
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: single product `Incident Response Control` (`productId=1`)
- Screenshots:
  - `./2026-04-04-validation-report/health-overview/product-1.png`
- Notities: overall Build Quality and per-product cards rendered with values.

### Backlog Health
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: single product `Incident Response Control` (`productId=1`)
- Screenshots:
  - `./2026-04-04-validation-report/backlog-health/product-1.png`
- Notities: refinement/backlog readiness content rendered for product 1.

### Validation Triage
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: single product `Incident Response Control` (`productId=1`)
- Screenshots:
  - `./2026-04-04-validation-report/validation-triage/product-1.png`
- Notities: structural integrity, refinement readiness, completeness, and missing effort category totals were visible.

### Validation Queue
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`, category `SI`
- Screenshots:
  - `./2026-04-04-validation-report/validation-queue/si-product-1.png`
- Notities: rule groups and start-fix actions rendered correctly.

### Validation Fix
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`, category `SI`, rule `SI-1`
- Screenshots:
  - `./2026-04-04-validation-report/validation-fix/si-rule-1-product-1.png`
- Notities: active work item details, validation reason, and session navigation were visible.

### Delivery workspace
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: default workspace context
- Screenshots:
  - `./2026-04-04-validation-report/delivery-workspace/default.png`
- Notities: delivery hub rendered with three entry tiles.

### Sprint Delivery
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`, `teamId=4`, `sprintId=2`
- Screenshots:
  - `./2026-04-04-validation-report/sprint-delivery/team-4-sprint-2.png`
- Notities: build quality summary and product breakdown rendered successfully.

### Sprint Execution
- Load status: loaded
- Data zichtbaar: nee
- Gebruikte context: `productId=1`, `teamId=4`, `sprintId=2`
- Screenshots:
  - `./2026-04-04-validation-report/sprint-execution/team-4-sprint-2-no-data.png`
- Afwijking: page showed `No execution data found for the selected sprint.` while Sprint Delivery for the same sprint rendered data.

### Trends workspace
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `teamId=4`, range `1..3`
- Screenshots:
  - `./2026-04-04-validation-report/trends-workspace/team-4-range-1-3.png`
- Notities: trend signal tiles and bug trend chart rendered correctly.

### Delivery Trends
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`, `teamId=4`, range `1..3`
- Screenshots:
  - `./2026-04-04-validation-report/delivery-trends/product-1-team-4-range-1-3.png`
- Notities: charts and per-sprint table rendered; throughput values were mostly zero.

### PR Overview
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `teamId=4`, rolling 30 days
- Screenshots:
  - `./2026-04-04-validation-report/pr-overview/team-4-rolling-30-days.png`
- Notities: PR insights page loaded under rolling-window context.

### Bug Insights
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1` (page also normalized to `teamId=4`)
- Screenshots:
  - `./2026-04-04-validation-report/bug-overview/product-1-team-4.png`
- Notities: bug metrics and severity distribution rendered with meaningful values.

### Bugs Triage
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`
- Screenshots:
  - `./2026-04-04-validation-report/bugs-triage/product-1.png`
- Notities: triage tree and detail pane loaded with tagged bug content.

### Work Item Explorer
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`
- Screenshots:
  - `./2026-04-04-validation-report/workitem-explorer/product-1.png`
- Notities: 4,973 work items plus validation summary rendered successfully.

### What's New Since Last Sync
- Load status: loaded
- Data zichtbaar: nee (verwacht)
- Gebruikte context: operational page buiten globale filtercontract
- Screenshots:
  - `./2026-04-04-validation-report/home-changes/no-change-data.png`
- Notities: page correctly explained that two successful syncs are required before change deltas can be shown.

### Planning workspace
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: default workspace context
- Screenshots:
  - `./2026-04-04-validation-report/planning-workspace/default.png`
- Notities: planning hub tiles rendered correctly.

### Plan Board
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: `productId=1`
- Screenshots:
  - `./2026-04-04-validation-report/plan-board/product-1.png`
- Notities: backlog tree rendered with epic/feature/PBI data, but sprint columns stayed unavailable.

### Product Roadmaps
- Load status: loaded
- Data zichtbaar: ja
- Gebruikte context: all products
- Screenshots:
  - `./2026-04-04-validation-report/product-roadmaps/all-products.png`
- Notities: page rendered 14 roadmap epics and per-product lanes on a shared axis.

### Project Planning Overview
- Load status: loaded
- Data zichtbaar: nee
- Gebruikte context: route project `battleship-systems`, query `productId=1`
- Screenshots:
  - `./2026-04-04-validation-report/project-planning-overview/route-project-empty.png`
- Afwijking: route loaded but summary showed `0 products` and all totals `0`, while Battleship project data exists elsewhere.

### Product Roadmap Editor
- Load status: loaded
- Data zichtbaar: ja (single-product state), nee (controlled no-selection state, expected)
- Gebruikte context:
  - data state: `productId=1`
  - controlled state: route-only `/planning/product-roadmaps/1`
- Screenshots:
  - `./2026-04-04-validation-report/product-roadmap-editor/product-1.png`
  - `./2026-04-04-validation-report/product-roadmap-editor/no-selection-state.png`
- Notities: product 1 editor showed available epics and roadmap controls; route-only access correctly rendered the controlled no-selection state.

### Multi-Product Planning
- Load status: loaded
- Data zichtbaar: nee
- Gebruikte context: all products
- Screenshots:
  - `./2026-04-04-validation-report/multi-product-planning/no-projections.png`
- Afwijking: page reported `No products with planning projections found` and `0 selected product(s)` while the products control still displayed `1, 2`.

### Pipeline Insights
- Load status: loaded
- Data zichtbaar: nee
- Gebruikte context: `productId=1`, `teamId=4`, `sprintId=2`
- Screenshots:
  - `./2026-04-04-validation-report/pipeline-insights/data-unavailable.png`
- Afwijking: explicit error state `Data unavailable` / `Error retrieving pipeline insights`.

## Encountered Errors

### 1. Pipeline Insights unavailable
- Page: `Pipeline Insights`
- Actie: direct navigation with valid product/team/sprint context
- Expected: pipeline stability metrics and build summary with mock data
- Actual: explicit error panel `Data unavailable` / `Error retrieving pipeline insights`
- Foutmelding: `Error retrieving pipeline insights`
- Vermoedelijke oorzaak: backend/client data source for pipeline insights not returning a consumable payload for the selected Battleship sprint context
- Ernst: **major**

### 2. Repeated frontend resource warnings across navigations
- Page: multiple
- Actie: every direct page load
- Expected: quiet console during normal navigation
- Actual: repeated invalid preload warning and blocked Google Fonts request
- Foutmelding:
  - `<link rel=preload> has an invalid href value`
  - `Failed to load resource: net::ERR_BLOCKED_BY_CLIENT.Inspector` for Google Fonts
- Vermoedelijke oorzaak: invalid preload tag plus blocked third-party font request in the sandbox/browser environment
- Ernst: **minor**

## Potential Errors / Suspected Issues

### 1. Sprint Execution returns no execution data for the current sprint
- Page: `Sprint Execution`
- Actie: open current sprint with `productId=1&teamId=4&sprintId=2&timeMode=Sprint`
- Expected: execution diagnostics for the same sprint that loads on Sprint Delivery
- Actual: `No execution data found for the selected sprint.`
- Foutmelding: none beyond the empty-state text
- Vermoedelijke oorzaak: execution metric query or canonical iteration matching diverges from Sprint Delivery for the same mock sprint
- Ernst: **major**

### 2. Project Planning Overview loses product/project data on the project route
- Page: `Project Planning Overview`
- Actie: open `/planning/battleship-systems/overview?productId=1`
- Expected: Battleship project summary across its products
- Actual: page shows `Read-only summary across 0 products` with all totals at zero
- Foutmelding: none; only empty metrics and `Product filter is active globally but not applied on this page.`
- Vermoedelijke oorzaak: project route context and product scoping are not reconciling correctly for the summary query
- Ernst: **major**

### 3. Multi-Product Planning selection/projection mismatch
- Page: `Multi-Product Planning`
- Actie: open `/planning/multi-product`
- Expected: timelines for both Battleship products or a consistent empty-state with correct selection count
- Actual: products input shows `1, 2`, but the page simultaneously reports `0 selected product(s)` and `No products with planning projections found`
- Foutmelding: empty-state text only
- Vermoedelijke oorzaak: selected-product UI state and loaded projection set are out of sync
- Ernst: **major**

### 4. Plan Board still blocks sprint columns after product selection
- Page: `Plan Board`
- Actie: open `/planning/plan-board?productId=1`
- Expected: backlog plus usable sprint columns for planning
- Actual: backlog tree loads, but page message says sprint columns remain unavailable because the board no longer derives them from product teams
- Foutmelding: `Plan Board no longer derives sprint columns from product teams. Sprint columns stay unavailable until explicit sprint inputs exist.`
- Vermoedelijke oorzaak: product-only plan-board flow no longer has a compatible sprint-context source after recent filter changes
- Ernst: **minor**

## Validatie-opmerkingen
- Pages with explicit no-data states were kept in the run and documented instead of being skipped.
- Screenshots were captured only after the pages settled into either a data state or a stable explicit empty/error state.
- `Product Roadmap Editor` no-selection state appears intentional and aligns with the current single-product requirement.
- `What's New Since Last Sync` empty state appears expected for a fresh mock sync baseline.

## Screenshot inventory
- Screenshot root: `./2026-04-04-validation-report/`
- Total screenshots: **25**
- Successful data-bearing pages have at least one screenshot each.
