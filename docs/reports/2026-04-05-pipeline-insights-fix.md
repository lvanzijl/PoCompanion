# Pipeline Insights fix

## Summary
- Target route: `/home/pipeline-insights?productId=1&teamId=4&sprintId=2&timeMode=Sprint`
- Mock profile: `Commander Elena Marquez`
- Target context: `productId=1`, `teamId=4`, `sprintId=2` (`Sprint 11`)
- Result after fix: the page now shows **real data** for Battleship mock mode.

## Whether required mock data existed before the fix
- **Both were true** before the fix:
  1. the API initially returned `Cache has not been built for the active profile yet` until the mock profile cache sync was executed
  2. after cache sync, the required Battleship pipeline/run data existed, but the Pipeline Insights API still failed with `Error retrieving pipeline insights`
- Conclusion: the remaining blocker was **not missing Pipeline Insights mock data for the target context**. The remaining blocker was a server-side SQLite query translation failure in the pipeline filter resolution path.

## Exact missing data added, if any
- **No new mock data was added.**
- Validation proved the synced Battleship cache already contained the required data for the target context:
  - cached pipeline rows: `433`
  - target response content after fix:
    - sprint: `Sprint 11`
    - total builds: `5`
    - product: `Incident Response Control`
    - top troubled pipelines: `Battleship.HullIntegrity.CI`, `Battleship.Core.CI`

## Exact root cause
- The failure occurred in `PipelineFilterResolutionService.LoadPipelineDefinitionsAsync`.
- The service projected `PipelineDefinitionEntity` rows into a private record type and then applied `Distinct()`/`OrderBy()` over that projected record in an EF Core SQLite query.
- SQLite could not translate that projected-record query shape, so the Pipeline Insights controller returned a failed data-state envelope with reason `Error retrieving pipeline insights`.
- This happened **after** valid mock data had been synced, so the failure was in the API query layer, not in the Battleship dataset.

## Files changed
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PipelineFilterResolutionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/PipelineFilterResolutionServiceTests.cs`

## What changed
- Moved pipeline-definition filtering to operate on `PipelineDefinitionEntity` before projection.
- Materialized the filtered rows first, then applied record-based `Distinct()` and ordering in memory so SQLite no longer has to translate that record shape.
- Added a SQLite-backed unit test that exercises the scoped product/repository pipeline-definition resolution path and proves it translates/runs under SQLite.

## Validation steps performed
1. Built the solution:
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
2. Ran targeted baseline/updated tests:
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~GetPipelineInsightsQueryHandlerTests"`
3. Ran the API in mock mode and triggered cache sync for profile `1`.
4. Recalled the target API endpoint after sync.
5. Reopened the target UI route and verified visible insight content.
6. Captured a screenshot of the working page.

## API result after fix
Request:
- `GET http://localhost:5291/api/pipelines/insights?productOwnerId=1&sprintId=2&productIds=1&includePartiallySucceeded=true&includeCanceled=false`

Observed result after fix:
- envelope `state`: `2` (available)
- `reason`: `null`
- response data highlights:
  - `sprintName`: `Sprint 11`
  - `totalBuilds`: `5`
  - `failedBuilds`: `2`
  - `warningBuilds`: `1`
  - `p90DurationMinutes`: `50.4`
  - product section:
    - `productName`: `Incident Response Control`
    - `hasData`: `true`
    - `pipelineBreakdown` count: `2`
    - `scatterPoints` count: `5`

Related overlay check:
- `GET http://localhost:5291/api/buildquality/pipeline?productOwnerId=1&sprintId=2&pipelineDefinitionId=1`
- returned available state for the selected pipeline definition.

## Screenshot path/filename of the working page
- Local proof screenshot: `/tmp/playwright-logs/2026-04-05-pipeline-insights-fixed.png`
- Optional user-provided screenshot URL suitable for PR use: `https://github.com/user-attachments/assets/6547dd4e-d2fb-41c0-ba92-d78563a47165`

## Brief note on the final rendered state
- The page now renders **real Pipeline Insights data**, not a generic error state and not an explicit no-data fallback.
- The rendered page shows the expected Battleship context and usable insight content for `Incident Response Control` in `Sprint 11`.
