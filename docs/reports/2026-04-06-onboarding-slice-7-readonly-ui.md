# Slice 7 — Read-only UI

## 1. Scope Confirmation

Included and implemented:

- read-only onboarding workspace UI
- visualization of onboarding entities and relationships
- validation state visibility
- status visibility
- filter/navigation within the onboarding workspace
- feature-flag protection for the new UI surface

Explicitly excluded and unchanged:

- no create/update/delete actions
- no import UI
- no migration execution UI
- no onboarding wizard behavior introduced in the new workspace
- no direct TFS access from UI
- no backend API expansion; only existing onboarding read endpoints were consumed through the regenerated governed client snapshot

## 2. Workspace Structure

New route:

- `/home/onboarding`

New page:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`

New supporting UI components:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingEntityCard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingStatusBadge.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingValidationBadge.razor`

New supporting client models/services:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/OnboardingWorkspaceModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/IOnboardingWorkspaceService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWorkspaceService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWorkspaceViewModelFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/IFeatureFlagService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/FeatureFlagService.cs`

Workspace composition:

- global summary panel for overall status, per-flow status, blockers, warnings, and counts
- filter bar for connection, project, product root, and status
- read-only entity sections for:
  - Connections
  - Projects
  - Teams
  - Pipelines
  - Product Roots
  - Bindings
- grouped relationship rendering:
  - teams/pipelines/roots grouped by project
  - bindings grouped by product root with project context

## 3. Data Consumption

Consumed existing backend endpoints only:

- `GET /api/onboarding/status`
- `GET /api/onboarding/connections`
- `GET /api/onboarding/projects`
- `GET /api/onboarding/teams`
- `GET /api/onboarding/pipelines`
- `GET /api/onboarding/roots`
- `GET /api/onboarding/bindings`

Filtering behavior:

- connection/project/product-root/status filters are passed back to the existing CRUD read endpoints
- filter-option dropdowns are also sourced from backend reads, not client-reconstructed data
- the client does not recompute status
- the client does not repair or rebuild invalid graphs outside backend-filtered grouping for display

Client contract work:

- refreshed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
- regenerated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- added onboarding generated-client JSON configuration in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.Extensions.cs`
- registered `IOnboardingCrudClient`, `IOnboardingLookupClient`, and `IOnboardingStatusClient` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`

## 4. Read-only UX Rules

Read-only enforcement implemented by design:

- workspace header explicitly states the view is read-only
- no create/update/delete/import/save/apply controls were added
- only safe controls are present:
  - filters
  - expand/collapse sections
  - refresh

Read-only evidence:

- no mutation button labels or wizard references in the new workspace Razor files
- `OnboardingWorkspaceReadOnlyAuditTests` enforces absence of visible mutation affordances and direct `HttpClient` usage in the new UI files

## 5. Visibility and Graph Representation

Per-entity visibility:

- identity/display name shown on every card
- external identity shown where applicable
- validation state shown on every entity card
- status shown on every entity card
- parent context shown on child entities and bindings

Relationship clarity:

- teams grouped by parent project
- pipelines grouped by parent project
- product roots grouped by parent project
- bindings grouped by parent product root and annotated with project context
- binding cards show:
  - root context
  - source type
  - source target/external id
  - project context
  - validation/status state

Soft-deleted entities remain hidden because the workspace only consumes the existing Slice 6.5 filtered read endpoints.

## 6. Filtering and Navigation

Implemented filters:

- connection
- project
- product root
- status

Behavior:

- all filters round-trip through existing onboarding read endpoints
- project/root filter options are refreshed from backend reads using the current parent filter context
- empty states are explicit for each section
- new navigation entry is added only when the feature flag is enabled

Routing/navigation changes:

- added `WorkspaceRoutes.OnboardingWorkspace`
- added feature-flag-aware onboarding item in `WorkspaceNavigationCatalog`
- updated `WorkspaceNavigationBar` to hide/show the route based on the feature flag

## 7. Error / Empty States

Handled explicitly:

- loading
- no onboarding data
- partially configured state
- blocking state
- warning state
- API failure

Rendered-state evidence:

- `OnboardingWorkspaceViewModelFactoryTests` covers:
  - no data / empty state
  - partial configuration summary
  - grouped relationship rendering
  - failed state mapping
- no screenshot was committed; rendered-state evidence is captured by the targeted unit tests because the new workspace is intentionally feature-flagged off by default in client configuration

## 8. Feature Flag Behavior

Implemented flag:

- `FeatureFlags:OnboardingWorkspace`

Client wiring:

- configured in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/wwwroot/appsettings.json`
- resolved through `FeatureFlagService`

Confirmed behavior:

- flag off:
  - no onboarding workspace navigation item
  - direct route navigation is redirected back to home
- flag on:
  - onboarding workspace navigation item is visible
  - onboarding workspace route is reachable

Automated coverage:

- `FeatureFlagServiceTests`
- `WorkspaceNavigationCatalogTests`

## 9. Test Results

Validation commands:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Targeted test result:

- Passed: 37
- Failed: 0
- Skipped: 0

Covered tests:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/FeatureFlagServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/OnboardingWorkspaceServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/OnboardingWorkspaceViewModelFactoryTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/OnboardingWorkspaceReadOnlyAuditTests.cs`
- updated route/navigation coverage:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Components/Common/WorkspaceNavigationCatalogTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Models/WorkspaceRoutesTests.cs`

## 10. Governance Compliance

Confirmed:

- no write actions introduced
- no import UI introduced
- no migration UI introduced
- no new wizard/session behavior introduced for the new workspace
- no direct TFS calls from UI
- no backend API expansion beyond existing read contracts
- feature flag protection added for the new UI surface
- release notes updated in `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

Intentional non-changes:

- existing backend onboarding behavior was not redesigned
- existing onboarding wizard/session code outside this slice was not refactored as part of Slice 7
- no write-side onboarding UI was added
