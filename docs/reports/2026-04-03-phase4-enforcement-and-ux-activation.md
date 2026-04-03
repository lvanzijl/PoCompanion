# Phase 4 — Filter Enforcement and UX Activation

## Page contract enforcement

- `GlobalFilterPageCatalog` remains the source of page capability contracts.
- `PageFilterExecutionGate` now evaluates the active page state from `GlobalFilterStore.CurrentUsage` and decides whether queries may run.
- Participating pages now stop query execution when the active page state is unresolved or invalid:
  - `SprintExecution`
  - `PortfolioDelivery`
  - `PortfolioProgressPage`
  - `PipelineInsights`
  - `PrOverview`
  - `DeliveryTrends`
  - `BugOverview`
  - `TrendsWorkspace`
  - `SprintTrend`
- Unsupported dimensions are preserved globally but surfaced as “not applied on this page” through the shared filter UX.

## Unresolved and invalid UX

- The filter summary remains visible at all times.
- `GlobalFilterControls` now shows explicit inline alerts for:
  - unresolved state
  - invalid state
  - dimensions that remain active globally but are not applied on the current page
- Unresolved alerts explain what is missing and include a direct “Open filters” action.
- Invalid route/state corrections now use `GlobalFilterCorrectionService` and surface a non-modal correction message through `GlobalFilterUiState`.

## Interactive filter UI

- Implemented a shared, collapsible filter surface in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/GlobalFilterControls.razor`.
- Summary behavior:
  - compact summary bar always visible
  - explicit expand/collapse action in `FilterSummaryBar`
  - source, status, and normalization chips remain visible when collapsed
- Controls implemented:
  - Product selector with explicit all/reset
  - Project selector using canonical `ProjectId` internally
  - Team selector with explicit reset to none
  - Time mode selector when the page contract allows more than one mode
  - Mode-specific time editors for sprint, range, and rolling windows
- Reset behavior:
  - per-dimension reset buttons
  - global reset-all button

## Route correction

- Added `GlobalFilterRouteService` as the centralized route emission path for shared filter interactions.
- Route generation now carries canonical query context including:
  - `projectId`
  - `timeMode`
  - `rollingWindow`
  - `rollingUnit`
- Planning routes now emit alias-shaped paths from canonical project identity through centralized alias resolution.
- `MainLayout` now runs route observation plus invalid-state correction through `GlobalFilterCorrectionService`.

## Query gating

- Added `PageFilterExecutionGate`.
- Pages now check the gate before calling data services.
- When the gate blocks:
  - page shell still renders
  - page data is cleared or left unloaded
  - no query is executed until the filter state becomes valid for the page

## DeliveryTrends decision

- Kept the existing end-sprint + count UI.
- The page now projects that UI onto the canonical inclusive range state in one explicit path.
- Rationale:
  - avoids a larger page UX redesign in this phase
  - preserves the current analytical workflow
  - keeps canonical store semantics authoritative

## Verification

### Build
- `dotnet build PoTool.sln --configuration Release --nologo` ✅

### Tests
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~PageFilterExecutionGateTests|FullyQualifiedName~GlobalFilterRouteServiceTests"` ✅
- `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"` ✅

### Targeted navigation/state verification
- direct route link: covered by `GlobalFilterStoreTests` project alias and route conflict cases ✅
- direct query link: covered by range and rolling query parsing tests ✅
- route + query conflict: covered by route-alias-over-query resolution test ✅
- refresh-equivalent route suppression: covered by route signature / pending signature tests ✅
- centralized planning alias emission: covered by `GlobalFilterRouteServiceTests` ✅
- unresolved / invalid gating: covered by `PageFilterExecutionGateTests` and page query guards ✅

## Remaining risks

- Existing page-local analytical controls still coexist with the shared global filter surface; they no longer own filter truth, but the UX is not fully consolidated yet.
- Planning pages still contain some local navigation helpers outside the new shared filter surface, even though centralized planning alias emission is now used for the new shared route path.

READY for Phase 5 once planning and analytical pages complete the remaining UI consolidation onto the shared filter surface.
