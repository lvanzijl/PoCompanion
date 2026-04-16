# Endpoint classification enforcement

## Metadata design (attribute + minimal API equivalent)

- Controller endpoints now declare classification with `[DataSourceMode(RouteIntent....)]`.
- `DataSourceModeAttribute` is applied at controller level for defaults and at action level for overrides.
- MVC adds one authoritative `DataSourceModeMetadata` entry per action endpoint through `DataSourceModeEndpointMetadataConvention`.
- Minimal API, health, and SignalR endpoints now declare classification with `.WithDataSourceMode(...)`, which adds the same `DataSourceModeMetadata`.
- Both controller and minimal endpoint paths therefore converge on one shared metadata contract: `IDataSourceModeMetadata`.

## Middleware changes (before vs after)

### Before

- `DataSourceModeMiddleware` classified requests by string path through `DataSourceModeConfiguration.ResolveRouteIntentOrThrow(...)`.
- Missing classification failed only when a request hit the route.

### After

- `DataSourceModeMiddleware` first reads endpoint metadata through `DataSourceModeEndpointMetadataResolver`.
- Endpoint metadata is authoritative when present.
- `DataSourceModeConfiguration` remains only as temporary fallback and validation support.
- Missing classification still throws `RouteNotClassifiedException`, but startup validation now prevents managed endpoints from reaching runtime without metadata.

## Migration coverage summary (% endpoints classified via metadata)

- Managed endpoints discovered through startup mapping: **221**
- Managed endpoints with exactly one metadata classification entry: **221 / 221 (100%)**
- Metadata intent totals from the startup validation host:
  - `LiveAllowed`: **134**
  - `CacheOnlyAnalyticalRead`: **86**
  - `BlockedAmbiguous`: **1**

## List of endpoints updated

### Controller defaults

- `BugTriageController` → `LiveAllowed`
- `BuildQualityController` → `CacheOnlyAnalyticalRead`
- `CacheSyncController` → `LiveAllowed`
- `DataSourceModeController` → `LiveAllowed`
- `FilteringController` → `CacheOnlyAnalyticalRead`
- `HealthCalculationController` → `LiveAllowed`
- `MetricsController` → `CacheOnlyAnalyticalRead`
- `OnboardingCrudController` → `LiveAllowed`
- `OnboardingLookupController` → `LiveAllowed`
- `OnboardingStatusController` → `LiveAllowed`
- `PipelinesController` → `CacheOnlyAnalyticalRead` default
- `PortfolioSnapshotsController` → `LiveAllowed`
- `ProductsController` → `LiveAllowed`
- `ProfilesController` → `LiveAllowed`
- `ProjectsController` → `LiveAllowed` default
- `PullRequestsController` → `CacheOnlyAnalyticalRead`
- `ReleasePlanningController` → `CacheOnlyAnalyticalRead`
- `RoadmapSnapshotsController` → `LiveAllowed`
- `SettingsController` → `LiveAllowed`
- `SprintsController` → `LiveAllowed`
- `StartupController` → `LiveAllowed`
- `StartupStateController` → `LiveAllowed`
- `TeamsController` → `LiveAllowed`
- `TriageTagsController` → `LiveAllowed`
- `WorkItemsController` → `CacheOnlyAnalyticalRead` default

### Controller action overrides

- `ProjectsController.GetPlanningSummary` → `CacheOnlyAnalyticalRead`
- `PipelinesController.GetDefinitions` → `LiveAllowed`
- `WorkItemsController.RefreshFromTfs` → `LiveAllowed`
- `WorkItemsController.RefreshByRootIdsFromTfs` → `LiveAllowed`
- `WorkItemsController.UpdateTags` → `LiveAllowed`
- `WorkItemsController.UpdateTitleDescription` → `LiveAllowed`
- `WorkItemsController.UpdateBacklogPriority` → `LiveAllowed`
- `WorkItemsController.UpdateIterationPath` → `LiveAllowed`
- `WorkItemsController.ValidateWorkItem` → `LiveAllowed`
- `WorkItemsController.GetAreaPathsFromTfs` → `LiveAllowed`
- `WorkItemsController.GetGoalsFromTfs` → `LiveAllowed`
- `WorkItemsController.GetStateTimeline` → `BlockedAmbiguous`
- `WorkItemsController.GetWorkItemRevisions` → `LiveAllowed`
- `WorkItemsController.FixValidationViolations` → `LiveAllowed`
- `WorkItemsController.BulkAssignEffort` → `LiveAllowed`
- `WorkItemsController.GetBugSeverityOptions` → `LiveAllowed`

### Minimal / hub / health endpoints

- `/hubs/cachesync` → `LiveAllowed`
- `/hubs/tfsconfig` → `LiveAllowed`
- `/health` → `LiveAllowed`
- `GET /api/tfsconfig` → `LiveAllowed`
- `POST /api/tfsconfig` → `LiveAllowed`
- `GET /api/tfsvalidate` → `LiveAllowed`
- `POST /api/tfsverify` → `LiveAllowed`
- `POST /api/tfsconfig/save-and-verify` → `LiveAllowed`

## Proof that unclassified endpoints fail at startup

- `MapPoToolEndpoints()` now calls `DataSourceModeEndpointValidation.ValidateManagedEndpoints(app)`.
- Validation scans all mapped managed route endpoints from the endpoint route builder, not from request-time paths.
- Validation fails startup when a managed endpoint:
  - has no `IDataSourceModeMetadata`
  - has multiple classification metadata entries
  - disagrees with an existing fallback classification that still exists for migration validation
- `DataSourceModeEndpointValidationTests` adds an explicit regression for a synthetic `/api/unclassified` endpoint and asserts startup validation throws immediately.

## Deprecation plan for `DataSourceModeConfiguration`

- Current role:
  - middleware fallback when endpoint metadata is absent
  - startup validation cross-check against migrated route semantics
  - temporary support for remaining path-based consumers during migration
- Next removal steps:
  1. remove the middleware fallback once metadata coverage is fully trusted
  2. delete string-based route matching helpers after any remaining path-based consumers are switched to metadata-aware resolution
  3. retain only metadata-based classification and endpoint validation

## Remaining risks or edge cases

- `PoTool.Tests.Unit` currently has unrelated pre-existing compile failures outside this work, so full unit-test compilation could not be completed in this task.
- OpenAPI and contract audits now infer cache-vs-live intent from endpoint declarations first, but full removal of every path-based helper still depends on follow-up cleanup of remaining fallback consumers.
- Startup validation covers managed route endpoints only; non-managed client fallback routes remain intentionally outside this classification system.
