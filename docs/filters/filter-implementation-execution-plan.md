# Canonical Filter Implementation Execution Plan

## 1. Overview

This document converts the approved canonical filter design into a strict phased execution plan.

Fixed inputs:

- `docs/filters/filter-implementation-design.md`
- `docs/filters/filter-current-state-analysis.md`
- `docs/filters/filter-canonical-model.md`
- `docs/filters/filter-implementation-plan.md`

This plan is intentionally execution-focused.
It does **not** change architecture, scope, or approved UX behavior.

The rollout must preserve five hard principles throughout execution:

1. **Backend correctness before UI**
2. **No parallel filter systems active in the same execution path**
3. **Each phase must be verifiable independently**
4. **Each phase must be revertible**
5. **No big-bang migration**

### 1.1 Execution guardrails

These guardrails apply across all phases:

- canonical infrastructure may coexist with legacy logic only when one side is **shadow/read-only** and not driving requests
- once a page execution path is switched to canonical filters, legacy logic for that same execution path must no longer participate in request building, URL generation, or validation
- endpoint migrations must happen by endpoint family, not all at once
- page migrations must happen page-by-page or tightly coupled page-flow by page-flow
- feature flags are allowed only as migration safety switches, not as permanent alternate architectures

### 1.2 What this plan optimizes for

This plan prioritizes:

- safe observability before replacement
- backend validation before client behavior changes
- replacement of duplicated logic only after canonical replacements are proven
- localized rollback when a page or endpoint migration regresses

### 1.3 Definition of “usable at all times”

During every phase:

- existing pages must still load
- URLs must still resolve
- supported endpoints must still return usable data
- invalid filter states must not crash the page or silently change meaning
- migrations must not create double-filtering or dual-request ambiguity

---

## 2. Phase Breakdown (0–9)

## Phase 0 — Safety Preparation

### Goal
Make filter behavior observable and safe to change.

### Scope
- request/page-level filter usage visibility
- inventory of filter entry points
- migration safety switches where needed
- baseline verification matrix

### Concrete steps
1. Inventory all current filter entry points identified in the current-state analysis:
   - `WorkspaceBase`
   - Home/Trends query builders
   - page-local filter state on Delivery/Portfolio/PR/Pipeline/Validation/Planning pages
   - client API helpers such as `WorkItemService`, PR API clients, portfolio consumption client
2. Add structured logging at the boundaries where filter meaning can currently change:
   - URL parse/read
   - page request construction
   - API request normalization
   - backend query dispatch for migrated endpoint families
3. Define one canonical log shape for filter tracing:
   - page/endpoint name
   - requested selected values
   - effective values used
   - ignored/not-applicable filters
   - invalid filters and reason codes
   - source of request (legacy page path vs canonical mapper path)
4. Define migration flags only where switching execution paths could be risky:
   - endpoint-family flag if backend canonical enforcement is introduced gradually
   - page-level activation flag when moving a page from legacy request building to canonical request mapping
5. Build a baseline matrix from existing behavior for the first migration candidates:
   - Portfolio read flows
   - Delivery/Trends flows
   - PR flows
   - Pipeline flows
   - Validation flows

### Success criteria
- every targeted request path can be traced from selected inputs to effective request values
- every page family has a documented entry-point inventory
- migration switches exist where rollback would otherwise be coarse-grained
- baseline behavior is captured before execution-path replacement starts

### Rollback strategy
- logging can remain enabled or be reduced independently
- feature flags default all not-yet-migrated execution paths to legacy behavior
- no runtime path is switched in this phase, so rollback is configuration-only

### Risks
- incomplete entry-point inventory leaves hidden filter logic unmigrated later
- inconsistent logging formats reduce comparison value during page migrations
- overuse of flags creates confusion if not scoped narrowly

---

## Phase 1 — Foundation (No Behavior Change)

### Goal
Introduce canonical infrastructure without changing runtime behavior.

### Scope
- canonical core types
- `FilterService` skeleton
- `PageFilterContract` model and registry
- URL parser/serializer layer (unused in execution)
- validation engine (not enforced)
- request mapper infrastructure (not used)

### Concrete steps
1. Add canonical shared types from the approved design:
   - filter keys/enums
   - canonical filter value types
   - time model
   - request/response DTO skeletons
