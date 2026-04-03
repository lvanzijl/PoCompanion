# Batch 02 — Bug Detail removal

## 1. Scope removed

Removed the standalone **Bug Detail** feature centered on route `/home/bugs/detail`.

Removed in this batch:

- client route/page `PoTool.Client/Pages/Home/BugDetail.razor`
- route constant `WorkspaceRoutes.BugDetail`
- workspace navigation registration and test coverage for the Bug Detail route
- Bug Detail-only client work-item read methods:
  - `WorkItemService.GetByTfsIdAsync`
  - `WorkItemService.GetByTfsIdStateAsync`
- the dedicated API/query path used only by Bug Detail:
  - `GET /api/workitems/{tfsId}`
  - `GetWorkItemByIdQuery`
  - `GetWorkItemByIdQueryHandler`
  - `GetWorkItemByIdQueryValidator`
  - `GetWorkItemByIdQueryValidatorTests`
- governed OpenAPI and generated client entries for `GET /api/workitems/{tfsId}`

Not removed in this batch because still shared:

- `BugOverview.razor`
- `BugsTriage.razor`
- shared bug/work-item repository and read-provider logic used by other features
- generic work-item retrieval methods used internally by validation, release-planning, and update handlers

## 2. Files deleted

- `PoTool.Client/Pages/Home/BugDetail.razor`
- `PoTool.Core/WorkItems/Queries/GetWorkItemByIdQuery.cs`
- `PoTool.Core/WorkItems/Validators/GetWorkItemByIdQueryValidator.cs`
- `PoTool.Api/Handlers/WorkItems/GetWorkItemByIdQueryHandler.cs`
- `PoTool.Tests.Unit/Validators/GetWorkItemByIdQueryValidatorTests.cs`

## 3. Files modified

- `PoTool.Api/Controllers/WorkItemsController.cs`
- `PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- `PoTool.Client/ApiClient/OpenApi/swagger.json`
- `PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`
- `PoTool.Client/Models/WorkspaceRoutes.cs`
- `PoTool.Client/Services/WorkItemService.cs`
- `PoTool.Tests.Unit/Components/Common/WorkspaceNavigationCatalogTests.cs`
- `docs/analysis/filter-analysis.md`
- `docs/architecture/navigation-map.md`
- `docs/release-notes.json`

## 4. Backend/API impact

Removed backend/API surface proven exclusive to Bug Detail:

- controller action for `GET /api/workitems/{tfsId}`
- mediator query `GetWorkItemByIdQuery`
- handler `GetWorkItemByIdQueryHandler`
- validator `GetWorkItemByIdQueryValidator`
- generated OpenAPI/client contract for the endpoint

Retained backend/API surface because it is still shared:

- work-item repository/provider methods `GetByTfsIdAsync(...)` used internally by:
  - validation flows
  - release-planning handlers/repositories
  - backlog-priority / iteration-path / effort update handlers
  - work-item-with-validation and timeline handlers
- bug overview and bug triage APIs
- write endpoints for bug/work-item updates used outside Bug Detail

Reason for retention:

- the removed page depended on a dedicated read endpoint, but the underlying work-item read infrastructure is still used by multiple active features and handlers.

## 5. Shared pieces retained

Retained as shared infrastructure, not Bug Detail residue:

- `PoTool.Client/Pages/Home/BugOverview.razor`
- `PoTool.Client/Pages/BugsTriage.razor`
- `PoTool.Client/Services/WorkItemService.cs` remaining methods
- `PoTool.Api/Repositories/WorkItemRepository.cs`
- `PoTool.Api/Services/CachedWorkItemReadProvider.cs`
- `PoTool.Api/Services/LiveWorkItemReadProvider.cs`
- `PoTool.Api/Services/LazyWorkItemReadProvider.cs`
- `PoTool.Core/Contracts/IWorkItemRepository.cs`
- `PoTool.Core/Contracts/IWorkItemReadProvider.cs`
- shared DTOs such as `PoTool.Shared/WorkItems/WorkItemDto.cs`

These remain because Bug Overview, Bug Triage, validation features, and release-planning logic still depend on shared work-item and bug infrastructure.

## 6. Residual reference check

### Code and governed contract sweep

Verified no remaining code references in `PoTool.Client`, `PoTool.Api`, `PoTool.Core`, or `PoTool.Tests.Unit` for:

- `BugDetail`
- `/home/bugs/detail`
- `WorkspaceRoutes.BugDetail`
- `GetByTfsIdStateAsync(`
- `GetWorkItemByIdQuery`
- `GetWorkItemByIdQueryHandler`
- `GetWorkItemByIdQueryValidator`
- `WorkItems_GetByTfsId`

### UI navigation reachability

- no UI navigation can still reach `/home/bugs/detail`
- the route constant was removed
- the page file was deleted
- workspace active-route registration was removed
- no remaining client links or references to `/home/bugs/detail` were found

### Orphan endpoint / client contract check

- no API endpoint remains for `GET /api/workitems/{tfsId}`
- no generated client method remains for that endpoint
- no Bug Detail-only client service methods remain

### Historical documentation references retained

Historical analysis/report artifacts still mention Bug Detail, including:

- `docs/analysis/assets/2026-04-02-ux-full-scan/scan-results.json`
- `docs/analysis/2026-04-02-ux-full-scan-report.md`
- older dated reports under `docs/reports/`

These were retained as historical records rather than current guidance. Active documentation was updated.

## 7. Risks / follow-ups

1. Historical reports still reference Bug Detail. If repository policy later requires stricter archival separation, move or annotate those dated artifacts explicitly.
2. The internal repository/provider `GetByTfsIdAsync(...)` methods remain because they are shared by active backend logic. Only the Bug Detail-specific public query/API surface was removed.
3. Manual edits were applied to the governed OpenAPI snapshot and generated client to keep the removal reviewable within this batch. A future controlled snapshot refresh can regenerate them from a running API if desired.

## 8. Validation results

### Route removal

- `/home/bugs/detail` is gone:
  - `BugDetail.razor` deleted
  - no remaining route constant or client route registration found

### Navigation and references

- no UI navigation can still reach `/home/bugs/detail`
- no remaining route/constants reference it in client/API/core/test code

### Backend/API

- no orphan API endpoint remains for Bug Detail
- no orphan query/handler/validator remains for Bug Detail
- no orphan client service methods remain for Bug Detail

### Build and test validation

- `dotnet build PoTool.sln --configuration Release --nologo` passed after cleanup
- `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"` passed after cleanup
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~NswagGovernanceTests"` passed after cleanup

### Validation issues encountered

- An initial baseline build/test run executed build and test in parallel and produced file-lock failures in release outputs and WebAssembly static assets.
- This was resolved by rerunning validation sequentially; no code change was required.

### Bug Overview and Bug Triage

- `PoTool.Client/Pages/Home/BugOverview.razor` remains intact
- `PoTool.Client/Pages/BugsTriage.razor` remains intact
- no code changes were made to Bug Overview or Bug Triage behavior

### Release notes

- updated: `docs/release-notes.json`
