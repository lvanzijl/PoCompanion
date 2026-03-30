> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Canonical Filter Phases 1–4 PR Breakdown

## 1. Overview

This document converts the approved canonical filter design and execution plan into implementation-ready, PR-sized work packages for **Phase 1 — Foundation**, **Phase 2 — Backend Enforcement**, **Phase 3 — Client Filter Engine Activation**, and **Phase 4 — URL Centralization** only.

Fixed inputs:

- `docs/analysis/filter-implementation-design.md`
- `docs/analysis/filter-implementation-execution-plan.md`
- `docs/analysis/filter-current-state-analysis.md`
- `docs/analysis/filter-canonical-model.md`

This breakdown is intentionally execution-focused.
It does **not** redesign architecture, change scope, or define work beyond Phase 4.

Phase 0 is treated as an already-defined **prerequisite** from the approved execution plan.
This document references Phase 0 only as a dependency and does **not** break Phase 0 into PR slices.

### 1.1 What PR should be opened first

**Open PR 1 first:** **“Add shared and core canonical filter primitives”**.

Reason:

- every later Phase 1–4 PR depends on stable shared filter keys, value types, and backend-aligned primitives
- it is the safest independently reviewable slice
- it can merge and deploy alone with no behavior change

### 1.2 What can be safely deployed before page migration

The following can be safely deployed before any Phase 5 page execution migration, provided each PR keeps execution paths inactive or shadow-only:

- all **Phase 1** PRs
- **Phase 2** backend portfolio enforcement PRs, but only for the selected endpoint family already using canonical backend resolution and metadata
- **Phase 3** shadow-mode activation PRs, if logging is visible and pages still build requests from legacy logic
- **Phase 4** URL parser/serializer core PRs and page switch PRs **only after** legacy alias acceptance and round-trip verification are in place

### 1.3 What cannot be safely deployed alone

The following must **not** be deployed alone:

- a page URL-emission switch without the corresponding parser switch and legacy alias acceptance
- a shadow-mode activation PR without logging needed to compare shadow vs legacy behavior
- a backend enforcement PR that changes effective behavior without matching controller/service/query tests
- any PR that causes both legacy and canonical logic to drive the same request path

### 1.4 Hard execution rules carried into every PR

- backend correctness before UI
- one active execution path per request
- no silent defaulting in canonical backend paths
- no canonical URL emission before parser switch and round-trip verification
- no page advances out of shadow mode with unexplained mismatches

---

## 2. PR Breakdown by Phase

## Phase 1 — Foundation

### PR 1 — Add shared and core canonical filter primitives
- **Phase:** 1
- **Goal:** Establish the shared/core filter vocabulary that every later client and API slice depends on.
- **Scope:** Shared filter keys, value objects, DTO skeletons, and alignment with existing core portfolio filter primitives.
- **Exact likely files / modules affected:**
  - `PoTool.Shared/Filters/Definitions/*` (new)
  - `PoTool.Shared/Filters/Values/*` (new)
  - `PoTool.Shared/Filters/Contracts/*` (new)
  - `PoTool.Shared/Filters/Responses/*` (new)
  - `PoTool.Core/Filters/FilterContext.cs`
  - `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`
  - `PoTool.Tests.Unit/**/` new MSTest files for shared/core filter primitives
- **Exact changes to make:**
  - add canonical shared filter enums/keys/value DTOs required by the approved design
  - add canonical request/response DTO shells without wiring them into live request paths
  - align existing `FilterContext`, `FilterSelection<T>`, and time-selection primitives with the approved terminology where needed without changing live portfolio behavior
  - keep existing portfolio DTOs compatible while creating the shared target model for later phases
- **Dependencies:**
  - Phase 0 logging and inventory completed
- **Tests required:**
  - new unit tests for filter key/value construction and time-mode validity rules
  - targeted tests for `PoTool.Core/Filters/FilterContext.cs`
  - compatibility tests for `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs` where existing portfolio filter metadata remains valid
- **Manual verification required:**
  - solution builds
  - no API/client contract generation breakage
  - existing portfolio metadata types still deserialize/serialize as before
- **Safe to merge alone?** **Yes** — it is infrastructure-only and should not activate any runtime path.
- **Safe to deploy alone?** **Yes** — there should be no behavior change and no page/controller switches.
- **Rollback scope:** revert shared/core filter primitive additions only.

