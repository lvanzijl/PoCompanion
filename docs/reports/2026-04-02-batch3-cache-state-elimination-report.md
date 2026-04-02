# Batch 3 — Cache State Elimination Report

## 1. Problem Class Definition
The remaining direct-entry failures were not route-context bugs anymore. They were cache-contract bugs: valid pages called cache-backed APIs that treated cache readiness as an HTTP failure instead of a first-class data state. That forced the client into exception-driven rendering, so a normal valid route could only discover “cache not ready” by failing first and then showing recovery UI.

The core defect pattern was:
- page assumes cached data exists
- middleware blocks the request with HTTP 409 when cache has never synced
- client catches transport exceptions and maps them into fallback UX
- valid routes degrade into recovery panels or error alerts instead of deterministic state rendering

## 2. Inventory

### Cache Dependency Inventory
- `/home/bugs/detail`
  - API endpoint: `GET /api/workitems/{tfsId}`
  - Failure mode: middleware returned HTTP 409 when cache was unavailable
  - Current behavior before fix: valid bug IDs landed in recovery/error state instead of a deterministic loading/not-ready state

- `/bugs-triage`
  - API endpoint: `GET /api/workitems/validated`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: inline error alert from exception handling for a valid page load

- `/home/validation-triage`
  - API endpoint: `GET /api/workitems/validation-triage`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: raw exception text rendered via `ex.Message`

- `/home/validation-queue`
  - API endpoint: `GET /api/workitems/validation-queue`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: guided recovery used as the primary valid-route path

- `/home/validation-fix`
  - API endpoint: `GET /api/workitems/validation-fix`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: guided recovery used as the primary valid-route path

- `/home/delivery/sprint/activity/{workItemId}`
  - API endpoint: `GET /api/metrics/work-item-activity/{workItemId}`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: exception-driven error UI for a valid route

- `/home/portfolio-progress`
  - API endpoint: `GET /api/metrics/portfolio-progress-trend`
  - Failure mode: HTTP 409 on cold cache
  - Current behavior before fix: route loaded, then failed into error/retry handling

