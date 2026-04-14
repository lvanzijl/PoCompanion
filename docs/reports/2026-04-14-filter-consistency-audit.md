# Filter consistency audit

## Pages audited and classification

### Sprint-based shared-filter pages
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/SprintTrend.razor`
- `PoTool.Client/Pages/Home/PipelineInsights.razor`

### Range/time-based shared-filter pages
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Pages/Home/PrOverview.razor`

### Non-time or mixed filter pages with canonical filter metadata
- `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- `PoTool.Client/Pages/Home/HomePage.razor`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`

### Shared infrastructure audited
- `PoTool.Client/Services/FilterStateResolver.cs`
- `PoTool.Client/Services/GlobalFilterStore.cs`
- `PoTool.Client/Services/GlobalFilterAutoResolveService.cs`
- `PoTool.Client/Services/GlobalFilterDefaultsService.cs`
- `PoTool.Client/Services/GlobalFilterLabelService.cs`
- `PoTool.Client/Helpers/CanonicalClientResponseFactory.cs`

## Issues found

### 1. Shared UI updates still depended on stale query parameters
- `FilterStateResolver` still preferred current query-string values over locally requested UI changes.
- Result: pages had to rely on follow-up navigation timing or page-local clearing logic to make a team/sprint change stick immediately.

### 2. Multiple pages duplicated shared sprint/range recovery
- `SprintExecution.razor`
- `PortfolioDelivery.razor`
- `PortfolioProgressPage.razor`
- `DeliveryTrends.razor`
- `PipelineInsights.razor`

These pages contained local logic to:
- clear invalid sprint selections
- rebuild default sprint windows
- recover from team changes by nulling local state and hoping navigation would catch up

### 3. Team and sprint label rendering was still partially local
- `SprintExecution.razor`
- `PortfolioDelivery.razor`
- `PortfolioProgressPage.razor`

These pages still mapped filter state IDs to display names with page-local list lookups instead of the shared label service.

### 4. Canonical filter metadata was loaded but not shown on Health Overview
- `HealthOverviewPage.razor` stored canonical filter metadata but did not render the shared notice component.

## Fixes applied

### Shared filter resolving
- Updated `PoTool.Client/Services/FilterStateResolver.cs`
  - local bridge state now overrides stale query values during in-page UI updates
  - explicit local clears now clear stale team/product/project/time query selections before navigation catches up
- Kept route/path authority intact for route-owned project/product cases

### Shared auto-resolution
- Updated `PoTool.Client/Services/GlobalFilterAutoResolveService.cs`
  - UI-driven unresolved or invalid sprint/range state now re-resolves through the shared layer
  - invalid sprint/range selections are revalidated against the selected team's sprint set

### Removed duplicated page logic
- Updated `PoTool.Client/Pages/Home/SprintExecution.razor`
- Updated `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- Updated `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- Updated `PoTool.Client/Pages/Home/DeliveryTrends.razor`
- Updated `PoTool.Client/Pages/Home/PipelineInsights.razor`

Removed page-local:
- invalid sprint reset checks
- invalid range reset checks
- default multi-sprint window creation on first load/team change

These pages now rely on shared filter resolution before their query gates run.

### Shared label formatting
- Updated `PoTool.Client/Services/GlobalFilterLabelService.cs`
  - added shared sprint and sprint-range formatting helpers
- Updated pages to use shared team/sprint label formatting instead of page-local ID-to-name lookups

### Canonical notice coverage
- Updated `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
  - now renders `CanonicalFilterMetadataNotice`
  - converts loaded canonical metadata into the shared notice model

### Tests added/updated
- Updated `PoTool.Tests.Unit/Services/GlobalFilterStoreTests.cs`
  - verifies UI-driven team changes now auto-resolve sprint pages to the new team's current sprint
  - verifies UI-driven team changes now auto-resolve range pages to the new team's valid sprint window

## Before vs after behavior

### Issue type: stale query precedence during UI changes
**Before**
- UI updates could leave the old route query authoritative until navigation completed
- pages compensated with local clears and local default-window logic

**After**
- shared local state is authoritative immediately during UI interactions
- shared resolution produces a valid effective filter before pages query data

### Issue type: page-local sprint/range recovery
**Before**
- several pages implemented their own invalid sprint/range recovery
- range pages rebuilt default windows locally

**After**
- shared filter resolution owns invalid sprint/range correction
- pages load sprints and query data without custom repair logic

### Issue type: inconsistent label mapping ownership
**Before**
- several pages still converted selected team/sprint IDs to display names locally

**After**
- shared label formatting is used for those page-level filter labels

### Issue type: hidden canonical filter corrections
**Before**
- Health Overview loaded canonical metadata but did not show the shared notice

**After**
- Health Overview surfaces the shared canonical filter notice like the other canonical-filter pages

## Remaining risks or edge cases

### Remaining page-local filter flows
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor` still uses page-local team/sprint/date parsing rather than `GlobalFilterStore`
- this page was audited but not refactored in this change because it still follows a page-local filter model instead of the shared global envelope flow

### Metadata not yet surfaced everywhere
- `HomePage.razor`, `PlanBoard.razor`, and `TrendsWorkspace.razor` still retain canonical filter metadata for internal consumers without rendering a shared top-level notice
- this is a visibility gap, not a first-load validity gap

## Architectural conclusions

- For pages already participating in `GlobalFilterStore`, the shared filter layer is now the authoritative owner for:
  - query/local requested-state merging
  - invalid sprint/range correction
  - default sprint/range resolution
  - shared team/sprint display formatting
- A remaining architectural gap exists where some pages still use page-local filter models rather than the shared filter store; those pages are now the main source of inconsistency risk rather than the shared filter layer itself.

## Validation

- `dotnet build PoTool.sln -c Release --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~GlobalFilterDefaultsServiceTests|FullyQualifiedName~CanonicalClientResponseFactoryTests" --logger "console;verbosity=normal"`
