# Battleship CDC Extension Report

## What was extended

The existing battleship mock dataset was extended in place inside `MockConfigurationSeedHostedService`.
No separate CDC seeder was introduced, no generic products were added, and the seeded CDC history reuses the default active battleship portfolio:

- Incident Response Control
- Crew Safety Operations

Each seeded snapshot row is derived from existing battleship backlog roots, objectives, and epics that already belong to those products.
The CDC history is deterministic, idempotent on re-run, and stays inside the same mock domain reality that already drives the UI.

## Snapshot timeline

The seeded portfolio history contains eight ordered snapshot groups for the active battleship profile.
Each group is written for both products so the portfolio view can show coherent progression.

1. **Empty portfolio**  
   - Timestamp: `1970-01-01T00:00:00Z`
   - Uses the UnixEpoch fallback timestamp with the existing `"Empty portfolio"` source label.
   - Seeds zero snapshot rows to cover the realistic empty-to-non-empty transition.

2. **Sprint 1 - Initial backlog**  
   - Three existing work packages appear per product.
   - All work starts at `0.0` progress to represent the initial backlog landing in CDC history.

3. **Sprint 2 - Work starts**  
   - The same work packages move into partial progress.
   - The portfolio leaves the empty/new-only state and now has a valid in-flight baseline.

4. **Sprint 3 - New work added**  
   - A fourth existing work package is introduced per product.
   - Earlier work packages continue to advance while new scope lands mid-stream.

5. **Sprint 3 - Completion checkpoint**  
   - Shares the exact same timestamp as the previous snapshot group.
   - Some work packages move close to completion on the same day, so ordering depends on `SnapshotId` after `TimestampUtc`.

6. **Sprint 4 - Reprioritized backlog**  
   - One existing work package per product is retained in history but marked retired.
   - The rest of the portfolio continues forward, covering removal/reprioritization without inventing extra domain entities.

7. **Sprint 5 - Majority completed**  
   - Most active work packages are fully complete.
   - One active work package remains partially complete so CDC deltas still show meaningful motion.

8. **Sprint 6 - Near completion**  
   - All active work packages reach full completion.
   - The retired package remains in history so the portfolio still shows the earlier reprioritization outcome.

## Covered CDC scenarios

- **`snapshotCount` behavior**
  - `N = 1`: the latest snapshot returns only `Sprint 6 - Near completion`.
  - `N = 2`: the latest two snapshots return `Sprint 6 - Near completion` and `Sprint 5 - Majority completed`.
  - `N > available`: requests larger than the history size return all eight seeded snapshot groups.

- **Identical timestamp**
  - `Sprint 3 - New work added` and `Sprint 3 - Completion checkpoint` intentionally share the same `TimestampUtc`.
  - Their ordering relies on `SnapshotId ordering`, not on the timestamp alone.

- **Fallback timestamp**
  - `Empty portfolio` uses the UnixEpoch fallback timestamp so empty-history CDC behavior is covered without a new data world.

- **Mixed progress**
  - Early snapshots show zero progress, mid snapshots show partial completion, and later snapshots reach full completion for active work.

- **Empty → non-empty transition**
  - The timeline starts empty and becomes populated in `Sprint 1 - Initial backlog`.

- **Removed/reprioritized work**
  - The reprioritized snapshot keeps one existing work package with a retired lifecycle state so downstream CDC views can show removed scope.

## How to run

1. Start the application in mock mode using the existing mock configuration path.
2. Let `MockConfigurationSeedHostedService` seed the database on startup.
3. Open the default active profile portfolio views; no separate CDC seed step is required.

For local validation, the extension was exercised with:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~MockConfigurationSeedHostedServiceTests|FullyQualifiedName~BattleshipCdcExtensionReportDocumentTests|FullyQualifiedName~PortfolioReadModelStateServiceTests" -v minimal`

## What to verify

- The default active profile contains portfolio snapshot history for **Incident Response Control** and **Crew Safety Operations**.
- The oldest snapshot is `Empty portfolio` at UnixEpoch and contains no rows.
- The latest snapshot is `Sprint 6 - Near completion`.
- `Sprint 3 - Completion checkpoint` sorts ahead of `Sprint 3 - New work added` despite the identical timestamp because of `SnapshotId` ordering.
- Requests for one, two, or more-than-available snapshots return the expected subset sizes.
- CDC comparison and read-only portfolio panels show:
  - an empty starting state
  - mid-stream scope growth
  - a retired/reprioritized work package
  - a mostly complete and then fully complete ending state
