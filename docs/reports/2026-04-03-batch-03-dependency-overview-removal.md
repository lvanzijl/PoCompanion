# Batch 3 — Dependency Overview removal

## 1. Scope removed

- Removed the standalone Dependency Overview page at `/home/dependencies`.
- Removed the dedicated client route constant and workspace active-route registration that kept the page classified as an active modern route.
- Removed the page-local and legacy visible links to `/dependency-graph` that pointed to a non-routable client page.

## 2. Files deleted

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DependencyOverview.razor`

## 3. Files modified

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/navigation-map.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/gebruikershandleiding.md`
- `/home/runner/work/PoCompanion/PoCompanion/features/Dependency_graph.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 4. Backend/API impact

- No backend dependency-graph endpoint, query, handler, DTO, or repository/query method was removed in this batch.
- The dependency graph stack remains required by shared consumers:
  - `DependenciesPanel` still calls `GetDependencyGraphAsync`.
  - `RoadmapAnalyticsService` still calls `GetDependencyGraphAsync` to derive dependency signals.
  - The API endpoint `GET /api/workitems/dependency-graph` still delegates to `GetDependencyGraphQuery`.
- Result: there are no orphan backend artifacts created by removing Dependency Overview.

## 5. Shared pieces retained

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapAnalyticsService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/WorkItems/Queries/GetDependencyGraphQuery.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/IWorkItemQuery.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/EfWorkItemQuery.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/DependencyGraphDto.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RoadmapAnalyticsServiceTests.cs`

These remain because they are shared dependency-analysis infrastructure, not Dependency Overview-only code.

## 6. Residual reference check

### Removed from live code / active navigation

- `/home/dependencies`
- `WorkspaceRoutes.DependencyOverview`
- `DependencyOverview.razor`
- visible `/dependency-graph` links from:
  - the removed Dependency Overview page
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor`

### Retained intentionally

- `/api/workitems/dependency-graph` references in API, generated client, tests, and dependency-analysis documentation because the shared dependency graph capability still exists.

### Historical references left in place

- Prior analysis and report artifacts under `docs/analysis/**` and older dated files under `docs/reports/**` still mention `/home/dependencies` and the removed page for historical traceability.

## 7. Risks / follow-ups

- `DependenciesPanel` and the dependency-graph API remain in use and should be evaluated again only when the remaining dependency-analysis consumers are removed or replaced.
- Historical analysis/report files still mention the removed route; this is intentional and not active navigation guidance.
- This batch did not remove broader dependency-analysis functionality because that would exceed the approved scope.

## 8. Validation results

- Verified `/home/dependencies` no longer exists in client code.
- Verified no UI navigation or workspace catalog references to `/home/dependencies` remain.
- Verified no live client `.razor` or `.cs` links to `/dependency-graph` remain.
- Verified no orphan API endpoint or DTO/service removal was required because shared consumers still reference the dependency graph stack.
- Updated active documentation and release notes for the removed page.
- Validation commands:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`
- Result: build passed; filtered tests passed.

Release notes: updated
