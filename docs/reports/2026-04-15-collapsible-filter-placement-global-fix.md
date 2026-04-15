# Collapsible Filter Placement Global Fix

## Root cause

The misplaced filter body was caused by shared layout structure, not by page-specific filter logic. `PoTool.Client/Layout/MainLayout.razor` rendered the shared filter header (`<FilterSummaryBar />`) before `@Body`, but rendered the collapsible shared filter body (`<GlobalFilterControls />`) after `@Body`. When the shared filter UI expanded, the content stayed in normal document flow and therefore appeared beneath the page body instead of directly below the header.

This was a structural markup problem in the shared layout. No evidence showed MudBlazor portal behavior, overlay behavior, or page-level CSS ordering as the primary cause.

## Shared implementation analyzed

- Shared filter header: `PoTool.Client/Components/Common/FilterSummaryBar.razor`
- Shared collapsible filter body: `PoTool.Client/Components/Common/GlobalFilterControls.razor`
- Shared expand/collapse primitive: `MudCollapse` inside `GlobalFilterControls.razor`
- Shared hosting layout: `PoTool.Client/Layout/MainLayout.razor`
- Shared layout CSS reviewed: `PoTool.Client/Layout/MainLayout.razor.css`
- Shared managed-route catalog reviewed: `PoTool.Client/Services/GlobalFilterPageCatalog.cs`

## Affected pages and scope

### Definitely affected shared-filter routes

These routes use the shared filter chrome hosted by `MainLayout` and therefore inherited the placement bug:

- `/home`
- `/home/health`
- `/home/health/overview`
- `/home/health/backlog-health`
- `/home/backlog-overview`
- `/home/changes`
- `/home/delivery`
- `/home/delivery/portfolio`
- `/home/delivery/execution`
- `/home/delivery/sprint`
- `/home/sprint-trend`
- `/home/trends`
- `/home/trends/delivery`
- `/home/portfolio-progress`
- `/home/pipeline-insights`
- `/home/pull-requests`
- `/home/pr-delivery-insights`
- `/home/bugs`
- `/home/bugs/detail`
- `/home/validation-triage`
- `/home/validation-queue`
- `/home/validation-fix`
- `/home/planning`
- `/planning/multi-product`
- `/planning/product-roadmaps`
- `/planning/plan-board`
- `/bugs-triage`
- `/workitems`
- `/planning/product-roadmaps/{productId}`
- `/planning/{projectAlias}/product-roadmaps`
- `/planning/{projectAlias}/plan-board`
- `/planning/{projectAlias}/overview`
- `/home/delivery/sprint/activity/{sprintId}`
- `/home/sprint-trend/activity/{sprintId}`

### Same implementation and therefore at risk

All routes resolved by `GlobalFilterPageCatalog` are hosted through the same shared layout/components above, so they were all at risk even if not individually reported by users.

### Different implementations and out of scope

The following collapsible or expandable UI patterns were reviewed and are not hosted by the shared global filter layout, so they were out of scope unless separately reported:

- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor` local "Context" collapse block
- `PoTool.Client/Pages/TfsConfig.razor` advanced verification expansion panel
- `PoTool.Client/Pages/Home/DeliveryTrends.razor` drill-down expansion panel
- other MudExpansion/MudCollapse usages for diagnostics, read-only details, or local page sections

No page-specific exception was required for the shared filter placement fix.

## What changed

- Moved the shared filter host region in `PoTool.Client/Layout/MainLayout.razor` so `<GlobalFilterControls />` now renders immediately after `<FilterSummaryBar />` and before `@Body`.
- Kept the shared filter state, summary chips, correction messages, blocking notices, and expand/collapse behavior unchanged.
- Added regression coverage in `PoTool.Tests.Unit/Audits/GlobalFilterLayoutAuditTests.cs` to verify:
  - shared filter controls stay before page body in `MainLayout`
  - the shared filter chrome remains hosted only by `MainLayout`
- Updated `docs/release-notes.json` because the fix changes user-visible page layout behavior.

## Shared vs. page-specific conclusion

The issue was shared. The defect lived in the shared layout host order, and one shared layout change corrected placement across all managed pages that use the shared filter UI.

## Validation performed

### Code inspection

- Verified the shared filter summary and shared filter body are separate components.
- Verified `GlobalFilterControls.razor` uses `MudCollapse` correctly inside its own component.
- Verified the incorrect placement came from `MainLayout.razor` rendering order.
- Verified no page-specific markup override was needed for the shared filter host.

### Automated validation

- Baseline before changes:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo` ✅
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --nologo --logger "console;verbosity=minimal"` ❌ with pre-existing unrelated failures in:
    - `CacheBackedGeneratedClientMigrationAuditTests`
    - `DocumentationVerificationBatch6Tests`
    - `NswagGovernanceTests`
    - `TestCategoryEnforcementTests`
- Added targeted structural regression coverage for shared filter layout order and host ownership.

### Visual verification

No browser-based visual verification is claimed in this report.

## Manual verification checklist

Because this change is layout-order based and no browser screenshot test framework is present here, use this manual checklist on a few representative managed pages:

1. Open a managed shared-filter page such as Sprint Execution, Delivery Trends, Product Roadmaps, and Work Item Explorer.
2. Confirm the shared filter summary renders above the main page content.
3. Expand filters.
4. Confirm the filter body appears directly below the summary header, not below the page body.
5. Collapse filters and confirm summary chips remain intact.
6. Trigger a page with blocking or correction messages and confirm they still render in the shared filter region.
7. Check a narrow/mobile-width viewport and confirm the expanded filter body still stays under the summary header.

## Remaining risks

- The fix addresses the shared layout host order only; independent page-local collapsible sections were not changed.
- Without browser automation, responsive spacing and animation were validated structurally rather than visually.
