# PR Live Provider Usage Audit

## Summary

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LivePullRequestReadProvider.cs` is **not** sync-only.

It is reachable from **user-facing API request paths** through the default `IPullRequestReadProvider` registration:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` registers `IPullRequestReadProvider` as `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LazyPullRequestReadProvider.cs`
- `LazyPullRequestReadProvider` defers every call to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs`
- `DataSourceAwareReadProviderFactory` returns the **cached** provider only when request mode is exactly `DataSourceMode.Cache`
- otherwise it returns the **live** provider

For `/api/pullrequests` routes, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs` chooses cache mode only when the current product owner has a cache state with `LastSuccessfulSync`.
If there is **no active profile**, **no cache state**, or **no successful sync**, the middleware sets **Live** mode for the request.

So the direct answer is:

- `LivePullRequestReadProvider` is **not limited to sync/cache-fill/ingestion/background refresh**
- it is also reachable from **user-facing PR endpoints and handlers** as a **runtime fallback when cache is unavailable**
- some PR endpoints bypass the provider entirely and are cache-only, but several user-facing endpoints can still hit live mode

---

## Registration and Resolution Paths

| Path | File | How selected |
|---|---|---|
| Keyed live registration | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` | `services.AddKeyedScoped<IPullRequestReadProvider, LivePullRequestReadProvider>("Live")` |
| Keyed cached registration | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` | `services.AddKeyedScoped<IPullRequestReadProvider, CachedPullRequestReadProvider>("Cached")` |
| Default injected provider | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` | `services.AddScoped<IPullRequestReadProvider, LazyPullRequestReadProvider>()` |
| Runtime provider factory | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceAwareReadProviderFactory.cs` | `GetPullRequestReadProvider()` returns cached only for `DataSourceMode.Cache`; all other modes resolve live |
| Lazy wrapper delegation | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/LazyPullRequestReadProvider.cs` | Every `IPullRequestReadProvider` method call resolves the current provider at call time |
| Request-time mode selection | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs` | Workspace routes call `GetModeAsync(productOwnerId)`; non-workspace routes are forced to Live |
| Cache availability decision | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceModeProvider.cs` | Returns Cache only when `ProductOwnerCacheStates.LastSuccessfulSync` exists; otherwise returns Live |
| Development-only guard | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs` | Throws after workspace requests that used Live mode, but only in development |

### What this means

Provider selection is **runtime conditional**, not compile-time.
Handlers and controllers typically depend only on `IPullRequestReadProvider`, which means the actual implementation is chosen per request by middleware + factory.

---

## User-Facing Query Reachability

| Endpoint/Handler | Provider path | Can hit live provider? | Condition |
|---|---|---:|---|
| `GET /api/pullrequests` → `GetAllPullRequestsQueryHandler` | Controller → Mediator → `IPullRequestReadProvider.GetAllAsync()` → Lazy → Factory | **Yes** | Any workspace request where middleware resolves mode to Live (no active profile, no cache state, or no successful sync) |
| `GET /api/pullrequests/{id}` → `GetPullRequestByIdQueryHandler` | Controller → Mediator → `GetByIdAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/metrics` → `GetPullRequestMetricsQueryHandler` | Controller → Mediator → `GetByRepositoryNamesAsync()` + batched detail methods → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/filter` → `GetFilteredPullRequestsQueryHandler` | Controller → Mediator → `GetByRepositoryNamesAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/{id}/iterations` → `GetPullRequestIterationsQueryHandler` | Controller → Mediator → `GetIterationsAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/{id}/comments` → `GetPullRequestCommentsQueryHandler` | Controller → Mediator → `GetCommentsAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/{id}/filechanges` → `GetPullRequestFileChangesQueryHandler` | Controller → Mediator → `GetFileChangesAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/review-bottleneck` → `GetPRReviewBottleneckQueryHandler` | Controller → Mediator → `GetAllAsync()` → Lazy → Factory | **Yes** | Same runtime Live fallback conditions |
| `GET /api/pullrequests/sprint-trends` → `GetPrSprintTrendsQueryHandler` | Controller → Mediator → direct `PoToolDbContext` cached tables | **No** | Handler does not use `IPullRequestReadProvider` |
| `GET /api/pullrequests/insights` → `GetPullRequestInsightsQueryHandler` | Controller → Mediator → direct `PoToolDbContext` cached tables | **No** | Handler does not use `IPullRequestReadProvider` |
| `GET /api/pullrequests/delivery-insights` → `GetPrDeliveryInsightsQueryHandler` | Controller → Mediator → direct `PoToolDbContext` cached tables | **No** | Handler does not use `IPullRequestReadProvider` |
| `GET /api/pullrequests/by-workitem/{workItemId}` → `GetPullRequestsByWorkItemIdQueryHandler` | Controller → Mediator → direct `PoToolDbContext` cached tables | **No** | Handler does not use `IPullRequestReadProvider` |

### Important clarification

Not every user-facing PR endpoint is provider-driven.
The provider-driven endpoints above can hit `LivePullRequestReadProvider`.
The insights, delivery-insights, sprint-trends, and by-workitem endpoints are cache-table queries and do **not** route through the live provider.

---

## Sync / Ingestion Usage

Confirmed non-user-facing pull-request data ingestion does **not** use `LivePullRequestReadProvider`.

### Confirmed non-user-facing usages

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/PullRequestSyncStage.cs`
  - Fetches pull requests and PR detail data directly from `ITfsClient`
  - Upserts into cached database tables
  - This is clearly **Sync / ingestion only**

