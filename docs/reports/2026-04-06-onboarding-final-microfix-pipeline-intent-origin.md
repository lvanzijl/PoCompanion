# Final Micro-Fix — Pipeline Intent Origin

## 1. Defect Confirmation

- invalid pipeline binding blocker was visible in the certification run
- blocker/root-cause entry path still emitted `create-binding`
- it should have emitted `replace-binding-source`
- the wrong intent opened create-binding mode and hid the replacement selector
- backend/service replacement execution was already proven to work in isolation

## 2. Intent-Origin Audit

Audited UI-side intent/suggestion origins for invalid existing pipeline bindings:

- `PoTool.Client/Services/OnboardingActionSuggestionService.cs`
  - current defect source
  - invalid pipeline-binding messages containing `pipeline source` fell through to the generic `binding` rule
  - emitted suggestion: `Create binding for product root`
- `PoTool.Client/Services/OnboardingExecutionIntentService.cs`
  - already correct
  - `Assign pipeline to project` with a binding id already maps to `replace-binding-source`
- `PoTool.Client/Services/OnboardingWorkspaceViewModelFactory.cs`
  - already centralizes problem-card, root-cause-card, and top-blocker label/intent creation through the action-suggestion and execution-intent services
  - no separate conflicting pipeline-specific mapping was found here

Result:

- only the action-suggestion mapping needed correction
- once corrected, problem cards, root-cause cards, and top-blocker summaries all inherit the replacement intent and binding target consistently

## 3. Mapping Fix

Implemented one deterministic rule for invalid existing pipeline bindings:

- messages containing `pipeline source` now resolve to `Assign pipeline to project`
- with an existing binding id, `OnboardingExecutionIntentService` deterministically emits:
  - `replace-binding-source`
  - binding scope
  - bindings section
  - `binding-{id}` anchor target

No fallback remains for this path to:

- `create-binding`
- generic validation resolution
- root-level create flow

## 4. Label Alignment

Visible labels now align with replacement behavior for invalid existing pipeline bindings:

- problem card suggested action: `Assign pipeline to project`
- root-cause card suggested action: `Assign pipeline to project`
- top-blocker summary suggested action: `Assign pipeline to project`

Added regression assertions proving `Create binding for product root` no longer appears for that invalid existing pipeline-binding path.

## 5. Regression Check

Verified unchanged/correct behavior:

- team invalid-binding path still suggests `Assign team to project`
- team invalid-binding path still emits `replace-binding-source`
- `OnboardingExecutionIntentService` pipeline replacement mapping remains unchanged and correct
- deterministic scenario tests still pass for:
  - `HappyBindingChainScenario_IsExecutableEndToEnd`
  - `TeamAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`
  - `PipelineAssignmentScenario_CanReplaceInvalidBindingAndClearBlocker`

Also updated deterministic support coverage:

- `MockConfigurationSeedHostedServiceTests.StartAsync_WhenTeamAssignmentScenarioSelected_SeedsReachableAssignmentBlocker`
  - now asserts the current reachable replacement fixture shape with two team sources

## 6. Test Results

Validated with:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~MockConfigurationSeedHostedServiceTests|FullyQualifiedName~OnboardingVerificationScenarioTests|FullyQualifiedName~OnboardingBindingActionModeResolverTests|FullyQualifiedName~OnboardingBindingReplacementLookupServiceTests|FullyQualifiedName~OnboardingExecutionServiceTests" --logger "console;verbosity=minimal"`

Result:

- build passed
- targeted onboarding micro-fix suite passed
- 56 targeted tests passed, 0 failed

Coverage added/fixed:

- invalid pipeline binding suggestion now resolves to replacement action
- invalid pipeline binding problem card/root-cause/top-blocker all emit replacement intent on the binding target
- team replacement label/intent remains unchanged
- deterministic team-assignment seed-shape support test now matches the reachable replacement fixture

## 7. Ready for Production Certification Rerun

Ready for production certification rerun: **Yes**

Completion criteria satisfied:

- pipeline-assignment blocker entry path now emits `replace-binding-source`
- no misleading create-binding label remains for that invalid existing pipeline-binding path
- deterministic support suite is green
