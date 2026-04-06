# Onboarding Implementation Slices

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-migration-strategy.md`

This document defines the executable implementation slicing plan for the onboarding redesign and migration. It does not change any prior design, domain, API, or migration decisions.

## 1. Slicing Principles

### 1.1 Definition of a valid slice

A slice is valid only if all of the following are true:

1. It has one architectural purpose.
2. It produces a stable, testable artifact.
3. It does not require hidden behavior from a later slice.
4. It can be enabled, disabled, or left dormant without corrupting persisted data.
5. It does not create a temporary persistence model that must later be rewritten.

### 1.2 Mandatory completion rule per slice

No later slice may start until the current slice has:

1. merged schema or code for its declared scope
2. passed its slice-specific automated tests
3. passed manual verification for its public surface
4. produced required logs/metrics for observability
5. no known blocker defects in its acceptance criteria

### 1.3 Backend-first rule

The implementation order is backend first:

1. persistence
2. lookup/validation
3. status engine
4. migration infrastructure
5. migration execution
6. CRUD/write APIs
7. UI flows
8. cutover
9. cleanup

No UI slice may begin before the backend slice it depends on is complete and verifiable.

### 1.4 Forbidden slice construction

The following are forbidden inside one slice:

- domain model change plus UI flow rollout
- migration execution plus cutover
- partial persistence schema without final constraints
- UI that depends on unimplemented validation or status rules
- temporary dual-write bridges between old and new onboarding systems
- migration against a schema that has not completed automated validation

### 1.5 Feature flag rule

Feature flags may isolate incomplete systems, but they may not hide broken backend contracts.

Rules:
1. Flags isolate exposure, not correctness.
2. No flag may allow mixed writes between old and new onboarding models in the same environment.
3. A slice that writes production data must be complete behind the flag before any user-facing UI is enabled.

### 1.6 Migration rule

Migration is not allowed until all of the following exist:

1. stable new schema
2. stable lookup and validation APIs
3. stable onboarding status engine
4. stable migration ledger and issue model
5. stable idempotent upsert logic

## 2. Slice Overview

| Slice | Name | Purpose | Blocks |
|---|---|---|---|
| 1 | Persistence foundation | Introduce final persisted onboarding model | All later slices |
| 2 | Lookup & validation APIs | Live lookup, validation, and snapshot shaping | Slices 3, 5, 6, 7, 8, 9, 10 |
| 3 | Completion/status engine | Compute onboarding status from persisted state | Slices 7, 8, 9, 11 |
| 4 | Migration infrastructure | Add ledger, issue tracking, and execution framework | Slice 5 |
| 5 | Migration execution | Perform legacy-to-new migration with idempotent upserts | Slices 10, 11 |
| 6 | Onboarding backend CRUD | Final write APIs for new model | Slices 7, 8, 9, 10 |
| 7 | UI Flow 1 — TFS Connection | New connection flow behind flag | Slice 11 |
| 8 | UI Flow 2 — Data Sources | New project/team/pipeline flow behind flag | Slice 9, 11 |
| 9 | UI Flow 3 — Domain Configuration | New product root/binding flow behind flag | Slice 11 |
| 10 | Import path | Import into new model only | Slice 11 |
| 11 | Cutover preparation | Switch routing/status authority and disable old writes | Slice 12 |
| 12 | Cleanup | Remove obsolete wizard/state/endpoints | Final |

## 3. Slice Definitions

### Slice 1 — Persistence foundation

**Scope**
- Add final persisted entities:
  - `TfsConnection`
  - `ProjectSource`
  - `TeamSource`
  - `PipelineSource`
  - `ProductRoot`
  - `ProductSourceBinding`
- Add final value objects needed by those entities:
  - validation state storage
  - snapshot metadata
  - final keys and uniqueness constraints
- Add database migrations for the new onboarding schema

**Explicit exclusions**
- no lookup endpoints
- no write endpoints
- no migration ledger
- no migration execution
- no onboarding status computation
- no UI

**Dependencies**
- authoritative redesign and domain/API alignment only

**Deliverables**
- final EF model
- final migration files
- schema constraints that enforce external-ID identity and binding uniqueness

**Acceptance criteria**
- database migration applies cleanly on a clean database
- database migration rolls back cleanly in local verification
- unique identities exist for all external-ID-backed entities
- no old tables or flows are modified to write to new tables

**Risks if done incorrectly**
- later slices build on unstable schema
- migration must be rewritten
- duplicate identities become possible at the database level

### Slice 2 — Lookup & validation APIs

**Scope**
- Add live lookup APIs for:
  - projects
  - teams
  - pipelines
  - work items
- Add live validation services for:
  - connection
  - project source
  - team source
  - pipeline source
  - product root
  - source bindings
- Add snapshot shaping from live responses into persisted snapshot contracts

**Explicit exclusions**
- no persisted onboarding writes except storing revalidation results in controlled backend tests if needed
- no migration execution
- no onboarding UI
- no status engine

**Dependencies**
- Slice 1 complete

**Deliverables**
- lookup endpoints with structured success/error contracts
- validation services with sanitized failure outputs
- snapshot mappers that match authoritative contracts

**Acceptance criteria**
- each lookup endpoint returns authoritative IDs plus required confirmation fields
- each validation path returns deterministic categorized failure codes
- no endpoint depends on wizard/session state
- automated tests cover success, not found, permission denied, and unavailable cases

**Risks if done incorrectly**
- later CRUD or migration logic validates against unstable rules
- snapshot content diverges from authoritative model
- UI slices would rely on incorrect lookup semantics

### Slice 3 — Completion/status engine

**Scope**
- Implement `OnboardingStatus` computation from persisted onboarding entities plus validation state
- Implement per-flow and global blocking/warning logic
- Implement dependency evaluation across connection, sources, roots, and bindings

**Explicit exclusions**
- no migration execution
- no new onboarding writes
- no UI routing switch yet
- no old wizard behavior changes

**Dependencies**
- Slice 1 complete
- Slice 2 complete

**Deliverables**
- status computation service
- status read endpoint/contract
- deterministic blocking and warning categories

**Acceptance criteria**
- same persisted state always yields same status result
- missing prerequisites produce blocking state, not inferred completion
- completion is derived only from persisted valid state
- automated tests cover not configured, partial, blocked, and complete outcomes

**Risks if done incorrectly**
- cutover may route users incorrectly
- migration success cannot be judged accurately
- UI may show false completion

### Slice 4 — Migration infrastructure

**Scope**
- Add migration ledger entities and schema
- Add migration issue tracking schema
- Add migration job framework and orchestration interfaces
- Add dry-run/reporting hooks

**Explicit exclusions**
- no legacy-to-new data movement
- no production execution of migration logic
- no UI changes

**Dependencies**
- Slice 1 complete
- Slice 2 complete
- Slice 3 complete

**Deliverables**
- migration run model
- migration unit model
- migration issue model
- job entry point with no-op or stubbed handlers

**Acceptance criteria**
- migration framework can start, record units, and finish without moving business data
- failed unit recording is durable and queryable
- rerunning infrastructure-only job does not alter onboarding entities

**Risks if done incorrectly**
- later migration execution has no trustworthy ledger
- partial failures cannot be diagnosed or resumed safely

### Slice 5 — Migration execution

**Scope**
- Implement legacy-to-new mapping rules from the migration strategy
- Implement idempotent upsert logic
- Implement migration issue creation for invalid/unresolvable legacy records
- Implement unit sequencing:
  - connection
  - projects
  - teams
  - pipelines
  - roots
  - bindings

**Explicit exclusions**
- no startup routing switch
- no enabling of new onboarding UI
- no old wizard cleanup

**Dependencies**
- Slice 1 complete
- Slice 2 complete
- Slice 3 complete
- Slice 4 complete

**Deliverables**
- executable migration job
- idempotent mapping/upsert services
- issue records for unmigratable legacy rows

**Acceptance criteria**
- migration can run against a legacy-populated database without duplicate onboarding entities
- rerunning migration with unchanged source data creates no additional rows
- missing external references create migration issues, not fabricated entities
- migration never writes partial child entities before required parents exist

**Risks if done incorrectly**
- data corruption in new onboarding tables
- duplicate entities
- blocked cutover with unreliable migration state

### Slice 6 — Onboarding backend CRUD

**Scope**
- Implement all write endpoints for new onboarding model:
  - connection create/update/revalidate/delete
  - project source add/update/revalidate/remove
  - team source add/update/revalidate/remove
  - pipeline source add/update/revalidate/remove
  - product root add/update/revalidate/remove
  - binding add/update/revalidate/remove
- Enforce strict validation and idempotency per action

**Explicit exclusions**
- no UI
- no routing cutover
- no legacy compatibility removal

**Dependencies**
- Slice 1 complete
- Slice 2 complete
- Slice 3 complete

**Deliverables**
- final onboarding write API surface
- request validators
- service-layer idempotent write behavior

**Acceptance criteria**
- repeating the same write updates the same persisted entity rather than creating duplicates
- invalid dependencies return structured failure
- write paths use live validation where required
- automated tests cover create, update, duplicate submit, dependency violation, and delete-blocked cases

**Risks if done incorrectly**
- new UI slices write unstable data
- cutover exposes incorrect onboarding behavior

### Slice 7 — UI Flow 1 (TFS Connection)

**Scope**
- Implement new TFS Connection & Validation flow against new backend only
- Render status from new onboarding status/read APIs
- Guard with feature flag

**Explicit exclusions**
- no team/pipeline/product-root UI
- no migration execution changes
- no routing cutover
- no legacy writes

**Dependencies**
- Slice 2 complete
- Slice 3 complete
- Slice 6 complete

**Deliverables**
- connection UI behind flag
- client integration with new APIs only

**Acceptance criteria**
- when flag is off, users cannot access the new connection flow
- when flag is on, all connection actions hit new APIs only
- no client preference or wizard state is required for function
- manual test proves re-entry loads persisted connection state

**Risks if done incorrectly**
- mixed old/new writes
- false impression that redesign works end-to-end before dependent flows exist

### Slice 8 — UI Flow 2 (Data Sources)

**Scope**
- Implement new project/team/pipeline source configuration flow
- Use new lookup, validation, status, and CRUD APIs only
- Keep behind feature flag

**Explicit exclusions**
- no product root/binding UI
- no import path
- no cutover

**Dependencies**
- Slice 2 complete
- Slice 3 complete
- Slice 6 complete
- Slice 7 complete

**Deliverables**
- data source flow UI behind flag
- client integration with new APIs only

**Acceptance criteria**
- project/team/pipeline actions persist immediately through new backend only
- source identities are external-ID-based in all round trips
- re-entry shows persisted sources without any session-created IDs
- automated client/service tests cover successful round-trip and structured failures

**Risks if done incorrectly**
- Flow 3 will lack reliable prerequisites
- UI may regress into session-local state

### Slice 9 — UI Flow 3 (Domain Configuration)

**Scope**
- Implement product root and binding configuration flow
- Use new work-item lookup, validation, status, and CRUD APIs only
- Keep behind feature flag

**Explicit exclusions**
- no import path
- no startup cutover
- no old wizard removal

**Dependencies**
- Slice 2 complete
- Slice 3 complete
- Slice 6 complete
- Slice 8 complete

**Deliverables**
- domain configuration flow UI behind flag
- binding management against new model only

**Acceptance criteria**
- work item selection uses new lookup/validation APIs only
- bindings are created only when required sources and roots exist
- repeating the same binding action does not create duplicates
- manual verification proves re-entry restores persisted roots and bindings

**Risks if done incorrectly**
- completion logic becomes unreliable
- bindings may drift from source/project scope rules

### Slice 10 — Import path

**Scope**
- Implement import validation and import execution into the new onboarding model only
- Integrate import results with status engine and migration issue reporting semantics where applicable

**Explicit exclusions**
- no old import path reuse that writes legacy entities
- no cutover
- no cleanup

**Dependencies**
- Slice 3 complete
- Slice 5 complete
- Slice 6 complete
- Slice 9 complete

**Deliverables**
- import validation service
- import execution service
- import result contract against new model

**Acceptance criteria**
- import writes only new onboarding entities
- import is idempotent for identical input
- invalid imported identities are rejected with structured failure
- import completion does not bypass onboarding status computation

**Risks if done incorrectly**
- import reintroduces dual-write or partial model behavior
- migrated and imported states diverge

### Slice 11 — Cutover preparation

**Scope**
- Switch startup routing authority to `OnboardingStatus`
- Disable old wizard writes
- Redirect or block old wizard entry points
- Enable new onboarding as default path

**Explicit exclusions**
- no deletion of old code yet
- no removal of compatibility endpoints yet
- no cleanup of old tables yet

**Dependencies**
- Slice 3 complete
- Slice 5 complete
- Slice 6 complete
- Slice 7 complete
- Slice 8 complete
- Slice 9 complete
- Slice 10 complete

**Deliverables**
- routing uses server-derived onboarding status
- old wizard no longer writes in cutover environments
- production-safe flag and environment configuration

**Acceptance criteria**
- old wizard write path returns blocked/redirected behavior in cutover environment
- startup no longer depends on client onboarding completion preferences
- migration has completed successfully in target environment before cutover enablement
- observability confirms zero mixed writes

**Risks if done incorrectly**
- users can write to both systems
- startup routing and persisted onboarding status disagree

### Slice 12 — Cleanup

**Scope**
- Remove old wizard UI
- Remove wizard state services
- Remove obsolete onboarding endpoints
- Remove compatibility layer
- Remove obsolete database structures only after verified non-use

**Explicit exclusions**
- no new feature work
- no redesign changes

**Dependencies**
- Slice 11 complete
- zero production traffic on old wizard write paths

**Deliverables**
- old wizard code removed
- old onboarding preferences and wizard-session services removed
- obsolete endpoints removed
- cleanup migrations only for structures proven unused

**Acceptance criteria**
- repository contains no old onboarding write path
- startup does not reference old onboarding preference gating
- old onboarding endpoints receive zero traffic before removal
- any removed database structure has no remaining runtime dependency

**Risks if done incorrectly**
- accidental removal of still-used legacy consumers
- broken startup or settings entry points after cleanup

## 4. Feature Flag Strategy

### 4.1 Flags required

Required flags:

1. `OnboardingRedesignFlow1Enabled`
2. `OnboardingRedesignFlow2Enabled`
3. `OnboardingRedesignFlow3Enabled`
4. `OnboardingRedesignImportEnabled`
5. `OnboardingRedesignCutoverEnabled`

### 4.2 Flag usage by slice

| Slice | Flag required | Purpose |
|---|---|---|
| 1 | No | Backend foundation must always be correct once merged |
| 2 | No | Lookup/validation backend is safe to merge dormant |
| 3 | No | Status engine backend is safe to merge dormant |
| 4 | No | Migration infrastructure is non-user-facing |
| 5 | No runtime user flag; operator execution control required | Migration job must be operator-triggered, not user-triggered |
| 6 | No | Backend CRUD can merge dormant before UI exposure |
| 7 | Yes | Isolate new connection UI |
| 8 | Yes | Isolate new data source UI |
| 9 | Yes | Isolate new domain configuration UI |
| 10 | Yes | Isolate new import path |
| 11 | Yes | Cutover must be explicit and environment-scoped |
| 12 | No | Cleanup happens only after cutover is complete |

### 4.3 Isolation rules

1. Flags are environment-scoped, not per-user within the same write environment.
2. If `OnboardingRedesignCutoverEnabled` is off, old onboarding remains the only writer.
3. If `OnboardingRedesignCutoverEnabled` is on, old onboarding writes must be blocked before new onboarding becomes default.
4. No environment may have both old and new onboarding write paths enabled simultaneously.

### 4.4 Flag removal

Flags are removed only when:

1. Slice 11 is complete in production
2. old wizard write traffic is zero
3. Slice 12 cleanup is ready to remove old code

## 5. Migration Cut Points

### 5.1 Exact moment migration is allowed

Migration is allowed only **after Slice 5 code is merged and deployed, and before Slice 11 cutover is enabled**.

Operationally:
1. Slice 1 deployed
2. Slice 2 deployed
3. Slice 3 deployed
4. Slice 4 deployed
5. Slice 5 deployed
6. schema validated and stable
7. operator starts migration job
8. migration completes successfully
9. only then may Slice 11 cutover be enabled

### 5.2 Preconditions for migration execution

All must be true:

1. final onboarding schema is deployed
2. lookup and validation APIs are deployed and verified
3. onboarding status engine is deployed and verified
4. migration ledger and issue tracking are deployed
5. idempotent upsert logic is deployed
6. old wizard is still the only writer in that environment
7. no pending schema changes for onboarding entities remain

### 5.3 What must not be running during migration

The following must not be running in the target environment during migration:

- new onboarding write UI
- cutover routing to new onboarding as default
- any dual-write bridge
- any schema migration that changes onboarding entities

The old wizard may remain active only because migration reads legacy data from its stable model before cutover. No new-system writes from end users are allowed until migration succeeds.

## 6. Verification Strategy

### 6.1 Slice-by-slice verification

| Slice | Manual verification | Automated tests | Logs / metrics |
|---|---|---|---|
| 1 | Apply and rollback DB migration locally | EF model/migration tests where available | migration apply success/failure |
| 2 | Call each lookup and validation endpoint with valid/invalid inputs | unit/integration tests for lookup, validation, error mapping, snapshot shaping | lookup success/failure counts, validation failure categories |
| 3 | Query status for seeded states and verify expected blocking/completion | unit tests for deterministic status outcomes | status distribution counts by outcome |
| 4 | Start and finish a no-op migration run | tests for ledger, unit recording, failure recording | migration run/unit creation logs |
| 5 | Run migration against representative legacy data set and rerun it | tests for idempotent upserts, issue creation, dependency ordering | migrated entity counts, issue counts, rerun skip/update counts |
| 6 | Exercise each write endpoint with create/update/repeat/failure scenarios | unit/integration tests for validators, idempotency, dependency violations | write success/failure counts by entity and code |
| 7 | Enable flag in non-production and verify connection flow end-to-end | client/service tests for new flow integration | flow 1 endpoint usage, zero old-write calls when flag on |
| 8 | Enable flag in non-production and verify source CRUD end-to-end | client/service tests for projects/teams/pipelines flows | source CRUD counts, validation failures |
| 9 | Enable flag in non-production and verify root/binding CRUD end-to-end | client/service tests for roots/bindings flows | root/binding CRUD counts, duplicate-prevention metrics |
| 10 | Run import with valid/invalid payloads against new model | tests for import validation, idempotency, error mapping | import success/failure counts |
| 11 | Verify startup routing and blocked old wizard writes in cutover env | automated tests for routing authority and blocked old writes | zero mixed writes, old wizard blocked count, onboarding status routing count |
| 12 | Verify old entry points are removed and no callers remain | regression tests for startup/settings surfaces affected by cleanup | zero traffic on removed endpoints before removal |

### 6.2 Binary verification rule

A slice is not complete unless:

1. manual verification passed
2. automated verification passed
3. observability signals exist and match expected behavior

### 6.3 Minimum observability required

Each slice that changes runtime behavior must emit:

- structured success/failure logs
- categorized error codes
- counts for create/update/skip/fail where relevant

## 7. Forbidden Combinations

The following combinations are explicitly not allowed:

1. UI and migration execution in the same slice
2. Migration execution before lookup and validation APIs are complete
3. Migration execution before status engine is complete
4. Cutover before migration success in the target environment
5. Cleanup before zero traffic on old wizard write paths
6. New onboarding UI before new onboarding CRUD APIs are complete
7. New onboarding UI using legacy endpoints as a temporary bridge
8. Partial persistence schema in one slice and identity constraints in a later slice
9. Feature flag that enables new UI while leaving old writes enabled in the same environment
10. Import path writing legacy entities after new onboarding model exists
11. Startup routing switch before `OnboardingStatus` is authoritative
12. Schema change and migration execution in the same deployment window

## 8. Critical Path

### 8.1 Critical-path slices

The critical path is:

1. Slice 1 — Persistence foundation
2. Slice 2 — Lookup & validation APIs
3. Slice 3 — Completion/status engine
4. Slice 4 — Migration infrastructure
5. Slice 5 — Migration execution
6. Slice 6 — Onboarding backend CRUD
7. Slice 7 — UI Flow 1
8. Slice 8 — UI Flow 2
9. Slice 9 — UI Flow 3
10. Slice 10 — Import path
11. Slice 11 — Cutover preparation
12. Slice 12 — Cleanup

### 8.2 Limited parallelization allowed

Parallelization is allowed only where it does not violate dependencies:

1. Slice 4 may start after Slices 1-3 are stable and may proceed in parallel with early Slice 6 work.
2. Slice 6 may proceed after Slices 1-3 without waiting for Slice 5.
3. Slice 7 may begin after Slice 6 is complete while Slice 5 is being finalized, but it may not be enabled in any environment that has not completed migration prerequisites.

### 8.3 Parallelization not allowed

The following are not parallelizable:

- Slice 5 and Slice 11
- Slice 11 and Slice 12
- Slice 8 before Slice 7
- Slice 9 before Slice 8
- Slice 10 before Slice 5

### 8.4 Execution rule

If there is any doubt whether a candidate parallel step would introduce mixed writes, hidden dependencies, or unstable migration conditions, it is not allowed and must stay sequential.