2. Add client state types:
   - `FilterState`
   - `SelectedFilters`
   - `EffectiveFilters`
   - `ApplicableFilters`
   - `InvalidFilterEntry`
3. Add `PageFilterContract` and registry for all known page families, but do not wire pages to use them yet.
4. Add `FilterService` in scoped lifetime with no page replacements yet.
5. Add URL parser/serializer/navigation adapter abstractions but do not replace `WorkspaceBase` or page query builders.
6. Add validation/resolution engine with isolated unit tests only.
7. Add request mapper interfaces and endpoint-family mapper shells but keep existing pages and clients unchanged.

### Success criteria
- solution builds cleanly
- no runtime behavior changes on existing pages
- canonical infrastructure is testable in isolation
- no page request path uses the new infrastructure yet

### Rollback strategy
- revert infrastructure-only additions without touching page behavior
- because runtime paths are unchanged, rollback is low-risk and code-only

### Risks
- accidental wiring into runtime behavior causes hidden regressions
- foundation types drift from approved design if convenience shortcuts are introduced
- contracts become incomplete if not created for all target page families up front

---

## Phase 2 — Backend Enforcement (Already Partially Done)

### Goal
Ensure backend uses canonical filter validation/resolution where introduced.

### Scope
- backend filter context enforcement
- API-side validation/resolution
- response metadata on selected safe endpoint families

### Concrete steps
1. Identify endpoint families already closest to canonical boundary control, starting with portfolio/read-model-related flows that already have stronger backend filter contracts.
2. Introduce API-side canonical filter normalization for selected endpoint families:
   - parse boundary inputs
   - resolve canonical effective filter context
   - reject or explicitly mark invalid combinations
3. Ensure query/application logic receives only validated effective filter data, not raw selected inputs.
4. Add response metadata for selected safe endpoints:
   - `RequestedFilter`
   - `EffectiveFilter`
   - `InvalidFields`
5. Expand endpoint coverage only after each family has targeted validation and log comparison.
6. Preserve existing transport shapes where necessary through adapters rather than endpoint redesign.

### Success criteria
- selected endpoint families are fully canonical at the backend boundary
- no silent defaults are introduced in migrated backend paths
- logs show backend requested vs effective filter values explicitly
- legacy endpoint contracts continue working through adapters where redesign is deferred

### Rollback strategy
- disable canonical enforcement for the affected endpoint family via migration switch if needed
- revert the family-specific API adapter without reverting the shared foundation
- preserve legacy endpoint binding as fallback until the family is reattempted

### Risks
- boundary enforcement exposes hidden client assumptions that pages still rely on
- response metadata shape differs across endpoint families too early
- canonical backend defaults may diverge from current legacy page expectations if not compared carefully

---

## Phase 3 — Client Filter Engine Activation

### Goal
Introduce `FilterService` as a passive source of truth.

### Scope
- initialize `FilterService` on target pages
- build `FilterState` from current page inputs and URL inputs
- keep existing page logic as the execution path

### Concrete steps
1. Initialize `FilterService` on selected pages in shadow mode.
2. Feed current page inputs into canonical `SelectedFilters` without changing the page’s current request-building behavior.
3. Resolve `ApplicableFilters`, `InvalidFilters`, and `EffectiveFilters` in memory only.
4. Log differences between:
   - legacy page-selected/request values
   - canonical shadow `FilterState`
5. Fix mismatches in contracts, validation rules, or mapper assumptions before any page begins executing from canonical state.
6. Do not let shadow `FilterState` drive API requests, URL writes, or page validation behavior yet.

### Success criteria
- canonical `FilterState` mirrors current page behavior closely enough for the first migration set
- discrepancies are observable through logs
- no user-visible regressions occur because legacy logic still executes
- shadow mode proves that page contracts and validation logic are complete enough to support migration

### Rollback strategy
- remove page initialization of `FilterService` for the affected page family
- keep backend and foundation intact
- because shadow mode is read-only, rollback is low-risk and page-local

### Risks
- shadow mode accidentally influences execution path, causing double logic
- canonical state may expose latent semantic mismatches that require design-faithful correction before migration
- pages with heavy local state may be difficult to mirror without missing transient values

---

## Phase 4 — URL Centralization

### Goal
Replace URL parsing/building with the canonical system.

### Scope
- replace `WorkspaceBase` URL parsing/building behavior
- replace page-specific query parsing
- replace query-string builder duplication