### Shared backend enforcement point
- `PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
  - cache-only analytical reads were blocked with `409 Conflict`
  - `ProblemDetails` payload title was `Cache not ready`

## 3. Root Cause
The architecture allowed cache readiness to live outside the endpoint contract.

Specifically:
- middleware knew whether cache was available
- controllers and DTO contracts did not
- client pages therefore had no structured state to render
- pages only learned about readiness by catching failed HTTP responses

This split produced an implicit, non-deterministic contract:
- “successful response means cache is ready”
- “transport failure might mean not ready, not found, or an actual error”

That made valid routes depend on hidden cache state instead of explicit data state.

## 4. Solution Design

### State model
Introduced shared explicit state contract:
- `NotRequested`
- `Loading`
- `Available`
- `Empty`
- `NotReady`
- `Failed`

### Backend design
- Added `PoTool.Shared/DataState/DataStateResponseDto.cs`
- Added `CacheReadinessStateService` to translate current profile + cache state into explicit `DataStateDto`
- Added `CacheStateResponseService` to wrap cache-backed loads into structured responses
- Added `CacheStateAwareRead` route intent so selected state endpoints run in cache mode without being blocked by middleware 409 handling
- Added state-aware endpoints for affected page loads:
  - `/api/workitems/state/{tfsId}`
  - `/api/workitems/state/validated`
  - `/api/workitems/state/validation-triage`
  - `/api/workitems/state/validation-queue`
  - `/api/workitems/state/validation-fix`
  - `/api/metrics/state/work-item-activity/{workItemId}`
  - `/api/metrics/state/portfolio-progress-trend`

### Client design
- Added `PoTool.Client/Models/DataStateViewModel.cs`
- Added `PoTool.Client/Components/Common/DataStatePanel.razor`
- Added `PoTool.Client/Services/MetricsStateService.cs`
- Extended `WorkItemService` with state-aware read methods
- Updated affected pages to render from explicit state instead of exception-to-UI mapping

### Behavioral rules now enforced
- valid route + cold cache → `NotReady`
- valid route + real data → `Available`
- valid route + valid empty result → `Empty`
- true request failure → `Failed`
- invalid context remains a route-level invalid-state/recovery path, not a cache-state path

## 5. Changes Made
- `PoTool.Shared/DataState/DataStateResponseDto.cs`
  - shared explicit data-state DTO

- `PoTool.Api/Services/CacheReadinessStateService.cs`
  - single source of truth for cache readiness state resolution

- `PoTool.Api/Services/CacheStateResponseService.cs`
  - shared execution wrapper returning `Available` / `Empty` / `NotReady` / `Failed`

- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
  - added `CacheStateAwareRead` routing classification

- `PoTool.Api/Middleware/DataSourceModeMiddleware.cs`
  - lets state-aware cache endpoints execute in cache mode instead of hard-failing with 409

- `PoTool.Api/Controllers/WorkItemsController.cs`
  - added state endpoints for bug detail, bugs triage, validation triage, queue, and fix session

- `PoTool.Api/Controllers/MetricsController.cs`
  - added state endpoints for sprint activity and portfolio progress trend

- `PoTool.Client/Services/WorkItemService.cs`
  - added state-aware work-item/validation methods

- `PoTool.Client/Services/MetricsStateService.cs`
  - added typed state-aware metrics reads

- `PoTool.Client/Models/DataStateViewModel.cs`
  - shared client load-state abstraction

- `PoTool.Client/Components/Common/DataStatePanel.razor`
  - shared non-error state renderer, including cache status for `NotReady`

- Updated pages:
  - `PoTool.Client/Pages/Home/BugDetail.razor`
  - `PoTool.Client/Pages/BugsTriage.razor`
  - `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`

- Regression coverage:
  - `PoTool.Tests.Unit/Services/CacheReadinessStateServiceTests.cs`
  - `PoTool.Tests.Unit/Services/CacheStateResponseServiceTests.cs`
  - updated `PoTool.Tests.Unit/Middleware/DataSourceModeMiddlewareTests.cs`
  - updated metrics controller tests for constructor dependency changes

## 6. Before vs After Behavior

### `/home/bugs/detail`
- Before: valid `bugId` could fail with cache-not-ready transport handling
- After: valid route now returns explicit `NotReady`, `Available`, or `Empty`; only invalid bug IDs use recovery guidance

### `/bugs-triage`
- Before: direct entry could land in exception-driven error alert
- After: page renders `NotReady` state while cache warms, and `Empty` only when there are truly no bugs

### `/home/validation-triage`
- Before: raw exception text on cache failure
- After: page renders `NotReady` as informative state, not as an error

### `/home/validation-queue`
- Before: valid route fell into recovery flow on cold cache
- After: valid route renders explicit `NotReady`; recovery remains only for missing/invalid route context

### `/home/validation-fix`
- Before: valid route fell into recovery flow on cold cache
- After: valid route renders explicit `NotReady`; recovery remains only for missing/invalid route context

### `/home/delivery/sprint/activity/{workItemId}`
- Before: valid route failed through API exception handling
- After: valid route renders `NotReady`, `Empty`, or `Available` deterministically

### `/home/portfolio-progress`
- Before: valid route could fail into error/retry handling on cold cache
- After: valid route renders `NotReady` as a normal informational state

## 7. Residual Risks
- The legacy middleware 409 behavior still exists for cache-only endpoints that have not been migrated to explicit state endpoints. The current fix eliminates the bug class for the identified direct-entry and exception-driven page flows, but it does not yet retrofit every cache-only analytical endpoint in the repository.
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor` still uses legacy exception formatting on portfolio history reads; that path was not part of the cache-only middleware routes fixed here.
- End-to-end browser validation against the local API could not be completed in this session because the mock API host failed during startup with an existing SQLite foreign-key seed failure in `MockConfigurationSeedHostedService`.

## 8. Confidence
**Medium**

Reasoning:
- High confidence in the implemented contract and targeted page behavior because:
  - the backend now exposes explicit state for all identified direct-entry cache-dependent page loads
  - focused regression tests passed for readiness-state resolution, response wrapping, middleware classification, and affected metrics controller construction
  - the solution builds cleanly
- Confidence is not High because:
  - full unit-suite execution still reports unrelated pre-existing failures in cached pipeline tests
  - local end-to-end API startup was blocked by an unrelated mock-seeding database failure
