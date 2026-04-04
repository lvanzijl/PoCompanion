# Bucket A generated-client migration

## 1. Scope confirmation

Migrated services:

- `PoTool.Client/Services/ProjectService.cs` (live-route methods only)
- `PoTool.Client/Services/TriageTagService.cs`
- `PoTool.Client/Services/ReleaseNotesService.cs`
- `PoTool.Client/Services/ConfigurationTransferService.cs`
- `PoTool.Client/Services/TeamService.cs` (`CreateTeamAsync` only)
- `PoTool.Client/Services/StartupOrchestratorService.cs`
- `PoTool.Client/Services/CacheSyncService.cs` (read-only methods only)

Confirmation:

- Only the client service layer and directly related client registration/test files were touched.
- No cache-backed services were modified.
- No pages, components, or UI bindings were refactored.

## 2. ProjectService exception (mandatory)

Migrated live-route methods:

- `GetAllProjectsAsync`
- `GetProjectAsync`
- `GetProjectProductsAsync`

Untouched methods:

- `GetPlanningSummaryAsync`

Explicit statement:

> ProjectService is intentionally partially migrated to avoid mixing cache-envelope logic into this slice.

## 3. Migration summary

Before:

- scoped services used raw `HttpClient`
- manual `/api/...` route strings
- `GetFromJsonAsync`, `PostAsJsonAsync`, `PutAsJsonAsync`
- manual response handling for live Bucket A calls

After:

- scoped methods call generated clients directly
- no manual route strings remain in migrated methods
- no manual serialization/deserialization remains in migrated methods
- `TriageTagService` uses a thin `ApiClient` wrapper only for generated-client status/body recovery on non-2xx responses

Generated clients used:

- `IProjectsClient`
- `ITriageTagsClient`
- `ISettingsClient`
- `ITeamsClient`
- `IStartupClient`
- `ICacheSyncClient`

Guardrails added:

- `PoTool.Tests.Unit/Audits/BucketAGeneratedClientMigrationAuditTests.cs`
- scoped audit coverage for raw `HttpClient`, `/api/` strings, and JSON helper usage in migrated methods

## 4. Validation results

### Startup

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release` ✅
- client root `http://localhost:5292/` returned `200 OK` ✅
- API startup readiness `http://localhost:5291/api/startup/readiness` returned `200 OK` ✅

### Project loading

- manual service validation against local API returned `projects-count=1` ✅

### Team creation

- not executed
- validation stopped after the mandatory release/config flow failure below

### Release/config flows

- `ReleaseNotesService.GetReleaseNotesAsync()` returned `release-notes-count=133` ✅
- `ConfigurationTransferService.ExportAsync()` failed with HTTP 500 ❌
- root cause reported by API:
  - `System.NotSupportedException: SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses`
  - stack trace pointed to `PoTool.Api/Services/Configuration/ExportConfigurationService.cs:53`

### CacheSync reads

- not executed
- validation stopped after the mandatory release/config flow failure above

## 5. Remaining HttpClient usage

Inside scoped migrated methods:

- none

Intentional remaining usage in scoped service files, outside this slice:

- `ProjectService.GetPlanningSummaryAsync` (cache-backed, out of scope)
- `CacheSyncService` write methods:
  - `TriggerSyncAsync`
  - `CancelSyncAsync`
  - `DeleteCacheAsync`
  - `ResetCacheSelectiveAsync`

Remaining raw `HttpClient` usage outside scope is still present in other service files, including:

- `BuildQualityService`
- `MetricsStateService`
- `PipelineStateService`
- `PullRequestStateService`
- `ReleasePlanningService`
- `TfsConfigService`
- `WorkItemService`

## 6. Issues encountered

- No DI registration failures were found during build/startup validation.
- `TriageTagService` needed a thin generated-client wrapper because the generated non-2xx contract expects `ProblemDetails`, while the API returns `TriageTagOperationResponse`.
- Manual validation exposed an existing server-side configuration export failure:
  - SQLite `DateTimeOffset` `ORDER BY` translation failure in `ExportConfigurationService`
- No missing endpoints were encountered in this slice.

## 7. Readiness for next slice

Status: **not ready to proceed yet** to cache-backed services migration.

Reason:

- The controlled Bucket A client-service migration itself builds and targeted tests pass.
- However, mandatory manual validation did not complete because configuration export failed with a server-side SQLite/`DateTimeOffset` query error.
- That failure should be investigated and cleared before proceeding to the cache-backed migration slice.