### PR 2 — Add client filter state and page contract model
- **Phase:** 1
- **Goal:** Introduce canonical client-side state types and page contract declarations without wiring pages to use them.
- **Scope:** `FilterState`, selected/effective/applicable/invalid state objects, page filter contracts, and filter registry.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Filters/State/*` (new)
  - `PoTool.Client/Filters/Contracts/*` (new)
  - `PoTool.Client/Filters/Definitions/*` (new)
  - `PoTool.Client/Program.cs`
  - `PoTool.Tests.Unit/**/` new MSTest files for state/contracts/registry
  - reference docs for grounded contract shapes:
    - `PoTool.Client/Pages/Home/WorkspaceBase.cs`
    - `PoTool.Client/Pages/Home/HomePage.razor`
    - `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
    - `PoTool.Client/Pages/Home/PrOverview.razor`
    - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
    - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
    - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
    - `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
- **Exact changes to make:**
  - add `FilterState`, `SelectedFilters`, `EffectiveFilters`, `ApplicableFilters`, and `InvalidFilterEntry`
  - add `PageFilterContract` model and contract registry covering known page families
  - add filter definition registry metadata used later by validation, URL, and mappers
  - register only inert client services needed to construct these models; do not initialize them on pages yet
- **Dependencies:**
  - PR 1
- **Tests required:**
  - unit tests for state construction and immutability/change-token behavior
  - unit tests for page contract applicability and supported time modes
  - DI registration tests if client service registration is added in `Program.cs`
- **Manual verification required:**
  - solution builds
  - no pages resolve or render through the new state yet
  - no DI errors after app startup
- **Safe to merge alone?** **Yes** — no page activation occurs.
- **Safe to deploy alone?** **Yes** — still inactive infrastructure.
- **Rollback scope:** revert client state/contracts/registry additions only.

### PR 3 — Add client validation engine and `FilterService` skeleton
- **Phase:** 1
- **Goal:** Introduce the canonical client runtime shell without letting it own any page behavior yet.
- **Scope:** `FilterService`, validation engine, applicability resolution, and inert subscription model.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Filters/Services/FilterService.cs` (new)
  - `PoTool.Client/Filters/Validation/*` (new)
  - `PoTool.Client/Program.cs`
  - `PoTool.Tests.Unit/**/` new MSTest files for validation and service behavior
- **Exact changes to make:**
  - add `FilterService` with scoped lifetime
  - add validation rules for product/project, team/product, repository/product, and time-mode shape/applicability
  - add inert public API methods required by the design, but do not call them from pages
  - register the service and validation collaborators in client DI
- **Dependencies:**
  - PR 1
  - PR 2
- **Tests required:**
  - unit tests for selected → invalid/effective transformation
  - time-mode validation tests including invalid shape handling
  - DI/service-construction tests
- **Manual verification required:**
  - solution builds
  - app starts with no runtime page behavior changes
  - no page subscribes to `FilterService` yet
- **Safe to merge alone?** **Yes** — if no page wiring is included.
- **Safe to deploy alone?** **Yes** — runtime remains inactive.
- **Rollback scope:** revert validation/service skeleton only.

### PR 4 — Add canonical URL and request-mapper skeletons (inactive)
- **Phase:** 1
- **Goal:** Land the serializer/parser and mapper scaffolding before any execution-path switch.
- **Scope:** URL parser/serializer abstractions, navigation adapter shell, request mapper interfaces, and portfolio/PR/validation mapper placeholders.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Filters/Url/*` (new)
  - `PoTool.Client/Filters/Mappers/*` (new)
  - `PoTool.Shared/Filters/Contracts/*`
  - `PoTool.Client/Services/NavigationContextService.cs`
  - `PoTool.Tests.Unit/Services/NavigationContextServiceTests.cs`
  - `PoTool.Tests.Unit/**/` new parser/serializer/mapper tests
  - audit-only references for future mapper consumers:
    - `PoTool.Client/Services/WorkItemService.cs`
    - `PoTool.Client/ApiClient/ApiClient.PullRequestInsights.cs`
    - `PoTool.Client/ApiClient/ApiClient.PrDeliveryInsights.cs`
- **Exact changes to make:**
  - add canonical URL parse/serialize abstractions without switching live page parsing
  - add canonical multi-select and time-mode serialization tests
  - add request mapper interfaces and endpoint-family mapper shells without changing page request construction
  - extend `NavigationContextService` only where necessary to host shared URL logic without changing current consumers yet
- **Dependencies:**
  - PR 1
  - PR 2
  - PR 3
- **Tests required:**
  - parser/serializer unit tests
  - URL round-trip tests added to `NavigationContextServiceTests`
  - inert mapper tests proving selected/effective inputs can be transformed without page activation
- **Manual verification required:**
  - existing `NavigationContextService` query-string tests still pass
  - no page URL output changes
  - no API helper query behavior changes
- **Safe to merge alone?** **Yes** — if kept inactive.
- **Safe to deploy alone?** **Yes** — no parsing/emission switch yet.
- **Rollback scope:** revert URL/mapper scaffolding only.

## Phase 2 — Backend Enforcement

### PR 5 — Harden portfolio canonical filter resolution and metadata mapping
- **Phase:** 2
- **Goal:** Make the already-existing portfolio canonical backend path fully explicit and testable against the approved model.
- **Scope:** Portfolio filter resolution, validation behavior, DTO mapping, and service registration confidence.
- **Exact likely files / modules affected:**
  - `PoTool.Core/Filters/FilterContext.cs`
  - `PoTool.Api/Services/PortfolioFilterResolutionService.cs`
  - `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs`
  - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
  - `PoTool.Tests.Unit/Services/PortfolioFilterResolutionServiceTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- **Exact changes to make:**
  - tighten `PortfolioFilterResolutionService` to match approved requested/effective/invalid semantics
  - ensure metadata DTO mapping is explicit and consistent
  - make invalid-field behavior and no-silent-default behavior explicit in tests
  - verify DI registration for the validator/resolution service if needed
