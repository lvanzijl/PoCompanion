# Filter Implementation Plan

## Goal

This document defines the phased implementation plan for migrating PoTool from the current fragmented filter system to the canonical filter model.

This plan is based on:

- `docs/analysis/filter-current-state-analysis.md`
- `docs/analysis/filter-canonical-model.md`

It is intentionally implementation-ready.
It defines the migration order, the required building blocks, the coexistence model, and the specific duplication that must be removed.

---

## 1. Migration strategy

### 1.1 Strategy choice: incremental migration

The migration must be **incremental**, not big bang.

### Why big bang is wrong

A big-bang replacement would require changing all of the following at once:

- Home/Workspace URL propagation
- page-owned filter state on Delivery, Trends, Pipeline, PR, Validation, Backlog, and Planning pages
- multiple API query mapping styles
- portfolio pages that currently run two parallel filter systems

The current-state analysis shows that filter semantics are already inconsistent across pages:

- Product is sometimes a real backend filter and sometimes only a client-side refinement
- Team is sometimes a backend filter and sometimes only a sprint-loader
- Time is represented as sprint, sprint range, or date range depending on the page
- multiple pages duplicate query builders and range builders

A big-bang rewrite would combine semantic correction and broad UI replacement in one release, which would make regressions difficult to isolate.

### Why incremental is correct

An incremental migration allows the team to:

1. build the canonical core once
2. run old and new page wiring side-by-side temporarily
3. migrate low-risk pages first
4. validate semantics before moving high-complexity pages
5. remove duplication only after the canonical replacement is proven on real pages

### 1.2 Coexistence strategy

The old and new systems must coexist temporarily.

#### Coexistence rule

During migration, there will be two layers:

1. **Canonical filter infrastructure**
   - owns `SelectedFilters`, `EffectiveFilters`, `ApplicableFilters`, and `InvalidFilters`
   - owns canonical URL serialization/deserialization
   - owns validation and applicability

2. **Compatibility mapping layer**
   - adapts canonical `EffectiveFilters` to existing page APIs and existing generated client calls
   - preserves current endpoint contracts until each endpoint is intentionally redesigned

#### Page migration rule

A page is considered migrated only when:

- it reads filter state from the canonical filter infrastructure
- it declares its filter applicability contract
- it creates backend requests only from `EffectiveFilters`
- it no longer owns duplicated filter parsing, validation, or query-string building

Until then, the page may continue using existing page-local state.

### 1.3 Regression prevention

Regression prevention must be explicit.

#### Guardrails

- do not rewrite all pages in one phase
- do not rewrite all endpoints in one phase
- keep generated API clients intact; adapt above them
- keep old query parameters working during transition
- compare old page behavior and new page behavior page-by-page before deleting compatibility code

#### Required verification per migrated page

For each migrated page, verify:

- same defaults as before unless the plan explicitly changes semantics
- same visible dataset unless the plan explicitly fixes a known semantic mismatch
- same deep-link behavior or better
- no dropped context during navigation
- invalid filters do not break page loading

---

## 2. Core building blocks

These are the first implementation units and must be built before page migration starts.

### 2.1 FilterState model

**Responsibility**

Represents canonical client-side filter state:

- `SelectedFilters`
- `EffectiveFilters`
- `ApplicableFilters`
- `InvalidFilters`

It is the single source of truth for page filtering.

**Location**

- `PoTool.Client/Filters/State/`

**Dependencies**

- canonical filter value types from shared contracts
- page applicability contracts
- validation engine output

**Why first**

The canonical model cannot exist without a stable state shape. Every later phase depends on this object.

### 2.2 Filter definitions

**Responsibility**

Defines canonical metadata for each filter:

- key
- category
- strong type
- persistence policy
- dependency rules
- serialization rules

Required initial definitions:

- Product
- Project
- Time
- Team
- Repository
- ValidationType
- Pipeline
- PR Status

**Location**

- shared filter contracts: `PoTool.Shared/Filters/Definitions/`
- client registration/registry: `PoTool.Client/Filters/Definitions/`

**Dependencies**

- canonical value objects
- canonical enums
- serialization model

**Why first**

The validation engine, URL serializer, and mapping layer must operate on stable filter definitions rather than page-specific logic.

### 2.3 Validation engine

**Responsibility**

Computes:

- invalid relationships
- invalid value shapes
- not-applicable filters
- effective filter output

Initial validation rules must cover:

- Product ↔ Project
- Team belongs to Product
- Repository belongs to Product
- Time applicability and time mode compatibility

**Location**

- client runtime validation: `PoTool.Client/Filters/Validation/`
- backend request validation/adapters: `PoTool.Api/Filters/Validation/`

**Dependencies**

- filter definitions
- page applicability contracts
- lookup services for product/team/repository membership where needed

**Why first**

The current system has no consistent selected/effective split. The validation engine is the mechanism that creates that split.

### 2.4 Applicability model

**Responsibility**

Defines which filters each page supports and which time modes each page accepts.

Each page contract must declare:

- supported filters
- unsupported filters
- required filters
- supported time modes
- page defaults where needed

**Location**

- `PoTool.Client/Filters/PageContracts/`

**Dependencies**

- filter definitions
- canonical filter keys

**Why first**

The canonical model depends on the distinction between ignored and invalid filters. That requires an explicit page applicability contract.

### 2.5 Mapping to backend DTO

**Responsibility**

Converts canonical `EffectiveFilters` into backend-facing request shapes without changing page logic or endpoint contracts.

This mapping must:

- create canonical `FilterRequestDto` where supported
- adapt canonical filters to existing query parameters where endpoints are unchanged
- prevent `SelectedFilters` from leaking into requests

**Location**

- shared DTO types: `PoTool.Shared/Filters/Contracts/`
- client request mappers: `PoTool.Client/Filters/Mappers/`
- API request adapters: `PoTool.Api/Filters/Mappers/`

**Dependencies**

- `FilterState`
- validation engine
- generated API clients

**Why first**

The migration must adapt the current APIs rather than forcing simultaneous endpoint rewrites.

---

## 3. Infrastructure layer

### 3.1 Central service

The system must introduce a single client-side orchestration service named `FilterService` or equivalent.

### 3.2 Responsibility of FilterService

The service must:

- own the current `FilterState`
- accept filter updates from UI controls
- run validation when selections change
- compute `EffectiveFilters`
- expose page-scoped views of the state
- coordinate URL synchronization
- notify pages when effective state changes

### 3.3 Lifetime

The service must be **scoped** in Blazor.

### Why scoped is correct

- it survives page navigation within the user session
- it fits client navigation behavior
- it avoids static/global singleton state leaking across tests or users
- it can still synchronize with the URL for deep linking

A singleton is the wrong lifetime because it makes testing harder and increases accidental global coupling.

### 3.4 Blazor interaction model

Blazor pages and components must interact with the service through a narrow API.

#### Update flow

1. UI control changes a canonical filter value
2. page calls `FilterService.SetSelectedFilter(...)`
3. service updates `SelectedFilters`
4. service applies page contract
5. service runs validation
6. service publishes new `FilterState`
7. page reacts to the new `EffectiveFilters`
8. page reloads data only from `EffectiveFilters`

#### Read flow

Pages must read:

- `SelectedFilters` for rendering controls
- `InvalidFilters` for messages and badges
- `ApplicableFilters` for showing, hiding, or disabling controls
- `EffectiveFilters` for building requests

### 3.5 Subscription model

Pages should subscribe to state changes through an observable event pattern or state-changed callback exposed by the service.

Required behavior:

- unsubscribe on disposal
- reload data only when relevant effective filters changed
- do not trigger reloads on pure UI-only state changes that do not affect `EffectiveFilters`

### 3.6 UI component boundary

Reusable filter controls must become thin components.

Their responsibilities:

- render inputs
- read current selected value
- call `FilterService` on changes
- display invalid/ignored state metadata

They must not:

- build query strings
- call APIs directly
- contain page-specific validation logic

---

## 4. URL and navigation integration

The current-state analysis identifies `WorkspaceBase` and multiple `BuildContextQuery()` implementations as duplicated navigation infrastructure. These must be replaced by one canonical URL integration layer.

### 4.1 Canonical URL strategy

The URL must represent the shareable filter state.

Canonical principles:

- global filters always use canonical parameter names
- conditional global filters use canonical time parameter names
- page filters appear only if the page contract marks them as deep-linkable
- unsupported legacy aliases remain readable during migration but are not emitted once a page is migrated

### 4.2 Canonical URL format

Global filters:

- `productId=<int>`
- `projectKey=<string>`

Time filters:

- single sprint: `timeMode=sprint&sprintId=<int>`
- sprint range: `timeMode=sprintRange&fromSprintId=<int>&toSprintId=<int>`
- date range: `timeMode=dateRange&fromUtc=<iso8601>&toUtc=<iso8601>`

