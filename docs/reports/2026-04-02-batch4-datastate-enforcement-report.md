# Batch 4 — DataState Enforcement Report

## 1. Non-compliant endpoints removed
Batch 3 left parallel cache contracts in place: canonical endpoints still returned raw DTOs while a smaller `/state/*` route set returned `DataStateResponseDto<T>`.

Batch 4 removed that dual-path design:

- removed legacy state-only routes from:
  - `PoTool.Api/Controllers/WorkItemsController.cs`
    - `/api/workitems/state/validated`
    - `/api/workitems/state/validation-triage`
    - `/api/workitems/state/validation-queue`
    - `/api/workitems/state/validation-fix`
    - `/api/workitems/state/{tfsId}`
  - `PoTool.Api/Controllers/MetricsController.cs`
    - `/api/metrics/state/portfolio-progress-trend`
    - `/api/metrics/state/work-item-activity/{workItemId}`

- enforced `DataStateResponseDto<T>` on canonical cache-backed routes instead:
  - all cache-classified `api/workitems/**`
  - all cache-classified `api/metrics/**`
  - all cache-classified `api/pullrequests/**`
  - all cache-classified `api/pipelines/**`
  - all cache-classified `api/filtering/**`
  - cache-classified portfolio reads:
    - `/api/portfolio/progress`
    - `/api/portfolio/snapshots`
    - `/api/portfolio/comparison`
    - `/api/portfolio/trends`
    - `/api/portfolio/signals`

## 2. Middleware changes
`PoTool.Api/Middleware/DataSourceModeMiddleware.cs` no longer blocks cache-backed requests when cache is missing or cold.

Changed behavior:
- before:
  - cache-backed route + no ready cache → middleware returned `409 Conflict`
  - response title `Cache not ready`
- after:
  - middleware only selects `DataSourceMode.Cache` vs `DataSourceMode.Live`
  - readiness is no longer surfaced as middleware transport failure
  - cache-backed requests continue to the DataState contract layer

Also:
- removed `CacheStateAwareRead` route intent from `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`
- reclassified canonical `/api/portfolio/*` analytical reads as cache-backed exact routes

## 3. Enforcement mechanism
The exclusive contract is now enforced by a global API filter:

- added `PoTool.Api/Filters/CacheBackedDataStateContractFilter.cs`

It enforces:
- cache-backed route + cache warming/missing → `200 OK` with `DataStateDto.NotReady`
- cache-backed route + handler failure → `200 OK` with `DataStateDto.Failed`
- cache-backed route + null/not found/no content → `200 OK` with `DataStateDto.Empty`
- cache-backed route + payload → `200 OK` with `DataStateDto.Available`

Supporting enforcement changes:
- `EnforceSharedDtoActionResultContractFilter` now expects `DataStateResponseDto<T>` for cache-backed routes
- `DataSourceModeConfiguration` exact-route rules now cover portfolio cache reads

Regression lock added:
- `PoTool.Tests.Unit/Audits/CacheBackedDataStateContractAuditTests.cs`
  - fails if legacy `/state/` routes reappear
  - fails if cache-backed endpoints do not resolve to `DataStateResponseDto<T>`

## 4. Client cleanup
Updated client consumers to read the canonical DataState contract instead of relying on middleware 409:

- `PoTool.Client/Services/WorkItemService.cs`
  - state methods now call canonical routes rather than removed `/state/*` routes

- `PoTool.Client/Services/MetricsStateService.cs`
  - expanded to cover additional canonical metrics and portfolio DataState reads

- added:
  - `PoTool.Client/Services/PullRequestStateService.cs`
  - `PoTool.Client/Services/PipelineStateService.cs`

- updated pages/components:
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
  - `PoTool.Client/Pages/Home/PrOverview.razor`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor`
  - `PoTool.Client/Pages/Home/DeliveryTrends.razor`
  - `PoTool.Client/Pages/Home/SprintExecution.razor`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor`

Result:
- removed remaining cache-readiness `ApiErrorMessageFormatter` usage
- removed server-side `/state/*` route dependency from the client
- converted several major analytical pages to explicit `NotReady` / `Failed` rendering

## 5. Before vs After (system-wide)
### Before
- cache-backed controllers exposed two contracts:
  - raw canonical DTO/envelope route
  - explicit `/state/*` route
- middleware could still terminate normal flows with `409 Conflict`
- client pages mixed:
  - exception/status-code handling
  - state-based rendering

### After
- one server contract for cache-backed reads: `DataStateResponseDto<T>`
- no cache-readiness `409 Conflict` from middleware
- canonical routes are the only cache-backed read routes
- compile-/test-level audit protects against reintroducing raw cache-backed contracts

## 6. Remaining violations (must be empty or justified)
Not empty.

Remaining client-side cache-backed consumers that still call raw cache-backed endpoints and therefore still need DataState-first rendering migration:

- `PoTool.Client/Components/Forecast/ForecastPanel.razor`
  - `MetricsClient.GetEpicForecastAsync`
- `PoTool.Client/Components/BacklogHealth/BacklogHealthPanel.razor`
  - `MetricsClient.GetMultiIterationBacklogHealthAsync`
- `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor`
  - `MetricsClient.GetEffortDistributionAsync`
- `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
  - `MetricsClient.GetCapacityCalibrationEnvelopeAsync`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `MetricsClient.GetCapacityCalibrationEnvelopeAsync`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
  - `PullRequestsClient.GetSprintTrendsEnvelopeAsync`
- `PoTool.Client/Pages/Home/BugOverview.razor`
  - `WorkItemService.GetAllAsync`
- `PoTool.Client/Components/ReleasePlanning/AddLaneDialog.razor`
  - `WorkItemService.GetAllAsync`

These are explicit violations because they still depend on raw cache-backed client calls after the server contract was made exclusive. They are not hidden: they are the remaining migration backlog to complete true end-to-end exclusivity on the client.

Non-violations intentionally left unchanged:
- cache sync write endpoints still use conflict responses for write coordination, not cache-readiness rendering
- non-cache live setup/discovery flows that use 404 handling remain outside this batch’s contract scope

## 7. Confidence
**Medium**

Reasoning:
- high confidence in the server-side enforcement because:
  - middleware no longer emits cache-readiness 409s
  - legacy `/state/*` analytical routes were removed
  - global contract filter now normalizes cache-backed responses to `DataStateResponseDto<T>`
  - new audit tests verify the contract shape
- medium overall confidence because:
  - several client cache-backed consumers still need migration to consume the exclusive DataState contract directly
  - the work is structurally correct but not yet fully propagated through every remaining cache-backed UI consumer
