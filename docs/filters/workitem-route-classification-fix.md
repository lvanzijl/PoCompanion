# Work Item Route Classification Fix

## Summary

This change fixes incorrect and ambiguous work item route → `DataSourceMode` mappings without redesigning the provider architecture.

What was fixed:

- corrected work item discovery route mismatches between controller/client paths and `DataSourceModeConfiguration`
  - `/api/workitems/area-paths/from-tfs`
  - `/api/workitems/goals/from-tfs`
- replaced the incorrect non-parameterized revisions mapping with explicit parameterized detail-route classification
  - `/api/workitems/{workItemId}/revisions`
- made work item live/write routes explicit instead of letting the broad `/api/workitems` cache-only prefix classify them incorrectly
- removed work item dependence on `Unknown -> Live` fallback by ensuring every current `WorkItemsController` route now resolves explicitly as:
  - cache-only analytical
  - live-allowed
  - or documented ambiguous (with a temporary explicit live classification in code)

The middleware design was not changed. The fix is limited to route-intent classification, tests, and documentation.  
Sources: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`, `PoTool.Api/Controllers/WorkItemsController.cs`

## Route Inventory

| HTTP | Route | Controller action | Classification |
|---|---|---|---|
| GET | `/api/workitems` | `GetAll` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/area-paths` | `GetDistinctAreaPaths` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validated` | `GetAllWithValidation` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validated/{tfsId:int}` | `GetByIdWithValidation` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validation-triage` | `GetValidationTriage` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validation-queue` | `GetValidationQueue` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validation-fix` | `GetValidationFixSession` | CacheOnlyAnalyticalRead |
| POST | `/api/workitems/{tfsId:int}/refresh-from-tfs` | `RefreshFromTfs` | LiveAllowed |
| POST | `/api/workitems/by-root-ids/refresh-from-tfs` | `RefreshByRootIdsFromTfs` | LiveAllowed |
| POST | `/api/workitems/{tfsId:int}/tags` | `UpdateTags` | LiveAllowed |
| POST | `/api/workitems/{tfsId:int}/title-description` | `UpdateTitleDescription` | LiveAllowed |
| POST | `/api/workitems/{tfsId:int}/backlog-priority` | `UpdateBacklogPriority` | LiveAllowed |
| POST | `/api/workitems/{tfsId:int}/iteration-path` | `UpdateIterationPath` | LiveAllowed |
| GET | `/api/workitems/filter/{filter}` | `GetFiltered` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/{tfsId:int}` | `GetByTfsId` | CacheOnlyAnalyticalRead |
| POST | `/api/workitems/validate` | `ValidateWorkItem` | LiveAllowed |
| GET | `/api/workitems/goals/all` | `GetAllGoals` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/area-paths/from-tfs` | `GetAreaPathsFromTfs` | LiveAllowed |
| GET | `/api/workitems/goals/from-tfs` | `GetGoalsFromTfs` | LiveAllowed |
| GET | `/api/workitems/goals` | `GetGoalHierarchy` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/{id:int}/state-timeline` | `GetStateTimeline` | Invalid / Ambiguous |
| GET | `/api/workitems/{workItemId:int}/revisions` | `GetWorkItemRevisions` | LiveAllowed |
| GET | `/api/workitems/advanced-filter` | `GetAdvancedFiltered` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/dependency-graph` | `GetDependencyGraph` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validation-history` | `GetValidationHistory` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/validation-impact-analysis` | `GetValidationImpactAnalysis` | CacheOnlyAnalyticalRead |
| POST | `/api/workitems/fix-validation-violations` | `FixValidationViolations` | LiveAllowed |
| POST | `/api/workitems/bulk-assign-effort` | `BulkAssignEffort` | LiveAllowed |
| GET | `/api/workitems/by-root-ids` | `GetByRootIds` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/bug-severity-options` | `GetBugSeverityOptions` | LiveAllowed |
| GET | `/api/workitems/backlog-state/{productId:int}` | `GetBacklogState` | CacheOnlyAnalyticalRead |
| GET | `/api/workitems/health-summary/{productId:int}` | `GetHealthSummary` | CacheOnlyAnalyticalRead |

Source: `PoTool.Api/Controllers/WorkItemsController.cs`

## Classification Table

| Route | Category | Reason |
|---|---|---|
| `/api/workitems`, `/area-paths`, `/validated`, `/validated/{id}`, `/filter/{filter}`, `/goals`, `/goals/all`, `/advanced-filter`, `/dependency-graph`, `/by-root-ids`, `/backlog-state/{productId}`, `/health-summary/{productId}` | CacheOnlyAnalyticalRead | Cached dashboard/detail/filtering reads driven by `IWorkItemReadProvider` or repository-backed analytical queries. |
| `/api/workitems/validation-triage`, `/validation-queue`, `/validation-fix`, `/validation-history`, `/validation-impact-analysis` | CacheOnlyAnalyticalRead | Validation analysis over cached work item/validation data; should not silently call TFS. |
| `/api/workitems/area-paths/from-tfs`, `/goals/from-tfs`, `/validate` | LiveAllowed | Discovery/validation endpoints used during configuration or setup; explicitly bypass cache. |
| `/api/workitems/{id}/revisions` | LiveAllowed | Handler calls `ITfsClient.GetWorkItemRevisionsAsync(...)` directly. |
| `/api/workitems/{id}/refresh-from-tfs`, `/by-root-ids/refresh-from-tfs` | LiveAllowed | Sync/refresh commands fetch data from TFS and update cache. |
| `/api/workitems/{id}/tags`, `/title-description`, `/backlog-priority`, `/iteration-path`, `/fix-validation-violations`, `/bulk-assign-effort` | LiveAllowed | TFS write/update commands; these must not be forced through cache-only analytical guardrails. |
| `/api/workitems/bug-severity-options` | LiveAllowed | Static support/config endpoint; it should not require cache readiness and should not depend on fallback classification. |
| `/api/workitems/{id}/state-timeline` | Invalid / Ambiguous | User-facing analytical route whose handler reads cached work item data but then fetches live revisions through `GetWorkItemRevisionsQuery`. This needs a later split to become cleanly cache-only or explicitly live. |

