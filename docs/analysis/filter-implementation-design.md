> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Canonical Filter Implementation Design

## 1. Target Architecture

This document defines the concrete implementation design for the canonical filter system in PoTool.

It bridges:

- the **current fragmented filter reality** described in `docs/analysis/filter-current-state-analysis.md`
- the **target canonical state and behavior** described in:
  - `docs/analysis/canonical_filter_state_model.md`
  - `docs/analysis/page-filter-contracts.md`
  - `docs/analysis/filter-ui-behavior.md`
  - `docs/analysis/filter-canonical-model.md`
  - `docs/analysis/filter-implementation-plan.md`

The design goal is one filter system with:

- one canonical state model
- one validation model
- one URL model
- one request-mapping model
- explicit selected vs effective vs applicable vs invalid semantics
- temporary compatibility with legacy page wiring and legacy endpoints

## Final implementation status (2026-03-28)

The final shipped implementation completed the canonical filtering workstream at the API boundary and in the migrated client response flow, but it did not materialize every planned client-side design type in this document.

Implemented final state:

- `PoTool.Api` owns active canonical filter execution through family-specific resolution services (`SprintFilterResolutionService`, `DeliveryFilterResolutionService`, `PipelineFilterResolutionService`, `PullRequestFilterResolutionService`, `PortfolioFilterResolutionService`)
- handlers and downstream services consume effective filter data produced by those boundary services
- migrated endpoints return requested/effective/invalid metadata in family-specific envelopes
- `PoTool.Client` preserves that metadata through `CanonicalClientResponseFactory` and surfaces it in migrated pages/components

Intentionally retained differences from the original design target:

- no standalone shared client `FilterState` / `FilterService` runtime was introduced
- no standalone shared `FilterUrlParser` / `FilterUrlSerializer` / `FilterNavigationAdapter` was introduced
- home/workspace navigation still uses `WorkspaceBase` and `NavigationContextService` for coarse context propagation (`productId`, `teamId`)
- client service methods still adapt to endpoint-family transport contracts such as `productIds`, sprint collections, and date-range parameters

The remainder of this document should therefore be read as the design rationale and target shape that informed the implementation, with the bullets above describing the actual final shipped architecture.

### 1.1 Layer responsibilities

| Layer | Responsibility | Filter-related contents | Must NOT contain |
|---|---|---|---|
| `PoTool.Shared` | Cross-boundary filter contracts, enums, URL-safe DTO shapes, backend request/response filter metadata | canonical filter keys/enums, value DTOs, `FilterRequestDto`, `FilterResponseMetadataDto`, time-mode enums, page-contract DTOs if shared across boundary | page logic, validation lookups, navigation services, page-specific defaults |
| `PoTool.Client` | Canonical filter state ownership, page applicability, URL sync, validation orchestration, request mapping to current API shapes | `FilterState`, `FilterService`, page contracts, URL parser/serializer, validation engine, request mappers, reusable filter UI adapters | backend authorization, repository membership truth that requires secure enforcement, endpoint-specific page logic in components |
| `PoTool.Api` | Boundary revalidation, legacy endpoint adaptation, canonical response metadata production, request-to-query normalization | API-side filter validator/resolver, request adapters, compatibility mappers, response metadata builder | UI state, navigation context, page applicability decisions |
| `PoTool.Core` | Canonical filter domain/value model only where backend/application logic needs filter semantics independent of transport | backend canonical filter value objects, time selection model, resolver inputs for query/application use | HTTP, query-string parsing, client page contracts, controller DTOs |

### 1.2 Architectural rule

The filter system is split into three explicit concerns:

1. **State concern** — what the user selected and what the page can use
2. **Execution concern** — what the page/backend actually uses
3. **Compatibility concern** — how canonical effective filters adapt to current endpoints during migration

That split is required because the current codebase mixes them inside pages:

- `WorkspaceBase` mixes shared navigation context with URL parsing
- PR pages mix sprint selection with sprint-to-date translation
- validation pages mix product resolution with request building
- portfolio pages mix sprint-driven filters with snapshot/read-model filters

### 1.3 Target ownership model

- **User intent** lives in `SelectedFilters`
- **Page applicability** lives in `PageFilterContract`
- **Validity** lives in `InvalidFilters`
- **Backend execution** uses only `EffectiveFilters`
- **Transport compatibility** lives in mappers/adapters

No page should own these transformations after migration.

### 1.4 Solution placement

#### `PoTool.Shared/Filters/`
Contains canonical filter definitions that must cross process or assembly boundaries.

