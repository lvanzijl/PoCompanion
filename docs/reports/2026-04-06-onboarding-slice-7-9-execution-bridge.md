# Slice 7.9 — Execution Bridge

## 1. Execution Intent Model

- Added client-only `ExecutionIntentViewModel` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/OnboardingWorkspaceModels.cs`
- Added supporting client-only types:
  - `ExecutionIntentNavigationTargetViewModel`
  - `OnboardingExecutionConfidenceLevel`
  - `OnboardingExecutionIntentQueryKeys`
- Each intent now carries:
  - intent type
  - target scope
  - `connectionId`
  - `projectId`
  - `rootId`
  - `bindingId`
  - suggested action label
  - confidence level
  - navigation target route + anchor + expanded sections

Problem-to-intent examples:

| Problem / suggested action | Intent type | Target scope | Context |
| --- | --- | --- | --- |
| Create or select a connection | `configure-connection` | Global | connection |
| Link project to connection | `link-project` | Project | connection + project |
| Assign pipeline to project | `assign-pipeline` | Project | connection + project |
| Assign team to project | `assign-team` | Project | connection + project |
| Create binding for product root | `create-binding` | Binding | connection + project + root |
| Resolve product root validation issue | `resolve-root-validation` | Root | connection + project + root |
| Resolve validation issue | `resolve-validation` | Existing problem scope | fallback to visible context |

## 2. Confidence Classification

Confidence stays deterministic and client-only.

| Classification | Rule | Example |
| --- | --- | --- |
| High | Direct mapping with one clear execution surface | missing binding → `create-binding` |
| Medium | Deterministic mapping but multiple visible valid targets remain | assign pipeline when multiple visible pipelines exist |
| Fallback | Generic or unknown mapping | resolve validation issue |

Examples verified in tests:

- `Create binding for product root` → `High`
- `Assign pipeline to project` with 3 visible pipelines → `Medium`
- `Resolve validation issue` → `Fallback`

## 3. Navigation Behavior

Execution intents now deep-link into the onboarding workspace without enabling edits.

Behavior added:

- problem / root-cause cards now expose `Go to fix location`
- graph cards now expose `Open location`
- navigation target route is always `/home/onboarding` with onboarding-specific query parameters
- route carries:
  - intent type
  - target section
  - target element id
  - connection / project / root / binding context where available

Navigation examples:

- missing binding before: user had to inspect the graph manually
- missing binding after: route carries root context and opens the product-root section with the target root highlighted

- project link issue before: user knew the problem but not the execution surface
- project link issue after: route carries connection + project context and opens the project section with the target project highlighted

## 4. Context Pre-selection

The page now applies read-only pre-selection when an execution intent is opened.

Implemented:

- connection / project / root filters update automatically from the intent
- target graph entity is highlighted
- relevant graph sections expand
- the page scrolls to the target element

This stays session-local and is not persisted beyond the navigation state.

## 5. UI Enhancements

Updated UI components:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingProblemCard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingRootCauseCard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingEntityCard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingFutureActionZone.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`

Visible additions:

- confidence label on problem cards
- confidence label on root-cause cards
- `Go to fix location` on problem cards
- `Go to fix location` on root-cause cards
- `Open location` on graph entities
- graph target highlighting
- future action-zone placeholders in connection, project, team, pipeline, root, and binding sections

## 6. Mutation Preparation (Placeholders)

Added non-interactive future action zones using `OnboardingFutureActionZone`.

Purpose:

- reserve the future execution surface for Slice 8
- show where connection / project / team / pipeline / root / binding controls will appear
- keep the current slice read-only

Safeguards:

- no save/apply/create/delete/import buttons were enabled
- no backend calls were introduced for placeholders
- no fake edit forms or write states were introduced

## 7. Test Results

Serial validation run:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Result:

- Build: passed
- Targeted tests: passed
- Passed tests: 49
- Failed tests: 0

Coverage added in this slice:

- intent creation
- confidence classification
- route/context generation
- root-cause execution intent propagation
- read-only audit for write endpoint usage

## 8. Backend Impact (if any)

No backend changes.

No API DTO changes.

No backend logic changes.

No direct TFS calls.

All execution-bridge behavior is derived in client code from existing Slice 7 / 7.5 / 7.8 data.

## 9. Governance Compliance

- Read-only only: yes
- No backend redesign: yes
- No mutation paths: yes
- No write endpoints used: yes
- Feature flag confinement: `FeatureFlags:OnboardingWorkspace`
- Guidance bridged to execution context, but execution itself remains disabled

Confirmation:

- no backend changes
- no write paths
