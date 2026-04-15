# Homepage unavailable signals analysis

## Root cause

The `/home` regression is a combination of two issues:

1. **Homepage metrics handling still collapsed cache-backed data-state into a generic failure.**
   - `HomePage.razor` called `HomeProductBarMetricsService.GetAsync`.
   - `HomeProductBarMetricsService` used `GetDataOrDefault()`, so any cache-backed `NotReady`, `Failed`, or `Empty` envelope from `/api/metrics/home-product-bar` became `null`.
   - `HomePage` treated that `null` as `Context metrics unavailable.` even when the backend had a more precise reason such as “Cache has not been built for the active profile yet.”

2. **The Home delivery tile still used a legacy single-product assumption.**
   - `WorkspaceSignalService.GetDeliverySignalAsync` derived current sprints for every scoped team, then called `GET /api/metrics/sprint-execution` with `productId = null` when Home was on its default all-products view.
   - `MetricsController.GetSprintExecution` resolves that request through `SprintFilterResolutionService`, which forwards `RequireExplicitProductScope: true` into `ContextResolver`.
   - `ContextResolver` rejects missing explicit product scope for that query, producing a 400 response.
   - The generated client surfaced that 400 as `ApiException`, which bubbled back to `HomePage.LoadWorkspaceSignalAsync`, and Home rendered `Signal unavailable.`.

## Exact failing paths

### `Context metrics unavailable.`

`/home` → `PoTool.Client/Pages/Home/HomePage.razor` (`LoadProductBarMetricsAsync`)  
→ `PoTool.Client/Services/HomeProductBarMetricsService.cs` (`GetAsync`)  
→ `IMetricsClient.GetHomeProductBarMetricsAsync`  
→ `PoTool.Api/Controllers/MetricsController.cs` (`GetHomeProductBarMetrics`)  
→ `DeliveryFilterResolutionService.ResolveAsync`  
→ `GetHomeProductBarMetricsQueryHandler`

Failure mode before the fix:
- cache-backed `NotReady` / `Failed` / `Empty` envelope
- client collapsed it to `null`
- page converted `null` to generic unavailable text

### `Signal unavailable.`

`/home` → `PoTool.Client/Pages/Home/HomePage.razor` (`LoadWorkspaceSignalsAsync`)  
→ `PoTool.Client/Services/WorkspaceSignalService.cs` (`GetDeliverySignalAsync`)  
→ `LoadCurrentSprintsAsync`  
→ `LoadDeliveryContextsAsync`  
→ `IMetricsClient.GetSprintExecutionAsync(productId: null)`  
→ `PoTool.Api/Controllers/MetricsController.cs` (`GetSprintExecution`)  
→ `SprintFilterResolutionService.ResolveAsync`  
→ `ContextResolver.ResolveAsync`

Failure mode before the fix:
- Home all-products scope sent no explicit product
- sprint execution endpoint requires explicit product scope
- backend returned validation failure / 400
- client threw `ApiException`
- Home converted the exception into `Signal unavailable.`

## `/home` shared-filter migration status

`/home` is **only partially migrated** to the shared filter model.

What is migrated:
- Home resolves its page scope through `GlobalFilterStore`.
- Home subscribes to shared filter changes.
- Product chip selection writes back through the shared store and route service.

What was still legacy:
- Home delivery-signal loading assumed one optional `productId` scalar instead of honoring effective scoped products end-to-end.
- Home product metrics handling still treated cache-backed state as `null` rather than preserving requested/effective filter diagnostics and data-state reasons.

## Homepage-specific or systemic?

- **Homepage-specific:** the broken delivery tile request path was specific to Home’s workspace signal aggregation logic.
- **Shared/provider-level:** the bad request originated in `WorkspaceSignalService`, which is the shared signal provider used by Home.
- **Fallback/empty-state issue:** the generic product-bar failure text came from Home’s client-side handling of cache-backed data-state, not from the backend contract itself.

So the issue is **a combination of homepage orchestration and shared signal-provider behavior**, not a backend contract drift.

## Contract verification

I checked the UI/client/server contract chain for:
- `home-product-bar`
- `sprint-execution`
- `backlog-health`
- `sprint-trend`
- `pull request sprint-trends`
- `capacity-calibration`
- `validation-triage`

The generated clients and controller signatures still compile and line up with the wrapped `DataStateResponseDto<T>` contracts. The failure was not a compile-time DTO mismatch; it was the **semantic handling of valid cache-backed states and an invalid sprint-execution query combination**.

## Local/mock/test data expectations

Without TFS, Home is expected to work from:
- cached local data,
- mock-seeded local data,
- or explicit cache-not-ready / empty states.

It should **not** report a generic broken state when the backend is intentionally returning:
- `NotReady`
- `Failed`
- `Empty`

The prior product-bar behavior misreported those states. The delivery tile behavior also misreported an invalid all-products query path as a generic signal failure.

## What changed

1. **Home product bar metrics now preserve cache/data-state reasons**
   - `HomeProductBarMetricsService` now returns a structured result with `DataStateDto`, data, filter metadata, and reason.
   - `HomePage` now shows the returned reason when metrics are not available instead of always showing `Context metrics unavailable.`.

2. **Home delivery signal now uses explicit product scope**
   - `WorkspaceSignalService` now loads sprint execution/backlog health per compatible product for each current sprint.
   - It no longer calls sprint execution with `productId = null` from the Home all-products view.
   - Invalid/404 per-product signal contexts are skipped instead of collapsing the whole tile into `Signal unavailable.`.

3. **Tests added**
   - `HomeProductBarMetricsServiceTests`
   - `WorkspaceSignalServiceTests` coverage for explicit product scoping from Home all-products context

## What was not changed

- No TFS/live-system behavior was assumed or validated.
- No server-side filter contracts were changed.
- No homepage navigation/query-string model changes were made.
- No workspace tile copy or visual hierarchy was redesigned beyond the corrected failure reporting behavior.

## Remaining risks

- `/home` still does not use the richer page-level `DataStateViewModel` pattern used by some deeper workspace pages, so it remains lighter-weight than the dedicated workspaces.
- Delivery signal aggregation still depends on local sprint/product-team link correctness; stale links can suppress a product-specific context instead of surfacing a richer canonical notice.
- Health, Trends, and Planning tiles still use neutral fallback text when their underlying cache-backed queries return no usable data; that is less misleading than the prior error path, but it is not yet a full shared Home data-state UX model.

## How to verify locally without TFS

1. Build and run targeted tests:
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~HomeProductBarMetricsServiceTests|FullyQualifiedName~GetHomeProductBarMetricsQueryHandlerTests|FullyQualifiedName~MetricsControllerDeliveryCanonicalFilterTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~PageFilterExecutionGateTests|FullyQualifiedName~CacheReadinessStateServiceTests|FullyQualifiedName~GlobalFilterArchitectureAuditTests|FullyQualifiedName~PageContextContractEnforcementAuditTests" --logger "console;verbosity=minimal"`

2. Mock/not-ready verification by code path:
   - `HomeProductBarMetricsServiceTests.GetAsync_WhenCacheIsNotReady_PreservesReasonInsteadOfReturningGenericFailure`

3. Delivery-signal verification by code path:
   - `WorkspaceSignalServiceTests.GetDeliverySignalAsync_WithAllProducts_UsesExplicitProductScopeForSprintExecution`

4. Manual local verification in mock mode:
   - open `/home`
   - confirm the product bar surfaces a concrete cache/data-state reason instead of generic unavailable text when cache is not ready
   - confirm the Delivery tile no longer falls into `Signal unavailable.` solely because Home is on all-products scope
