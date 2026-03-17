# Runtime Integrity Fix (Dev Startup + TFS Access Boundary)

## Root cause

- `PoTool.Api/appsettings.Development.json` overrode the base mock-backed default and forced `TfsIntegration:UseMockClient` to `false`.
- `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs` bypassed `ITfsClient` and made direct HTTP calls for the profile bootstrap goal picker.
- That combination meant a fresh development run could hit live TFS behavior before any cache-backed state existed.

## Changes made

- Updated `PoTool.Api/appsettings.Development.json` so development keeps `TfsIntegration:UseMockClient=true`.
- Routed goal bootstrap access through `ITfsClient.GetWorkItemsByTypeAsync(...)` instead of handler-level HTTP calls.
- Implemented `GetWorkItemsByTypeAsync(...)` in both `RealTfsClient` and `MockTfsClient` so the existing DI selection decides live vs mock behavior.
- Normalized mock area-path matching so the development configuration’s project-root area path resolves against the Battleship mock hierarchy.
- Added a runtime guard in `GetGoalsFromTfsQueryHandler` that refuses to continue when mock mode is enabled but the resolved TFS client is not the mock client.
- Reworked focused unit tests so the handler verifies the abstraction boundary instead of raw `HttpClient` behavior.

## Validation results

- `dotnet build PoTool.sln --no-restore -m:1`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~GetGoalsFromTfsQueryHandlerTests|FullyQualifiedName~RealTfsClientRequestTests|FullyQualifiedName~MockTfsClientTests" -v minimal`
- Manual development startup validation succeeded without environment overrides:
  - `/api/workitems/area-paths/from-tfs` returned 12 mock area paths.
  - `/api/workitems/goals/from-tfs` returned 10 mock goals after saving mock-aligned config.
  - Profile creation succeeded against mock goal IDs.
  - After adding a product for the new profile, `POST /api/CacheSync/{productOwnerId}/sync` completed successfully in mock mode (`SyncStatus=Success`, `WorkItemCount=5429`).

## Notes

- No CDC contracts or DTOs were changed.
- The handler keeps the same outward behavior: on configuration or TFS errors it logs and returns an empty result.