### Concrete steps
1. Introduce canonical URL parsing in adapter form while still accepting legacy query formats.
2. Replace legacy parsing sources incrementally:
   - `WorkspaceBase.ParseContextQueryParameters()`
   - page-specific query parsing in Trends, PR, Validation, and Work Item flows
3. Replace query-string builders with canonical serializer/navigation adapter:
   - `WorkspaceBase.BuildContextQuery()`
   - `HomePage.BuildContextQuery()`
   - page-specific URL updaters
4. Start emitting canonical query parameter names for migrated pages while continuing to read legacy aliases.
5. Compare old vs new URLs on migrated pages for externally visible behavior.
6. Keep page request execution unchanged unless the page is already scheduled for Phase 5 migration.

### Success criteria
- URL behavior remains externally compatible for existing deep links
- internal parsing/building duplication is removed from migrated pages
- canonical parameter emission begins on migrated pages
- legacy links continue to hydrate canonical selected state correctly

### Rollback strategy
- switch the affected page back to legacy parser/serializer path
- keep canonical parser available but not active for that page
- preserve read support for canonical URLs even if emission is temporarily reverted

### Risks
- URL alias handling becomes inconsistent across pages
- legacy deep links break if parsing coverage is incomplete
- navigation context loss appears in flows that previously hand-carried `productId` and `teamId`

---

## Phase 5 — Page Migration (Controlled)

### Goal
Migrate pages one by one to canonical filter usage.

### Scope
Migration order mandated for execution:
1. Portfolio (already partially done)
2. Delivery / Trends
3. PR pages
4. Pipeline pages
5. Validation / WorkItem pages

### Concrete steps for every page
1. remove local filter state ownership
2. bind UI controls to `FilterService.SelectedFilters`
3. build requests only from `EffectiveFilters`
4. remove local validation logic
5. plug the page into endpoint-family request mappers
6. verify logging shows canonical request source
7. delete only the page-local logic replaced in that page

### Page family details

#### 5.1 Portfolio
**Why first:** backend canonicalization is already partially aligned here and the design explicitly identifies portfolio as partially done.

**Scope:**
- `PortfolioProgressPage`
- `PortfolioDelivery`
- `PortfolioCdcReadOnlyPanel`
- supporting portfolio endpoint mappers

**Execution steps:**
1. make portfolio pages derive both sprint-driven and read-model calls from the same canonical `FilterState`
2. remove page-owned sprint-range logic where canonical time support replaces it
3. ensure CDC panel filters and sprint-driven metrics no longer represent separate filter systems in the same page execution path
4. validate backend requested/effective metadata for portfolio endpoints

**Success criteria:**
- no dual portfolio filter systems remain active in the same execution path
- portfolio requests come from canonical mappers only
- logs show one selected/effective state feeding all portfolio calls on the page

**Rollback strategy:**
- revert the portfolio page family only
- restore legacy page-local request composition while keeping shared infrastructure intact

**Risks:**
- portfolio page currently spans the highest filter-model complexity
- read-model and sprint-driven endpoints may diverge semantically under one state model if not compared carefully

#### 5.2 Delivery / Trends
**Scope:**
- `DeliveryTrends`
- `SprintExecution`
- `PortfolioDelivery` if not fully completed with portfolio family
- `TrendsWorkspace`

**Execution steps:**
1. replace page-local team/sprint/time state with canonical selected state
2. replace sprint-range expansion and time defaulting with shared services
3. route requests through delivery/trends request mappers
4. remove page-owned time conversions and context query builders

**Success criteria:**
- no page-local sprint-range expansion remains on migrated pages
- same visible dataset unless an intentional semantic correction is explicitly documented
- canonical logs confirm effective values drive requests

**Rollback strategy:**
- revert the affected delivery/trends page independently
- keep shared time resolver services for already-migrated pages only

**Risks:**
- Delivery Trends currently applies product client-side after the response
- trends pages currently mix URL-carried context and page-owned time logic

#### 5.3 PR pages
**Scope:**
- `PrOverview`
- `PrDeliveryInsights`

**Execution steps:**
1. replace page-local team/sprint/date/repository state with canonical state
2. move sprint-to-date translation fully into request mappers
3. keep endpoint-specific transport differences isolated behind PR mapper family
4. remove page-owned URL parsing and request composition

