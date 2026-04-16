# Data-state global enforcement report

## Scope
- User-approved scope: **option B**
- Enforcement target: **data-query/read surfaces only**
- Excluded from this migration: transient mutation flows, CRUD/forms, dialogs, onboarding write/action zones, and other operational status UI

## Initial inventory of remaining legacy patterns

### Pages
- **Null-driven rendering**
  - `PoTool.Client/Pages/BugsTriage.razor`
  - `PoTool.Client/Pages/Home/Components/CanonicalPlanningQuality.razor`
  - `PoTool.Client/Pages/Home/Components/PipelineBreakdownTable.razor`
  - `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
  - `PoTool.Client/Pages/Home/Components/TrendChart.razor`
  - `PoTool.Client/Pages/Home/Components/TrendMultiSeriesChart.razor`
  - `PoTool.Client/Pages/Home/Components/WorkspaceTileBadge.razor`
  - `PoTool.Client/Pages/Home/DeliveryTrends.razor`
  - `PoTool.Client/Pages/Home/HomePage.razor`
  - `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
  - `PoTool.Client/Pages/Home/OnboardingWorkspace.razor`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor`
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - `PoTool.Client/Pages/Home/PrOverview.razor`
  - `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
  - `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - `PoTool.Client/Pages/Home/SprintExecution.razor`
  - `PoTool.Client/Pages/Home/SprintTrend.razor`
  - `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
  - `PoTool.Client/Pages/ProfilesHome.razor`
  - `PoTool.Client/Pages/PullRequests/SubComponents/PRDateRangeFilter.razor`
  - `PoTool.Client/Pages/Settings/EditProductOwner.razor`
  - `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
  - `PoTool.Client/Pages/Settings/ManageTeams.razor`
  - `PoTool.Client/Pages/TfsConfig.razor`
- **Manual loading flags / inline loading**
  - `PoTool.Client/Pages/BugsTriage.razor`
  - `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - `PoTool.Client/Pages/Home/BugOverview.razor`
  - `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
  - `PoTool.Client/Pages/Home/DeliveryTrends.razor`
  - `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
  - `PoTool.Client/Pages/Home/HomeChanges.razor`
  - `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor`
  - `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
  - `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - `PoTool.Client/Pages/Home/PrOverview.razor`
  - `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
  - `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - `PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`
  - `PoTool.Client/Pages/Home/SprintExecution.razor`
  - `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
  - `PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor`
  - `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - `PoTool.Client/Pages/Home/ValidationTriagePage.razor`
  - `PoTool.Client/Pages/ProfilesHome.razor`
  - `PoTool.Client/Pages/Settings/EditProductOwner.razor`
  - `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
  - `PoTool.Client/Pages/Settings/ManageTeams.razor`
  - `PoTool.Client/Pages/Settings/WorkItemStates.razor`
  - `PoTool.Client/Pages/SyncGate.razor`
  - `PoTool.Client/Pages/TfsConfig.razor`

### Components
- **Null-driven rendering**
  - `PoTool.Client/Components/Charts/PullRequestDeliveryScatterSvg.razor`
  - `PoTool.Client/Components/Charts/PullRequestScatterSvg.razor`
  - `PoTool.Client/Components/Charts/TimeScatterSvg.razor`
  - `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor`
  - `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor`
  - `PoTool.Client/Components/Common/Compact/CompactTable.razor`
  - `PoTool.Client/Components/Common/EmptyStateDisplay.razor`
  - `PoTool.Client/Components/Common/ErrorDisplay.razor`
  - `PoTool.Client/Components/Common/FilterSummaryBar.razor`
  - `PoTool.Client/Components/Common/GlobalFilterControls.razor`
  - `PoTool.Client/Components/Common/NavigationTileCard.razor`
  - `PoTool.Client/Components/Common/PageHelp.razor`
  - `PoTool.Client/Components/Common/WorkItemLink.razor`
  - `PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
  - `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor`
  - `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
  - `PoTool.Client/Components/Onboarding/OnboardingEntityCard.razor`
  - `PoTool.Client/Components/Onboarding/OnboardingFutureActionZone.razor`
  - `PoTool.Client/Components/Onboarding/OnboardingMutationActionZone.razor`
  - `PoTool.Client/Components/Onboarding/OnboardingWizard.razor`
  - `PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
  - `PoTool.Client/Components/ReleasePlanning/UnplannedEpicsDialog.razor`
  - `PoTool.Client/Components/ReleasePlanning/UnplannedEpicsList.razor`
  - `PoTool.Client/Components/Settings/CacheManagementSection.razor`
  - `PoTool.Client/Components/Settings/InlineTeamCreationDialog.razor`
  - `PoTool.Client/Components/Settings/ProductEditor.razor`
  - `PoTool.Client/Components/Settings/ProfileSelector.razor`
  - `PoTool.Client/Components/Settings/TeamEditor.razor`
  - `PoTool.Client/Components/Settings/TfsTeamPickerDialog.razor`
  - `PoTool.Client/Components/Timeline/TimelinePanel.razor`
  - `PoTool.Client/Components/WorkItems/SubComponents/ValidationHistoryPanel.razor`
  - `PoTool.Client/Components/WorkItems/SubComponents/ValidationSummaryPanel.razor`
  - `PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor`
