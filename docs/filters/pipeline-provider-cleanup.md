# Pipeline Provider Cleanup

## Summary

Pipeline analytical reads no longer depend on runtime provider switching.

- the default `IPipelineReadProvider` registration is now `CachedPipelineReadProvider`
- `LazyPipelineReadProvider` was removed
- `DataSourceAwareReadProviderFactory` no longer contains a pipeline branch
- `GET /api/pipelines/definitions` remains a live-allowed discovery/configuration route and now resolves the keyed live provider explicitly

This keeps analytical pipeline reads deterministic and cache-backed while preserving the configuration/discovery behavior of pipeline definitions.  
Sources: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-224`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:23-70`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:9-64`

## Analytical Cleanup

The following analytical pipeline paths now resolve through the default cached provider:

- `GET /api/pipelines`
- `GET /api/pipelines/{id}/runs`
- `GET /api/pipelines/metrics`
- `GET /api/pipelines/runs`

How this is enforced:

1. `/api/pipelines` remains part of `CacheModeRequiredRoutes`, so analytical routes still require cache at middleware level.  
   Source: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:59-70`

2. The default `IPipelineReadProvider` DI registration is now `CachedPipelineReadProvider`.  
   Source: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:219-224`

3. Analytical handlers still depend on `IPipelineReadProvider`, but that dependency is now deterministic for the analytical slice.
   - `GetAllPipelinesQueryHandler`  
     Source: `PoTool.Api/Handlers/Pipelines/GetAllPipelinesQueryHandler.cs:8-26`
   - `GetPipelineRunsQueryHandler`  
     Source: `PoTool.Api/Handlers/Pipelines/GetPipelineRunsQueryHandler.cs:8-26`
   - `GetPipelineMetricsQueryHandler`  
     Source: `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:10-126`
   - `GetPipelineRunsForProductsQueryHandler`  
     Source: `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:10-43`

Result:

- no analytical pipeline endpoint depends on lazy provider indirection
- no analytical pipeline handler can silently switch to the live provider through the default DI path
- middleware still blocks analytical routes when cache is unavailable

## Definitions Discovery Handling

`GET /api/pipelines/definitions` is intentionally handled differently from analytical pipeline reads.

### Route intent

`/api/pipelines/definitions` was added to `LiveModeAllowedRoutes`, which are matched before the broader `/api/pipelines` cache-only prefix.  
Source: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:23-38`, `73-92`

This means:

- `GET /api/pipelines/definitions` remains live-allowed
- `GET /api/pipelines/*` analytical endpoints stay cache-only

### Handler resolution

`GetPipelineDefinitionsQueryHandler` no longer uses the default `IPipelineReadProvider`.

Instead, it resolves the keyed live provider directly:

- constructor receives `IServiceProvider`
- handler resolves `GetRequiredKeyedService<IPipelineReadProvider>("Live")`
- product/repository definition lookups always use that explicit live provider  
  Source: `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:15-64`

### Controller intent

`PipelinesController.GetDefinitions` now documents the route as configuration/discovery and explicitly separates it from cache-only analytical reads.  
Source: `PoTool.Api/Controllers/PipelinesController.cs:151-171`

## Removed / Simplified Components

### Removed

- `PoTool.Api/Services/LazyPipelineReadProvider.cs`

This wrapper existed only to defer pipeline provider selection until request mode was known. Once analytical pipeline DI became deterministic and definitions was handled explicitly, the wrapper became redundant.

### Simplified

- `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
  - removed `GetPipelineReadProvider()`
  - work item runtime switching remains in place  
  Source: `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:8-41`

- analytical pipeline handler comments
  - updated to describe cached analytical reads instead of “live and cached modes”  
  Sources:
  - `PoTool.Api/Handlers/Pipelines/GetAllPipelinesQueryHandler.cs:8-26`
  - `PoTool.Api/Handlers/Pipelines/GetPipelineRunsQueryHandler.cs:8-26`
  - `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:10-16`
  - `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:10-14`

## DI Changes

### Before

- keyed registrations:
  - `IPipelineReadProvider[Live] -> LivePipelineReadProvider`
  - `IPipelineReadProvider[Cached] -> CachedPipelineReadProvider`
- default registration:
  - `IPipelineReadProvider -> LazyPipelineReadProvider`
- definitions handler:
  - inherited the same default provider path as analytical reads

### After

- keyed registrations unchanged:
  - `IPipelineReadProvider[Live] -> LivePipelineReadProvider`
  - `IPipelineReadProvider[Cached] -> CachedPipelineReadProvider`
- default registration:
  - `IPipelineReadProvider -> CachedPipelineReadProvider`
- definitions handler:
  - explicitly resolves `IPipelineReadProvider[Live]`

Source: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-224`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:20-25`

## Validation

### Automated validation

Executed successfully:

- `dotnet build PoTool.sln --configuration Release --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests|FullyQualifiedName~GetPipelineDefinitionsQueryHandlerTests|FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~GetPipelineRunsForProductsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Passing targeted checks now cover:

- route intent for `/api/pipelines/definitions` being live-allowed  
  Source: `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs:39-47`
- middleware setting `Live` mode for `/api/pipelines/definitions`  
  Source: `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs:163-179`
- service collection resolving default `IPipelineReadProvider` to cached, while preserving keyed live resolution  
  Source: `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs:284-313`
- definitions handler explicitly using the live provider  
  Source: `PoTool.Tests.Unit/Handlers/GetPipelineDefinitionsQueryHandlerTests.cs:20-161`

### Confirmed behavior

- analytical pipeline handlers resolve deterministically to cached reads
- no analytical pipeline endpoint can silently hit the live provider through default DI
- `GET /api/pipelines/definitions` remains live-capable and explicitly separated
- live keyed registration still exists for explicit discovery flows

## Known Limitations

- this cleanup does **not** redesign the remaining work item runtime-switching architecture
- `DataSourceAwareReadProviderFactory` still exists because work items still use it
- pipeline definitions discovery still shares the `/api/pipelines` controller, but it is now intentionally exempted by explicit route intent and explicit keyed live resolution instead of implicit fallback
- no new pipeline-specific live controller or abstraction was introduced; the change stays minimal and localized