## Fixes Applied

### 1. Corrected route patterns

Updated `DataSourceModeConfiguration` to match actual controller/client routes:

- from incorrect dash-based patterns:
  - `/api/workitems/area-paths-from-tfs`
  - `/api/workitems/goals-from-tfs`
- to actual slash-based controller routes:
  - `/api/workitems/area-paths/from-tfs`
  - `/api/workitems/goals/from-tfs`

### 2. Fixed parameterized work item detail route matching

Removed the misleading non-parameterized revisions entry and replaced it with explicit parameterized work item detail-route matching for:

- `/api/workitems/{id}/revisions`
- `/api/workitems/{id}/refresh-from-tfs`
- `/api/workitems/{id}/tags`
- `/api/workitems/{id}/title-description`
- `/api/workitems/{id}/backlog-priority`
- `/api/workitems/{id}/iteration-path`
- `/api/workitems/{id}/state-timeline`

Implementation detail:

- `LiveModeAllowedRoutePrefixes` now holds broad live prefixes like `/api/settings`
- `LiveModeAllowedExactRoutes` holds exact live routes
- `IsLiveAllowedWorkItemDetailRoute(...)` handles numeric `/api/workitems/{id}/...` live/detail routes explicitly  
  Source: `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`

### 3. Removed work item dependence on implicit fallback

Before this fix, several work item endpoints were only “working” because:

- the route did not match the intended explicit entry, and/or
- unclassified routes defaulted to Live mode

After this fix:

- discovery routes are explicitly live-allowed
- live write/sync/detail routes are explicitly live-allowed
- analytical routes remain under the cache-only `/api/workitems` prefix
- work item routes no longer depend on `Unknown -> Live` fallback for correctness

### 4. Kept middleware behavior unchanged

No middleware redesign was introduced.

- `DataSourceModeMiddleware` still interprets route intent the same way
- `WorkspaceGuardMiddleware` still enforces cache-only boundaries
- only the route classification data was corrected

## Remaining Ambiguities

### `/api/workitems/{id}/state-timeline`

This endpoint is the main remaining ambiguous route.

Current behavior:

- reads the base work item from the cached repository
- then calls `GetWorkItemRevisionsQuery`
- `GetWorkItemRevisionsQueryHandler` fetches revisions directly from `ITfsClient`

So the route is not honestly cache-only today.

Stabilization choice in this prompt:

- treat it as **explicitly live-capable in code**
- document it as **Invalid / Ambiguous** in the route inventory
- defer the real architectural split to a later prompt

Sources:

- `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetWorkItemRevisionsQueryHandler.cs`

## Validation

### Tests updated

- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
  - corrected slash-based discovery route assertions
  - added coverage for:
    - goals discovery
    - revisions route
    - state timeline route
    - update route
    - validation triage cache-only route
    - bug severity support route

- `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
  - corrected slash-based discovery route assertion
  - added coverage for:
    - parameterized revisions route resolving to Live
    - state timeline route resolving to Live
    - validation-triage child route resolving to Cache when cache is ready

- `PoTool.Tests.Unit/Middleware/WorkspaceGuardMiddlewareTests.cs`
  - corrected slash-based discovery route assertion
  - added coverage for parameterized revisions route remaining allowed in Live mode

### Commands executed

- `dotnet restore PoTool.sln`
- `dotnet build PoTool.sln --configuration Release --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests|FullyQualifiedName~GetWorkItemActivityDetailsQueryHandlerTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Result:

- Release build passed
- 37 focused tests passed

### CI inspection

GitHub Actions was checked for the current branch via MCP:

- recent completed runs on `copilot/enforce-cache-only-analytical-reads` were all `success`
- latest completed run inspected had `0` failed jobs

This prompt therefore addressed route-classification correctness rather than an active failing workflow.  
Sources: GitHub Actions runs `23705858468`, `23705443263`, `23704727657`, `23703651179`, `23703073748`

## Known Limitations

- no provider removal or provider architecture redesign was performed
- `LazyWorkItemReadProvider` and the broader work item provider model are unchanged
- `Unknown -> Live` fallback still exists globally in middleware for non-work-item, non-classified routes; this prompt removes work item dependence on it but does not redesign the fallback itself
- `/api/workitems/{id}/state-timeline` still needs a future architectural split because it mixes cached and live data sources
- no controller route templates were changed because the controller/client paths were already correct; the misclassification lived in `DataSourceModeConfiguration`
