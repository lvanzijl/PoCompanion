# Cache-Only Guardrail Analysis — Pipeline and Work Item Read Paths

## Summary

- **Pipeline:** **apply now** for PR-style cleanup/simplification, because user-facing `/api/pipelines` analytical routes are already cache-only at middleware level and the remaining ambiguity is mostly DI/provider indirection rather than legitimate live analytical behavior. The main caveat is clarifying whether `/api/pipelines/definitions` is analytical-only or should be split into a separate explicit live discovery path.  
  Evidence: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:60-68`, `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:35-88`, `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`, `PoTool.Api/Controllers/PipelinesController.cs:35-209`.

- **Work items:** **apply partially** after a route-intent cleanup, not as a straight PR-style copy. Broad `/api/workitems` cache-only routing already exists, but the slice still contains several legitimate live flows and at least one analytical route that still reaches TFS directly. Work item cleanup should start by separating analytical reads from explicit live discovery / revision / refresh / write-support routes before simplifying provider wiring.  
  Evidence: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:23-68`, `PoTool.Api/Controllers/WorkItemsController.cs:406-585`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:12-37`, `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`.

## Runtime Selection Map

### Shared runtime selection mechanism

For both pipeline and work item provider-backed reads, the current request path still flows through the same runtime mode-selection chain:

1. `DataSourceModeMiddleware` resolves route intent with `DataSourceModeConfiguration.GetRouteIntent(path)`.  
   - `CacheOnlyAnalyticalRead` routes require cache and return `409 Cache not ready` if no successful sync exists.  
   - `LiveAllowed` and `Unknown` routes set request mode to `Live`.  
   Source: `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:32-88`

2. `DataSourceModeProvider.GetModeAsync(productOwnerId)` returns `Cache` only when `ProductOwnerCacheStates.LastSuccessfulSync` exists; otherwise it returns `Live`.  
   Source: `PoTool.Api/Services/DataSourceModeProvider.cs:41-73`

3. Default DI for work items and pipelines still resolves to lazy wrappers:
   - `IWorkItemReadProvider -> LazyWorkItemReadProvider`
   - `IPipelineReadProvider -> LazyPipelineReadProvider`
   Source: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`

4. The lazy wrappers defer method calls to `DataSourceAwareReadProviderFactory`.
   - `LazyWorkItemReadProvider`: `PoTool.Api/Services/LazyWorkItemReadProvider.cs:10-43`
   - `LazyPipelineReadProvider`: `PoTool.Api/Services/LazyPipelineReadProvider.cs:11-67`

5. `DataSourceAwareReadProviderFactory` still branches by request mode:
   - `Cache -> keyed "Cached"`
   - `Live -> keyed "Live"`
   Source: `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:28-56`

6. `WorkspaceGuardMiddleware` is the second-line defense: after the request runs, it throws if a cache-only analytical route executed while `modeProvider.Mode == Live`.  
   Source: `PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs:24-47`

### Pipeline slice

Current default runtime selection:

`IPipelineReadProvider`  
→ `LazyPipelineReadProvider`  
→ `DataSourceAwareReadProviderFactory.GetPipelineReadProvider()`  
→ keyed `"Cached"` / `"Live"` `IPipelineReadProvider`  
→ `CachedPipelineReadProvider` or `LivePipelineReadProvider`

Relevant files:

- DI: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`
- Lazy wrapper: `PoTool.Api/Services/LazyPipelineReadProvider.cs:11-67`
- Factory: `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:43-56`
- Cached provider: `PoTool.Api/Services/CachedPipelineReadProvider.cs:15-260`
- Live provider: `PoTool.Api/Services/LivePipelineReadProvider.cs:13-204`

### Work item slice

Current default runtime selection:

`IWorkItemReadProvider`  
→ `LazyWorkItemReadProvider`  
→ `DataSourceAwareReadProviderFactory.GetWorkItemReadProvider()`  
→ keyed `"Cached"` / `"Live"` `IWorkItemReadProvider`  
→ `CachedWorkItemReadProvider` or `LiveWorkItemReadProvider`

Relevant files:

- DI: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`
- Lazy wrapper: `PoTool.Api/Services/LazyWorkItemReadProvider.cs:10-43`
- Factory: `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:28-41`
- Cached provider: `PoTool.Api/Services/CachedWorkItemReadProvider.cs:14-193`
- Live provider: `PoTool.Api/Services/LiveWorkItemReadProvider.cs:12-124`