- **Manual loading flags / inline loading**
  - `PoTool.Client/Components/BacklogHealth/BacklogHealthPanel.razor`
  - `PoTool.Client/Components/Common/CacheStatusSection.razor`
  - `PoTool.Client/Components/Common/ReleaseNotesDialog.razor`
  - `PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
  - `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor`
  - `PoTool.Client/Components/Flow/FlowPanel.razor`
  - `PoTool.Client/Components/Forecast/ForecastPanel.razor`
  - `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
  - `PoTool.Client/Components/ReleasePlanning/AddLaneDialog.razor`
  - `PoTool.Client/Components/ReleasePlanning/ObjectiveEpicsDialog.razor`
  - `PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
  - `PoTool.Client/Components/ReleasePlanning/SplitEpicDialog.razor`
  - `PoTool.Client/Components/Settings/ProfileSelector.razor`
  - `PoTool.Client/Components/Settings/TriageTagsSection.razor`
  - `PoTool.Client/Components/Timeline/TimelinePanel.razor`
  - `PoTool.Client/Components/WorkItems/SubComponents/ValidationHistoryPanel.razor`
  - `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`

## Governed read-surface migration completed in this change

### Pages migrated
- `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - Removed `_isLoading`, `_loadError`, `_state` render branching
  - Replaced direct `MudAlert`/`LoadingIndicator` fallback path with `DataStateViewModel<ProductBacklogStateDto>` + `CanonicalDataStateView`
  - Context-product validation now maps to canonical `InvalidFilter`
- `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
  - Removed page-level loading/error branching
  - Added canonical `_planningState` for loading, empty, success, and failure rendering
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
  - Removed `_isLoading`, `_errorMessage`, `_details` state inference
  - Collapsed envelope-to-view-state mapping into one canonical `_activityState`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
  - Removed direct context-error/loading/null-summary page branches
  - Added canonical summary state and canonical board state wrapper for the read path

### Components migrated
- `PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
  - Removed manual loading/error rendering and raw graph null checks
  - Added canonical `_graphState` with `CanonicalDataStateView`
- `PoTool.Client/Components/Timeline/TimelinePanel.razor`
  - Removed manual loading/error rendering and raw timeline null checks
  - Added canonical `_timelineState` with `CanonicalDataStateView`

## Legacy patterns removed from governed read surfaces
- Manual `_isLoading` render gates
- Page/component-local `_loadError` / `_errorMessage` fallback branches
- Direct `LoadingIndicator` use in governed read surfaces
- Direct `ErrorDisplay` use in governed read surfaces
- Null-driven read rendering branches for the migrated surfaces

## Enforcement rules added
- Extended `PoTool.Tests.Unit/Audits/DataStateGovernanceAuditTests.cs`
- Added governed read-surface coverage for:
  - `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
  - `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
  - `PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
  - `PoTool.Client/Components/Timeline/TimelinePanel.razor`
- New audit failures now block, on governed read surfaces:
  - null-driven render branches
  - manual loading flags in the rendering path
  - direct `LoadingIndicator`
  - direct `ErrorDisplay`
  - direct `Severity.Error` fallback alerts

## Governance coverage result
- `DataStatePanel_IsOnlyUsedByTheGovernedDataStateView` ✅
- `ClientPages_DoNotRenderDataStatePanelDirectly` ✅
- `UiLayers_DoNotReferenceNotReadyStates` ✅
- `GovernedRendererFiles_DoNotUseStandaloneLegacyStateDisplays` ✅
- `GovernedReadSurfaces_DoNotUseManualLoadingFlagsOrLegacyErrorDisplays` ✅
- `GovernedReadSurfaces_DoNotUseNullDrivenRenderingBranches` ✅

## Validation results on representative governed pages
- **Loading**
  - Backlog Overview: `_backlogState = Loading()`
  - Multi-Product Planning: `_planningState = Loading()`
  - Sprint Activity: `_activityState = Loading()`
  - Plan Board: `_boardState = Loading()` mapped through `BoardRenderState`
- **Success**
  - All migrated pages/components render only inside `CanonicalDataStateView` child content
- **Empty**
  - Backlog Overview and Multi-Product Planning now surface canonical empty messages
  - Dependencies and Timeline panels now surface canonical empty messages
- **InvalidFilter**
  - Backlog Overview and Plan Board map invalid product scope into canonical invalid state
  - Sprint Activity maps blocked filter execution into canonical invalid state
- **Error**
  - All migrated surfaces now surface one canonical failure message path instead of direct ad-hoc alerts/components

## Rendering-path confirmation
- For the **option B governed read surfaces listed above**, only one mandatory read rendering path remains:
  - `DataStateViewModel<T>`
  - `CanonicalDataStateView`

## Remaining technical debt
- Remaining legacy patterns still exist outside this governed option B slice, especially in:
  - older analytics/read pages such as `PipelineInsights`, `PrDeliveryInsights`, `PrOverview`, `PortfolioProgressPage`, `ProductRoadmaps`, and `SprintExecution`
  - operational dialogs/forms/settings flows intentionally excluded by option B
  - shared read-only panels not yet folded into the governed audit set, such as `EffortDistributionPanel` and `CapacityCalibrationPanel`
- This debt is justified only by the user-approved scope reduction to option B; it is not part of the governed read-surface slice completed here.
