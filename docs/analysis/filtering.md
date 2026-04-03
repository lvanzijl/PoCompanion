# Global Filters & Context Model Analysis

## 1. Current filter architecture

The repository currently applies filtering through two distinct context-propagation mechanisms and several local component-state patterns rather than one global filter layer.

### 1.1 WorkspaceBase — Home workspace context propagation

`PoTool.Client/Pages/Home/WorkspaceBase.cs` is the base class for all current Home workspace pages.
It stores two protected properties:

- `ProductId` (`int?`) — product scope
- `TeamId` (`int?`) — team scope

These are populated by `ParseContextQueryParameters()`, which reads `productId` and `teamId` from the current URL's query string using `HttpUtility.ParseQueryString`. Outbound navigation uses `BuildContextQuery()` to reconstruct the same parameters into a `?productId=…&teamId=…` suffix appended to route paths.

Profile context is resolved once per page load via `EnsureProfileAsync()`, which calls `IProfileService.GetActiveProfileAsync()` and caches the result via `ProfileService.SetCachedActiveProfileId(activeProfile.Id)`.

Pages that inherit `WorkspaceBase`:

- `ValidationQueuePage` — inherits productId
- `ValidationTriagePage` — inherits productId
- `ValidationFixPage` — inherits productId, adds `category` and `ruleId` query params manually
- `HealthOverviewPage` — inherits productId
- `SprintExecution` — inherits productId, adds `sprintId` as a separate URL parameter

### 1.2 INavigationContextService — Legacy workspace context propagation

`PoTool.Client/Services/INavigationContextService` (implemented by `NavigationContextService`) manages a richer, immutable context object used exclusively by the remaining legacy workspace pages:

- `AnalysisWorkspace`
- `ProductWorkspace`
- `TeamWorkspace`

The context object carries:

- `Intent` (enum: Plannen / Begrijpen / Overzien)
- `Scope.ProductId` and `Scope.TeamId`
- `TimeHorizon` (Current / Future / Past)
- `Mode` and `Trigger`

Navigation is performed via `NavigateWithContextAsync(route, context)`. The full context is serialized to and from URL query strings using `ToQueryString()` and `FromQueryString()`. A stack (`_contextStack`) tracks back-navigation history.

These pages do **not** inherit `WorkspaceBase`.

### 1.3 Local component-state filters

Many Home workspace pages manage filter state locally as Blazor component fields, bypassing `WorkspaceBase` entirely for their filtering beyond `productId` and `teamId`. The most common pattern is a collapsible `MudPaper` filter panel containing `MudSelect` dropdowns wired to `OnTeamChanged` / `OnSprintChanged` / `OnProductChanged` event handlers.

Pages with full local filter panels:

| Page | Local filter fields |
|---|---|
| `BacklogOverviewPage` | productId |
| `BugOverview` | productId, teamId |
| `DeliveryTrends` | productId, teamId, sprintId (sprint range) |
| `PortfolioDelivery` | fromSprintId, toSprintId |
| `PortfolioProgressPage` | productId, fromSprintId, toSprintId |
| `PipelineInsights` | productId, teamId, sprintId |
| `PrDeliveryInsights` | productId, teamId, sprintId |
| `PrOverview` | productId, teamId, sprintId |
| `PlanBoard` | productId |
| `ProductRoadmaps` | productId |
| `DependencyOverview` | productId |
| `SprintTrend` | productId (list), teamId, sprintId (navigation) |

`SprintExecution` uses a popover-based pattern (not a collapsible panel) but also keeps local `_selectedSprintId` and `_selectedProductId` fields.

### 1.4 API-level filtering

Backend handlers receive filter dimensions as query class constructor parameters. The dominant pattern is:

```
ProductOwnerId (required) + ProductIds[]? + SprintIds[]? + TeamIds[]?
```

Key query types and their filter signatures:

| Query class | Required | Optional |
|---|---|---|
| `GetSprintExecutionQuery` | `ProductOwnerId`, `SprintId` | `ProductId?` |
| `GetCapacityCalibrationQuery` | `ProductOwnerId`, `SprintIds[]` | `ProductIds[]?` |
| `GetPortfolioProgressTrendQuery` | `ProductOwnerId`, `SprintIds[]` | `ProductIds[]?` |
| `GetPortfolioDeliveryQuery` | `ProductOwnerId`, `SprintIds[]` | — |
| `GetSprintTrendMetricsQuery` | `ProductOwnerId`, `SprintIds[]` | — |
| `GetValidationQueueQuery` | — | `CategoryKey`, `ProductIds[]?` |
| `GetValidationTriageSummaryQuery` | — | `ProductIds[]?` |
| `GetPipelineInsightsQuery` | — | `ProductIds[]?`, `TeamIds[]?` |
| `GetHealthWorkspaceProductSummaryQuery` | `ProductId` | — |
| `GetProductBacklogStateQuery` | `ProductId` | — |
| `GetHomeProductBarMetricsQuery` | `ProductOwnerId` | `ProductId?` |

Controllers translate HTTP query parameters (e.g., `productIds[]`, `sprintIds[]`, `categoryKey`) into these query records and delegate them to Mediator handlers. Filtering is enforced at the handler layer inside the database query (`Where` clause), not at the controller layer.

---

## 2. Filter dimensions

The following dimensions appear across the codebase:

| Dimension | URL param | Component field name | Query class param |
|---|---|---|---|
| Product Owner (Profile) | `productOwnerId` | `_profileId` | `ProductOwnerId` |
| Product | `productId` | `_selectedProductId` | `ProductId` / `ProductIds[]` |
| Team | `teamId` | `_selectedTeamId` | `TeamIds[]` |
| Sprint | `sprintId` | `_selectedSprintId` | `SprintId` / `SprintIds[]` |
| Sprint range (from) | — | `_fromSprintId` | `SprintIds[]` (ordered list) |
| Sprint range (to) | — | `_toSprintId` | `SprintIds[]` (ordered list) |

