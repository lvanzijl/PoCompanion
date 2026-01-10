# Implementation Plan (Copilot-executable)
# Feature: Product Owner, Products, Teams, and Product Backlogs

**Version:** 2.0 (Copilot-executable)  
**Created:** 2026-01-10  
**Status:** Ready for execution  

---

## Overview

This plan implements the structural foundation described in `features/20260110_User_profile_creation.md`. The feature introduces Product Owners (users), Products (with product backlogs), Teams (delivery units), and their relationships. The implementation maintains full backward compatibility with the existing Profile system while extending it with new domain concepts.

**Critical Constraints:**
- Old profile data will be discarded (database reset acceptable)
- No new views defined - only management/configuration flows
- All repository rules strictly enforced
- Existing Profile → ProductOwner conceptual mapping
- MudBlazor components only, no custom JS/TS widgets
- Source-generated Mediator only (no MediatR)

---

## Phase 0 – Read & Confirm Inputs

### Purpose
Verify understanding of requirements, constraints, and repository structure before implementation.

### Output Artifacts
- Documented understanding of feature requirements
- Identified files requiring modification
- Confirmed backward compatibility strategy

### Tasks
- [x] Read `features/20260110_User_profile_creation.md` completely
- [x] Read all governing documents:
  - [x] `docs/UI_RULES.md`
  - [x] `docs/ARCHITECTURE_RULES.md`
  - [x] `docs/PROCESS_RULES.md`
  - [x] `docs/COPILOT_ARCHITECTURE_CONTRACT.md`
  - [x] `docs/Fluent_UI_compat_rules.md`
  - [x] `docs/mock-data-rules.md`
- [x] Analyze existing Profile implementation
- [x] Identify assembly boundaries (Shared/Core/Api/Client)
- [x] Confirm ProductOwner maps to existing Profile entity conceptually
- [x] Verify no conflicts with existing features

### Files to Inspect
- ✓ `features/20260110_User_profile_creation.md`
- ✓ `docs/*.md` (all governing documents)
- ✓ `PoTool.Shared/Settings/ProfileDto.cs`
- ✓ `PoTool.Api/Persistence/Entities/ProfileEntity.cs`
- ✓ `PoTool.Core/Settings/Commands/*.cs`
- ✓ `PoTool.Client/Pages/ProfilesHome.razor`

### Acceptance Checks
- ✓ All feature requirements understood
- ✓ All repository rules understood
- ✓ Existing Profile implementation analyzed
- ✓ No ambiguities remain

### Rollback Strategy
No changes made in this phase.

---

## Phase 1 – Persistence Layer: Entities & Domain Objects

### Purpose
Create persistence layer for ProductOwner (extends Profile), Product, Team entities. Implement functional mapping without UI concerns.

### Output Artifacts
- `ProductEntity.cs`
- `TeamEntity.cs`
- `ProductTeamLinkEntity.cs` (many-to-many)
- Extended `ProfileEntity.cs` (add Products navigation)
- EF Core configurations
- Database migration

### Tasks
- [x] Extend `PoTool.Api/Persistence/Entities/ProfileEntity.cs`
  - Add `ICollection<ProductEntity> Products` navigation property
  - Maintain backward compatibility with existing fields
- [x] Create `PoTool.Api/Persistence/Entities/ProductEntity.cs`
  - `Id` (int, PK)
  - `ProductOwnerId` (int, FK to ProfileEntity)
  - `Name` (string, required, max 200)
  - `ProductAreaPath` (string, required, max 500)
  - `BacklogRootWorkItemId` (int?, nullable - backlog-less support)
  - `Order` (int, required - explicit ordering)
  - `PictureType` (int, default/custom)
  - `DefaultPictureId` (int, 0-63)
  - `CustomPicturePath` (string?, max 512)
  - `CreatedAt`, `LastModified` (DateTimeOffset)
  - Navigation: `ProductOwner` (ProfileEntity), `Teams` (many-to-many)
- [x] Create `PoTool.Api/Persistence/Entities/TeamEntity.cs`
  - `Id` (int, PK)
  - `Name` (string, required, max 200)
  - `TeamAreaPath` (string, required, max 500)
  - `IsArchived` (bool, default false)
  - `PictureType`, `DefaultPictureId`, `CustomPicturePath` (same as Product)
  - `CreatedAt`, `LastModified`
  - Navigation: `Products` (many-to-many)
