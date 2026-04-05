# Onboarding Migration Strategy

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`

This document defines the migration strategy from the current onboarding wizard to the redesigned onboarding system. It does not change the redesign or the domain/API alignment.

## 1. Current vs New Mapping

### 1.1 Current onboarding state inventory

Current onboarding state is split across:

1. **Persisted backend configuration**
   - `TfsConfigEntity`
   - `ProfileEntity`
   - `ProductEntity`
   - `ProductBacklogRootEntity`
   - `TeamEntity`
   - `ProductTeamLinkEntity`
   - `RepositoryEntity`
   - `PipelineDefinitionEntity`

2. **Client-side wizard/session state**
   - `OnboardingWizard.razor` fields such as `_configCompleted`, `_createdProfileId`, `_createdProductId`, `_createdTeamId`, `_selectedRepositoryNames`
   - `IOnboardingWizardState` / `OnboardingWizardState` for TFS verification fingerprint state

3. **Client-side completion state**
   - `IOnboardingService` / `OnboardingService` preference keys:
     - `OnboardingCompleted`
     - `OnboardingSkipped`

4. **Derived startup gating**
   - `StartupReadinessDto` plus client onboarding preferences drive routing into `/onboarding`

### 1.2 Current wizard-state storage

Current wizard state is not stored in the backend. It is stored in:

- component fields inside `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor`
- scoped client service `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWizardState.cs`
- browser preferences via `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingService.cs`

This is incompatible with the redesign because completion and progression currently depend on transient client state.

### 1.3 Persistence gaps and inconsistencies

Current state model has the following migration-relevant defects:

1. TFS config can be persisted before validation fully succeeds.
2. Profile, product, team, and repository entries can exist only in component memory until `Save*` runs.
3. Product/team/repository creation is not idempotent by external identity.
4. Team linking and repository persistence depend on current-session IDs.
5. Completion is stored in client preferences instead of being derived from persisted backend state.

### 1.4 Explicit mapping table

| Current structure | Current role | New target | Migration rule |
|---|---|---|---|
| `TfsConfigEntity` | Stores URL/project plus test/verify flags | `TfsConnection` | One-to-one migration. Preserve URL, timeout, API version, and latest validation timestamps. Re-run live validation before marking migrated connection valid. |
| `StartupReadinessDto` + client onboarding preferences | Determines whether onboarding route is shown | `OnboardingStatus` | No direct data migration. Replace with server-derived onboarding status from persisted onboarding entities. |
| `OnboardingService` preference keys (`OnboardingCompleted`, `OnboardingSkipped`) | Stores user-triggered completion/skip | No persistent equivalent | Do not migrate. Drop on cutover. New completion is derived only from backend state. |
| `OnboardingWizardState` (`TfsVerified`, fingerprint, dirty flag) | In-dialog verification state | No persistent equivalent | Do not migrate. Remove after wizard removal. |
| `OnboardingWizard.razor` fields (`_configCompleted`, `_createdProfileId`, `_createdProductId`, `_createdTeamId`, `_selectedRepositoryNames`) | Session-local orchestration state | No persistent equivalent | Do not migrate. Replace with immediate writes and server reads. |
| `ProfileEntity` | Product Owner profile created during onboarding step 2 | No onboarding-domain equivalent | Not migrated into onboarding domain. Profile management remains outside onboarding scope and must be removed from onboarding flow. |
| `ProductEntity` | Local product shell with owner/name/project linkage | No one-to-one onboarding entity | Product shell itself is not migrated into onboarding authority. Only related backlog roots and source relationships feed onboarding migration. |
| `ProductBacklogRootEntity` | Stores product backlog root work item IDs | `ProductRoot` | Create one `ProductRoot` per distinct TFS work item ID after live lookup succeeds. |
| `TeamEntity` with `TfsTeamId` and `ProjectName` | Local delivery team plus optional TFS identity | `TeamSource` | Migrate only rows with resolvable `TfsTeamId` and resolvable TFS project. Rows without stable external identity are blocked and reported for manual remediation. |
| `RepositoryEntity` | Local repository name attached to product | No direct onboarding-domain equivalent | Not migrated as a first-class onboarding entity. Used only as supporting context when reconciling legacy pipeline definitions. |
| `PipelineDefinitionEntity` | Persisted pipeline definition linked to product/repository | `PipelineSource` | Create one `PipelineSource` per distinct pipeline external ID after live validation succeeds. Use stored repository metadata for snapshot enrichment. |
| `ProductTeamLinkEntity` | Links local product to local team | `ProductSourceBinding` (`Team`) | For each linked product, create one binding per migrated `ProductRoot` to the migrated `TeamSource`. |
| Product-to-pipeline association implied by `PipelineDefinitionEntity.ProductId` | Associates pipelines to a product | `ProductSourceBinding` (`Pipeline`) | For each product pipeline, create one binding per migrated `ProductRoot` to the migrated `PipelineSource`. |
| Project choice stored only as `TfsConfigEntity.Project` or `TeamEntity.ProjectName` | Weak project reference | `ProjectSource` | Create one `ProjectSource` per distinct resolvable TFS project ID discovered from connection, teams, roots, or pipelines. |
| Local `ProjectEntity` | Internal routing container generated from product name | No onboarding-domain equivalent | Do not migrate into onboarding domain. It is not a TFS-backed project identity. |

### 1.5 Mapping constraints

1. A legacy row is migrated only if a stable external identity can be resolved.
2. External-ID resolution is mandatory before new onboarding entity creation.
3. Legacy records that cannot resolve to authoritative external identities are not silently transformed.
4. Unmigratable legacy rows become blocking migration issues, not inferred entities.

## 2. Migration Approach

### Chosen approach: Incremental replacement

The migration strategy is **incremental replacement**, not big bang and not dual-write parallel systems.

### Why incremental replacement is chosen

1. The redesign changes both the persistence model and the completion model.
2. The current wizard mixes client state, preferences state, direct HTTP orchestration, and legacy entity writes.
3. A big bang cutover would combine schema change, API replacement, data backfill, routing change, and UX cutover into one failure domain.
4. True parallel write systems would create drift because the current wizard writes legacy entities while the new system writes external-ID-based onboarding entities.

### Risks mitigated

Incremental replacement mitigates:

- cutover blast radius
- dual-write divergence
- incomplete backfill hidden until users enter onboarding
- inability to validate migrated data before user traffic switches

### Risks that remain

Remaining risks:

- legacy data may be incomplete or invalid for external-ID-based migration
- external TFS permissions may differ at migration time versus original setup time
- downstream consumers may continue depending on legacy entities longer than onboarding does

## 3. Rollout Phases

### Phase 1 — Backend readiness

Enabled:
- new onboarding persistence schema
- new onboarding API surface
- migration ledger and migration issue reporting
- read-only migration diagnostics

Disabled:
- new onboarding UI route
- new onboarding write path in production traffic

Data written:
- new onboarding tables may exist but are not user-written
- legacy onboarding flow continues writing legacy entities only

Who is affected:
- backend operators and developers only

Exit criteria:
- schema deployed
- onboarding APIs available
- migration dry-run passes in non-production

### Phase 2 — Backfill and validation in pilot ring

Enabled:
- eager migration job for pilot environment
- new onboarding UI behind environment-level feature flag
- compatibility reads/projections for legacy consumers in pilot environment

Disabled:
- old wizard write path in pilot environment

Data written:
- migrated `TfsConnection`
- migrated `ProjectSource`
- migrated `TeamSource`
- migrated `PipelineSource`
- migrated `ProductRoot`
- migrated `ProductSourceBinding`
- migration run records
- migration issue records

Who is affected:
- pilot environment users only

Rules:
- in the pilot ring, new onboarding becomes the only onboarding write path
- old wizard route may remain reachable only as read-only compatibility or redirect

Exit criteria:
- no unresolved blocker-level migration issues in pilot data
- onboarding status derivation matches expected migrated state
- migration rerun proven idempotent

### Phase 3 — Production backfill and default switch

Enabled:
- migration job in production
- new onboarding UI as default onboarding experience
- server-derived onboarding status for startup routing

Disabled:
- old wizard as a production write path
- client-preference-based onboarding completion gating

Data written:
- new onboarding entities only for onboarding changes
- compatibility projection data for legacy downstream consumers if still required

Who is affected:
- all users

Rules:
- cutover occurs only after production backfill succeeds and health checks are green
- no environment may allow both old and new onboarding write paths at the same time

Exit criteria:
- production migration completed
- new onboarding is default
- old wizard traffic is zero or redirected

### Phase 4 — Cleanup and removal

Enabled:
- only redesigned onboarding system

Disabled:
- old wizard UI
- old wizard routing logic
- onboarding preference gating
- wizard-specific live lookup endpoints

Data written:
- only redesigned onboarding entities

Who is affected:
- all users, but no expected behavior change if previous phases succeeded

Exit criteria:
- legacy onboarding write APIs removed
- compatibility layer removed
- obsolete onboarding-only state removed

## 4. Data Migration Strategy

### 4.1 Migration timing

Migration runs **eagerly per environment**, not lazily on first user access.

Execution point:
1. deploy backend with new schema and migration job
2. run migration job explicitly
3. validate migration health
4. only then enable the new onboarding write path in that environment

### 4.2 Why eager migration is required

1. The redesigned onboarding must open against complete persisted state.
2. Lazy migration would create first-user timing risk and partial availability.
3. Live validation and external-ID resolution are too important to hide inside user-triggered page entry.

### 4.3 Migration unit of work

Migration processes these logical units:

1. connection
2. project sources
3. team sources
4. pipeline sources
5. product roots
6. product-source bindings

Each unit is independently rerunnable and recorded in a migration ledger.

### 4.4 Idempotency rules

Migration is idempotent by design:

1. `TfsConnection` upserts by singleton identity.
2. `ProjectSource`, `TeamSource`, `PipelineSource`, and `ProductRoot` upsert by external ID.
3. `ProductSourceBinding` upserts by composite identity.
4. Re-running migration with unchanged legacy input produces no duplicate rows.
5. Re-running migration after legacy cleanup updates migrated rows instead of adding replacements.

### 4.5 Detecting already migrated state

Already-migrated detection requires:

- migration version
- migration run status
- source fingerprint or row-version summary for legacy input set
- existence check on target onboarding identities

Rules:
1. If migration version is complete and source fingerprint is unchanged, rerun is skipped.
2. If migration version is complete but source fingerprint changed before cutover, rerun performs upserts.
3. After cutover, legacy writes are disabled, so post-cutover reruns should be rare and operator-initiated only.

### 4.6 Legacy-to-new migration rules

1. Migrate connection first.
2. Resolve distinct TFS projects next.
3. Migrate teams and pipelines only after parent projects exist.
4. Migrate product roots only after live work-item validation succeeds.
5. Create bindings only after both endpoints exist and validate successfully.
6. Skip and record any unresolved legacy row; do not fabricate identities.

## 5. Coexistence Rules

### 5.1 Source of truth by phase

| Phase | Source of truth |
|---|---|
| Phase 1 | Legacy onboarding entities and client gating |
| Phase 2 pilot | New onboarding entities only |
| Phase 3 production | New onboarding entities only |
| Phase 4 | New onboarding entities only |

### 5.2 Conflict prevention

1. Coexistence is allowed only at code level, not as concurrent write systems in one environment.
2. Feature flags are environment-scoped or ring-scoped, not per-user within the same write environment.
3. Once new onboarding is enabled in an environment, the old wizard becomes read-only or unavailable there.
4. No dual-write bridge from old wizard to new model is allowed.

### 5.3 Old system behavior during coexistence

- In environments where the flag is off, old wizard remains unchanged.
- In environments where the flag is on, old wizard is blocked from writing and must redirect to the new onboarding surface or show read-only status.

## 6. Backward Compatibility

### 6.1 APIs that must remain temporarily

These endpoints remain temporarily because existing startup or legacy clients still depend on them:

- `GET /api/startup/readiness`
- `GET /api/tfsconfig`
- `POST /api/tfsconfig`
- `POST /api/tfsconfig/save-and-verify`
- `GET /api/startup/tfs-projects`
- `GET /api/startup/tfs-teams`
- `GET /api/startup/git-repositories`
- current `products`, `teams`, and repository-linking endpoints used outside the new onboarding flow

### 6.2 Temporary adaptation rules

1. Startup readiness must be adapted to derive readiness from new onboarding status after cutover.
2. Legacy `GET /api/tfsconfig` may be projected from `TfsConnection` until callers are removed.
3. Legacy startup lookup endpoints may proxy new lookup services until clients are switched.
4. Old wizard-specific orchestration endpoints remain only until old wizard traffic is zero.

### 6.3 Compatibility removal point

Compatibility endpoints are removed only after:

1. new onboarding is default in production
2. old wizard route is disabled
3. startup routing no longer depends on client onboarding preferences
4. downstream callers no longer require legacy onboarding-specific API shapes

## 7. Cleanup Strategy

### 7.1 Cleanup preconditions

Cleanup starts only when all are true:

1. new onboarding is the only write path in production
2. migration blocker count is zero or explicitly accepted outside onboarding scope
3. no production traffic uses old wizard endpoints
4. startup routing uses server-derived onboarding status only

### 7.2 Order of removal

1. remove client preference gating
2. remove scoped wizard-state service
3. remove old wizard route and dialog component
4. remove old wizard-specific API endpoints
5. remove compatibility adapters/wrappers
6. remove obsolete onboarding-only database structures

### 7.3 Structures to remove

Backend and frontend cleanup targets:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWizardState.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs` onboarding-only TFS save/verify endpoints
- startup onboarding lookup wrappers once callers move to the new onboarding lookup API

