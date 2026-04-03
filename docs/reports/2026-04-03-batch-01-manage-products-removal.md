# Batch 01 — Manage Products removal

## 1. Scope removed

Removed the standalone **Manage Products** feature centered on route `/settings/products`.

Removed in this batch:

- client route/page `PoTool.Client/Pages/Settings/ManageProducts.razor`
- legacy in-app link from the hidden Product workspace to `/settings/products`
- product-orphan read path used only by the deleted page:
  - `GET /api/products/orphans`
  - `GetOrphanProductsQuery`
  - `GetOrphanProductsQueryHandler`
  - `IProductRepository.GetOrphanProductsAsync`
  - `ProductRepository.GetOrphanProductsAsync`
  - `ProductService.GetOrphanProductsAsync`
  - generated client and governed OpenAPI entries for the orphan endpoint

Not removed in this batch because still shared:

- product CRUD and assignment flows used by `ManageProductOwner.razor`
- `ProductEditor.razor`
- shared product DTOs, entities, repository, controller, and service methods used by onboarding, planning, metrics, validation, and work-item flows

## 2. Files deleted

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Settings/ManageProducts.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Queries/GetOrphanProductsQuery.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetOrphanProductsQueryHandler.cs`

## 3. Files modified

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/ProfileTile.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/IProductRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/filter-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/gebruikershandleiding.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/implementation/tfs-cache-implementation-plan.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/implementation/ui-migration-plan.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 4. Backend/API impact

Removed backend/API surface proven exclusive to the deleted page:

- controller action for `GET /api/products/orphans`
- mediator query `GetOrphanProductsQuery`
- handler `GetOrphanProductsQueryHandler`
- repository contract/implementation method `GetOrphanProductsAsync`
- generated OpenAPI/client contract for the orphan endpoint

Retained backend/API surface because it is still shared with surviving features:

- `GET /api/products` for profile-scoped product reads
- `GET /api/products/{id}`
- `GET /api/products/all}`
- `GET /api/products/selectable`
- create/update/delete/reorder/change-owner/link-team/unlink-team operations
- product repository/entity infrastructure

Reason for retention:

- `ManageProductOwner.razor` still uses create, update, delete, reorder, selectable-products, and owner-reassignment flows.
- `ProductEditor.razor` still uses create/update/link/unlink.
- onboarding still uses product creation and team linking.
- multiple analytics/planning/work-item flows still depend on shared product reads and DTOs.

## 5. Shared pieces intentionally retained

Retained as shared infrastructure, not feature residue:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Settings/ManageProductOwner.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/ProductEditor.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductEditorDraft.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductService.cs` remaining methods
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs` remaining endpoints
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/IProductRepository.cs` remaining methods
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductRepository.cs` remaining methods
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`
- product persistence entities and mappings

These remain because they still support active Product Owner product management and broader product-scoped application features.

## 6. Residual references check

### Code and governed contract sweep

Verified no remaining code references in client/API/core for:

- `ManageProducts`
- `@page "/settings/products"`
- `GetOrphanProductsAsync(`
- `GetOrphanProductsQuery`
- `api/Products/orphans`
- `Products_GetOrphanProducts`

Validation evidence:

- repo-wide sweeps under `PoTool.Client`, `PoTool.Api`, and `PoTool.Core` returned no matches after cleanup
- no `/settings/products` reference remains under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces`

### Legacy page references

- No legacy page still links to `/settings/products`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor` now shows plain text guidance instead of a route link

### Historical documentation references retained

Historical analysis/report artifacts still mention Manage Products, including:

- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/assets/2026-04-02-ux-full-scan/scan-results.json`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-ux-full-scan-report.md`
- older dated reports under `/home/runner/work/PoCompanion/PoCompanion/docs/reports/`
- the pre-cleanup navigation audit report created earlier today

These were retained as historical records rather than current product guidance. Active guidance docs were updated.

## 7. Risks / follow-ups

1. Historical reports still reference the removed feature. If repository policy later requires full archival cleanup, move or annotate those dated artifacts explicitly.
2. `GET /api/products/all` remains because it is still shared by active flows and deep-link-capable work-item loading; it is not removed in this batch.
3. Product CRUD remains exposed through Product Owner management. This is intentional for Batch 1.
4. Manual edits were applied to the governed OpenAPI snapshot and generated client to keep the removal reviewable within this batch. A future controlled snapshot refresh can regenerate them from a running API if desired.

## 8. Validation results

### Route removal

- `/settings/products` is gone:
  - `ManageProducts.razor` deleted
  - no remaining `@page "/settings/products"` directive found

### UI navigation

- no current UI navigation can still reach `/settings/products`
- the only previously found legacy link in `ProductWorkspace.razor` was removed

### Backend/API

- no API endpoint remains for `/api/products/orphans`
- no orphan-only query/handler/repository/service code remains
- shared product APIs remain intact for surviving Product Owner flows

### DTOs/contracts/services

- no orphan-only contract/service/query artifacts remain
- shared product DTOs/contracts/services remain intentionally because they are still used

### Build and test validation

- `dotnet build PoTool.sln --configuration Release --nologo` passed after cleanup
- `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"` passed after cleanup
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~NswagGovernanceTests"` passed after cleanup

### Validation issues encountered

- An initial parallel build/test run produced transient file-lock warnings/errors in `PoTool.Client` WebAssembly outputs because build and test were running concurrently against the same artifacts.
- This was resolved by rerunning validation sequentially; no code change was required.

### Release notes

- updated: `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`