Suggested folders:

- `PoTool.Shared/Filters/Definitions/`
- `PoTool.Shared/Filters/Values/`
- `PoTool.Shared/Filters/Contracts/`
- `PoTool.Shared/Filters/Responses/`

#### `PoTool.Client/Filters/`
Contains the canonical client filtering runtime.

Suggested folders:

- `PoTool.Client/Filters/State/`
- `PoTool.Client/Filters/Definitions/`
- `PoTool.Client/Filters/Contracts/`
- `PoTool.Client/Filters/Validation/`
- `PoTool.Client/Filters/Url/`
- `PoTool.Client/Filters/Mappers/`
- `PoTool.Client/Filters/Services/`

#### `PoTool.Api/Filters/`
Contains API-boundary filter enforcement and compatibility mapping.

Suggested folders:

- `PoTool.Api/Filters/Validation/`
- `PoTool.Api/Filters/Mappers/`
- `PoTool.Api/Filters/Responses/`

#### `PoTool.Core/Filters/`
Contains canonical backend filter value types and query-facing filter semantics where backend logic needs them independent of DTO transport.

---

## 2. Core Types

This section defines the concrete types needed to implement the canonical filter system.

### 2.1 `FilterState`

- **Purpose:** single source of truth for the client filter runtime
- **Suggested name:** `FilterState`
- **Suggested location:** `PoTool.Client/Filters/State/FilterState.cs`
- **Key fields:**
  - `SelectedFilters SelectedFilters`
  - `EffectiveFilters EffectiveFilters`
  - `ApplicableFilters ApplicableFilters`
  - `IReadOnlyList<InvalidFilterEntry> InvalidFilters`
  - `FilterStateVersion Version` or equivalent change token
- **Relationship:** root container; computed by `FilterService`

### 2.2 `SelectedFilters`

- **Purpose:** exact user selections, including invalid and not-applicable values
- **Suggested name:** `SelectedFilters`
- **Suggested location:** `PoTool.Client/Filters/State/SelectedFilters.cs`
- **Key fields:**
  - `ProductFilterValue? Product`
  - `ProjectFilterValue? Project`
  - `TimeFilterValue? Time`
  - `PageFilterSelections Page`
- **Relationship:** input to validation and URL serialization

### 2.3 `EffectiveFilters`

- **Purpose:** validated, page-applicable filters used for data queries
- **Suggested name:** `EffectiveFilters`
- **Suggested location:** `PoTool.Client/Filters/State/EffectiveFilters.cs`
- **Key fields:**
  - same canonical shape as `SelectedFilters`, but only effective values
  - `PageFilterSelections Page`
- **Relationship:** produced from `SelectedFilters + PageFilterContract + validation`

### 2.4 `ApplicableFilters`

- **Purpose:** machine-readable page applicability for filter visibility and execution
- **Suggested name:** `ApplicableFilters`
- **Suggested location:** `PoTool.Client/Filters/State/ApplicableFilters.cs`
- **Key fields:**
  - `bool Product`
  - `bool Project`
  - `bool Time`
  - `bool Team`
  - `bool Repository`
  - `bool ValidationCategory`
  - `bool ValidationRule`
  - `bool PipelineToggles`
  - `bool Tags`
  - `bool Search`
  - `IReadOnlySet<TimeFilterMode> SupportedTimeModes`
- **Relationship:** derived directly from `PageFilterContract`, not from user choices

### 2.5 `InvalidFilters`

- **Purpose:** explicit invalid-state tracking without losing user intent
- **Suggested name:** `InvalidFilterEntry`
- **Suggested location:** `PoTool.Client/Filters/State/InvalidFilterEntry.cs`
- **Key fields:**
  - `FilterKey Filter`
  - `string Code`
  - `string Message`
  - `InvalidFilterScope Scope` (global/page)
  - optional `string? RelatedFilter`
- **Relationship:** computed by validation engine; referenced by UI and response metadata

### 2.6 `FilterDefinition`

- **Purpose:** canonical metadata describing each filter type once
- **Suggested name:** `FilterDefinition<TValue>` plus non-generic registry abstraction
- **Suggested location:**
  - shared contract: `PoTool.Shared/Filters/Definitions/`
  - client registry: `PoTool.Client/Filters/Definitions/FilterRegistry.cs`
- **Key fields:**
  - `FilterKey Key`
  - `FilterCategory Category`
  - `FilterCardinality Cardinality`
  - `FilterPersistencePolicy Persistence`
  - `bool CanAppearInSummary`
  - `bool CanBeDeepLinked`
  - `FilterDefaultBehavior DefaultBehavior`
