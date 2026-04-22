# Phase 28c sync-gate unblock

## Scope

- DEBUG / UNBLOCKING phase only
- No planning logic changes
- No execution hint logic changes
- No CDC slice changes
- No interpretation changes
- No UX changes

## 1. Reproduction

- VERIFIED: startup redirected to `/sync-gate?returnUrl=%2Fhome`.
- VERIFIED: before the fix, first-load sync could fail during cache-state creation with:
  - `SQLite Error 19: 'UNIQUE constraint failed: ProductOwnerCacheStates.ProductOwnerId'`
- VERIFIED: on clean startup, sync then appeared stuck at `ComputeSprintTrends` with `stageProgressPercent = 0` for an extended period even though the stage was still doing work.
- VERIFIED: the original validation blocker was reproducible in the Battleship mock environment using Phase 28b data.

## 2. Root cause explanation

### ROOT CAUSE

1. **Concurrent cache-state creation race**
   - `GET /api/CacheSync/{productOwnerId}` and the background sync path both relied on `CacheStateRepository.GetOrCreateEntityAsync(...)`.
   - That helper created a new `ProductOwnerCacheStateEntity` but did **not** persist it immediately.
   - Under first-load startup, one request could save the row while the sync pipeline still held a different unsaved added entity for the same `ProductOwnerId`.
   - When the sync pipeline tried to save, SQLite enforced the unique index and threw `UNIQUE constraint failed: ProductOwnerCacheStates.ProductOwnerId`.
   - Result: the startup sync could fail before meaningful progress was persisted, leaving sync-gate polling an idle/non-terminal state.

2. **`ComputeSprintTrends` progress was invisible**
   - `SprintTrendProjectionSyncStage` only wrote `0` at stage start and `100` at stage completion.
   - `SprintTrendProjectionService` did real projection work for a noticeable amount of time, but sync-gate had no persisted intermediate progress to show.
   - Result: sync-gate looked frozen at `ComputeSprintTrends` even when the stage was actively computing.

## 3. Fix implemented

### FIXED

1. **Concurrency-safe cache-state creation**
   - File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/CacheStateRepository.cs`
   - Change:
     - `GetOrCreateCacheStateAsync(...)` now reuses `GetOrCreateEntityAsync(...)`.
     - `GetOrCreateEntityAsync(...)` now persists a newly created cache-state row immediately.
     - If the initial insert loses a race, the repository detaches the pending entity, re-queries the existing row, and reuses it instead of failing startup sync.

2. **Visible progress during sprint-trend computation**
   - Files:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs`
   - Change:
     - `SprintTrendProjectionService.ComputeProjectionsAsync(...)` now supports an optional async progress callback.
     - The projection loop reports intermediate progress as sprint/product projection work completes.
     - `SprintTrendProjectionSyncStage` persists that progress back to the cache-state row through a separate scope so sync-gate can see advancing progress while the stage runs.

3. **Regression coverage**
   - Files:
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Repositories/CacheStateRepositoryTests.cs`
     - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
   - Added tests for:
     - concurrent cache-state creation recovery
     - intermediate sprint-trend progress reporting

## 4. Code references

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/CacheStateRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/SprintTrendProjectionSyncStage.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Repositories/CacheStateRepositoryTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 5. Validation results

### VERIFIED

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
  - Result: passed

- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --filter "CacheStateRepositoryTests|SprintTrendProjectionServiceSqliteTests" --no-build`
  - Result: passed
  - Summary: `Passed: 16, Failed: 0`

- Clean startup validation with the real app in mock mode:
  - `GET /api/startup-state?returnUrl=%2Fhome`
    - before sync completion: routed to `/sync-gate`
    - after sync completion: routed to `/home`
  - Browser validation:
    - sync-gate rendered on startup
    - after sync completion, browser navigated to `/home`

- Progress evidence during the previously misleading stage:
  - OBSERVED via `/api/CacheSync/1` during a clean startup run:
    - `currentSyncStage = "ComputeSprintTrends", stageProgressPercent = 77`
    - then `currentSyncStage = "ComputeSprintTrends", stageProgressPercent = 97`
    - then the pipeline advanced to `ComputeForecastProjections`
    - then later completed with `syncStatus = Success`

## Final section

### ROOT CAUSE

- ROOT CAUSE: a first-load race in cache-state creation could break the background sync before startup had a stable cache-state row.
- ROOT CAUSE: `ComputeSprintTrends` had no persisted intermediate progress, so the gate looked frozen even while work was still running.

### FIXED

- FIXED: cache-state creation now survives duplicate-row races by persisting immediately and reusing the winner row.
- FIXED: sprint-trend computation now reports persisted intermediate progress during the long-running stage.

### VERIFIED

- VERIFIED: targeted regression tests pass.
- VERIFIED: solution build passes.
- VERIFIED: sync-gate now shows advancing progress through `ComputeSprintTrends`.
- VERIFIED: sync-gate exits to `/home` after sync success on a clean startup run.

### RISK

- RISK: `stageProgressPercent` remains at the last reported non-zero value after sync success instead of resetting to `0`; this did not block startup completion, but it is a cleanup candidate if success-state progress display semantics need to be stricter.
- RISK: first-run sync duration is still substantial because the Battleship mock dataset is large; the fix makes progress visible and startup recoverable, but it does not make the underlying computation itself faster.

## GO / NO-GO for re-running Phase 28

- GO — sync-gate now completes on a clean startup run, the planning board is reachable again, and the Phase 28 real-usage validation can be re-run against the unblocked startup flow.
