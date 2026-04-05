# Onboarding Validation Strategy

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-migration-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-implementation-slices.md`

This document defines the system validation strategy for the onboarding redesign and migration. It does not change any prior design, migration, or slicing decisions.

## 1. Validation Layers

### 1.1 Domain validation

**Purpose**
- Protect entity correctness inside the authoritative persisted onboarding model.

**What is validated**
- identity rules
- required fields
- parent/child relationship rules
- binding scope rules
- validation-state shape
- snapshot shape and metadata presence
- deletion blocking rules

**When it runs**
- before persistence on every create, update, revalidate, delete, import, and migration upsert
- after hydration for consistency checks in status computation

**What happens on failure**
- write is rejected
- persistence is not attempted
- structured failure is returned or migration issue is recorded
- failure is logged as a domain validation failure

### 1.2 API validation

**Purpose**
- Ensure request and response contracts are structurally correct and deterministic.

**What is validated**
- request shape
- required request fields
- enum values
- malformed identifiers
- unsupported combinations
- response envelope completeness
- HTTP status to error-code mapping

**When it runs**
- at request boundary before domain or integration execution
- at response assembly before payload is returned

**What happens on failure**
- request is rejected with structured error envelope
- no domain mutation is attempted
- malformed responses are treated as server faults and block release

### 1.3 Integration validation

**Purpose**
- Verify live TFS authority for all operations that depend on external truth.

**What is validated**
- external ID existence
- permission sufficiency
- capability sufficiency
- availability
- parent/child association from TFS
- snapshot fields returned from live TFS

**When it runs**
- synchronously on write operations that require live confirmation
- during explicit revalidation
- during migration resolution and post-migration reconciliation

**What happens on failure**
- hard reject if live confirmation is required for the attempted action
- persist warning state only when the operation is a revalidation of already persisted data and no structural mutation is being requested
- categorized failure is logged and counted

### 1.4 Migration validation

**Purpose**
- Guarantee legacy-to-new correctness, idempotency, and recoverability.

**What is validated**
- legacy source health
- external-ID resolvability
- ordering of migration units
- parent existence before child upsert
- target identity uniqueness
- issue creation for skipped/unmigratable records
- rerun idempotency

**When it runs**
- before migration starts
- for every migration unit during execution
- after migration completes

**What happens on failure**
- unit stops or job stops according to severity rules in Section 4
- issue or run failure is recorded durably
- cutover remains blocked

### 1.5 System validation

**Purpose**
- Verify that end-to-end onboarding behavior matches persisted reality and slice intent.

**What is validated**
- status derivation correctness
- no dependency on wizard/session state
- no success without persistence
- no completion without valid persisted state
- no mixed write paths
- no startup routing based on client preferences after cutover

**When it runs**
- after each implementation slice
- before migration
- before cutover
- continuously after cutover through monitoring

**What happens on failure**
- current slice is incomplete
- cutover is blocked
- production rollout is halted or rolled back at feature-flag level

### 1.6 Observability validation

**Purpose**
- Ensure the system can prove correctness and detect drift.

**What is validated**
- required logs exist
- required metrics emit
- alert thresholds are wired
- success/failure categorization is consistent
- status distributions and error trends are queryable

**When it runs**
- during slice verification
- during migration dry runs and live runs
- continuously in pilot and production

**What happens on failure**
- release gate fails for the affected slice
- cutover remains blocked until observability gaps are fixed

## 2. System Invariants

### Invariant 1 — No duplicate entities by authoritative identity

**Definition**
- There is at most one `ProjectSource` per `projectExternalId`, one `TeamSource` per `teamExternalId`, one `PipelineSource` per `pipelineExternalId`, one `ProductRoot` per `workItemExternalId`, and one `ProductSourceBinding` per composite key.

**Enforcement**
- unique database constraints
- idempotent upsert rules
- conflict checks before write

**Detection**
- constraint violations
- duplicate-identity audit query
- duplicate write-failure metric

### Invariant 2 — No binding exists without valid endpoints

**Definition**
- A `ProductSourceBinding` may exist only if its `ProductRoot` exists and the referenced source entity exists and matches required scope rules.

**Enforcement**
- synchronous dependency validation on write and migration
- parent-first migration ordering
- delete blocking for still-referenced endpoints

**Detection**
- orphan-binding audit query
- dependency-violation errors
- status engine blocker output

### Invariant 3 — No entity is treated as valid without validation evidence

**Definition**
- An onboarding entity cannot contribute to completion unless it has required persisted validation state and that state is acceptable for the relevant operation.

**Enforcement**
- status engine uses persisted validation state
- write paths require live validation where mandated
- `SnapshotOnly` never authorizes create or update

**Detection**
- status audit showing entity counted complete without valid state
- consistency alert between entity count and valid-entity count

### Invariant 4 — Completion equals persisted reality

**Definition**
- Global or per-flow completion may be reported only when persisted entities and their validation states satisfy the redesign rules.

**Enforcement**
- server-side `OnboardingStatus` only
- no client preference gating after cutover
- status computation tests for all blocking paths

**Detection**
- route/status comparison checks
- completion-with-missing-prerequisite audit query
- cutover validation gate

### Invariant 5 — No onboarding action succeeds without persistence

**Definition**
- A successful mutating onboarding response means the intended persisted state exists after the transaction completes.

**Enforcement**
- success responses emitted only after durable save
- no optimistic success before commit
- no client-side completion state

**Detection**
- write-success log missing persisted identity is a blocker
- post-write verification query mismatch
- success/failure correlation audit

### Invariant 6 — No onboarding status derives from client state

**Definition**
- Status, readiness, and completion use persisted onboarding data only.

**Enforcement**
- server status endpoint is authoritative
- old preference-based gating removed at cutover
- no backend contract for wizard/session state

**Detection**
- code-path review at slice boundaries
- cutover gate checks for old preference usage
- unexpected status changes without persisted data changes

### Invariant 7 — No mixed write systems in one environment

**Definition**
- At any given time in one environment, either the legacy wizard or the redesign is the only onboarding writer.

**Enforcement**
- environment-scoped feature flags
- cutover blocks old wizard writes before enabling new writes
- no dual-write bridge

**Detection**
- endpoint traffic split audit
- old-write count after cutover alert
- environment configuration gate

### Invariant 8 — Migration never creates inferred identities

**Definition**
- Migration may upsert only records with resolved authoritative external identities.

**Enforcement**
- live or authoritative resolution required before target creation
- unresolved rows become migration issues

**Detection**
- migration issue counts
- target rows missing authoritative external IDs
- post-migration reconciliation reports

### Invariant 9 — Parent existence precedes child persistence

**Definition**
- Projects exist before teams and pipelines; roots and sources exist before bindings.

**Enforcement**
- ordered write logic
- ordered migration units
- dependency validation in CRUD and migration

**Detection**
- foreign-key/dependency violations
- orphan-child audit query
- migration unit failure logs

### Invariant 10 — Sanitized failures only

**Definition**
- Errors, validation outputs, and migration issues contain no secrets or raw credentials.

**Enforcement**
- sanitized error contract
- no logging of PATs or raw auth details

**Detection**
- log scanning for forbidden fields
- response contract review
- security audit checks

## 3. Write-Time Validation

### 3.1 Common synchronous validation for every write

Every write operation must synchronously validate:

1. request shape and required fields
2. identity format and authoritative identifier presence
3. duplicate identity or singleton conflict
4. parent/child dependency rules
5. live TFS confirmation when the action introduces or changes a TFS-backed identity
6. scope compatibility for bindings and child sources
7. delete blockers for referenced entities

### 3.2 External lookups required per write type

| Operation | Required external lookup |
|---|---|
| Connection create/update | connection availability, permission, capability |
| ProjectSource add/update | live project lookup by external ID |
| TeamSource add/update | live team lookup plus project association confirmation |
| PipelineSource add/update | live pipeline lookup plus project association confirmation |
| ProductRoot add/update | live work item lookup plus required fields confirmation |
| Binding add/update | live validation of root and selected source relationship/scope when required by current state |
| Revalidate any entity | fresh live validation for that entity |
| Delete | no live lookup required unless delete safety depends on current authority refresh; dependency checks are mandatory |

### 3.3 Dependency checks required per write type

| Operation | Required dependency checks |
|---|---|
| Connection delete | no sources, roots, or bindings remain |
| TeamSource add/update | parent `ProjectSource` exists and is valid enough to allow child configuration |
| PipelineSource add/update | parent `ProjectSource` exists and is valid enough to allow child configuration |
| Binding add/update (`Project`) | root exists; project source exists |
| Binding add/update (`Team`) | root exists; team exists; team project scope matches binding/root rules |
| Binding add/update (`Pipeline`) | root exists; pipeline exists; pipeline project scope matches binding/root rules |
| Any delete of source/root | no dependent bindings remain unless operation explicitly removes dependents in the same validated unit, which onboarding redesign does not allow |

### 3.4 Hard failures

Hard failures reject the write and persist nothing for the requested mutation.

Hard-failure cases:
- malformed request
- missing required fields
- duplicate identity conflict
- external entity not found
- TFS unavailable for an operation that requires live confirmation
- permission denied
- capability denied
- dependency violation
- scope mismatch
- delete blocked by dependents

**Failure response**
- structured error envelope
- stable error code
- sanitized message
- retryable flag set correctly
- no success payload

### 3.5 Soft warnings

Soft warnings may be persisted only when the write does not create or mutate authoritative identity and the persisted structure remains valid.

Allowed warning cases:
- snapshot stale but entity structure unchanged
- rename detected on revalidation
- source currently unavailable during non-mutating revalidation of already persisted data

Not allowed as soft warnings:
- missing external identity
- dependency violation
- permission denied on create/update
- not found on create/update

**Persist behavior**
- keep existing valid persisted entity
- update warning codes and validation state where allowed
- do not report completion if warning state blocks completeness

### 3.6 Operation outcome rules

1. Every successful write returns authoritative persisted identity.
2. Every idempotent repeat returns the same authoritative identity and final state.
3. Every rejected write leaves persisted state unchanged for the requested mutation.
4. Every accepted write updates status-relevant persisted state before success is returned.

## 4. Migration Validation

### 4.1 Pre-migration validation

Pre-migration validation must check:

1. target onboarding schema version is deployed and final
2. migration ledger schema exists
3. lookup and validation services are available
4. legacy source tables are readable
5. legacy rows required for identity resolution are present
6. no new onboarding UI write path is enabled in the environment
7. no in-flight schema change on onboarding tables exists

**Pre-migration outputs**
- ready / blocked decision
- counts of resolvable vs unresolved legacy records
- predicted blocker issue count

**Pre-migration failure handling**
- if schema, lookup services, or ledger are not ready: stop migration
- if unresolved legacy rows exist: migration may start only if those rows can be isolated into issues without breaking referential completeness for migrated rows

### 4.2 In-migration validation per unit

Every migration unit must validate:

1. input legacy rows for that unit are readable
2. external IDs resolve before target upsert
3. parent prerequisites already exist in target model
4. upsert target identity is unique
5. resulting target snapshot and validation state are complete
6. skipped rows are recorded as issues with stable reason codes

**Unit stop conditions**
- target schema mismatch
- ledger write failure
- duplicate identity not explainable as idempotent rerun
- unresolved required parent for the unit
- inability to persist issue records

**Unit continue-with-issues conditions**
- individual legacy row lacks resolvable external ID
- individual legacy row references missing external entity
- individual legacy row fails live validation but failure can be recorded and other units remain structurally sound

### 4.3 Post-migration validation

Post-migration validation must verify:

1. every migrated target row has authoritative identity
2. no duplicate target identities exist
3. no orphan bindings exist
4. no child source exists without parent project
5. onboarding status computation succeeds on migrated data
6. migrated counts reconcile with resolvable legacy counts
7. blocker migration issues are fully enumerated
8. rerun produces no net-new duplicates

### 4.4 When migration must stop

Migration must stop immediately if any of the following occurs:

- target schema not at required version
- migration ledger unavailable
- duplicate target identities created or detected
- parent-first ordering is violated
- issue recording is unavailable
- status engine cannot evaluate migrated state

### 4.5 When migration may continue with issues

Migration may continue only if:

- failing records are isolated to explicit migration issues
- continued processing cannot create orphaned or partial target state
- blocker issues are visible and cutover remains blocked

### 4.6 Detection of skipped or invalid records

Skipped or invalid records must emit:

- migration issue record
- source row identifier
- failure category
- sanitized evidence
- target unit name
- blocker vs non-blocker severity

## 5. Cross-Slice Validation

### 5.1 Revalidation required after each slice

| Slice | Mandatory revalidation |
|---|---|
| 1 | schema validity, uniqueness constraints, parent/child model rules |
| 2 | all lookup/validation contracts, sanitized failures, snapshot shaping |
| 3 | status invariants, completion derivation, blocker/warning determinism |
| 4 | ledger durability, issue recording, no-op execution integrity |
| 5 | migration idempotency, parent ordering, issue completeness, post-migration invariants |
| 6 | write-time hard failures, idempotent success, dependency enforcement, status update after write |
| 7 | no client-state authority, no legacy writes from Flow 1, persisted re-entry |
| 8 | no client-state authority, no legacy writes from Flow 2, dependency blocking from Flow 1 |
| 9 | no client-state authority, binding scope enforcement, completion correctness |
| 10 | import idempotency, import-to-new-model-only, status recomputation |
| 11 | no dual-write, server routing authority, migration-complete gating |
| 12 | no legacy endpoints or wizard state remain, no hidden dependency on removed code |

### 5.2 Invariants rechecked after every slice

These invariants must be rechecked after every slice from Slice 1 onward:

1. duplicate identity prevention
2. parent-before-child correctness
3. no status from client state
4. no success without persistence for any new write path introduced so far
5. no mixed writes for any environment configuration introduced so far

### 5.3 Slice-boundary verification rule

A slice boundary is valid only if:

1. no later-slice behavior is assumed by the current slice
2. all introduced operations are manually testable
3. all introduced operations emit required logs and metrics
4. acceptance criteria from the implementation-slices report are met without exceptions

## 6. Cutover Validation

### 6.1 Hard gate checks

All hard gates must pass before enabling cutover:

1. migration completed in the target environment
2. zero blocker migration issues remain
3. post-migration invariant audit passes
4. `OnboardingStatus` matches migrated persisted state for sampled and audited scenarios
5. old wizard write path is blocked in that environment
6. no dual-write path exists in code or configuration
7. new UI flows 1-3 and import path are operational against new backend only
8. startup routing reads server-derived onboarding status only
9. required logs, metrics, and alerts are live

### 6.2 Soft signals

Soft signals may warn but not automatically block cutover if hard gates pass:

- non-blocking warning counts above baseline
- elevated but stable revalidation warnings caused by known stale snapshots
- low-volume lookup transient failures below defined availability threshold

### 6.3 Cutover must fail closed

If any hard gate fails:

- cutover flag remains off
- new onboarding does not become default
- old wizard write path remains sole writer until the issue is resolved

## 7. Post-Cutover Validation

### 7.1 Continuous monitoring requirements

Continuously monitor:

1. onboarding status distribution
2. write success and failure rates by entity type
3. lookup and validation failure rates by category
4. migration rerun attempts
5. legacy endpoint traffic
6. duplicate conflict rates
7. status inconsistency detections

### 7.2 Drift and corruption detection

Detect drift through:

- scheduled duplicate-identity audits
- orphan-binding audits
- status-vs-persisted-state reconciliation checks
- snapshot/validation-state anomaly reporting

### 7.3 Unexpected legacy usage detection

Detect unexpected legacy usage through:

- old wizard route hits
- old onboarding write endpoint hits
- client preference reads for onboarding completion after cutover

Any non-zero sustained legacy write attempt after cutover is a production incident.

## 8. Observability

### 8.1 Required logs

Required structured logs:

1. onboarding write attempted
2. onboarding write succeeded
3. onboarding write rejected
4. onboarding revalidation completed
5. onboarding status computed
6. migration run started
7. migration unit started
8. migration unit succeeded
9. migration unit failed
10. migration issue recorded
11. cutover gate evaluated
12. old wizard write blocked after cutover

Each log must include:
- operation type
- entity type
- authoritative identity or unit identifier where applicable
- outcome
- stable error or warning code if not fully successful
- environment and timestamp

### 8.2 Required metrics

Required metrics:

- write attempts count by operation/entity
- write success count by operation/entity
- write failure count by error code
- revalidation count by entity/outcome
- lookup failure rate by failure category
- onboarding status distribution
- blocker warning count distribution
- migration unit success/failure counts
- migration issue count by severity and category
- blocked old-wizard write count after cutover
- duplicate conflict count
- orphan-binding audit count

### 8.3 Required alerts

Required alerts:

1. duplicate identity detected
2. orphan binding detected
3. old wizard writes attempted after cutover
4. onboarding marked complete while blocker count is non-zero
5. migration job failed
6. migration blocker issues present when cutover flag is enabled
7. sustained TFS availability failure above threshold
8. write failure rate above threshold for any onboarding entity

## 9. Failure Handling

### 9.1 Write failure

**Immediate behavior**
- reject the write
- persist nothing for the attempted mutation
- return structured failure

**Recovery options**
- retry only if failure is retryable
- correct request or prerequisite state, then resubmit
- use revalidation when the issue is stale validation state only

**System behavior**
- block the operation; do not degrade into partial persistence

### 9.2 Validation failure

**Immediate behavior**
- classify as hard failure or allowed warning according to Section 3
- record failure or warning with stable code

**Recovery options**
- restore TFS availability
- correct permissions
- correct dependency state
- rerun validation

**System behavior**
- block create/update on hard failures
- degrade only for non-mutating revalidation warnings

### 9.3 Migration failure

**Immediate behavior**
- stop the affected unit or whole job according to stop rules
- record run failure and issue details
- keep cutover blocked

**Recovery options**
- fix schema/configuration/legacy data
- rerun the failed unit or the full job using idempotent upserts

**System behavior**
- degrade to legacy writer only until migration is healthy

### 9.4 Invariant violation

**Immediate behavior**
- treat as blocker severity
- stop cutover or trigger incident if post-cutover
- prevent further writes if continuing would deepen corruption

**Recovery options**
- isolate environment
- correct corrupted rows using authoritative data and rerun validations
- re-audit all affected invariants before resuming

**System behavior**
- block release or, in production, fail closed around the affected onboarding path

## 10. Must-Never-Break Guarantees

### Guarantee 1 — Duplicate external identities must never exist in production

**Safeguards**
- unique constraints
- idempotent upserts
- duplicate conflict handling

**Detection**
- duplicate alerts
- reconciliation audits

### Guarantee 2 — Successful writes without validation must never occur

**Safeguards**
- synchronous write-time validation
- live confirmation for TFS-backed mutations
- no success before persistence

**Detection**
- success log without validation evidence is a blocker
- audit of success responses vs validation-state updates

### Guarantee 3 — Onboarding must never be marked complete without valid configuration

**Safeguards**
- server-only status engine
- persisted validation-state requirements
- blocker/warning rules

**Detection**
- completion-with-blocker alert
- status reconciliation audit

### Guarantee 4 — Client or wizard state must never control onboarding status

**Safeguards**
- no backend wizard/session state
- client preference gating removed at cutover
- routing uses `OnboardingStatus`

**Detection**
- legacy preference usage telemetry
- code-path verification at cutover

### Guarantee 5 — Mixed writes between old and new systems must never occur

**Safeguards**
- environment-scoped cutover flag
- old writer blocked before new writer becomes authoritative
- no dual-write bridge

**Detection**
- concurrent old/new write telemetry
- blocked old-write alert

### Guarantee 6 — Migration must never run on unstable schema

**Safeguards**
- pre-migration schema gate
- operator-triggered execution only
- cutover blocked until migration validation passes

**Detection**
- schema-version check failure
- migration start denied log

### Guarantee 7 — Partial persistence must never be reported as success

**Safeguards**
- transaction-bound success semantics
- dependency validation before save
- no finish-button completion model

**Detection**
- persisted-state verification mismatch
- success-with-missing-row audit
