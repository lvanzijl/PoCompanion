# Cold Cache Normalization

## Pages audited

### Health

- `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- `PoTool.Client/Pages/Home/PipelineInsights.razor`

### Planning

- `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`

### Trend

- `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/PrOverview.razor`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`

### Delivery

- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/SprintTrend.razor`
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`

### Shared cold-cache surfaces

- `PoTool.Client/Components/Common/DataStatePanel.razor`
- `PoTool.Client/Components/Common/DataStateView.razor`
- `PoTool.Client/Pages/Home/Components/WorkspaceTileBadge.razor`
- `PoTool.Client/Services/TrendsWorkspaceTileSignalService.cs`

## Inconsistencies found

- Not-ready and failed states used many page-specific titles and messages for the same cold-cache condition.
- Several cache-backed page loaders caught exceptions locally, cleared data, and only showed snackbars or generic alerts, which masked canonical state handling.
- Workspace trend tiles mapped cold-cache conditions to generic `No data`, which made a cold cache indistinguishable from a valid empty result.
- Some pages had retry actions only on generic error alerts, not on canonical not-ready/failed state panels.
- The UI had no canonical mapping for `Loading`, `Ready`, `NotReady`, `Failed`, and `EmptyButValid`.

## Changes made

- Added canonical UI state mapping through `UiDataState` and `CacheStatePresentation`.
- Extended `DataStateViewModel<T>` with canonical UI-state mapping plus `Ready`, `Empty`, and `Failed` helpers.
- Standardized `DataStatePanel` so not-ready and failed states use one shared presentation pattern with:
  - canonical title/message generation
  - optional retry action
  - existing cache status section for not-ready states
- Updated `DataStateView` to pass through canonical subject/recovery behavior.
- Normalized audited health, planning, trend, and delivery pages to use shared subject/reason-based state panels instead of page-specific cold-cache wording.
- Replaced key cache-backed exception fallbacks in audited pages so failures set canonical failed state instead of silently clearing data and only showing snackbars.
- Added explicit workspace tile fallback states for:
  - `Data not ready`
  - `Data unavailable`
- Normalized additional cache-backed pages/components already using `DataStatePanel`:
  - `BugsTriage`
  - `ValidationQueuePage`
  - `ValidationTriagePage`
  - `ValidationFixPage`
  - `PortfolioCdcReadOnlyPanel`

## Remaining edge cases

- Some shared analytical components still pass custom `NotReadyTitle` or `FailedTitle` through `DataStateView`; they remain functional, but they were not all migrated in this pass.
- A few non-cache context loaders still use page-level alerts for invalid route/profile/setup conditions; those are intentionally distinct from cold-cache behavior.
- Parallel WebAssembly build/test runs can still hit transient `webcil` file-lock issues, so sequential validation remains the reliable path.