### Important slice difference from pull requests

Pull requests were simplified so the default provider is deterministic and cache-backed. Pipelines and work items were **not** simplified in the same way:

- `IPullRequestReadProvider -> CachedPullRequestReadProvider`
- `IWorkItemReadProvider -> LazyWorkItemReadProvider`
- `IPipelineReadProvider -> LazyPipelineReadProvider`

Source: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:219-225`

## User-Facing Live Reachability

The table below is strict about **actual HTTP reachability**, not just whether a provider implementation exists.

### Pipeline

| Area | Endpoint / Handler | Provider path | Can hit live today? | Condition |
|---|---|---|---|---|
| Pipeline | `GET /api/pipelines` → `GetAllPipelinesQueryHandler` | `IPipelineReadProvider -> LazyPipelineReadProvider -> Factory -> Cached/Live` | **No** | `/api/pipelines` is classified as `CacheOnlyAnalyticalRead`; middleware either sets `Cache` or returns `409` before the handler runs. `PoTool.Api/Controllers/PipelinesController.cs:35-48`, `PoTool.Api/Handlers/Pipelines/GetAllPipelinesQueryHandler.cs:14-29`, `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:35-75` |
| Pipeline | `GET /api/pipelines/{id}/runs` → `GetPipelineRunsQueryHandler` | Same provider path | **No** | Same cache-only middleware behavior. `PoTool.Api/Controllers/PipelinesController.cs:53-69`, `PoTool.Api/Handlers/Pipelines/GetPipelineRunsQueryHandler.cs:14-29` |
| Pipeline | `GET /api/pipelines/metrics` → `GetPipelineMetricsQueryHandler` | Same provider path | **No** | Same cache-only middleware behavior. `PoTool.Api/Controllers/PipelinesController.cs:74-109`, `PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs:15-130` |
| Pipeline | `GET /api/pipelines/runs` → `GetPipelineRunsForProductsQueryHandler` | Same provider path | **No** | Same cache-only middleware behavior. `PoTool.Api/Controllers/PipelinesController.cs:114-149`, `PoTool.Api/Handlers/Pipelines/GetPipelineRunsForProductsQueryHandler.cs:14-43` |
| Pipeline | `GET /api/pipelines/definitions` → `GetPipelineDefinitionsQueryHandler` | Same provider path | **No** through current HTTP route | The route is still under `/api/pipelines`, so middleware makes it cache-only. The handler itself still supports live provider resolution if invoked under a Live-mode request. `PoTool.Api/Controllers/PipelinesController.cs:154-171`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:14-64` |
| Pipeline | `GET /api/pipelines/insights` → `GetPipelineInsightsQueryHandler` | **No provider**; direct `PoToolDbContext` cache query | **No** | Handler already reads only cached DB data. `PoTool.Api/Controllers/PipelinesController.cs:178-209`, `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:23-236` |

### Work items

