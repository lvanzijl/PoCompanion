# Route Authority Changes

- Added explicit route-authority metadata to global filter resolutions so route-scoped project and product pages can distinguish path-owned context from editable query-owned filters.
- Updated route rebuilding to preserve route-scoped planning and roadmap paths and to stop echoing route-owned project/product values back into the query string.
- Disabled project/product edits in shared filter controls when the active route owns that context.
- Stopped global filter correction from rewriting route-authoritative pages.
- Normalized unresolved route project aliases as route-authoritative context instead of treating them as invalid page state, which keeps project-scoped planning pages stable even when the route alias cannot be translated into a cached canonical project ID.

# Default Filter Definitions per Page

## Shared rules
- Product default
  - Keep route-owned `productId` when present.
  - Use **all products** by default on overview-style pages.
  - Use the **first owned product** on `PlanBoard` so the board is populated on first load.
- Team default
  - Use the **first valid team** reachable from the active profile's owned products.
- Sprint default
  - Sprint pages use the team's **current sprint**.
  - Range pages use a **five-sprint window ending at the current sprint**.
  - Rolling pages use **30 days** when no rolling window is selected.
- Session behavior
  - Defaults apply **once per route signature per browser session** using session storage.
  - Defaults do **not** re-apply after user-driven filter changes.

## Page-specific outcomes
- `SprintExecution` → first valid team + current sprint.
- `SprintTrend` / `SprintTrendActivity` → first valid team + current sprint.
- `PipelineInsights` → first valid team + current sprint.
- `PrDeliveryInsights` → first valid team + current sprint.
- `DeliveryTrends` → first valid team + 5-sprint range ending at current sprint.
- `PortfolioDelivery` → first valid team + 5-sprint range ending at current sprint.
- `PortfolioProgressPage` → first valid team + 5-sprint range ending at current sprint.
- `TrendsWorkspace` trend signal loading → first valid team + 5-sprint range ending at current sprint when a downstream trend page is opened directly.
- `PrOverview` → first valid team + rolling 30-day window.
- `PlanBoard` → first owned product.
- Route-scoped planning pages (`/planning/{projectAlias}/overview`, `/planning/{projectAlias}/plan-board`, `/planning/{projectAlias}/product-roadmaps`, `/planning/product-roadmaps/{productId}`) keep route-owned context and do not allow shared filters to override it.

# Pages Fixed (before vs after behavior)

- **`/planning/{projectAlias}/overview`**
  - Before: global filter correction redirected the page away from the route or flagged it as invalid route state.
  - After: the route remains on-page, project context is treated as route-owned, and shared filters no longer offer project overrides.

- **`/planning/plan-board`**
  - Before: the first view opened on an empty board until the user chose a product.
  - After: the first owned battleship product is selected automatically on initial load, so the board renders real backlog content above the fold.

- **`/home/delivery/execution`**
  - Before: the page opened blocked behind team and sprint requirements.
  - After: the first valid battleship team and current sprint are applied on initial load so the page opens with a concrete delivery context.

- **Other team/sprint-gated pages (`SprintTrend`, `PipelineInsights`, `PrDeliveryInsights`, range-based trend pages, PR rolling view`)**
  - Before: pages opened in an unresolved filter state and required manual setup before showing meaningful signals.
  - After: pages initialize with stable mock defaults once per session and keep those defaults unless the user changes them.

- **Shared layout**
  - Before: the full shared filter panel rendered above page content.
  - After: compact filter summary remains near the top, but the full filter controls render below the main page body so the primary signal stays above the fold.

# Remaining Risks

- `/planning/{projectAlias}/overview` is now stable, but the project planning summary still renders as an empty cross-product summary for the battleship route; the route blocker is removed, but the summary data contract still needs a follow-up if the page should show richer project-level totals.
- Shared filter summary chips still show raw IDs or empty placeholders such as `()` for project context, which is now less disruptive because the controls moved below the page body but is still not ideal.
- Default team selection currently uses the first valid team discovered from the active profile's owned products; if product/team ordering changes, the exact initial team may change as well.
- Session-scoped default application intentionally does not re-run after the user clears filters on the same route, so a manual reset can still leave a page intentionally sparse until the user chooses a new context.