- **Relationship:** used by validation, URL serialization, and page contracts

### 2.7 Canonical value types

#### Global core
- `ProductRef`
  - `ProductId: int`
  - `DisplayName: string`
- `ProjectRef`
  - `ProjectKey: string`
  - `DisplayName: string`

#### Conditional global
- `TimeFilterValue`
  - discriminated union of:
    - `SingleSprintTimeValue`
    - `SprintRangeTimeValue`
    - `DateRangeTimeValue`

#### Page-level examples
- `TeamRef`
- `RepositoryRef`
- `ValidationCategoryValue`
- `ValidationRuleValue`
- `PipelineToggleFilterValue`
- `TagFilterValue`
- `SearchFilterValue`

Suggested location:
- `PoTool.Shared/Filters/Values/`

### 2.8 Time filter model

- **Purpose:** represent all time selection semantics in one type
- **Suggested name:** `TimeFilterValue`
- **Suggested location:** `PoTool.Shared/Filters/Values/Time/`
- **Key fields:**
  - union discriminator `TimeFilterMode`
  - for single sprint: `SprintRef Sprint`
  - for range: `int FromSprintId`, `int ToSprintId`
  - for date range: `DateTimeOffset FromUtc`, `DateTimeOffset ToUtc`
- **Relationship:** selected globally, effective conditionally per page

### 2.9 `PageFilterSelections`

- **Purpose:** isolate page/workspace-local filters from global filters without inventing page-specific state objects everywhere
- **Suggested name:** `PageFilterSelections`
- **Suggested location:** `PoTool.Client/Filters/State/PageFilterSelections.cs`
- **Key fields:**
  - `TeamRef? Team`
  - `IReadOnlyList<RepositoryRef> Repositories`
  - `IReadOnlyList<ValidationType> ValidationTypes`
  - `ValidationRuleValue? ValidationRule`
  - `PipelineToggleFilterValue? PipelineToggles`
  - `TagFilterValue? Tags`
  - `SearchFilterValue? Search`
- **Relationship:** owned by page contract and request mappers

### 2.10 `FilterRequestDto`

- **Purpose:** canonical backend request envelope built from `EffectiveFilters`
- **Suggested name:** `FilterRequestDto`
- **Suggested location:** `PoTool.Shared/Filters/Contracts/FilterRequestDto.cs`
- **Key fields:**
  - `GlobalFiltersDto Global`
  - `ConditionalFiltersDto Conditional`
  - `PageFiltersDto Page`
- **Relationship:** optional transition target; some endpoints will use compatibility mapping instead of accepting this DTO immediately

### 2.11 `FilterResponseMetadataDto`

- **Purpose:** backend-to-client feedback describing what was requested and what was actually applied
- **Suggested name:** `FilterResponseMetadataDto`
- **Suggested location:** `PoTool.Shared/Filters/Responses/FilterResponseMetadataDto.cs`
- **Key fields:**
  - `SelectedFiltersDto RequestedFilter`
  - `EffectiveFiltersDto EffectiveFilter`
  - `IReadOnlyList<InvalidFilterDto> InvalidFields`
- **Relationship:** attached to endpoint responses directly or via a shared envelope

### 2.12 `PageFilterContract`

- **Purpose:** authoritative declaration of what a page can use
- **Suggested name:** `PageFilterContract`
- **Suggested location:** `PoTool.Client/Filters/Contracts/PageFilterContract.cs`
- **Key fields:**
  - `PageFilterKey Page`
  - `IReadOnlySet<FilterKey> Supported`
  - `IReadOnlySet<FilterKey> Primary`
  - `IReadOnlySet<FilterKey> Advanced`
  - `IReadOnlySet<FilterKey> NotApplicable`
  - `IReadOnlySet<TimeFilterMode> SupportedTimeModes`
  - `IReadOnlyDictionary<FilterKey, FilterDefaultBehavior> RequiredDefaults`
  - `IReadOnlySet<FilterKey> DeepLinkableFilters`
- **Relationship:** consumed by `FilterService`, UI layout, URL layer, request mapping

### 2.13 Optional helper types

Recommended additional types to keep implementation explicit:

- `FilterKey`
- `FilterCategory`
- `FilterVisibility`
- `FilterStatus`
- `TimeFilterMode`
- `FilterChangeSet`
- `FilterStateSnapshot`
- `FilterNavigationContext`

---

## 3. Client Filter Engine

The client filter engine is the runtime that eliminates page-owned filter logic.

