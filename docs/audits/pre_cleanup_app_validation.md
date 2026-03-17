# Pre-Cleanup App Validation

_Validated: 2026-03-17_

## Build / Startup Status

- `dotnet restore PoTool.sln -m:1` completed successfully.
- `dotnet build PoTool.sln --no-restore -m:1` completed successfully with 0 warnings and 0 errors.
- Focused validation tests completed successfully:
  - `GetBacklogHealthQueryHandlerTests`
  - `GetMultiIterationBacklogHealthQueryHandlerMultiProductTests`
  - `GetSprintExecutionQueryHandlerTests`
  - `GetSprintTrendMetricsQueryHandlerTests`
  - `GetEpicCompletionForecastQueryHandlerTests`
  - `GetPortfolioProgressTrendQueryHandlerTests`
  - `GetPortfolioDeliveryQueryHandlerTests`
  - `GetEffortDistributionQueryHandlerTests`
  - `GetEffortDistributionTrendQueryHandlerTests`
  - `GetEffortEstimationQualityQueryHandlerTests`
  - `GetEffortEstimationSuggestionsQueryHandlerTests`
  - `TransportNamingAlignmentDocumentTests`
  - `CdcUsageCoverageDocumentTests`
- `PoTool.Api` started successfully and listened on `http://localhost:5291` without DI failures or serialization startup failures.
- Fresh-clone startup behavior was not clean in the default `Development` configuration:
  - `PoTool.Api/appsettings.Development.json` sets `TfsIntegration:UseMockClient` to `false`, so a plain local development run goes to live-mode behavior even though `PoTool.Api/appsettings.json` defaults it to `true`.
  - For local validation of the mock-backed analytics flow, the app had to be started with `TfsIntegration__UseMockClient=true`.
- First-run profile setup exposed a startup regression before mock mode was forced:
  - `/settings/productowner/edit` triggered `/api/workitems/goals/from-tfs`
  - that flow hit workspace-guard/live-mode failures on a fresh clone before cache-backed state existed
  - after mock mode was forced and a profile/product/team were configured, cache sync completed successfully with `workItemCount = 3705`

## Page Rendering Status

Validated by starting the app locally, syncing a mock-backed profile cache, and opening the main analytics routes in the browser.

| Surface | Route / entry point | Result |
| --- | --- | --- |
| Backlog health | `/home/health` | Loaded successfully. Health shell, signal chips, product card, and cross-workspace actions rendered without null-reference failures. |
| Backlog overview | `/home/backlog-overview` | Loaded successfully. Product card groups and readiness sections rendered. |
| Sprint metrics / sprint trends | `/home/delivery/sprint` | Loaded successfully. Sprint navigation, summary cards, and product delivery table rendered. |
| Sprint execution | `/home/delivery/execution` | Loaded successfully. Context chips rendered and the page fell back to a clean empty state (`No execution data found for the selected sprint.`) instead of throwing. |
| Portfolio progress | `/home/portfolio-progress` | Loaded successfully. Trend cards and charts rendered with story-point labels. |
| Portfolio delivery | `/home/delivery/portfolio` | Loaded successfully. Summary cards, product contribution section, feature contribution section, and bug table rendered. |
| Forecast | `/workspace/analysis/forecast` | Loaded successfully. Forecast panel rendered and forecast calculation for work item `1002` completed without mapping errors. |
| Effort distribution | `/workspace/analysis/effort` | Loaded successfully. Summary cards, heat map, chart, and utilization table rendered. |
| Effort estimation quality | API validation via `/api/metrics/effort-estimation-quality?maxIterations=6` | No standalone current client route was found. API returned a valid JSON payload with quality-by-type and trend data. |
| Effort estimation suggestions | API validation via `/api/metrics/effort-estimation-suggestions?onlyInProgressItems=true` | No standalone current client route was found. API returned a valid JSON payload with 23 suggestions. |

Observed browser console noise during page checks was limited to a blocked Google Fonts request and the existing preload warning; no analytics page produced a null-reference or DTO-mapping exception in the browser.

## Binding / DTO Status

- Forecast transport binding check:
  - `/api/metrics/epic-forecast/1002?maxSprintsForVelocity=6` returned both legacy and canonical fields:
    - `totalEffort`, `completedEffort`, `remainingEffort`
    - `totalStoryPoints`, `doneStoryPoints`, `deliveredStoryPoints`, `remainingStoryPoints`
  - the response serialized successfully and the forecast panel rendered the canonical story-point labels.
