# Copilot Instructions — PO Companion (Authoritative)

This file is the single authoritative rule source for AI-assisted work in this repository.

- `.github/copilot-instructions.md` = enforcement + source of truth
- `docs/rules/` = human-readable mirror (non-authoritative)

You MUST apply every rule in this file to every response, proposal, refactor, review, and implementation.

---

## 1. Authority and rule loading

- Do not treat any other rule document as authoritative.
- `docs/rules/` exists only as a mirror for humans.
- If a mirrored rule file conflicts with this file, this file wins.
- If any instruction set conflicts, is ambiguous, or cannot be satisfied, stop and ask for clarification unless the active user prompt explicitly resolves the conflict.

---

## 2. Binding pre-generation verification (silent)

Before producing output or code, you MUST internally verify that:

- UX and UI rules are respected
- architecture boundaries are respected
- process rules are respected
- no duplication is introduced
- no unapproved dependencies are required
- release-note obligations are satisfied or a valid bypass applies
- any requested documentation change respects documentation governance

Do not mention this verification in the output.

---

## 3. Decision discipline (hard)

- Do not invent requirements, behavior, or architecture.
- Do not make implicit product or architecture decisions.
- If multiple valid options exist:
  - list the options
  - describe trade-offs
  - wait for instruction before choosing
- When uncertain, stop.
- If clarification is required, batch blocking questions into one response.

---

## 4. Documentation governance (hard)

Repository documentation governance is enforced strictly.

- Markdown files under `docs/` may live only in these canonical folders:
  - `architecture`
  - `analysis`
  - `reports`
  - `audits`
  - `history`
  - `archive`
  - `user`
  - `rules`
  - `plans`
  - `reviews`
- `docs/README.md` is the only markdown file allowed at the `docs/` root.
- Markdown filenames under `docs/` must use lowercase kebab-case.
- Active documentation must not be stored under `docs/archive`.
- Historical material belongs under `docs/history` or `docs/archive`, not in active canonical folders.
- When moving or renaming documentation, update all affected links and references in the same change.
- Do not create markdown report, analysis, or planning files unless the user explicitly requests a specific markdown file or an existing canonical repository document must be updated.
- `docs/rules/` is a mirror only; do not treat it as the source of truth.

If documentation governance cannot be satisfied without clarification, batch all blocking questions together before asking.

---

## 5. Markdown report output (hard)

For every analysis, validation, audit, review, migration, cleanup, or implementation prompt:

- create a markdown report file in the repository
- the prompt must specify the exact path, or the active task must already define a canonical repository path to use
- the task is incomplete if the report is not written

### Filename convention (hard)

- Format: `YYYY-MM-DD-<kebab-case-name>.md`
- lowercase only
- kebab-case only
- no spaces or underscores
- no generic names

Hard failure:

- missing report = failure
- invalid filename = failure

Required behavior:

1. perform the requested work
2. write the report file
3. confirm the path

---

## 6. Pull request fitness and output quality

All output MUST be:

- suitable for senior-level review
- single-purpose and scope-controlled
- free of speculation
- architecture-safe
- duplication-free
- maintainable over time
- explicit about known limitations and intentionally unchanged areas

Assume strict review by default.

---

## 7. Duplication policy (non-negotiable)

- Duplication is forbidden.
- Repeated UI structures MUST be extracted into reusable Blazor components.
- Repeated backend logic MUST be extracted into Core services or helpers.
- “Leave it for later” is not acceptable.

---

## 8. Technology constraints

- Frontend: Blazor WebAssembly
- UI library: open-source Blazor components only
- MudBlazor is the mandatory component library
- No JS/TS UI widgets
- Backend: ASP.NET Core Web API + SignalR
- Mediator: source-generated Mediator only
- DI: Microsoft.Extensions.DependencyInjection only
- Tests: MSTest; no real TFS usage
- Development environment: HTTP-only is allowed for development, but production MUST use HTTPS

---

## 9. Architecture and layering (non-negotiable)

### 9.1 Clean architecture

- Dependencies must point inward toward the domain.
- The domain layer must have zero external dependencies.
- The application/core layer may depend on shared contracts but not on infrastructure or UI frameworks.
- Infrastructure and API layers may depend on Core and Shared.
- The client must not reference Core.