Page filters:

- `teamId=<int>`
- `repositoryIds=<repeated or canonical list form>`
- `validationTypes=<repeated or canonical list form>`
- `pipelineIds=<repeated or canonical list form>`
- `prStatuses=<repeated or canonical list form>`

### 4.3 Multi-select URL rule

The URL layer must define one canonical multi-select format and use it everywhere.

Recommended format:

- repeated query keys
  - `repositoryIds=1&repositoryIds=2`
  - `pipelineIds=10&pipelineIds=11`

Why this format is correct:

- it avoids custom comma-separated parsing
- it matches standard ASP.NET query binding behavior
- it reduces malformed-string handling

### 4.4 URL restore flow

On page activation:

1. page resolves its page contract
2. canonical URL parser reads known query parameters
3. parser produces `SelectedFilters`
4. `FilterService` applies applicability and validation
5. page receives computed `FilterState`
6. page loads data from `EffectiveFilters`

### 4.5 Replacement for WorkspaceBase and query builders

#### Replace `WorkspaceBase`

`WorkspaceBase` must be retired in favor of a canonical URL-state adapter used by all pages.

#### Replace `BuildContextQuery()` duplication

All manual query-string builders must be replaced by a single serializer service.

Proposed client location:

- `PoTool.Client/Filters/Url/`

Required types:

- `FilterUrlParser`
- `FilterUrlSerializer`
- `FilterNavigationAdapter`

### 4.6 Backward compatibility rule

During migration, the parser must accept current legacy parameters where necessary, including existing page-specific parameter names.

However:

- migrated pages must emit only canonical names
- compatibility aliases must be deleted after the last page depending on them is migrated

---

## 5. Backend integration

### 5.1 Where FilterRequestDto is created

`FilterRequestDto` must be created in the client mapping layer, after validation and after applicability are resolved.

Creation rule:

- source = `EffectiveFilters`
- never source from `SelectedFilters`

### 5.2 Where existing API calls are adapted

Existing API calls must be adapted in the client-side request mappers and API helper layer.

Do **not** rewrite all endpoints first.

#### Adaptation strategy

- canonical page logic produces `EffectiveFilters`
- request mapper converts `EffectiveFilters` into the current endpoint’s parameter model
- generated API clients remain the transport layer

### 5.3 Required mapping modes

The mapper layer must support three transition modes:

1. **Canonical-to-legacy query mapping**
   - for existing endpoints such as validation, PR, pipeline, and metrics endpoints
2. **Canonical-to-canonical DTO mapping**
   - for any endpoint upgraded later to accept `FilterRequestDto`
3. **Legacy-read compatibility mapping**
   - temporary parsing of old URL/page state into canonical `SelectedFilters`

### 5.4 Endpoint adaptation rules

#### Validation endpoints

Current issue from the analysis:
- validation pages rebuild `productIds` independently and `WorkItemService` serializes comma-separated values

Migration rule:
- canonical Product selection must map once to validation request parameters in a single validation-request mapper
- comma-separated `productIds` must be isolated behind the compatibility mapper until the endpoint is upgraded

#### PR endpoints

Current issue from the analysis:
- PR Overview turns sprint into date range only
- PR Delivery sends both sprint and dates

Migration rule:
- page uses canonical Time filter
- request mapper translates Time into the exact request shape each PR endpoint currently needs
- page does not perform sprint-to-date mapping itself

#### Pipeline endpoints

Current issue from the analysis:
- team is a sprint-loader, not a final backend filter

Migration rule:
- page declares Team as page filter and Time as required
- mapper sends only the endpoint-supported fields
- the team-to-sprint dependency is handled before request mapping, not inside the page

#### Portfolio endpoints

Current issue from the analysis:
- sprint-driven portfolio pages and CDC read-model pages use parallel filter systems

Migration rule:
- introduce one canonical portfolio mapping layer
- sprint-driven and read-model endpoints may stay separate temporarily
- both must consume the same canonical `FilterState`

---

## 6. Page migration plan

Migration order must follow risk and semantic complexity.

### 6.1 Group 1 — low-risk pages

These pages are read-only or use simple filter models.

#### Target pages

- Backlog Health
- Plan Board
- Validation Triage
- Validation Queue
- Validation Fix
- Product Roadmap Editor search flow

#### Required changes

- replace page-local Product resolution with canonical Product handling
- replace manual query-string propagation with canonical URL serializer
- replace repeated validation product-to-`productIds` construction with one request mapper
- make page contracts explicit

