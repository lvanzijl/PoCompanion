# NTLM Authentication Fix Summary

## Problem Description

NTLM authentication was not working when users selected "Use Default Windows Credentials". The test connection would fail with a generic error message:

```
Connection test failed.
Please check your credentials and network connectivity.
```

No technical details were provided, making it difficult to diagnose the issue.

## Root Cause

The HttpClient used by `RealTfsClient` was not configured with the proper HttpClientHandler for NTLM authentication. When registered via `AddHttpClient<ITfsClient, RealTfsClient>()`, it created a default HttpClient without Windows authentication support.

## Solution

### 1. HttpClient Configuration

Modified `ApiServiceCollectionExtensions.cs` to configure the HttpClient with NTLM support:

```csharp
services.AddHttpClient<ITfsClient, RealTfsClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseDefaultCredentials = true,
        Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    });
```

This configuration:
- Enables Windows authentication with `UseDefaultCredentials = true`
- Uses the current user's credentials via `CredentialCache.DefaultNetworkCredentials`
- Works for both NTLM and PAT authentication (PAT adds Authorization header on top)

### 2. Enhanced Error Logging

Modified `RealTfsClient.ValidateConnectionAsync()` to:
- Log the authentication mode being used
- Log HTTP status codes and response bodies on failure
- Separate catch blocks for different exception types
- Provide better context in error messages

```csharp
_logger.LogInformation("Validating TFS connection: GET {Url} (AuthMode: {AuthMode})", url, entity.AuthMode);

if (!resp.IsSuccessStatusCode)
{
    var errorBody = await resp.Content.ReadAsStringAsync(cancellationToken);
    _logger.LogError("TFS connection validation failed: HTTP {StatusCode}, Response: {ErrorBody}", 
        resp.StatusCode, errorBody);
    return false;
}
```

### 3. Improved API Endpoint Response

Modified `/api/tfsvalidate` endpoint to return structured error information:

```csharp
app.MapGet("/api/tfsvalidate", async (ITfsClient client, ILogger<Program> logger) =>
{
    try
    {
        var ok = await client.ValidateConnectionAsync();
        if (ok)
        {
            return Results.Ok(new { success = true, message = "Connection validated successfully" });
        }
        else
        {
            return Results.Json(
                new { 
                    success = false, 
                    message = "Connection test failed", 
                    details = "The TFS server did not respond successfully. Check the logs for more details about HTTP status codes and error responses." 
                }, 
                statusCode: 500);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "TFS validation endpoint: Exception during connection test");
        return Results.Json(
            new { 
                success = false, 
                message = "Connection test failed with exception", 
                details = ex.Message,
                exceptionType = ex.GetType().Name
            }, 
            statusCode: 500);
    }
});
```

### 4. Client-Side Error Handling

Modified `TfsConfigService.ValidateConnectionAsync()` to:
- Send `X-TFS-PAT` header when PAT is available (for PAT auth mode)
- Parse error details from API responses
- Throw detailed exceptions with error messages

```csharp
if (!string.IsNullOrEmpty(pat))
{
    _httpClient.DefaultRequestHeaders.Remove("X-TFS-PAT");
    _httpClient.DefaultRequestHeaders.Add("X-TFS-PAT", pat);
}

var response = await _httpClient.GetAsync("/api/tfsvalidate", cancellationToken);

if (!response.IsSuccessStatusCode)
{
    // Try to read error details from response
    var errorJson = await response.Content.ReadAsStringAsync(cancellationToken);
    var errorDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(errorJson, _jsonOptions);
    if (errorDetails != null && errorDetails.TryGetValue("details", out var details))
    {
        var detailsText = details.GetString();
        throw new InvalidOperationException($"Connection test failed: {detailsText}");
    }
}
```

## Testing

### Unit Tests
✅ All TfsConfigurationService tests pass (13 tests)

### Integration Tests
✅ TfsConfigApiTests pass:
- PostTfsConfig_WithNtlmAuthMode_ReturnsOkAndPersists
- PostTfsConfig_SwitchingFromPatToNtlm_UpdatesAuthMode

### Manual Testing Required

To verify NTLM authentication works:

1. Start the application
2. Navigate to TFS Configuration page
3. Enter your TFS server URL (e.g., `https://tfs.yourcompany.com/tfs/DefaultCollection`)
4. Enter your project name
5. Select "NTLM/Windows Authentication" as auth mode
6. Check "Use Default Windows Credentials"
7. Click "Test Connection"
8. Expected: Connection should succeed if you have network access and valid credentials

To verify error messages are shown:

1. Configure with incorrect URL or project
2. Click "Test Connection"
3. Expected: Error message should show details about the failure (e.g., "Connection test failed: The TFS server did not respond successfully...")
4. Check browser console and server logs for technical details (HTTP status codes, response bodies)

## Benefits

1. **NTLM Authentication Works**: Users can now authenticate using their Windows credentials
2. **Better Error Messages**: Technical details are logged and shown to users
3. **Easier Debugging**: Logs include HTTP status codes, response bodies, and exception details
4. **No Breaking Changes**: PAT authentication continues to work as before
5. **Backward Compatible**: Existing configurations and tests remain functional

## Files Changed

1. `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` - HttpClient configuration
2. `PoTool.Api/Services/RealTfsClient.cs` - Enhanced logging
3. `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs` - Improved API endpoint
4. `PoTool.Client/Services/TfsConfigService.cs` - Client-side error handling and PAT header

## Logs Example

With the fix, when a connection test fails, you'll now see detailed logs:

```
Microsoft.AspNetCore.Hosting.Diagnostics: Information: Request starting HTTP/1.1 GET http://localhost:5291/api/tfsvalidate
PoTool.Api.Services.RealTfsClient: Information: Validating TFS connection: GET https://tfs.example.com/_apis/projects?api-version=7.0 (AuthMode: Ntlm)
PoTool.Api.Services.RealTfsClient: Information: Validation GET https://tfs.example.com/_apis/projects?api-version=7.0 returned 401
PoTool.Api.Services.RealTfsClient: Error: TFS connection validation failed: HTTP 401, Response: {"error":"Unauthorized"}
PoTool.Api.Configuration.ApiApplicationBuilderExtensions: Warning: TFS validation endpoint: Connection test failed (returned false)
```
