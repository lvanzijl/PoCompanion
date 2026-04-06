# Slice 8 — Mutation Layer

## 1. Scope Confirmation

Slice 8 scope was implemented as the write-capable onboarding layer that starts from the existing execution bridge and executes safe onboarding mutations through existing onboarding CRUD endpoints.

Included:
- write-capable onboarding UI behind `FeatureFlags:OnboardingWorkspace`
- execution of mutations through `ExecutionIntentViewModel`
- mutation surfaces for connection, project, team, pipeline, product root, and binding
- validation before execution
- explicit success and failure feedback
- authoritative read-model refresh after mutation

Explicitly excluded:
- no import UI
- no migration execution UI
- no cutover or routing redesign
- no wizard or session-state behavior
- no direct UI-side CRUD outside intent flow
- no direct TFS access from UI

## 2. Intent-first Execution Model

Implemented client execution flow:

1. user reaches a write surface only from an existing execution intent (`Go to fix location` / `Open location`)
2. the onboarding workspace resolves the active `ExecutionIntentViewModel` from route context
3. only the matching action zone enables write controls
4. the action zone calls `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingExecutionService.cs`
5. the execution service validates intent + local input + required context
6. the service calls the typed generated onboarding CRUD client
7. on success, the service refreshes the workspace read model through `IOnboardingWorkspaceService`
8. the page rebuilds graph and problem-first projections from refreshed read data

Updated client components and services:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingMutationActionZone.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingFutureActionZone.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingExecutionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/OnboardingWorkspaceModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`

## 3. Mutation Surfaces

Mutation-capable action zones now exist for:

- Connection
  - create connection
  - update allowed connection settings
  - soft delete with reason
- Project
  - create/link project under the selected connection
  - update allowed project metadata
  - soft delete with reason
- Team
  - add team under the selected project
  - update allowed team metadata
  - soft delete with reason
- Pipeline
  - add pipeline under the selected project
  - update allowed pipeline metadata
  - soft delete with reason
- Product Root
  - add root under the selected project
  - update allowed root metadata
  - soft delete with reason
- Binding
  - create binding for the selected root and source type
  - update enabled state only
  - soft delete with reason

Backend endpoints consumed:
- `POST /api/onboarding/connections`
- `PUT /api/onboarding/connections/{id}`
- `DELETE /api/onboarding/connections/{id}`
- `POST /api/onboarding/projects`
- `PUT /api/onboarding/projects/{id}`
- `DELETE /api/onboarding/projects/{id}`
- `POST /api/onboarding/teams`
- `PUT /api/onboarding/teams/{id}`
- `DELETE /api/onboarding/teams/{id}`
- `POST /api/onboarding/pipelines`
- `PUT /api/onboarding/pipelines/{id}`
- `DELETE /api/onboarding/pipelines/{id}`
- `POST /api/onboarding/roots`
- `PUT /api/onboarding/roots/{id}`
- `DELETE /api/onboarding/roots/{id}`
- `POST /api/onboarding/bindings`
- `PUT /api/onboarding/bindings/{id}`
- `DELETE /api/onboarding/bindings/{id}`

No unsupported write semantics were added.

## 4. Validation-before-Write

Validation now runs in two layers:

- local client validation in `OnboardingExecutionService`
  - correct execution-intent section
  - required context present
  - required IDs and delete reasons present
  - required local input shape present
  - fallback-confidence writes blocked when required context cannot be resolved
- existing Slice 6 / Slice 2 backend validation through the onboarding CRUD API

The UI does not assume success and does not apply optimistic local truth.

## 5. Success / Failure Feedback

Success behavior:
- explicit success alert rendered in the onboarding workspace
- mutation-specific success message returned from the execution service
- workspace read model reloaded immediately
- graph view and problem-first projections rebuilt from refreshed data
- active filters preserved or corrected to the refreshed entity scope

Failure behavior:
- explicit error alert rendered in the active action zone and workspace
- backend validation / dependency / permission / not-found errors surfaced from `OnboardingErrorDto`
- no false success state
- no hidden local mutation
- not-found failures refresh workspace data to remove stale state when possible

Examples:
- success: creating a team shows a success alert and reloads the project-scoped onboarding graph
- failure: backend validation failure keeps the user in the same action zone and shows the sanitized backend reason

## 6. Context Preservation

Context preservation behavior:
- active connection / project / root filters stay aligned with the execution intent
- expanded sections remain aligned with the action zone section
- highlighted graph target stays visible after navigation and after refreshed reads
- successful writes keep the user in the relevant onboarding workspace context instead of redirecting away

For destructive operations that remove the current root or project filter target, the filter is cleared only for the removed scope so the user does not remain pinned to deleted data.

## 7. Mutation Safety Rules

Implemented safety rules:
- no write controls are enabled without an active execution intent
- action zones only enable when the active intent matches the section and local context
- fallback-confidence binding writes are blocked when required project/root context is missing
- delete operations require a reason before any API call
- all deletes use existing Slice 6 soft-delete endpoints only
- no bulk mutation behavior
- no direct CRUD client usage in workspace UI outside `OnboardingExecutionService`

## 8. Read Model Refresh

Refresh strategy after mutation:
- mutations execute through typed onboarding CRUD clients
- refreshed data comes from `IOnboardingWorkspaceService`
- refreshed workspace data includes:
  - onboarding status
  - connections
  - projects
  - teams
  - pipelines
  - product roots
  - bindings
  - rebuilt graph sections
  - rebuilt problem-first/root-cause projections

The backend remains the only source of truth; no client shadow graph is persisted.

## 9. Error Mapping

Backend write failures now map back into visible onboarding context by:
- keeping the user in the same action zone
- showing the backend message and details from `OnboardingErrorDto`
- preserving local inputs on failure
- refreshing stale state for not-found failures

Failure categories surfaced without new backend semantics:
- validation failure
- dependency violation / conflict
- permission denied
- not found
- temporary backend unavailability

## 10. Test Results

Validation run:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingActionSuggestionServiceTests|FullyQualifiedName~OnboardingExecutionIntentServiceTests|FullyQualifiedName~OnboardingExecutionServiceTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests|FullyQualifiedName~OnboardingCrudServiceTests|FullyQualifiedName~OnboardingValidationServiceTests|FullyQualifiedName~OnboardingStatusServiceTests" --logger "console;verbosity=minimal"`

Result:
- Build: passed
- Targeted tests: passed
- Passed tests: 87
- Failed tests: 0

Added / updated coverage includes:
- mutation can only start from matching execution intent
- invalid local input blocked before backend call
- backend validation failure surfaced cleanly
- successful mutation refreshes workspace data
- not-found failure refreshes stale workspace data
- delete requires reason
- non-action-zone files do not expose write controls
- workspace UI does not bypass the execution service or use direct TFS lookup clients

## 11. Backend Impact (if any)

No backend changes.

No backend contract redesign.

No backend logic changes.

No new endpoints.

Slice 8 consumes the existing Slice 6 onboarding CRUD API only.

## 12. Governance Compliance

- No mutation outside execution intent: yes
- No backend redesign: yes
- No direct TFS access: yes
- Validation before write: yes
- Explicit success/failure feedback: yes
- Read-model refresh after successful mutation: yes
- No direct CRUD bypass: yes
- No wizard/session behavior: yes
- No hidden write paths: yes

Confirmation:
- no direct CRUD bypass
- no direct TFS calls
- no wizard/session behavior
- no hidden write paths
