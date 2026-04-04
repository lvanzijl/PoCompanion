# Cache Contract Fix

## Affected endpoints/services

### Cache-backed endpoints confirmed in scope

- `/api/buildquality/rolling`
- `/api/buildquality/sprint`
- `/api/buildquality/pipeline`
- `/api/projects/{alias}/planning-summary`

### Client services fixed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProjectService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/DataStateHttpClientHelper.cs`

### Pages fixed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProjectPlanningOverview.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`

## Before vs after behavior

### Before

- Cache-backed endpoints were wrapped by `DataStateResponseDto<T>` on the backend, but several client calls still deserialized directly into the inner DTO.
- `BuildQualityService` and `ProjectService` could treat envelope JSON as malformed domain payload, producing client exceptions or invalid deserialization.
- Not-ready cache states could surface as empty/default data, missing summaries, misleading zeroed charts, or generic crash/error flows instead of explicit readiness messaging.
- Build Quality overlay/detail requests in Pipeline Insights could fail during per-pipeline fan-out and leave the page in a degraded or noisy state.

### After

- Cache-backed `HttpClient` calls now go through one shared helper that reads the backend envelope safely.
- Valid `DataStateResponseDto<T>` responses unwrap into typed client data only when the envelope is truly successful.
- `NotReady`, `Failed`, `Unavailable`, and `Empty` are carried forward explicitly instead of being mistaken for valid payloads.
- Affected pages now render explicit not-ready/failed states for cache-backed Build Quality and project planning summary data.
- Malformed or empty-success payloads are treated as failures, not as valid data.

## What was changed

- Added a shared client result model for cache-backed reads.
- Added `GetDataStateAsync<T>()` as the canonical client helper for cache-backed envelope handling.
- Refactored Build Quality reads to use the shared helper for rolling, sprint, and pipeline detail requests.
- Refactored project planning summary reads to use the shared helper.
- Updated affected UI pages to show explicit sync/failure states instead of falling through to empty/default rendering.
- Added unit coverage for:
  - valid envelope unwrapping
  - not-ready envelope handling
  - malformed payload rejection

## Pages that were fixed

- Health Overview
- Sprint Delivery / Sprint Trend Build Quality section
- Pipeline Insights Build Quality overlay and drawer detail
- Trends Workspace pipeline signal tile
- Project Planning Overview
- Plan Board project planning summary card
- Product Roadmaps project summary card

## Remaining inconsistencies

- Some cache-backed client surfaces already used generated envelope-aware clients or dedicated state services and were not changed here.
- NSwag-generated clients for some cache-backed endpoints still reflect pre-envelope inner payload signatures, but the affected paths in this task are now routed through corrected handwritten services.
- `Unavailable` is a client transport/contract interpretation state layered on top of the shared backend envelope; the backend shared DTO still only emits `Available`, `Empty`, `NotReady`, and `Failed`.