#### Duplication removed

- repeated product-to-`productIds` mapping
- repeated BuildContextQuery-style navigation propagation
- repeated category/rule query handling patterns where possible

#### Risks

- deep-link compatibility for validation flow
- accidental change in default “all products vs selected product” behavior
- product context still being dropped in backlog → explorer navigation if explorer is not migrated in the same slice

### 6.2 Group 2 — medium-complexity pages

These pages already expose multiple filters but do not have the most complex cross-page semantics.

#### Target pages

- Pipeline Insights
- PR Overview
- PR Delivery Insights
- Work Item Explorer
- Bugs Triage
- Health Overview

#### Required changes

- move team→sprint defaulting into canonical services
- move sprint-to-date derivation into canonical time mappers
- convert repository and validation filters into canonical page filters
- distinguish ignored vs invalid filter states in UI
- align URL persistence behavior across PR and pipeline pages

#### Duplication removed

- duplicate team→sprints→current/most-recent sprint selection
- duplicate sprint-to-date conversion on PR pages
- page-local filter URL parsing on PR pages

#### Risks

- visible behavior may appear unchanged while effective backend requests change
- current repository semantics differ between PR and build-quality APIs
- Work Item Explorer has unsupported flags such as `AllTeams`; this must not be reintroduced as fake canonical behavior

### 6.3 Group 3 — high-complexity pages

These pages have the largest semantic mismatch between visible filters and effective backend queries.

#### Target pages

- Trends Workspace
- Delivery Trends
- Sprint Execution
- Portfolio Delivery
- Portfolio Progress
- Portfolio CDC read-only panel
- Home workspace routing surfaces that launch these pages

#### Required changes

- replace sprint-range builders with canonical Time handling
- unify product/team/time semantics across trends and delivery pages
- move portfolio pages to one canonical filter pipeline even if endpoints stay different temporarily
- eliminate side-by-side portfolio filter systems on Portfolio Progress
- move home/trends workspace navigation to canonical URL serialization

#### Duplication removed

- multiple sprint-range expansion implementations
- multiple product/team context builders
- parallel portfolio filter systems on the same page

#### Risks

- Delivery Trends currently shows Product as a visible refinement that is not sent to the backend; migration may intentionally change effective behavior
- team currently means “backend scope” on some pages and “sprint loader only” on others
- portfolio pages currently mix sprint-driven metrics and read-model filtering; alignment must be staged carefully

### 6.4 Recommended migration order inside groups

1. Validation pages
2. Backlog Health and Plan Board
3. Pipeline Insights
4. PR Overview and PR Delivery Insights together
5. Work Item Explorer and Bugs Triage
6. Sprint Execution
7. Trends Workspace
8. Delivery Trends
9. Portfolio Delivery
10. Portfolio Progress and CDC panel together
11. Home workspace navigation cleanup and final legacy removal

This order minimizes semantic risk and ensures the highest-duplication helpers are introduced before the hardest pages migrate.

---

## 7. Duplication removal plan

The current-state analysis identifies four major duplication clusters. Each must be mapped to one canonical replacement.

### 7.1 Sprint range builders

**Current duplication**

Separate sprint range expansion exists across:

- Delivery Trends
- Portfolio Progress
- Portfolio Delivery
- Trends Workspace

**Canonical replacement**

- `TimeRangeResolver`
- `SprintRangeSelectionService`
- page contracts that declare supported time modes

**Removal rule**

No page may compute sprint ID ranges or date windows directly after migration.

### 7.2 Product-to-`productIds` mapping

**Current duplication**

Validation pages each rebuild `productIds`, and the serialization path remains custom.

**Canonical replacement**

- `ValidationRequestFilterMapper`
- shared Product resolution in `FilterService`
- canonical URL parsing for Product

**Removal rule**

No page may manually fetch profile products solely to convert Product into endpoint query parameters.

### 7.3 Team → sprint logic

**Current duplication**

Pipeline Insights, PR Overview, and PR Delivery Insights all implement team-based sprint loading and sprint defaulting.

**Canonical replacement**

- `TeamSprintSelectionService`
- canonical Time filter initialization rules
- page applicability contracts for Team and Time

**Removal rule**

Pages may request available sprint choices from a shared service, but may not own the default-selection algorithm.

### 7.4 Query-string builders

**Current duplication**

Multiple pages build query strings independently.

**Canonical replacement**

