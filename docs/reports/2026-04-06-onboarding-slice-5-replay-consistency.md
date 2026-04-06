# Slice 5 — Replay Consistency

## 1. Replay Consistency Results

- Added frozen-response replay tests that run the real Slice 5 execution path twice against the same legacy dataset and the same mocked Slice 2 responses.
- Verified that the second run keeps onboarding entities, bindings, validation states, and persisted snapshots byte-stable.
- Verified that unchanged legacy input plus unchanged mocked external responses still produces a `NoOp` run with no onboarding-entity mutation.
- Compared persisted onboarding state before and after replay to confirm there are no entity changes on the verified no-op path.

## 2. Drift Detection Results

- Added replay-drift tests for:
  - renamed project response
  - unavailable work item after prior success
  - permission denied after prior success
- Exposed and corrected a real Slice 5 defect: unchanged fingerprints previously short-circuited directly to `NoOp` without proving that mocked external authority still matched the persisted migration state.
- Added fingerprint replay verification that:
  - validates persisted onboarding entities against current mocked Slice 2 responses
  - records warning drift issues for snapshot mismatches
  - records blocking validation issues for external failures
  - prevents silent overwrite and leaves previously persisted onboarding entities unchanged when drift is detected

## 3. Adversarial Data Results

- Added adversarial tests for:
  - duplicate team external IDs across projects
  - duplicate pipeline external IDs across projects
  - duplicate work item external IDs across products/projects
  - missing dependency propagation into binding failures
  - broken legacy rows with missing required team IDs
- Verified that invalid adversarial cases now surface issues instead of being silently accepted.
- Added a narrowly scoped correction for duplicate work-item external IDs reused across multiple products in one run so those roots are rejected with explicit issues instead of being silently conflated.

## 4. Partial Failure Recovery

- Kept and extended partial-failure replay coverage using mocked `TfsUnavailable` on the first run and success on the second run.
- Verified that recovery reruns:
  - complete the previously missing entities
  - do not duplicate already successful entities
  - do not falsely `NoOp`
  - leave no stale failure state behind in persisted onboarding entities

## 5. Concurrency Safety

- Revalidated simultaneous execution with a shared run lock and mocked blocking lookup behavior.
- Verified that one run is rejected while the first run holds the per-connection lock.
- Verified that the rejected concurrent run does not create interleaved or duplicate onboarding entities.

## 6. Ledger Validation

- Added proof assertions that binding-unit processed/failed counts reconcile with actual attempted binding failures.
- Verified that skipped/failed replay-drift paths now produce ledger-visible issues instead of silent no-op behavior.
- Verified that replay drift is reflected in run status and unit outcomes rather than hidden behind fingerprint short-circuiting.

## 7. Dry-Run Equivalence

- Re-ran dry-run versus live-path equivalence using identical mocked Slice 2 responses.
- Verified equivalent issue sets, validation outcomes, and dependency-failure behavior between dry-run and live-path execution.
- Confirmed that the only expected difference remains persistence of onboarding entities in live mode.

## 8. Fingerprint Verification

- Verified that unchanged input plus equivalent mocked external responses yields `NoOp`.
- Verified that unchanged input plus changed mocked external responses no longer yields a false `NoOp`.
- Preserved the existing behavior that meaningful legacy changes produce a new fingerprint and a new real execution.
- Added ordering-only fingerprint coverage through deterministic reader tests so insertion order changes alone do not change the computed fingerprint.

## 9. Final Verdict

- **PASS**

Proof summary:
- replay consistency is now demonstrated under frozen mocked Slice 2 responses
- drift is surfaced instead of being hidden by unconditional fingerprint no-op handling
- adversarial duplicate and dependency cases are ledger-visible
- partial-failure recovery remains idempotent
- concurrency protection remains effective
- dry-run and live-path behavior remain equivalent under mocked external authority

Validation:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingMigrationExecutionServiceTests|FullyQualifiedName~OnboardingMigrationLedgerServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingPersistenceFoundationTests|FullyQualifiedName~PersistenceRelationshipContractTests|FullyQualifiedName~OnboardingStatusServiceTests"`

Result:
- build passed
- targeted onboarding validation passed (`52` tests)
