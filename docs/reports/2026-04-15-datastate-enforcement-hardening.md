# DataState enforcement hardening

## Scope
This change hardens the client-side DataState migration by removing remaining payload-only reads from the main cache-backed service layer, updating affected callers to consume `DataStateResult<T>`, extending shared UI mapping for invalid filter scope, and adding guard tests to prevent regressions.

## Baseline
- Repository baseline was clean before work started.
- Baseline validation before edits:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --logger "console;verbosity=minimal"`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj -c Release --no-build --logger "console;verbosity=minimal"`
- The baseline unit suite already had unrelated failures in governance/documentation audits and an existing generated-client migration audit; those failures were not introduced by this change.

## Removed or replaced payload-only paths

### Critical service paths now using `DataStateResult<T>`
- `PoTool.Client/Services/PipelineService.cs`
  - `GetAllAsync`
  - `GetRunsAsync`
  - existing `GetMetricsAsync`
  - existing `GetRunsForProductsAsync`
- `PoTool.Client/Services/PullRequestService.cs`
  - `GetAllAsync`
  - `GetByIdAsync`
  - `GetCommentsAsync`
  - `GetIterationsAsync`
  - existing `GetMetricsAsync`
  - existing `GetFilteredAsync`
- `PoTool.Client/Services/WorkItemService.cs`
  - `GetAllAsync`
  - `GetFilteredAsync`
  - `GetByTfsIdWithValidationAsync`
  - `GetAllGoalsAsync`
  - `GetGoalHierarchyAsync`
  - `GetAllWithValidationAsync` overloads
  - `GetValidationTriageSummaryAsync`
  - `GetHealthWorkspaceProductSummaryAsync`
  - `GetValidationQueueAsync`
  - `GetValidationFixSessionAsync`
  - `GetDistinctAreaPathsAsync`
  - `GetByRootIdsAsync`
  - `EnsureLoadedForRootsAsync`
  - `GetRevisionsAsync`
  - existing `GetBacklogStateResultAsync`
- `PoTool.Client/Services/WorkItemFilteringService.cs`
  - `FilterByValidationWithAncestorsAsync`
  - `GetWorkItemIdsByValidationFilterAsync`
  - `CountWorkItemsByValidationFilterAsync`
  - `IsDescendantOfGoalsAsync`
  - `FilterByGoalsAsync`
- `PoTool.Client/Services/ReleasePlanningService.cs`
  - `GetBoardAsync`
  - `GetUnplannedEpicsAsync`
  - `GetObjectiveEpicsAsync`
  - `GetEpicFeaturesAsync`
- `PoTool.Client/Services/RoadmapAnalyticsService.cs`
  - `LoadDependencySignalsAsync`
  - `LoadForecastAsync`
  - `LoadBacklogHealthAsync`

### Direct component/page payload-only reads replaced with explicit state handling
- `PoTool.Client/Components/Timeline/TimelinePanel.razor`
- `PoTool.Client/Components/Dependencies/DependenciesPanel.razor`
- `PoTool.Client/Components/WorkItems/SubComponents/ValidationHistoryPanel.razor`
- `PoTool.Client/Components/WorkItems/SubComponents/ValidationSummaryPanel.razor`
- `PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor`
- `PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
- `PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor`
- `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
- `PoTool.Client/Pages/BugsTriage.razor`
- `PoTool.Client/Pages/Metrics/SubComponents/BacklogHealthFilters.razor`
- `PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
- `PoTool.Client/Components/ReleasePlanning/ObjectiveEpicsDialog.razor`
- `PoTool.Client/Components/ReleasePlanning/SplitEpicDialog.razor`

### Unsafe helper surface reduced
- Removed payload-collapsing `GetDataOrDefault`/`GetReadOnlyListOrDefault` extension APIs from `PoTool.Client/Helpers/GeneratedClientEnvelopeExtensions.cs`.
- Added generated-envelope to `DataStateResult<T>` conversion helpers in `PoTool.Client/Helpers/GeneratedCacheEnvelopeHelper.cs`.

## Remaining exceptions
- `PoTool.Client/Services/ReleasePlanningService.cs` still uses `GeneratedCacheEnvelopeHelper.GetDataOrDefault(...)` for mutation result envelopes (`CreateLaneAsync`, `DeleteLaneAsync`, epic/line mutation methods, `RefreshValidationAsync`, `ExportBoardAsync`, `SplitEpicAsync`).
  - Justification: these are write-result flows rather than cache-backed analytical reads, and converting the mutation UI to `DataStateResult<T>` would be a separate mutation-surface migration.
- `PoTool.Client/ApiClient/ApiClient.PortfolioConsumption.cs`
- `PoTool.Client/ApiClient/ApiClient.DeliveryFilters.cs`
- `PoTool.Client/ApiClient/ApiClient.SprintFilters.cs`
- `PoTool.Client/ApiClient/ApiClient.PullRequestFilters.cs`
- `PoTool.Client/ApiClient/ApiClient.PipelineFilters.cs`
  - These legacy convenience wrappers still use `RequireData(...)`, but no active client code path in the repository consumes them after this change. They remain technical debt and are now documented as audit exceptions.

## Before vs after architecture

### Before
- Cache-backed endpoints in multiple client services still returned `T`, `T?`, or `IEnumerable<T>`.
- Callers silently collapsed `NotReady`, `Empty`, `Failed`, and invalid filter scope into null/default payloads.
- Shared UI state views only reasoned about raw `DataStateDto` and treated corrected invalid filter scope as ordinary success.

### After
- Main cache-backed service reads return `DataStateResult<T>` and preserve state, reason, retry hints, and canonical filter metadata.
- Callers explicitly branch on `CanUseData`, `Status`, and `Reason` instead of null/default fallbacks.
- Shared `DataStateViewModel`, `DataStateView`, `DataStatePanel`, and `CacheStatePresentation` now expose invalid filter scope as a first-class warning state.
- Regression coverage now fails if guarded client files reintroduce payload-only DataState helpers.

## Enforcement mechanisms added
- `PoTool.Tests.Unit/Audits/ClientDataStateEnforcementHardeningAuditTests.cs`
  - prevents guarded service/page/component files from using `GetDataOrDefault`, `GetReadOnlyListOrDefault`, or `RequireData`.
- `PoTool.Tests.Unit/Models/DataStateViewModelTests.cs`
  - verifies shared UI mapping preserves invalid filter state.
- Existing `DataStateResultFactoryTests` continue covering reason and metadata preservation.

## Risks and trade-offs
- Some non-analytical mutation result paths still use legacy payload collapsing and remain explicit exceptions.
- Legacy `ApiClient/*Filters.cs` convenience wrappers still exist for compatibility, although active client paths now avoid them.
- Several callers intentionally preserve neutral/empty UI behavior by converting non-usable results into local empty collections after inspecting the DataState first.

## Local verification without TFS
1. Build:
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
2. Run focused tests for this change:
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~DataStateResultFactoryTests|FullyQualifiedName~DataStateViewModelTests|FullyQualifiedName~WorkItemFilteringServiceClientTests|FullyQualifiedName~ClientDataStateEnforcementHardeningAuditTests" --logger "console;verbosity=minimal"`
3. Optionally rerun the broader unit suite to confirm this change does not add failures beyond the known baseline governance/documentation audits.

## Limitations
- This hardening pass does not complete the separate migration of write-result envelopes or legacy generated convenience wrappers that are no longer consumed by active client pages.
- The repository still contains unrelated pre-existing governance/documentation test failures outside this task scope.
