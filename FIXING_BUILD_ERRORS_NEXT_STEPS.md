# Next Steps to Complete Build Error Fixes

## Current Status
- Fixed 3 string-to-int conversion errors by adding fix-openapi-types.ps1
- Identified root cause of 40+ API method naming errors
- Created infrastructure to fix operation ID generation

## Root Cause Analysis
The API client method names don't match what the code expects because:
1. NSwag client generator is set to `operationGenerationMode: MultipleClientsFromPathSegments`
2. This generates method names from URL paths (e.g., `VelocityAsync`) instead of controller method names (e.g., `GetVelocityTrendAsync`)
3. The OpenAPI spec doesn't include operation IDs from controller method names

## Solution Created (Needs Testing)
1. Added `OperationMethodNameProcessor` to API's OpenAPI configuration
   - File: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
   - This should make NSwag.AspNetCore use controller method names as operation IDs

2. Created post-processing scripts:
   - `tools/add-operation-ids.ps1` - Adds operation IDs to openapi.json
   - `tools/fix-openapi-types.ps1` - Fixes integer union type issues
   - `tools/regenerate-with-enums.ps1` - Complete regeneration workflow

3. Updated nswag.json to use operation IDs:
   - Change `operationGenerationMode` to `MultipleClientsFromOperationId`

## To Complete the Fix

### Option 1: Test the Automated Solution
```powershell
cd tools
./regenerate-with-enums.ps1
```

This should:
- Generate OpenAPI spec with operation IDs from controller method names
- Fix integer type issues
- Generate client with correct method names like `GetVelocityTrendAsync`

### Option 2: Manual Steps if Script Fails
1. Start API without Client reference to generate OpenAPI:
   ```bash
   # Temporarily comment out Client reference in PoTool.Api/PoTool.Api.csproj
   dotnet run --project PoTool.Api
   # Download from http://localhost:5291/openapi/v1.json
   ```

2. Fix OpenAPI types:
   ```powershell
   ./tools/fix-openapi-types.ps1
   ```

3. Add operation IDs (if OperationMethodNameProcessor didn't work):
   ```powershell
   ./tools/add-operation-ids.ps1
   ```

4. Change nswag.json:
   ```json
   "operationGenerationMode": "MultipleClientsFromOperationId"
   ```

5. Regenerate client:
   ```bash
   cd PoTool.Client
   dotnet nswag run nswag.json
   ```

6. Restore API project reference and rebuild

### Option 3: If Operation IDs Still Don't Work
If the OperationMethodNameProcessor doesn't set operation IDs correctly, you may need to:
1. Add `[HttpGet("velocity", Name = "GetVelocityTrend")]` attributes to all controller methods
2. OR accept that method names won't match exactly and do a find-replace across the codebase

## Remaining Errors After Method Names Are Fixed
Once the ~40 API method naming errors are resolved, these remain:

### TimeSpan/String Conversion Errors (~15)
- Files: `StateTimeline.razor`, `PullRequestMetricsService.cs`
- Issue: Properties are `string` but code expects `TimeSpan`
- Fix: Check DTO definitions and either fix the types or convert in the calling code

### Enum Conversion Errors (~30)
- Files: Multiple Razor pages
- Issue: Properties are `int` but code expects enum types
- Fix: Cast to enum types or fix DTO generation to use enums

### Nullable Issues (~5)
- Files: `PipelineMetricsSummaryPanel.razor`, etc.
- Issue: Properties changed nullability after OpenAPI fix
- Fix: Add null checks or update property types

### Misc (~3)
- StartupReadinessDto type conflict
- Lambda return type mismatches

## Testing
After all fixes:
```bash
dotnet build PoTool.sln
dotnet test PoTool.sln
```

## Files Modified
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` - Added OperationMethodNameProcessor
- `PoTool.Client/nswag.json` - Ready to switch to MultipleClientsFromOperationId
- `tools/regenerate-with-enums.ps1` - Updated workflow
- `tools/add-operation-ids.ps1` - New script
- `tools/fix-openapi-types.ps1` - New script

