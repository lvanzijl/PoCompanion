# Global data-state unification

## Audit results

### Fully compliant
- Trend pages already using the hardened state model through `TrendDataStateView`: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BugOverview.razor`, `DeliveryTrends.razor`, `PipelineInsights.razor`, `PortfolioDelivery.razor`, `PortfolioProgressPage.razor`, `PrDeliveryInsights.razor`, `PrOverview.razor`, `SprintTrendActivity.razor`, `TrendsWorkspace.razor`
- Shared panels already using `DataStateView`: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`, `BacklogHealthPanel.razor`, `ForecastPanel.razor`, `EffortDistributionPanel.razor`, `AddLaneDialog.razor`

### Partially compliant
- Pages with `DataStateViewModel<T>` but legacy render branches before this change: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/BugsTriage.razor`, `ValidationQueuePage.razor`, `ValidationTriagePage.razor`, `ProjectPlanningOverview.razor`, `HealthOverviewPage.razor`, `PlanBoard.razor`, `SprintExecution.razor`, `ProductRoadmaps.razor`, `SprintTrend.razor`
- Components/pages still holding internal `NotReady` checks or ad-hoc null/error gates after the migration slice: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ValidationFixPage.razor`, `Components/PortfolioCdcReadOnlyPanel.razor`, `SprintTrend.razor`, `SprintExecution.razor`, `TrendsWorkspace.razor`

### Legacy
- Remaining page-local loading/error/empty flows still outside the canonical renderer include `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor`, `HomeChanges.razor`, `MultiProductPlanning.razor`, `BacklogOverviewPage.razor`, `ProductRoadmapEditor.razor`, `OnboardingWorkspace.razor`, `DeliveryWorkspace.razor`, `HealthWorkspace.razor`, `PlanningWorkspace.razor`

## Canonical state model

Rendered UI states are governed as:
- `Loading`
- `Success`
- `Empty`
- `InvalidFilter`
- `Error`

Rules:
- `NotReady` is not a rendered UI state
- `NotReady` is normalized to the governed loading path
- backend filter corrections with invalid fields map to `InvalidFilter`
- cache/backend failures and unavailable reads map to `Error`
- valid zero-result responses map to `Empty`
- pages should render data only from `DataStateViewModel<T>`

## Refactored components

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/CanonicalDataStateView.razor`
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/TrendDataStateView.razor` to delegate to the canonical wrapper
- Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStateView.razor` so cache readiness stays inside the governed loading surface
- Reduced `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStatePanel.razor` to an internal display primitive only
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/CacheBackedDataStateViewModelFactory.cs` to normalize cache-backed envelopes into `DataStateViewModel<T>`

## Migration summary by page group

### Completed in this slice
- Planning/health pages: `ProjectPlanningOverview.razor`, `HealthOverviewPage.razor`
- Validation/bug pages: `ValidationQueuePage.razor`, `ValidationTriagePage.razor`, `BugsTriage.razor`
- Shared detail page: `SprintTrendActivity.razor`
- Shared fallback usage removal: `PlanBoard.razor`, `ProductRoadmaps.razor`, `SprintExecution.razor`, `SprintTrend.razor`, `TrendsWorkspace.razor`

### Remaining migration work
- Full canonicalization of `ValidationFixPage.razor` and `PortfolioCdcReadOnlyPanel.razor`
- Removal of residual ad-hoc loading/error/null rendering from older workspace and editor pages
- Removal of remaining internal page branches that still key directly on `DataStateDto.NotReady`

## NotReady isolation strategy

- Keep `NotReady` only in cache/service normalization layers such as `CacheBackedClientResult<T>` and data-envelope helpers
- Convert cache-backed results into `DataStateViewModel<T>` before UI rendering
- Use `UiDataState.Loading` as the only rendered representation for not-ready responses
- Block new direct `DataStatePanel` usage so `NotReady` cannot leak back into page markup

## Validation approach and results

### Implemented safeguards
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DataStateGovernanceAuditTests.cs` to block direct `DataStatePanel` usage in pages
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Helpers/CacheBackedDataStateViewModelFactoryTests.cs` to verify NotReady, Empty, Error, and InvalidFilter normalization

### Local validation
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo` succeeded after the current migration slice
- Further unit-test validation is still required after the remaining page migrations land

## Remaining risks and technical debt

- The application is not fully migrated; several legacy workspace/editor pages still use ad-hoc UI state handling
- Some pages still branch on `DataStateDto.NotReady` internally even though rendered fallback panels were reduced
- End-to-end visual consistency is improved for migrated pages but not yet uniform across the entire app
- Additional governance tests are still needed if the repository wants to forbid direct null/empty rendering patterns globally
