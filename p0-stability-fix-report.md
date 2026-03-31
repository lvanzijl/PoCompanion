# P0 stability fix report

Timestamp: 2026-03-31T17:45:00Z

## Summary

### Data contract fixes

1. **Build Quality UTC timestamps now use offset-aware contracts**
   - Root cause: the API returned offset-less UTC `DateTime` values while the client consumed offset-aware Build Quality DTOs, which caused successful `200` responses to fail client-side deserialization on Health Overview and Sprint Delivery.
   - Files changed:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/BuildQuality/BuildQualityPageDto.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/BuildQuality/DeliveryBuildQualityDto.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/BuildQuality/PipelineBuildQualityDto.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/BuildQualityController.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/BuildQuality/GetBuildQualityRollingWindowQueryHandler.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/BuildQuality/GetBuildQualitySprintQueryHandler.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/BuildQuality/GetBuildQualityPipelineDetailQueryHandler.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.BuildQualityDeliveryFilters.cs`
   - Before: Health Overview and Sprint Delivery could receive `200 OK` from Build Quality APIs and still fail with client deserialization errors.
   - After: Build Quality endpoints emit offset-aware UTC timestamps consistently and the query-string formatter sends ISO-8601 UTC values with offsets.

2. **Build Quality frontend deserialization now uses shared contracts directly**
   - Root cause: the generated Build Quality client path remained unstable in the live WASM runtime even after the backend payload was corrected.
   - Files changed:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/IBuildQualityService.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/BuildQualityCompactComponent.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/BuildQualityTooltipComponent.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HealthOverviewPage.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TrendsWorkspaceTileSignalService.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/TrendsWorkspaceTileSignalServiceTests.cs`
   - Before: Health Overview still failed in the browser even though the corrected JSON payload deserialized successfully outside the generated client path.
   - After: the typed frontend Build Quality service reads the shared contracts directly through `HttpClient`, so pages and Build Quality components all consume one canonical contract.

### API endpoint fixes

1. **Sprint/backlog analytical reads no longer surface empty scope as 404**
   - Root cause: `MetricsController` translated empty sprint/backlog analytics results into `404 Not Found`, which broke Home tile loads and made valid but empty analytical scope look like a missing endpoint.
   - Files changed:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/MetricsControllerSprintCanonicalFilterTests.cs`
   - Before: calls such as `/api/Metrics/backlog-health` and `/api/Metrics/sprint` could return `404` for empty or invalidated sprint scope.
   - After: the controller returns a successful envelope with canonical validation messages and an empty DTO, so the UI keeps working without hiding invalid filter normalization.

### Context propagation fixes

1. **Workspace navigation now preserves canonical product/team/sprint query context**
   - Root cause: multiple pages and the shared workspace navigation bar dropped query context when routing between hubs and detail pages, forcing re-selection or silently resetting filters.
   - Files changed:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/WorkspaceQueryContextHelper.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/WorkspaceBase.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Layout/MainLayout.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryWorkspace.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanningWorkspace.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Helpers/WorkspaceQueryContextHelperTests.cs`
   - Before: Home → Delivery/Planning hub navigation dropped `productId`; selector-driven pages like Plan Board and Portfolio Delivery did not reliably sync their effective context back to the URL.
   - After: workspace routes share one query-context helper, preserve product/team/sprint identifiers across navigation, and sync selector state back into the route so revisits reuse the active context.

## Endpoints verified

- `GET /api/buildquality/rolling`
- `GET /api/buildquality/sprint`
- `GET /api/Metrics/backlog-health`
- `GET /api/Metrics/sprint`
- `GET /api/Metrics/sprint-execution`
- `GET /api/Metrics/sprint-trend`
- `GET /api/PullRequests/sprint-trends`

## Pages validated

- Home dashboard (`/home?productId=2`)
- Health hub (`/home/health?productId=2`)
- Health Overview (`/home/health/overview?productId=2`)
- Delivery hub (`/home/delivery?productId=2`)
- Sprint Delivery (`/home/delivery/sprint?productId=2`)
- Planning hub (`/home/planning?productId=2`)
- Plan Board (`/planning/plan-board?productId=2`)
- Backlog Health (`/home/health/backlog-health?productId=2`)

## Remaining risks or assumptions

- Attempted NSwag regeneration against the refreshed live swagger exposed broader pre-existing generated-client type collisions unrelated to this P0 fix. The implemented solution therefore keeps the existing generated client in place for unaffected areas and moves Build Quality reads onto the shared typed service path instead of expanding scope into a full client-generation cleanup.
- Browser console still shows the blocked Google Fonts request caused by the sandboxed browser environment, but no application-level 404s or Build Quality deserialization errors were observed during the validated flow.

## Confirmation

The P0 class of issues targeted in this task is eliminated for the validated surfaces:

- Health Overview loads without client deserialization errors.
- Sprint Delivery loads without client deserialization errors.
- No `404` calls were observed for the validated metrics endpoints.
- Product context remained attached across Home, Health, Delivery, Planning, Plan Board, and Backlog Health navigation.
- Empty analytical sprint/backlog results now return explicit empty envelopes instead of broken endpoint responses.
