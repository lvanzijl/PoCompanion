# Slice 7.8 — Actionability Layer

## 1. Actionability Model

Slice 7.8 adds a client-only `ActionableProblemViewModel` layer on top of the existing read-only onboarding workspace projection.

Derived only from existing data:

- onboarding status engine output
- entity validation state
- visible entity relationships already returned by the workspace read

Each actionable problem now includes:

- title
- scope
- reason
- severity
- suggested action
- expected impact
- root-cause grouping key

New client-side models:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/OnboardingWorkspaceModels.cs`
  - `ActionableProblemViewModel`
  - `OnboardingRootCauseGroupViewModel`
  - updated `OnboardingProblemSummaryViewModel`

## 2. Suggested Action Mapping

Deterministic mapping lives in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingActionSuggestionService.cs`

The service is pure UI-side mapping only.

Mapping table:

| Visible validation/status pattern | Suggested action |
| --- | --- |
| missing/required connection | create or select a connection |
| permission denied / insufficient permissions | grant or resolve required read permissions |
| capability denied / missing capabilities | enable the required connection capabilities |
| project mapping / project not linked | link project to connection |
| missing or unassociated pipeline | assign pipeline to project |
| team source issue | assign team to project |
| binding issue | create binding for product root |
| root issue / root metadata issue | resolve product root validation issue |
| unknown issue | resolve validation issue |

Properties of the mapping:

- deterministic
- transparent
- no mutation logic
- no “click here” instructions
- fallback remains generic and safe

## 3. Impact Statements

Impact is derived only from visible graph data already loaded into the workspace.

Examples now shown in the UI:

- `Affects 2 team source(s), 1 root(s), and 3 binding(s) in this project.`
- `Blocks 3 binding(s) for this root.`
- `Prevents this binding from being usable.`

Implementation details:

- connection-level impact counts visible projects, teams, pipelines, roots, and bindings
- project-level impact counts visible downstream team, pipeline, root, and binding items
- root-level impact counts visible bindings
- team, pipeline, and binding items remain scoped to their visible entity only

No hidden dependency computation was introduced.

## 4. Root Cause Grouping

Root causes are now grouped deterministically by:

- same visible reason text
- same upstream entity target

Examples:

- repeated binding symptoms under the same root and same reason collapse into one root cause
- repeated team/pipeline symptoms under the same project and same reason collapse into one root cause

Behavior:

- the root cause remains visible as the primary actionable item
- grouped child issues remain available in expanded `Derived issues` views
- top blockers now prioritize root causes instead of repeated duplicate symptoms

Client files involved:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWorkspaceViewModelFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingRootCauseCard.razor`

## 5. Problem-first View Enhancements

Slice 7.5 problem-first view was enhanced, not replaced.

New problem-first behavior:

- top blockers are now root-cause aware
- each blocker/warning shows:
  - suggested action
  - expected impact
  - grouped root-cause context
- new `Root causes` section lists all grouped root issues
- expanded root causes reveal `Derived issues`
- `Derived issues` remain grouped by scope so individual symptoms are still inspectable

Updated/new UI components:

- new `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingRootCauseCard.razor`
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingProblemCard.razor`
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`

## 6. Graph View Enhancements

Graph view now adds minimal action hints without overloading the existing Slice 7 structure.

Added in graph view:

- short `Next:` action hint on entities with visible actionable problems
- `Part of root cause:` hint when the current entity is a grouped symptom under a higher upstream cause
- existing blocker/warning emphasis remains intact

This remains read-only:

- no mutation buttons
- no import affordances
- no write navigation

## 7. Test Results

Added/updated tests:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/OnboardingActionSuggestionServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/OnboardingWorkspaceViewModelFactoryTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/OnboardingWorkspaceReadOnlyAuditTests.cs`

Covered scenarios:

- known validation/status patterns map to the expected suggestion
- unknown patterns use the fallback suggestion
- impact statements are derived from visible graph data only
- repeated issues group into a single root cause
- expanded root causes still expose underlying issues
- read-only audit still blocks write affordances and wizard/direct `HttpClient` leakage

Validation commands:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Result:

- Passed: 45
- Failed: 0
- Skipped: 0

## 8. Backend Impact (if any)

Backend impact: none.

Confirmed:

- no backend API changes
- no backend logic changes
- no new DTOs
- no new read queries
- no direct TFS calls from the client

All Slice 7.8 behavior is a client-side projection over existing Slice 7 / 7.5 data.

## 9. Governance Compliance

Compliance summary:

- read-only only
- no backend redesign
- no mutation paths
- no create/update/delete/import actions
- no wizard/session flows
- feature flag remains `FeatureFlags:OnboardingWorkspace`

Before vs after:

- before:
  - users could see what was broken, where, and why
  - repeated symptoms could still feel noisy
  - next action and visible unblock impact were not explicit
- after:
  - users can see what to do next
  - users can see what a fix will unblock
  - repeated symptoms are grouped under shared root causes
  - the graph reinforces the same guidance with minimal action hints

Confirmation:

- no backend changes
- no write paths
