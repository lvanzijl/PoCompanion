# Pipeline Guardrail Implementation Plan and Work Item API Split Design

## Summary

- **Pipeline approach:** implement the guardrail by making the default analytical `IPipelineReadProvider` deterministic and cache-backed, while keeping the existing request-time middleware guard in place. This is an implementation-ready cleanup because the current `/api/pipelines` HTTP surface is already cache-only by route intent, and legitimate live pipeline flows already bypass the analytical provider path.  
  Key sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:60-68`, `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:35-88`, `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`, `PoTool.Api/Controllers/PipelinesController.cs:35-209`

- **Work item strategy:** do **not** copy the pipeline/PR cleanup directly. First split work item APIs into a cache-only **WorkItemQuery** surface and an explicit live **WorkItemCommand / WorkItemLive** surface. The current controller still mixes analytical reads, live discovery, live detail reads, refresh operations, and live updates under `/api/workitems`.  
  Key sources: `PoTool.Api/Controllers/WorkItemsController.cs:229-919`, `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:23-68`, `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`, `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs:25-37`

## Pipeline Guardrail Plan

### Pipeline analytical endpoints

The following user-facing endpoints serve pipeline insights, dashboards, or statistics.

| Endpoint | File / handler | Current provider usage | Notes |
|---|---|---|---|
| `GET /api/pipelines` | `PoTool.Api/Controllers/PipelinesController.cs:35-48` → `GetAllPipelinesQueryHandler` | `IPipelineReadProvider -> LazyPipelineReadProvider -> DataSourceAwareReadProviderFactory -> Cached/Live` | Workspace analytical read |
| `GET /api/pipelines/{id}/runs` | `PoTool.Api/Controllers/PipelinesController.cs:53-69` → `GetPipelineRunsQueryHandler` | Same runtime-switched pipeline provider path | Workspace analytical read |
| `GET /api/pipelines/metrics` | `PoTool.Api/Controllers/PipelinesController.cs:74-109` → `GetPipelineMetricsQueryHandler` | Same runtime-switched pipeline provider path | Dashboard/statistics read |
| `GET /api/pipelines/runs` | `PoTool.Api/Controllers/PipelinesController.cs:114-149` → `GetPipelineRunsForProductsQueryHandler` | Same runtime-switched pipeline provider path | Dashboard/statistics read |
| `GET /api/pipelines/definitions` | `PoTool.Api/Controllers/PipelinesController.cs:154-171` → `GetPipelineDefinitionsQueryHandler` | Same runtime-switched pipeline provider path | Ambiguous: analytical cache read vs possible future discovery use |
| `GET /api/pipelines/insights` | `PoTool.Api/Controllers/PipelinesController.cs:178-209` → `GetPipelineInsightsQueryHandler` | No `IPipelineReadProvider`; direct cached `PoToolDbContext` reads | Already deterministic and cache-backed |
| `GET /api/buildquality/rolling` | `PoTool.Api/Controllers/BuildQualityController.cs:28-48` | No pipeline provider; delivery filter + cached build-quality services | Pipeline-adjacent dashboard, already cache-backed |
| `GET /api/buildquality/sprint` | `PoTool.Api/Controllers/BuildQualityController.cs:50-68` | No pipeline provider; delivery filter + cached build-quality services | Pipeline-adjacent dashboard, already cache-backed |
| `GET /api/buildquality/pipeline` | `PoTool.Api/Controllers/BuildQualityController.cs:70-83` → `GetBuildQualityPipelineDetailQueryHandler` | No pipeline provider; direct cached `PoToolDbContext` + `BuildQualityScopeLoader` | Already cache-backed. `PoTool.Api/Handlers/BuildQuality/GetBuildQualityPipelineDetailQueryHandler.cs:13-88` |

### Current enforcement location

The current request-level enforcement already exists:

1. `DataSourceModeConfiguration` classifies `/api/pipelines` as `CacheOnlyAnalyticalRead`.  
   Source: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:60-68`

