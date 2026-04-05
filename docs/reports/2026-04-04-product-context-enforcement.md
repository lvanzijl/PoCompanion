# Product Context Enforcement

Scope note: this report defines the target interaction model for Product as the single global context and compares it to the current `PoTool.Client` behavior. It does not implement the changes; it records the behavioral alignment required to enforce the rule without redesigning the UI or removing features.

## Canonical Product definition

### Canonical rule
- **Product is always user-selected.**
  - The active Product scope must come from the shared global filter state, not from page-local fallback rules or route-owned page parameters.
- **Product is always globally available.**
  - Every routed page may read the current Product selection, even if that page does not actively query by Product.
- **Product is never implicitly overridden by pages.**
  - Pages must not silently replace the selected Product with a route value, a first-owned-product default, or a page-local selector state.
- **Product is required for all portfolio-level queries.**
  - Any page that answers a portfolio, planning, roadmap, health, delivery, bug, or trend question across owned work must respect the selected Product set explicitly.

### Operational consequences
- Default global Product state becomes **All products selected**.
- Product selection must persist across navigation and refresh.
- Route-owned Product semantics are non-canonical and must be removed or reinterpreted as page lookup only.
- Pages may still expose product-related affordances, but those controls must read/write the same shared global Product state.

## Per-page Product behavior (before/after)