- **Dependencies:**
  - PR 1
  - Phase 0 logging visibility for migrated backend boundaries
- **Tests required:**
  - `PortfolioFilterResolutionServiceTests`
  - `ServiceCollectionTests`
  - additional validator tests for invalid product/project/work-package/time combinations
- **Manual verification required:**
  - selected portfolio endpoints still return valid payloads
  - logs show requested vs effective filter data for the enforced boundary
- **Safe to merge alone?** **Yes** — limited to the selected backend family and backed by existing tests.
- **Safe to deploy alone?** **Yes** — because portfolio canonical backend enforcement already exists partially and this PR only hardens it.
- **Rollback scope:** revert portfolio resolution/metadata hardening only.

### PR 6 — Standardize portfolio read endpoint metadata and handler usage
- **Phase:** 2
- **Goal:** Ensure the selected portfolio read endpoints consume canonical filter resolution consistently and expose response metadata everywhere in that family.
- **Scope:** Portfolio read endpoints, handlers/services, and controller tests.
- **Exact likely files / modules affected:**
  - `PoTool.Api/Controllers/MetricsController.cs`
  - portfolio query/handler/service files already consuming `PortfolioReadQueryOptions` and `FilterContext`, including:
    - `PoTool.Api/Services/PortfolioReadModelStateService.cs`
    - `PoTool.Api/Services/PortfolioQueryServices*` / related portfolio query services
    - `PoTool.Api/Services/PortfolioReadModelFiltering.cs`
  - `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
  - `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
  - `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
- **Exact changes to make:**
  - ensure each selected portfolio endpoint receives canonical resolution before query execution
  - standardize returned filter metadata across progress/snapshots/comparison/trends/signals responses in the selected family
  - remove any remaining ambiguity in how requested and effective filter values are attached to portfolio responses
- **Dependencies:**
  - PR 5
- **Tests required:**
  - `MetricsControllerPortfolioReadTests`
  - `PortfolioQueryServicesTests`
  - `PortfolioReadModelStateServiceTests`
  - targeted integration-style tests for metadata presence and invalid-field echoing
- **Manual verification required:**
  - portfolio read pages still load
  - portfolio responses include consistent metadata
  - no endpoint in the selected family silently drops invalid fields without metadata
- **Safe to merge alone?** **Yes** — it stays inside the selected portfolio endpoint family.
- **Safe to deploy alone?** **Yes** — but only with targeted endpoint regression verification.
- **Rollback scope:** revert portfolio endpoint family metadata/handler changes only.

## Phase 3 — Client Filter Engine Activation

### PR 7 — Activate shadow-mode `FilterService` for shared workspace context pages
- **Phase:** 3
- **Goal:** Start shadow-state construction on pages that already depend mainly on `productId`/`teamId` context and have low page-local filter complexity.
- **Scope:** Shared context pages and simple descendants of `WorkspaceBase`.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/WorkspaceBase.cs`
  - `PoTool.Client/Pages/Home/HomePage.razor`
  - `PoTool.Client/Pages/Home/HealthWorkspace.razor`
  - `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
  - `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
  - `PoTool.Client/Filters/Services/FilterService.cs`
  - `PoTool.Tests.Unit/**/` new shadow-mode page integration tests where feasible
- **Exact changes to make:**
  - initialize `FilterService` for pages driven primarily by inherited context
  - hydrate canonical selected state from the same product/team inputs those pages already use
  - log shadow selected/effective state while keeping existing page requests untouched
  - do not change current navigation or request-building logic