| Area | Endpoint / Handler | Provider path | Can hit live today? | Condition |
|---|---|---|---|---|
| Work item analytical | `GET /api/workitems` → `GetAllWorkItemsQueryHandler` | `IWorkItemReadProvider -> LazyWorkItemReadProvider -> Factory -> Cached/Live` | **No** through provider path | `/api/workitems` is cache-only; middleware either sets `Cache` or returns `409`. `PoTool.Api/Controllers/WorkItemsController.cs:29-45`, `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsQueryHandler.cs:15-72`, `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:35-75` |
| Work item analytical | `GET /api/workitems/area-paths` → `GetDistinctAreaPathsQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:47-63`, `PoTool.Api/Handlers/WorkItems/GetDistinctAreaPathsQueryHandler.cs:13-44` |
| Work item analytical | `GET /api/workitems/validated` → `GetAllWorkItemsWithValidationQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:65-89`, `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsWithValidationQueryHandler.cs:18-144` |
| Work item analytical | `GET /api/workitems/validated/{tfsId}` → `GetWorkItemByIdWithValidationQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:91-123` |
| Work item analytical | `GET /api/workitems/filter/{filter}` → `GetFilteredWorkItemsQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:360-378` |
| Work item analytical | `GET /api/workitems/{tfsId}` → `GetWorkItemByIdQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:380-404`, `PoTool.Api/Handlers/WorkItems/GetWorkItemByIdQueryHandler.cs:13-33` |
| Work item analytical | `GET /api/workitems/goals/all` → `GetAllGoalsQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:436-452`, `PoTool.Api/Handlers/WorkItems/GetAllGoalsQueryHandler.cs:16-87` |
| Work item analytical | `GET /api/workitems/goals` → `GetGoalHierarchyQueryHandler` | Same provider path | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:492-533`, `PoTool.Api/Handlers/WorkItems/GetGoalHierarchyQueryHandler.cs:17-55` |
| Work item analytical | `GET /api/workitems/advanced-filter` → `GetFilteredWorkItemsAdvancedQueryHandler` | Mix of `IWorkItemReadProvider` and `IWorkItemRepository` | **No** through provider path | Same cache-only middleware behavior. `PoTool.Api/Controllers/WorkItemsController.cs:588-637`, `PoTool.Api/Handlers/WorkItems/GetFilteredWorkItemsAdvancedQueryHandler.cs:13-140` |
| Work item analytical but still live | `GET /api/workitems/{id}/state-timeline` → `GetWorkItemStateTimelineQueryHandler` | Cache lookup via repository **plus** `GetWorkItemRevisionsQueryHandler -> ITfsClient` | **Yes** | The route is under cache-only `/api/workitems`, but the handler still calls TFS revisions indirectly for every request. This is live reachability inside a cache-only analytical path. `PoTool.Api/Controllers/WorkItemsController.cs:535-561`, `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:12-37` |
| Work item explicit live discovery | `POST /api/workitems/validate` → `ValidateWorkItemQueryHandler` | Direct `ITfsClient.GetWorkItemByIdAsync` | **Yes** | Explicit live route and direct TFS query. `PoTool.Api/Controllers/WorkItemsController.cs:406-434`, `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs:13-97`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-41` |
| Work item explicit live discovery but misclassified | `GET /api/workitems/area-paths/from-tfs` → `GetAreaPathsFromTfsQueryHandler` | Direct `ITfsClient.GetAreaPathsAsync` | **Yes** | The controller/client use `/area-paths/from-tfs`, but `DataSourceModeConfiguration` whitelists `/area-paths-from-tfs`. The real route therefore falls through `Unknown -> Live`, not explicit `LiveAllowed`. `PoTool.Api/Controllers/WorkItemsController.cs:454-471`, `PoTool.Client/Services/WorkItemService.cs:25-26`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-40`, `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:78-88` |
| Work item explicit live discovery but misclassified | `GET /api/workitems/goals/from-tfs` → `GetGoalsFromTfsQueryHandler` | Direct `ITfsClient.GetWorkItemsByTypeAsync` | **Yes** | Same route mismatch as area paths. `PoTool.Api/Controllers/WorkItemsController.cs:473-490`, `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs:17-87`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-40` |
| Work item direct live detail but misclassified | `GET /api/workitems/{workItemId}/revisions` → `GetWorkItemRevisionsQueryHandler` | Direct `ITfsClient.GetWorkItemRevisionsAsync` | **Yes** | The handler always calls TFS, but the whitelist `/api/workitems/revisions` does not match `/api/workitems/{id}/revisions`. This remains a live call under a cache-only controller prefix. `PoTool.Api/Controllers/WorkItemsController.cs:564-585`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:12-37`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-41` |
| Work item live refresh/write support | `POST /api/workitems/{tfsId}/refresh-from-tfs` | Direct `ITfsClient.GetWorkItemByIdAsync` then cache upsert | **Yes** | Explicit live refresh, but not listed in `LiveModeAllowedRoutes`; it currently sits under the cache-only `/api/workitems` prefix. `PoTool.Api/Controllers/WorkItemsController.cs:223-244`, `PoTool.Api/Handlers/WorkItems/RefreshWorkItemFromTfsCommandHandler.cs:12-43` |
| Work item live refresh/write support | `POST /api/workitems/by-root-ids/refresh-from-tfs` | Direct `ITfsClient.GetWorkItemsByRootIdsAsync` then cache upsert | **Yes** | Same as above. `PoTool.Api/Controllers/WorkItemsController.cs:246-269`, `PoTool.Api/Handlers/WorkItems/RefreshWorkItemsByRootIdsFromTfsCommandHandler.cs:12-49` |

