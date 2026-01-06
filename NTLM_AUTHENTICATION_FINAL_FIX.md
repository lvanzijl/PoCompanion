# NTLM Authentication Final Fix

## Executive Summary

This document provides a comprehensive explanation of the recurring NTLM authentication issue and its resolution.

**IMPORTANT UPDATE**: This document previously contained incorrect guidance about WIQL endpoint URLs. The correct approach is documented below and aligned with `NTLM_WIQL_URL_FIX.md`.

## The Problem

Users have repeatedly reported that NTLM authentication fails with a BadRequest (400) error when attempting to sync work items from TFS. This issue has been reported and "fixed" multiple times but kept recurring.

### Error Pattern

```
PoTool.Core.Exceptions.TfsException: TFS request failed: BadRequest
   at PoTool.Api.Services.RealTfsClient.HandleHttpErrorsAsync(...)
   at PoTool.Api.Services.RealTfsClient.GetWorkItemsAsync(...)
```

## Root Cause Analysis

### The Core Issue

The WIQL (Work Item Query Language) endpoint was being called **without** the project name in the URL path. For on-prem TFS with NTLM authentication, when querying work items by area path (which is project-specific), the WIQL endpoint needs the project context to properly resolve the query.

**Incorrect URL** (causing 400 BadRequest):
```
{CollectionUrl}/_apis/wit/wiql?api-version={version}
Example: https://tfs.rhmarine.com/tfs/DefaultCollection/_apis/wit/wiql?api-version=7.0
```

**Correct URL** (with project for proper context):
```
{CollectionUrl}/{Project}/_apis/wit/wiql?api-version={version}
Example: https://tfs.rhmarine.com/tfs/DefaultCollection/MyProject/_apis/wit/wiql?api-version=7.0
```

### Why This Matters

The WIQL endpoint can operate at both collection and project levels according to the Azure DevOps REST API documentation. However:

1. **When querying with area paths from a specific project**, the project-scoped endpoint (`/{project}/_apis/wit/wiql`) provides the necessary context
2. **On-prem TFS with NTLM authentication** may require the project context to properly handle authentication and authorization
3. **The query in this application uses area paths** (e.g., `[System.AreaPath] = 'ProjectName\Area'`), which are project-specific

## The Solution

### Changes Made

**File**: `PoTool.Api/Services/RealTfsClient.cs`

**Location 1**: `GetWorkItemsAsync` method (line 193)
```csharp
// BEFORE (missing project context)
var wiqlUrl = $"{config.Url.TrimEnd('/')}/_apis/wit/wiql?api-version={config.ApiVersion}";

// AFTER (correct - includes project)
var wiqlUrl = $"{config.Url.TrimEnd('/')}/{config.Project}/_apis/wit/wiql?api-version={config.ApiVersion}";
```

**Location 2**: `VerifyWorkItemQueryAsync` method (line 1167)
```csharp
// BEFORE (missing project context)
var url = $"{entity.Url.TrimEnd('/')}/_apis/wit/wiql?api-version={entity.ApiVersion}";

// AFTER (correct - includes project)
var url = $"{entity.Url.TrimEnd('/')}/{entity.Project}/_apis/wit/wiql?api-version={entity.ApiVersion}";
```

### Why This Fix Is Correct

1. **Aligns with how this application uses WIQL**: Queries use project-specific area paths
2. **Works better with on-prem TFS and NTLM**: Project context helps with authentication/authorization
3. **Matches other project-scoped work item operations**: Creating work items, getting revisions, etc. all use project-scoped URLs

## Verification

### Testing Steps

1. Configure TFS with NTLM authentication and "Use Default Credentials" enabled
2. Navigate to Work Item Explorer in the application
3. Click "Full Sync" button
4. Verify that work items are retrieved successfully
5. Check logs for successful WIQL query execution

### Expected Behavior

**Before Fix**:
```
System.Net.Http.HttpClient.TfsClient.NTLM.ClientHandler: Information: 
  POST https://tfs.rhmarine.com/tfs/DefaultCollection/_apis/wit/wiql?*
System.Net.Http.HttpClient.TfsClient.NTLM.ClientHandler: Information: 
  Received HTTP response headers after 412ms - 400
PoTool.Api.Services.RealTfsClient: Error: TFS HTTP error: BadRequest
```

**After Fix**:
```
System.Net.Http.HttpClient.TfsClient.NTLM.ClientHandler: Information: 
  POST https://tfs.rhmarine.com/tfs/DefaultCollection/MyProject/_apis/wit/wiql?*
System.Net.Http.HttpClient.TfsClient.NTLM.ClientHandler: Information: 
  Received HTTP response headers after 150ms - 200
PoTool.Api.Services.RealTfsClient: Information: Retrieved 42 work items for areaPath=...
```

## TFS API Endpoint Guidelines

### Project-Scoped Endpoints (use `/{project}/_apis/...`)

- `/{project}/_apis/wit/wiql` - WIQL queries (recommended for project-specific queries)
- `/{project}/_apis/wit/workitems/{id}` - Work item operations
- `/{project}/_apis/wit/workitems/${type}` - Create work item
- `/{project}/_apis/git/*` - All Git operations
- `/{project}/_apis/build/*` - Build definitions and runs
- `/{project}/_apis/release/*` - Release definitions and deployments

### Collection-Scoped Endpoints (use `/_apis/...`)

- `/_apis/projects` - List projects
- `/_apis/wit/fields` - Work item field definitions
- `/_apis/wit/workitems?ids=...` - Batch get work items by ID

## References

- [Azure DevOps REST API Reference](https://docs.microsoft.com/en-us/rest/api/azure/devops/)
- [Work Item Tracking REST API](https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/)
- [WIQL Syntax Reference](https://docs.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax)

## Conclusion

The recurring NTLM authentication failure was caused by missing project context in the WIQL endpoint URL. By adding the project name to the WIQL URL path, the endpoint now has proper project context for on-prem TFS with NTLM authentication, resolving the BadRequest error.

This fix is minimal, surgical, and addresses the root cause.