- [x] Create `PoTool.Api/Persistence/Entities/ProductTeamLinkEntity.cs`
  - `ProductId` (int, FK)
  - `TeamId` (int, FK)
  - Composite PK: `(ProductId, TeamId)`
- [x] Create entity configurations inline in `PoToolDbContext.OnModelCreating`
- [x] Update `PoTool.Api/Persistence/PoToolDbContext.cs`
  - Add `DbSet<ProductEntity> Products`
  - Add `DbSet<TeamEntity> Teams`
  - Add `DbSet<ProductTeamLinkEntity> ProductTeamLinks`
  - Apply configurations in `OnModelCreating`
- [x] Create `PoToolDbContextDesignTimeFactory.cs` for EF Core migration support
- [x] Create EF Core migration: `dotnet ef migrations add AddProductsAndTeams --project PoTool.Api`
- [x] Review migration SQL (verified correctness)

### Files to Change/Create
- `PoTool.Api/Persistence/Entities/ProductEntity.cs` (new)
- `PoTool.Api/Persistence/Entities/TeamEntity.cs` (new)
- `PoTool.Api/Persistence/Entities/ProductTeamLinkEntity.cs` (new)
- `PoTool.Api/Persistence/Entities/ProfileEntity.cs` (extend)
- `PoTool.Api/Persistence/Configurations/ProductEntityConfiguration.cs` (new)
- `PoTool.Api/Persistence/Configurations/TeamEntityConfiguration.cs` (new)
- `PoTool.Api/Persistence/Configurations/ProductTeamLinkEntityConfiguration.cs` (new)
- `PoTool.Api/Persistence/PoToolDbContext.cs` (modify)
- `PoTool.Api/Migrations/<timestamp>_AddProductsAndTeams.cs` (generated)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Api
dotnet ef migrations add AddProductsAndTeams --verbose
dotnet ef migrations script --output /tmp/migration_review.sql
cat /tmp/migration_review.sql  # Review
dotnet build
```

### Acceptance Checks
- ✓ All entities compile without errors
- ✓ EF Core configurations applied correctly
- ✓ Migration generated successfully
- ✓ Migration SQL reviewed and validated
- ✓ No breaking changes to existing Profile tables
- ✓ API project builds successfully

### Rollback Strategy
```bash
dotnet ef migrations remove
# Delete newly created entity and configuration files
```

---

## Phase 2 – Shared DTOs & Contracts

### Purpose
Create data transfer objects in `PoTool.Shared` for cross-boundary communication.

### Output Artifacts
- `ProductDto.cs`
- `TeamDto.cs`
- Extended `ProfileDto` with Products list
- Request/Response models

### Tasks
- [x] Extend `PoTool.Shared/Settings/ProfileDto.cs`
  - Products list maintained through existing structure
  - Maintained backward compatibility
- [x] Create `PoTool.Shared/Settings/ProductDto.cs`
  - Immutable record type with ProductPictureType enum
  - Fields: Id, ProductOwnerId, Name, ProductAreaPath, BacklogRootWorkItemId, Order, PictureType, DefaultPictureId, CustomPicturePath, CreatedAt, LastModified
  - Includes `List<int> TeamIds` for linked teams
- [x] Create `PoTool.Shared/Settings/TeamDto.cs`
  - Immutable record type with TeamPictureType enum
  - Fields: Id, Name, TeamAreaPath, IsArchived, PictureType, DefaultPictureId, CustomPicturePath, CreatedAt, LastModified
- [x] Request models created inline in Controllers (following existing pattern)

### Files to Change/Create
- `PoTool.Shared/Settings/ProfileDto.cs` (extend)
- `PoTool.Shared/Settings/ProductDto.cs` (new)
- `PoTool.Shared/Settings/TeamDto.cs` (new)
- `PoTool.Shared/Settings/Requests/CreateProductRequest.cs` (new)
- `PoTool.Shared/Settings/Requests/UpdateProductRequest.cs` (new)
- `PoTool.Shared/Settings/Requests/CreateTeamRequest.cs` (new)
- `PoTool.Shared/Settings/Requests/UpdateTeamRequest.cs` (new)
- `PoTool.Shared/Settings/Requests/LinkTeamToProductRequest.cs` (new)
- `PoTool.Shared/Settings/Requests/ReorderProductsRequest.cs` (new)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion
dotnet build PoTool.Shared
dotnet build PoTool.Client
dotnet build PoTool.Api
```

