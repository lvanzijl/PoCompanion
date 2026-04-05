# Onboarding Reconciliation Strategy

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-migration-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-implementation-slices.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-validation-strategy.md`

This document defines the reconciliation and drift detection strategy for the onboarding system. It does not change any prior redesign, validation, migration, or slicing decisions.

## 1. Scope

Reconciliation compares authoritative persisted onboarding configuration with authoritative live TFS state. It never invents entities, never repairs relationships silently, and never bypasses validation.

### 1.1 TfsConnection

**Valid relative to TFS**
- the configured organization URL resolves to the intended TFS endpoint
- authentication succeeds
- required onboarding capabilities are available
- required permissions remain available

**What can drift**
- endpoint availability
- authentication validity
- permission scope
- capability availability
- API reachability or timeout characteristics

### 1.2 ProjectSource

**Valid relative to TFS**
- `projectExternalId` exists in TFS
- the project is reachable through the configured connection
- persisted snapshot still matches the same project identity

**What can drift**
- project deletion
- project rename or description change
- project inaccessibility due to permission loss
- stale snapshot metadata

### 1.3 TeamSource

**Valid relative to TFS**
- `teamExternalId` exists in TFS
- the team still belongs to the persisted `projectExternalId`
- the team is reachable through the configured connection

**What can drift**
- team deletion
- team moved or re-scoped away from the expected project
- team rename or area-path change
- permission loss
- stale snapshot metadata

### 1.4 PipelineSource

**Valid relative to TFS**
- `pipelineExternalId` exists in TFS
- the pipeline still belongs to the persisted `projectExternalId`
- required pipeline metadata remains readable

**What can drift**
- pipeline deletion
- pipeline moved to another project or no longer resolvable in the expected scope
- pipeline rename, folder change, YAML path change, or repository metadata change
- permission loss
- stale snapshot metadata

### 1.5 ProductRoot

**Valid relative to TFS**
- `workItemExternalId` exists in TFS
- the work item remains readable
- required work-item metadata remains available for status and binding evaluation

**What can drift**
- work item deletion
- work item no longer readable
- work-item type, title, state, project, or area-path change
- permission loss
- stale snapshot metadata

### 1.6 ProductSourceBinding

**Valid relative to TFS**
- both persisted endpoints still exist
- the binding still represents a valid relationship under current scope rules
- for `Team` and `Pipeline` bindings, project scope remains compatible

**What can drift**
- one endpoint deleted or inaccessible
- persisted project scope no longer matches endpoint reality
- source entity remains valid individually but the relationship is no longer valid
- persisted binding points at a structurally stale source after external movement

## 2. Drift Types

### 2.1 Hard drift

**Definition**
- drift that makes the persisted entity unusable as valid onboarding configuration

**Examples**
- entity no longer exists in TFS
- endpoint is permanently not found
- required TFS capability is gone

**Severity**
- blocking

**Expected system behavior**
- mark affected entity invalid
- degrade onboarding status to blocking for any dependent flow
- require user action before configuration is considered complete

### 2.2 Soft drift

**Definition**
- drift where identity and structural validity remain intact, but display or secondary metadata changed

**Examples**
- project renamed
- team area path changed while team identity still exists
- pipeline folder or YAML path changed
- work item title changed

**Severity**
- non-blocking warning unless downstream rules explicitly require the changed field

**Expected system behavior**
- keep configuration usable
- persist updated snapshot only after validated reconciliation
- surface warning or refresh event visibly

### 2.3 Structural drift

**Definition**
- drift that invalidates the relationship model even if one or more entities still exist individually

**Examples**
- binding points to a team now outside the expected project scope
- binding references a source entity that still exists but no longer matches the persisted relationship rules
- child source no longer belongs to its persisted parent project

**Severity**
- blocking

**Expected system behavior**
- mark the affected binding or source invalid
- do not auto-rebind
- require user correction

### 2.4 Permission drift

**Definition**
- drift caused by loss or reduction of TFS access for a previously valid configuration

**Examples**
- project or pipeline becomes unauthorized
- connection authentication now fails
- work item exists but is no longer readable

**Severity**
- blocking for writes and validation-dependent reads; warning only for historical snapshot display

**Expected system behavior**
- block writes requiring live authority
- retain last confirmed snapshot for display
- mark validation state as `PermissionDenied` or `Unavailable` as appropriate

## 3. Detection Mechanisms

### 3.1 On-demand validation

**When it runs**
- user-triggered revalidate actions
- API-triggered explicit validation requests
- pre-write validation for mutating onboarding operations

**Entities covered**
- all onboarding entities

**Performance constraints**
- must validate only the requested entity and its required dependencies
- must not fan out across the full onboarding graph
- must return categorized results within normal interactive request budgets

### 3.2 Scheduled reconciliation jobs

**When it runs**
- background, operator-configured schedule
- after cutover and during pilot rings
- outside user request flow

**Entities covered**
- `TfsConnection`
- all enabled `ProjectSource`
- all enabled `TeamSource`
- all enabled `PipelineSource`
- all enabled `ProductRoot`
- all enabled `ProductSourceBinding`

**Performance constraints**
- process in bounded batches
- validate entities independently by type and dependency order
- never mutate persisted state without a completed validation result
- never scan TFS without emitting run-level observability

### 3.3 Lazy validation on usage

**When it runs**
- when a runtime action depends on a specific entity and its last validation result is older than the freshness threshold
- when a dependent feature tries to use a pipeline, team, project, or root that has stale validation

**Entities covered**
- `PipelineSource`
- `TeamSource`
- `ProjectSource`
- `ProductRoot`
- `ProductSourceBinding`

**Performance constraints**
- limited to the directly used entity and required relationship checks
- must not degrade into full-environment reconciliation
- stale usage validation must not hide failure; the caller must receive categorized results

## 4. Reconciliation Frequency

### 4.1 Default schedule

- full enabled-entity reconciliation: once every 24 hours
- connection health reconciliation: every 1 hour

### 4.2 High-risk entity frequency

- `PipelineSource`: every 6 hours because pipelines are more likely to move, rename, or lose access
- `ProductRoot`: every 6 hours because work item state and scope can change frequently
- `TeamSource`: every 12 hours
- `ProjectSource`: every 24 hours
- `ProductSourceBinding`: every 6 hours, after endpoint reconciliation for the same run

### 4.3 Manual triggers

Manual reconciliation must support:
- per-entity revalidate
- per-flow revalidate
- full onboarding reconciliation run
- operator-triggered rerun after TFS outage recovery

Manual triggers must be logged and attributed.

## 5. Drift Handling

### 5.1 Hard drift handling

**System behavior**
- block usage that requires the drifted entity
- mark entity invalid
- mark dependent onboarding flow blocked

**Status impact**
- onboarding completion degrades from complete to partial or blocked

**User action**
- required

**When to require reconfiguration**
- entity no longer exists
- identity cannot be validated
- capability or permission no longer satisfies onboarding requirements

### 5.2 Soft drift handling

**System behavior**
- allow continued usage
- persist refreshed snapshot only after successful validation
- record visible warning or refresh event

**Status impact**
- warning state only; completion remains valid if all blocking checks still pass

**User action**
- optional unless the changed metadata creates a business decision the user must confirm

### 5.3 Structural drift handling

**System behavior**
- block the affected binding or child entity
- keep unrelated valid entities available
- do not auto-rebind

**Status impact**
- dependent flow becomes blocked
- global onboarding status includes explicit blocking reason

**User action**
- required

### 5.4 Permission drift handling

**System behavior**
- block writes and live-confirmation actions
- allow snapshot display with non-authoritative labeling

**Status impact**
- completion degrades if the entity is required for a completed flow

**User action**
- required to restore permission or replace configuration

### 5.5 Invalid configuration marking rules

Mark configuration invalid when:
- TFS reports `NotFound`
- TFS reports `PermissionDenied`
- TFS reports required relationship mismatch
- the connection fails required capability validation

Allow continued usage with warnings only when:
- identity still resolves
- relationship still validates
- drift is limited to metadata refresh or snapshot staleness

Require reconfiguration when:
- hard drift or structural drift affects a configured entity
- permission drift persists beyond retry policy
- repeated reconciliation shows the same blocking drift

## 6. Onboarding Status Interaction

### 6.1 Status computation rules

`OnboardingStatus` must consume persisted reconciliation outcomes. It must not infer health from client state or recent UI behavior.

### 6.2 Completion interaction

- hard drift on any required completed entity removes completed status for the affected flow
- structural drift on any binding removes completed status for Domain Configuration
- permission drift on the connection removes completed status for all live-dependent flows
- soft drift does not remove completion when structural validity remains intact

### 6.3 Blocking vs warning examples

- missing pipeline: blocking
- missing project: blocking
- missing product root: blocking
- renamed project: warning
- renamed work item title: warning
- pipeline folder change with same identity: warning
- binding scope mismatch after team moved projects: blocking

## 7. User Visibility

### 7.1 UI surfaces

Drift must be surfaced in:
- onboarding dashboard summary
- entity detail rows in each onboarding flow
- binding detail rows in Domain Configuration
- operator-visible reconciliation history surface

### 7.2 Detail level

Each visible drift item must show:
- affected entity type
- authoritative identity
- drift category
- current severity
- last validated time
- sanitized explanation
- required action, if any

### 7.3 Required action presentation

- blocking drift must show an explicit corrective action path
- warnings must show why the system did not block
- repeated unresolved drift must remain visible until reconciliation confirms resolution

No silent failures are allowed. If the system detects drift, the user-facing onboarding surfaces must be able to show it.

## 8. Observability

### 8.1 Required logs

Every reconciliation run must log:
- run started
- run completed
- run failed
- entity validated
- drift detected
- drift resolved
- snapshot refreshed
- correction rejected because user action is required

Each log must include:
- run identifier
- trigger type (`Scheduled`, `Manual`, `Lazy`, `OnDemand`)
- entity type
- entity identity
- drift category if detected
- severity
- sanitized outcome
- timestamp

### 8.2 Required metrics

Required metrics:
- reconciliation run count by trigger type
- reconciliation run duration
- drift count by entity type and drift category
- blocking drift count
- warning drift count
- affected-entity count per run
- unresolved drift age distribution
- drift resolution rate
- repeated validation failure count

### 8.3 Required alerts

Required alerts:
- sudden spike in blocking drift
- sudden spike in permission drift
- repeated reconciliation run failures
- unresolved blocking drift older than threshold
- repeated authentication failures on `TfsConnection`
- sudden increase in structural drift on bindings

## 9. Correction Strategy

### 9.1 Allowed automatic correction

The only automatic correction allowed is validated snapshot refresh for the same authoritative external identity, including:
- display name changes
- description changes
- folder/path metadata changes
- repository metadata refresh
- work item title/state metadata refresh

Automatic correction rules:
- live validation must succeed first
- identity must remain the same
- the update must be logged
- the update must be visible in reconciliation history

### 9.2 Corrections that always require user action

These must always require user action:
- missing entity replacement
- rebinding a `ProductSourceBinding`
- replacing a project, team, pipeline, or work item identity
- restoring permission by changing credentials or scopes
- choosing a different source because the original was removed

### 9.3 Explicitly forbidden corrections

The system must never:
- auto-create onboarding entities
- auto-create TFS entities
- auto-rebind relationships
- silently downgrade blocking drift to warning
- silently repair invalid configuration

## 10. Failure Scenarios

### 10.1 TFS unavailable

**System behavior**
- mark affected validations `Unavailable`
- keep last confirmed snapshot readable
- block writes requiring live authority

**Retry strategy**
- retry only through bounded scheduled reruns and explicit manual reruns
- do not loop indefinitely inside one reconciliation run

**Impact on onboarding status**
- completed status degrades only if freshness rules require current live validation for the affected entity or if the connection itself becomes unavailable
- otherwise warnings may remain until the next successful reconciliation

### 10.2 Partial validation succeeds

**System behavior**
- commit validation outcomes only for entities actually validated
- leave untouched entities unchanged
- record partial-run outcome explicitly

**Retry strategy**
- rerun failed entities in a later bounded pass
- do not mark unvalidated entities as resolved

**Impact on onboarding status**
- status uses the most recent persisted validation result per entity
- unresolved blocking drift remains blocking

### 10.3 Authentication fails

**System behavior**
- mark `TfsConnection` invalid
- block all live-dependent validation and write actions
- cascade warning or blocking visibility to dependent entities without fabricating per-entity success

**Retry strategy**
- bounded retries only after credential or endpoint correction
- operator or user must revalidate explicitly after correction

**Impact on onboarding status**
- Flow 1 becomes blocked
- dependent flows become blocked because live authority is unavailable

## 11. No-Silent-Drift Guarantees

The system provides these guarantees:

1. drift cannot be corrected without a validation result
2. drift cannot be corrected silently; every correction emits logs and visible reconciliation history
3. blocking drift cannot be hidden from `OnboardingStatus`
4. stale snapshots cannot authorize create or update operations
5. relationship drift cannot be auto-repaired
6. repeated reconciliation failures remain visible through logs, metrics, and alerts

Violations are detected by:
- reconciliation history gaps
- correction events without validation records
- status showing complete while blocking drift exists
- alerts for repeated failures or unresolved blocking drift
