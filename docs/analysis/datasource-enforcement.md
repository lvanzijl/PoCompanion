# DataSourceMode Enforcement

## Summary

Prompt 25 converts route classification from advisory behavior into enforced runtime guarantees.

What was added:

- runtime route resolution now fails fast for unclassified routes
- ambiguous routes are explicitly blocked before handler execution
- request middleware now sets and logs explicit route intent and resolved mode
- live provider entry points now guard against cache-only requests reaching live providers
- focused tests now cover:
  - unknown route failure
  - blocked ambiguous route behavior
  - provider enforcement for cache-only vs live-allowed access

Changed files:

- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- `PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
- `PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- `PoTool.Api/Services/LiveWorkItemReadProvider.cs`
- `PoTool.Api/Services/LivePipelineReadProvider.cs`
- `PoTool.Api/Services/LivePullRequestReadProvider.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Exceptions/RouteNotClassifiedException.cs`
- `PoTool.Api/Exceptions/InvalidDataSourceUsageException.cs`

## Behavior Changes

### Before

- `DataSourceModeConfiguration.GetRouteIntent(...)` could return `Unknown`
- `DataSourceModeMiddleware` treated unknown routes as Live
- ambiguous endpoints like `/api/workitems/{id}/state-timeline` still executed
- live providers logged possible cache bypasses, but did not block cache-only misuse
- runtime behavior still depended on implicit fallback

### After

- unknown routes now throw `RouteNotClassifiedException`
- middleware no longer defaults unclassified routes to Live
- blocked ambiguous routes now throw `NotSupportedException` before next middleware/handler execution
- live providers now throw `InvalidDataSourceUsageException` when current mode is cache-only
- every request path now gets an explicit route-intent decision:
  - `CacheOnlyAnalyticalRead`
  - `LiveAllowed`
  - `BlockedAmbiguous`

## Enforcement Rules

The runtime guarantees now enforced are:

1. **No Unknown fallback**
   - unclassified routes are not allowed to run
   - runtime throws immediately via `RouteNotClassifiedException`

2. **Cache-only routes cannot execute in Live mode**
   - middleware blocks cache-only routes when no successful cache is available
   - live provider entry points also reject cache-only misuse defensively

3. **Live usage must be explicit**
   - live-allowed requests are set to `DataSourceMode.Live` intentionally in middleware
   - route intent is stored in `HttpContext.Items`
   - provider selection and live provider access are logged

4. **Ambiguous routes are blocked**
   - `/api/workitems/{id}/state-timeline` is intentionally blocked
   - TODO marker added: `Requires endpoint split; see Deferred Work in this document`

5. **Structured logging**
   - request mode decisions log as:
     - `[DataSourceMode] Route=/api/... Mode=CacheOnly Provider=Cache`
     - `[DataSourceMode] Route=/api/... Mode=LiveAllowed Provider=Live`
   - blocked violations log as:
     - `[Violation] Route=/api/... Mode=Unknown AttemptedProvider=None Action=Blocked`
     - `[Violation] Route=/api/... Mode=CacheOnly AttemptedProvider=Live Action=Blocked`
     - `[Violation] Route=/api/... Mode=BlockedAmbiguous AttemptedProvider=Live Action=Blocked`

## Violations Prevented

Examples of scenarios now blocked explicitly:

### 1. Unclassified route fallback

Before:

- `/api/unclassified-route`
- route intent resolved to `Unknown`
- middleware defaulted to Live

After:

- `RouteNotClassifiedException`
- request stops immediately

### 2. Cache-only request reaching a live provider

Before:

- a cache-only route could still reach a live provider if provider resolution/injection drifted
- runtime behavior relied on conventions and post-request guardrails

After:

- live provider entry points throw `InvalidDataSourceUsageException` when current mode is cache-only
- violation is logged with route, mode, and attempted provider

### 3. Mixed cache/live analytical route execution

Before:

- `/api/workitems/{id}/state-timeline` read cached work item data
- then fetched live revisions through `GetWorkItemRevisionsQuery`

After:

- route is classified as `BlockedAmbiguous`
- middleware throws `NotSupportedException`
- endpoint no longer executes silently with mixed behavior

## Tests

### Updated tests

- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`
  - state timeline now resolves as `BlockedAmbiguous`
  - unknown route resolution now throws

- `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
  - ambiguous state timeline route now throws `NotSupportedException`
  - unknown route now throws `RouteNotClassifiedException`

- `PoTool.Tests.Unit/Services/DataSourceModeProviderTests.cs`
  - verifies mode access throws when middleware has not set a mode
  - verifies `SetCurrentMode(...)` makes the mode readable

- `PoTool.Tests.Unit/Services/DataSourceAwareReadProviderFactoryTests.cs`
  - verifies cache mode resolves cached work item provider
  - verifies live mode resolves live work item provider

- `PoTool.Tests.Unit/Services/LivePipelineReadProviderDataSourceEnforcementTests.cs`
  - verifies cache mode + live provider throws
  - verifies live mode allows the live provider call

### Validation commands

- `dotnet restore PoTool.sln`
- `dotnet build PoTool.sln --configuration Release --no-restore`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~WorkspaceGuardMiddlewareTests|FullyQualifiedName~DataSourceModeProviderTests|FullyQualifiedName~DataSourceAwareReadProviderFactoryTests|FullyQualifiedName~LivePipelineReadProviderDataSourceEnforcementTests|FullyQualifiedName~LivePullRequestReadProviderTests|FullyQualifiedName~LazyReadProviderTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Validation result:

- Release build passed
- 61 focused tests passed

## Deferred Work

The following are intentionally deferred:

- endpoint splitting / provider redesign
- `WorkItemQuery` / `WorkItemLive` separation
- broader external-call enforcement beyond current live provider entry points
- redesign of direct `ITfsClient` callers outside the current explicit route/middleware enforcement scope

Still requires future architectural split:

- `/api/workitems/{id}/state-timeline`
  - currently blocked intentionally
  - TODO marker included: `Requires endpoint split; see Deferred Work in this document`

Decision retained for now:

- `/api/workitems/{id}` remains cache-only
- hypothetical future detail routes such as `/api/workitems/{id}/children` must default to cache-only unless explicitly classified otherwise; that route is not currently implemented in `WorkItemsController`