### 3.1 Central service

- **Suggested name:** `FilterService`
- **Suggested location:** `PoTool.Client/Filters/Services/FilterService.cs`
- **Lifetime:** `Scoped`

Scoped is correct because:

- it survives navigation within the Blazor session
- it supports shared cross-page remembered state
- it avoids global singleton coupling
- it matches the migration plan and existing Home workspace flow

### 3.2 Ownership model

`FilterService` owns:

- current `FilterState`
- current page contract
- URL sync state
- lookup-backed validation cache references

Pages do not own canonical filter state after migration.

### 3.3 Public API shape

Recommended narrow API:

```text
InitializeForPage(PageFilterKey page, NavigationContext? context = null)
GetState() -> FilterState
SetSelectedFilter(FilterKey key, object? value)
SetSelectedTimeMode(TimeFilterMode mode, object? value)
ClearFilter(FilterKey key)
ResetPageFilters()
GetPageView(PageFilterKey page) -> PageFilterView
Subscribe(...) / Unsubscribe(...)
```

The exact code shape may vary, but the service API must preserve these semantics:

- pages update only selected values
- pages read only canonical state
- pages never compute effective values directly

### 3.4 Internal processing flow

When a page updates a filter:

1. `FilterService` updates `SelectedFilters`
2. current `PageFilterContract` is resolved
3. `ApplicableFilters` is rebuilt from the contract
4. validation engine runs
5. `InvalidFilters` is rebuilt
6. `EffectiveFilters` is rebuilt
7. URL serializer is invoked if this page supports URL sync
8. subscribers are notified with the new `FilterState`

### 3.5 Validation trigger model

Validation must run:

- on page initialization
- on every filter change
- on URL restore
- on page contract change
- after lookup data refresh if membership constraints depend on loaded data

### 3.6 Subscription model

Recommended model:

- event-based `StateChanged`
- event args include `FilterChangeSet`
- page decides whether its relevant `EffectiveFilters` changed before reloading

Required behavior:

- pages unsubscribe on disposal
- reload only when effective state changed
- do not reload for purely presentational state changes

### 3.7 How pages consume the engine

Each page should follow one pattern:

1. resolve its `PageFilterContract`
2. call `FilterService.InitializeForPage(...)`
3. bind UI controls to `SelectedFilters`
4. render visibility/state from `ApplicableFilters + InvalidFilters`
5. build backend requests only from `EffectiveFilters`

### 3.8 How page-owned logic is eliminated

Current duplicated page behavior becomes shared engine behavior:

- page-local sprint defaults -> time resolver service
- page-local product/project constraint checks -> validation engine
- page-local query builders -> URL serializer + request mapper
- page-local request-parameter composition -> endpoint mapper

Pages remain responsible only for:

- declaring their contract
- binding controls
- requesting data when effective state changes

---

## 4. Page Contract Mechanism

### 4.1 Where contracts live

Contracts should live in:

- `PoTool.Client/Filters/Contracts/`

Suggested files:

- `PageFilterContract.cs`
- `PageFilterContractRegistry.cs`
- one contract per page family or workspace page

### 4.2 Registration mechanism

Pages must not hand-build contracts inline.

Use one registry/service:

- `IPageFilterContractRegistry`
- `Get(PageFilterKey page)`

Registration options:

- explicit dictionary registration in one place
- static contract instances grouped by workspace family

Preferred design: static, code-declared contracts grouped by page family to keep behavior reviewable.

### 4.3 Contract responsibilities

Each contract declares:

- supported filters
- primary filters
- advanced filters
- not-applicable filters
- supported time modes
- deep-linkable filters
- page-required defaults
- page-local-only filters

### 4.4 Page categories mapped to repository reality

#### Home / Landing
Contract:
- supports `Product`, `Project`
- `Time` is remembered but not applicable
- no page filters effective

Replaces current `HomePage` local product state plus duplicated `BuildContextQuery()` behavior.

#### Health pages
Contract:
- supports `Product`, `Project`
- supports validation drill-down filters where relevant
- `Time` not applicable

Replaces implicit reliance on `WorkspaceBase` product context only.

#### Delivery workspace pages
Examples:
- `DeliveryTrends.razor`
- `SprintExecution.razor`
- `PortfolioDelivery.razor`
- `PortfolioProgressPage.razor`

Contract:
- `Product`, `Project`, `Time`
- optional `Team`
- page-specific advanced filters as needed
- explicit supported time modes per page

