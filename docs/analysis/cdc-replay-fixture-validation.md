# CDC Replay Fixture Validation

## Replayable Data Sources

The repository already contains replayable local assets that exercise CDC behavior without any live TFS dependency:

| Source | Location | Replay value |
| --- | --- | --- |
| SQLite-backed projection rebuild tests | `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs` | Persists `WorkItems`, `ResolvedWorkItems`, `ActivityEventLedgerEntries`, `SprintMetricsProjections`, and `PortfolioFlowProjections` in local SQLite and verifies deterministic rebuild behavior. |
| Activity ledger fixtures in service tests | `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` and `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs` | Encodes realistic `System.State`, `System.IterationPath`, and portfolio membership changes that reconstruct delivery and flow semantics from local events. |
| SprintFacts CDC datasets | `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs` | Validates commitment, added scope, removed scope, delivered scope, spillover, and corrected SprintFacts invariants from persisted field-change histories. |
| Effort planning mixes | `PoTool.Tests.Unit/Services/EffortPlanningCdcServicesTests.cs` | Provides realistic effort-hour mixes and keeps effort planning assertions separate from story-point semantics. |
| Exported revision snapshots | `PoTool.Tests.Unit/RecordedPayloads/per_item_revisions_page_1.json` and `PoTool.Tests.Unit/RecordedPayloads/per_item_revisions_page_2.json` | Local exported revision pages that can be replayed into field-change events without any TFS call. |

The replay validation added in this issue reuses these shapes rather than introducing new live connectivity or alternate semantics.

## CDC Replay Tests Added

`PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs` adds local replay coverage over persisted SQLite rows, activity ledger events, and exported revision snapshots.

Added replay validations:

- `RecordedRevisionSnapshots_ReplayLocallyWithoutLiveTfs`
  - replays `RecordedPayloads/per_item_revisions_page_*.json` into deterministic local `FieldChangeEvent` sequences
- `SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`
  - replays persisted sprint history into `SprintFactService`
  - validates commitment, added scope, removed scope, delivered scope, spillover, and remaining scope
- `PortfolioFlow_ReplayFixture_ReconstructsStockInflowAndThroughputDeterministically`
  - replays persisted SQLite product membership and completion history into `PortfolioFlowProjectionService`
  - validates stock, inflow, throughput, and non-negative remaining scope
- `DeliveryTrends_ReplayFixture_RemainsDeterministicAcrossRebuilds`
  - replays persisted SQLite sprint history into `SprintTrendProjectionService`
  - validates stable `SprintMetricsProjectionEntity` outputs across repeated rebuilds
- `Forecasting_ReplayFixture_RemainsStableOverPersistedSprintHistory`
  - reuses persisted sprint projection history as local historical velocity samples
  - validates stable forecast outputs across repeated runs
- `EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes`
  - replays a realistic persisted work-item mix into effort distribution, estimation-quality, and suggestion services
  - validates deterministic outputs and separation from SprintFacts story-point totals

## Invariant Results

Replay validation was checked against the corrected invariants from `docs/analysis/cdc-invariant-tests.md`.

Observed results on replay fixtures:

- SprintFacts
  - `RemainingSP = CommittedSP + AddedSP - RemovedSP - DeliveredSP` holds on the replay fixture
  - no negative remaining scope
  - no spillover outside remaining scope
  - delivered added scope stays within added scope
- PortfolioFlow
  - remaining scope stays non-negative on persisted replay runs
  - stock, inflow, and throughput remain story-point based
- EffortPlanning
  - effort totals equal persisted effort-hour sums from the selected replay mix
  - no effort/story-point cross-mixing

No CDC semantic change was required. The tests only validate existing CDC behavior against replayable local fixtures.

## Determinism Results

Repeated local replay runs produce stable outputs:

- exported revision snapshot replay produces identical `FieldChangeEvent` sequences on repeated loads
- SprintFacts replay returns identical `SprintFactResult` values for the same persisted history
- PortfolioFlow replay returns identical stock/inflow/throughput results and preserves a single persisted projection row per sprint/product pair
- DeliveryTrends replay returns identical `SprintMetricsProjectionEntity` semantic values across repeated rebuilds
- Forecasting replay returns identical velocity, remaining scope, confidence, and projection sequences for the same persisted sprint history
- EffortPlanning replay returns identical distribution, quality, and suggestion outputs for the same persisted work-item mix

These checks validate deterministic repeated outputs without relying on live TFS state.

## Known Fixture Limitations

- The exported revision snapshot assets currently represent a small task-level cross-page revision example, so they are useful for replay plumbing validation but not sufficient on their own for story-point-heavy analytics.
- The SQLite replay fixture is intentionally compact. It covers commitment, churn, spillover, portfolio entry, delivery, and effort-hour mix behavior, but it is not a full production export.
- Replay validation focuses on CDC behavior, not on broad integration coverage of every handler or UI surface.
- Older repository planning/documentation references mention a now-missing mock data rules document; the replay validation added here does not depend on that document.