**Success criteria:**
- pages no longer translate sprint-to-date themselves
- `PrOverview` and `PrDeliveryInsights` both derive requests from canonical time input
- repository and team behavior are explicit in logs and no longer implicit in page code

**Rollback strategy:**
- revert PR page family only
- restore legacy page mapping while keeping backend and shared services intact

**Risks:**
- one PR endpoint uses dates only while another uses sprint plus dates
- page-local author/highlight refinements must not accidentally be promoted into canonical execution logic

#### 5.4 Pipeline pages
**Scope:**
- `PipelineInsights`
- any dependent build-quality detail invocation path affected by canonical filter usage

**Execution steps:**
1. move team-to-sprint selection dependency into shared support services
2. move toggles and canonical time usage into canonical state/request mapping
3. keep endpoint-specific repository/build-quality detail mapping isolated
4. remove page-owned sprint defaulting and request composition

**Success criteria:**
- team no longer acts as hidden page-owned request scaffolding
- pipeline requests originate from canonical effective state
- build-quality detail integration continues working with no implicit filter loss

**Rollback strategy:**
- revert pipeline page family only
- restore legacy page-local sprint scaffolding for this family

**Risks:**
- team is not a true backend filter in current pipeline flows
- repository support is split between page behavior and build-quality detail endpoints

#### 5.5 Validation / WorkItem pages
**Scope:**
- `ValidationTriagePage`
- `ValidationQueuePage`
- `ValidationFixPage`
- `WorkItemExplorer`
- related backlog-to-validation navigation paths where product context is carried

**Execution steps:**
1. replace duplicated product-to-`productIds` construction with canonical validation mapper
2. unify validation category/rule parsing through canonical URL parsing
3. move product context carry-forward into canonical navigation adapter
4. remove page-local request parameter composition and local validation parsing duplication

**Success criteria:**
- no page-local `productIds` reconstruction remains on migrated validation flows
- category/rule/product selected vs effective values are explicit
- backlog/work-item navigation no longer drops product context where canonical navigation applies

**Rollback strategy:**
- revert the affected validation/work-item page family only
- restore legacy page-local request building and parsing

**Risks:**
- validation flows currently use multiple parameter naming conventions
- Work Item Explorer has mixed canonical-candidate filters and UI-only local state

### Success criteria for Phase 5 overall
- each migrated page has no local filter execution logic remaining
- page behavior is unchanged unless an intentional semantic correction is explicitly documented and validated
- logs confirm canonical selected/effective/request flow per migrated page
- rollback remains localized to the page family currently being migrated

### Rollback strategy for Phase 5 overall
- rollback at the smallest migrated page-family boundary
- do not revert unrelated families already proven stable
- retain shared infrastructure while reverting page execution-path activation only

### Risks
- hidden local defaults may be deleted before equivalent canonical defaults are active
- page-local UI concerns may be confused with canonical execution filters
- transport differences across endpoints may leak back into page code if mapper coverage is incomplete

---

## Phase 6 — Remove Legacy Logic

### Goal
Delete all duplicated filter logic once canonical execution paths are stable.

### Scope
- `WorkspaceBase` helpers
- page-specific query parsing
- page-owned sprint/date conversions
- page-owned `productIds` reconstruction
- obsolete compatibility branches that are no longer read

### Concrete steps
1. Remove legacy helpers replaced by canonical URL/navigation layer.
2. Remove page-local conversion utilities already superseded by shared services.
3. Remove request-building logic already replaced by endpoint-family mappers.
4. Remove unused compatibility aliases only after the final dependent page is migrated.
5. Remove flags that protected only already-stable legacy paths.

### Success criteria
- only the canonical filter system remains in active execution paths
- duplicated parsing/building/conversion logic is deleted
- no page request path relies on pre-canonical helpers

### Rollback strategy
- revert specific deletion commits if a hidden dependency is discovered
- if necessary, temporarily restore a compatibility alias or adapter without reviving full page-local logic

### Risks
- removing aliases too early breaks long-lived deep links
- hidden page flows may still reference deleted helpers
- incomplete log coverage may mask remaining legacy execution paths

---

## Phase 7 — Response Contract Expansion

### Goal
Standardize filter metadata across endpoints.

### Scope
- shared response envelope or inline metadata adoption
- consistent requested/effective/invalid metadata exposure across endpoint families

