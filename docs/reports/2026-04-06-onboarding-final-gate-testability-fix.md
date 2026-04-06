# Final Gate — Testability Fix

## 1. Verification Environment Gaps

- The previous final-gate run was blocked by incomplete mock onboarding lookup coverage rather than by the mutation architecture itself.
- Mock startup seeded core PO data and TFS configuration, but it did not seed onboarding graph state for named verification scenarios.
- Mock onboarding lookups did not expose deterministic project/root/team/pipeline candidates aligned with the onboarding write paths.
- Permission denied, stale/not-found, TFS unavailable, and lookup-empty states were not selectable as reproducible local scenarios.

## 2. Scenario Fixtures Added

Added deterministic mock verification scenarios through the onboarding verification scenario catalog:

- `happy-binding-chain`
  - connection seeded
  - deterministic project, root, team, and pipeline lookup candidates available
  - supports full project → root → binding execution
- `missing-root`
  - connection + project seeded
  - root lookup intentionally empty
  - exercises incomplete-chain and lookup-empty behavior
- `team-assignment`
  - connection + project + root + valid project binding seeded
  - invalid seeded team binding blocker present
  - multiple visible team candidates available for correction
- `pipeline-assignment`
  - connection + project + root + valid project binding seeded
  - invalid seeded pipeline binding blocker present
  - multiple visible pipeline candidates available for correction
- `permission-denied`
  - deterministic permission-denied lookup failure
- `stale-project`
  - seeded project exists in read model
  - live lookup intentionally no longer returns it
- `tfs-unavailable`
  - deterministic TFS unavailable lookup failure
- `lookup-empty`
  - seeded project/root path with empty assignment candidates

Each scenario now defines:

- initial onboarding read-model seed state
- lookup candidate set
- forced failure modes
- intended final-gate path coverage

## 3. Forced Failure Modes

Deterministic local/mock failure support now includes:

- `PermissionDenied`
- `NotFound` / stale entity
- `TfsUnavailable`
- lookup empty state
- ambiguous target state via multi-candidate team/pipeline scenarios

These are selected by named scenario through mock/dev verification configuration and do not depend on random behavior or external services.

## 4. Lookup Flow Coverage

Lookup-backed flows now have deterministic executable candidates for:

- project creation / linking
- product-root creation
- team assignment
- pipeline assignment
- binding creation

Consistency improvements:

- external IDs and display names come from one scenario catalog
- seeded onboarding entities resolve from the same lookup fixtures used by validation
- root, team, and pipeline candidates now map cleanly into real CRUD/validation paths

## 5. Deterministic Verification Support

Added mock/dev-only verification infrastructure:

- onboarding verification scenario options and selector
- named scenario catalog with expected final-gate paths
- scenario-aware onboarding lookup interception in mock mode
- onboarding graph seeding in `MockConfigurationSeedHostedService`
- reset-and-reseed behavior so selected scenarios are reproducible on rerun

Production isolation:

- scenario support is enabled only when `TfsIntegration:UseMockClient=true`
- no production TFS path was redesigned
- no direct TFS access was introduced
- existing onboarding mutation flow still goes through the same validation and CRUD services

## 6. Test Results

Added/extended automated coverage for:

- full binding chain execution end-to-end
- permission-denied reproducibility
- stale/not-found reproducibility
- reachable team-assignment blocker
- valid pipeline/root lookup candidates
- deterministic scenario selection
- same-scenario rerun stability
- SQLite mock seeding of scenario-specific onboarding graph states

Validation run:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingExecutionServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests|FullyQualifiedName~OnboardingVerificationScenarioTests|FullyQualifiedName~MockConfigurationSeedHostedServiceTests|FullyQualifiedName~OnboardingLiveLookupClientTests|FullyQualifiedName~OnboardingLookupServiceTests|FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingStatusServiceTests" --logger "console;verbosity=minimal"`

Result:

- build succeeded
- targeted onboarding validation passed
- 110 tests passed in the targeted verification run

## 7. Product Isolation

Confirmed:

- no backend contract redesign
- no new mutation semantics
- no UI-side CRUD bypass or flow workaround
- no direct TFS usage
- no production-only behavior changed beyond mock/test verification isolation

The changes are limited to:

- mock/dev scenario selection
- mock onboarding lookup data
- mock onboarding graph seeding
- test coverage

Release notes: N/A (no user impact)

## 8. Ready for Final Gate Rerun

The local/mock onboarding verification environment is now prepared for deterministic final-gate reruns because:

- every critical onboarding flow has executable fixture data behind it
- failure scenarios are selectable and repeatable
- lookup-backed action zones have valid and invalid paths
- seeded read-model state aligns with the lookup fixtures used during validation
- scenario reruns do not depend on hidden state or real TFS access

Final-gate reruns should no longer be blocked by “partial / not reproducible” mock-data gaps.