- **Dependencies:**
  - PR 2
  - PR 3
  - Phase 0 logging schema
- **Tests required:**
  - unit tests for page bootstrap/input hydration into `FilterService`
  - regression tests proving pages still rely on legacy request logic
- **Manual verification required:**
  - Home → Health still works
  - Home → Validation Triage still works
  - shadow logs appear and no visible behavior changes occur
- **Safe to merge alone?** **Yes** — because the execution path remains legacy.
- **Safe to deploy alone?** **Yes** — if logging overhead is acceptable and no requests are switched.
- **Rollback scope:** revert shadow activation for the shared-context page set only.

### PR 8 — Activate shadow-mode `FilterService` for trends and delivery pages
- **Phase:** 3
- **Goal:** Shadow the most important time/team/product pages before URL centralization or page execution migration.
- **Scope:** Trends and delivery pages with sprint/sprint-range/date derivation.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
  - `PoTool.Client/Pages/Home/DeliveryTrends.razor`
  - `PoTool.Client/Pages/Home/SprintExecution.razor`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
  - `PoTool.Client/Services/SprintService.cs`
  - `PoTool.Client/Filters/Services/FilterService.cs`
  - `PoTool.Tests.Unit/**/` new shadow-mode state comparison tests for time resolution
- **Exact changes to make:**
  - feed current team/sprint/sprint-range/product inputs into shadow canonical state
  - compute shadow effective time values without replacing page-owned sprint-range or request logic
  - log shadow vs legacy mismatches for time/team/product interpretation
- **Dependencies:**
  - PR 7
- **Tests required:**
  - unit tests for sprint/sprint-range/date shadow-state creation
  - tests covering applicable time modes per page contract
  - regression tests confirming no page request source changes
- **Manual verification required:**
  - Trends page still loads from current URLs
  - Delivery Trends and Sprint Execution still display the same data
  - Portfolio pages still work with existing request paths
  - shadow logs explain every mismatch
- **Safe to merge alone?** **Yes** — if strictly shadow-only.
- **Safe to deploy alone?** **Yes** — but only after verifying zero unexplained mismatches on the activated page set.
- **Rollback scope:** revert shadow activation for trends/delivery/portfolio pages only.

### PR 9 — Activate shadow-mode `FilterService` for PR, pipeline, validation queue/fix, and work item pages
- **Phase:** 3
- **Goal:** Complete phase-3 shadow coverage for the pages that will need the most careful phase-4 and phase-5 transitions.
- **Scope:** PR, pipeline, validation drill-down, and work item pages.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/PrOverview.razor`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor`
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
  - `PoTool.Client/Services/WorkItemFilteringService.cs`
  - `PoTool.Client/Services/WorkItemService.cs`
  - `PoTool.Client/ApiClient/ApiClient.PullRequestInsights.cs` (audit reference only unless strictly needed for shadow logging)
  - `PoTool.Client/ApiClient/ApiClient.PrDeliveryInsights.cs` (audit reference only unless strictly needed for shadow logging)
- **Exact changes to make:**
  - initialize shadow selected/effective state from page-local date/repository/category/rule inputs and query-backed validation/work-item inputs
  - log canonical shadow output alongside existing legacy page request parameters
  - keep API helpers and page request builders unchanged
- **Dependencies:**
  - PR 7
  - PR 8
- **Tests required:**
  - page bootstrap tests for repository/date/category/rule/query hydration
  - validation/work-item filter state tests
  - no-request-switch regression tests
- **Manual verification required:**
  - PR pages still load and reload from current URLs
  - Validation Queue/Fix still load from drill-down URLs
  - Work Item Explorer still restores its query-driven context
  - zero unexplained shadow-vs-legacy mismatches remain, or all are documented as intentional corrections
- **Safe to merge alone?** **Yes** — but only if no page request logic changes.
- **Safe to deploy alone?** **Conditionally yes** — only after shadow/legacy mismatch review is complete for the activated pages.
- **Rollback scope:** revert shadow activation for PR/pipeline/validation/work-item pages only.

## Phase 4 — URL Centralization