2. `DataSourceModeMiddleware` blocks cache-only routes unless `IDataSourceModeProvider.GetModeAsync(...)` returns `Cache`. It does **not** silently fall back to live for analytical routes.  
   Source: `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:35-75`

3. `WorkspaceGuardMiddleware` throws if a cache-only route completes in `Live` mode.  
   Source: `PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs:24-47`

4. The remaining ambiguity is **not** middleware behavior. It is the analytical provider registration:
   - default `IPipelineReadProvider -> LazyPipelineReadProvider`
   - lazy wrapper calls `DataSourceAwareReadProviderFactory.GetPipelineReadProvider()`
   - factory still switches between keyed `"Cached"` and `"Live"`
   Sources: `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:205-225`, `PoTool.Api/Services/LazyPipelineReadProvider.cs:11-67`, `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs:43-56`

### Enforcement approach

#### Where to enforce

**Primary enforcement point:** **DI/provider resolution boundary**, not provider implementation logic.

Reason:

- request-time route guarding already exists and already blocks live fallback for `/api/pipelines`
- changing provider implementations would preserve runtime ambiguity instead of removing it
- the PR slice precedent was to make the default injected analytical provider deterministic

#### Exact implementation-ready changes

##### 1. `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

Change the default pipeline registration from lazy/runtime-switched to deterministic cached:

- keep keyed registrations:
  - `services.AddKeyedScoped<IPipelineReadProvider, LivePipelineReadProvider>("Live");`
  - `services.AddKeyedScoped<IPipelineReadProvider, CachedPipelineReadProvider>("Cached");`
- replace:
  - `services.AddScoped<IPipelineReadProvider, LazyPipelineReadProvider>();`
- with:
  - `services.AddScoped<IPipelineReadProvider, CachedPipelineReadProvider>();`

Why:

- all current analytical `/api/pipelines` requests must already be cache-backed by middleware
- default injected pipeline reads should reflect that invariant directly

##### 2. `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`

Remove `GetPipelineReadProvider()`.

Why:

- after DI is deterministic for pipeline analytical reads, pipeline branching in the factory is dead code
- the factory still remains needed for work items until the work item split is complete

##### 3. `PoTool.Api/Services/LazyPipelineReadProvider.cs`

Delete the file and its registration.

Why:

- there is no longer any valid analytical pipeline path that should defer provider selection to request-time mode

##### 4. Pipeline handlers/comments

Update handler comments that still describe “live and cached modes” for pipeline analytical handlers:

- `GetAllPipelinesQueryHandler`
- `GetPipelineRunsQueryHandler`
- `GetPipelineMetricsQueryHandler`
- `GetPipelineRunsForProductsQueryHandler`
- `GetPipelineDefinitionsQueryHandler`

Reason:

- after the DI change, those handlers are no longer runtime-live-capable through default injection

##### 5. No `DataSourceModeMiddleware` behavior change

Keep current middleware behavior as-is.

Reason:

- it already blocks analytical pipeline requests when cache is unavailable
- overriding to cache when cache is not ready would be incorrect
- switching pipeline analytical reads to deterministic cached DI is a simplification, not a middleware rewrite

##### 6. No `CachedPipelineReadProvider` / `LivePipelineReadProvider` behavior change

Do not add cache enforcement logic inside provider implementations.

Reason:

- provider-level runtime checks are redundant once the analytical provider is deterministic
- `LivePipelineReadProvider` should remain available only for explicit keyed resolution or future live-only flows

### Protect legitimate live paths

Pipeline flows that must remain live are already outside the analytical provider path:

| Flow | File / class | Why it stays live | Guardrail impact |
|---|---|---|---|
| Cache sync for pipeline runs | `PoTool.Api/Services/Sync/PipelineSyncStage.cs:34-118` | Reads runs from TFS via `ITfsClient` and writes cache | Unaffected; does not use default `IPipelineReadProvider` |
| Pipeline definition discovery during sync context build | `PoTool.Api/Services/Sync/SyncPipelineRunner.cs:532-590` | Discovers definitions from TFS before pipeline sync | Unaffected; direct `ITfsClient` use |
| Repository/configuration/startup setup | `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:25-34` | Setup routes remain `LiveAllowed` | Unaffected |