### Notably absent

I did **not** find application code that uses `LivePullRequestReadProvider` as the sync/cache-population adapter.
The sync stage talks to `ITfsClient` directly instead.

That means the live provider’s practical role is mainly:

- request-time live reads selected by factory/middleware
- test coverage for live behavior

rather than ingestion.

---

## Fallback Behavior

These are the exact runtime conditions that can select live mode for PR requests.

### 1. Workspace route with no active profile

In `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs`:

- workspace routes call `ICurrentProfileProvider.GetCurrentProductOwnerIdAsync(...)`
- if it returns `null`, middleware logs a warning and calls `SetCurrentMode(DataSourceMode.Live)`

So user-facing PR routes fall back to live when no active profile is configured or profile lookup fails.

### 2. Workspace route with no cache state row

In `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceModeProvider.cs`:

- `GetModeAsync(productOwnerId)` loads `ProductOwnerCacheStates`
- if no row exists, it returns `DataSourceMode.Live`

So first-use / never-synced product owners route user-facing PR requests to live mode.

### 3. Workspace route with cache state but no successful sync

Also in `GetModeAsync(...)`:

- if a cache row exists but `LastSuccessfulSync` is missing, it returns `DataSourceMode.Live`

So stale-or-incomplete startup states do **not** use cache until at least one successful sync has happened.

### 4. Workspace route with any successful sync

- if `LastSuccessfulSync.HasValue`, `GetModeAsync(...)` returns `DataSourceMode.Cache`
- this remains true even if a new sync is currently in progress

So once a successful cache exists, user-facing PR routes use cache by default.

### 5. Non-workspace routes always live

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs` sets **Live** for non-workspace routes.

This matters less for PR page routes because `/api/pullrequests` is explicitly marked as a workspace route in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`.

### 6. No stale-cache age check

I did **not** find runtime logic that says:

- stale cache timestamp → switch to live
- partial repository coverage → switch to live
- repository mapping missing → switch to live

The selection rule is much simpler:

- **successful sync exists** → Cache
- otherwise → Live

### 7. No durable user toggle forcing future live mode

`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/DataSourceModeController.cs` exposes `SetMode(...)`, but `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/DataSourceModeProvider.cs` does not persist a durable “preferred mode” flag.
`GetModeAsync(...)` still derives future request mode only from cache availability.

So based on actual code, the controller does **not** appear to create a lasting user override that would force user-facing PR routes into live mode after the request ends.

---

## Risk Assessment

The practical meaning for performance work is:

1. **Live PR provider is user-facing in fallback scenarios**
   - It is incorrect to treat it as sync-only infrastructure.
   - Provider performance still matters for real user requests before the first successful cache sync or when no active profile is available.

2. **Not all PR pages share this risk**
   - Metrics, filtered lists, by-id/detail endpoints, and review-bottleneck can hit live mode.
   - Insights, delivery insights, sprint trends, and by-workitem are cache-table handlers and do not use the live provider.

3. **Production guardrails are incomplete**
   - `WorkspaceGuardMiddleware` only enforces the “workspace should be cache-backed” rule in development.
   - In production, workspace requests can still run live when cache prerequisites are not met.

4. **Partial live batching still matters**
   - Because user-facing metrics and detail endpoints can reach live mode, the remaining per-PR TFS fan-out in `LivePullRequestReadProvider` is not purely theoretical or back-office only.

Overall risk statement:

- `LivePullRequestReadProvider` is **conditionally user-facing**, not merely ingestion plumbing.
- Therefore any claim that incomplete live batching is acceptable because “live is sync-only” is **not supported by the current codebase**.

---

## Recommendation

**Add explicit guardrails so live provider cannot be used in user-facing PR metrics paths.**

This recommendation is grounded in the current code usage:

- provider-driven PR endpoints are user-facing
- those endpoints can still resolve to `LivePullRequestReadProvider`
- live selection happens under concrete fallback conditions that are present in code today
- sync/ingestion does not depend on `LivePullRequestReadProvider`, so tightening request-path guardrails would not break the cache-population pipeline

Why this is the best fit:

- **Do not accept current partial batching as “good enough because live is sync-only”** — that premise is false in the current implementation.
- **No need to redesign everything immediately** — the architecture already distinguishes cache-table handlers from provider-driven handlers.
- **Guardrails are the most direct next step** — if PR metrics and similar workspace endpoints must be cache-backed, enforce that in production as well, or block/short-circuit those requests when no valid cache exists.

If guardrails are not added, then the alternative conclusion is that the live path must be treated as a real user-facing performance path and optimized accordingly.

---

## CI / Build Notes

Recent GitHub Actions inspection found a failed workflow run:

- run id: `23695490187`
- workflow: `Running Copilot coding agent`
- job id: `69030008182`

Run metadata was retrievable, but failed-job log content retrieval returned `HTTP 404`, so no reliable failure-cause analysis could be extracted from the log artifact.

---

## Local Verification Notes

Local verification performed successfully with:

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~GetPullRequestMetricsQueryHandlerTests|FullyQualifiedName~GetPullRequestInsightsQueryHandlerTests|FullyQualifiedName~GetPrDeliveryInsightsQueryHandlerTests|FullyQualifiedName~PullRequestFilterResolutionServiceTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Focused local result:

- **Build succeeded**
- **71 targeted tests passed**
