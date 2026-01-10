# Orphan Products Implementation Notes

## Overview
This document describes the implementation of orphan product support in PO Companion, completed in Phases 1 and 2.

## What is an Orphan Product?
An orphan product is a Product that exists in the database but has no Product Owner assigned (ProductOwnerId = null). This allows:
1. Products to be created without immediately assigning an owner
2. Products to be removed from one owner without being deleted
3. A two-step workflow for moving products between owners

## Domain Rules Implemented

### Product Ownership
- **Orphan Creation**: Products can be created with ProductOwnerId = null
- **Orphaning**: Setting ProductOwnerId = null makes a product orphaned
- **Assignment**: Setting ProductOwnerId to a valid ProfileId assigns ownership

### Product Selection Rules (for Product Owner Edit)
When a Product Owner is being edited, they can select from:
- Products they already own (ProductOwnerId = their ID)
- Orphaned products (ProductOwnerId = null)
- Products owned by OTHER Product Owners are NEVER shown

### Moving Products Between Owners
Two-step process:
1. Open current owner, deselect product → product becomes orphan
2. Open new owner, select orphaned product → product is now owned by new owner

## Database Changes

### Migration: MakeProductOwnerIdNullable
**File**: `PoTool.Api/Migrations/20260110221715_MakeProductOwnerIdNullable.cs`

Changes:
```sql
ALTER TABLE Products 
ALTER COLUMN ProductOwnerId INTEGER NULL;
```

Previously: ProductOwnerId was NOT NULL
Now: ProductOwnerId is nullable

### Entity Changes
**File**: `PoTool.Api/Persistence/Entities/ProductEntity.cs`

- `ProductOwnerId`: Changed from `int` to `int?`
- `ProductOwner`: Changed from `ProfileEntity` (non-null) to `ProfileEntity?`
- Added documentation explaining orphan state

## API Changes

### DTOs Modified
**File**: `PoTool.Shared/Settings/ProductDto.cs`

- ProductOwnerId changed from `int` to `int?`

### Commands Modified
**File**: `PoTool.Core/Settings/Commands/CreateProductCommand.cs`

- ProductOwnerId parameter changed from `int` to `int?`
- Allows creating orphan products

### New Commands
**File**: `PoTool.Core/Settings/Commands/ChangeProductOwnerCommand.cs`

```csharp
public sealed record ChangeProductOwnerCommand(
    int ProductId,
    int? NewProductOwnerId  // null = orphan, value = assign to owner
) : ICommand<ProductDto>;
```

### New Queries
1. **GetAllProductsQuery**: Returns ALL products in system
2. **GetOrphanProductsQuery**: Returns only orphan products  
3. **GetSelectableProductsQuery**: Returns products selectable by a specific owner

### Controller Endpoints

#### New Endpoints
```csharp
// Get all products
GET /api/products/all
Returns: IEnumerable<ProductDto>

// Get orphan products
GET /api/products/orphans
Returns: IEnumerable<ProductDto>

// Get selectable products for an owner
GET /api/products/selectable?productOwnerId={id}
Returns: IEnumerable<ProductDto>

// Change product owner
PATCH /api/products/{productId}/owner
Body: { "newProductOwnerId": 123 } or { "newProductOwnerId": null }
Returns: ProductDto
```

#### Modified Endpoints
```csharp
// Create product (ProductOwnerId now optional)
POST /api/products
Body: {
  "productOwnerId": 123,  // Optional - can be null
  "name": "Product Name",
  "backlogRootWorkItemId": 456,
  ...
}
```

## Repository Changes

### IProductRepository Interface
**File**: `PoTool.Core/Contracts/IProductRepository.cs`

Added methods:
```csharp
Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, ...);
Task<IEnumerable<ProductDto>> GetAllProductsAsync(...);
Task<IEnumerable<ProductDto>> GetOrphanProductsAsync(...);
Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, ...);
```

Modified:
```csharp
Task<ProductDto> CreateProductAsync(
    int? productOwnerId,  // Changed from int to int?
    ...
);
```

### ProductRepository Implementation
**File**: `PoTool.Api/Repositories/ProductRepository.cs`

Key implementation details:

**CreateProductAsync**:
- Handles nullable ProductOwnerId
- Only calculates Order for owned products
- Orphans get Order = 0

**ChangeProductOwnerAsync**:
- Updates ProductOwnerId
- Recalculates Order when assigning to owner
- Sets Order = 0 for orphans

**GetAllProductsAsync**:
```csharp
_context.Products
    .Include(p => p.ProductTeamLinks)
    .OrderBy(p => p.Name)
```

**GetOrphanProductsAsync**:
```csharp
_context.Products
    .Include(p => p.ProductTeamLinks)
    .Where(p => p.ProductOwnerId == null)
    .OrderBy(p => p.Name)
```

**GetSelectableProductsAsync**:
```csharp
_context.Products
    .Include(p => p.ProductTeamLinks)
    .Where(p => p.ProductOwnerId == productOwnerId || p.ProductOwnerId == null)
    .OrderBy(p => p.Order)
    .ThenBy(p => p.Name)
```

## Handlers Created

All handlers follow the standard Mediator pattern:

1. **ChangeProductOwnerCommandHandler.cs** - Handles ownership changes
2. **GetAllProductsQueryHandler.cs** - Returns all products
3. **GetOrphanProductsQueryHandler.cs** - Returns orphans
4. **GetSelectableProductsQueryHandler.cs** - Returns selectable products

## UI Changes (Phase 1)

### ProfilesHome.razor
- Removed "Manage Profiles" button (was showing a notification)
- Kept "Manage Teams" button but only as global navigation

### ManageProductOwner.razor
- Removed broken `ProductAreaPath` reference
- Product display now shows only name and team count

### ProfileTile.razor
- Removed broken `TeamName` reference
- Profile tile now shows only name and active status

## Phase 3 Implementation (UI Integration)

### Goals
1. Regenerate API client with new endpoints
2. Add ProductService wrapper methods
3. Create ManageProducts page for global product management
4. Support creating orphan products

### ProductService Updates
**File**: `PoTool.Client/Services/ProductService.cs`

Added four new methods:

```csharp
// Get all products in the system
public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default)

// Get only orphaned products
public async Task<IEnumerable<ProductDto>> GetOrphanProductsAsync(CancellationToken cancellationToken = default)

// Get products selectable by a specific Product Owner (owned + orphaned)
public async Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default)

// Change the Product Owner for a product
public async Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default)
```

Also updated `CreateProductAsync` to accept nullable `productOwnerId` parameter.

### ManageProducts Page
**File**: `PoTool.Client/Pages/Settings/ManageProducts.razor`
**Route**: `/settings/products`

Features implemented:
- **Global Product Listing**: Shows all products across all Product Owners
- **Orphan Badge**: Orange "Orphan" chip for products with ProductOwnerId == null
- **Filter Toggle**: "Show Only Orphans" switch to filter display
- **Warning Banner**: Displays count of orphaned products with quick filter button
- **CRUD Operations**: Add, Edit, Delete products
- **Product Owner Display**: Shows owner ID for owned products
- **Work Item Display**: Shows BacklogRootWorkItemId for each product
- **Team Count**: Shows number of linked teams
- **Empty States**: Different messages for no products vs no orphans

### ProductEditor Updates
**File**: `PoTool.Client/Components/Settings/ProductEditor.razor`

Changes:
- Updated to handle `ProductOwnerId = 0` as "create orphan"
- When ProductOwnerId is 0, passes null to CreateProductAsync
- This allows creating products without a Product Owner from ManageProducts page

### Compilation Fixes
Fixed errors caused by DTO changes:

1. **ProfileSelector.razor**: Removed TeamName reference (doesn't exist in ProfileDto)
2. **ProductEditor.razor**: Fixed BacklogRootWorkItemId handling (not nullable in DTO)
3. **ManageProductOwner.razor**: Removed null check for BacklogRootWorkItemId

### API Client Regeneration
- Started API server to generate fresh swagger.json
- Manually updated swagger.json to make ProductOwnerId nullable in CreateProductRequest
- Regenerated NSwag client with `dotnet build`
- New client includes all 4 new endpoints:
  - GET /api/products/all
  - GET /api/products/orphans
  - GET /api/products/selectable
  - PATCH /api/products/{id}/owner

### CreateProductRequest Update
**File**: `PoTool.Api/Controllers/ProductsController.cs`

Changed ProductOwnerId from `int` to `int?`:

```csharp
public record CreateProductRequest(
    int? ProductOwnerId,  // Changed from int to int?
    string Name,
    int BacklogRootWorkItemId,
    ...
);
```

This allows API to accept requests for creating orphan products.

## Testing Checklist for Next Phase

### Backend Testing
- [x] Create orphan product via API (via ManageProducts page)
- [x] Create owned product via API (via Product Owner page)
- [ ] Change product owner from Owner A to orphan (Phase 4)
- [ ] Change product owner from orphan to Owner B (Phase 4)
- [x] Get all products returns both owned and orphan
- [x] Get orphan products returns only orphans
- [ ] Get selectable products for Owner A returns their products + orphans (Phase 4)
- [ ] Get selectable products for Owner A does NOT return Owner B's products (Phase 4)

### Frontend Integration (Phase 3) ✅ COMPLETE
- [x] Regenerate API client with new endpoints
- [x] Create ProductService wrapper methods
- [x] Fix compilation errors from DTO changes
- [x] Update swagger.json to support nullable ProductOwnerId
- [x] Build ManageProducts page
- [x] Test page loads and displays products
- [x] Verify orphan badge displays correctly

### Manage Products Page (Phase 3) ✅ COMPLETE
- [x] Display all products
- [x] Show orphan indicator badge/chip
- [x] Filter to show only orphans
- [x] Show warning banner when orphans exist
- [x] Create/edit/delete products
- [x] Proper error handling

### Product Owner Selection (Phase 4)
- [ ] Update ManageProductOwner to use GetSelectableProductsAsync
- [ ] Build product selector showing only valid products
- [ ] Test product assignment (orphan → owner)
- [ ] Test product deselection (owner → orphan)
- [ ] Verify persistence across page refresh

## Known Limitations

1. ~~**API Client Not Regenerated**~~: ✅ FIXED in Phase 3. The NSwag-generated API client has been regenerated with all new endpoints.

2. ~~**No UI for Orphan Management Yet**~~: ✅ FIXED in Phase 3. ManageProducts page provides full orphan management UI.

3. **Order Field for Orphans**: Orphans have Order = 0. If this becomes a problem (e.g., sorting multiple orphans), we may need to add orphan-specific ordering logic.

4. **No Orphan Cascade**: Deleting a Product Owner does NOT automatically orphan their products. They're currently cascade-deleted. This may need review.

5. **Manual Swagger Update**: The swagger.json file was manually updated to make ProductOwnerId nullable in CreateProductRequest. Future API restarts will need to regenerate and update this field.

## Next Session Tasks

### ~~Priority 1: API Client Regeneration~~ ✅ DONE (Phase 3)
The API client has been regenerated with all new endpoints.

### ~~Priority 2: ProductService Updates~~ ✅ DONE (Phase 3)
Added these methods to `PoTool.Client/Services/ProductService.cs`:

```csharp
public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(...)
public async Task<IEnumerable<ProductDto>> GetOrphanProductsAsync(...)
public async Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, ...)
public async Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, ...)
```

### ~~Priority 3: Create ManageProducts.razor~~ ✅ DONE (Phase 3)
Location: `PoTool.Client/Pages/Settings/ManageProducts.razor`
Route: `/settings/products`

Implemented features:
- List all products (uses `GetAllProductsAsync`)
- Show orphan chip/badge for products where ProductOwnerId == null
- Filter toggle to show only orphans
- Warning banner: "X orphaned products need assignment"
- Add/Edit/Delete functionality via ProductEditor

### Priority 4: Product Owner Selection Filter (Phase 4)
Update `PoTool.Client/Pages/Settings/ManageProductOwner.razor`:
- Replace `GetProductsByOwnerAsync` with `GetSelectableProductsAsync`
- Show products owned by this owner + orphaned products
- Add UI to assign orphaned products to this owner
- Add UI to deselect products (makes them orphaned)
- Test ownership changes persist

### Priority 5: TFS Validation (Phase 5)
Update `PoTool.Client/Components/Settings/ProductEditor.razor`:
- Add validation on BacklogRootWorkItemId blur
- Call WorkItemService to verify work item exists
- Show inline error if invalid
- Disable save button if validation fails

## Architecture Notes

### Why Nullable Instead of Sentinel Value?
We could have used ProductOwnerId = -1 or 0 to indicate "no owner", but nullable is clearer:
- Database schema explicitly shows optionality
- No magic numbers
- Better null handling in C#
- Clearer intent in domain model

### Why Two-Step Ownership Transfer?
Direct transfer (Owner A → Owner B) would require:
- Complex authorization (can user access both owners?)
- Atomic transaction across two owner contexts
- More complex UI (select source and target)

Two-step via orphan:
- Simpler authorization (each owner controls their own products)
- Each step is atomic
- Clearer user intent
- Better audit trail

### Order Field Behavior
Products maintain an Order field for display sequencing:
- **Owned products**: Order relative to other products of same owner
- **Orphan products**: Order = 0 (ordering not relevant without owner context)
- **On assignment**: New order calculated as max(existing orders) + 1

This ensures no order conflicts when reassigning products.

## Questions for Product Owner

1. When a Product Owner is deleted, should their products be:
   - Orphaned (current behavior would delete)
   - Cascade deleted (prevents orphan buildup)
   - Prevented (can't delete owner with products)

2. Should orphan products appear anywhere in the main UI, or only in management?

3. Should there be a limit on how long products can remain orphaned?

4. Should we track orphan history (who was the previous owner)?

## References

### Files Modified
- `PoTool.Api/Persistence/Entities/ProductEntity.cs`
- `PoTool.Api/Repositories/ProductRepository.cs`
- `PoTool.Api/Controllers/ProductsController.cs`
- `PoTool.Core/Contracts/IProductRepository.cs`
- `PoTool.Core/Settings/Commands/CreateProductCommand.cs`
- `PoTool.Shared/Settings/ProductDto.cs`
- `PoTool.Client/Pages/ProfilesHome.razor`
- `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
- `PoTool.Client/Components/Settings/ProfileTile.razor`
- `PoTool.Client/Components/Settings/ProfileSelector.razor` (Phase 3)
- `PoTool.Client/Components/Settings/ProductEditor.razor` (Phase 3)
- `PoTool.Client/Services/ProductService.cs` (Phase 3)
- `PoTool.Client/swagger.json` (Phase 3 - manual update for nullable ProductOwnerId)
- `PoTool.Client/ApiClient/ApiClient.g.cs` (Phase 3 - regenerated)

### Files Created
- `PoTool.Core/Settings/Commands/ChangeProductOwnerCommand.cs`
- `PoTool.Core/Settings/Queries/GetAllProductsQuery.cs`
- `PoTool.Core/Settings/Queries/GetOrphanProductsQuery.cs`
- `PoTool.Core/Settings/Queries/GetSelectableProductsQuery.cs`
- `PoTool.Api/Handlers/Settings/Products/ChangeProductOwnerCommandHandler.cs`
- `PoTool.Api/Handlers/Settings/Products/GetAllProductsQueryHandler.cs`
- `PoTool.Api/Handlers/Settings/Products/GetOrphanProductsQueryHandler.cs`
- `PoTool.Api/Handlers/Settings/Products/GetSelectableProductsQueryHandler.cs`
- `PoTool.Api/Migrations/20260110221715_MakeProductOwnerIdNullable.cs`
- `PoTool.Api/Migrations/20260110221715_MakeProductOwnerIdNullable.Designer.cs`
- `PoTool.Client/Pages/Settings/ManageProducts.razor` (Phase 3)

### Commits
1. `8e99404` - Phase 1: Fix immediate UI issues
2. `af8eaa1` - Phase 2: Backend support for orphan products
3. `e8a9ed1` - Phase 3: ProductService wrapper methods and ManageProducts page

---

**Author**: GitHub Copilot Agent  
**Date**: 2026-01-10  
**Status**: Phases 1-3 Complete, Ready for Phase 4