- `FilterUrlSerializer`
- `FilterUrlParser`
- `FilterNavigationAdapter`

**Removal rule**

No page may concatenate filter query strings manually once migrated.

### 7.5 Sprint-to-date conversion

**Current duplication**

PR pages convert sprint selections into `fromDate` and `toDate` independently.

**Canonical replacement**

- `TimeFilterMapper`
- endpoint-specific PR request mappers

**Removal rule**

Pages may display the selected sprint but may not compute endpoint date parameters directly.

### 7.6 Parallel portfolio filters

**Current duplication**

Portfolio Progress hosts both a sprint-driven filter model and a separate CDC read-model filter model.

**Canonical replacement**

- one canonical portfolio page contract
- one `PortfolioFilterMapper` family with endpoint-specific adapters underneath

**Removal rule**

A page may call multiple endpoints, but all portfolio calls must be derived from the same canonical `FilterState`.

---

## 8. Validation introduction plan

### 8.1 When validation appears

Validation must be introduced in two steps.

#### Phase A — silent correctness protection

Introduced with the core filter infrastructure.

Behavior:

- invalid filters are excluded from `EffectiveFilters`
- UI may continue using minimal messaging on non-migrated pages
- compatibility mappers ensure invalid values do not reach APIs

#### Phase B — visible invalid/ignored UX

Introduced when medium-complexity pages begin migration.

Behavior:

- invalid filters become visible to the user
- ignored filters are distinguished from invalid filters
- pages show which filters are selected but not used

### 8.2 First phase where invalid filters are visible

The first visible invalid-filter UI should appear when migrating:

- PR Overview
- PR Delivery Insights
- Pipeline Insights

Why here:

- these pages already expose enough filter context for the user to understand ignored vs invalid state
- they strongly benefit from clarifying team/time relationships
- they do not yet carry the portfolio complexity risk

### 8.3 UI rules for invalid vs ignored

#### Invalid

UI must show:

- selected value
- invalid badge or message
- reason text where practical
- exclusion from effective request

#### Ignored

UI must show:

- selected value remains visible in filter summary or advanced area
- message that the current page does not apply it
- no error styling

### 8.4 Fallback behavior

If a filter is invalid:

- keep it in `SelectedFilters`
- exclude it from `EffectiveFilters`
- keep page load working
- preserve URL state unless the page explicitly normalizes on save/navigation

If a filter is not applicable:

- keep it in `SelectedFilters`
- exclude it from `EffectiveFilters`
- show it only as remembered context where appropriate

---

## 9. Rollout strategy

### 9.1 Feature flags

**No user-facing feature flags are required.**

### Why no feature flags

The migration is already incremental because pages can adopt the canonical infrastructure one at a time while old pages continue using existing logic.

That coexistence model is sufficient and simpler than layering runtime flags on top of already duplicated filter flows.

### Exception

If one high-risk portfolio surface requires a temporary rollback path, use an internal page-level opt-in toggle during development only. It must not become permanent product configuration.

### 9.2 Partial rollout model

Roll out by page family, not by infrastructure branch.

#### Sequence

- ship core infrastructure unused
- migrate low-risk pages
- migrate medium-complexity pages
- migrate high-complexity trends and portfolio pages
- remove legacy compatibility code only after all dependent pages are migrated

### 9.3 Testing approach

#### Unit tests

Add focused unit tests for:

- filter definitions
- validation engine
- applicability engine
- URL parser/serializer
- request mappers
- page contracts

Recommended location:

- `PoTool.Tests.Unit/Filters/`

#### Page-level tests

For each migrated page, test:

- default selected filters
- effective request mapping
- URL restore and URL update
- ignored filter behavior
- invalid filter behavior

#### Regression verification

For every migrated page, capture before/after verification for:

- initial page load
- navigation into page with shared context
- navigation away and back
- deep-link open in new tab
- invalid or stale URL values

### 9.4 No-regression verification checklist

A page migration is not complete until the team verifies:

- same route still works
- old bookmarks still open during compatibility period
- filters persist exactly as declared by the page contract
- no duplicate filter computation remains on the page
- backend request shape matches the mapper contract

---

## 10. Risks

### 10.1 Hidden filter semantics

The current-state analysis shows that visible page filters are not always the same as effective backend filters.

Examples:

- Delivery Trends shows Product but applies it client-side after the API call
- Team often determines available sprints without being part of the final backend request
- PR pages expose sprint while one endpoint consumes only derived dates

**Risk**
- migration may appear to change behavior even when it is only making semantics explicit