### PR 10 — Add canonical URL parser/serializer core with legacy-alias support
- **Phase:** 4
- **Goal:** Land the reusable browser URL layer before any page path switches.
- **Scope:** Canonical URL parse/serialize core, alias acceptance, and test coverage.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Filters/Url/*`
  - `PoTool.Client/Services/NavigationContextService.cs`
  - `PoTool.Client/Models/NavigationContext.cs`
  - `PoTool.Tests.Unit/Services/NavigationContextServiceTests.cs`
  - new URL parser/serializer MSTest files
- **Exact changes to make:**
  - implement canonical browser URL parsing/serialization logic for product, project, time modes, and page-deep-linkable filters
  - add legacy alias read support needed by current `productId`, `teamId`, `fromSprintId`, `toSprintId`, `category`, `ruleId`, and similar current query names
  - keep all existing pages on their old parsing/emission paths until later PRs switch them
- **Dependencies:**
  - PR 4
  - PR 9 for known page input shapes
- **Tests required:**
  - `NavigationContextServiceTests` updates for round-trip coverage
  - parser tests for legacy aliases and canonical names
  - serializer tests for repeated multi-select keys and canonical time modes
- **Manual verification required:**
  - existing `NavigationContextService` behavior remains intact
  - no page URL changes yet
- **Safe to merge alone?** **Yes** — no page switch occurs.
- **Safe to deploy alone?** **Yes** — parser/serializer core remains unused by live pages.
- **Rollback scope:** revert URL core only.

### PR 11 — Switch shared workspace context parsing and emission to canonical URL handling
- **Phase:** 4
- **Goal:** Replace duplicated `productId`/`teamId` query handling on the lowest-risk shared navigation paths.
- **Scope:** `WorkspaceBase`, `HomePage`, and the workspace pages that only rely on shared context propagation.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/WorkspaceBase.cs`
  - `PoTool.Client/Pages/Home/HomePage.razor`
  - `PoTool.Client/Pages/Home/HealthWorkspace.razor`
  - `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
  - `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
  - `PoTool.Client/Pages/Home/HomeChanges.razor`
  - `PoTool.Client/Services/NavigationContextService.cs`
- **Exact changes to make:**
  - replace `WorkspaceBase.ParseContextQueryParameters()` and `BuildContextQuery()` usage with canonical parser/serializer calls
  - replace `HomePage.BuildContextQuery()` with the same canonical serializer
  - preserve legacy URL readability while beginning canonical emission on the switched pages
- **Dependencies:**
  - PR 10
  - PR 7
- **Tests required:**
  - updated `NavigationContextServiceTests`
  - targeted tests for `WorkspaceBase` parsing/build behavior if feasible
  - regression coverage for pages using `BuildContextQuery()` inheritance
- **Manual verification required:**
  - Home → Health
  - Home → Trends
  - Home → Validation Triage
  - browser back/forward with product/team context
  - canonical URL emission visible only after parser switch
- **Safe to merge alone?** **Yes** — if legacy aliases remain accepted and round-trip tests pass.
- **Safe to deploy alone?** **Conditionally yes** — only with manual navigation verification because this changes live deep-link behavior.
- **Rollback scope:** revert shared workspace URL switch only.

### PR 12 — Switch trends workspace URL parsing and emission
- **Phase:** 4
- **Goal:** Centralize the most duplicated sprint-range browser URL behavior.
- **Scope:** Trends workspace query parsing/emission and downstream navigation carry-forward from that page.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
  - `PoTool.Client/Pages/Home/WorkspaceBase.cs`
  - `PoTool.Client/Services/NavigationContextService.cs`
  - possibly navigation targets linked directly from Trends workspace:
    - `PoTool.Client/Pages/Home/DeliveryTrends.razor`
    - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
    - `PoTool.Client/Pages/Home/PrOverview.razor`
  - tests in `PoTool.Tests.Unit/Services/NavigationContextServiceTests.cs` plus new workspace-specific URL tests
- **Exact changes to make:**
  - replace `ParseSprintQueryParameters()` and `UpdateSprintUrlParameters()` with canonical URL handling
  - preserve current external behavior for `teamId`, `fromSprintId`, and `toSprintId` while enabling canonical emission
  - keep page execution logic unchanged; this PR changes only browser URL ownership
- **Dependencies:**
  - PR 10
  - PR 8
- **Tests required:**
  - round-trip tests for trends URLs
  - alias-acceptance tests for existing sprint-range URLs
  - regression tests proving linked pages still receive expected context
- **Manual verification required:**
  - Trends deep links
  - Trends page reload from URL
  - browser back/forward after changing team/from sprint/to sprint
  - navigation from Trends to Delivery, Portfolio, PR, and Backlog still carries expected context
- **Safe to merge alone?** **Yes** — but only if legacy aliases are still accepted and no page execution logic is switched.
- **Safe to deploy alone?** **Conditionally yes** — this is a high-risk navigation PR and needs expanded manual verification.
- **Rollback scope:** revert trends workspace URL switch only.

### PR 13 — Switch PR and validation/work-item drill-down URL parsing and emission
- **Phase:** 4
- **Goal:** Centralize the highest-risk page-specific deep-link and drill-down query parsing before any page execution migration starts.
- **Scope:** PR pages, validation drill-down pages, and Work Item Explorer query-backed state.
- **Exact likely files / modules affected:**
  - `PoTool.Client/Pages/Home/PrOverview.razor`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
  - `PoTool.Client/Services/NavigationContextService.cs`
  - audit references for current request/query composition that must stay unchanged in this phase:
    - `PoTool.Client/Services/WorkItemService.cs`
    - `PoTool.Client/ApiClient/ApiClient.PullRequestInsights.cs`
    - `PoTool.Client/ApiClient/ApiClient.PrDeliveryInsights.cs`
- **Exact changes to make:**
  - replace manual page query parsing for PR URLs, validation category/rule URLs, and work-item deep-link flags with canonical parser usage
  - replace page-built drill-down URLs with canonical serializer output
  - keep transport query builders (`WorkItemService`, handcrafted PR API client query builders) unchanged unless reuse of a shared serializer can be done with zero behavior change
- **Dependencies:**
  - PR 10
  - PR 9
- **Tests required:**
  - parser/serializer tests for PR date/repository query state
  - parser/serializer tests for validation `category`/`ruleId` URLs
  - parser/serializer tests for Work Item Explorer deep-link flags
  - regression tests that legacy aliases remain readable
- **Manual verification required:**
  - PR page reload from URL
  - Validation drill-down URLs
  - Work Item Explorer deep links
  - browser back/forward across drill-down navigation
- **Safe to merge alone?** **Yes** — only if parsing switch, round-trip verification, and legacy alias acceptance ship together.
- **Safe to deploy alone?** **Conditionally yes** — not without manual reload/deep-link verification because these are user-facing deep links.
- **Rollback scope:** revert PR/validation/work-item URL switch only.

---

## 3. Cross-PR Dependencies

### 3.1 Dependency chain

1. **PR 1** unlocks all later PRs.
2. **PR 2** depends on PR 1 and unlocks client-side contracts/state usage.
3. **PR 3** depends on PR 1–2 and unlocks shadow-mode page activation.
4. **PR 4** depends on PR 1–3 and unlocks URL and mapper implementation work.
5. **PR 5** depends on PR 1 and Phase 0 visibility; it unlocks endpoint-family hardening.
6. **PR 6** depends on PR 5 and unlocks stable backend metadata for the selected portfolio family.
7. **PR 7** depends on PR 2–3 and Phase 0 logging; it unlocks broader shadow-mode rollout.
8. **PR 8** depends on PR 7; it unlocks trends/time shadow verification.
9. **PR 9** depends on PR 7–8; it unlocks Phase 4 page-level URL switches for PR/validation/work-item pages.
10. **PR 10** depends on PR 4 and benefits from PR 9 input coverage; it unlocks all page URL switch PRs.
11. **PR 11** depends on PR 10 and PR 7; it unlocks low-risk canonical URL emission.
12. **PR 12** depends on PR 10 and PR 8; it unlocks trends URL centralization completion.
13. **PR 13** depends on PR 10 and PR 9; it unlocks completion of Phase 4 and readiness for Phase 5 page execution migration.

### 3.2 Cross-PR sequencing rules

- do not start any Phase 3 page shadow PR before PR 3 is merged
- do not start any page URL switch PR before PR 10 is merged
- do not switch page URL emission and parsing in separate PRs for the same page family
- do not start Phase 5 page execution migration on any page family until the relevant Phase 3 shadow PR and Phase 4 URL PR are both complete

---

## 4. Test Strategy by PR

### PR 1
- **Required automated tests:** shared/core filter primitive unit tests; `FilterContext` validation tests; DTO compatibility tests
- **Required manual checks:** build only; verify no runtime behavior changes

### PR 2
- **Required automated tests:** state model tests; contract applicability tests; registry tests; DI tests if needed
- **Required manual checks:** app starts; no pages read the new state yet

### PR 3
- **Required automated tests:** validation engine tests; `FilterService` state recomputation tests; time-mode invalid-shape tests
- **Required manual checks:** app startup with no visible behavior change

### PR 4
- **Required automated tests:** parser/serializer tests; mapper shell tests; `NavigationContextServiceTests` updates
- **Required manual checks:** existing navigation and API behavior unchanged

### PR 5
- **Required automated tests:** `PortfolioFilterResolutionServiceTests`; service registration tests; invalid-field and effective-filter tests
- **Required manual checks:** selected portfolio endpoints still return valid payloads and log requested/effective filters

### PR 6
- **Required automated tests:** `MetricsControllerPortfolioReadTests`; `PortfolioQueryServicesTests`; `PortfolioReadModelStateServiceTests`
- **Required manual checks:** portfolio pages still load; metadata is present and consistent

### PR 7
- **Required automated tests:** shadow bootstrap tests for shared-context pages; no-request-switch regression tests
- **Required manual checks:** Home → Health, Home → Validation Triage, page load success, shadow logs visible

### PR 8
- **Required automated tests:** time-state shadow tests; contract applicability tests for trends/delivery pages
- **Required manual checks:** Trends page load, Delivery Trends load, Sprint Execution load, Portfolio pages load, zero unexplained mismatches

### PR 9
- **Required automated tests:** PR/validation/work-item shadow hydration tests; no-request-switch regression tests
- **Required manual checks:** PR reload from URL, Validation Queue/Fix load, Work Item Explorer deep links, mismatch review completed

### PR 10
- **Required automated tests:** canonical URL round-trip tests; legacy alias parser tests; canonical serializer tests
- **Required manual checks:** no page URL change yet; legacy navigation context tests still pass

### PR 11
- **Required automated tests:** updated `NavigationContextServiceTests`; shared workspace parsing/emission regression tests
- **Required manual checks:** Home → Health, Home → Trends, Home → Validation Triage, back/forward behavior, canonical emission after parser switch only

### PR 12
- **Required automated tests:** trends URL round-trip tests; legacy sprint-range alias tests; downstream context-carry tests
- **Required manual checks:** Trends deep links, reload, team/from/to sprint changes, back/forward, linked navigation still works

### PR 13
- **Required automated tests:** PR URL tests; validation drill-down parser tests; Work Item Explorer deep-link tests; alias-acceptance tests
- **Required manual checks:** PR page reload from URL, validation drill-down URLs, Work Item Explorer deep links, back/forward across drill-down flows

---

## 5. Safe Merge / Safe Deploy Assessment

### 5.1 Safe to merge alone

**Safe to merge alone:** PRs **1, 2, 3, 4, 5, 6, 7, 8, 9, 10** — provided they remain infrastructure-only, backend-family-only, or shadow-only.

**Conditionally safe to merge alone:** PRs **11, 12, 13** — only if each PR contains:

- parser switch and serializer switch together for that page family
- legacy alias acceptance
- round-trip tests
- explicit manual verification plan for the affected routes

### 5.2 Safe to deploy alone

**Safely deployable before page migration:**

- PRs **1–4**
- PRs **5–6** for the selected portfolio endpoint family
- PRs **7–10** if shadow mode remains read-only and visible through logging

**Not safe to deploy alone without manual verification:**

- PR **11** — shared workspace navigation context changes
- PR **12** — Trends deep-link and sprint-range URL changes
- PR **13** — PR reload/deep-link and validation/work-item drill-down URL changes

### 5.3 What must accompany unsafe deployments

- **PR 11** must ship with manual verification of Home-based navigation and back/forward behavior.
- **PR 12** must ship with Trends deep-link and sprint-range round-trip verification.
- **PR 13** must ship with PR reload, validation drill-down, and Work Item Explorer deep-link verification.

---

## 6. Phase Exit Criteria

### Phase 1 exit criteria
Must be true before Phase 2 starts:

- shared/core canonical filter primitives exist and are tested
- client state/contracts/registry exist and are tested
- `FilterService` and client validation engine exist but are unused by pages
- canonical URL/parser and request-mapper skeletons exist but are unused by pages
- solution builds cleanly with no behavior change

### Phase 2 exit criteria
Must be true before Phase 3 starts:

- selected backend endpoint family has explicit requested/effective/invalid filter behavior
- selected backend endpoint family exposes filter metadata consistently
- no silent defaults remain in the selected canonical backend path
- targeted controller/service/query tests pass
- logs make canonical backend filter resolution visible per request

### Phase 3 exit criteria
Must be true before Phase 4 starts:

- target pages are initialized with shadow-mode `FilterService`
- pages still use legacy request logic only
- shadow selected/effective/applicable/invalid state is visible in logs
- for every activated page family there are **zero unexplained shadow-vs-legacy mismatches**, **or all remaining mismatches are documented as intentional corrections**
- no page reloads or visible regressions are introduced by shadow activation

### Phase 4 exit criteria
Must be true before any Phase 5 page execution migration starts:

- canonical URL parser/serializer core is merged and tested
- page families switched in Phase 4 parse through the canonical path
- each switched page family has round-trip verification
- legacy aliases are still accepted for switched page families
- a page emits canonical URLs **only after**:
  - its parsing path is switched
  - round-trip is verified
  - legacy aliases remain accepted
- manual test matrix items affected by the switched page families have passed

---

## 7. High-Risk PRs

### PR 6 — Standardize portfolio read endpoint metadata and handler usage
- **Why risky:** touches live backend semantics and response contracts for selected portfolio endpoints
- **How to reduce risk:** keep the scope inside the portfolio read family only; reuse existing `PortfolioFilterResolutionService`; add controller/query/state-service tests before merge
- **Extra verification required:** portfolio page load, response metadata presence, invalid-field echoing, no silent fallback

### PR 11 — Switch shared workspace context parsing and emission to canonical URL handling
- **Why risky:** affects the most common navigation paths and shared `productId`/`teamId` propagation
- **How to reduce risk:** keep legacy alias parsing; switch parser and serializer together; verify `WorkspaceBase` descendants as a group
- **Extra verification required:** Home → Health, Home → Trends, Home → Validation Triage, browser back/forward

### PR 12 — Switch trends workspace URL parsing and emission
- **Why risky:** trends currently owns bespoke sprint-range parsing and emits URLs used to restore time context
- **How to reduce risk:** do not change page execution logic in the same PR; preserve `teamId`, `fromSprintId`, `toSprintId` readability; verify navigation to linked pages
- **Extra verification required:** trends reload, deep-link round-trip, linked navigation from Trends, back/forward after selection changes

### PR 13 — Switch PR and validation/work-item drill-down URL parsing and emission
- **Why risky:** these flows are deep-link heavy and user-facing; they mix page-local parsing with drill-down navigation and reload behavior
- **How to reduce risk:** keep API request helpers unchanged; switch only browser URL ownership; preserve legacy aliases; verify each page family independently
- **Extra verification required:** PR reload from URL, Validation Queue/Fix drill-downs, Work Item Explorer deep links, browser history behavior

### PR 9 — Activate shadow-mode `FilterService` for PR, pipeline, validation queue/fix, and work item pages
- **Why risky:** highest chance of shadow-vs-legacy mismatch because these pages combine date, sprint, repository, validation category/rule, and query-backed state
- **How to reduce risk:** log every selected/effective mismatch; keep request logic entirely legacy; block progression until mismatches are explained
- **Extra verification required:** mismatch review sign-off plus page load verification for PR, validation, and work-item routes

---

## 8. Minimum Manual Test Matrix

| Flow | What to verify | Mandatory after PR(s) |
|---|---|---|
| Home → Health | selected product context still carries forward; page loads; URL remains readable; back returns to Home cleanly | PR 7, PR 11 |
| Home → Trends | selected product context still carries forward; Trends page opens with expected context | PR 7, PR 11 |
| Trends deep links | `teamId`, sprint-range values restore correctly; canonical round-trip works; legacy URLs still open | PR 8, PR 12 |
| PR page reload from URL | PR Overview and PR Delivery pages still restore the same visible state from URL on reload | PR 9, PR 13 |
| Validation drill-down URLs | Validation Triage → Queue → Fix still preserves category/rule/product context and reloads correctly | PR 9, PR 13 |
| Work Item Explorer deep links | query-backed flags and root context still restore correctly; no filter loss on reload | PR 9, PR 13 |
| Browser back/forward behavior | no context loss or duplicated state transitions when navigating among Home, Trends, PR, and validation flows | PR 11, PR 12, PR 13 |
| Portfolio read pages | portfolio read pages still load; response metadata is present; no unexpected filter behavior | PR 5, PR 6 |

### 8.1 Mandatory manual verification before pushing after Phase 4

Before pushing any PR that completes part of Phase 4, manually verify:

- canonical URL round-trip for the switched page family
- legacy alias acceptance for that same page family
- page reload from URL for the switched page family
- browser back/forward behavior involving that page family
- no visible behavior change beyond canonical URL emission

---

## 9. Summary

This PR breakdown creates a strict, reviewable chain for Phases 1–4.

- **Open first:** **PR 1 — Add shared and core canonical filter primitives**
- **Safely deploy before page migration:** Phase 1, selected Phase 2 backend portfolio hardening, shadow-only Phase 3, and inactive/core Phase 4 URL infrastructure
- **Cannot safely deploy alone:** page-family URL switch PRs without parser switch, round-trip verification, and legacy alias support
- **Must be manually verified before pushing after Phase 4:** shared workspace navigation, Trends deep links, PR reload from URL, validation drill-down URLs, Work Item Explorer deep links, and browser back/forward behavior

If executed in this order, the team can complete Phases 1–4 without collapsing risky concerns into giant PRs, without activating dual logic in the same execution path, and without starting Phase 5 page execution migration before the foundation, backend enforcement, shadow-state confidence, and URL ownership changes are all independently proven.