- Sprint trend binding check:
  - `/api/metrics/sprint-trend?productOwnerId=1&sprintIds=1&sprintIds=2&includeDetails=true` returned `featureProgress` and `epicProgress` entries containing both:
    - legacy compatibility fields such as `totalEffort` and `doneEffort`
    - canonical fields such as `totalStoryPoints` and `doneStoryPoints`
  - no dual-field binding exception was observed in `/home/delivery/sprint`.
- Portfolio progress binding check:
  - `/home/portfolio-progress` rendered `Net Flow (cumul.)`, `Scope Change`, `Portfolio Stock (SP)`, and `Throughput (SP)` successfully.
  - `/api/metrics/portfolio-progress-trend?productOwnerId=1&sprintIds=1&sprintIds=2` serialized successfully.
- Portfolio delivery binding check:
  - `/home/delivery/portfolio` rendered summary cards and tables without broken bindings.
  - `/api/metrics/portfolio-delivery?productOwnerId=1&sprintIds=1&sprintIds=2` serialized successfully.
- Sprint execution binding check:
  - `/api/metrics/sprint-execution?productOwnerId=1&sprintId=2` returned both effort-hour fields (`initialScopeEffort`, `completedEffort`, `spilloverEffort`) and story-point fields (`committedStoryPoints`, `deliveredStoryPoints`, `remainingStoryPoints`) without serialization issues.
- Effort analytics binding check:
  - `/api/metrics/effort-distribution?maxIterations=6` returned stable effort-hour payloads.
  - `/api/metrics/effort-estimation-quality?maxIterations=6` and `/api/metrics/effort-estimation-suggestions?onlyInProgressItems=true` both serialized successfully.

## Semantic Label Validation

- Story-point-oriented surfaces used story-point wording in the validated UI:
  - `/home/health` showed `ready story points` and `sp`
  - `/home/delivery/sprint` showed `Delivered Story Points`
  - `/home/portfolio-progress` showed `+0 SP`, `Portfolio Stock (SP)`, and `Throughput (SP)`
  - `/workspace/analysis/forecast` showed `Total Story Points`, `Delivered Story Points`, and `Remaining Story Points`
- Effort-hour-oriented surfaces kept effort wording:
  - `/workspace/analysis/effort` showed `Total Effort`, `Effort Heat Map`, `Effort by Iteration`, and `Effort`
  - `/api/metrics/effort-estimation-quality` and `/api/metrics/effort-estimation-suggestions` returned effort-based estimation payloads rather than story-point aliases
- Mixed portfolio delivery wording remained internally consistent with the current response shape:
  - `/home/delivery/portfolio` showed `Delivered Effort (hours)` for the portfolio summary
  - the same page still used `Feature Contribution (Story Points)` where the underlying data is story-point based
- Forecast numbers were stable for the validated sample:
  - epic `1002` returned zeros for both legacy compatibility fields and canonical story-point fields
  - the forecast panel rendered the same zero-valued summary without mismatched labels
- Sprint totals remained stable for the validated sample routes:
  - `/home/delivery/sprint` rendered `Completed PBIs`, `Delivered Story Points`, and bug totals without broken labels
  - `/home/delivery/execution` rendered effort-hour and story-point totals together without binding failures, even when the selected sprint had no execution data

## Regressions Found

1. **Default local development startup does not exercise the mock-backed path**
   - `PoTool.Api/appsettings.Development.json` forces `TfsIntegration:UseMockClient` to `false`
   - this causes a fresh local validation run to behave like live TFS instead of the repository’s mock-backed development path unless an environment override is supplied

2. **Add Profile / goals bootstrap path is not validation-safe on a fresh clone**
   - `/settings/productowner/edit` calls `/api/workitems/goals/from-tfs`
   - `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs` uses direct HTTP calls to TFS rather than the mock client
   - on a fresh local run this produced network/workspace-guard failures before cache-backed state existed

3. **Effort estimation quality and suggestions do not have a current standalone client route**
   - no current `PoTool.Client/Pages/**` route was found for these two analytics surfaces
   - validation had to be completed through the API endpoints instead of a user-facing page

## Safe To Continue Decision

**Decision: not yet safe to continue with cleanup work without addressing the runtime regressions above.**

Reasoning:

- the main cache-backed analytics pages do render once the application is forced into mock mode and the profile cache is synced
- transport alias serialization and the validated UI bindings looked stable after CDC and transport alignment
- however, the default fresh-clone development flow still exposes startup/bootstrap regressions before a user can reliably reach those pages
- because this step is explicitly a pre-cleanup validation pass, those regressions should be treated as blockers or documented follow-up work before broader UI adoption or compatibility cleanup continues
