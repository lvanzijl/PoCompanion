# Global filter migration

## Pages originally non-compliant

### Fully page-local filter pages at the start of this migration
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
  - local `team` / `sprint` state
  - direct query parsing for `teamId`
  - local sprint loading and invalid-selection clearing
  - local requested-to-effective filter behavior
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
  - direct query parsing for filter-related period parameters
  - profile/time filter construction inside the page
  - no shared query gate before loading

### Shared-store pages still carrying residual local filter behavior
- `PoTool.Client/Pages/Home/PrOverview.razor`
  - dead local time defaults
  - page-local invalid sprint recovery fallback

## Pages migrated
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- `PoTool.Client/Pages/Home/PrOverview.razor`

## Changes made per page

### `PrDeliveryInsights.razor`
- Added `GlobalFilterStore`, `GlobalFilterRouteService`, `PageFilterExecutionGate`, and `GlobalFilterLabelService`
- Removed local team/sprint state ownership
- Removed direct filter query parsing
- Removed page-local defaulting and sprint validity repair
- Switched team/sprint UI interactions to shared filter updates through `GlobalFilterStore`
- Switched collapsed filter labels to `GlobalFilterLabelService`

### `SprintTrendActivity.razor`
- Added `GlobalFilterStore` and `PageFilterExecutionGate`
- Removed filter-related query parsing for product-owner and period state
- Replaced page-local period construction with sprint context resolved through `GlobalFilterStore`
- Derived sprint period from the resolved team+sprint selection
- Ensured the page only queries after shared filter resolution is valid
- Kept non-filter `view` query parsing only for back-navigation behavior

### `PrOverview.razor`
- Removed dead local date defaults
- Removed page-local invalid sprint fallback logic
- Left data loading fully dependent on shared filter state plus the shared execution gate

## Architectural safeguard

Added:
- `PoTool.Tests.Unit/Audits/GlobalFilterArchitectureAuditTests.cs`

Safeguards:
- managed filter pages must use `GlobalFilterStore` directly or inherit `WorkspaceBase`
- home pages must not parse shared filter query keys locally

## Before vs after behavior

### Before
- `PrDeliveryInsights` owned its own team/sprint state and manually interpreted query parameters
- `SprintTrendActivity` depended on page-local query parameters for the effective sprint period
- `PrOverview` still contained residual page-local recovery behavior
- no audit guard existed to catch direct parsing of shared filter keys in home pages

### After
- `PrDeliveryInsights` reacts only to shared requested/effective filter state
- `SprintTrendActivity` derives its effective sprint period from shared filter resolution instead of route-provided filter fragments
- `PrOverview` no longer carries redundant local fallback logic
- architectural audit tests now flag new local parsing of shared filter keys or filter-managed pages that bypass shared infrastructure

## Remaining exceptions

### Intentional non-filter query parsing
- `SprintTrendActivity.razor` still reads `view` from the query string for back-navigation mode only
- `ValidationQueuePage.razor`, `ValidationFixPage.razor`, and `OnboardingWorkspace.razor` still parse non-filter query parameters unrelated to shared filter ownership

No remaining exceptions were found for team, sprint, product, project, or shared time filter ownership in the migrated home pages.

## Confirmation

`GlobalFilterStore` is now the single source of truth for shared page filter state across the migrated home pages in this scope.

## Validation

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~GlobalFilterDefaultsServiceTests|FullyQualifiedName~GlobalFilterArchitectureAuditTests|FullyQualifiedName~CanonicalClientResponseFactoryTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`
