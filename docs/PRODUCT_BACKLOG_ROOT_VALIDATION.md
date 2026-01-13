# Product Backlog Root Work Item ID Validation

## Overview

When creating or editing a Product in the Add Product view, the Backlog Root Work Item ID field requires validation to ensure the work item exists in TFS/Azure DevOps.

## How It Works

The validation process is designed to **always query TFS directly** and **never use the local cache**.

### Validation Flow

1. User enters a work item ID in the Add Product form
2. On blur or save, `ProductEditor.razor` calls `WorkItemService.ValidateWorkItemAsync()`
3. Client sends request to API endpoint `/api/workitems/validate`
4. API sends `ValidateWorkItemQuery` via Mediator
5. `ValidateWorkItemQueryHandler` calls `ITfsClient.GetWorkItemByIdAsync()`
6. **In Real Mode:** `RealTfsClient` makes HTTP POST to TFS API (`_apis/wit/workitemsbatch`)
7. **In Mock Mode:** `MockTfsClient` returns data from in-memory mock data facade
8. Response is returned with `Exists`, `Title`, `Type`, and optional `ErrorMessage`

### Key Characteristics

#### ✅ What It Does

- **Direct TFS Query**: Always calls TFS/Azure DevOps API directly
- **Cache Bypass**: Never reads from SQLite cache (WorkItemRepository)
- **Real-time Validation**: Ensures work item exists at the moment of validation
- **Stale Cache Handling**: Works correctly even when cache is empty or outdated
- **Error Handling**: Returns clear error messages for auth, connection, or validation failures

#### ❌ What It Does NOT Do

- Does NOT read from WorkItemRepository (SQLite cache)
- Does NOT assume work item exists based on cache
- Does NOT allow invalid IDs even if cache contains unrelated work items
- Does NOT cache validation results between requests

## Mode-Specific Behavior

### Real Mode (Production)

When running with real TFS configuration:

```csharp
ITfsClient → RealTfsClient → HTTP POST to TFS
```

- Makes actual HTTP call to `{TfsUrl}/_apis/wit/workitemsbatch`
- Requires valid TFS PAT token
- Returns 404 if work item doesn't exist
- Returns auth errors if credentials invalid

### Mock Mode (Development/Testing)

When running in mock mode:

```csharp
ITfsClient → MockTfsClient → BattleshipMockDataFacade
```

- Returns work items from in-memory mock data hierarchy
- No HTTP calls made
- Simulates TFS behavior for development
- **Note:** This is NOT the same as the SQLite cache!

## Why This Design?

### Problem It Solves

Without direct TFS validation, these issues would occur:

1. **Stale Cache**: User creates product with ID 12345, but cache doesn't have it yet → validation fails incorrectly
2. **Empty Cache**: First-time user has no cached data → all validations fail
3. **Wrong Product**: Cache has data from different area paths → validates wrong work items

### Design Decision

The validation intentionally bypasses cache to ensure:

- Product backlog root IDs are always current
- Validation is independent of sync/cache state
- Users can create products immediately after setup
- Cross-product area path validation works correctly

## Testing

See `PoTool.Tests.Unit/Handlers/ValidateWorkItemQueryHandlerTests.cs` for comprehensive test coverage:

- ✅ Valid work item returns success
- ✅ Non-existent work item returns not found
- ✅ TFS auth failure returns auth error
- ✅ Connection failure returns connection error
- ✅ Generic errors return error message
- ✅ Multiple validations call TFS each time (no caching)

All tests verify that `ITfsClient.GetWorkItemByIdAsync()` is called, proving cache is not used.

## Troubleshooting

### "Work item validated successfully" but I can't see it in the app

- **Cause**: Work item exists in TFS but hasn't been synced to cache yet
- **Solution**: This is normal! Validation only checks existence. Sync will pull the work item later.

### Validation fails but work item exists in TFS

Possible causes:

1. **TFS Configuration**: Check TFS URL and PAT token in settings
2. **Permissions**: Ensure PAT has work item read permissions
3. **Network**: Check connection to TFS server
4. **Work Item Type**: Verify work item type is supported (Epic, Feature, etc.)

### Running in Mock Mode

- Mock mode uses predefined mock data from `BattleshipMockDataFacade`
- Only mock work item IDs will validate successfully
- Check mock data setup in `PoTool.Api/Services/MockData/`

## Related Files

- **UI**: `PoTool.Client/Components/Settings/ProductEditor.razor`
- **Service**: `PoTool.Client/Services/WorkItemService.cs`
- **Handler**: `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs`
- **Query**: `PoTool.Core/WorkItems/Queries/ValidateWorkItemQuery.cs`
- **Tests**: `PoTool.Tests.Unit/Handlers/ValidateWorkItemQueryHandlerTests.cs`
- **Real Client**: `PoTool.Api/Services/RealTfsClient.cs`
- **Mock Client**: `PoTool.Api/Services/MockTfsClient.cs`
- **Cache**: `PoTool.Api/Repositories/WorkItemRepository.cs` (NOT used for validation)

## Summary

**Product Backlog Root Work Item ID validation always queries TFS directly and never uses the cache.** This ensures validation accuracy regardless of cache state and provides a reliable user experience when creating or editing products.
