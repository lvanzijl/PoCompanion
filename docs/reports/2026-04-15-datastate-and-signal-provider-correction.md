# DataState and signal provider correction

## Root cause summary

Two shared client-layer patterns were causing misleading `/home` failures and similar silent degradations elsewhere:

1. **Cache-backed envelopes were collapsed into payload-or-null semantics**
   - `GetDataOrDefault()` and equivalent wrappers converted `NotReady`, `Empty`, and `Failed` into `null`, empty collections, or default DTOs.
   - This dropped backend reasons and canonical filter diagnostics.

2. **Signal providers mixed requested scope, locally normalized scope, and endpoint-specific scope rules**
   - The delivery signal fan-out used a nullable `productId` path for sprint execution even though the backend requires explicit product scope.
   - Client code also resolved product selection locally in a way that could hide invalid requested scope before any backend normalization or user-visible diagnostic.

## Affected services and pages

### Services that were collapsing DataState

- `PoTool.Client/Services/HomeProductBarMetricsService.cs`
- `PoTool.Client/Services/SprintDeliveryMetricsService.cs`
- `PoTool.Client/Services/PipelineService.cs`
- `PoTool.Client/Services/PullRequestService.cs`
- `PoTool.Client/Services/WorkspaceSignalService.cs`
- `PoTool.Client/Services/WorkItemService.cs` (signal-related result paths added)

### Direct `GetDataOrDefault()` / payload-only call sites identified during audit

- `PoTool.Client/Services/WorkspaceSignalService.cs`
- `PoTool.Client/Services/SprintDeliveryMetricsService.cs`
- `PoTool.Client/Services/PipelineService.cs`
- `PoTool.Client/Services/PullRequestService.cs`
- `PoTool.Client/Services/HomeProductBarMetricsService.cs`
- `PoTool.Client/Services/WorkItemService.cs`

### Pages/components updated to consume preserved state

- `PoTool.Client/Pages/Home/HomePage.razor`
- `PoTool.Client/Pages/Home/SprintTrend.razor`
- `PoTool.Client/Components/Flow/FlowPanel.razor`

## Before vs after behavior

### Before

- Cache-backed wrapper services often returned:
  - `T?`
  - empty collections
  - default DTO payloads
- Reasons like:
  - cache not ready
  - no data in scope
  - canonical filter correction
  were discarded.
- Home delivery signals could still reach sprint execution with invalid scope combinations and then react after the backend rejected the request.

### After

- Shared client wrappers now return `DataStateResult<T>`.
- `DataStateResult<T>` preserves:
  - explicit state
  - reason
  - retry hint
  - canonical filter metadata
  - invalid fields / validation messages
- Home and shared signal providers now:
  - derive effective local product scope explicitly
  - reject invalid requested product scope before sprint-scoped calls
  - aggregate over effective products for all-products signal flows
  - keep non-ready / empty / failed states explicit instead of translating them into generic unavailable text

## DataState handling changes

### Introduced

- `PoTool.Client/Models/DataStateResult.cs`
- `PoTool.Client/Helpers/DataStateResultFactory.cs`

### Result model

`DataStateResult<T>` now carries:

- `Status`
- `State`
- `Data`
- `Reason`
- `RetryAfterSeconds`
- canonical filter metadata list
- derived:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
  - `CanUseData`

### UI mapping

`CacheStatePresentation` now has an `Invalid` UI state so shared UI helpers can represent filter-correction failures distinctly from cache-not-ready or hard failures.

## Signal provider changes

### Delivery

- Delivery signal scope now uses effective product scope derived from the current filter state plus available products.
- Invalid requested product scope is rejected locally and surfaced as `Invalid` instead of falling through to a backend 400.
- All-products delivery now fans out over explicit `(sprint, product)` contexts only.

### Trends

- Trends signal now keeps separate state for sprint and PR trend reads.
- If at least one source is ready, the tile can still compute a signal.
- If no source is ready, the result preserves whether the cause was `Empty`, `NotReady`, `Failed`, or `Invalid`.

### Planning

- Planning signal now preserves backlog-state and capacity-calibration data-state instead of silently treating missing data as neutral success.

### Health

- Health signal now consumes a preserved validation-triage result instead of a nullable summary DTO.

## Filter usage corrections

- `GlobalProductSelectionHelper` now exposes shared effective-scope resolution via `ProductScopeResolution`.
- Home signal loading now passes the current shared `FilterState` instead of a pre-normalized nullable selected product.
- Home product-bar loading now uses the requested product id from shared filter state rather than only the client-side single-product shortcut.
- Home navigation query building now preserves the requested product id instead of silently dropping invalid requested scope from the URL.

## Risks and limitations

- Some older client methods still exist for non-signal/non-home flows and still use payload-only convenience paths; this change corrected the shared signal and metrics root paths first.
- Home signal invalid-state handling is now explicit, but tile UX still remains intentionally compact and does not expose full canonical filter diff details the way deeper workspace pages do.
- The backend still owns canonical filter normalization for envelope-based endpoints; client-side signal scope validation is only used where the client must compose multiple explicit per-product requests locally.

## How to verify locally without TFS

1. Build:
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`

2. Run targeted tests:
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~DataStateResultFactoryTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~HomeProductBarMetricsServiceTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~GeneratedClientStateServiceTests|FullyQualifiedName~GeneratedCacheEnvelopeHelperTests|FullyQualifiedName~GeneratedDtoMappingHardeningAuditTests|FullyQualifiedName~GlobalFilterArchitectureAuditTests|FullyQualifiedName~PageContextContractEnforcementAuditTests" --logger \"console;verbosity=minimal\"`

3. Key checks:
   - `DataStateResultFactoryTests` verifies state/reason/filter metadata are preserved.
   - `HomeProductBarMetricsServiceTests` verifies cache-not-ready reasons are not collapsed to `null`.
   - `WorkspaceSignalServiceTests` verifies invalid product scope is rejected before backend calls and all-products delivery uses explicit per-product scope.
   - `PipelineServiceTests` verifies invalid product selection no longer appears as successful empty data.
