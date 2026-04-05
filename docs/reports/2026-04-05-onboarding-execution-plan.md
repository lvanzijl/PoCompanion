# Onboarding Execution Plan

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-migration-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-implementation-slices.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-validation-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-reconciliation-strategy.md`

This document defines how the onboarding redesign is executed across contributors, pull requests, validation gates, rollout phases, and cutover. It does not change any prior slice, migration, validation, or reconciliation decision.

## 1. Execution Model

### 1.1 Team model

Execution uses a **multi-team delivery model with single release control**.

- **Backend stream** owns persistence, validation services, status engine, and write-path behavior.
- **API stream** owns endpoint contracts, request/response validation, and thin-controller integration.
- **UI stream** owns flagged user-facing onboarding flows only after required backend slices are complete.
- **Infra/Migration stream** owns migration framework, migration execution, rollout controls, and cutover operations.
- **Release owner** is singular and has final authority to advance from one slice to the next.

### 1.2 Division of work

- Backend and API streams may collaborate inside one slice only when the slice definition already contains both concerns.
- UI work is not allowed to start implementation until all backend dependencies listed for that slice are complete.
- Infra/Migration work is limited to slices 4, 5, 11, and 12 plus rollout execution support.
- No contributor may extend a slice to “finish something nearby” from a later slice.

### 1.3 Coordination rules

1. Slice number is the scheduling authority.
2. The release owner opens the next slice only after the current slice is explicitly accepted.
3. Every contributor works against the current active slice only.
4. Reviewers reject any PR that introduces responsibilities from another slice.
5. Validation evidence must be attached before approval; verbal confirmation is insufficient.
6. Feature flags isolate exposure only; they do not authorize incomplete backend contracts.

## 2. Slice Ownership

| Slice | Ownership type | Primary implementer | Reviewer | Approver |
|---|---|---|---|---|
| 1. Persistence foundation | Backend | Backend engineer responsible for EF model and migrations | Backend peer reviewer | Architecture owner |
| 2. Lookup & validation APIs | API | API engineer responsible for lookup endpoints and validation contracts | Backend reviewer for TFS/domain alignment | Architecture owner |
| 3. Completion/status engine | Backend | Backend engineer responsible for status computation | API reviewer for contract correctness | Architecture owner |
| 4. Migration infrastructure | Infra/Migration | Migration engineer responsible for ledger, issue model, and job framework | Backend reviewer for persistence boundaries | Release owner |
| 5. Migration execution | Infra/Migration | Migration engineer responsible for mapping and idempotent execution | Backend reviewer plus API reviewer for validation usage | Release owner |
| 6. Onboarding backend CRUD | API | API engineer responsible for final write surface | Backend reviewer for idempotency and validation | Architecture owner |
| 7. UI Flow 1 — TFS Connection | UI | UI engineer responsible for flagged connection flow | API reviewer for contract usage | Product/UI owner |
| 8. UI Flow 2 — Data Sources | UI | UI engineer responsible for flagged project/team/pipeline flow | API reviewer for lookup/revalidate behavior | Product/UI owner |
| 9. UI Flow 3 — Domain Configuration | UI | UI engineer responsible for flagged root/binding flow | Backend reviewer for dependency/status correctness | Product/UI owner |
| 10. Import path | API | API engineer responsible for import into the new model | Migration reviewer for model compatibility | Release owner |
| 11. Cutover preparation | Infra/Migration | Release engineer responsible for routing switch and legacy-write disablement | Architecture owner plus Product/UI owner | Release owner |
| 12. Cleanup | Backend | Backend engineer responsible for removing obsolete wizard/state/endpoints | Cross-stream reviewer from the most affected remaining surface | Architecture owner |

### Required role behavior

- **Primary implementer** delivers only the active slice scope.
- **Reviewer** verifies slice boundaries, acceptance criteria, validation evidence, and no hidden cross-slice behavior.
- **Approver** confirms the slice is complete enough to unlock the next slice and that rollout phase conditions are met.

## 3. Sequencing Rules

### 3.1 Strict sequencing

Implementation slices are **strictly sequential by slice number**:

`1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12`

This sequence is mandatory because the implementation-slices report states that no later slice may start until the current slice has merged code, passed tests, passed manual verification, produced required observability, and has no blocker defects.

### 3.2 Safe overlap limits

The following may overlap safely without violating slice order:

- test-plan authoring for the next slice
- reviewer scheduling
- release checklist preparation
- documentation of validation procedures

The following may **not** overlap:

- coding from a later slice
- enabling a later slice’s feature flag
- migration execution before slices 1–4 are complete
- UI implementation before its backend/API dependencies are complete

### 3.3 Blocking dependencies

| Slice | Must be finished before it starts |
|---|---|
| 1 | none beyond authoritative design documents |
| 2 | Slice 1 |
| 3 | Slices 1–2 |
| 4 | Slices 1–3 |
| 5 | Slices 1–4 |
| 6 | Slices 1–3 |
| 7 | Slices 1–3 and 6 |
| 8 | Slices 1–3 and 6 |
| 9 | Slices 1–3, 6, and 8 |
| 10 | Slices 1–3, 5, and 6 |
| 11 | Slices 1–10 |
| 12 | Slice 11 |

### 3.4 What “finished” means

A slice is finished only when all of the following are true:

1. implementation for that slice is merged
2. all acceptance criteria from `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-implementation-slices.md` are satisfied
3. slice-specific automated checks pass
4. manual verification for the slice public surface is recorded
5. required logs and metrics exist and are queryable for that slice
6. no known blocker defect remains open for that slice

If any one of these is missing, the next slice does not start.

## 4. PR Strategy

### 4.1 PR boundaries

- one PR maps to exactly one active slice
- no cross-slice PRs
- no PR may contain cleanup for a later slice
- no PR may introduce dormant code for a later slice unless it is required by the current slice acceptance criteria

### 4.2 Branch strategy

- use one working branch per slice implementation
- if a slice requires multiple PRs, each PR must still remain inside that slice and the slice stays open until all PRs merge
- branch naming must include the slice number, for example `feature/onboarding-slice-03-status-engine`

### 4.3 Mandatory review requirements

Every PR must have:

- the primary reviewer for its ownership type
- the approver defined in Section 2
- one reviewer from any directly affected dependency boundary when the slice touches cross-layer contracts

### 4.4 Required validation evidence in every PR

Every PR must include:

- explicit slice reference
- changed-scope statement proving no unrelated changes
- automated validation output
- manual verification steps and observed results
- observability proof when the slice requires logs or metrics
- feature flag proof if the slice is flag-gated

### 4.5 PR rejection criteria

Reject the PR immediately if:

- it mixes slice concerns
- it introduces mixed writes
- it enables a flag without the required slice dependencies
- it lacks validation evidence
- it contains unrelated refactors or cleanup

## 5. Definition of Done

### 5.1 Shared Definition of Done for every slice

No slice is done until all of the following are true:

- implementation complete for declared scope only
- acceptance criteria satisfied
- validation checks passed
- no known regressions introduced
- required observability is in place
- feature flag behavior verified when applicable

### 5.2 Slice-specific completion gates

| Slice | Additional done conditions |
|---|---|
| 1 | migration apply/rollback evidence recorded; uniqueness constraints proven |
| 2 | success/not-found/permission/unavailable validation evidence recorded for each entity type |
| 3 | deterministic status results recorded for not configured, partial, blocked, and complete states |
| 4 | migration ledger and issue recording proven durable without moving business data |
| 5 | rerun idempotency proven; unresolved migration issues recorded; no fabricated entities |
| 6 | repeated writes proven idempotent; delete blocking and dependency validation proven |
| 7 | flagged connection flow proven to persist only through new write path; no legacy wizard dependency |
| 8 | flagged data-source flow proven to use live lookup/revalidate rules and new write path only |
| 9 | flagged domain-configuration flow proven to enforce root/binding dependency rules and blocking/warning states |
| 10 | import proven to target only the new onboarding model and to reject invalid legacy assumptions |
| 11 | legacy wizard writes disabled in the target environment before new onboarding becomes authoritative; routing/status authority verified |
| 12 | old wizard routes, state, preferences, and obsolete endpoints removed with no surviving compatibility dependency |

## 6. Rollout Phases

### Phase 1 — Development

- feature flags off by default
- developers validate locally and in isolated development environments
- legacy wizard remains the only write path in shared environments
- no slice advances without local and CI evidence

### Phase 2 — Internal validation

- enable the active slice only in dedicated dev/test environments
- disable legacy wizard writes in those environments before any new onboarding write flow is enabled
- execute full slice validation, including manual verification and observability checks
- keep production unchanged

### Phase 3 — Pilot

- enable completed flagged slices for limited internal users or a dedicated pilot environment
- run reconciliation and validation monitoring continuously
- do not start cutover until pilot defects are zero at blocker severity

### Phase 4 — Production rollout

- enable only production-ready flags gradually and only after pilot acceptance
- monitor validation failures, drift metrics, and flag-specific behavior continuously
- pause immediately if invariants fail or legacy/new writes are both observed

### Phase 5 — Cutover

- run migration at the approved cut point only
- disable old wizard writes first
- enable new onboarding authority only after migration validation completes
- switch routing and status authority to the new model

### Phase 6 — Cleanup

- remove old code paths only after cutover stability is confirmed
- remove all temporary onboarding flags
- remove obsolete compatibility logic, wizard state, and routing guards

## 7. Feature Flag Lifecycle

| Flag | Introduced in slice | Enabled in phase/environment | Removed in slice | Rules |
|---|---|---|---|---|
| `Onboarding.NewConnectionFlowEnabled` | 7 | Phase 2 in dedicated dev/test; Phase 3 pilot; Phase 4 production after slice 7 acceptance | 12 | Requires slice 6 complete and `Onboarding.LegacyWizardWriteEnabled=false` in that environment |
| `Onboarding.NewDataSourcesFlowEnabled` | 8 | Phase 2 in dedicated dev/test; Phase 3 pilot; Phase 4 production after slice 8 acceptance | 12 | Requires slices 6 and 8 complete and legacy writes disabled in that environment |
| `Onboarding.NewDomainConfigurationFlowEnabled` | 9 | Phase 2 in dedicated dev/test; Phase 3 pilot; Phase 4 production after slice 9 acceptance | 12 | Requires slices 6, 8, and 9 complete and legacy writes disabled in that environment |
| `Onboarding.NewImportPathEnabled` | 10 | Phase 2 in dedicated dev/test; Phase 3 pilot; Phase 4 production after slice 10 acceptance | 12 | Must write only to the new model; never exposed where migration readiness is unknown |
| `Onboarding.NewExperienceEnabled` | 11 | Phase 5 cutover in the target environment after migration approval | 12 | Enables new onboarding as authoritative entry point; cannot be true while legacy writes are enabled |
| `Onboarding.LegacyWizardWriteEnabled` | existing transition guard formalized in 11 | True until cutover in environments still using the legacy wizard | 12 | Must be forced false before any authoritative new onboarding write flow is enabled in that environment |

### Flag governance rules

1. No flag may remain after Slice 12.
2. No environment may set `Onboarding.NewExperienceEnabled=true` while `Onboarding.LegacyWizardWriteEnabled=true`.
3. Flags gate exposure only; they do not waive acceptance criteria or validation.

## 8. Migration Ownership

### 8.1 Triggering migration

- migration is triggered by the **Infra/Migration primary implementer**
- trigger execution requires explicit approval from the **Release owner**
- production migration must not be self-approved by the implementer

### 8.2 Validating migration results

Migration results are validated by:

- Migration reviewer for ledger completeness and idempotent behavior
- Backend reviewer for entity integrity and dependency ordering
- Release owner for go/no-go decision on cutover readiness

### 8.3 Required reports before migration

Before migration starts, the following must exist and be accepted:

- slice completion evidence for Slices 1–5
- current validation evidence proving slices 1–5 satisfy acceptance criteria
- migration dry-run report for the target environment or equivalent non-production data set
- reconciliation readiness report showing required observability and drift visibility are in place
- cutover readiness checklist signed by the release owner

### 8.4 Required reports after migration

After migration completes, the following must be produced before cutover:

- migration run report with unit-by-unit outcome
- migration issue report listing all blocker and non-blocker issues
- post-migration validation report proving entity counts, uniqueness, and dependency integrity
- post-migration reconciliation baseline report proving drift detection is functioning
- cutover approval record

## 9. Risk Controls

### 9.1 Detecting slice violations

Slice violations are detected by:

- PR checklist requiring explicit slice declaration
- reviewer scope audit against the current slice definition
- changed-path audit to detect later-slice files or responsibilities
- validation review confirming no hidden dependencies on future slices

### 9.2 Rollout stop conditions

Rollout stops immediately if:

- any system invariant from the validation strategy is broken
- mixed writes are observed
- migration creates duplicate or orphaned entities
- required logs or metrics are missing
- blocking drift is detected and not surfaced correctly
- routing or onboarding status disagrees with persisted state

### 9.3 Rollback strategy by phase

- **Phase 1 — Development:** revert branch or local flag override; no shared-environment rollback needed
- **Phase 2 — Internal validation:** disable the active slice flag in the dedicated environment and reopen the slice
- **Phase 3 — Pilot:** disable the pilot flag set, stop further rollout, preserve evidence, and return pilot users to the last accepted behavior
- **Phase 4 — Production rollout:** disable the new exposure flag for the affected environment segment; do not proceed to cutover
- **Phase 5 — Cutover:** if post-migration or routing validation fails before authoritativeness is declared, keep legacy wizard disabled state reversible only if migration rollback criteria from the migration strategy are satisfied; otherwise halt and remediate before user entry resumes
- **Phase 6 — Cleanup:** no cleanup deployment proceeds without cutover stability evidence; if cleanup introduces regression, redeploy the last accepted post-cutover build

## 10. Communication Model

### 10.1 Progress tracking

- progress is tracked per slice, not per person
- each slice has a single status: `Not Started`, `In Progress`, `In Review`, `Accepted`, or `Blocked`
- the release owner updates the slice board only after validation evidence is reviewed

### 10.2 Blocker escalation

- implementers escalate blockers the same working day they are discovered
- blockers go first to the slice reviewer, then immediately to the release owner if they affect sequencing, migration, or cutover
- architecture or boundary blockers require architecture-owner involvement before work resumes

### 10.3 Validation result sharing

Validation results are shared in three places:

- the PR description for slice-local evidence
- the slice tracking record for accepted evidence
- the rollout/cutover channel for environment-level validation and migration outcomes

Validation evidence is incomplete unless it includes:

- automated results
- manual verification notes
- observability confirmation
- explicit blocker/no-blocker statement