### 10.2 Backend assumptions

Existing endpoints make different assumptions about filter shape and parameter naming.

Examples:

- `productId` vs `productIds`
- `category` vs `validationCategory`
- `repositoryName` vs `repositoryId`
- sprint IDs vs date windows

**Risk**
- canonical mapping may fail if these differences are not isolated in one compatibility layer

### 10.3 Performance

Central validation and URL synchronization will run more often than current page-local logic.

**Risk**
- repeated validation or unnecessary data reloads if pages subscribe too broadly

**Mitigation**
- reload only on relevant effective-filter changes
- cache lookup data needed for validation where appropriate

### 10.4 UX confusion

Making invalid and ignored filters visible is correct, but it introduces new UI states.

**Risk**
- users may interpret ignored filters as errors

**Mitigation**
- differentiate ignored vs invalid visually and textually
- introduce visible invalid/ignored UX on medium-complexity pages before high-complexity pages

### 10.5 Portfolio migration risk

Portfolio pages currently combine sprint-driven metrics with read-model filters.

**Risk**
- partial migration could leave a page with one canonical filter bar but two incompatible effective request pipelines

**Mitigation**
- migrate Portfolio Progress and the CDC panel together in the same high-complexity phase

### 10.6 Navigation compatibility risk

Current deep links use multiple page-specific query formats.

**Risk**
- bookmarks and cross-page navigation could break if canonical serialization replaces them too early

**Mitigation**
- read legacy aliases during migration
- emit only canonical format on migrated pages
- remove aliases only after the last dependent page is migrated

---

## 11. Final state

After migration, the filter system must have the following properties.

### 11.1 Single filter pipeline

All pages use the same pipeline:

1. URL and UI update `SelectedFilters`
2. page contract defines `ApplicableFilters`
3. validation engine computes `InvalidFilters`
4. filter engine computes `EffectiveFilters`
5. request mappers create backend requests from `EffectiveFilters`

### 11.2 No page-level duplication

No page owns its own:

- sprint-range builder
- team-to-sprint defaulting algorithm
- product-to-`productIds` mapper
- filter query-string builder
- sprint-to-date conversion logic

### 11.3 Consistent backend requests

All backend requests are derived from canonical filters through request mappers.

This means:

- pages no longer pass ad-hoc combinations of local state to APIs
- endpoint differences are isolated in mapper classes
- canonical filter semantics remain stable even when endpoint shapes differ

### 11.4 Consistent UX semantics

Users can always distinguish:

- what they selected
- what the page can apply
- what the page is actually using
- what is invalid
- what is merely ignored on the current page

### 11.5 Clear architectural boundary

The final architecture is:

- `PoTool.Shared`: canonical filter types and shared DTO contracts
- `PoTool.Client`: filter state, validation, applicability, URL integration, page contracts, and request mappers
- `PoTool.Api`: backend validation and endpoint adapter logic where needed

The frontend remains responsible for client-side state and URL behavior.
The backend remains responsible for request validation and endpoint execution.

---

## 12. Phased execution summary

### Phase 1 — Foundation

Build:

- canonical filter types
- `FilterState`
- filter definitions
- validation engine
- applicability contracts
- URL parser/serializer
- request mapper infrastructure

Do not migrate pages yet.

### Phase 2 — Low-risk page adoption

Migrate:

- Validation Triage
- Validation Queue
- Validation Fix
- Backlog Health
- Plan Board
- Product Roadmap Editor

Remove:

- duplicated product-to-`productIds` logic
- duplicated simple query builders

### Phase 3 — Medium-complexity adoption

Migrate:

- Pipeline Insights
- PR Overview
- PR Delivery Insights
- Work Item Explorer
- Bugs Triage
- Health Overview

Introduce:

- visible invalid vs ignored state
- shared team→sprint and sprint→date mapping services

### Phase 4 — High-complexity adoption

Migrate:

- Sprint Execution
- Trends Workspace
- Delivery Trends
- Portfolio Delivery
- Portfolio Progress
- Portfolio CDC panel
- Home workspace navigation links

Remove:

- parallel portfolio filter systems
- sprint-range duplication
- remaining page-owned navigation context builders

### Phase 5 — Legacy removal

Delete:

- `WorkspaceBase` filter parsing/building role
- legacy query-string builders
- page-local compatibility adapters
- URL compatibility aliases no longer needed

At the end of Phase 5, the application has one canonical filter system and one consistent request pipeline.
