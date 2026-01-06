# NTLM Authentication Fix - Authorization Header Conflict

## Issue Description

Users reported that NTLM authentication fails with a **400 Bad Request** error when trying to sync work items, even though the test connection succeeds.

### Error Log
```
System.Net.Http.HttpClient.ITfsClient.ClientHandler: Information: Received HTTP response headers after 14.6485ms - 400
PoTool.Api.Services.RealTfsClient: Error: TFS HTTP error: BadRequest - TFS request failed: BadRequest
```

## Root Cause

The application uses a **shared HttpClient instance** (scoped lifetime) that is configured with an NTLM handler at startup. When switching between PAT and NTLM authentication modes, the following issue occurs:

1. When **PAT mode** is used, `ConfigurePatAuthentication()` sets the `Authorization` header on `HttpClient.DefaultRequestHeaders`
2. These headers **persist** on the HttpClient instance for subsequent requests
3. When switching to **NTLM mode**, the old PAT `Authorization` header remains
4. TFS/Azure DevOps receives **both** NTLM credentials (from HttpClientHandler) AND a PAT Authorization header
5. This causes a **400 Bad Request** because the server sees conflicting authentication methods

## Solution

The fix ensures that the `Authorization` header is **explicitly cleared** when switching to NTLM mode:

```csharp
private async Task ConfigureAuthenticationAsync(TfsConfigEntity entity, CancellationToken cancellationToken)
{
    if (entity.AuthMode == TfsAuthMode.Pat)
    {
        // ... configure PAT authentication ...
    }
    else if (entity.AuthMode == TfsAuthMode.Ntlm)
    {
        // Clear any Authorization header that might have been set by previous PAT requests
        // NTLM authentication is handled by the HttpClientHandler, not by headers
        _httpClient.DefaultRequestHeaders.Authorization = null;
        
        _logger.LogDebug("Configured NTLM authentication for TFS request (cleared Authorization header)");
    }
}
```

### Why This Works

- **NTLM authentication** is handled entirely by the `HttpClientHandler` configured at startup
- It does NOT require any `Authorization` header to be set
- By clearing the header, we ensure there's no conflict between handler-based auth and header-based auth

### Additional Improvements

Added defensive error handling for unsupported authentication modes:
```csharp
else
{
    throw new TfsAuthenticationException(
        $"Unsupported authentication mode: {entity.AuthMode}. " +
        "Only PAT and NTLM modes are supported.", 
        (string?)null);
}
```

This ensures that if a new authentication mode is added to the enum in the future, the code will fail fast with a clear error message rather than silently doing nothing.

## Testing

### Build Verification
✅ Solution builds successfully without errors or warnings

### Unit Tests
✅ No authentication-related test failures
- All TfsConfigurationService tests pass
- All RealTfsClient tests pass
- Pre-existing test failures (unrelated to auth) remain unchanged

### Manual Testing

To verify this fix:

1. Configure TFS with NTLM authentication
2. Test the connection (should succeed)
3. Try syncing work items (should now succeed instead of 400 Bad Request)
4. Switch to PAT authentication (should still work)
5. Switch back to NTLM authentication (should work without conflicts)

## Files Changed

1. `PoTool.Api/Services/RealTfsClient.cs` - Added header cleanup for NTLM mode

## Impact

- **Minimal change**: Only 8 lines added
- **No breaking changes**: PAT authentication continues to work as before
- **No new dependencies**: Uses existing HttpClient APIs
- **Backward compatible**: Existing configurations work correctly

## Related Documentation

- See `NTLM_FIX_SUMMARY.md` for previous NTLM authentication fixes
- See `docs/PAT_STORAGE_BEST_PRACTICES.md` for PAT storage architecture
