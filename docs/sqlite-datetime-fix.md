# SQLite DateTime translation fix

## What was changed

Standardized SQLite query predicates/sorts to use persisted UTC `DateTime` columns (while keeping `DateTimeOffset` at DTO/API edges):

- `WorkItems.TfsChangedDateUtc` (backfilled from `TfsChangedDate`)
- `PullRequests.CreatedDateUtc` (backfilled from `CreatedDate`)
- `PullRequestComments.CreatedDateUtc` (backfilled from `CreatedDate`)
- `Sprints.StartDateUtc`, `Sprints.EndDateUtc`, `Sprints.LastSyncedDateUtc` (backfilled from existing DateTimeOffset columns)
- `TfsConfigs.UpdatedAtUtc` (backfilled from `UpdatedAt`)

## Workaround removals/replacements

- Removed client-side PR date filtering in `CachedPullRequestReadProvider` (`ToListAsync` then `Where`).
- Removed client-side comment ordering workaround in `PullRequestRepository` and replaced with server-side order on `CreatedDateUtc`.
- Removed client-side TFS config ordering workaround in `TfsConfigurationService` and replaced with server-side order on `UpdatedAtUtc`.
- Removed client-side sprint ordering workaround in `SprintRepository` and replaced with server-side ordering using UTC `DateTime` columns.
- Updated activity watermark query in `ActivityEventIngestionService` to server-side compare on `TfsChangedDateUtc`.

## Indexes added

- `IX_WorkItems_TfsChangedDateUtc`
- `IX_PullRequests_CreatedDateUtc`
- `IX_PullRequestComments_CreatedDateUtc`
- `IX_TfsConfigs_UpdatedAtUtc`
- `IX_Sprints_TeamId_StartDateUtc`
- `IX_Sprints_TeamId_LastSyncedDateUtc`

## Migration/backfill notes

Migration `20260225091849_AddUtcDateColumnsForSqliteTranslation` adds UTC columns and backfills using SQLite `datetime(...)` conversion from existing offset values.

## Do / Don't

### Do

- Persist predicate/sort timestamps as UTC `DateTime` columns for EF+SQLite queries.
- Convert input `DateTimeOffset` filters to UTC `DateTime` before EF predicates.
- Keep `DateTimeOffset` in external contracts where needed.

### Don’t

- Don’t fix translation issues by moving filtering/sorting to client-side (`AsEnumerable`, `ToList` before `Where`/`OrderBy`).
- Don’t write new SQLite predicates directly against `DateTimeOffset` entity columns.
