# Slice 5 — Remediation

## 1. Determinism Fixes

- Removed mapper-side `DateTime.UtcNow` usage and stamped mapped entities with the migration run timestamp.
- Passed a fixed per-run timestamp into validation so snapshot metadata and validation states no longer drift within a run.
- Added per-run lookup caches for projects, teams, pipelines, and work items so the same run reuses the same live results.
- Replaced implicit dictionary-order behavior with explicit ordering for project candidates, lookup payloads, and binding work items.
- Reused prior successful root/team/pipeline validation state during binding validation instead of re-validating with fresh live lookups.

## 2. Idempotency Fixes

- Aligned upsert queries with database uniqueness scopes:
  - `ProjectSource` → `(TfsConnectionId, ProjectExternalId)`
  - `TeamSource` → `(ProjectSourceId, TeamExternalId)`
  - `PipelineSource` → `(ProjectSourceId, PipelineExternalId)`
  - `ProductRoot` → `(ProjectSourceId, WorkItemExternalId)`
  - `ProductSourceBinding` → `(ProductRootId, SourceType, SourceExternalId)`
- Added dry-run persistence simulation with scoped keys and deterministic synthetic IDs so dry-run exercises the same identity model as live mode.
- Added a singleton migration run lock so only one migration per connection can execute at a time.
- Limited fingerprint no-op behavior to prior successful/no-op runs so partial-failure reruns still execute.

## 3. Validation Fixes

- `ResolveProjectCandidatesAsync` now records issues for work-item discovery failures and pipeline discovery failures instead of dropping them.
- Validation now consumes cached lookup results and the fixed run timestamp when execution calls it.
- Binding validation reuses existing validated parent state when available and only falls back to live validation when necessary.
- Discovery failures now remain visible in the ledger instead of silently pruning candidate sets.

## 4. Dependency Fixes

- Removed silent binding filtering by converting binding generation into explicit valid or invalid work items.
- Missing migrated roots, teams, pipelines, and project sources now generate blocking issues for each skipped binding path.
- Team, pipeline, and product-root units now emit per-entity dependency issues when prerequisites are missing instead of failing with synthetic counts.
- Project discovery now records inconsistent or unresolved legacy references rather than skipping them.

## 5. Issue Coverage

- Added explicit issue recording for:
  - unchanged fingerprint no-op runs
  - project reference discovery misses and ambiguities
  - work-item discovery failures during project resolution
  - pipeline project discovery failures and ambiguities
  - missing binding dependencies
- Severity model is now exercised in practice:
  - `Blocking` for write-preventing failures
  - `Info` for successful fingerprint-based no-op runs

## 6. Dry-Run Parity

- Dry-run now uses the same upsert path as live mode through an in-memory persistence simulator.
- Synthetic IDs are assigned deterministically so dry-run no longer depends on `Id = 0` behavior.
- Scoped uniqueness and FK-like parent requirements are enforced during dry-run via the simulator.
- Added parity coverage comparing dry-run and live issue sets for the same failing legacy input.

## 7. Ledger Corrections

- Removed synthetic unit failure counts for dependency-only cases and aligned processed/failed counts with actual attempted work items.
- Every invalid binding work item now increments counts and records a corresponding issue.
- Added `NoOp` run status for fingerprint-based skips.
- Ledger finalization now treats all-skipped runs as `NoOp`, while blocking execution paths still fail units truthfully.

## 8. Fingerprint Strategy

- Implemented preferred Option A:
  - unchanged fingerprint + prior successful/no-op run → new run recorded as `NoOp`
  - unchanged fingerprint + prior failed/partial run → execution still proceeds
- Removed the unused `HasMatchingFingerprint` execution-state behavior by replacing it with active control flow.

## 9. Test Results

Added or updated adversarial coverage for:
- same-fingerprint rerun → `NoOp`
- partial-failure rerun with unchanged fingerprint still executes
- duplicate team external IDs across projects persist as scoped records
- pipeline discovery failure records an issue
- missing migrated root produces binding dependency issues
- dry-run/live issue equivalence
- concurrent migration attempt is rejected by the run lock

Validation run:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingMigrationExecutionServiceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`

Result:
- Build passed
- Targeted onboarding remediation tests passed (`44` tests)

## 10. Final Verification

- Deterministic per-run timestamps replace wall-clock mapping drift.
- Same-run live lookups are cached and reused.
- Dry-run and live now share the same scoped identity and persistence simulation semantics.
- Silent discovery and binding drops were removed; every failure path records a ledger issue.
- Reruns do not duplicate scoped entities, and concurrent execution is blocked per connection.
