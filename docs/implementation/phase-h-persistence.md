# Phase H Snapshot Persistence & Selection Policy

## Summary

Implemented durable CDC portfolio snapshot persistence, explicit persisted snapshot selection, and strict integrity handling so portfolio comparison no longer depends on hidden transient snapshot truth.

Phase H now provides:

- dedicated persisted CDC snapshot headers and items via `PortfolioSnapshotEntity` and `PortfolioSnapshotItemEntity`
- explicit domain ↔ persistence mapping through `PortfolioSnapshotPersistenceMapper`
- persisted-only selection via `PortfolioSnapshotSelectionService`
- validation-first capture flow that fails fast on missing required business keys
- default archival exclusion without deleting historical data
- comparison/query integration that reads current and previous snapshots from persisted selection

Requested acceptance checks:

1. Snapshot persistence entities created — yes (`PortfolioSnapshotEntity`, `PortfolioSnapshotItemEntity`)
2. Domain ↔ persistence mapping explicit and correct — yes (`PortfolioSnapshotPersistenceMapper`, `PortfolioReadModelMapper`)
3. Invalid snapshots never persist — yes (capture fails before persistence on missing required keys)
4. Selection service defines latest explicitly — yes (`TimestampUtc` desc, then `SnapshotId` desc)
5. Archived snapshots excluded by default — yes (opt-in includeArchived only)
6. No silent row skipping remains — yes (missing `ProjectNumber` throws)
7. Comparison uses persisted selection — yes (`PortfolioReadModelStateService` + `PortfolioComparisonQueryService`)
8. Determinism preserved — yes (explicit sort/tie-break rules)
9. Tests added and passing — yes (see commands below)
10. Build succeeds — yes (see commands below)

## Persistence model

Added separate CDC persistence entities under `PoTool.Api/Persistence/Entities`:

- `PortfolioSnapshotEntity`
- `PortfolioSnapshotItemEntity`

Persisted header fields:

- `SnapshotId`
- `TimestampUtc`
- `ProductId`
- `Source`
- `CreatedBy`
- `IsArchived`

Persisted item fields:

- `SnapshotId`
- `ProjectNumber`
- `WorkPackage`
- `Progress`
- `TotalWeight`
- `LifecycleState`

The CDC persistence model is separate from roadmap snapshots and does not reuse roadmap semantics.

## Selection policy

Added `PortfolioSnapshotSelectionService` for explicit retrieval from persisted snapshots only.

Default selection rules:

- use persisted snapshots only
- exclude `IsArchived = true` snapshots unless explicitly included
- define latest as:
  - highest `TimestampUtc`
  - if timestamps are equal, highest `SnapshotId`

Supported selection queries:

- latest snapshot for a product
- previous snapshot for a product
- latest snapshot before a given timestamp for a product
- latest/current and previous persisted portfolio groups for a product-owner portfolio using the same persisted source and timestamp anchor

This removes insertion-order ambiguity and hidden in-memory fallback logic.

## Validation/persistence flow

The capture flow now follows this order:

1. derive source candidates from sprint-backed CDC data
2. build canonical `PortfolioSnapshotFactoryEpicInput` rows
3. fail immediately if required business keys such as `ProjectNumber` are missing
4. create the canonical domain snapshot through `PortfolioSnapshotFactory`
5. persist the validated snapshot as one atomic header+items write
6. retrieve current/previous snapshots only through persisted selection

Invalid snapshots are not stored. There is no header-only persistence and no partial row persistence.

## Integrity handling

Missing required business keys are no longer skipped silently. `PortfolioSnapshotCaptureDataService` throws before persistence when `ProjectNumber` is missing.

Persisted reads also validate integrity explicitly:

- persistence mapping recreates domain snapshot rows without coercing missing values
- corrupted persisted rows surface an explicit `InvalidOperationException`
- archived snapshots remain queryable when explicitly requested, but default selection excludes them

## Test coverage

Added focused coverage for:

- full snapshot persistence
- invalid snapshot non-persistence on missing business keys
- archived snapshot exclusion by default
- latest / previous / latest-before selection
- deterministic tie-break behavior
- corrupted persisted-row surfacing
- persisted current/previous selection inside read-model and comparison integration
- report and DI registration coverage

## Files changed

- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Api/Services/PortfolioReadModelMapper.cs`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs`
- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs`
- `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs`
- `PoTool.Api/Services/PortfolioSnapshotSelectionService.cs`
- `PoTool.Tests.Unit/Audits/PhaseHPersistenceDocumentTests.cs`
- `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioReadModelMapperTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
- `PoTool.Tests.Unit/Services/PortfolioSnapshotPersistenceServiceTests.cs`
- `docs/implementation/phase-h-persistence.md`

## Build/test results

Required checks for this phase:

- `dotnet build PoTool.sln --configuration Release`
- relevant `dotnet test` coverage for persistence, selection, integrity, comparison integration, DI, and report audits

Status at implementation time:

- initial focused Phase H test run exposed test-only assertion issues and a SQLite-translatability issue in grouped selection, which were corrected
- `dotnet build PoTool.sln --configuration Release` — passed
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PortfolioSnapshot|FullyQualifiedName~PortfolioReadModelStateServiceTests|FullyQualifiedName~PortfolioReadModelMapperTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~PhaseHPersistenceDocumentTests" -v minimal` — passed
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~PortfolioSnapshot|FullyQualifiedName~PortfolioReadModel|FullyQualifiedName~PortfolioComparison|FullyQualifiedName~MetricsControllerPortfolioRead|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~PhaseHPersistenceDocumentTests" -v minimal` — passed

## Remaining risks for Phase I

- Phase H persists and selects snapshots safely, but it still captures only the latest sprint-backed sources rather than introducing broader retention or scheduled backfill orchestration.
- Archival is supported for exclusion semantics only; explicit archive workflows and archive-specific query surfaces remain future work.
- If Phase I needs broader historical recovery across many missing periods, that should remain a separate retrieval/backfill concern instead of weakening the explicit persisted selection policy introduced here.
