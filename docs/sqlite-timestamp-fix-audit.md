# SQLite timestamp translation fix audit

## Scope

This change set fixes remaining SQLite translation risks for persisted timestamp predicates/sorts in the activity-ledger and pipeline-run paths, and adds a guardrail to prevent indexed `DateTimeOffset` columns from being used in SQLite server-side query paths.

## Inventory of changed occurrences

| File | What was wrong | Change made |
|---|---|---|
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | SQLite translation failure in `ComputeProjectionsAsync` due to `DateTimeOffset` range filter on `EventTimestamp`. | Converted range bounds to UTC `DateTime` variables outside query and switched predicates to `EventTimestampUtc`. |
| `PoTool.Api/Services/LedgerActivityEventSource.cs` | Server-side filters/sort on `EventTimestamp` (`DateTimeOffset`) in EF query path. | Switched `Where` and `OrderBy` to `EventTimestampUtc` with UTC `DateTime` bounds computed before query. |
| `PoTool.Api/Services/CacheManagementService.cs` | Server-side filters/sorts on `EventTimestamp` in activity-ledger validation query. | Switched filter/sort predicates to `EventTimestampUtc`; converted inbound range values to UTC `DateTime` outside query. |
| `PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs` | No persisted UTC predicate/sort timestamp for activity events. | Added `EventTimestampUtc : DateTime`. |
| `PoTool.Api/Services/ActivityEventIngestionService.cs` | Writes only populated `EventTimestamp` (`DateTimeOffset`). | Writes now also set `EventTimestampUtc = update.RevisedDate.UtcDateTime`. |
| `PoTool.Api/Persistence/Entities/CachedPipelineRunEntity.cs` | Indexed pipeline completion timestamp used `DateTimeOffset` (`FinishedDate`). | Added `FinishedDateUtc : DateTime?` for SQLite-safe indexing/predicate/sort usage. |
| `PoTool.Api/Services/Sync/PipelineSyncStage.cs` | Writes only populated `FinishedDate` (`DateTimeOffset?`). | Writes now also set `FinishedDateUtc = dto.FinishTime?.UtcDateTime`. |
| `PoTool.Api/Persistence/PoToolDbContext.cs` | Indexed `DateTimeOffset` columns (`ActivityEventLedgerEntries.EventTimestamp`, `CachedPipelineRuns.FinishedDate`) can reintroduce translation issues. | Replaced indexes with `EventTimestampUtc` and `FinishedDateUtc`; added SQLite guardrail that fails if any indexed mapped property is `DateTimeOffset`. |
| `PoTool.Api/Migrations/20260226121512_AddUtcTimestampColumnsForSqliteActivityAndPipeline.cs` | Missing schema support/backfill for new UTC columns and index move. | Added `EventTimestampUtc` and `FinishedDateUtc`, backfilled from legacy columns, replaced indexes to UTC columns. |
| `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs` | No SQLite regression test for sprint projection timestamp translation/guardrail. | Added SQLite test for `ComputeProjectionsAsync` translation safety and model enforcement test that indexed `DateTimeOffset` properties are empty. |

## Copy-paste proposal for .github-instructions

```text
SQLite timestamp rule (strict):
- Do not use DateTimeOffset EF-mapped columns in server-side predicate/sort/aggregate query paths.
- Persist queryable timestamps as UTC DateTime columns with *Utc suffix (for example, EventTimestampUtc, CreatedDateUtc, FinishedDateUtc).
- Convert DateTimeOffset -> UTC DateTime at write time (`value.UtcDateTime`).
- Compute UTC DateTime bounds outside LINQ queries; use those bounds inside Where/OrderBy/Min/Max.

Do:
- `var fromUtc = from.Value.UtcDateTime; query = query.Where(x => x.EventTimestampUtc >= fromUtc);`
- `query = query.OrderBy(x => x.CreatedDateUtc);`

Don’t:
- `query = query.Where(x => x.EventTimestamp >= from);` (DateTimeOffset in EF predicate on SQLite)
- `query = query.OrderBy(x => x.SomeDateTimeOffsetColumn);`
- Force client evaluation (`AsEnumerable`, `ToList`) just to bypass translation.

Common failure patterns to block:
- Range filters (`>=`, `<=`) on DateTimeOffset columns
- Watermark comparisons on DateTimeOffset columns
- Min/Max/OrderBy/ThenBy on DateTimeOffset columns

Guardrails:
- Keep SQLite model guardrail enabled (fails startup if an indexed mapped DateTimeOffset property exists).
- Keep SQLite translation tests enabled (including sprint trend projection execution test).
```
