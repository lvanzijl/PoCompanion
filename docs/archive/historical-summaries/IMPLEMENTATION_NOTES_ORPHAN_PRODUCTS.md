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

## Phase 4 Implementation (Product Selection Filtering)

### Goals
1. Update ManageProductOwner page to show selectable products (owned + orphaned)
2. Add UI for removing products from owner (make orphan)
3. Add UI for assigning orphaned products to owner
4. Implement two-step ownership transfer workflow

### ManageProductOwner Page Updates
**File**: `PoTool.Client/Pages/Settings/ManageProductOwner.razor`

#### Data Loading Changes
Changed from loading only owned products to loading selectable products:

```csharp
// OLD: Only owned products
_products = await ProductService.GetProductsByOwnerAsync(ProfileId);

// NEW: Owned + orphaned products, filtered
var selectableProducts = await ProductService.GetSelectableProductsAsync(ProfileId);
_products = selectableProducts.Where(p => p.ProductOwnerId == ProfileId).ToList();
_availableOrphans = selectableProducts.Where(p => p.ProductOwnerId == null).ToList();
```

This separates owned products (shown in main list) from available orphans (shown in separate section).

#### UI Additions

**1. Remove from Owner Button**
Added to each owned product:
- Orange warning icon (`RemoveCircleOutline`)
- Title: "Remove from this owner (make orphan)"
- Confirmation dialog explaining the action
- Calls `ChangeProductOwnerAsync(productId, null)` to orphan the product

**2. Available Orphaned Products Section**
New section shown when orphans exist:
- Header: "Available Orphaned Products" (warning color)
- Explanation text
- List of orphan products with orange "Orphan" badge
- "Assign to Owner" button (green, success color)
- Calls `ChangeProductOwnerAsync(productId, ProfileId)` to assign

#### New Methods

```csharp
private async Task RemoveProductFromOwner(ProductDto product)
{
    // Confirmation dialog
    var result = await DialogService.ShowMessageBox(
        "Remove Product from Owner",
        "...The product will become orphaned...",
        yesText: "Remove", cancelText: "Cancel");
    
    if (result == true)
    {
        await ProductService.ChangeProductOwnerAsync(product.Id, null);
        // Success message and reload
    }
}

private async Task AssignOrphanToOwner(ProductDto orphan)
{
    // Confirmation dialog
    var result = await DialogService.ShowMessageBox(
        "Assign Product to Owner",
        $"Assign '{orphan.Name}' to {_profile?.Name}?",
        yesText: "Assign", cancelText: "Cancel");
    
    if (result == true)
    {
        await ProductService.ChangeProductOwnerAsync(orphan.Id, ProfileId);
        // Success message and reload
    }
}
```

### Two-Step Ownership Transfer Workflow

**Complete Flow Example:**

1. **User wants to move Product X from Owner A to Owner B**

2. **Step 1: Remove from Owner A**
   - Navigate to `/settings/productowner/{ownerA_id}`
   - Find Product X in the owned products list
   - Click orange "Remove from Owner" icon
   - Confirm action
   - Product X becomes orphaned (ProductOwnerId = null)
   - Product X disappears from Owner A's list

3. **Step 2: Assign to Owner B**
   - Navigate to `/settings/productowner/{ownerB_id}`
   - Product X appears in "Available Orphaned Products" section
   - Click green "Assign to Owner" button
   - Confirm action
   - Product X assigned to Owner B (ProductOwnerId = ownerB_id)
   - Product X appears in Owner B's owned products list

### UX Design Decisions

**Visual Distinction:**
- Owned products: Standard list with edit/delete/reorder buttons
- Orphaned products: Separate section below, marked with warning color header
- Orange "Orphan" badge on available products
- Green "Assign" button to make action clear

**Confirmation Dialogs:**
- "Remove from Owner": Explains product becomes orphaned
- "Assign to Owner": Shows owner name for clarity
- Both use clear Yes/No language

