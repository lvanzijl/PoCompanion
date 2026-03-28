# Sprint Slice Canonical Filter Migration

## Summary

The Sprint slice now resolves shared sprint scope once at the API boundary and passes deterministic effective scope into migrated handlers and services.

Implemented changes:

- added `SprintFilterResolutionService` to normalize requested Sprint scope into a canonical `SprintEffectiveFilter`
- updated migrated Sprint endpoints to resolve requested versus effective scope before dispatching mediator queries
- wrapped migrated Sprint responses in envelopes containing:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
- removed handler-local product, owner-derived product selection, iteration-path interpretation, and sprint-window fallback logic from the migrated Sprint handlers
- normalized legacy `iterationPath` inputs once at the boundary and propagated the resolved iteration/time semantics downstream
- updated Sprint-related client calls to read the new response envelopes without changing existing page behavior

Why:

- Sprint filtering semantics were previously split across controller parsing, handler-local owner/product loading, and local sprint/iteration interpretation
- legacy Sprint endpoints mixed raw `iterationPath`, `productOwnerId`, `productId`, `productIds`, and `areaPath` semantics differently per endpoint
- Sprint responses did not explain requested versus effective scope, which made invalid or out-of-scope selections hard to reason about

## Affected Files

### Controllers

- `PoTool.Api/Controllers/MetricsController.cs`

### Filter resolution / shared Sprint filtering

- `PoTool.Api/Services/SprintFilterResolutionService.cs`
- `PoTool.Api/Services/SprintScopedWorkItemLoader.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### Sprint handlers / services

- `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintCapacityPlanQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs`

### Sprint query contracts / filter models

- `PoTool.Core/Metrics/Filters/SprintFilterModels.cs`
- `PoTool.Core/Metrics/Filters/SprintFilterFactory.cs`
- `PoTool.Core/Metrics/Queries/GetBacklogHealthQuery.cs`
- `PoTool.Core/Metrics/Queries/GetMultiIterationBacklogHealthQuery.cs`
- `PoTool.Core/Metrics/Queries/GetSprintCapacityPlanQuery.cs`
- `PoTool.Core/Metrics/Queries/GetSprintExecutionQuery.cs`
- `PoTool.Core/Metrics/Queries/GetSprintMetricsQuery.cs`
- `PoTool.Core/Metrics/Queries/GetSprintTrendMetricsQuery.cs`
- `PoTool.Core/Metrics/Queries/GetWorkItemActivityDetailsQuery.cs`

### Shared/client DTO and API client updates

- `PoTool.Shared/Metrics/SprintFilterDtos.cs`
- `PoTool.Client/ApiClient/ApiClient.SprintFilters.cs`
- `PoTool.Client/Services/SprintDeliveryMetricsService.cs`
- `PoTool.Client/Services/WorkspaceSignalService.cs`
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`

### Tests

- `PoTool.Tests.Unit/Services/SprintFilterResolutionServiceTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerSprintCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerDeliveryCanonicalFilterTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`

## Before vs After

| Concern | Before | After |
| --- | --- | --- |
| Product scope | Sprint handlers reloaded owner products or interpreted product narrowing locally. | Product scope is resolved once at the controller boundary and propagated as `SprintEffectiveFilter`. |
| Sprint / iteration scope | Legacy endpoints used raw `iterationPath` and handlers re-interpreted sprint semantics locally. | Legacy iteration input is normalized once; handlers consume resolved iteration and time semantics only. |
| Multi-sprint comparison | Sprint trend handler inferred current/previous sprint comparison locally from raw IDs. | The effective Sprint filter now carries ordered sprint comparison semantics resolved at the boundary. |
| Area path scope | Multi-iteration backlog health treated `areaPath` as a special local fallback. | Shared area-path scope is normalized into the effective Sprint filter and applied consistently. |
| Response metadata | In-scope Sprint endpoints returned raw payloads only. | Migrated Sprint endpoints now return canonical filter metadata envelopes plus the payload. |
| Client consumption | Sprint client paths assumed raw endpoint payloads. | Sprint client/service paths now read envelope responses while preserving existing UI behavior. |

## Validation

Correctness was ensured by:

- compiling the full solution in Release mode
- running focused Sprint tests covering:
  - new Sprint filter resolution behavior
  - new Sprint controller envelope behavior
  - Sprint trend, Sprint execution, Sprint metrics, backlog health, multi-iteration backlog health, and work-item activity behavior
  - release note validation

Validation commands:

```bash
dotnet build PoTool.sln --configuration Release
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~SprintFilterResolutionServiceTests|FullyQualifiedName~MetricsControllerSprintCanonicalFilterTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerSqliteTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~GetBacklogHealthQueryHandlerTests|FullyQualifiedName~GetMultiIterationBacklogHealthQueryHandlerMultiProductTests|FullyQualifiedName~GetWorkItemActivityDetailsQueryHandlerTests|FullyQualifiedName~MetricsControllerDeliveryCanonicalFilterTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal
```

## Known Limitations

- `SprintsController` read endpoints were intentionally left unchanged because they are settings/reference endpoints rather than migrated Sprint analytics endpoints
- `work-item-activity` now consumes canonical product/time scope, but its UI still provides explicit period start/end values rather than a dedicated shared Sprint selector
- non-Sprint metrics endpoints such as effort-distribution, effort-estimation, and health pages outside the defined Sprint slice were intentionally left unchanged
- `areaPath` remains a true slice-local option outside the migrated Sprint endpoints that already used it as shared analytical scope

## Correctness Fixes

- invalid or out-of-scope product selections for migrated Sprint endpoints are now normalized deterministically at the boundary instead of silently producing endpoint-specific differences
- backlog-health, Sprint metrics, and capacity-plan now use the same resolved Sprint/iteration semantics instead of each endpoint independently reinterpreting raw iteration input
- Sprint trend product narrowing now applies consistently to the trend projections and the derived feature/epic analytics that ride on top of the same effective Sprint scope
