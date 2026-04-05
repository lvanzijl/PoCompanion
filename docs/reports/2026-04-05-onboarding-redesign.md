# Onboarding Redesign

Basis for this redesign:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`

Fixed decisions applied in this design:
- Immediate persistence only; no draft state
- Onboarding edits existing system state idempotently
- Three flows only:
  1. TFS Connection & Validation
  2. Data Source Setup
  3. Domain Configuration
- Import is a separate entry path, not a flow step
- Identity is based on external TFS IDs
- Lookup is hybrid: live data plus cached snapshots
- Multi-item configuration is explicit list CRUD
- Validation is strict per action
- Completion is derived from minimal valid persisted state; no finish button
- Permissions are explicit state
- No offline creation when TFS is unavailable

## 1. Architecture Overview

The onboarding system is a configuration workspace, not a wizard.

It operates on persisted onboarding state only. There is no authoritative wizard/session progression state. The current audit and gap reports show that the existing flow fails because progression, persistence, and completion are tied to transient session state, mixed-responsibility screens, and explicit save behavior. The redesign removes those failure modes by making persisted system state the only authority.

### 1.1 Operating model

1. On onboarding entry, the system loads persisted onboarding entities and derived onboarding status.
2. The UI renders three independent flows:
   - TFS Connection & Validation
   - Data Source Setup
   - Domain Configuration
3. Each user action is an immediate, validated, atomic write against persisted state or a read-only lookup.
4. Completion is computed from persisted state after every successful write and after every validation refresh.
5. Re-entry into onboarding reopens the same persisted configuration; it never starts a new onboarding session.

### 1.2 Authority boundaries

- **Persisted system state** is the only source of truth.
- **Derived onboarding state** is computed from persisted state plus latest validation results.
- **Transient UI state** exists only to support interaction during one user action and is discarded after the action completes or is cancelled.

### 1.3 Flow dependency model

- Flow 1 is the prerequisite for all live TFS-dependent actions.
- Flow 2 and Flow 3 are freely navigable; they are not ordered by “next/back”.
- Flow 3 actions are blocked until the required Flow 1 and Flow 2 prerequisites exist in persisted valid state.
- Teams and pipelines depend on a configured project source.
- Domain bindings depend on configured source entities and valid external work items.

### 1.4 Idempotent update model

- Every persisted onboarding entity is matched by stable identity.
- External entities use external TFS IDs as identity.
- Repeating the same successful action updates the same persisted entity instead of creating a duplicate.
- Omission of an entity from the current UI view does not delete it.

### 1.5 Lookup model

- Cached snapshots are used for browsing context, prefill, rename detection, and stale-state display.
- Live TFS data is authoritative for all create, update, confirm, and validate actions.
- If live TFS is unavailable, create and update actions that require TFS validation are blocked.

## 2. State Model

## 2.1 Persisted state

Persisted state is authoritative and must survive refresh, navigation, re-entry, and restart.

### A. Connection state
- **TfsConnection**
  - Logical singleton for the configured TFS connection
  - Fields:
    - Organization URL
    - Connection mode metadata
    - Last successful validation timestamp
    - Last attempted validation timestamp
    - Capability result
    - Permission result
    - Availability result
    - Validation failure reason, if latest validation failed

### B. External data source state
- **ProjectSource**
  - Identity: external project ID
  - Fields:
    - External project ID
    - Enabled flag
    - Last confirmed snapshot
    - Last live validation result
    - Last validation timestamp

- **TeamSource**
  - Identity: external team ID
  - Parent identity: external project ID
  - Fields:
    - External team ID
    - External project ID
    - Enabled flag
    - Last confirmed snapshot
    - Last live validation result
    - Last validation timestamp

- **PipelineSource**
  - Identity: external pipeline ID
  - Parent identity: external project ID
  - Fields:
    - External pipeline ID
    - External project ID
    - Enabled flag
    - Last confirmed snapshot
    - Last live validation result
    - Last validation timestamp

### C. Domain configuration state
- **ProductRoot**
  - Identity: external work item ID
  - Fields:
    - External work item ID
    - Enabled flag
    - Last confirmed snapshot
    - Last live validation result
    - Last validation timestamp

- **ProductSourceBinding**
  - Identity: composite of:
    - Product root external work item ID
    - Source type
    - Source external ID
  - Source types:
    - Project
    - Team
    - Pipeline
  - Fields:
    - Binding identity
    - Enabled flag
    - Last validation result
    - Last validation timestamp

## 2.2 Identity model

1. Connection is a single logical configuration record.
2. Project, team, pipeline, and product root identities are external-ID based.
3. Snapshot display names are not identity.
4. Renames update snapshots and do not create new records.
5. Duplicate persisted entities with the same external identity are invalid state and must be prevented at write time.

## 2.3 Snapshot model

Every external-ID-backed entity stores a last confirmed snapshot for display and comparison.

### Required snapshot fields

- **Project snapshot**
  - External project ID
  - Name
  - Description, if present

- **Team snapshot**
  - External team ID
  - External project ID
  - Team name
  - Area path

- **Pipeline snapshot**
  - External pipeline ID
  - External project ID
  - Pipeline name
  - Pipeline type/path, if present

- **Product root snapshot**
  - External work item ID
  - Title
  - Work item type
  - Current state

### Snapshot rules

1. Snapshot data is persisted only after live confirmation.
2. Snapshot data may be shown when live TFS is unavailable, but it must be marked non-authoritative for writes.
3. Snapshot mismatch against live data creates a warning or rename update, not a duplicate entity.

## 2.4 Derived state

Derived state is computed; it is never edited directly.

### A. Per-entity derived state
- Configured / NotConfigured
- Valid / Invalid
- Available / Unavailable
- PermissionOk / PermissionDenied
- SnapshotCurrent / SnapshotStale
- Referenced / Unreferenced

### B. Per-flow derived state
- NotConfigured
- PartiallyConfigured
- Complete
- Blocked

### C. Global onboarding derived state
- NotConfigured
- PartiallyConfigured
- Complete
- Blocking reasons
- Warning list

## 2.5 Transient UI state

Transient UI state is never authoritative.

Allowed transient state:
- Search text
- Type filters
- Paging/sort state
- Expanded/collapsed details
- In-flight request state
- Local form inputs before submit
- Selection candidate before confirmation

Forbidden transient state:
- Authoritative onboarding completion
- Authoritative configured entity set
- Authoritative selected project/team/pipeline/root
- Current-session-only IDs required for later actions

## 3. Flow Definitions

### 3.1 TFS Connection & Validation

#### Responsibilities

- Establish the TFS connection identity used by onboarding
- Validate reachability, capabilities, and permissions
- Persist only validated connection state
- Expose explicit connection status for downstream flows

#### Inputs

- Organization URL
- Connection mode/auth context

#### Atomic actions

1. **Validate and persist connection**
   - Validates URL format
   - Executes live TFS validation
   - Evaluates required capabilities
   - Evaluates permission scope
   - Persists the connection record only if the full validation succeeds

2. **Revalidate existing connection**
   - Re-runs live validation against the persisted connection
   - Updates validation, permission, and availability results

3. **Clear connection**
   - Removes the connection record only if no dependent configured entities remain

#### Validation rules

1. Organization URL must be syntactically valid.
2. Live TFS must be reachable for create or update.
3. Required TFS capabilities must be present.
4. Required permissions for onboarding discovery must be present.
5. If any required validation fails, the connection write is rejected.
6. Validation failure must not replace the last known valid connection with invalid new values.

#### Persisted output state

- One persisted TfsConnection record
- Latest capability result
- Latest permission result
- Latest availability result
- Latest validation timestamps

#### Blocked conditions

- TFS unavailable
- Permission denied
- Capability validation failure
- Invalid URL
- Dependent entities exist when attempting to clear the connection

### 3.2 Data Source Setup

Flow 2 manages explicit lists of externally-backed source entities. It does not create local placeholder entities without live confirmation.

#### Responsibilities

- Manage configured project sources
- Manage configured team sources
- Manage configured pipeline sources
- Maintain validated snapshots for those entities
- Preserve idempotent updates by external identity

#### Entity rules

##### Projects
- Identity: external project ID
- Projects are the parent scope for teams and pipelines
- At least one valid enabled project is required for onboarding completion

##### Teams
- Identity: external team ID
- Teams are optional configuration entities
- A team may exist only under a configured project source

##### Pipelines
- Identity: external pipeline ID
- Pipelines are optional configuration entities
- A pipeline may exist only under a configured project source

#### Atomic actions

##### Project actions
1. **Add project source**
   - Lookup project via hybrid lookup
   - Confirm retrieved project details
   - Validate live existence and permissions
   - Persist or update the project source by external project ID

2. **Refresh project source**
   - Revalidate live existence and permissions
   - Update the snapshot and validation result

3. **Enable/disable project source**
   - Persist enabled state immediately
   - Recompute completion immediately

4. **Remove project source**
   - Remove only if no team, pipeline, or domain bindings depend on it

##### Team actions
1. **Add team source**
   - Requires an existing configured project source
   - Lookup team scoped to the parent project
   - Confirm retrieved team details
   - Validate live existence and permissions
   - Persist or update the team source by external team ID

2. **Refresh team source**
   - Revalidate live existence and permissions
   - Update the snapshot and validation result

3. **Enable/disable team source**
   - Persist enabled state immediately

4. **Remove team source**
   - Remove only if no domain bindings depend on it

##### Pipeline actions
1. **Add pipeline source**
   - Requires an existing configured project source
   - Lookup pipeline scoped to the parent project
   - Confirm retrieved pipeline details
   - Validate live existence and permissions
   - Persist or update the pipeline source by external pipeline ID

2. **Refresh pipeline source**
   - Revalidate live existence and permissions
   - Update the snapshot and validation result

3. **Enable/disable pipeline source**
   - Persist enabled state immediately

4. **Remove pipeline source**
   - Remove only if no domain bindings depend on it

#### Validation rules

1. All add and update actions require live TFS availability.
2. A source entity is valid only if its external ID resolves live and required permissions are present.
3. Snapshot-only data is insufficient for create or update.
4. Parent-child integrity is strict:
   - Team requires parent project
   - Pipeline requires parent project
5. Removal is blocked if the entity is still referenced by persisted children or bindings.

#### Persisted output state

- Explicit project source list
- Explicit team source list
- Explicit pipeline source list
- Latest validated snapshots for each configured source entity

#### Blocked conditions

- No validated connection
- TFS unavailable
- Permission denied
- Parent project missing
- Duplicate external ID
- Dependent bindings exist during removal

### 3.3 Domain Configuration

Flow 3 manages domain entities rooted in external work items and explicit mappings to configured source entities.

#### Responsibilities

- Manage product roots by external work item ID
- Maintain validated work item snapshots
- Create and manage explicit bindings from product roots to source entities
- Enforce that domain configuration references only valid configured source entities

#### Domain entity rules

##### Product roots
- Identity: external work item ID
- Product roots are explicit list entities with full CRUD
- A product root is valid only when live validation confirms the work item and required snapshot fields are retrieved

##### Source bindings
- Every binding links one product root to one configured source entity
- Project binding is the minimum required domain linkage
- Team and pipeline bindings are optional refinements
- Team binding requires a project binding to the same project scope
- Pipeline binding requires a project binding to the same project scope

#### Atomic actions

##### Product root actions
1. **Add product root**
   - Lookup or search work items via hybrid lookup
   - Filter search results by work item type
   - Confirm retrieved work item details
   - Validate live existence and allowed type
   - Persist or update the product root by external work item ID

2. **Refresh product root**
   - Revalidate live existence and details
   - Update the snapshot and validation result

3. **Enable/disable product root**
   - Persist enabled state immediately

4. **Remove product root**
   - Remove only if all source bindings for that root are removed in the same action set

##### Binding actions
1. **Add project binding**
   - Requires an existing valid product root
   - Requires an existing enabled valid project source
   - Persists the binding immediately

2. **Add team binding**
   - Requires an existing valid product root
   - Requires an existing enabled valid team source
   - Requires a persisted project binding in the same project scope
   - Persists the binding immediately

3. **Add pipeline binding**
   - Requires an existing valid product root
   - Requires an existing enabled valid pipeline source
   - Requires a persisted project binding in the same project scope
   - Persists the binding immediately

4. **Remove binding**
   - Removes the specific persisted binding immediately
   - Recomputes completion immediately

#### Validation rules

1. Product root add/update requires live TFS availability.
2. Product root add/update requires retrieval of:
   - External work item ID
   - Title
   - Type
   - State
3. Work item search results must be filterable by type before selection.
4. Raw numeric entry alone is insufficient to create or update a product root.
5. Binding creation is invalid if the referenced source entity is not persisted, enabled, and valid.
6. Duplicate bindings with the same composite identity are rejected.

#### Persisted output state

- Explicit product root list
- Explicit product-source binding list
- Latest validated product root snapshots

#### Blocked conditions

- No validated connection
- TFS unavailable
- Permission denied
- Work item not found
- Work item type not allowed
- Required source entity missing
- Required project binding missing for team/pipeline binding
- Duplicate binding

## 4. Interaction Model (Rules)

### 4.1 Navigation rules

1. Navigation between flows is always allowed.
2. Navigation never performs implicit persistence.
3. Persistence never depends on “Next”, “Back”, or wizard progression.
4. A flow may be viewed while blocked, but blocked actions must be non-executable.
5. Completion status changes only through persisted writes or validation refresh, never through navigation.

### 4.2 Persistence rules

1. Every write happens at the moment the user executes a named action.
2. Every write is atomic for its action scope.
3. Successful persistence is required before success may be reported.
4. A failed validation produces no authoritative write for that action.
5. Repeating the same valid action updates the existing persisted entity matched by identity.
6. Partial success is forbidden for a single logical action.

### 4.3 Selection rules

1. Every selectable entity declares single-item or multi-item behavior before selection begins.
2. Multi-item configuration is represented as an explicit persisted list with add, update/refresh, enable/disable, and remove actions.
3. Every external selection requires confirmation data retrieved from TFS before create or update.
4. Confirmation must include the persisted entity identity and the required snapshot fields for that entity type.
5. Snapshot-only confirmation is insufficient for create or update when live TFS is unavailable.

### 4.4 Validation rules

#### Hard validation

Hard validation blocks the write.

Hard validation applies to:
- Invalid connection URL
- TFS unavailable during live-required actions
- Missing required permission
- Missing required capability
- External entity not found
- Duplicate external identity
- Missing required parent/linked entity
- Duplicate binding
- Missing required confirmation data

#### Soft validation

Soft validation does not block the persisted record but must remain visible as derived state.

Soft validation applies to:
- Snapshot stale
- External entity renamed
- Previously valid entity currently unavailable after persistence

#### Trigger rules

1. Hard validation runs before every create and update write.
2. Soft validation is recomputed on refresh and background revalidation.
3. Completion ignores entities in hard-invalid state.

### 4.5 List behavior rules

1. Every multi-item entity is represented as a persisted list, not a hidden internal list field.
2. Add, enable/disable, refresh, and remove are independent actions per list item.
3. Removing a parent item with persisted dependents is blocked.
4. Disabling an item does not delete it.
5. Reordering does not change identity or semantics.

### 4.6 Error handling rules

#### TFS unavailable
1. New create/update actions requiring live validation are blocked.
2. Existing persisted entities remain visible from snapshots.
3. Availability is derived as unavailable.
4. Overall onboarding completion may degrade from Complete to PartiallyConfigured if required entities cannot be validated.

#### Permission issues
1. Permission-denied state is persisted as latest validation result.
2. Actions requiring the missing permission are blocked.
3. Existing persisted entities remain visible.
4. Completion may degrade if required entities lose mandatory permissions.

#### Success rules
1. Success may be shown only after persistence succeeds.
2. Success may not be shown when any required sub-check or sub-write failed.
3. Success must describe resulting persisted state, not attempted action execution.

## 5. Completion Model

Completion is derived from persisted state only.

### 5.1 Not configured

Overall onboarding status is **NotConfigured** when both conditions are true:
1. No persisted TFS connection exists.
2. No persisted project sources, team sources, pipeline sources, product roots, or bindings exist.

### 5.2 Partially configured

Overall onboarding status is **PartiallyConfigured** when either condition is true:
1. At least one onboarding entity exists in persisted state, but the complete criteria are not satisfied.
2. Previously complete configuration loses validity because of unavailability, permission loss, or deletion of required entities.

### 5.3 Complete

Overall onboarding status is **Complete** only when all conditions are true:
1. A persisted TFS connection exists.
2. The latest connection validation is valid.
3. At least one enabled valid project source exists.
4. At least one enabled valid product root exists.
5. Every enabled product root has at least one enabled valid project binding.
6. Every enabled team binding references an enabled valid team source and a project binding in the same project scope.
7. Every enabled pipeline binding references an enabled valid pipeline source and a project binding in the same project scope.

### 5.4 UI exposure

The UI receives a derived onboarding status object containing:
- Overall status
- Per-flow status
- Blocking reasons
- Warnings
- Counts of valid/invalid configured entities

The UI does not infer completion locally.

## 6. Import Integration

Import is a separate onboarding entry path.

### 6.1 Entry behavior

1. Import is entered independently from the three onboarding flows.
2. Import does not mark onboarding complete by itself.
3. After import, the system returns to persisted onboarding state with completion recomputed.

### 6.2 Validation rules

Import validates before writing:
1. File schema and structure
2. Unique identity per imported entity
3. Referential integrity between imported entities
4. Live validation of all external references required for imported creates or updates
5. Permission and capability requirements for imported entities

If any hard validation fails, the import writes nothing.

### 6.3 Merge strategy

Import uses **merge/upsert by identity**.

Rules:
1. Connection import targets the logical singleton connection record.
2. Project, team, pipeline, and product root imports match by external ID.
3. Binding imports match by composite identity.
4. Matching entities are updated in place.
5. Missing imported entities are created.
6. Existing persisted entities that are absent from the import remain unchanged.
7. Import-driven delete is not supported.

### 6.4 Post-import state

After a successful import:
1. Persisted entities are updated or created by identity.
2. Snapshots reflect imported and live-confirmed data.
3. Derived completion is recomputed.
4. Any non-blocking warnings remain visible as derived state.

## 7. Edge Cases

### 7.1 Duplicate prevention

1. Duplicate project/team/pipeline/root external IDs are rejected.
2. Duplicate bindings with the same composite identity are rejected.
3. Renamed entities are matched by external ID and update the snapshot instead of creating duplicates.

### 7.2 Lost permissions after configuration

1. Persisted entities remain stored.
2. Latest validation state changes to permission-denied.
3. Actions requiring the missing permission are blocked.
4. If a required entity becomes permission-denied, completion degrades to PartiallyConfigured.

### 7.3 Renamed TFS entities

1. Live refresh updates the stored snapshot fields.
2. External identity remains unchanged.
3. Rename never creates a second entity.
4. Existing bindings remain attached to the same external ID.

### 7.4 Partial configuration

1. Partial configuration is valid persisted state.
2. Partial configuration does not block free navigation.
3. Partial configuration blocks only actions missing prerequisites.
4. Partial configuration is exposed explicitly as PartiallyConfigured.

### 7.5 Re-entry into onboarding

1. Re-entry loads persisted entities and derived status.
2. Re-entry never resets onboarding progress.
3. Re-entry edits existing entities idempotently by identity.
4. Re-entry never depends on entities created during the current browser session.

### 7.6 Deletion of required entities

1. Removing a project source is blocked while dependent teams, pipelines, or bindings exist.
2. Removing a product root is blocked while bindings for that root exist.
3. Removing the last valid required project source or last valid product root degrades completion to PartiallyConfigured immediately.
4. Clearing the connection is blocked while any dependent configured entities remain.

### 7.7 TFS unavailable during lookup

1. Cached snapshots may still be shown for context.
2. New create/update actions are blocked.
3. No offline creation is permitted from cached data.

## 8. Anti-Patterns

The redesigned onboarding system must not contain any of the following:

1. **No mixed-responsibility flows**
   - Connection validation, import, source setup, and domain configuration must not be merged into one action surface.

2. **No hidden multi-item behavior**
   - Multi-item configuration must never be hidden behind a single field or implicit internal list.

3. **No save-vs-next ambiguity**
   - Persistence must never depend on wizard navigation semantics.

4. **No success without persistence**
   - Success must never be shown before the authoritative write succeeds.

5. **No unvalidated references**
   - External references must never be persisted from raw user input without live validation and confirmation.

6. **No current-session-only prerequisites**
   - A later action must never depend on an ID created only in the current browser session.

7. **No wizard-local authoritative state**
   - Completion, configured entities, and prerequisites must never be stored only in transient UI state.

8. **No offline create/update from stale snapshots**
   - Cached snapshots may support lookup context only; they must not authorize writes.

9. **No implicit destructive import behavior**
   - Import must not remove persisted entities by omission.

10. **No silent degradation**
    - Availability loss, permission loss, rename detection, and completion downgrade must always exist as explicit derived state.
