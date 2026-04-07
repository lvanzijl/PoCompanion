# Production Certification — Rerun

## 1. Scenario Results

| Scenario | Evidence | Result |
| --- | --- | --- |
| Happy path | `HappyBindingChainScenario_IsExecutableEndToEnd` completed the full link/create/bind flow and finished with `OverallStatus = Complete` and all required counts valid. | Pass |
| Team invalid binding | API smoke on `team-assignment` returned `TEAM_BINDING_SOURCE_INVALID`; lookup smoke returned two team candidates; `TeamAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker` cleared the blocker. | Pass |
| Pipeline invalid binding | API smoke on `pipeline-assignment` returned `PIPELINE_BINDING_SOURCE_INVALID`; lookup smoke returned two pipeline candidates; `PipelineAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker` cleared the blocker. | Pass |
| Mixed graph | Temporary mixed-graph certification harness in `/tmp/onboarding-cert-check` produced simultaneous team and pipeline blockers, both with replacement intents, and returned deterministic output across two runs. | Pass |
| No false positives | Happy-path completion test finished with no remaining blockers and valid final counts. | Pass |

Executed evidence:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~Onboarding|FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`
- API smoke:
  - `GET http://localhost:5291/health`
  - `GET http://localhost:5291/api/onboarding/status` with `OnboardingVerification__SelectedScenario=team-assignment`
  - `GET http://localhost:5291/api/onboarding/lookups/projects/battleship-systems-project/teams?limit=10&offset=0`
  - `GET http://localhost:5291/api/onboarding/status` with `OnboardingVerification__SelectedScenario=pipeline-assignment`
  - `GET http://localhost:5291/api/onboarding/lookups/projects/battleship-systems-project/pipelines?limit=10&offset=0`
- Temporary mixed-graph harness:
  - output: `blocking-count=2`
  - output: `team-intent=replace-binding-source;team-action=Assign team to project`
  - output: `pipeline-intent=replace-binding-source;pipeline-action=Assign pipeline to project`
  - output: `deterministic=True`

## 2. Intent Consistency

Verified UI-side intent generation is consistent across all required entry points:

- problem cards
  - `Create_PipelineBindingAssignmentProblemsRouteToReplacementIntentWithReplacementLabels`
  - invalid existing pipeline binding emits `replace-binding-source`
- root-cause cards
  - same test verifies the root-cause group inherits `replace-binding-source`
- top-blocker summaries
  - same test verifies the top-blocker summary inherits `replace-binding-source`
- graph hints / `Next:` labels
  - `OnboardingWorkspace.razor` resolves graph/entity hints from `GetPrimaryProblemForTarget(...).SuggestedAction`
  - `OnboardingEntityCard.razor` renders `Next: @SuggestedAction`
  - therefore the graph hint uses the same corrected suggested action source as the validated problem model

Deterministic mapping rule confirmed:

- team invalid existing binding → `Assign team to project` → `replace-binding-source`
- pipeline invalid existing binding → `Assign pipeline to project` → `replace-binding-source`
- no `create-binding` intent remains for invalid existing team or pipeline bindings

## 3. Action-Zone Validation

Replacement mode validation passed.

Evidence:

- `OnboardingBindingActionModeResolverTests.Resolve_WhenIntentIsReplacement_ReturnsReplaceMode`
- `OnboardingBindingReplacementLookupServiceTests.GetCandidatesAsync_ReturnsTeamCandidatesThatExistInCurrentOnboardingContext`
- `OnboardingBindingReplacementLookupServiceTests.GetCandidatesAsync_ReturnsPipelineCandidatesThatExistInCurrentOnboardingContext`
- `OnboardingMutationActionZone.razor`
  - replacement mode is active when the binding action mode resolves to `Replace`
  - team replacement renders `Replacement team source`
  - pipeline replacement renders `Replacement pipeline source`
  - create-mode controls render only in the non-replacement branch

Result:

- replacement mode opens from the replacement intent
- selector is available for both team and pipeline replacements
- project-scoped replacement candidates are loaded
- no fallback to create mode occurs for invalid existing team or pipeline bindings

## 4. Execution Validation

Execution-layer validation passed.

Evidence:

- `OnboardingExecutionServiceTests.ReplaceBindingSourceAsync_SuccessfullyRefreshesWorkspaceWithSelectedTeamSource`
- `OnboardingExecutionServiceTests.ReplaceBindingSourceAsync_WhenBindingIsStale_RefreshesWorkspace`
- `TeamAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`
- `PipelineAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`

Observed contract:

- replacement execution uses the existing execution service path
- workspace data refreshes after mutation
- the corresponding blocker disappears after successful replacement
- no orphan binding state is left behind in the deterministic scenario flows

## 5. Determinism Check

Determinism validation passed.

Evidence:

- `SameScenario_ProducesSameResultsOnRerun`
- `MockConfigurationSeedHostedServiceTests.StartAsync_WhenTeamAssignmentScenarioSelected_SeedsReachableAssignmentBlocker`
- temporary mixed-graph harness returned identical serialized problem/intents across two runs

Verified invariants:

- same input → same blockers
- same blockers → same intents
- same intents → same execution result

No randomness or ordering instability was observed in the rerun evidence.

## 6. Test Results

Certification rerun validation command:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~Onboarding|FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Result:

- build passed
- onboarding certification suite passed
- 191 tests passed
- 0 failed
- 0 skipped

Additional deterministic support validation:

- team-assignment API smoke returned the expected invalid-binding blocker plus reachable replacement candidates
- pipeline-assignment API smoke returned the expected invalid-binding blocker plus reachable replacement candidates
- mixed-graph harness passed

## 7. Regression Check

Regression scan passed.

Confirmed no regression in:

- team assignment
  - team binding still emits replacement intent and clears correctly
- happy flow
  - full create/link/bind chain still reaches complete status
- UI read-only views
  - `OnboardingWorkspaceReadOnlyAuditTests` passed
- mutation layer
  - execution and mutation-path onboarding tests passed
- execution bridge
  - onboarding execution-intent, replacement lookup, and execution-service tests passed

## 8. Final Verdict

**PASS — Production Ready**

Blocking defect status:

- previous blocker fixed: invalid existing pipeline bindings no longer downgrade to `create-binding`
- no incorrect replacement fallback or intent inconsistency was found in the certification rerun evidence