#### Trends / Portfolio pages
Examples:
- `TrendsWorkspace.razor`
- `PortfolioProgressPage.razor`
- `PortfolioCdcReadOnlyPanel.razor`

Contract:
- `Product`, `Project`, `Time`
- optional future `Team`
- portfolio advanced fields remain page-level until canonicalized into the broader model

#### PR pages
Examples:
- `PrOverview.razor`
- `PrDeliveryInsights.razor`

Contract:
- `Product`, `Project`, `Time`, `Repository`
- optional advanced `Team`
- supported time modes declared explicitly

#### Pipeline pages
Examples:
- `PipelineInsights.razor`

Contract:
- `Product`, `Project`, `Time`, `Repository`
- advanced pipeline toggles

#### Validation / Work Item flows
Examples:
- `ValidationTriagePage.razor`
- `ValidationQueuePage.razor`
- `ValidationFixPage.razor`
- `WorkItemExplorer.razor`

Contract:
- `Product`, `Project`
- advanced validation category/rule
- time generally not applicable

### 4.5 Primary vs advanced vs disabled mapping

This comes from the contract, not UI heuristics.

- `Primary` drives always-visible expanded controls
- `Advanced` drives advanced drawer/panel sections
- `NotApplicable` drives disabled-context section
- `SupportedTimeModes` drives time-mode UI and validation

### 4.6 Why page contracts are required

They are the only reliable way to distinguish:

- invalid filter
- ignored but remembered filter
- unsupported time mode
- hidden because page does not support the filter

Without explicit contracts, the current page-owned behavior would reappear in a different form.

---

## 5. Validation and Resolution Design

### 5.1 Validation responsibilities

#### Client-side validation
Runs in `PoTool.Client/Filters/Validation/`

Responsibilities:

- cross-filter consistency
- page applicability
- supported time modes
- lookup-based membership checks when safe client data is available
- preserving selected invalid values

#### Backend validation
Runs in `PoTool.Api/Filters/Validation/`

Responsibilities:

- revalidate effective filters at the boundary
- protect against tampered URLs, stale state, unsupported combinations, and unauthorized scope
- normalize endpoint inputs before query handlers use them

### 5.2 Validation pipeline

1. parse URL into `SelectedFilters`
2. resolve page contract
3. compute `ApplicableFilters`
4. validate selected values against:
   - definitions
   - page contract
   - membership constraints
   - structural rules
5. produce `InvalidFilters`
6. compute `EffectiveFilters`
7. inject defaults only into `EffectiveFilters`

### 5.3 Invalid vs ignored distinction

#### Ignored / not applicable
A selected filter is ignored when:

- it is valid in itself
- current page does not support it

Then:

- keep it in `SelectedFilters`
- do not place it in `InvalidFilters`
- exclude it from `EffectiveFilters`
- expose it in `DisabledContext`

#### Invalid
A selected filter is invalid when:

- it conflicts with another selected filter
- it violates membership rules
- it has an invalid structure
- its selected time mode is unsupported on the page

Then:

- keep it in `SelectedFilters`
- include it in `InvalidFilters`
- exclude it from `EffectiveFilters`
- do not silently rewrite the selected value

### 5.4 Default resolution

Defaults belong only in resolution, never in page code.

Canonical defaults:

- Product -> All Products
- Project -> All Projects
- Time -> Current Sprint when page supports time and contract requires time defaulting
- Team -> None

Page-specific defaults are declared in `PageFilterContract.RequiredDefaults`, not hardcoded in pages.

### 5.5 Product / Project constraint

The design must support both selection orders:

- Product first
- Project first

Resolution rule:

- selected values remain visible
- invalid axis is marked invalid
- invalid axis drops from `EffectiveFilters`
- remaining valid axis stays effective

This replaces the current absence of product/project enforcement outside the CDC portfolio panel.

### 5.6 Time applicability and mode resolution

`Time` is globally remembered but page-conditional.

Resolution rule:

- page supports time and selected mode -> effective
- page supports time but not that mode -> invalid, not effective
- page does not support time -> not applicable, remembered, not effective

### 5.7 What API receives vs what queries receive

#### API boundary receives
Either:

- canonical `FilterRequestDto`, or
- legacy endpoint inputs produced by client compatibility mapping

In both cases, API boundary logic must reconstruct a backend canonical filter context before query execution.

#### Query handlers receive
- only validated backend effective filter data
- never raw selected values
- never ambiguous page-owned defaults

This is the core enforcement boundary that prevents silent or implicit filtering in query logic.

---

## 6. URL and Navigation Design

### 6.1 Canonical parameter names

Use one canonical query vocabulary:

#### Global core
- `productId`
- `projectKey`

#### Time
- `timeMode`
- `sprintId`
- `fromSprintId`
- `toSprintId`
- `fromUtc`
- `toUtc`

#### Page filters
- `teamId`
- `repositoryIds`
- `validationTypes`
- `validationRuleId`
- `pipelineIds`
- `prStatuses`
- `tags`
- `search`

### 6.2 URL parsing design

Suggested client types:

- `FilterUrlParser`
- `FilterUrlSerializer`
- `FilterNavigationAdapter`

Flow:

1. navigation target determines page contract
2. parser reads canonical keys
3. parser also reads legacy aliases if configured for that page during migration
4. parser produces `SelectedFilters`
5. `FilterService` validates and resolves

### 6.3 URL serialization design

Only canonical parameter names are emitted for migrated pages.

Serialization source:

- `SelectedFilters` for shareable state
- filtered through page contract deep-link rules

Important rule:

- URL represents **selected** state, not effective fallback state

This is required by the UI behavior doc: invalid selections must remain visible and shareable.

### 6.4 Navigation synchronization

Use one navigation adapter that replaces:

- `WorkspaceBase.ParseContextQueryParameters()`
- `WorkspaceBase.BuildContextQuery()`
- `HomePage.BuildContextQuery()`
- page-specific `UpdateUrlParameters()` helpers
- page-specific `ParseSprintQueryParameters()`

Navigation behavior:

- on page activation: parse URL -> state
- on meaningful state change: serialize state -> URL
- on programmatic navigation between workspaces: carry global core and remembered conditional filters automatically

### 6.5 Legacy parameter handling during migration

The parser must temporarily accept existing names such as:

- `productId`
- `teamId`
- `fromSprintId`
- `toSprintId`
- `category`
- `validationCategory`
- `ruleId`
- `repositoryName`

But migrated pages must emit only canonical names.

### 6.6 URL persistence rules

- global core filters are always deep-linkable
- conditional global time filter is deep-linkable when supported by the page
- page filters are deep-linkable only when the contract marks them as shareable
- local UI-only refinements like client-side author highlight should stay outside canonical filter URL state unless explicitly elevated later

---

## 7. Request Mapping Design

### 7.1 Mapping location

Mapping occurs in the client compatibility layer after validation/resolution.

Suggested location:

- `PoTool.Client/Filters/Mappers/`

Suggested structure:

- `IEndpointFilterMapper<TRequest>`
- one mapper per endpoint family
- mappers accept `EffectiveFilters + PageFilterContract`

Pages must stop building request parameters directly.

### 7.2 Mapping rule

Input to all request mapping:

- `EffectiveFilters`

Never:

- `SelectedFilters`
- raw page fields
- page-built query strings

### 7.3 Legacy endpoint coexistence

Until endpoints are redesigned, support three mapper modes:

1. canonical -> legacy query params
2. canonical -> legacy typed client call args
3. canonical -> future `FilterRequestDto`

This isolates old transport shapes without keeping old page logic.

### 7.4 Validation / work item flows

Current duplication:

- validation pages rebuild `productIds`
- `WorkItemService` serializes comma-separated values

Target mapping:

- `ValidationRequestMapper`
- input: `EffectiveFilters.Global.Product`, `EffectiveFilters.Global.Project`, validation page filters
- output: current validation endpoint shape (`productIds`, category, rule) until endpoint redesign

### 7.5 Sprint / delivery / trends flows

Current duplication:

- team is often only a sprint-loader
- sprint range expands per page
- product filter is sometimes client-side only

Target mapping:

- `DeliveryMetricsRequestMapper`
- input: `EffectiveFilters.Global`, `EffectiveFilters.Time`, page `Team`
- output: current endpoint-specific shapes such as:
  - `sprintIds[]`
  - `sprintId`
  - optional `productIds[]`

Team-to-sprint dependency is resolved by shared time/sprint support services before mapping, not in the page.

### 7.6 PR flows

Current duplication:

- PR Overview converts sprint to dates on the page
- PR Delivery sends both sprint and dates

Target mapping:

- `PullRequestInsightsRequestMapper`
- one canonical time input, endpoint-specific output

Examples:

- PR Overview mapper converts canonical time to `fromDate`, `toDate`, optional `repositoryName`
- PR Delivery mapper converts canonical time to `sprintId` and derived dates if that endpoint still requires both

Pages no longer translate sprint-to-date themselves.

### 7.7 Pipeline flows

Current duplication:

- team selects sprint list, but final API ignores team
- toggles are page-local