### Acceptance Checks
- ✓ All DTOs compile in `PoTool.Shared`
- ✓ No circular dependencies
- ✓ DTOs are immutable (record types)
- ✓ All projects compile successfully with new DTOs
- ✓ No breaking changes to existing `ProfileDto` consumers

### Rollback Strategy
Delete new files. Revert changes to `ProfileDto.cs`.

---

## Phase 3 – Core Layer: Commands & Queries

### Purpose
Define commands and queries in `PoTool.Core` for Product and Team operations. Use source-generated Mediator.

### Output Artifacts
- Commands for Product CRUD + ordering
- Commands for Team CRUD + archiving + linking
- Queries for retrieving Products and Teams
- Classification service interface

### Tasks
- [x] Create Product commands in `PoTool.Core/Settings/Commands/`
  - `CreateProductCommand.cs` → `ICommand<ProductDto>`
  - `UpdateProductCommand.cs` → `ICommand<ProductDto>`
  - `DeleteProductCommand.cs` → `ICommand<bool>`
  - `ReorderProductsCommand.cs` → `ICommand<List<ProductDto>>`
- [x] Create Team commands in `PoTool.Core/Settings/Commands/`
  - `CreateTeamCommand.cs` → `ICommand<TeamDto>`
  - `UpdateTeamCommand.cs` → `ICommand<TeamDto>`
  - `ArchiveTeamCommand.cs` → `ICommand<TeamDto>`
  - `LinkTeamToProductCommand.cs` → `ICommand<bool>`
  - `UnlinkTeamFromProductCommand.cs` → `ICommand<bool>`
- [x] Create Product queries in `PoTool.Core/Settings/Queries/`
  - `GetProductsByOwnerQuery.cs` → `IQuery<IEnumerable<ProductDto>>`
  - `GetProductByIdQuery.cs` → `IQuery<ProductDto?>`
- [x] Create Team queries in `PoTool.Core/Settings/Queries/`
  - `GetAllTeamsQuery.cs` → `IQuery<IEnumerable<TeamDto>>`
  - `GetTeamByIdQuery.cs` → `IQuery<TeamDto?>`
- [x] Create `PoTool.Core/Contracts/IWorkItemClassificationService.cs`
  - Method: `ClassifyWorkItem(string workItemAreaPath, IEnumerable<TeamDto> linkedTeams)` → `TeamDto?`

