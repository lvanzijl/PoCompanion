# CDC Critical Fixes

## Summary of fixes

This corrective phase removes write-on-read behavior from the portfolio CDC query path, moves snapshot capture behind an explicit write boundary, makes snapshot persistence idempotent under concurrency, and allows empty portfolio snapshots to be stored as first-class state.

Confirmed outcomes:

1. No write-on-read remains in `PortfolioReadModelStateService` or the portfolio query services.
2. Snapshot capture now runs through `PortfolioSnapshotCaptureOrchestrator` and the explicit `POST /api/portfolio/snapshots/capture` boundary.
3. `SnapshotId` is database-controlled; the application no longer computes `MAX(SnapshotId) + 1`.
4. Persistence enforces uniqueness on `(ProductId, TimestampUtc, Source)` and treats duplicate inserts as idempotent retries.
5. Empty snapshot headers are persisted and remain queryable.
6. Selection and trend ordering use `TimestampUtc` first and `SnapshotId` second.
7. Queries read only persisted snapshots.
8. Focused tests were added for read-only behavior, idempotent persistence, empty snapshots, ordering, DI, and the implementation report.
9. Release build and tests were run for validation.

## Before vs after architecture

### Before

- GET-backed query flows loaded current source data
- query flows built snapshots with `PortfolioSnapshotFactory`
- query flows persisted new snapshots before returning data
- repeated reads could mutate the database

### After

- GET-backed query flows only use `PortfolioSnapshotSelectionService` and persisted snapshot tables
- snapshot creation is isolated in `PortfolioSnapshotCaptureOrchestrator`
- capture is triggered only from the explicit write boundary:
  - `POST /api/portfolio/snapshots/capture`
- retries reuse existing persisted snapshots instead of creating duplicates

## Persistence model changes

- Removed application-side `MAX(SnapshotId) + 1` allocation from `PortfolioSnapshotPersistenceService`
- kept `SnapshotId` database-generated
- added a unique database index on `(ProductId, TimestampUtc, Source)`
- on duplicate insert:
  - catch provider uniqueness violations
  - clear the failed tracked insert
  - reload the canonical persisted snapshot
- legacy duplicate logical snapshots are not merged in memory; selection logs a warning and chooses a single canonical row per logical key

## Empty snapshot handling

- `PortfolioSnapshot` no longer rejects zero-item snapshots
- `PortfolioSnapshotCaptureOrchestrator` persists a header even when a product has no captured inputs for a source
- header-only snapshots are returned through the existing selection/query path
- trend, comparison, and signal queries now treat empty persisted snapshots as valid history rather than auto-capturing replacements

## Ordering guarantees

Ordering is consistent across persisted reads:

- primary key for chronology: `TimestampUtc`
- secondary tie-break: `SnapshotId`

Applied in:

- `PortfolioSnapshotSelectionService.GetLatestAsync`
- `PortfolioSnapshotSelectionService.GetPreviousAsync`
- `PortfolioSnapshotSelectionService.GetLatestBeforeAsync`
- grouped portfolio history selection
- `PortfolioTrendAnalysisService`

This removes ambiguity when multiple persisted snapshots share the same timestamp.

## Test coverage

Added or updated tests for:

- no write-on-read in `PortfolioReadModelStateServiceTests`
- explicit capture boundary in `PortfolioSnapshotCaptureOrchestratorTests`
- empty snapshot factory and persistence coverage
- concurrent/idempotent persistence in `PortfolioSnapshotPersistenceServiceTests`
- equal-timestamp ordering with `SnapshotId` tie-break
- API verb coverage for GET-only read endpoints and explicit POST capture endpoint
- DI registration and report presence audits

## Migration notes

The schema now requires uniqueness on `(ProductId, TimestampUtc, Source)`.

Migration strategy:

1. detect duplicate logical snapshots before creating the unique index
2. keep one canonical row per logical key
3. delete duplicate headers and their dependent items
4. create the unique index

`SnapshotId` was already stored as a database identity/autoincrement key in the snapshot schema; this phase removes the remaining application-side override so new inserts rely on the database identity only.

## Files changed

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Controllers/PortfolioSnapshotsController.cs`
- `PoTool.Api/Handlers/Metrics/CapturePortfolioSnapshotsCommandHandler.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs`
- `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs`
- `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs`
- `PoTool.Core/Metrics/Commands/CapturePortfolioSnapshotsCommand.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/PortfolioSnapshotComparisonService.cs`
- `PoTool.Shared/Metrics/PortfolioSnapshotCaptureResultDto.cs`
- `PoTool.Tests.Unit/Audits/CdcCriticalFixesDocumentTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Controllers/MetricsControllerPortfolioReadTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotCaptureOrchestratorTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotFactoryTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`

## Build/test results

- `dotnet build PoTool.sln --configuration Release`
- targeted CDC unit tests covering snapshot/query/controller/DI/audit paths
- `dotnet test` run after the corrective implementation

## Remaining risks

- Existing deployed databases still need the migration applied before the uniqueness guarantee is enforced at the schema level.
- Legacy duplicates are handled safely at read time, but migration cleanup is still the authoritative fix.
- The explicit POST capture endpoint is available immediately; if scheduled/background capture is introduced later, it must continue to use `PortfolioSnapshotCaptureOrchestrator` and must not reintroduce GET-side writes.