**Immediate Feedback:**
- Success/error snackbar messages
- Automatic page reload after ownership change
- Orphan count updates in real-time

**Accessibility:**
- Icon buttons have title attributes
- Clear button labels ("Assign to Owner")
- Descriptive alert text above orphan section

## Testing Checklist for Next Phase

### Backend Testing
- [x] Create orphan product via API (via ManageProducts page)
- [x] Create owned product via API (via Product Owner page)
- [x] Change product owner from Owner A to orphan (via Remove button)
- [x] Change product owner from orphan to Owner B (via Assign button)
- [x] Get all products returns both owned and orphan
- [x] Get orphan products returns only orphans
- [x] Get selectable products for Owner A returns their products + orphans
- [x] Get selectable products for Owner A does NOT return Owner B's products (implicit filtering)

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

### Product Owner Selection (Phase 4) ✅ COMPLETE
- [x] Update ManageProductOwner to use GetSelectableProductsAsync
- [x] Show owned products + available orphaned products separately
- [x] Implement "Remove from Owner" UI (deselect → make orphan)
- [x] Implement "Assign to Owner" UI (select orphan → assign)
- [x] Test product ownership changes with confirmation dialogs
- [x] Verify persistence (changes persist immediately)

### TFS Work Item Validation (Phase 5) ✅ COMPLETE
- [x] Update ProductEditor to validate BacklogRootWorkItemId on blur
- [x] Call WorkItemService.GetByTfsIdAsync to verify work item exists
- [x] Show inline error if invalid
- [x] Block save button if validation fails
- [x] Add loading spinner during validation
- [x] Show success indicator when validation passes

### Team Deletion Behavior (Phase 6) ✅ COMPLETE
- [x] Created DeleteTeamCommand for hard delete
- [x] Implemented repository method with product link cleanup
- [x] Created handler and API endpoint DELETE /api/teams/{id}
- [x] Team deletion removes ProductTeamLink entities before deleting team
- [x] Returns appropriate status codes (204/404)

### Draft Persistence (Phase 7) ✅ COMPLETE
- [x] Design draft storage strategy (localStorage)
- [x] Create DraftStorageService for browser storage
- [x] Create ProductEditorDraft model
- [x] Implement draft state save on form change
- [x] Implement draft state restore on page load
- [x] Clear draft on successful save
- [x] Clear draft on cancel
- [x] Register service in DI container

### Integration & Testing (Phase 8) ✅ DOCUMENTED
- [x] End-to-end workflow fully functional
- [x] Orphan creation tested via ManageProducts page
- [x] Orphan assignment tested via ManageProductOwner page
- [x] Product selection filtering verified
- [x] TFS validation tested with real-time feedback
- [x] Team deletion CASCADE behavior verified in code
- [x] All acceptance criteria met
- [x] Documentation comprehensive and maintained

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

### ~~Priority 4: Product Owner Selection Filter (Phase 4)~~ ✅ DONE
Updated `PoTool.Client/Pages/Settings/ManageProductOwner.razor`:
- Uses `GetSelectableProductsAsync` to load owned + orphaned products
- Separates owned products from available orphans
- Added "Remove from Owner" button (makes product orphaned)
- Added "Available Orphaned Products" section with "Assign to Owner" buttons
- Confirmation dialogs for both operations
- Changes persist immediately

### Priority 5: TFS Validation (Phase 5) - NEXT
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
- `PoTool.Client/Pages/Settings/ManageProductOwner.razor` (Phases 1, 4)
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
4. `61a1c89` - Phase 4: Product selection filtering and assignment UI
5. `078e80a` - Phase 5: Implement TFS Work Item validation in ProductEditor
6. `5664885` - Phase 6: Implement team deletion with product link cleanup
7. `80438b4` - Phase 7: Implement draft persistence for ProductEditor

---

**Author**: GitHub Copilot Agent  
**Date**: 2026-01-10  
**Status**: All Phases 1-7 Complete ✅

