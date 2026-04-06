# Final Fix — Assignment Correction

## 1. Problem Summary

The final onboarding gate still failed `team-assignment` and `pipeline-assignment` because the binding action zone could route users into the binding correction context but could not expose a usable replacement source or execute an in-place source replacement through the existing execution path.

## 2. Implementation

- Added a typed client-side binding replacement lookup service that calls the existing onboarding lookup endpoints for teams and pipelines and intersects live lookup results with the current onboarding project context.
- Extended `ExecutionIntentViewModel` with optional replacement-source payload fields so the selected replacement source id and source type travel through the execution flow without direct UI CRUD.
- Updated the binding action zone to load replacement candidates for invalid team and pipeline bindings, show clear empty/error states, allow selection in the same surface, and execute replacement through `OnboardingExecutionService`.
- Added `ReplaceBindingSourceAsync` in `OnboardingExecutionService` and completed backend `UpdateBindingAsync` support for in-place team and pipeline source replacement with validation, conflict checks, and standard refresh handling.
- Updated release notes for the user-visible onboarding fix.

## 3. Execution Flow

1. User opens an assignment blocker fix location that targets an invalid binding.
2. The binding action zone loads authoritative team or pipeline lookup candidates for the current project context.
3. The user selects a replacement candidate in the binding action zone.
4. The action zone enriches the active execution intent with the selected replacement source.
5. `OnboardingExecutionService.ReplaceBindingSourceAsync` sends the mutation through the existing onboarding CRUD update path.
6. The backend validates scope, existence, and duplicate-binding constraints, updates the binding source, revalidates the binding, and returns the standard success or failure envelope.
7. On success, the workspace refreshes graph data, problem-first projections, counts, and status so the resolved blocker disappears without navigation.

## 4. Test Results

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~OnboardingBindingReplacementLookupServiceTests|FullyQualifiedName~OnboardingExecutionServiceTests|FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingVerificationScenarioTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests" --logger "console;verbosity=minimal"`

Results:

- build passed
- targeted onboarding tests passed (`36` passed, `0` failed)
- verification scenario coverage now includes successful end-to-end correction for both `team-assignment` and `pipeline-assignment`

## 5. Regression Check

- `happy-binding-chain` remains covered by `OnboardingVerificationScenarioTests`
- project linking, root creation, and binding creation regression coverage remains in existing onboarding execution and CRUD tests
- no onboarding API contract changes were introduced
- mutation boundaries remain unchanged: UI still routes through execution intents and `OnboardingExecutionService`

## 6. Final Gate Readiness

The assignment correction surface is now complete for team and pipeline binding blockers. Based on the updated verification scenario coverage and targeted onboarding regression suite, the final gate is ready to rerun with the remaining assignment scenarios expected to pass.