### Metrics / workspace endpoints that use pipeline or work item data

| Area | Endpoint / Handler | Data path | Can hit live today? | Condition |
|---|---|---|---|---|
| Work item metrics | `GET /api/metrics/backlog-health` → `GetBacklogHealthQueryHandler` | `SprintScopedWorkItemLoader`, which uses `IWorkItemReadProvider` or `GetWorkItemsByRootIdsQuery` | **No** through HTTP provider path | `/api/metrics` is cache-only; request mode is `Cache` or `409`. `PoTool.Api/Controllers/MetricsController.cs:110-156`, `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs:16-79`, `PoTool.Api/Services/SprintScopedWorkItemLoader.cs:26-80` |
| Work item metrics | `GET /api/metrics/multi-iteration-health` → `GetMultiIterationBacklogHealthQueryHandler` | `SprintScopedWorkItemLoader` | **No** through HTTP provider path | Same cache-only route behavior. `PoTool.Api/Controllers/MetricsController.cs:158-202`, `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:20-236` |
| Work item metrics | `GET /api/metrics/capacity-plan` → `GetSprintCapacityPlanQueryHandler` | Work-item scope via sprint loader | **No** through HTTP provider path | Same cache-only route behavior. `PoTool.Api/Controllers/MetricsController.cs:244-280` |
| Work item metrics | `GET /api/metrics/sprint-trend` → `GetSprintTrendMetricsQueryHandler` | Direct cached `PoToolDbContext` and cached projection services | **No** | Already cache-backed, independent of provider switching. `PoTool.Api/Controllers/MetricsController.cs:734-783`, `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:19-220` |
| Work item metrics | `GET /api/metrics/sprint-execution` → `GetSprintExecutionQueryHandler` | Direct cached `PoToolDbContext` and cached activity ledger | **No** | Already cache-backed, independent of provider switching. `PoTool.Api/Controllers/MetricsController.cs:950-989`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs:27-220` |
| Work item metrics | `GET /api/metrics/work-item-activity/{workItemId}` → `GetWorkItemActivityDetailsQueryHandler` | Direct cached `PoToolDbContext` and activity ledger | **No** | Already cache-backed, independent of provider switching. `PoTool.Api/Controllers/MetricsController.cs:991-1020`, `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs:12-137` |
| Pipeline metrics | `GET /api/metrics/*` pipeline-adjacent handlers are not used; pipeline analytics live under `/api/pipelines` and `/api/buildquality` | Mixed cached DB/services | **No** | Build quality handlers are already DB-backed, not provider-switched. `PoTool.Api/Controllers/BuildQualityController.cs:14-70` |

## Legitimate Live-Allowed Paths

### Pipeline live-allowed paths

1. **Required live**
   - `POST /api/cachesync/{productOwnerId}/sync` and related cache sync endpoints  
     Reason: sync stages fetch pipeline data from TFS and persist to cache.  
     Sources: `PoTool.Api/Controllers/CacheSyncController.cs:64-222`, `PoTool.Api/Services/Sync/PipelineSyncStage.cs:34-118`
   - `SyncPipelineRunner.DiscoverAndUpsertPipelineDefinitionsAsync(...)`  
     Reason: pipeline definitions are discovered from TFS before pipeline-run sync.  
     Source: `PoTool.Api/Services/Sync/SyncPipelineRunner.cs:532-590`

2. **Probably should stay live**
   - Upstream repository/configuration routes used to configure repos whose pipeline definitions will later be discovered by sync (`/api/repositories`, startup discovery routes). These are not pipeline read endpoints themselves, but they are part of the live setup chain.  
     Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:25-34`, `PoTool.Api/Controllers/StartupController.cs:43-75`

3. **Unclear**
   - `GET /api/pipelines/definitions` if it is ever used as a configuration-time discovery endpoint rather than an analytical cached read.  
     Current code allows live provider behavior inside the handler, but the route itself is classified cache-only.  
     Sources: `PoTool.Api/Controllers/PipelinesController.cs:151-171`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:35-64`

### Work item live-allowed paths

1. **Required live**
   - `POST /api/workitems/validate`  
     Used to validate root work item IDs directly from TFS during product/profile configuration.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:406-434`, `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs:13-97`
   - `GET /api/workitems/area-paths/from-tfs`  
     Used for Add Profile / setup discovery.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:454-471`, `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs:8-49`
   - `GET /api/workitems/goals/from-tfs`  
     Used for Add Profile / setup discovery.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:473-490`, `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs:13-87`
   - `POST /api/workitems/{tfsId}/refresh-from-tfs` and `POST /api/workitems/by-root-ids/refresh-from-tfs`  
     Explicit cache refresh support after workspace actions.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:223-269`, `PoTool.Api/Handlers/WorkItems/RefreshWorkItemFromTfsCommandHandler.cs:12-43`, `PoTool.Api/Handlers/WorkItems/RefreshWorkItemsByRootIdsFromTfsCommandHandler.cs:12-49`

2. **Probably should stay live**
   - `GET /api/workitems/{workItemId}/revisions`  
     Detailed inspection endpoint; direct TFS revision history is likely intentional.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:564-585`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:12-37`
   - `POST /api/workitems/{tfsId}/tags`, `/title-description`, `/backlog-priority`, `/iteration-path`  
     Explicit TFS mutation / post-write refresh support in the workspace.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:272-358`

3. **Unclear**
   - `GET /api/workitems/{id}/state-timeline`  
     It is presented as an analytical timeline endpoint, but it still reconstructs timeline data from live TFS revisions. That may be intentional for detailed inspection, or it may be a leftover cache-bypass that should be redesigned.  
     Sources: `PoTool.Api/Controllers/WorkItemsController.cs:535-561`, `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`

## Guardrail Applicability

### Pipeline

**Recommendation:** **yes / apply now**

Rationale:

- The middleware guardrail model is already effectively in place for `/api/pipelines` because the whole prefix is classified as `CacheOnlyAnalyticalRead`.  
  Source: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:60-68`
- The user-facing pipeline controller does not currently expose separate live write/update endpoints under the same prefix.  
  Source: `PoTool.Api/Controllers/PipelinesController.cs:35-209`
- One major analytical endpoint (`GetPipelineInsightsQueryHandler`) already bypasses the provider and reads directly from the cached DB.  
  Source: `PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:23-236`
- Remaining ambiguity is mostly structural:
  - default DI is still lazy/runtime-conditional
  - the factory still branches for pipelines
  - several handler comments still describe live/cache switching
  - `LivePipelineReadProvider` remains reachable by DI even though HTTP analytical routes should not need it

What would become cache-only:

- Default injected `IPipelineReadProvider` for analytical handlers
- `/api/pipelines` reads that still go through `LazyPipelineReadProvider`

What would remain live-allowed:

- `/api/cachesync` pipeline sync
- `SyncPipelineRunner` discovery/upsert of pipeline definitions from TFS
- any future explicit configuration/discovery endpoint, if it is split out from `/api/pipelines`

Runtime ambiguity removed:

- the default analytical read path could become deterministic, like pull requests
- `LazyPipelineReadProvider` and the pipeline branch in `DataSourceAwareReadProviderFactory` could likely disappear

Risk that remains:

- `GET /api/pipelines/definitions` is the one ambiguous endpoint; decide whether it is analytical cache-backed data or configuration-time live discovery before simplifying too far

### Work items

**Recommendation:** **partially**

Rationale:

- Broad cache-only middleware already covers `/api/workitems` and `/api/metrics`, so many analytical reads are effectively cache-only today.
- However, the work item slice is not as cleanly separated as pull requests:
  - explicit live discovery routes are partially misclassified
  - revision/detail routes still hit TFS under the `/api/workitems` cache-only prefix
  - post-write refresh/update endpoints also live under `/api/workitems`
  - one analytical route (`state-timeline`) still calls TFS revisions during a cache-only request
- In other words, the **guardrail idea** fits analytical work item paths, but the **current route layout** is too mixed to safely do a direct PR-style cleanup first.

What would become cache-only:

- provider-backed analytical reads such as:
  - `GetAllWorkItemsQueryHandler`
  - `GetDistinctAreaPathsQueryHandler`
  - `GetAllWorkItemsWithValidationQueryHandler`
  - `GetWorkItemByIdQueryHandler`
  - `GetAllGoalsQueryHandler`
  - `GetGoalHierarchyQueryHandler`
  - `SprintScopedWorkItemLoader`-driven metrics reads

What would remain live-allowed:

- validate / setup discovery
- refresh-from-TFS commands
- TFS mutations and post-write refresh support
- probably revisions

Runtime ambiguity removed if applied after cleanup:

- default `IWorkItemReadProvider` for analytical flows could become deterministic
- `LazyWorkItemReadProvider` and the work item branch in `DataSourceAwareReadProviderFactory` could shrink or disappear for analytical use

Risk that remains:

- the slice already contains non-provider live behavior, so simplifying provider wiring alone would not fully solve the architectural ambiguity

## Cleanup Opportunity

### Pipeline cleanup likely available after guardrails

Realistic simplifications:

- bind default `IPipelineReadProvider` directly to `CachedPipelineReadProvider` for analytical reads
- remove `LazyPipelineReadProvider`
- remove `GetPipelineReadProvider()` from `DataSourceAwareReadProviderFactory`
- update handler comments that still describe live/cache runtime switching
- keep `LivePipelineReadProvider` only for explicit live sync/discovery scenarios if still needed

Files likely affected:

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Services/LazyPipelineReadProvider.cs`
- `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- `PoTool.Api/Handlers/Pipelines/*.cs`

### Work item cleanup likely available after route-intent cleanup

Realistic simplifications:

- make analytical work item handlers deterministic and cache-backed
- narrow `IWorkItemReadProvider` usage to analytical reads only
- remove or reduce `LazyWorkItemReadProvider` only after explicit live routes are separated
- reduce factory branching for analytical work item reads
- leave direct live handlers for validate / revisions / refresh / write support explicitly outside the analytical provider path

Files likely affected:

- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- `PoTool.Api/Controllers/WorkItemsController.cs`
- `PoTool.Api/Services/LazyWorkItemReadProvider.cs`
- `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- selected `PoTool.Api/Handlers/WorkItems/*.cs`

## Risks and Blockers

### Pipeline

1. **Ambiguous `definitions` endpoint** — **medium**
   - `GetPipelineDefinitionsQueryHandler` still supports live provider behavior, but the route sits under cache-only `/api/pipelines`.  
   - If that endpoint is actually used for setup/discovery, simplifying it to cache-only would be a functional change.  
   - Sources: `PoTool.Api/Controllers/PipelinesController.cs:151-171`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:35-64`

2. **Provider abstraction still does real work in tests/DI** — **low**
   - Existing lazy/factory tests and registrations still assume runtime switching for pipelines.  
   - Cleanup is still straightforward, but not zero-touch.  
   - Sources: `PoTool.Tests.Unit/Services/LazyReadProviderTests.cs`, `PoTool.Tests.Unit/Services/DataSourceAwareReadProviderFactoryTests.cs`

### Work items

1. **Live discovery route mismatch in route-intent configuration** — **high**
   - Configuration/tests whitelist `/api/workitems/area-paths-from-tfs` and `/api/workitems/goals-from-tfs`.
   - Controller/client use `/api/workitems/area-paths/from-tfs` and `/api/workitems/goals/from-tfs`.
   - Those routes still work only because `Unknown` defaults to `Live`, not because they are explicitly classified correctly.  
   - Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:20-40`, `PoTool.Api/Controllers/WorkItemsController.cs:454-490`, `PoTool.Client/Services/WorkItemService.cs:25-26`, `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs:29-37`

2. **Revision route is live but not actually whitelisted** — **high**
   - The whitelist contains `/api/workitems/revisions`, but the real route is `/api/workitems/{workItemId}/revisions`.
   - That means a direct-TFS detail endpoint still lives under the cache-only `/api/workitems` umbrella.  
   - Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-41`, `PoTool.Api/Controllers/WorkItemsController.cs:571-585`

3. **Analytical state timeline still calls TFS** — **high**
   - `GetWorkItemStateTimelineQueryHandler` loads cached work item data, then always fetches revisions through `GetWorkItemRevisionsQuery`, which is direct TFS.
   - That is incompatible with a strict “cache-only analytical reads” model until the endpoint is reclassified or redesigned.  
   - Sources: `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:25-37`

4. **Mixed-purpose `/api/workitems` controller** — **high**
   - The same controller contains analytical reads, explicit live discovery reads, refresh-from-TFS commands, and TFS update commands.
   - Broad prefix-based guardrails are therefore too coarse for cleanup unless endpoints are explicitly separated/classified first.  
   - Source: `PoTool.Api/Controllers/WorkItemsController.cs:29-637`

5. **Provider cleanup would only cover part of the slice** — **medium**
   - Several important work item metrics already use cached `PoToolDbContext`, repositories, or `RepositoryBackedWorkItemReadProvider`.
   - That means a PR-style provider cleanup would reduce confusion, but it would not fully normalize the slice.  
   - Sources: `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:19-220`, `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs:27-220`, `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs:12-137`, `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs:44-89`

## Final Recommendation

### Pipeline

**Recommendation: apply now**

Why:

- The HTTP guardrail is already effectively there.
- User-facing analytical pipeline endpoints do not appear to require live mode.
- The remaining work is mainly cleanup/simplification of provider wiring.

What the next prompt should target:

1. confirm whether `GET /api/pipelines/definitions` is analytical-only or needs an explicit live split
2. if analytical-only, mirror the PR cleanup:
   - default `IPipelineReadProvider -> CachedPipelineReadProvider`
   - remove `LazyPipelineReadProvider`
   - remove pipeline branching from `DataSourceAwareReadProviderFactory`
   - add focused DI/provider tests and update docs/comments

### Work items

**Recommendation: apply partially**

Why:

- Analytical work item reads generally fit the cache-only model.
- But the slice first needs route-intent and endpoint-boundary cleanup because live discovery/detail/refresh/write paths are still mixed into `/api/workitems`, and some are misclassified.

What the next prompt should target:

1. audit and fix route intent classification for work item live routes:
   - `/api/workitems/area-paths/from-tfs`
   - `/api/workitems/goals/from-tfs`
   - `/api/workitems/{id}/revisions`
   - refresh/update endpoints under `/api/workitems`
2. decide whether `/api/workitems/{id}/state-timeline` should remain live-assisted or become cache-backed
3. only after that, evaluate PR-style provider cleanup for the **remaining analytical work item handlers**

## Validation

Analysis was grounded in current code inspection and baseline verification:

- `dotnet restore PoTool.sln`
- `dotnet build PoTool.sln --configuration Release --no-restore` ✅
- Focused baseline test run for middleware + pipeline/work-item-related handlers showed **pre-existing unrelated failures** in `GetMultiIterationBacklogHealthQueryHandlerMultiProductTests`; this report does not change code or attempt to address them.
