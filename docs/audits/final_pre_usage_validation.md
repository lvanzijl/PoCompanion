# Final Pre-Usage Validation

_Validated: 2026-03-17_

## Startup

- `dotnet restore PoTool.sln -m:1` completed successfully on the fresh clone.
- `dotnet build PoTool.sln --no-restore -m:1` completed successfully with 0 warnings and 0 errors.
- Focused validation tests completed successfully:
  - `MockConfigurationSeedHostedServiceTests`
  - `WorkItemStateClassificationServiceTests`
  - `GetGoalsFromTfsQueryHandlerTests`
  - `GetBacklogHealthQueryHandlerTests`
  - `GetSprintExecutionQueryHandlerTests`
  - `GetSprintTrendMetricsQueryHandlerTests`
  - `GetEpicCompletionForecastQueryHandlerTests`
  - `GetPortfolioProgressTrendQueryHandlerTests`
  - `GetPortfolioDeliveryQueryHandlerTests`
  - `GetEffortDistributionQueryHandlerTests`
  - `GetEffortDistributionTrendQueryHandlerTests`
  - `GetEffortEstimationQualityQueryHandlerTests`
  - `GetEffortEstimationSuggestionsQueryHandlerTests`
  - `UiSemanticLabelsTests`
- `PoTool.Api` started successfully on `http://localhost:5291` in the default `Development` profile with no environment overrides.
- Startup logs showed the mock configuration seed completing before the host began serving requests:
  - `Seeded mock configuration with 3 profiles, 6 products, and 8 teams.`
  - no DI failures were observed
  - no serialization startup failures were observed
- Browser console noise during validation remained limited to the existing blocked Google Fonts request and the preload warning.

## Onboarding

- `/onboarding` rendered the onboarding wizard successfully on the fresh run.
- `GET /api/tfsconfig` returned seeded mock configuration:
  - `url = https://dev.azure.com/mock`
  - `project = Battleship Systems`
  - `defaultAreaPath = Battleship Systems`
- Onboarding bootstrap endpoints responded successfully without workspace/bootstrap errors:
  - `GET /api/workitems/area-paths/from-tfs` returned 12 area paths
  - `GET /api/workitems/goals/from-tfs` returned 10 goals
- Fresh-start seeded usage data was present immediately:
  - `GET /api/profiles` returned 3 profiles
  - `GET /api/products/all` returned 6 products
  - `GET /api/profiles/active` returned Product Owner `1` (`Commander Elena Marquez`)
- The blocking bootstrap issue found during this validation pass was localized and fixed:
  - mock startup previously seeded profiles/products/teams without seeding TFS configuration
  - that allowed cache sync to fail later with `TFS configuration not found`
  - `PoTool.Api/Services/MockData/MockConfigurationSeedHostedService.cs` now seeds the mock TFS configuration together with the rest of the mock onboarding data

## Cache

- The cache validation used the normal client-style sequence:
  - `GET /api/CacheSync/1`
  - `POST /api/CacheSync/1/sync`
- Cache sync completed successfully for Product Owner `1`.
- Final cache state from `GET /api/CacheSync/1`:
  - `syncStatus = 2` (`Success`)
  - `workItemCount = 6743`
  - `pullRequestCount = 0`
  - `pipelineCount = 0`
  - `lastErrorMessage = null`
- `GET /api/CacheSync/1/insights` confirmed a non-trivial dataset:
  - `WorkItems = 6743`
  - `Metrics = 25`
  - `Validations = 1268`
  - `SprintProjections = 24`
  - `Relationships = 6740`