Database structures are removed only if they become unused across the application. At minimum, old wizard-only completion state in preferences is always removed. Legacy persisted entities such as `ProductEntity`, `RepositoryEntity`, `ProductBacklogRootEntity`, and `ProductTeamLinkEntity` are removed only after non-onboarding consumers are migrated.

## 8. Risks & Mitigations

| Risk | Detection | Mitigation | Recovery |
|---|---|---|---|
| Partial migration failure mid-process | Migration ledger shows run `Failed` with incomplete unit statuses | Migration commits by logical unit, records per-unit outcome, and never enables new onboarding flag after a failed run | Fix failing cause and rerun only failed units; completed units are safe to upsert again |
| Invalid legacy data | Preflight validation report shows missing IDs, malformed values, or legacy rows without stable external identity | Do not migrate invalid rows; create migration issue records that block cutover | Correct legacy data or manually retire invalid rows, then rerun migration |
| Missing external references (deleted TFS entities) | Live lookup returns `404 NotFound` during migration or revalidation | Skip creation of that onboarding entity; record blocking issue with legacy row reference | User/admin selects replacement source/root or removes obsolete legacy record; rerun migration |
| Permission loss after migration | Revalidation returns `403 PermissionDenied`; onboarding status shows blocked connection or entity | Cutover is blocked if permission loss occurs before enablement; after cutover, mark entity invalid and expose blocking reason | Restore permissions and rerun revalidation; no data rollback required |
| Duplicate entities created during migration | Duplicate external IDs or duplicate binding composite keys detected in preflight or upsert stage | Collapse by external identity into one migrated source/root and generate multiple bindings only where semantics require them | Resolve ambiguous duplicates manually if collapse is impossible, then rerun |