Important current code fact:

- There is **no** current pipeline controller endpoint explicitly dedicated to live onboarding or connectivity testing.
- Therefore the analytical guardrail does not need a pipeline-specific live bypass path today.

If a future live pipeline discovery endpoint is needed, it should use:

- a separate controller/route outside cache-only `/api/pipelines`, or
- explicit keyed live provider resolution in a dedicated handler

It should **not** reuse the default analytical `IPipelineReadProvider`.

### Remove ambiguity

| Code | Status | Why |
|---|---|---|
| `LazyPipelineReadProvider` | **Removable now** | Default pipeline analytical reads should no longer switch at call time |
| `DataSourceAwareReadProviderFactory.GetPipelineReadProvider()` | **Removable now** | Factory pipeline branching becomes dead once default DI is cached |
| “supports both Live and Cached modes” comments in pipeline handlers | **Removable now** | Becomes inaccurate after deterministic DI |
| Keyed `"Live"` `IPipelineReadProvider` registration | **Keep now** | Safe to preserve for future explicit live-only use and avoids coupling sync/discovery decisions to this cleanup |
| Any future attempt to reuse `/api/pipelines/definitions` for live discovery | **Removable only after validation / product decision** | Current route is cache-only; only split it if an explicit live use case is confirmed |

### Validation plan

#### Automated validation

Run and/or update:

1. `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
   - add a pipeline equivalent of the PR default-provider test:
     - default `IPipelineReadProvider` resolves `CachedPipelineReadProvider`
     - keyed `"Live"` still resolves `LivePipelineReadProvider`
     - keyed `"Cached"` still resolves `CachedPipelineReadProvider`

2. Guardrail tests already relevant:
   - `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
   - `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
   - `PoTool.Tests.Unit/Middleware/WorkspaceGuardMiddlewareTests.cs`

3. Pipeline behavior tests already covering the analytical slice:
   - `GetPipelineMetricsQueryHandlerTests`
   - `GetPipelineRunsForProductsQueryHandlerTests`
   - `GetPipelineInsightsQueryHandlerTests`
   - `GetPipelineInsightsBreakdownTests`
   - `PipelineFilterResolutionServiceTests`
   - `PipelinesControllerCanonicalFilterTests`
   - `PipelineServiceTests`

These existing suites already validate the cache-oriented pipeline UI/service path used in the repository baseline.

#### Manual/runtime validation

1. Trigger pipeline pages with a profile that has successful cache sync.
2. Verify logs contain:
   - middleware cache selection log from `DataSourceModeMiddleware`  
     (`"DataSourceMode set to Cache for analytical route ..."`)  
     Source: `PoTool.Api/Middleware/DataSourceModeMiddleware.cs:48-52`
   - cached provider debug logs from `CachedPipelineReadProvider`  
     Sources: `PoTool.Api/Services/CachedPipelineReadProvider.cs:31-33`, `103-105`, `157-159`
3. Verify logs do **not** contain any `LivePipelineReadProvider.{Method} called — may indicate cache bypass` warnings.  
   Sources: `PoTool.Api/Services/LivePipelineReadProvider.cs:36-37`, `46-47`, `79-80`, `96-97`, `123-129`, `134-135`, `174-175`
4. Verify behavior when cache is not ready:
   - `/api/pipelines/*` returns `409 Cache not ready`
   - no pipeline handler executes

## Work Item Split Design

### Current usage mapping

#### A. Analytical work item reads

These endpoints behave like workspace/dashboard/query flows and should ultimately be cache-only:

- `GET /api/workitems`
- `GET /api/workitems/area-paths`
- `GET /api/workitems/validated`
- `GET /api/workitems/validated/{tfsId}`
- `GET /api/workitems/validation-triage`
- `GET /api/workitems/validation-queue`
- `GET /api/workitems/validation-fix`
- `GET /api/workitems/filter/{filter}`
- `GET /api/workitems/{tfsId}`
- `GET /api/workitems/goals/all`
- `GET /api/workitems/goals`
- `GET /api/workitems/advanced-filter`
- `GET /api/workitems/dependency-graph`
- `GET /api/workitems/validation-history`
- `GET /api/workitems/validation-impact-analysis`
- `GET /api/workitems/by-root-ids`
- `GET /api/workitems/backlog-state/{productId}`
- `GET /api/workitems/health-summary/{productId}`
- sprint/delivery metrics that indirectly use work item analytical data under `/api/metrics`

Representative sources:

- `PoTool.Api/Controllers/WorkItemsController.cs:363-919`
- `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsWithValidationQueryHandler.cs:18-144`
- `PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs:15-152`
- `PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs:18-172`
- `PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs:16-159`

#### B. Operational / live work item interactions

These are explicit live setup, refresh, or mutation flows and should stay outside cache-only analytical abstractions:

- `POST /api/workitems/validate`
- `GET /api/workitems/area-paths/from-tfs`
- `GET /api/workitems/goals/from-tfs`
- `GET /api/workitems/{workItemId}/revisions`
- `POST /api/workitems/{tfsId}/refresh-from-tfs`
- `POST /api/workitems/by-root-ids/refresh-from-tfs`
- `POST /api/workitems/{tfsId}/tags`
- `POST /api/workitems/{tfsId}/title-description`
- `POST /api/workitems/{tfsId}/backlog-priority`
- `POST /api/workitems/{tfsId}/iteration-path`
- `POST /api/workitems/fix-validation-violations`
- `POST /api/workitems/bulk-assign-effort`

Representative sources:

- `PoTool.Api/Controllers/WorkItemsController.cs:223-358`
- `PoTool.Api/Controllers/WorkItemsController.cs:406-490`
- `PoTool.Api/Controllers/WorkItemsController.cs:571-585`
- `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs:13-97`
- `PoTool.Api/Handlers/WorkItems/RefreshWorkItemFromTfsCommandHandler.cs:12-43`
- `PoTool.Api/Handlers/WorkItems/RefreshWorkItemsByRootIdsFromTfsCommandHandler.cs:12-49`
- `PoTool.Api/Handlers/WorkItems/FixValidationViolationBatchCommandHandler.cs:14-98`
- `PoTool.Api/Handlers/WorkItems/BulkAssignEffortCommandHandler.cs:14-105`

#### C. Mixed endpoints

These cannot be safely classified as analytical-only today:

- `GET /api/workitems/{id}/state-timeline`
  - user-facing analytical presentation
  - but implementation calls `GetWorkItemRevisionsQuery`, which hits TFS directly
  - current source: `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`

### Target split

#### A. `WorkItemQuery` (cache-only)

Use for all analytical/workspace reads.

Rules:

- always cache-backed
- no `ITfsClient`
- no runtime fallback to live
- deterministic default DI

Target implementation shape:

- analytical handlers should depend on `IWorkItemQuery` or a cache-only read abstraction with equivalent scope
- existing cached provider/repository logic becomes the backing implementation:
  - `CachedWorkItemReadProvider`
  - `IWorkItemRepository`
  - direct cached `PoToolDbContext` queries

#### B. `WorkItemCommand / WorkItemLive`

Use for:

- discovery
- validation
- revisions/detail reads that intentionally query TFS
- refresh/update/write support

Rules:

- allowed to use `ITfsClient`
- never exposed as the default analytical read path
- explicit in handler/controller naming and routing

Target implementation shape:

- live discovery/read handlers depend on `IWorkItemLive` or direct `ITfsClient`
- update/refresh flows remain commands and continue to upsert cache after TFS operations

### API surface changes

The table below is the concrete endpoint split target.

| Endpoint | Current role | Target layer | Action |
|---|---|---|---|
| `GET /api/workitems` | Analytical list | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/area-paths` | Analytical cache-derived metadata | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validated` | Analytical validation dashboard | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validated/{tfsId}` | Analytical validation detail | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validation-triage` | Analytical dashboard | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validation-queue` | Analytical dashboard | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validation-fix` | Analytical session load | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/filter/{filter}` | Analytical search | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/{tfsId}` | Analytical cached detail | `WorkItemQuery` | Move to Query |
| `POST /api/workitems/validate` | Setup validation against TFS | `WorkItemLive` | Move to Live |
| `GET /api/workitems/goals/all` | Analytical cache read | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/area-paths/from-tfs` | Setup discovery | `WorkItemLive` | Move to Live |
| `GET /api/workitems/goals/from-tfs` | Setup discovery | `WorkItemLive` | Move to Live |
| `GET /api/workitems/goals` | Analytical hierarchy query | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/{id}/state-timeline` | Mixed analytical + live detail | Mixed | **Split**: move current implementation to `WorkItemLive` first; introduce a separate future `WorkItemQuery` timeline only if a cache-backed model is added |
| `GET /api/workitems/{workItemId}/revisions` | Live detail read | `WorkItemLive` | Move to Live |
| `GET /api/workitems/advanced-filter` | Analytical filter query | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/dependency-graph` | Analytical graph/query | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validation-history` | Analytical reporting | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/validation-impact-analysis` | Analytical reporting | `WorkItemQuery` | Move to Query |
| `POST /api/workitems/fix-validation-violations` | Live bulk update | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/bulk-assign-effort` | Live bulk update | `WorkItemCommand` | Move to Command |
| `GET /api/workitems/by-root-ids` | Analytical scoped tree load | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/bug-severity-options` | Static metadata | keep as-is | Keep as-is |
| `GET /api/workitems/backlog-state/{productId}` | Analytical workspace read | `WorkItemQuery` | Move to Query |
| `GET /api/workitems/health-summary/{productId}` | Analytical workspace read | `WorkItemQuery` | Move to Query |
| `POST /api/workitems/{tfsId}/refresh-from-tfs` | Live refresh | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/by-root-ids/refresh-from-tfs` | Live refresh | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/{tfsId}/tags` | Live update | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/{tfsId}/title-description` | Live update | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/{tfsId}/backlog-priority` | Live update | `WorkItemCommand` | Move to Command |
| `POST /api/workitems/{tfsId}/iteration-path` | Live update | `WorkItemCommand` | Move to Command |

### Provider architecture impact

#### New abstraction boundary

Target state:

- `IWorkItemQuery`
  - cache-only
  - supports analytical methods such as:
    - get by root IDs
    - get by area paths
    - get by id
    - get validated data
    - get graph/backlog-state/health-summary source data

- `IWorkItemLive`
  - setup/discovery/detail live reads:
    - validate work item
    - fetch area paths from TFS
    - fetch goals from TFS
    - fetch revisions

- commands continue to use explicit command handlers and `ITfsClient`
  - refresh
  - iteration changes
  - tag/title/priority updates
  - batch fixes / bulk effort updates

#### Ambiguity removed

After the split:

- analytical handlers no longer need `LazyWorkItemReadProvider`
- analytical handlers no longer need `DataSourceAwareReadProviderFactory`
- route intent can classify analytical query routes as cache-only and live routes as explicit exceptions
- live TFS calls become visible by route and abstraction name instead of hidden behind shared `IWorkItemReadProvider`

### Migration strategy

Safe sequence:

1. **Introduce `WorkItemQuery` abstraction**
   - wrap current cache-backed analytical reads
   - do not change route behavior yet

2. **Route analytical handlers to `WorkItemQuery`**
   - start with handlers already using `IWorkItemReadProvider` only for cache-friendly reads:
     - `GetAllWorkItemsQueryHandler`
     - `GetAllWorkItemsWithValidationQueryHandler`
     - `GetAllGoalsQueryHandler`
     - `GetGoalHierarchyQueryHandler`
     - `GetDependencyGraphQueryHandler`
     - `GetValidationImpactAnalysisQueryHandler`
     - `GetProductBacklogStateQueryHandler`
     - `GetHealthWorkspaceProductSummaryQueryHandler`

3. **Keep existing live behavior intact**
   - do not rewrite discovery/update handlers yet
   - let `ValidateWorkItemQueryHandler`, revisions, refresh, and update commands continue to use live access

4. **Split controller surface without behavior change**
   - keep old routes temporarily as thin shims if needed
   - add explicit query vs live/command groupings

5. **Validate**
   - ensure analytical pages still use cache only
   - ensure setup/update flows still work live

6. **Prepare for future guardrail**
   - once all analytical handlers are off the runtime-switched provider path, make default analytical work item DI deterministic and remove work-item branching from `DataSourceAwareReadProviderFactory`

## Risks

### Pipeline risks

1. **`/api/pipelines/definitions` remains ambiguous** — **medium**
   - current route is cache-only by middleware
   - current handler/provider shape still allows live behavior if reused outside that route
   - if product requirements later treat definitions as live discovery, the route should be split explicitly rather than silently reintroducing runtime switching  
   Sources: `PoTool.Api/Controllers/PipelinesController.cs:151-171`, `PoTool.Api/Handlers/Pipelines/GetPipelineDefinitionsQueryHandler.cs:35-64`

2. **Test expectations must be updated** — **low**
   - DI/service tests will need to reflect pipeline’s new deterministic default provider
   - this is localized, similar to the PR cleanup precedent  
   Sources: `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs:253-282`

### Work item risks

1. **Route-intent mismatch for live discovery routes** — **high**
   - controller/client use `/area-paths/from-tfs` and `/goals/from-tfs`
   - route config whitelists dash-separated `/area-paths-from-tfs` and `/goals-from-tfs`
   - live setup calls currently work through `Unknown -> Live`, not explicit route intent  
   Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:37-40`, `PoTool.Api/Controllers/WorkItemsController.cs:458-478`, `PoTool.Client/Services/WorkItemService.cs:25-26`

2. **Revisions route is still misclassified** — **high**
   - config whitelists `/api/workitems/revisions`
   - actual route is `/api/workitems/{id}/revisions`  
   Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs:39-40`, `PoTool.Api/Controllers/WorkItemsController.cs:571-585`

3. **State timeline is not split-ready yet** — **high**
   - current implementation requires live revisions
   - there is no current cache-backed replacement for the same feature  
   Sources: `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-72`

4. **Analytical work item reads already use multiple cache-backed patterns** — **medium**
   - some handlers use `IWorkItemReadProvider`
   - others use `IWorkItemRepository`
   - others use direct cached `PoToolDbContext`
   - the split must avoid introducing a second layer of duplicated query composition  
   Representative sources: `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsQueryHandler.cs:15-72`, `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs:14-143`, `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:19-220`

## Final Next Step

**Prompt 23 should implement the pipeline guardrail cleanup only**, with no work item behavioral changes yet:

1. change default `IPipelineReadProvider` DI to `CachedPipelineReadProvider`
2. remove `LazyPipelineReadProvider`
3. remove `GetPipelineReadProvider()` from `DataSourceAwareReadProviderFactory`
4. add/update service collection and focused pipeline guardrail tests
5. update pipeline handler comments to reflect deterministic cache-backed reads
6. verify no `LivePipelineReadProvider` warnings appear during analytical pipeline usage

For work items, Prompt 23 should **not** start the full split. The immediate follow-up after pipeline implementation should be a dedicated work item route/classification prompt that:

1. fixes explicit live route intent mismatches
2. decides the fate of `state-timeline`
3. introduces the non-breaking `WorkItemQuery` vs `WorkItemLive/Command` boundary

## Validation

Baseline validation for this docs-only report:

- `dotnet restore PoTool.sln` ✅
- `dotnet build PoTool.sln --configuration Release --no-restore` ✅
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests|FullyQualifiedName~GetPipelineMetricsQueryHandlerTests|FullyQualifiedName~GetPipelineRunsForProductsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests|FullyQualifiedName~GetPipelineInsightsBreakdownTests|FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~GetSprintTrendMetricsQueryHandlerTests|FullyQualifiedName~GetWorkItemActivityDetailsQueryHandlerTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal` ✅