## Phase 7 Implementation (Draft Persistence)

### Goals
1. Persist form state across navigation and page refreshes
2. Prevent data loss when users navigate away
3. Maintain wizard stack navigation pattern
4. Provide seamless UX for form completion

### DraftStorageService
**File**: `PoTool.Client/Storage/DraftStorageService.cs`

Browser-based localStorage wrapper for form state persistence:

```csharp
public class DraftStorageService
{
    private const string DraftKeyPrefix = "draft_";
    
    // Save draft to localStorage
    public async Task SaveDraftAsync<T>(string key, T data)
    
    // Load draft from localStorage
    public async Task<T?> LoadDraftAsync<T>(string key)
    
    // Clear specific draft
    public async Task ClearDraftAsync(string key)
    
    // Clear all drafts
    public async Task ClearAllDraftsAsync()
}
```

**Key Features:**
- Uses browser localStorage (5MB limit)
- JSON serialization for complex state
- Unique keys per context (e.g., "draft_product_edit_0_0")
- Silent failure handling (draft persistence not critical)
- Prefix for easy identification and cleanup

### ProductEditorDraft Model
**File**: `PoTool.Client/Models/ProductEditorDraft.cs`

```csharp
public class ProductEditorDraft
{
    public string Name { get; set; }
    public string BacklogRootWorkItemIdText { get; set; }
    public int SelectedImageId { get; set; }
    public List<int> SelectedTeamIds { get; set; }
}
```

Captures all form state for ProductEditor component.

### ProductEditor Integration
**File**: `PoTool.Client/Components/Settings/ProductEditor.razor`

#### Draft Key Generation
```csharp
private string DraftKey => $"product_edit_{ProductOwnerId}_{(Product?.Id ?? 0)}";
```

Unique key combines:
- Product Owner ID (context)
- Product ID (0 for new products)

#### OnInitializedAsync
```csharp
protected override async Task OnInitializedAsync()
{
    if (Product != null)
    {
        // Editing existing product - use product data
        _name = Product.Name;
        // ... other fields
    }
    else
    {
        // Creating new product - try to restore draft
        await RestoreDraftAsync();
    }
}
```

**Logic:**
- Existing products: Load from Product parameter (no draft needed)
- New products: Attempt to restore draft from localStorage

#### Auto-Save on Field Changes
All form fields updated with auto-save:

```razor
<CompactTextField @bind-Value="_name" 
              @bind-Value:after="@(() => SaveDraftAsync())" />

<CompactTextField @bind-Value="_backlogRootWorkItemIdText"
              @bind-Value:after="@(() => SaveDraftAsync())" />
              
<CompactSelect @bind-SelectedValues="_selectedTeamIds"
           @bind-SelectedValues:after="@(() => SaveDraftAsync())">
```

**Behavior:**
- Every field change triggers `SaveDraftAsync()`
- Debounced by Blazor's binding system
- Non-blocking (async)

#### SaveDraftAsync Method
```csharp
private async Task SaveDraftAsync()
{
    // Only save drafts for new products (not when editing existing ones)
    if (IsEditing) return;

    var draft = new ProductEditorDraft
    {
        Name = _name,
        BacklogRootWorkItemIdText = _backlogRootWorkItemIdText,
        SelectedImageId = _selectedImageId,
        SelectedTeamIds = _selectedTeamIds.ToList()
    };

    await DraftStorageService.SaveDraftAsync(DraftKey, draft);
}
```

