# Data-state enforcement completion

## Scope

This slice completed the remaining **page-level NotReady normalization** work, extended the governed rendering boundary across the already-migrated cache-backed views, and added CI governance to stop direct UI-layer `NotReady` regressions.

## Violations found

### UI-layer NotReady handling
Resolved:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/BugsTriage.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor`

Remaining after this slice:
- none in `PoTool.Client/Pages/**`
- none in `PoTool.Client/Components/**`

### Direct DataStatePanel usage
Resolved earlier and retained:
- all page-level direct usages were removed

Remaining after this slice:
- only `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStateView.razor` retains `DataStatePanel` as the internal primitive behind the governed renderer

### Legacy loading / error / null-driven rendering still present
Still present and **not fully eliminated**:
- Page-level manual loading patterns remain in 31 page files
- Page-level direct error displays remain in 27 page files
- Page-level null-driven rendering patterns remain in 30 page files
- Component-level manual loading patterns remain in 18 component files
- Component-level direct error displays remain in 17 component files
- Component-level null-driven rendering patterns remain in 33 component files

Representative remaining files:
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/ProfilesHome.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Settings/EditProductOwner.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Settings/ManageProductOwner.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Settings/ManageTeams.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Timeline/TimelinePanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`

## Summary of removed legacy patterns

- Removed UI-page branching on `DataStateDto.NotReady`
- Removed UI-page branching on `DataStateResultStatus.NotReady`
- Normalized cache-backed `NotReady` responses into `Loading` inside `DataStateViewModel<T>` and `CacheBackedDataStateViewModelFactory`
- Replaced page-level `NotReady` / `Failed` fallback pairs with `UiDataState`-based canonical rendering
- Removed duplicate `NotReady`/`Failed` fallback markup from plan board, sprint execution, sprint trend activity, and roadmap summary flows

## Enforcement mechanisms added

- `PoTool.Tests.Unit/Audits/DataStateGovernanceAuditTests.cs`
  - blocks direct page-level `DataStatePanel`
  - blocks any `NotReady` token usage under `PoTool.Client/Pages/**`
  - blocks any `NotReady` token usage under `PoTool.Client/Components/**`
  - blocks standalone legacy state displays in a governed renderer file set

## Governance test coverage

Current governance coverage includes:
- direct `DataStatePanel` isolation
- page/component `NotReady` token prohibition
- governed renderer file checks
- cache-backed state normalization tests
- `DataStateViewModel` normalization tests

Current governance gaps:
- no global blocker yet for every remaining manual `LoadingIndicator` usage
- no global blocker yet for every remaining direct `ErrorDisplay` / error `MudAlert` usage
- no global blocker yet for every remaining null-driven render branch

## Validation results

Local validation completed:
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`

Recommended targeted validation after a follow-up cleanup slice:
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~DataStateGovernanceAuditTests|FullyQualifiedName~CacheBackedDataStateViewModelFactoryTests|FullyQualifiedName~DataStateViewModelTests" --logger "console;verbosity=minimal"`

## Confirmation of alternative rendering paths

Status:
- there is now **one governed path for NotReady normalization** before page rendering
- there is **not yet one exclusive rendering path for all loading/error/empty/null cases across the entire application**

## Remaining technical debt

The repository is **not yet fully compliant** with the original end-state goal.

Open cleanup remains in:
- settings pages/dialogs
- release-planning dialogs
- work-item explorer/detail/history components
- dependency/timeline/forecast reusable panels
- older planning and workspace pages that still use manual loading/error/null branches

Follow-up work should:
- migrate remaining manual `LoadingIndicator` and `ErrorDisplay` branches to `CanonicalDataStateView`
- remove null-driven top-level rendering from remaining pages/components
- extend governance from the current guarded file set to the full UI surface