Target mapping:

- `PipelineInsightsRequestMapper`
- input: `EffectiveFilters.Time`, `EffectiveFilters.Page.Repository`, pipeline toggles
- output: current pipeline endpoint args

Team remains a dependency for sprint lookup until endpoint semantics evolve, but that dependency is owned by shared filter-support services.

### 7.8 Portfolio flows

Current fragmentation:

- main portfolio pages use sprint-driven filtering
- embedded CDC panel uses snapshot/read-model filtering

Target mapping:

- `PortfolioFlowRequestMapper` for sprint-driven trend/delivery endpoints
- `PortfolioReadModelRequestMapper` for `/api/portfolio/*`
- both map from the same canonical `EffectiveFilters`

This preserves temporary endpoint coexistence while removing the dual filter model.

---

## 8. Duplication Replacement Map

| Current duplication | Where it exists now | Replacement component |
|---|---|---|
| Product -> `productIds` reconstruction | `ValidationTriagePage`, `ValidationQueuePage`, `ValidationFixPage` | `ValidationRequestMapper` + shared product scope resolver |
| Team -> sprints -> current sprint fallback | `PipelineInsights`, `PrOverview`, `PrDeliveryInsights` | `SprintContextService` / `TimeSupportService` used by `FilterService` |
| Sprint-range expansion to `sprintIds[]` | `DeliveryTrends`, `PortfolioProgressPage`, `PortfolioDelivery`, `TrendsWorkspace` | `TimeRangeResolver` + delivery/trends request mappers |
| Sprint -> date-range conversion | `PrOverview`, `PrDeliveryInsights`, parts of `TrendsWorkspace` | `TimeTranslationService` used only inside request mappers |
| Query-string builder duplication | `WorkspaceBase.BuildContextQuery()`, `HomePage.BuildContextQuery()`, `DeliveryTrends.UpdateUrlParameters()`, `TrendsWorkspace.UpdateSprintUrlParameters()` | `FilterUrlSerializer` + `FilterNavigationAdapter` |
| Page-specific query parsing | `WorkspaceBase`, PR pages, trends workspace, validation pages | `FilterUrlParser` |
| Parallel portfolio filter systems | `PortfolioProgressPage` main trend query vs `PortfolioCdcReadOnlyPanel` snapshot/read-model query set | shared canonical `FilterState` + separate portfolio endpoint mappers |
| Product applied only client-side after backend response | `DeliveryTrends` | canonical delivery mapper that sends effective product scope to backend where supported, otherwise explicit compatibility adapter |
| Team used as quasi-global in some pages and local sprint-loader in others | Delivery, PR, Pipeline pages | `PageFilterContract` + `SprintContextService` to separate page filter semantics from execution |
| Inconsistent naming (`productId` vs `productIds`, `category` vs `validationCategory`, `repositoryName` vs `repositoryId`) | multiple pages and clients | canonical filter keys + endpoint-family compatibility mappers |

### 8.1 Shared services implied by the replacement map

Recommended shared components:

- `FilterService`
- `FilterRegistry`
- `PageFilterContractRegistry`
- `FilterUrlParser`
- `FilterUrlSerializer`
- `FilterNavigationAdapter`
- `FilterValidationEngine`
- `FilterResolutionEngine`
- `SprintContextService`
- `TimeRangeResolver`
- `TimeTranslationService`
- endpoint-family request mappers

---

## 9. API Response Design

### 9.1 Required metadata

For later UI work, responses must expose:

- `RequestedFilter`
- `EffectiveFilter`
- `InvalidFields`

This metadata represents backend truth after API-boundary validation/resolution.

### 9.2 Where metadata lives

Two acceptable architectural patterns:

#### Option A — shared envelope
`FilteredResponseEnvelope<T>`

Fields:

- `T Data`
- `FilterResponseMetadataDto Filter`

Best for:

- new endpoints
- endpoints returning collections or complex DTOs where consistency matters most

#### Option B — metadata property on existing DTOs
Add:

- `FilterResponseMetadataDto Filter`

to existing response DTOs.

Best for:

- endpoints already returning named DTO objects
- minimizing transport churn during migration

### 9.3 Recommended architecture

Use **both**, depending on endpoint shape:

- object DTO endpoints may add `Filter` directly
- raw collection endpoints should use an envelope

This avoids redesigning every endpoint immediately while keeping metadata consistent.

### 9.4 Collections and bare arrays

Endpoints that currently return bare collections should not remain bare once canonical filter metadata is required.

Recommended pattern:

- wrap collection endpoints in `FilteredResponseEnvelope<IReadOnlyList<T>>`

This is especially relevant for endpoints that today return arrays/lists without room for metadata.

### 9.5 Backend generation point

The API boundary should generate response metadata after:

1. request parsing
2. backend revalidation
3. default resolution
4. final effective request construction

Query handlers and repositories should not build response filter metadata themselves.

---

## 10. Implementation Boundaries

### 10.1 Foundation design

This includes reusable infrastructure only:

- canonical filter value types and definitions
- `FilterState`
- `FilterService`
- validation and resolution engines
- page contract model and registry
- URL parser/serializer/navigation adapter
- request mappers
- backend filter boundary validation/resolution
- response metadata model

Foundation design must be page-agnostic.

### 10.2 Page migration design

This includes page-specific adoption only:

- replacing page-local fields with `FilterService`
- replacing page-specific URL parsing
- replacing page-specific request building
- wiring pages to their contracts
- moving page-local time/product/team logic into shared services

Page migration work must not redefine canonical filter semantics.

### 10.3 Backend-extension design

This includes endpoint/query changes that can happen later:

- redesigning legacy endpoints to accept `FilterRequestDto`
- pushing more canonical filter support into backend query models
- standardizing envelopes for currently bare collection endpoints
- aligning sprint-driven and read-model portfolio endpoints under one backend filter contract
- clarifying team semantics at the endpoint/query level

### 10.4 Boundary rule

The implementation design deliberately separates:

- reusable infrastructure
- page adoption
- backend contract redesign

That separation is necessary so the next implementation plan can be written without mixing architecture with migration sequencing.

---

## 11. Design Risks

### 11.1 Sprint vs date-range coexistence

Risk:
- some pages think in sprint IDs, others in derived dates, and some endpoints require both

Design implication:
- `TimeFilterValue` must remain canonical while request mappers handle endpoint-specific translation

### 11.2 Team semantics are inconsistent today

Risk:
- on some pages team is a true backend filter
- on others it is only a sprint-list dependency

Design implication:
- team must remain page-level in the canonical model until endpoint semantics are made consistent

### 11.3 Pages with dual filter models

Risk:
- `PortfolioProgressPage` currently combines sprint-driven metrics with embedded portfolio read-model filters

Design implication:
- both surfaces must consume one shared canonical state even if they still map to different endpoints temporarily

### 11.4 Response-envelope consistency

Risk:
- some endpoints return DTOs, others may return collections or legacy shapes

Design implication:
- metadata architecture must support both inline-property and shared-envelope patterns during migration

### 11.5 Legacy URL compatibility can linger too long

Risk:
- accepting old parameter names indefinitely would recreate ambiguity

Design implication:
- compatibility aliases must be parser-only and temporary; migrated pages emit only canonical names

### 11.6 Lookup-dependent validation timing

Risk:
- product/project/team/repository membership may depend on asynchronously loaded lookup data

Design implication:
- client validation must tolerate temporarily incomplete lookup data and revalidate once lookups are available; backend remains authoritative

### 11.7 Pages currently dropping context during navigation

Risk:
- current flows like Backlog Health -> Work Item Explorer lose product context

Design implication:
- navigation adapter must carry canonical global state automatically instead of depending on hand-built query strings

### 11.8 Existing page-local UI-only refinements

Risk:
- some filters are canonical candidates, others are merely local view refinements (for example author highlight/search variants)

Design implication:
- the contract model must explicitly distinguish canonical page filters from purely local UI refinements to prevent scope creep

---

## 12. Summary

The canonical filter implementation design for PoTool is:

- one **shared canonical filter vocabulary** in `PoTool.Shared`
- one **client-owned canonical runtime** in `PoTool.Client`
- one **API boundary validation/resolution layer** in `PoTool.Api`
- optional **backend canonical value model** in `PoTool.Core` where query/application logic needs filter semantics independent of transport

The most important design decisions are:

1. **Selected, Effective, Applicable, and Invalid are separate first-class concepts**
2. **Pages stop owning filter logic** and instead declare a `PageFilterContract`
3. **URL parsing and serialization become centralized services** instead of page helpers and `WorkspaceBase` duplication
4. **Request mapping is isolated in compatibility mappers** so legacy endpoints can coexist during migration
5. **Backend queries must consume only effective filter data**
6. **Response metadata must expose requested vs effective vs invalid filter state** for future UI behavior

This design directly maps the current fragmented system to a concrete target implementation without redefining approved UX and without turning the design into a phased rollout plan.