### Additional mandatory rules

1. Migration issues are blocking until explicitly resolved or excluded from onboarding scope.
2. No hidden fallbacks are allowed for missing external IDs.
3. Partial success may exist only at the migration-run level with explicit failed-unit recording; it is never silent.

## 9. Rollback Strategy

### 9.1 Rollback support

**Full rollback of migrated onboarding data after production cutover is not the primary safety strategy.**

Supported rollback:
- **application-path rollback**, not destructive data rollback

This means:
1. the feature flag can switch the environment back to the old wizard before final cleanup
2. migrated onboarding tables remain in place
3. legacy data remains untouched until cleanup

### 9.2 Why data rollback is not primary

1. The migration is additive during coexistence phases.
2. Destructive rollback would reintroduce the same inconsistency risks the migration is trying to remove.
3. Keeping legacy data intact until cleanup provides the safer fallback.

### 9.3 What is lost on application-path rollback

If rollback occurs after pilot or early production enablement:

- new onboarding edits made after cutover are not automatically translated back into old wizard session semantics
- operators may need a temporary read-only freeze while deciding whether to re-enable legacy edits

### 9.4 Risk reduction instead of destructive rollback

Risk is reduced by:

1. environment-scoped rollout
2. eager migration before enablement
3. keeping legacy persisted data intact until final cleanup
4. making old wizard unavailable as a concurrent writer once the new system is enabled

