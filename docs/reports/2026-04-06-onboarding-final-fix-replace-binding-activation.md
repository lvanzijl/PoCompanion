# Final Fix — Replace Binding Activation

## 1. Failure Chain Confirmation

Confirmed from `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-06-onboarding-final-gate-verification-final.md` and reproduced before the fix:

- invalid team/pipeline binding blocker was visible
- `Go to fix location` navigated into the binding action zone
- the binding action zone still behaved like create-binding because the active navigation target resolved to the root instead of the binding
- replacement selector was missing in the exercised correction path
- deterministic verification failed with `Sequence contains no elements` in:
  - `TeamAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`
  - `PipelineAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`

Activation divergence was in:

- intent creation for binding-scoped assignment corrections
- action-zone mode selection
- replacement candidate activation/wiring
- deterministic scenario seeding for replacement alternatives

## 2. Intent Routing Fix

Implemented an explicit replacement intent for invalid binding correction:

- invalid team binding now routes to `replace-binding-source`
- invalid pipeline binding now routes to `replace-binding-source`

Also corrected the navigation target for binding-scoped replacement intents so:

- problem cards
- root-cause cards
- graph card action hints

all target the binding element itself (`binding-{id}`) inside the bindings section instead of downgrading to the root target.

## 3. Action Zone Mode Fix

Added deterministic binding action-mode resolution:

- `replace-binding-source` intent => replacement mode
- `create-binding` intent without an existing binding => create mode
- existing binding without replacement intent => update mode

The binding action zone now:

- renders replacement selector UI only in replacement mode
- hides create-binding controls while replacement intent is active
- stays in replacement mode even if binding context is missing, and shows explicit feedback instead of falling back to create mode

## 4. Candidate Lookup Fix

Replacement lookup is now bound to the active binding context:

- project context is required
- current binding is passed into lookup resolution
- team replacement queries only team candidates for the current project
- pipeline replacement queries only pipeline candidates for the current project
- current source is excluded from the candidate list
- disabled or invalid candidates are excluded

Deterministic assignment scenarios now seed one valid alternate team/pipeline source in the same project scope so replacement activation has a real correction target.

## 5. Empty-State Handling

Unsafe assumptions were removed from the replacement path:

- empty candidate sets remain a handled state
- the action zone stays in replacement context
- readable empty-state feedback is shown
- no fallback to create-binding mode occurs
- no exception is thrown

## 6. Mutation Dispatch Verification

Verified replacement dispatch remains intent-first:

- UI mutation still goes through `OnboardingExecutionService.ReplaceBindingSourceAsync`
- selected replacement source id and source type are carried in `ExecutionIntentViewModel`
- replacement dispatch uses binding update, not binding create
- successful replacement refreshes workspace data and removes the binding blocker

Browser verification on the team-assignment scenario confirmed:

- route changed to `onboardingIntentType=replace-binding-source`
- the replacement selector rendered with `Crew Safety Team`
- replace action succeeded
- the binding updated to `team-crew-safety`
- the binding-specific blocker disappeared

## 7. Test Results

Validated with:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingBindingReplacementLookupServiceTests|FullyQualifiedName~OnboardingBindingActionModeResolverTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingExecutionServiceTests|FullyQualifiedName~OnboardingVerificationScenarioTests" --logger "console;verbosity=minimal"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingBindingActionModeResolverTests|FullyQualifiedName~OnboardingBindingReplacementLookupServiceTests|FullyQualifiedName~OnboardingExecutionServiceTests|FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingVerificationScenarioTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingStatusServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Passing proof now includes:

- intent routing for team/pipeline invalid bindings
- deterministic action-zone mode selection
- team and pipeline replacement lookup candidates
- empty replacement state handling
- end-to-end deterministic scenarios:
  - `team-assignment`
  - `pipeline-assignment`
  - `happy-binding-chain`

## 8. Regression Check

Verified no regression in the targeted onboarding slice:

- create-binding flow still passes
- project linking still passes
- root creation still passes
- permission-denied handling still passes
- stale/not-found handling still passes
- tfs-unavailable and lookup-empty deterministic behavior still remains covered by the scenario suite

Release notes updated in `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`.

## 9. Ready for Final Gate Rerun

Ready for final gate rerun: **Yes**

Completion criteria satisfied:

- `team-assignment` passes
- `pipeline-assignment` passes
- `happy-binding-chain` still passes
- targeted onboarding regressions remain green