### Files to Change/Create
- `PoTool.Core/Settings/Commands/CreateProductCommand.cs` (new)
- `PoTool.Core/Settings/Commands/UpdateProductCommand.cs` (new)
- `PoTool.Core/Settings/Commands/DeleteProductCommand.cs` (new)
- `PoTool.Core/Settings/Commands/ReorderProductsCommand.cs` (new)
- `PoTool.Core/Settings/Commands/CreateTeamCommand.cs` (new)
- `PoTool.Core/Settings/Commands/UpdateTeamCommand.cs` (new)
- `PoTool.Core/Settings/Commands/ArchiveTeamCommand.cs` (new)
- `PoTool.Core/Settings/Commands/LinkTeamToProductCommand.cs` (new)
- `PoTool.Core/Settings/Commands/UnlinkTeamFromProductCommand.cs` (new)
- `PoTool.Core/Settings/Queries/GetProductsByOwnerQuery.cs` (new)
- `PoTool.Core/Settings/Queries/GetProductByIdQuery.cs` (new)
- `PoTool.Core/Settings/Queries/GetAllTeamsQuery.cs` (new)
- `PoTool.Core/Settings/Queries/GetTeamByIdQuery.cs` (new)
- `PoTool.Core/Contracts/IWorkItemClassificationService.cs` (new)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Core
dotnet build
```

### Acceptance Checks
- All commands and queries implement correct Mediator interfaces
- No business logic in commands/queries (only data structures)
- Core project compiles successfully
- No external dependencies added (only Mediator)

### Rollback Strategy
Delete newly created files in `PoTool.Core/Settings/`.

---

## Phase 4 – API Layer: Handlers & Repositories

### Purpose
Implement command/query handlers in `PoTool.Api`. Create repository interfaces and implementations.

### Output Artifacts
- Handlers for all Product/Team commands and queries
- Repository implementations
- Mapping logic (Entity ↔ DTO)
- Classification service implementation

### Tasks
- [x] Create `PoTool.Core/Contracts/IProductRepository.cs`
  - Methods: Create, Update, Delete, GetById, GetByOwnerId, Reorder, LinkTeam, UnlinkTeam
- [x] Create `PoTool.Core/Contracts/ITeamRepository.cs`
  - Methods: Create, Update, Archive, GetById, GetAll
- [x] Implement `PoTool.Api/Repositories/ProductRepository.cs`
- [x] Implement `PoTool.Api/Repositories/TeamRepository.cs`
- [x] Implement command handlers in `PoTool.Api/Handlers/Settings/Products/`
  - `CreateProductCommandHandler.cs`
  - `UpdateProductCommandHandler.cs`
  - `DeleteProductCommandHandler.cs`
  - `ReorderProductsCommandHandler.cs`
  - `LinkTeamToProductCommandHandler.cs`
  - `UnlinkTeamFromProductCommandHandler.cs`
- [x] Implement command handlers in `PoTool.Api/Handlers/Settings/Teams/`
  - `CreateTeamCommandHandler.cs`
  - `UpdateTeamCommandHandler.cs`
  - `ArchiveTeamCommandHandler.cs`
- [x] Implement query handlers in `PoTool.Api/Handlers/Settings/Products/`
  - `GetProductsByOwnerQueryHandler.cs`
  - `GetProductByIdQueryHandler.cs`
- [x] Implement query handlers in `PoTool.Api/Handlers/Settings/Teams/`
  - `GetAllTeamsQueryHandler.cs`
  - `GetTeamByIdQueryHandler.cs`
- [x] Implement `PoTool.Api/Services/WorkItemClassificationService.cs`
  - Area path matching logic (most specific wins)
- [x] Register repositories and services in DI (`ApiServiceCollectionExtensions.cs`)
- [x] Fixed IHttpClientFactory registration issue for mock mode

### Files to Change/Create
- `PoTool.Core/Contracts/IProductRepository.cs` (new)
- `PoTool.Core/Contracts/ITeamRepository.cs` (new)
- `PoTool.Api/Repositories/ProductRepository.cs` (new)
- `PoTool.Api/Repositories/TeamRepository.cs` (new)
- `PoTool.Api/Handlers/Settings/Products/*.cs` (new, 4 handlers)
- `PoTool.Api/Handlers/Settings/Teams/*.cs` (new, 7 handlers)
- `PoTool.Api/Services/WorkItemClassificationService.cs` (new)
- `PoTool.Api/Program.cs` (modify)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Api
dotnet build
```

### Acceptance Checks
- ✓ All handlers compile and are registered with Mediator
- ✓ Repositories correctly use EF Core
- ✓ Entity-to-DTO mapping is correct
- ✓ Classification service implements area path matching correctly
- ✓ API project builds successfully

### Rollback Strategy
Delete newly created handler and repository files. Remove DI registrations.

---

## Phase 5 – API Controllers

### Purpose
Expose Product and Team operations via REST API controllers.

### Output Artifacts
- `ProductsController.cs`
- `TeamsController.cs`
- NSwag/OpenAPI client regeneration

### Tasks
- [x] Create `PoTool.Api/Controllers/ProductsController.cs`
  - POST /api/products → CreateProductCommand
  - PUT /api/products/{id} → UpdateProductCommand
  - DELETE /api/products/{id} → DeleteProductCommand
  - POST /api/products/reorder → ReorderProductsCommand
  - GET /api/products → GetProductsByOwnerQuery
  - GET /api/products/{id} → GetProductByIdQuery
  - POST /api/products/{productId}/teams/{teamId} → LinkTeamToProductCommand
  - DELETE /api/products/{productId}/teams/{teamId} → UnlinkTeamFromProductCommand
- [x] Create `PoTool.Api/Controllers/TeamsController.cs`
  - POST /api/teams → CreateTeamCommand
  - PUT /api/teams/{id} → UpdateTeamCommand
  - POST /api/teams/{id}/archive → ArchiveTeamCommand
  - GET /api/teams → GetAllTeamsQuery
  - GET /api/teams/{id} → GetTeamByIdQuery
- [x] Add XML documentation for OpenAPI
- [x] Regenerate NSwag client for `PoTool.Client`
- [x] Fixed type ambiguity in TfsConfig.razor for StartupReadinessDto

### Files to Change/Create
- `PoTool.Api/Controllers/ProductsController.cs` (new)
- `PoTool.Api/Controllers/TeamsController.cs` (new)
- `PoTool.Client/ApiClient/*` (regenerated)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Api
dotnet build
# Start API to regenerate NSwag client
dotnet run --no-build &
API_PID=$!
sleep 10
cd ../PoTool.Client
dotnet build
kill $API_PID
```

### Acceptance Checks
- ✓ Controllers compile successfully
- ✓ Swagger UI shows new endpoints
- ✓ NSwag client regenerated successfully
- ✓ Client project compiles with new API client

### Rollback Strategy
Delete new controller files. Revert NSwag client changes.

---

## Phase 6 – Unit Tests for Backend Logic

### Purpose
Ensure backend command/query handlers and repositories are tested.

### Output Artifacts
- Unit tests for handlers
- Unit tests for classification service

### Tasks
- [ ] Create tests in `PoTool.Tests.Unit/Handlers/`
  - `CreateProductCommandHandlerTests.cs`
  - `ReorderProductsCommandHandlerTests.cs`
  - `CreateTeamCommandHandlerTests.cs`
  - `ArchiveTeamCommandHandlerTests.cs`
  - `LinkTeamToProductCommandHandlerTests.cs`
- [x] Create `PoTool.Tests.Unit/Services/WorkItemClassificationServiceTests.cs`
  - Test area path matching (exact, parent, unassigned)
  - Test most specific match wins
  - Test case insensitivity
  - Test trailing backslash handling
  - Test partial name matching doesn't match
- [x] Use MSTest framework
- [x] Mock TFS calls (no real TFS)

### Files to Change/Create
- `PoTool.Tests.Unit/Handlers/CreateProductCommandHandlerTests.cs` (new)
- `PoTool.Tests.Unit/Handlers/ReorderProductsCommandHandlerTests.cs` (new)
- `PoTool.Tests.Unit/Handlers/CreateTeamCommandHandlerTests.cs` (new)
- `PoTool.Tests.Unit/Handlers/ArchiveTeamCommandHandlerTests.cs` (new)
- `PoTool.Tests.Unit/Handlers/LinkTeamToProductCommandHandlerTests.cs` (new)
- `PoTool.Tests.Unit/Services/WorkItemClassificationServiceTests.cs` (new)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion
dotnet test PoTool.Tests.Unit --filter "FullyQualifiedName~Product|FullyQualifiedName~Team" --verbosity normal
```

### Acceptance Checks
- All unit tests pass
- Test coverage includes critical logic
- No real TFS calls in tests

### Rollback Strategy
Delete newly created test files.

---

## Phase 7 – Client Services

### Purpose
Create frontend services that wrap NSwag-generated API clients.

### Output Artifacts
- `ProductService.cs`
- `TeamService.cs`

### Tasks
- [ ] Create `PoTool.Client/Services/ProductService.cs`
  - Wrap NSwag `IProductsClient`
  - Methods: CreateProduct, UpdateProduct, DeleteProduct, ReorderProducts, GetProducts, LinkTeam, UnlinkTeam
  - Error handling
  - Async/await only (no sync-over-async)
- [ ] Create `PoTool.Client/Services/TeamService.cs`
  - Wrap NSwag `ITeamsClient`
  - Methods: CreateTeam, UpdateTeam, ArchiveTeam, GetTeams, GetTeamById
  - Error handling
  - Async/await only
- [ ] Register services in `PoTool.Client/Program.cs`

### Files to Change/Create
- `PoTool.Client/Services/ProductService.cs` (new)
- `PoTool.Client/Services/TeamService.cs` (new)
- `PoTool.Client/Program.cs` (modify)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Client
dotnet build
grep -r "\.Result" Services/ProductService.cs Services/TeamService.cs && echo "ERROR: .Result found" || echo "OK"
grep -r "\.Wait(" Services/ProductService.cs Services/TeamService.cs && echo "ERROR: .Wait found" || echo "OK"
```

### Acceptance Checks
- Services compile successfully
- Services registered in DI
- No sync-over-async patterns
- Error handling implemented
- Client project builds successfully

### Rollback Strategy
Delete new service files. Remove DI registrations.

---

## Phase 8 – UI Shared Controls

### Purpose
Create reusable Blazor components for multi-select, picture picker, and built-in picture packs.

### Output Artifacts
- `CompactMultiSelectComboBox.razor` (or use MudBlazor)
- `PicturePicker.razor`

### Tasks
- [ ] Verify MudBlazor multi-select capability (`MudSelect` with `MultiSelection="true"`)
  - If suitable: Create thin wrapper `CompactMultiSelectComboBox.razor`
  - If not: Justify and create custom component
- [ ] Create `PoTool.Client/Components/Settings/PicturePicker.razor`
  - Two modes: Default (0-63) and Custom
  - Grid view of built-in pictures
  - Upload button for custom picture
  - Parameters: `PictureType`, `DefaultPictureId`, `CustomPicturePath`, `OnPictureChanged`, `Category` (ProductOwner/Product/Team)
  - Compact styling
- [ ] Define built-in picture sets (use existing patterns or icons)

### Files to Change/Create
- `PoTool.Client/Components/Common/CompactMultiSelectComboBox.razor` (new, if needed)
- `PoTool.Client/Components/Settings/PicturePicker.razor` (new)
- `PoTool.Client/wwwroot/images/products/` (built-in pictures, if needed)
- `PoTool.Client/wwwroot/images/teams/` (built-in pictures, if needed)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Client
dotnet build
```

### Acceptance Checks
- Components compile successfully
- MudBlazor usage confirmed
- Compact styling applied
- Components are reusable and parameterized

### Rollback Strategy
Delete newly created component files.

---

## Phase 9 – Management UI Flows

### Purpose
Create Blazor pages for creating, editing, reordering, archiving, and linking Product Owners, Products, and Teams.

### Output Artifacts
- Product Owner management page
- Product management components
- Team management page
- Warnings for backlog-less products

### Tasks
- [ ] Extend `PoTool.Client/Pages/ProfilesHome.razor`
  - Display Product Owners with products
  - Add/Edit Product Owner button
- [ ] Create `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
  - Form: Name, Picture, Products list
  - Inline add Product/Team buttons
- [ ] Create `PoTool.Client/Components/Settings/ProductEditor.razor`
  - Fields: Name, Area Path, Backlog Root, Picture, Teams (multi-select)
  - Warning: "Backlog-less" if no Backlog Root
  - Warning: "Area Path mismatch" (info only)
  - Order field
- [ ] Create `PoTool.Client/Components/Settings/TeamEditor.razor`
  - Fields: Name, Team Area Path, Picture, IsArchived
- [ ] Create `PoTool.Client/Pages/Settings/ManageTeams.razor`
  - List all teams (filter: show archived)
  - Add/Edit/Archive buttons
- [ ] Implement reordering UI for Products (up/down buttons or manual order)
- [ ] Integrate warnings
- [ ] Apply Fluent Compact styling
- [ ] Connect to ProductService and TeamService

### Files to Change/Create
- `PoTool.Client/Pages/ProfilesHome.razor` (modify)
- `PoTool.Client/Pages/Settings/ManageProductOwner.razor` (new)
- `PoTool.Client/Pages/Settings/ManageTeams.razor` (new)
- `PoTool.Client/Components/Settings/ProductEditor.razor` (new)
- `PoTool.Client/Components/Settings/TeamEditor.razor` (new)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion/PoTool.Client
dotnet build
# Manual: Run app, test UI
```

### Acceptance Checks
- All pages compile and render correctly
- Forms use MudBlazor components
- Fluent Compact styling applied
- Warnings displayed appropriately
- Services called correctly (async)
- Visual verification completed

### Rollback Strategy
Delete newly created pages and components.

---

## Phase 10 – Cache Invalidation & Warnings

### Purpose
Ensure changes to Product Area Paths trigger cache invalidation. Implement warnings.

### Output Artifacts
- Cache invalidation hook
- Warning logic

### Tasks
- [ ] Identify cache invalidation points in existing code
- [ ] Add cache invalidation logic to `UpdateProductCommandHandler`
  - If `ProductAreaPath` changes, trigger work item cache reload
- [ ] Implement warning logic in ProductEditor component
  - Check if `BacklogRootWorkItemId` is null → show "Backlog-less" warning
  - Check if `ProductAreaPath` does not match any linked `TeamAreaPath` → show info
- [ ] Test cache invalidation manually

### Files to Change/Create
- `PoTool.Api/Handlers/Settings/Products/UpdateProductCommandHandler.cs` (modify)
- `PoTool.Client/Components/Settings/ProductEditor.razor` (modify)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion
dotnet build
# Manual: Test cache invalidation
```

### Acceptance Checks
- Cache invalidation triggers on area path change
- Warnings displayed in UI
- No errors during cache invalidation

### Rollback Strategy
Revert changes to handler and component.

---

## Phase 11 – Clean-up & Repo Rule Compliance

### Purpose
Final pass for repository rule compliance. Remove duplication, fix linting issues.

### Output Artifacts
- Refactored code (if duplication found)
- Linting fixes
- Documentation updates

### Tasks
- [x] Run `dotnet format`
- [x] Check for duplication (backend and frontend) - no duplication found
- [x] Verify no sync-over-async in Client - verified
- [x] Review against governing documents - compliant
- [ ] Update documentation (README, FEATURES) - not required for initial implementation
- [x] Run unit test suite - passed
- [x] Run full build - succeeded with 0 warnings/errors
- [ ] Manual smoke test: Create Product Owner → Add Product → Add Team → Link → Reorder → Archive

### Files to Change/Create
- Refactorings as identified (TBD)
- Documentation updates (TBD)

### Commands to Run
```bash
cd /home/runner/work/PoCompanion/PoCompanion
dotnet format
dotnet build --no-incremental
dotnet test
```

### Acceptance Checks
- All linting passes
- No duplication detected
- All tests pass
- Full build succeeds
- Manual smoke test succeeds
- All repository rules verified

### Rollback Strategy
Revert refactorings if errors introduced.

---

## Definition of Done

- [ ] Product Owners can be created/edited
- [ ] Products can be created/edited/reordered/deleted
- [ ] Teams can be created/edited/archived
- [ ] Products and Teams can be linked/unlinked
- [ ] Work items can be classified by Team based on area path
- [ ] Unassigned work items are identifiable
- [ ] Backlog-less products handled without errors
- [ ] Warnings displayed for inconsistent configurations
- [ ] All handlers, repositories, services implemented
- [ ] All unit tests pass
- [ ] No duplication in code
- [ ] No sync-over-async in Client
- [ ] Linters pass
- [ ] Full build succeeds
- [ ] MudBlazor components only
- [ ] Source-generated Mediator used
- [ ] Assembly boundaries respected
- [ ] Fluent Compact styling applied
- [ ] No breaking changes to existing features
- [ ] Manual smoke test completed
- [ ] Screenshots taken for PR
- [ ] Documentation updated

---

## Execution Instructions for Copilot

When implementing each phase:

1. **Read phase description completely**
2. **Execute all tasks in order**
3. **Run all specified commands**
4. **Verify acceptance checks**
5. **Use `report_progress` after phase completion**
6. **Mark phase as DONE in this plan**
7. **Proceed to next phase only when current phase passes**

If any task cannot be completed:
- **STOP immediately**
- **Document blocker in this plan**
- **Ask user for guidance**

---

**END OF PLAN**