## 10. Observability

### 10.1 Required logs

The migration and cutover process must log:

- migration run ID
- migration version
- environment/ring
- unit start and finish timestamps
- created/updated/skipped/error counts per entity type
- blocking issue count
- duplicate collapse count
- permission-denied count
- not-found external reference count
- feature-flag enable/disable events

Logs must use sanitized identifiers only where required by policy.

### 10.2 Required metrics

Success/failure metrics:

- migration run success/failure
- migration duration by unit
- total migrated connections/projects/teams/pipelines/roots/bindings
- unresolved migration issues
- revalidation failure rate
- onboarding status distribution after cutover (`NotConfigured`, `PartiallyConfigured`, `Complete`, `Blocked`)
- old wizard endpoint traffic count
- new onboarding endpoint traffic count

### 10.3 Early issue detection

Migration problems must be considered early if any of the following occur:

1. blocker issue count is non-zero after migration
2. revalidation failure rate exceeds the accepted threshold
3. duplicate collapse count is higher than expected baseline
4. permission-denied or not-found counts spike after cutover
5. old wizard endpoints continue receiving production traffic after default switch

### 10.4 Cutover health gate

Promotion to the next phase requires:

1. migration run success
2. zero unresolved blocker issues
3. stable onboarding status derivation
4. no unexpected traffic on disabled write paths
5. operator review of migration metrics and logs