**Design Decisions:**
- Skip for editing (existing products already persisted)
- Serialize all form state
- Silent failure (doesn't break UX)

#### RestoreDraftAsync Method
```csharp
private async Task RestoreDraftAsync()
{
    var draft = await DraftStorageService.LoadDraftAsync<ProductEditorDraft>(DraftKey);
    
    if (draft != null)
    {
        _name = draft.Name;
        _backlogRootWorkItemIdText = draft.BacklogRootWorkItemIdText;
        _selectedImageId = draft.SelectedImageId;
        _selectedTeamIds = draft.SelectedTeamIds;
        
        if (int.TryParse(_backlogRootWorkItemIdText, out var workItemId))
        {
            _backlogRootWorkItemId = workItemId;
        }
    }
}
```

**Behavior:**
- Loads draft from localStorage
- Populates all form fields
- Parses work item ID if valid
- No-op if draft not found

#### Draft Clearing
**On successful save:**
```csharp
Snackbar.Add("Product created successfully", Severity.Success);
await ClearDraftAsync();  // Clear after success
```

**On cancel:**
```csharp
private async Task Cancel()
{
    if (!IsEditing)
    {
        await ClearDraftAsync();  // Clear on abandon
    }
    await OnCancelled.InvokeAsync();
}
```

**Logic:**
- Save: Clear draft (no longer needed)
- Cancel: Clear draft (user abandoned)
- Edit: Never clear (no draft exists)

### DI Registration
**File**: `PoTool.Client/Program.cs`

```csharp
builder.Services.AddScoped<DraftStorageService>();
```

Registered as scoped service (lifetime per browser tab).

### User Experience Flow

**Scenario 1: Happy Path**
1. User navigates to "Add Product"
2. User fills in name: "MyProduct" → Auto-saved to localStorage
3. User enters work item ID: "12345" → Auto-saved
4. User selects teams → Auto-saved
5. User clicks Save → Product created, draft cleared
6. User returns to "Add Product" → Form empty (draft was cleared)

**Scenario 2: Navigation Away**
1. User navigates to "Add Product"
2. User fills in name: "MyProduct" → Auto-saved
3. User enters work item ID: "12345" → Auto-saved
4. User navigates away (e.g., clicks back)
5. User returns to "Add Product" → Form shows "MyProduct" and "12345" (draft restored)
6. User completes form and saves → Draft cleared

**Scenario 3: Browser Refresh**
1. User navigates to "Add Product"
2. User fills in partial data → Auto-saved
3. User refreshes browser (F5)
4. Form reloads → Data restored from localStorage
5. User continues editing

**Scenario 4: Cancel**
1. User navigates to "Add Product"
2. User fills in data → Auto-saved
3. User clicks Cancel → Draft cleared
4. User returns to "Add Product" → Form empty (draft was cleared)

### Benefits

**User Benefits:**
- No data loss on accidental navigation
- Can safely refresh browser
- Can resume work after interruption
- Seamless multi-step workflow

**Technical Benefits:**
- Minimal code (~100 lines)
- No server storage needed
- Works offline
- Fast (localStorage is synchronous)
- Graceful degradation

### Limitations

**Scope:**
- Only implemented for ProductEditor (new products)
- Not implemented for:
  - Team creation
  - Product Owner creation
  - Product editing (not needed - already persisted)

**Technical:**
- localStorage 5MB limit (sufficient for forms)
- Per-domain storage (doesn't sync across devices)
- Cleared if user clears browser data
- No encryption (don't store sensitive data)

**Future Enhancements:**
- Extend to other forms if needed
- Add expiration timestamps
- Implement draft list/management UI
- Add cross-tab synchronization

### Testing Considerations

**Manual Testing:**
1. Start creating a product
2. Fill in some fields
3. Navigate away
4. Return → Verify data restored
5. Complete and save → Verify draft cleared
6. Try creating another product → Verify form empty

**Browser DevTools:**
```javascript
// View draft in console
localStorage.getItem('draft_product_edit_0_0')

// Clear all drafts
Object.keys(localStorage)
  .filter(k => k.startsWith('draft_'))
  .forEach(k => localStorage.removeItem(k))
```

### Error Handling

All localStorage operations wrapped in try-catch:
- Quota exceeded → Silent failure
- Security error → Silent failure
- Parse error → Silent failure

**Rationale:** Draft persistence is a nice-to-have, not critical. Form still works without it.

