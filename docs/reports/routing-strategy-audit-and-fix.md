# Routing strategy audit and fix

## Summary

This change fixes the startup failure on `/` by making `DataSourceModeMiddleware` bypass non-managed client and framework routes, while hardening explicit classification for managed API, health, and SignalR paths. The audit also closed other predictable failures in the same category by classifying missing minimal API, hub, and controller routes and by normalizing route matching for case and trailing slashes.

## Reported bug

- Application startup succeeded, but requesting `/` threw `PoTool.Api.Exceptions.RouteNotClassifiedException`.
- The exception originated from `PoTool.Api.Configuration.DataSourceModeConfiguration.ResolveRouteIntentOrThrow(...)`, called by `PoTool.Api.Middleware.DataSourceModeMiddleware.InvokeAsync(...)`.
- The failure happened in the same host that serves the Blazor SPA shell and Swagger/OpenAPI middleware.

## Root cause

`DataSourceModeMiddleware` ran after `UseRouting()` and before endpoint execution, so fallback-served SPA requests such as `/` and `/home/...` still passed through route classification. `DataSourceModeConfiguration` only classified selected API and health paths, so fallback-served client routes were treated as `Unknown` and immediately threw.

The audit also found additional managed routes that would predictably fail for the same reason:

- SignalR hub paths under `/hubs`
- minimal API verification paths `/api/tfsvalidate` and `/api/tfsverify`
- controller prefixes that were real application routes but missing from classification, especially `/api/buildquality`, `/api/bugtriage`, `/api/healthcalculation`, `/api/roadmapsnapshots`, `/api/sprints`, `/api/triagetags`, and `/api/portfolio/snapshots`

Matching also depended on raw string prefix checks, so trailing slashes and segment-boundary mismatches were brittle.

## Routing strategy findings

### Middleware and endpoint order

`ApiApplicationBuilderExtensions.ConfigurePoToolApi(...)` currently orders the relevant pipeline as follows:

1. `MapOpenApi()` / `UseOpenApi()` / `UseSwaggerUi()` in development
2. `UseBlazorFrameworkFiles()`
3. `UseStaticFiles()`
4. `UseRouting()`
5. `UseCors(...)`
6. `UseMiddleware<DataSourceModeMiddleware>()`
7. `UseMiddleware<WorkspaceGuardMiddleware>()`
8. `MapControllers()`
9. `MapHub<CacheSyncHub>("/hubs/cachesync")`
10. `MapHub<TfsConfigHub>("/hubs/tfsconfig")`
11. minimal API mappings such as `/health`, `/api/tfsconfig`, `/api/tfsvalidate`, `/api/tfsverify`
12. `MapFallbackToFile("index.html")`

This means fallback-served SPA routes are selected by routing but still pass through `DataSourceModeMiddleware` before the fallback endpoint executes.

### Route-category audit results

- **SPA entry and client routes**: `/`, `/home/...`, `/settings/...`, `/planning/...`, `/profiles`, `/onboarding`, and other client `@page` routes are served by fallback and should bypass data-source classification.
- **Static files and Blazor framework assets**: served before `DataSourceModeMiddleware`; no change required.
- **Swagger/OpenAPI**: development-only middleware runs before the guarded routing section and already bypasses classification.
- **SignalR hubs**: `/hubs/cachesync` and `/hubs/tfsconfig` do flow through the middleware during negotiate/connect requests and therefore require explicit handling.
- **Minimal API endpoints**: `/health` and `/api/tfsconfig/...` were already covered; `/api/tfsvalidate` and `/api/tfsverify` were not.
- **Controller routes**:
  - cache-only analytical: `/api/workitems`, `/api/pullrequests`, `/api/pipelines`, `/api/releaseplanning`, `/api/filtering`, `/api/metrics`, `/api/buildquality`
  - live-allowed operational/configuration/supporting routes: `/api/settings`, `/api/tfsconfig`, `/api/startup`, `/api/profiles`, `/api/teams`, `/api/products`, `/api/datasource`, `/api/cachesync`, `/api/bugtriage`, `/api/healthcalculation`, `/api/roadmapsnapshots`, `/api/sprints`, `/api/triagetags`, `/api/portfolio/snapshots`