`WorkspaceBase` covers only `productId` and `teamId`. The `sprintId` dimension is absent from the base class and is handled ad-hoc by each page. `ProductOwnerId` is resolved via `IProfileService` on every page independently.

---

## 3. Conflicts with desired global model

### 3.1 No shared global filter service

There is no client-side service that owns the currently active product, team, sprint, or profile selection. State lives in:

- `WorkspaceBase` protected properties (productId, teamId) for some pages
- Local `_selected*` fields in component code-behind for most pages
- `IProfileService._cachedActiveProfileId` (profile scope only)

Every page that needs a filter must declare its own fields, load the relevant lists (products, teams, sprints) from the API, and wire change handlers. The same initialization sequence is repeated across at least twelve pages.

### 3.2 SprintId is not a first-class base-class concern

`WorkspaceBase.ParseContextQueryParameters` does not extract `sprintId`. Pages that need sprint context parse the URL parameter themselves or use local state. There is no `BuildContextQuery` equivalent that includes sprint context, so sprint state is not propagated through navigation links the way productId/teamId are.

### 3.3 Two competing context-propagation models

Home pages (inheriting `WorkspaceBase`) use a flat, stateless URL-query-parameter approach. Legacy pages (injecting `INavigationContextService`) use a richer, stack-based, immutable-context approach with Intent/Scope/TimeHorizon semantics.

These are incompatible: a user navigating from a Legacy page to a Home page loses all context beyond productId/teamId, and vice versa. There is no bridge layer.

### 3.4 ProfileService is not a true filter layer

`IProfileService` provides `ProductOwnerId` (the active profile ID), which scopes all backend queries. However it is not architecturally treated as a filter dimension — it is a session-level credential rather than a user-selected filter. Pages call `EnsureProfileAsync()` individually and the result is not shared reactively with child components.

### 3.5 Mixed placement of sprint loading logic

Pages that need a sprint list must individually inject `SprintService`, choose whether to auto-select the current sprint, and determine what to do when no sprint is found. The logic for loading sprints for a given team and defaulting to the current sprint is duplicated across `SprintExecution`, `PipelineInsights`, `PrOverview`, `PrDeliveryInsights`, `DeliveryTrends`, and `SprintTrend`.

### 3.6 Product list loading is duplicated

Every page with a product selector injects `ProductService` and calls a loading method that returns the PO-owned product list. This is the same call on every page; there is no shared product catalog subscription.

---

## 4. Required refactor points

### 4.1 Introduce a GlobalFilterService (client-side)

A singleton (or scoped Blazor service) that owns:

- `ActiveProductOwnerId` (derived from `IProfileService`)
- `SelectedProductId` (nullable, user selection)
- `SelectedTeamId` (nullable, user selection)
- `SelectedSprintId` (nullable, user selection)

This service would raise a `FilterChanged` event/observable so pages re-render when the selection changes. `WorkspaceBase` could subscribe to it instead of carrying protected properties. High-priority pages for migration: `PipelineInsights`, `PrOverview`, `PrDeliveryInsights`, `SprintExecution`.

### 4.2 Extend WorkspaceBase with SprintId

Add `SprintId` as a protected property alongside `ProductId` and `TeamId`, and update `ParseContextQueryParameters()` and `BuildContextQuery()` to include `sprintId`. This eliminates per-page custom URL parsing for the sprint dimension.

### 4.3 Extract shared sprint-loading helper

Move the "load sprints for team, default to current sprint" sequence into a reusable method — either on `WorkspaceBase`, in `SprintService`, or in a dedicated helper — and replace the six current inline implementations.

### 4.4 Expose product list as a reactive catalog

`ProductService` should expose a cached list of the active PO's products so pages subscribe rather than each making an independent API call on initialization. This also enables a global product-selector component that updates all subscribed pages simultaneously.

### 4.5 Introduce a shared filter-panel component

A reusable Blazor component (e.g., `WorkspaceFilterPanel`) that renders the product/team/sprint selectors and delegates to `GlobalFilterService`. Each page embeds it instead of duplicating the collapsible `MudPaper` filter block. Pages that currently contain identical filter UX: `PipelineInsights`, `PrOverview`, `PrDeliveryInsights`.

### 4.6 Align or sunset the Legacy context model

Decide whether `INavigationContextService` will be extended to cover the Home workspace pages or retired as part of the Legacy workspace migration. Until that decision is made, the two models should at minimum share the same URL parameter names (`productId`, `teamId`) to allow partial context transfer across navigation boundaries.

### 4.7 Standardize API query filter transport

Consolidate the optional-product-list / optional-team-list / sprint-list pattern into a shared `WorkspaceFilterContext` input record that is accepted by all relevant query handlers. This reduces repetition in query class definitions and allows the filter transport contract to evolve in one place.

---

## 5. Summary

| Layer | Current state | Desired state |
|---|---|---|
| Client filter state | Per-page local fields | `GlobalFilterService` singleton |
| Context propagation | `WorkspaceBase` (productId, teamId) + local sprint | `WorkspaceBase` + `GlobalFilterService` (all dimensions) |
| Legacy context | `INavigationContextService` (separate model) | Aligned or replaced |
| Sprint loading | Duplicated in ≥ 6 pages | Shared helper / service method |
| Product list loading | Duplicated in ≥ 10 pages | Reactive catalog in `ProductService` |
| Filter UI | Duplicated `MudPaper` blocks in ≥ 3 pages | `WorkspaceFilterPanel` component |
| API filtering | Consistent (query class params, handler-enforced) | No change needed |