### 9.2 Assembly boundaries

- `PoTool.Shared`: DTOs, enums, constants, request/response contracts, and cross-boundary exception contracts only.
- `PoTool.Core`: business logic, domain services, validators, abstractions, command/query contracts, and use-case orchestration.
- `PoTool.Api`: handlers, controllers, persistence, repositories, TFS clients, SignalR hubs, and integration implementations.
- `PoTool.Client`: Razor components, pages, layouts, UI state, view models, and typed frontend services.

### 9.3 Communication rules

- The client communicates with the backend by HTTP only, through typed frontend services using Shared DTOs.
- Direct `HttpClient` calls from pages or components are forbidden.
- UI code must not reference Core or infrastructure code.
- UI code must not call TFS APIs directly.

### 9.4 Mediator, commands, queries, and results

- Source-generated Mediator is mandatory; MediatR is forbidden.
- Commands and queries must use the repository’s mediator/result patterns.
- Handlers live in `PoTool.Api`.
- Controllers must remain thin and delegate to handlers.
- Service and handler flows should return structured `Result`/`Result<T>` outcomes instead of ad hoc business exceptions.

### 9.5 Persistence and repositories

- Repositories are allowed where they improve clarity, but direct `DbContext` use is acceptable for simple cases.
- Keep persistence logic out of Core and Client.
- Return materialized results, not `IQueryable`, from service boundaries.

### 9.6 Dependency injection

- Register services in layer-appropriate DI registration files.
- Choose lifetimes deliberately: transient for stateless services, scoped for request-bound services, singleton only for stateless services independent of scoped dependencies.
- Depend on abstractions instead of concrete implementations where it improves boundaries and testability.

---

## 10. Entity Framework rules (non-negotiable)

### 10.1 Core invariant

No EF Core operation may run concurrently on the same `DbContext` instance.

### 10.2 Mandatory two-phase pattern

- Phase 1 — collect: network calls and CPU-only work may run in parallel, but no EF Core access is allowed.
- Phase 2 — persist: use a single `DbContext`, fully await each EF operation, and persist sequentially.

### 10.3 Forbidden EF patterns

- EF access inside `Task.WhenAll`, `Parallel.ForEach`, throttled fan-out loops, or async LINQ selectors
- returning `IQueryable` from service boundaries
- caching or storing `DbContext`/repositories for reuse across scopes
- injecting `DbContext` into singleton or hosted services

### 10.4 Required EF patterns

- Materialize data before leaving the persistence boundary.
- Use `IEntityTypeConfiguration<T>` for model configuration and apply configurations from assembly.
- Keep `DbContext` scoped.
- If true parallel database work is required, use isolated scopes or `IDbContextFactory<TContext>`.

### 10.5 SQLite timestamp rule

- Do not use `DateTimeOffset` in server-side SQLite predicates, sorting, aggregates, or watermark logic.
- Persist queryable timestamps as UTC `DateTime` columns with a `Utc` suffix.
- Convert incoming timestamps to UTC before persistence.
- Compute UTC bounds outside LINQ and compare against UTC values inside LINQ.

### 10.6 Migration rules

- Commit each migration with both generated files.
- Never hand-edit migration designer files.
- Generate migrations only from a clean, building solution with verified model changes.
- Validate upgrade and rollback behavior locally before commit.

---

## 11. TFS integration rules (binding)

### 11.1 Boundaries and source ownership

- TFS/Azure DevOps access is allowed only through the integration layer.
- Direct database access to TFS is forbidden.
- Treat TFS as an external source of truth that must be mapped into internal canonical models.
- Do not leak TFS-specific concepts, IDs, or assumptions outside the integration boundary unless they are explicit public contracts.

### 11.2 Authentication and secrets

- PATs may be accepted only for immediate use and validation.
- PATs must never be stored server-side, persisted to disk, cached, or logged.
- Credentials and secrets must never appear in logs or diagnostics.

### 11.3 API usage

- Use documented REST APIs only.
- Specify the API version explicitly.
- Avoid undocumented endpoints and version mixing.
- Keep reads idempotent and bounded.
- Keep writes explicitly scoped and minimally invasive.

### 11.4 Verify TFS API diagnostics

Verify-TFS capabilities must produce deterministic, human-readable diagnostics that include:

- a stable capability identifier
- impacted functionality
- expected behavior and observed behavior
- categorized failure type
- sanitized evidence only
- likely causes and resolution guidance

### 11.5 Write-verification safeguards

- Write verification must be opt-in.
- Never auto-select production work items.
- Use deterministic temporary artifact naming when temporary artifacts are permitted.
- Never delete user-created artifacts.
- Report cleanup status explicitly.
- A feature that changes TFS write behavior is incomplete unless its write-verification path is defined and validated.

### 11.6 Resilience and testing

- Categorize TFS failures clearly.
- Retry only transient failures.
- Assume TFS may be slow, stale, or unavailable.
- All TFS code must be testable without live TFS.

---

## 12. UI architecture and UX rules (binding)

### 12.1 Platform and responsibility split

- The UI is Blazor WebAssembly hosted by ASP.NET Core.
- UI code is presentational and orchestration-focused only.
- UI code must not contain business logic, persistence logic, or domain rule implementation.
- UI may map DTOs to view models, manage UI state, and coordinate service calls.

### 12.2 Component model

- Prefer small, focused, reusable components.
- Extract repeated UI structures into shared components or helpers.
- Keep components parameter-driven and stateless where practical.

### 12.3 Navigation model

- Navigation is intent-driven and contextual.
- Do not introduce a permanent feature-based sidebar as the final architecture.
- Users enter through explicit workspace entry points and progress through contextual navigation.
- Navigation must be explicit, reversible, and free of side effects.
- Use the repository’s workspace navigation components for workspace navigation.

### 12.4 Workspace rules

- Navigation workspaces use static tiles and should not require heavy hub-entry data loading.
- Signal workspaces may expose dynamic signal tiles only when the runtime signal is meaningful and independently loaded.
- Static tiles must remain understandable without live data.
- Dynamic tiles must degrade gracefully and must not require heavy hub-entry queries.

### 12.5 UX principles

- Clarity over density.
- State must always be visible.
- Behavior must be predictable.
- Use progressive disclosure for advanced capabilities.
- Avoid surprise navigation.

### 12.6 Buttons and emphasis

- Visual hierarchy is: navigation tiles > cards and dashboards > buttons.
- Buttons must not visually compete with tiles or primary analytical content.
- Use utility, action, and critical button emphasis deliberately.
- Icons support recognition only and must not dominate labels.

### 12.7 Forms, validation, feedback, and accessibility

- Keep form validation consistent and visible.
- Errors, empty states, disabled states, and filtered states must be visually distinct.
- Preserve accessibility, responsiveness, and dark-only theme expectations.

### 12.8 Analytical pages

- Analytical pages must have exactly one primary visualization.
- Secondary visualizations belong below the primary visualization.
- Filters and configuration UI must not dominate the visual hierarchy.
- Summary tiles must stay visually secondary to the primary visualization.

---

## 13. Fluent compact density rules (binding)

- Follow a compact, Fluent-aligned density system consistently.
- Use the repository spacing tokens and consistent compact dimensions.
- Prefer fixed, predictable component heights over content-driven vertical drift.
- MudBlazor compact defaults are mandatory where supported.
- Do not mix dense and non-dense variants without explicit need.
- Use repository wrapper components for compact controls where available.
- Avoid arbitrary pixel tuning; use the density token system.

---

## 14. UI loading and rendering rules (binding)

Core principle: render first, then load data progressively.

- Never block first render.
- Always render the page skeleton first.
- Load data asynchronously after the initial render.
- Use progressive, component-level loading.
- Avoid full-page loading gates.
- Cache expensive queries appropriately.
- Load only visible or immediately relevant data.
- Keep pages lightweight and push heavy logic into services.
- Cancel unnecessary background work when the user navigates away.

---

## 15. Validation and semantic rules (binding)

### 15.1 Validation pipeline

- Commands and queries with input must have validators.
- Validators belong alongside the command or query.
- Validation runs through the mediator pipeline, not in controllers.

### 15.2 Validation semantics and integrity model

The current validation/integrity model includes structural integrity, refinement readiness, and implementation readiness rules. Preserve canonical ownership and avoid duplicate semantic definitions.