- **Unknown managed API routes**: should remain strict and still throw until intentionally classified.

## Classification strategy findings

- The strict classification model is still valuable for managed server routes that can affect data-source selection.
- The original failure came from applying that strictness to the SPA shell and fallback routes, which do not need cache/live selection.
- A coherent boundary is:
  - **managed paths**: `/api`, `/health`, `/hubs`
  - **bypass paths**: everything else, including SPA fallback routes and docs paths
- Route matching needed normalization:
  - case-insensitive handling
  - trailing-slash normalization
  - segment-aware prefix checks so `/api/tfsconfig/...` matches but unrelated strings do not accidentally qualify

## Files changed

- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- `PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
- `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
- `docs/release-notes.json`

## Fixes applied

1. Added `ShouldBypassMiddleware(...)` so non-managed routes such as `/` and SPA fallback paths bypass `DataSourceModeMiddleware`.
2. Normalized route handling inside `DataSourceModeConfiguration` to support trailing slashes and case-insensitive matching consistently.
3. Replaced brittle raw `StartsWith(...)` checks with segment-aware prefix matching.
4. Explicitly classified missing legitimate managed routes:
   - live-allowed: `/hubs`, `/api/tfsvalidate`, `/api/tfsverify`, `/api/bugtriage`, `/api/healthcalculation`, `/api/roadmapsnapshots`, `/api/sprints`, `/api/triagetags`, `/api/portfolio/snapshots`
   - cache-only analytical: `/api/buildquality`
5. Added focused regression coverage for:
   - `/` bypass behavior
   - hub route classification
   - TFS validation route classification
   - build-quality cache classification
   - case and trailing-slash normalization
   - preservation of strict failure for unknown managed API routes

## Additional predicted routing/classification risks found

- New managed endpoints added under `/api`, `/health`, or `/hubs` will still fail closed until explicitly categorized. That is intentional and should remain part of the protection model.
- `MapFallbackToFile("index.html")` will continue serving unmatched non-file client routes, so accidental non-managed typos outside the guarded prefixes can still land in the SPA shell. That behavior matches the current hosted Blazor architecture.
- Swagger/OpenAPI currently bypasses classification because it runs before the guarded routing section. That is acceptable for development tooling, but future production docs exposure should be reviewed explicitly.

## Validation performed

- `dotnet restore PoTool.sln --nologo`
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo -v minimal --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests"`

### Verified by tests

- `/` no longer throws inside `DataSourceModeMiddleware`; it bypasses classification and continues to the next middleware/endpoint.
- hub and TFS verification routes resolve as live-allowed.
- build-quality routes resolve as cache-only analytical reads.
- unknown managed API routes still throw `RouteNotClassifiedException`.
- normalized casing and trailing slashes resolve correctly.

### Not fully runtime-verified

- I did not perform a full browser/runtime host verification against a running server in this environment.
- Swagger UI, SignalR negotiate traffic, and the SPA fallback were verified by code audit and unit coverage rather than end-to-end HTTP execution.

## Remaining risks / follow-up

- If future work introduces additional managed endpoints, classification tests should be updated in the same change.
- If the application later adds production docs endpoints or new non-API server endpoints that do require data-source selection, the middleware-managed prefix list will need to be revisited.
- The intentionally blocked ambiguous work-item state-timeline route remains unchanged.

## Final outcome

The `/` startup crash is fixed without weakening strict protection for managed server routes. SPA fallback paths now bypass data-source classification, legitimate managed routes are explicitly categorized, normalization is more robust, and focused tests now guard against regression across root, hubs, docs-adjacent bypass behavior, representative API routes, and unknown managed routes.