### Concrete steps
1. Apply the approved metadata architecture to remaining endpoint families.
2. For object DTO endpoints, add inline `Filter` metadata where appropriate.
3. For collection-returning endpoints, wrap responses in the agreed envelope where metadata cannot otherwise be attached.
4. Keep transport changes family-scoped and verifiable.
5. Update API/client tests to verify metadata consistency per family.

### Success criteria
- all targeted endpoints expose requested/effective/invalid filter metadata consistently
- metadata generation happens at the API boundary, not in downstream query logic
- clients can rely on one consistent metadata interpretation

### Rollback strategy
- revert metadata expansion for the affected endpoint family while keeping already standardized families intact
- preserve core backend enforcement even if response-shape standardization is temporarily backed out

### Risks
- response-shape changes may have wider client impact than filter logic itself
- collection endpoints may require broader contract changes than object DTO endpoints
- inconsistent family-by-family metadata naming would create long-term drift

---

## Phase 8 — Team Semantics (Optional / Controlled)

### Goal
Introduce team as a real filter only where it is genuinely meaningful.

### Scope
- selected pages only
- no forced cross-application rollout

### Concrete steps
1. Identify pages where team should remain a page-local execution dependency versus pages where it should become a true backend filter.
2. Extend only those endpoint families that can correctly and explicitly honor team as an effective filter.
3. Update page contracts for those pages only.
4. Validate selected vs effective behavior where team is applicable versus ignored.

### Success criteria
- team semantics are explicit on participating pages
- team is not forced into pages where current backend semantics do not support it
- no page regresses into “team as hidden scaffolding” after semantic expansion

### Rollback strategy
- revert team-semantic activation per page or endpoint family
- preserve baseline canonical model where team remains page-level and optional

### Risks
- over-expanding team semantics would reintroduce scope creep
- partial backend support could create false expectations in UI behavior
- team/product membership validation may require stronger lookup guarantees

---

## Phase 9 — UX Refinement

### Goal
Implement the already-approved filter UX behavior on top of stable logic.

### Scope
- invalid filter highlighting
- advanced filter panel behavior
- disabled/not-applicable section
- time selector UX

### Concrete steps
1. Bind visual invalid-state treatment to canonical `InvalidFilters`.
2. Bind disabled-context rendering to canonical `ApplicableFilters` and remembered selected values.
3. Implement approved advanced time mode behavior using canonical time state.
4. Ensure summary rendering uses selected values while execution continues to use effective values.
5. Keep this phase presentation-only; do not change filter semantics.

### Success criteria
- UI behavior matches the approved filter UI documents
- no new filter logic is introduced in components
- selected/effective/applicable/invalid distinctions are visible without changing execution meaning

### Rollback strategy
- revert UI refinement changes only
- preserve canonical runtime and migrated execution paths

### Risks
- UI components may accidentally reintroduce local logic if they are allowed to infer execution state
- invalid vs ignored presentation could confuse users if backend metadata and client state drift
- timing of asynchronous lookup refresh could affect visual validity state if not handled carefully

---

## 3. Migration Order

The migration order is fixed and must remain:

1. **Portfolio**
2. **Delivery / Trends**
3. **PR pages**
4. **Pipeline pages**
5. **Validation / WorkItem pages**

### 3.1 Why this order is required

#### Portfolio first
- already partially aligned with backend enforcement
- highest complexity and highest duplication payoff
- resolves the explicit “parallel portfolio filter systems” risk early

#### Delivery / Trends second
- large duplication surface for sprint-range and time logic
- strong dependency on shared time services that later PR/Pipeline migrations also need

#### PR third
- depends on canonical time services and URL centralization
- contains endpoint-specific time translation differences best handled after delivery time infrastructure stabilizes

#### Pipeline fourth
- relies on the same team-to-sprint support but has weaker current team semantics than PR flows

#### Validation / WorkItem fifth
- easiest to isolate functionally, but depends on canonical navigation carry-forward and final URL/mapper consistency

### 3.2 Migration sequencing inside each family

For each family:

1. shadow-mode `FilterService` first if not already activated
2. canonical URL integration second if not already centralized
3. one execution path switched to canonical requests
4. logging comparison and verification completed
5. legacy page-local logic for that family removed only after stabilization

---

## 4. Page Migration Checklist

Use this checklist for **every** page or tightly coupled page flow.