- Structural integrity rules cover parent/child state consistency.
- Refinement readiness rules cover description quality and required child readiness for higher-level items.
- Implementation readiness rules cover PBI-level readiness, including required effort.
- The missing-effort rule is an alias of the canonical implementation-readiness effort rule and must not diverge semantically.
- Preserve deterministic execution order and consistent categorization.

### 15.3 UI semantics

- Story points represent planning and delivery scope.
- Effort hours represent engineering workload.
- Never label effort hours as points.
- Portfolio-flow and related analytics surfaces must use canonical story-point semantics in both computation and labels.

---

## 16. Domain model and canonical analytics rules (binding)

PoTool analytics logic must follow the canonical domain model in `docs/domain/domain_model.md`.

### 16.1 Hierarchy rules

- Operational planning hierarchy is Epic → Feature → PBI, with Tasks below PBIs.
- The full theoretical hierarchy may include Goal and Objective above Epic.
- PBI is the delivery unit for sprint and delivery metrics.
- Product scope is the primary analytics boundary.
- Parent completion is not automatically derived from child completion.

### 16.2 Estimation rules

- Story points originate on PBIs only and represent relative delivery complexity.
- Ignore story points on Bugs and Tasks for canonical delivery metrics.
- Resolve story-point values using the repository’s canonical field precedence.
- Missing or zero values are handled according to canonical completion-aware semantics.
- Effort is an hours-based workload metric that may originate on Epic, Feature, or PBI and rolls up by precedence.
- Derived story points are for aggregation only and must remain explicit as derived semantics.

### 16.3 State rules

- All raw TFS states must map to one canonical lifecycle state: New, InProgress, Done, or Removed.
- Mapping is per work-item type and comes from repository configuration, not hardcoded strings.
- Delivery is the first transition to canonical Done.
- Reopen transitions represent rework, not a new delivery event.
- Same-state canonical transitions are not meaningful transitions.

### 16.4 Sprint rules

- Sprint analytics use the sprint window and the repository’s commitment timestamp semantics.
- “On sprint” and “during sprint” are distinct concepts and must not be conflated.
- Delivery is determined by first-done-in-window behavior, not by current sprint assignment.
- Churn, activity, work, and spillover must follow the canonical sprint event rules.
- Spillover requires committed scope, not done at sprint end, and movement into the next sprint.

### 16.5 Propagation rules

- Activity and work signals propagate upward as boolean presence.
- Story-point and effort propagation follow canonical origin and rollup rules.
- Delivery does not automatically roll up from descendants; state-driven parent logic remains authoritative.
- Removed scope remains historically visible and participates only in the analytics that canonically require it.

### 16.6 Metrics rules

- Velocity counts delivered story points from PBIs with first-done transitions inside the sprint window.
- Committed scope is reconstructed at the canonical commitment timestamp.
- Commitment completion removes removed scope from the denominator.
- Churn, spillover, added-delivery, unestimated-delivery, and remaining-scope metrics must use canonical formulas and exclusions.

### 16.7 Data-source rules

- Snapshots answer current-state questions.
- Update history answers event-timing questions.
- Hybrid analyses may combine both, but current-state truth comes from snapshots.
- Resolve source conflicts with the canonical snapshot/update rules, not ad hoc logic.

---

## 17. Process rules (binding)

### 17.1 Core process

- Speed is secondary to correctness, clarity, and consistency.
- Follow the mandatory work order: understand, confirm scope, check rules, propose, implement, review, and merge.
- Skipping process steps is not allowed.

### 17.2 Review standard

- All code must be reviewed before merge.
- Review is a rule-compliance and design check, not just a style pass.
- Block on rule violations, security issues, hidden assumptions, and boundary violations.

### 17.3 Review checklist

Every review must check:

- UX/UI compliance
- architecture compliance
- process compliance
- duplication
- correctness
- scope control
- testability and maintainability

### 17.4 Review feedback limits

- Unlimited blocker comments for true blockers
- at most 3 major comments
- at most 5 minor comments
- defer or document anything beyond that as follow-up noise control

### 17.5 Scope control and refactoring

- One PR = one goal.
- Do not add convenient extras.
- Do not introduce cross-cutting changes without approval.
- Refactoring during review is allowed only if it reduces duplication, improves rule compliance, or improves testability without changing observable behavior or architecture boundaries.

### 17.6 Review outcome

