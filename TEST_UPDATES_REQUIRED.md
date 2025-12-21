# Test Updates Required for PAT Storage Changes

## Overview

The PAT storage implementation has been updated to use client-side secure storage (MAUI SecureStorage) instead of server-side database storage. This is a breaking change that requires test updates.

## Tests Requiring Updates

### TfsConfigurationServiceTests.cs

The following tests need to be updated or removed:

#### Tests to Remove (Test Obsolete Functionality):
1. `SaveConfigAsync_ValidInput_EncryptsAndStoresConfig` - Tests PAT encryption, which is no longer done server-side
2. `SaveConfigAsync_UpdateExisting_UpdatesEncryptedPat` - Tests PAT update in database, which is no longer done
3. `UnprotectPatEntity_ValidEntity_ReturnsDecryptedPat` - Tests decryption method that no longer exists
4. `UnprotectPatEntity_NullEntity_ReturnsNull` - Tests decryption method that no longer exists
5. `UnprotectPatEntity_InvalidProtectedPat_ReturnsNullAndLogsWarning` - Tests decryption error handling, no longer relevant
6. `SaveConfigAsync_EmptyPat_EncryptsEmptyString` - Tests empty PAT encryption, no longer done server-side
7. `SaveConfigAsync_SpecialCharactersInPat_EncryptsCorrectly` - Tests PAT with special chars encryption, obsolete
8. `SaveConfigAsync_VeryLongPat_EncryptsCorrectly` - Tests long PAT encryption, obsolete

#### Tests to Update:
1. `SaveConfigAsync_ValidInput_*` - Update to verify only URL and Project are saved (not PAT)
2. `GetConfigAsync_*` - Update to verify PAT is not returned in config

### TfsClientTests.cs

Tests that call `SaveConfigAsync` with PAT parameter need to be updated:
- Update method calls to use new signature (without PAT parameter)
- Mock the TfsClient behavior to not expect PAT from config

### New Tests to Add

#### Client-Side Storage Tests:
1. Test `ISecureStorageService` interface behavior
2. Test `MauiSecureStorageService` implementation
3. Test `TfsConfigService.GetPatAsync()` retrieval
4. Test `TfsConfigService.SaveConfigAsync()` with client-side PAT storage

#### Integration Tests:
1. Test full flow: User enters PAT → Stored in SecureStorage → Retrieved for TFS operations
2. Test PAT not persisted to server database
3. Test PAT not exposed in API responses

## Approach

### Option 1: Update Existing Tests
- Modify tests to work with new architecture
- Add new tests for client-side secure storage
- Keep test coverage at same level

### Option 2: Remove Obsolete Tests (Recommended for MVP)
- Remove tests for functionality that no longer exists (server-side PAT storage)
- Add basic tests for new secure storage service
- Full test coverage can be added later as needed

## Implementation Notes

The following changes were made to the codebase:
1. `TfsConfigurationService.SaveConfigAsync()` signature changed from `(url, project, pat)` to `(url, project)`
2. `TfsConfigurationService.UnprotectPatEntity()` method removed
3. `TfsConfigEntity.ProtectedPat` property removed
4. `ISecureStorageService` and `MauiSecureStorageService` added for client-side PAT storage

## Recommendation

For the initial implementation (following the "minimal changes" principle), I recommend:

1. **Comment out or delete obsolete tests** that test removed functionality
2. **Update tests** that call changed methods to use new signatures
3. **Add basic smoke tests** for new `ISecureStorageService` functionality
4. **Defer comprehensive testing** to a future task when the full TFS client flow is refactored to accept PAT as a parameter

This approach allows us to ship the security improvement (client-side PAT storage) without spending excessive time on tests for deprecated functionality.

## Files to Update

- `PoTool.Tests.Unit/TfsConfigurationServiceTests.cs` - Remove/update 10+ tests
- `PoTool.Tests.Unit/TfsClientTests.cs` - Update 4-5 tests
- Add new test file: `PoTool.Tests.Unit/Services/SecureStorageServiceTests.cs` (optional for MVP)