- Persisted projection artifacts were present in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/potool.db`:
  - `ResolvedWorkItems = 6743`
  - `SprintMetricsProjections = 24`
  - `PortfolioFlowProjections = 24`
- `GET /api/sprints?teamId=4` returned 5 sprint definitions after sync, confirming sprint projection prerequisites were built successfully.

## Pages

Validated routes and endpoint-backed surfaces after the successful sync:

| Surface | Route / endpoint | Result |
| --- | --- | --- |
| Backlog health | `/home/health` | Loaded successfully with populated product cards and story-point wording (`ready story points`, `sp`). Screenshot evidence: `https://github.com/user-attachments/assets/a3e686d3-dd0e-4e09-95b9-7c017d49a2e2` |
| Sprint delivery | `/home/delivery/sprint` | Loaded successfully with sprint navigation, summary cards, `Delivered Story Points`, and product delivery rows. |
| Sprint execution | `/home/delivery/execution` | Loaded successfully and rendered the controlled empty state `No sprints found. Select a team to view sprint execution data.` instead of throwing. |
| Forecast | `/workspace/analysis/forecast` | Loaded successfully. Calculating Epic `1002` rendered `Total Story Points`, `Delivered Story Points`, and `Remaining Story Points`. Screenshot evidence: `https://github.com/user-attachments/assets/99b051a0-9341-4a3f-95a1-94f060e3d9f4` |
| Portfolio progress | `/home/portfolio-progress` | Loaded successfully and rendered the shell plus `Select a sprint range to view portfolio progress data.` empty state without binding failures. |
| Portfolio delivery | `/home/delivery/portfolio` | Loaded successfully and rendered the shell plus `Select a team to load sprints.` empty state without runtime failures. |
| Effort analytics | `/workspace/analysis/effort` | Loaded successfully with `Total Effort`, `Effort Heat Map`, `Effort by Iteration`, and utilization content. |
| Effort estimation quality | `GET /api/metrics/effort-estimation-quality?productOwnerId=1&maxIterations=6` | Serialized successfully with `qualityByType` (5 entries), `trendOverTime` (6 entries), and `averageEstimationAccuracy = 0.3988004168187705`. |
| Effort estimation suggestions | `GET /api/metrics/effort-estimation-suggestions?productOwnerId=1&onlyInProgressItems=true` | Serialized successfully as an array with 42 suggestions. |

## Regressions

- Forecast bindings are canonical only after Phase 3 cleanup:
  - `GET /api/metrics/epic-forecast/1002?maxSprintsForVelocity=6` returned `totalStoryPoints`, `doneStoryPoints`, `deliveredStoryPoints`, and `remainingStoryPoints`
  - the same response did **not** contain `totalEffort`, `completedEffort`, or `remainingEffort`
- Sprint trend bindings are canonical only for story-point progress values:
  - `GET /api/metrics/sprint-trend?productOwnerId=1&sprintIds=1&sprintIds=2&includeDetails=true` returned `featureProgress` and `epicProgress` entries with `totalStoryPoints` and `doneStoryPoints`
  - the validated progress DTO keys did **not** include removed alias fields such as `totalEffort` or `doneEffort`
- Portfolio delivery mappings remained stable for the validated sample:
  - `GET /api/metrics/portfolio-delivery?productOwnerId=1&sprintIds=1&sprintIds=2` serialized successfully
  - summary totals remained effort-based where intended (`totalCompletedEffort`)
  - no missing-field serialization or binding error was observed on `/home/delivery/portfolio`
- Semantic label checks remained internally consistent:
  - story-point surfaces used story-point wording on `/home/health`, `/home/delivery/sprint`, and `/workspace/analysis/forecast`
  - effort surfaces used effort wording on `/workspace/analysis/effort` and the effort-estimation endpoints
  - no mixed-label regression was observed on the validated routes
- Blocking issue fixed during this pass:
  - mock startup now seeds TFS configuration together with the mock profiles/products/teams
  - cache sync no longer fails on a fresh clone with `TFS configuration not found`

## Decision

**Decision: SYSTEM IS READY FOR REAL USAGE**

Reasoning:

- the solution builds cleanly and the app starts in default `Development` mode without overrides
- onboarding/bootstrap endpoints work on a fresh clone and the seeded mock profiles/products are usable immediately
- cache sync succeeds and produces a non-trivial dataset with resolved work items and projections
- the validated delivery, forecast, portfolio, and effort surfaces render without runtime or binding failures
- forecast and sprint-trend transport bindings now use canonical StoryPoints-only fields where Phase 3 removed aliases
- no semantic inconsistency was observed between story-point pages and effort-based pages during this final validation pass