Every reviewed change must end with explicit confirmation of rule compliance, what changed, what intentionally did not change, and known limitations.

### 17.7 Definition of done

A change is done only when it has passed review, addressed duplication, re-checked rules, compiled cleanly, and passed required tests.

---

## 18. Client-side async guardrails (mandatory)

- No sync-over-async in `PoTool.Client`.
- Treat `.Result`, `.Wait(...)`, `GetAwaiter().GetResult()`, `AsTask().Result`, and `AsTask().Wait()` as forbidden patterns.
- Client-facing APIs must be async-first.
- Changes touching `PoTool.Client` must demonstrate proper async usage through lifecycle and event flows.
- Any sync-over-async in the client is an architectural violation and a review blocker.

---

## 19. Release notes discipline (mandatory)

### 19.1 Canonical source

User-visible release notes belong only in `docs/release-notes.json`.

### 19.2 When release notes are required

Add or update release notes when a change affects:

- routes or pages
- workflow or UI behavior
- metrics, trend, grouping, filtering, or semantic meaning
- scoping or filter defaults and drill-down behavior
- sync/cache behavior visible to users
- validation behavior or user-visible validation messages
- any PO-facing decision surface

### 19.3 Path-based triggers

Assume release notes are required for changes under:

- `PoTool.Client/**`
- `PoTool.Api/**` when DTOs or metric/trend computations visible to the UI are affected
- `docs/GEBRUIKERSHANDLEIDING*.md` when shipped behavior is being updated

### 19.4 Allowed bypass

Release notes may be skipped only for no-user-impact changes such as pure refactors, internal cleanup, comments, or formatting, and only if the PR description contains exactly:

`ReleaseNotes: N/A (no user impact)`

### 19.5 End-of-work confirmation

At task completion, explicitly state one of:

- `Release notes: updated`
- `Release notes: N/A (no user impact)`

---

## 20. Testing rules (binding)

- Use MSTest for unit tests.
- Do not use real TFS in tests.
- Business logic must remain unit-testable without infrastructure.
- Do not disable or skip tests to get a change through.
- Conditional UI behavior should be testable.
- Critical user flows should have automated coverage where practical.

---

## 21. Code quality rules (binding)

- Use descriptive names.
- PascalCase for public members and types.
- camelCase for locals.
- Prefix private fields with underscores.
- Add documentation where it explains why, not what.
- Keep architecture and design documents aligned with repository reality.

---

## 22. Repository hygiene (critical)

### 22.1 Never commit build artifacts

- Before staging or committing, verify no build artifacts are staged.
- Never commit binary files, DLLs, EXEs, or build outputs.
- After builds/tests, inspect `git status` before committing.
- If build artifacts appear, reset them and fix the root cause.

### 22.2 Path handling

- Always use forward slashes in paths.
- Do not create or stage paths containing literal backslashes.

### 22.3 Pre-commit verification

1. run `git status`
2. verify no `bin/`, `obj/`, or equivalent build output paths are staged
3. if build artifacts appear, unstage them and fix the ignore/root cause before retrying

---

## 23. Mandatory stop conditions

Stop and ask for clarification if:

- requirements are incomplete or ambiguous
- any rule would be violated
- a new dependency appears necessary
- a cross-layer or cross-cutting change is implied without approval
- scope creep is detected

Stopping is correct behavior.

---

## 24. Prohibited practices (hard)

- direct database access to TFS
- UI code calling TFS APIs directly
- hardcoded org/project/collection assumptions in integration logic
- MediatR usage
- sync-over-async in `PoTool.Client`
- EF Core concurrency on the same `DbContext`
- returning `IQueryable` from service boundaries
- hidden behavior changes outside scope
- full-page loading gates
- arbitrary CSS density drift outside the token system
- silent approval after review
- empty release-note handling for user-visible changes
- secrets in logs, code, or persisted state

---

## 25. Self-review checklist

Before finalizing work, verify:

- no architecture boundary violations exist
- Core remains infrastructure-free
- Client does not reference Core
- EF concurrency rules are preserved
- client async rules are preserved
- UI rules and loading rules are respected
- duplication has been actively removed
- TFS integration boundaries are intact
- release-note obligations are satisfied
- required tests pass
- repository hygiene is intact

---

Deviations from these rules require explicit approval and documentation.
