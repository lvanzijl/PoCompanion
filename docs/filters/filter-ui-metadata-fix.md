# UI/Client Canonical Filter Metadata Fix

## Summary

This fix stops the client/UI from silently collapsing migrated filter envelopes to `response.Data`.

What changed:

- added a shared client-side response pattern in `PoTool.Client` that keeps:
  - `RequestedFilter`
  - `EffectiveFilter`
  - `InvalidFields`
  - `ValidationMessages`
- added a shared compact UI notice component for canonical filter metadata
- updated migrated PR, Pipeline, Delivery, and Sprint pages to preserve metadata and surface material filter differences
- updated client services and cross-slice consumers so metadata is no longer dropped at the first service hop
- kept page layouts, controls, and backend semantics unchanged

Why:

- the backend now returns correct canonical filter metadata
- the client was discarding that metadata immediately
- users therefore could not see when invalid filters were normalized or when requested scope differed from applied scope

---

## Root Cause

The migrated backend endpoints already returned filter envelopes, but the client commonly did this:

```csharp
var response = await client.Get...EnvelopeAsync(...);
return response.Data;
```

That caused two problems:

1. canonical filter metadata was lost before the page/component could use it
2. normalized or invalid filters became silent fallbacks from the user’s perspective

As a result, backend behavior could be correct while the UI still looked misleading or incomplete.

---

## Affected Files

### Shared client pattern

- `PoTool.Client/Models/CanonicalClientResponse.cs`
- `PoTool.Client/Helpers/CanonicalClientResponseFactory.cs`
- `PoTool.Client/Components/Common/CanonicalFilterMetadataNotice.razor`

### Client services

- `PoTool.Client/Services/PipelineService.cs`
- `PoTool.Client/Services/PullRequestService.cs`
- `PoTool.Client/Services/BuildQualityService.cs`
- `PoTool.Client/Services/IBuildQualityService.cs`
- `PoTool.Client/Services/HomeProductBarMetricsService.cs`
- `PoTool.Client/Services/SprintDeliveryMetricsService.cs`
- `PoTool.Client/Services/WorkspaceSignalService.cs`

### Pages and components

- `PoTool.Client/Pages/Home/PrOverview.razor`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `PoTool.Client/Pages/Home/PipelineInsights.razor`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- `PoTool.Client/Pages/Home/SprintTrend.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- `PoTool.Client/Pages/Home/HomePage.razor`
- `PoTool.Client/Pages/Home/PlanBoard.razor`
- `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
- `PoTool.Client/Components/Flow/FlowPanel.razor`

### Tests

- `PoTool.Tests.Unit/Helpers/CanonicalClientResponseFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PipelineServiceTests.cs`
- `PoTool.Tests.Unit/Services/WorkspaceSignalServiceTests.cs`

### Documentation

- `docs/filters/filter-ui-metadata-fix.md`
- `docs/release-notes.json`

---

## Before vs After

### Before

- migrated endpoints returned envelopes
- client services/pages often returned or stored only `response.Data`
- invalid fields and requested/effective differences were dropped silently
- workspace signal consumers also discarded metadata internally

### After

- migrated envelopes are converted into a shared client-side response model that preserves data plus canonical filter metadata
- visible PR/Pipeline/Delivery/Sprint pages can render a compact reusable metadata notice
- cross-slice consumers keep metadata available for diagnostics and future rendering even when they do not display it directly
- existing charts, grids, filters, and page layouts remain unchanged

In short:

- **before:** backend metadata existed but vanished in the client
- **after:** metadata flows from backend to client services to pages/components, with visible signaling where it matters

---

## Validation

Correctness was validated with:

- solution build in Release mode
- focused client/service/helper tests for the new metadata-preservation path

Validation commands:

```bash
dotnet build PoTool.sln --configuration Release --no-restore
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~CanonicalClientResponseFactoryTests|FullyQualifiedName~ReleaseNotesServiceTests|FullyQualifiedName~PortfolioCdcUiAuditTests" -v minimal
```

Observed result:

- build succeeded
- **29 / 29** focused tests passed

What was proven:

- client service responses preserve canonical filter metadata
- the shared notice helper detects invalid or normalized filters
- main migrated pages can display canonical filter differences without redesigning their layouts
- workspace signal consumers no longer silently throw away metadata

---

## Known Limitations

- the fix uses compact notices rather than a larger filter UX redesign
- some aggregate/workspace consumers retain metadata only for diagnostics/future rendering and do not surface it visually yet
- backend filter semantics and DTO contracts were intentionally left unchanged
- non-migrated slices were intentionally left untouched