- [ ] Page contract exists and matches approved design
- [ ] All page filter entry points are inventoried
- [ ] Shadow `FilterState` matches current selected/effective behavior
- [ ] Canonical URL parsing handles both current and canonical formats as needed
- [ ] Canonical URL serializer is ready for this page
- [ ] Request mapper exists for every endpoint the page calls
- [ ] Page binds UI to `SelectedFilters`
- [ ] Page reads `ApplicableFilters` for visibility/disabled behavior
- [ ] Page reads `InvalidFilters` for invalid-state display
- [ ] Page builds requests only from `EffectiveFilters`
- [ ] Legacy page-local validation logic is removed
- [ ] Legacy page-local request composition is removed
- [ ] Logging confirms canonical execution path
- [ ] Targeted tests cover mapper/validation/URL behavior for the page family
- [ ] Rollback switch or localized revert path is clear before activation

### 4.1 Hard migration rule

A page is only considered migrated when **all** of the following are true:

- `FilterService` is the source of selected state
- `EffectiveFilters` are the only request source
- page-local filter parsing/validation/request building is removed from the active execution path
- logs identify the page as canonical, not legacy

---

## 5. Verification Strategy

### 5.1 Cross-phase verification rules

Every phase must be independently verifiable through:

- build success
- targeted tests for affected infrastructure/page family/endpoint family
- log comparison before and after activation
- page-level behavior comparison against the baseline matrix

### 5.2 Verification checklist

For every migrated endpoint or page, verify:

- requested filter values match what the user selected or URL restored
- effective filter values match what the page/backend actually uses
- not-applicable filters remain remembered but do not affect execution
- invalid filters remain visible but do not affect execution
- URL parsing restores the expected selected state
- URL serialization emits the canonical format for migrated pages
- API requests match the endpoint-family mapper output
- no silent fallback is introduced
- no double filtering occurs in page code plus backend code
- no product/team/time context is dropped during navigation

### 5.3 How to avoid double filtering

The following rules are mandatory:

- when shadow mode is active, canonical state may observe but may not drive requests
- once canonical execution is active for a page, legacy page-local filters must stop participating in request construction
- backend canonical enforcement must consume normalized effective input only once
- pages must not apply post-response filtering that duplicates an effective backend filter unless the legacy endpoint cannot yet support that filter and the compatibility path explicitly documents the temporary behavior

### 5.4 Regression detection approach

Use three-way comparison where applicable:

1. legacy selected/request behavior
2. canonical shadow/effective behavior
3. backend requested/effective metadata

A migration may advance only when these comparisons are understood and any intentional differences are documented.

### 5.5 Suggested validation focus by phase

- **Phase 1:** unit tests for state, contracts, validation, URL, and mapper infrastructure
- **Phase 2:** targeted backend unit/integration tests for canonical enforcement and response metadata
- **Phase 3:** page-level logging verification in shadow mode, plus no-regression page load checks
- **Phase 4:** URL round-trip tests and legacy-link compatibility tests
- **Phase 5:** page-family targeted tests plus request/log comparison
- **Phase 6:** dead-code removal validation and no-reference verification
- **Phase 7:** response metadata contract tests across endpoint families
- **Phase 8:** team-semantic endpoint/page tests where activated
- **Phase 9:** UI tests/audits for approved invalid/disabled/advanced behavior

---

## 6. Logging Strategy

### 6.1 Required visibility during migration

For each canonicalized request path, logs must show:

- page or endpoint family name
- migration mode (`legacy`, `shadow`, `canonical`)
- selected filters
- effective filters
- invalid filters with reason codes
- ignored/not-applicable filters
- URL source values if request came from navigation restore
- mapper output or normalized backend filter context

### 6.2 Minimum logging points

#### Client-side
- page initialization
- URL parse result
- filter state recomputation
- request mapper output before transport call

#### API-side
- request boundary parse
- canonical validation/resolution result
- query dispatch filter context
- response metadata generation

### 6.3 Logging use per phase

- **Phase 0:** establish baseline and log schema
- **Phase 2:** prove backend enforcement is explicit
- **Phase 3:** compare shadow canonical state against legacy request behavior
- **Phase 4:** trace URL read/write compatibility
- **Phase 5:** confirm canonical execution per page family
- **Phase 6:** confirm no legacy execution-path logs remain for migrated families

### 6.4 Logging quality rule

Logging must be precise enough to answer, for any migrated request:

- what the user selected
- what the page considered applicable
- what was invalid or ignored
- what the backend actually used
- whether the request traveled through legacy or canonical mapping

---

## 7. Risk Management

### 7.1 Top 5 execution risks

#### Risk 1 — Hidden filter semantics
Current pages sometimes display one filter meaning but execute another.

**Mitigation:**
- Phase 0 logging inventory
- Phase 3 shadow mode
- Phase 5 per-family request comparison before legacy deletion

#### Risk 2 — Double filtering / dual logic conflicts
Legacy and canonical logic could both affect the same request path.

**Mitigation:**
- strict shadow-only rule before activation
- one execution-path owner per page after activation
- explicit log field for request source mode

#### Risk 3 — URL compatibility regressions
Legacy deep links may break if canonical parsing/emission is incomplete.

**Mitigation:**
- Phase 4 parser accepts legacy aliases first
- emit canonical names only on migrated pages
- keep alias support until final dependent page is migrated

#### Risk 4 — Portfolio migration instability
Portfolio currently mixes sprint-driven and read-model filters in one surface.

**Mitigation:**
- migrate portfolio first
- require one canonical state feeding all portfolio requests
- keep endpoint adapters separate while sharing state

#### Risk 5 — Time semantics divergence
Sprint, sprint range, and date range are currently translated differently by different pages.

**Mitigation:**
- delivery/trends migration before PR/pipeline
- centralize time translation in shared services/mappers
- verify requested vs effective time for each family

### 7.2 Additional controlled risks

- team semantics remain intentionally deferred to Phase 8 where needed
- response metadata standardization is delayed to Phase 7 to avoid coupling transport churn to initial execution-path changes
- UI refinement is delayed to Phase 9 so presentation does not mask logic defects

---

## 8. Rollback Strategy

### 8.1 Rollback principles

- rollback at the smallest practical boundary
- never require repository-wide reversion for a single page-family regression
- preserve foundation code unless it is itself defective
- prefer disabling activation over deleting infrastructure during rollback

### 8.2 Rollback levels

#### Level 1 — Configuration rollback
Used when a feature flag or activation switch exists.

Examples:
- disable canonical backend enforcement for one endpoint family
- return a page family from `canonical` to `legacy`

#### Level 2 — Page-family rollback
Used when a migrated page family regresses.

Examples:
- revert PR page migration only
- revert pipeline page migration only

#### Level 3 — Endpoint-family rollback
Used when backend enforcement or response metadata changes regress an endpoint family.

Examples:
- revert metadata envelope change for one endpoint family
- restore legacy adapter for one family while keeping shared backend enforcement elsewhere

#### Level 4 — Shared infrastructure rollback
Used only if a foundation defect affects multiple phases.

Examples:
- revert validation engine change that miscomputes effective filters globally
- revert URL serializer defect that corrupts canonical emission

### 8.3 Rollback readiness requirement

Before activating any new canonical execution path, the implementation must identify:

- the owning page family or endpoint family
- the activation point
- the exact fallback path
- the targeted verification that proves rollback succeeded

### 8.4 What must never happen during rollback

- reviving two active filter systems in one execution path
- leaving canonical logs ambiguous about which path is active
- mixing legacy URL emission with partially canonical request building on the same page without an explicit adapter boundary

---

## 9. Summary

This execution plan converts the approved canonical filter design into a controlled rollout with ten strictly separated phases:

- **Phase 0** prepares observability and safety
- **Phase 1** adds foundation without behavior change
- **Phase 2** introduces backend enforcement where safe
- **Phase 3** activates `FilterService` in shadow mode only
- **Phase 4** centralizes URL parsing and serialization
- **Phase 5** migrates page families in the required order
- **Phase 6** removes legacy duplicated logic
- **Phase 7** standardizes response metadata contracts
- **Phase 8** introduces team semantics only where explicitly justified
- **Phase 9** applies the approved UX behavior on top of stable logic

The critical execution rules are:

1. backend correctness comes before UI refinement
2. shadow parallelism is allowed only when it is read-only and not part of the execution path
3. canonical and legacy logic must never both drive the same request path
4. every phase must have explicit success criteria, risk controls, and rollback boundaries
5. migration proceeds by page family and endpoint family, never by big-bang replacement

If followed strictly, this plan allows PoTool to move from the current fragmented filter system to the canonical system incrementally, safely, and with continuous verification.
