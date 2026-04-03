# Project Entity With Alias Support

## Scope
This report documents the implemented project-entity foundation that was added for planning-aware routing and filtering without breaking existing product-based behavior.

## Data model

### Shared contract
- `PoTool.Shared/Settings/ProjectDto.cs`
  - `Id: string`
  - `Alias: string`
  - `Name: string`
  - `ProductIds: List<int>`

### Persistence
- `PoTool.Api/Persistence/Entities/ProjectEntity.cs`
  - `Id` is a string primary key
  - `Alias` is required, max length 128, unique
  - `Name` is required, max length 200
  - `Products` is a one-to-many navigation collection
- `PoTool.Api/Persistence/Entities/ProductEntity.cs`
  - new required `ProjectId` foreign key
  - new `Project` navigation property
- `PoTool.Api/Persistence/PoToolDbContext.cs`
  - new `DbSet<ProjectEntity>`
  - unique index on `Projects.Alias`
  - required `Products.ProjectId`
  - `Products -> Projects` foreign key with `DeleteBehavior.Restrict`

### Runtime project creation behavior
- New products still go through existing product creation flows.
- `PoTool.Api/Repositories/ProductRepository.cs` now creates a default project automatically when a product is created.
- The default project:
  - gets a generated string ID
  - gets a normalized, URL-safe alias
  - uses the product name as the initial project name
- Alias collisions are resolved by suffixing `-2`, `-3`, and so on.

## Migration result

### Migration file
- `PoTool.Api/Migrations/20260401112645_AddProjectsWithAliases.cs`
- Generated designer and snapshot were produced by EF Core.

### Backfill behavior
The migration performs two steps for existing products:
1. creates a default project row per existing product
2. updates each existing product to point at its new project via `ProjectId`

### Backfill naming rules
- `Project.Id = "project-<productId>"`
- `Project.Name = Product.Name`
- `Project.Alias = normalized Product.Name`
- duplicate aliases are disambiguated with a numeric suffix

### Manual migration verification
A real SQLite database was migrated from the previous migration level to the new one after inserting an existing legacy product row.

Observed result:
- `Projects` contained `('project-1', 'payments-platform', 'Payments Platform')`
- `Products` contained `(1, 'Payments Platform', 'project-1')`

This confirmed that the migration backfilled both the project row and the product foreign key correctly.

## API surface

### New endpoints
- `GET /api/projects`
- `GET /api/projects/{alias}`
- `GET /api/projects/{alias}/products`

### Resolution behavior
- `{alias}` resolves by project alias first
- `{alias}` also accepts the internal string ID as a fallback

### Example responses
`GET /api/projects`
```json
[
  {
    "id": "project-1",
    "alias": "payments-platform",
    "name": "Payments Platform",
    "productIds": [1]
  }
]
```

`GET /api/projects/payments-platform/products`
```json
[
  {
    "id": 1,
    "name": "Payments Platform",
    "productOwnerId": 1,
    "backlogRootWorkItemIds": []
  }
]
```

Note: the second example is representative of the returned product payload; `ProductDto` itself remains backward compatible and does not expose project fields.

## Routing changes

### Existing routes preserved
- `/planning/product-roadmaps`
- `/planning/product-roadmaps/{productId}`
- `/planning/plan-board`

### New alias-aware routes
- `/planning/{projectAlias}/product-roadmaps`
- `/planning/{projectAlias}/plan-board`

### Route helper additions
- `WorkspaceRoutes.GetProjectProductRoadmaps(string projectAlias)`
- `WorkspaceRoutes.GetProjectPlanBoard(string projectAlias)`

### Resolution behavior in planning pages
- Product Roadmaps filters visible lanes to products that belong to the selected project.
- Plan Board filters the product selector to products that belong to the selected project.
- If only a `productId` is present in the query context, the client infers the project from the loaded project metadata.
- Existing productId-based routes continue to work.

## Planning and filter integration

### Query/context propagation
- `WorkspaceQueryContext` now carries `ProjectAlias`
- `WorkspaceQueryContextHelper` parses and builds `projectAlias`
- `WorkspaceBase` now preserves `projectAlias` alongside `productId`, `teamId`, and sprint query parameters
- Update note (2026-04-03): the legacy `NavigationContext` model and `NavigationContextService` were removed during final legacy workspace cleanup; current project-alias propagation now relies on `WorkspaceQueryContext` and `WorkspaceBase`.

### Planning pages
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - new project selector above product selector
  - project selection restricts the product list
  - alias route is used when a project is selected
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - new project selector
  - roadmap lanes are filtered to the selected project
  - editor navigation preserves project context through query state
- `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
  - return navigation now preserves alias context back to the correct roadmap route
- `PoTool.Client/Pages/Home/PlanningWorkspace.razor`
  - tile navigation now prefers alias routes when project context is present

## Non-breaking behavior preserved
- Existing product APIs remain unchanged
- Existing productId planning routes remain unchanged
- Existing product-based planning logic still operates at the product level
- No cross-product planning logic was introduced
- `ProductDto` was left backward compatible

## Runtime risks found

### 1. Data-source route classification
New `/api/projects` routes were initially blocked by `DataSourceModeMiddleware` because they were not classified.

Fix applied:
- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` now treats `/api/projects` as live-allowed
- covered by `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`

### 2. Alias collision risk
Two products with the same normalized name can produce the same base alias.

Implemented mitigation:
- runtime alias generation appends numeric suffixes
- migration backfill appends numeric suffixes for duplicate normalized names

### 3. Alias normalization differences between runtime and migration
- runtime alias generation uses the shared C# normalization helper in `ProjectAliasGenerator`
- migration backfill uses SQL normalization because migrations must be self-contained

Impact:
- both produce URL-safe aliases
- edge-case punctuation normalization may differ slightly between runtime creation and migration backfill
- collision handling remains deterministic in both paths

### 4. Project ownership remains implicit
Projects do not have a separate owner column.

Current behavior:
- planning pages determine relevant projects by intersecting project product IDs with the current profile's products
- this keeps the change backward compatible with the existing product-owner model

## Validation performed
- `dotnet build PoTool.sln --configuration Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo -v minimal --filter "FullyQualifiedName~ProductTeamLinkRepositoryTests|FullyQualifiedName~WorkspaceRoutesTests|FullyQualifiedName~NavigationContextServiceTests|FullyQualifiedName~WorkspaceQueryContextHelperTests|FullyQualifiedName~DataSourceModeConfigurationTests"`
- manual SQLite migration/backfill verification
- manual UI verification of:
  - `/planning/payments-platform/plan-board`
  - `/planning/payments-platform/product-roadmaps`

## Screenshots captured during verification
- Plan Board alias route screenshot available
- Product Roadmaps alias route screenshot available
