# CDC Mock Dataset Report

## Dataset structure

The mock dataset is implemented by `PoTool.Tests.Unit/Services/CdcTestDataSeeder.cs` and seeds deterministic SQLite data through the existing `PortfolioSnapshotPersistenceService`.

Products:

1. Product A — Active evolving product
2. Product B — Empty → becomes active
3. Product C — Completed product

Snapshots per product:

| Product | Snapshots | Notes |
| --- | --- | --- |
| Product A | 6 | Sprint 1, Sprint 2, Sprint 3, Sprint 4A, Sprint 4B, Sprint 5 |
| Product B | 6 | Empty portfolio, Sprint 2, Sprint 3, Sprint 4A, Sprint 4B, Sprint 5 |
| Product C | 6 | Sprint 1, Sprint 2, Sprint 3, Sprint 4A, Sprint 4B, Sprint 5 |

Total seeded portfolio snapshot headers: 18  
Total seeded portfolio snapshot items: 53

Work-package structure:

- Product A
  - active evolving work package (`A-FEAT-1`)
  - added mid-stream work package (`A-FEAT-2`)
  - removed and re-added same key (`A-FEAT-3`)
  - zero-weight stable row (`A-FEAT-0`)
- Product B
  - empty owner state followed by first active scope
  - mixed project-level and work-package scope across different projects
  - new latest work package for signal generation (`B-FEAT-3`)
- Product C
  - all active rows converge to progress `1.0`
  - latest completed product aggregation weight = `15`

## Covered scenarios

- Empty owner state
  - Product B first snapshot is `Empty portfolio` at `UnixEpoch` with zero items.
- Transition from empty → non-empty
  - Product B remains empty at `Sprint 2`, then receives active scope at `Sprint 3`.
- Identical timestamps
  - `Sprint 4A` and `Sprint 4B` share the same `TimestampUtc` (`2026-03-28T00:00:00Z`) for all three products and must be ordered by `SnapshotId`.
- Fallback timestamp cases
  - `Empty portfolio` uses `UnixEpoch`.
  - Product B `Sprint 2` is an empty snapshot using the latest sprint fallback timestamp.
- snapshotCount scenarios
  - The dataset supports `snapshotCount = 1`, `snapshotCount = 2`, and `snapshotCount > available` because the owner-level history contains 7 logical groups and Product B contains 6 product-level snapshots.
- Completed product
  - Product C ends with all active items at progress `1.0`, producing completed product progress = `1.0`.
- Lifecycle variety
  - new → in progress → done via progress evolution on `A-FEAT-1`, `B-FEAT-1`, and Product C rows
  - added mid-stream via `A-FEAT-2` and `B-FEAT-2`
  - removed via retired `A-FEAT-3`
  - re-added with same key via `A-FEAT-3` in `Sprint 4B`
- Aggregation and signal coverage
  - latest portfolio progress = `0.8619047619`
  - latest total active weight = `42`
  - signal set includes progress improving, weight decreasing, new work package, retired work package, and repeated no-change

## How to run

1. Create a SQLite-backed `PoToolDbContext`.
2. Call `await new CdcTestDataSeeder(context).SeedAsync(cancellationToken)`.
3. Query the persisted data through the existing read-model services.

The repository validates the dataset with:

```bash
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~CdcTestDataSeederTests|FullyQualifiedName~CdcMockDatasetReportDocumentTests" -v minimal
```

The seeder is deterministic and idempotent:

- running it twice does not create duplicate snapshot headers or items
- it reuses existing profile, product, team, and sprint rows
- it persists snapshots through the existing CDC persistence service rather than inserting inconsistent rows directly

## What to verify manually

Concrete API/service checks after seeding:

1. Snapshot retrieval and ordering
   - `PortfolioSnapshotSelectionService.GetLatestAsync(ProductA)` returns `Sprint 5`
   - `PortfolioSnapshotSelectionService.GetPreviousAsync(ProductA)` returns `Sprint 4B`
   - owner history is ordered `Sprint 5`, `Sprint 4B`, `Sprint 4A`, `Sprint 3`, `Sprint 2`, `Sprint 1`, `Empty portfolio`
2. Grouping
   - `GetPortfolioSnapshotBySourceAsync` for `Sprint 2` groups Product A, Product B, and Product C correctly
   - Product B contributes an empty snapshot while Product A and Product C contribute seven combined items
3. Trends
   - `snapshotCount = 1` yields null deltas for portfolio progress and total weight
   - `snapshotCount >= 2` yields positive progress delta and weight delta `-1`
4. Signals
   - `PortfolioDecisionSignalQueryService` returns empty results when only one snapshot is selected
   - with full history it returns the expected signal types for identical timestamps, new scope, retired scope, and repeated no-change
5. Empty snapshots
   - Product B `Empty portfolio` and `Sprint 2` are queryable and both have zero items
6. Aggregation
   - Product C latest aggregation reaches completed product progress = `1.0`
   - owner-level latest progress and weight match the expected seeded values
