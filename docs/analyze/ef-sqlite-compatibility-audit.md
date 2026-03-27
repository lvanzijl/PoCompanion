# EF Core SQLite Compatibility Report

## Issues found

### CRITICAL

1. **`PoTool.Api/Services/WorkItemBackfillStartProvider.cs` → `GetEarliestChangedDateUtcAsync(...)`**
   - Previous query:
     ```csharp
     var earliest = await context.WorkItems
         .Where(w => workItemIds.Contains(w.TfsId) && w.CreatedDate != null)
         .Select(w => w.CreatedDate)
         .ToListAsync(cancellationToken);

     return earliest.Count > 0 ? earliest.Min() : null;
     ```
   - Why unsafe on SQLite:
     - forced client-side aggregation on a `DateTimeOffset` column to avoid provider translation limits
     - materialized every matching row before calculating the minimum
     - touched CDC backfill boundary selection, so failure or scale issues would affect history ingestion

### HIGH

1. **`PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `GetCommentsAsync(...)`**
   - Previous query:
     ```csharp
     var entities = await _dbContext.PullRequestComments
         .AsNoTracking()
         .Where(c => c.PullRequestId == pullRequestId)
         .ToListAsync(cancellationToken);

     return entities.OrderBy(c => c.CreatedDate).Select(MapCommentToDto);
     ```
   - Why unsafe on SQLite:
     - relied on client-side ordering over `DateTimeOffset`
     - bypassed the repository’s existing SQLite-safe `CreatedDateUtc` ordering pattern
     - had no stable tie-breaker for identical timestamps

2. **Diagnostic aggregate queries**
   - Files / methods:
     - `PoTool.Api/Services/CachedPullRequestReadProvider.cs` → `LogEmptyResultDiagnosticsAsync(...)`
     - `PoTool.Api/Services/CachedPipelineReadProvider.cs` → `LogEmptyRunsDiagnosticsAsync(...)`
     - `PoTool.Api/Services/Sync/SyncPipelineRunner.cs` → `LogPersistenceDiagnosticsAsync(...)`
   - Previous query shape:
     ```csharp
     .GroupBy(_ => 1)
     .Select(g => new { Min = g.Min(...Utc), Max = g.Max(...Utc) })
     .FirstOrDefaultAsync(cancellationToken)
     ```
   - Why unsafe on SQLite:
     - more translation-sensitive than necessary for simple scalar diagnostics
     - failures would surface in operational logging paths rather than business paths

3. **SQLite coverage gaps for EF query paths**
   - Files updated for provider-backed coverage:
     - `PoTool.Tests.Unit/Handlers/GetPortfolioDeliveryQueryHandlerTests.cs`
     - `PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs`
     - `PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
     - `PoTool.Tests.Unit/Handlers/GetSprintTrendMetricsQueryHandlerSqliteTests.cs`
     - `PoTool.Tests.Unit/Services/CachedReadProviderDiagnosticsTests.cs`
     - `PoTool.Tests.Unit/Services/CachedPullRequestReadProviderSqliteTests.cs`
     - `PoTool.Tests.Unit/Services/WorkItemBackfillStartProviderSqliteTests.cs`
     - `PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
   - Why unsafe on SQLite:
     - `UseInMemoryDatabase(...)` does not prove SQL translation or SQLite ordering/aggregate behavior

### MEDIUM

1. **Unchanged InMemory-only tests remain elsewhere in the solution**
   - These were outside the confirmed CRITICAL/HIGH query paths addressed here.
   - Risk: they still do not prove SQLite translation for unrelated handlers.

2. **Some EF query paths still use client-side ordering or materialization outside this audit’s fix scope**
   - No confirmed SQLite crash was reproduced for those paths during this change.
   - Risk remains limited to future provider-specific regressions in untouched areas.

## Fixes applied

1. **Backfill boundary aggregation moved to a SQLite-safe UTC scalar**
   - Added `WorkItemEntity.CreatedDateUtc`.
   - Populated it during work-item persistence in:
     - `PoTool.Api/Repositories/WorkItemRepository.cs`
     - `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`
   - Added migration:
     - `PoTool.Api/Migrations/20260327090926_AddWorkItemCreatedDateUtcForSqliteBackfill.cs`
     - `PoTool.Api/Migrations/20260327090926_AddWorkItemCreatedDateUtcForSqliteBackfill.Designer.cs`
   - New query:
     ```csharp
     var earliestCreatedDateUtc = await context.WorkItems
         .Where(w => workItemIds.Contains(w.TfsId) && w.CreatedDateUtc != null)
         .MinAsync(w => w.CreatedDateUtc, cancellationToken);
     ```
   - Tradeoff:
     - adds one persisted UTC helper column and migration
     - avoids client-side `DateTimeOffset` aggregation and preserves existing “earliest created date” intent

2. **Cached PR comments now use server-side UTC ordering**
   - Updated query to:
     ```csharp
     .OrderBy(c => c.CreatedDateUtc)
     .ThenBy(c => c.InternalId)
     ```
   - Tradeoff:
     - pushes sorting to the database
     - preserves comment chronology while making equal timestamps deterministic

3. **Diagnostic min/max queries simplified**
   - Replaced constant-key `GroupBy` projections with separate `MinAsync` / `MaxAsync` calls on UTC columns.
   - Tradeoff:
     - two scalar queries instead of one shaped aggregate
     - simpler SQL and lower provider-translation risk

4. **SQLite-backed tests added or converted for fixed query paths**
   - Added direct SQLite coverage for:
     - cached PR comment ordering
     - backfill boundary aggregation
     - snapshot source lookup
     - handler execution paths that previously depended on InMemory-only coverage
   - Tradeoff:
     - slightly heavier tests than InMemory
     - materially better provider-specific confidence

## Behavior changes

- **NONE intended**
- CDC behavior remains the same:
  - the backfill provider still derives the earliest relevant boundary from work-item creation metadata
  - only the query mechanism changed from client-side `DateTimeOffset` aggregation to server-side UTC scalar aggregation
- Aggregation math remains unchanged.
- Comparison logic remains unchanged.
- No hidden data loss was introduced; existing `CreatedDate` values are backfilled into `CreatedDateUtc` during migration.

## Remaining risks

1. Some unrelated handler tests in the solution still use `UseInMemoryDatabase(...)`.
2. This change intentionally did not refactor untouched EF query paths that were not confirmed as CRITICAL/HIGH during this audit.

## Notes for reviewers

- Scope is limited to EF Core / SQLite compatibility.
- No CDC domain rules were changed.
- No delivery, planning, or aggregation semantics were changed.
- Migration verification completed locally against SQLite:
  - upgrade to `20260327090926_AddWorkItemCreatedDateUtcForSqliteBackfill`
  - rollback to `20260327061554_CdcCriticalFixes`
  - re-apply to `20260327090926_AddWorkItemCreatedDateUtcForSqliteBackfill`
- Focused validation completed with:
  - `dotnet build PoTool.sln --configuration Release`
  - targeted SQLite-backed unit tests covering the fixed production query paths