| Page | Product before | Product after | Gap / action |
|---|---|---|---|
| Index (`/`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Onboarding (`/onboarding`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Profiles (`/profiles`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Sync Gate (`/sync-gate`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Startup Blocked (`/startup-blocked`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Not Found (`/not-found`) | No Product behavior | Global Product available but not used | No change beyond passive availability |
| Settings hub (`/settings`, `/settings/{SelectedTopic}`) | No Product behavior | Global Product available but not query-driving | No functional change |
| Manage Teams (`/settings/teams`) | No Product filter; team admin only | Global Product available but not query-driving | No functional change |
| Manage Product Owner (`/settings/productowner/{id}`) | Product is managed entity, not page scope | Global Product available but not query-driving | No functional change |
| Edit Product Owner (`/settings/productowner/edit/{id?}`) | No Product behavior | Global Product available but not query-driving | No functional change |
| Work Item States (`/settings/workitem-states`) | No Product behavior | Global Product available but not query-driving | No functional change |
| Home (`/home`) | Page-local product chip bar writes `productId` into the query and can operate as its own selector | Must use the shared global Product state; local chip bar becomes a global-state surface only | Remove local override semantics |
| Health workspace (`/home/health`) | No page-local Product query | Product should remain globally available for downstream navigation | Preserve current behavior |
| Health Overview (`/home/health/overview`) | Catalog marks Product as used via shared filters | Must respect the selected global Product set | Mostly aligned; keep global only |
| Backlog Health (`/home/health/backlog-health`) | Page-local product selector; defaults to route/root parameter or first product | Must use global Product selection only; no first-product fallback | Convert page-local selector into shared-state consumer |
| Validation Triage (`/home/validation-triage`) | Shared filter Product supported | Must respect selected global Product set | Aligned |
| Validation Queue (`/home/validation-queue`) | Shared filter Product supported | Must respect selected global Product set | Aligned |
| Validation Fix (`/home/validation-fix`) | Shared filter Product supported | Must respect selected global Product set | Aligned |
| Home Changes (`/home/changes`) | Product intentionally not part of page filter model | Product remains globally available but should not silently constrain change-log behavior unless explicitly intended | Keep product non-driving |
| Delivery workspace (`/home/delivery`) | No direct Product query | Global Product should flow to delivery destinations | Preserve current behavior |
| Sprint Delivery (`/home/delivery/sprint`) | Shared Product filter supported | Must respect selected global Product set | Aligned |
| Sprint Delivery Activity (`/home/delivery/sprint/activity/{id}`) | Shared Product filter supported | Must respect selected global Product set | Aligned |
| Sprint Execution (`/home/delivery/execution`) | Shared Product filter supported | Must respect selected global Product set | Aligned |
| Portfolio Delivery (`/home/delivery/portfolio`) | Shared Product filter supported | Product becomes mandatory global scope for portfolio query | Strengthen requirement; no all-data fallback outside global state |
| Trends workspace (`/home/trends`) | Product included in shared context | Product remains global and persistent | Aligned |
| Delivery Trends (`/home/trends/delivery`) | Shared Product filter supported | Must respect selected global Product set | Aligned |
| Portfolio Flow Trend (`/home/portfolio-progress`) | Shared Product filter supported | Product becomes mandatory global scope for portfolio query | Strengthen requirement |
| Pull Request Insights (`/home/pull-requests`) | Catalog does not use Product; page is repository-centered | Global Product should be available but only constrain the page if the query is product-aware | Product remains optional support context |
| PR Delivery Insights (`/home/pr-delivery-insights`) | Product not part of canonical page filter model | Global Product should remain available but secondary to team/sprint context unless data query is product-aware | No hard Product enforcement unless query scope expands |
| Pipeline Insights (`/home/pipeline-insights`) | Shared Product filter supported | Must respect selected global Product set | Aligned |
| Bug Insights (`/home/bugs`) | Shared Product filter supported | Must respect selected global Product set | Aligned, but must stop relying on team as surrogate scope |
| Bugs Triage (`/bugs-triage`) | Product scope comes implicitly from active profile ownership, not explicit global state | Must consume the shared global Product selection and avoid profile-only hidden scoping | Replace implicit ownership-only scoping |
| Planning workspace (`/home/planning`) | No direct Product query | Product stays globally available for planning destinations | Preserve current behavior |
| Project Planning Overview (`/planning/{projectAlias}/overview`) | Project route-owned; Product summarized indirectly, not selected | Product must remain global and visible to downstream planning actions, but page should not silently replace it | Avoid deriving Product from project route |
| Product Roadmaps (`/planning/product-roadmaps`) | Broad page; no route-owned Product, product lanes loaded from project/all context | Must respect selected global Product set when loading lanes | Align lane loading to global Product set |
| Product Roadmaps (`/planning/{projectAlias}/product-roadmaps`) | Project route-owned; product list narrowed from route project | Must still respect global Product selection within that project slice | Remove silent project-to-product override |
| Product Roadmap Editor (`/planning/product-roadmaps/{productId}`) | Product is route-owned and authoritative | Product must not be route-owned; route parameter conflicts with canonical rule | Replace route authority with global Product context |
| Plan Board (`/planning/plan-board`) | Product required; defaults to first owned product when empty | Product must come from global selection; no silent first-product default | Remove implicit first-product default |
| Plan Board (`/planning/{RouteProjectAlias}/plan-board`) | Project route narrows products; page then selects product from context or first matching product | Product must stay globally user-selected inside project scope; no silent substitution | Remove route/project-driven Product override |
| Multi-Product Planning (`/planning/multi-product`) | Shared Product filter supported | Product becomes mandatory global scope for portfolio planning query | Strengthen requirement |
| Work Item Explorer (`/workitems`) | Loads profile-owned products or all products implicitly; no explicit global Product control | Must consume selected global Product set and default to All products from shared state | Replace profile-only hidden scoping |

## Changes applied

These are the alignment changes required to enforce the canonical Product rule.

### 1. Global Product becomes the only Product authority
- Remove route authority semantics for Product from the interaction model.
- Treat page routes as navigation/look-up aids only, not as Product selectors.
- Any page-local Product selector must read and write the shared global Product state instead of maintaining an independent page selection.

### 2. Silent Product overrides must stop
- Remove implicit first-product defaults such as the current Plan Board and Backlog Health fallback behavior.
- Remove hidden profile-only Product scoping such as the current Work Item Explorer and Bugs Triage behavior.
- Stop allowing route Product values to override global selection, especially on Product Roadmap Editor.

### 3. Portfolio queries must require Product explicitly
- Portfolio Delivery, Portfolio Flow Trend, Multi-Product Planning, Product Roadmaps, Home dashboard signals, and similar aggregate pages must query against the selected global Product set.
- “All products” remains valid, but only as an explicit global selection state, not as an unstated page fallback.

### 4. Navigation must preserve Product state
- Product selection must survive workspace-to-workspace navigation.
- Product selection must survive page refresh and direct URL revisit.
- Pages must no longer clear or replace Product state when they cannot resolve a local page-specific Product.

### 5. Route and filter model implications
- Current route-authority logic in `GlobalFilterRouteService` and `FilterStateResolver` conflicts with the canonical rule because it treats route Product as authoritative.
- Current default logic in `GlobalFilterDefaultsService` conflicts with the canonical rule where it injects a first Product for Plan Board.
- Current page implementations that keep local `_selectedProductId` state conflict where that state can diverge from the shared filter state.

## Risks introduced

- **Route compatibility risk:** the current `/planning/product-roadmaps/{productId}` editor route is structurally incompatible with “Product is never route-owned.” Preserving deep links will require a compatibility strategy.
- **Single-vs-multi Product risk:** several pages and helpers currently assume a single primary Product (`PrimaryProductId`) even though the canonical rule allows global “all products” scope. Some pages may need an explicit rule for whether they support one Product, many Products, or All Products.
- **Project/Product tension risk:** project-scoped planning pages currently derive available Products from project route context. Enforcing global Product first may expose invalid Product/project combinations unless the UI clearly constrains selections.
- **Query semantics risk:** pages that were previously profile-scoped or route-scoped may show broader or different data once forced onto the shared Product state.
- **Persistence risk:** if Product persistence is only query-string based, direct navigation and refresh may still appear to reset state unless the global store becomes the single persisted source of truth.
- **User expectation risk:** users may currently rely on page-specific Product memory (for example, Plan Board choosing the first product automatically). Removing silent defaults improves consistency but may feel less convenient without explicit empty-state guidance.
