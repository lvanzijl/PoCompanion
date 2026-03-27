# CDC Fix Report — Empty Snapshot & SnapshotCount

## 1. Summary of Changes
- Fixed the remaining empty-owner CDC correctness gap by updating portfolio snapshot capture so a persisted snapshot is still created when source discovery returns no rows.
- Fixed the historical selection contract so `snapshotCount` is honored exactly instead of rewriting `N = 1` to `2`.
- Added focused regression tests for empty-owner capture, exact snapshot count selection, single-snapshot trend behavior, single-snapshot signal behavior, and non-positive snapshot count behavior.

- File-level overview:
  - `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs`
    - Added an explicit empty-capture path when `GetLatestSourcesAsync` returns no sources.
    - Added deterministic fallback source construction via `BuildEmptyCaptureSourceAsync`.
    - This change fixes the case where an empty owner previously could not persist any snapshot at all.
  - `PoTool.Api/Services/PortfolioReadModelStateService.cs`
    - Removed the implicit `NormalizeSnapshotCount` rewrite.
    - History selection now passes the requested `SnapshotCount` through unchanged.
    - This change restores read-contract correctness for historical queries.
  - `PoTool.Api/Services/PortfolioDecisionSignalQueryService.cs`
    - Added an explicit guard that returns an empty signal set when history has fewer than two snapshots or no comparison baseline exists.
    - This keeps `N = 1` exact while preventing invented change signals.
  - `PoTool.Tests.Unit/Services/PortfolioSnapshotCaptureOrchestratorTests.cs`
    - Added regression coverage for deterministic empty-owner capture and idempotent repeat capture.
  - `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
    - Added regression coverage for exact `SnapshotCount` behavior, available-only history, and non-positive `SnapshotCount`.
  - `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`
    - Added regression coverage for valid empty snapshot query payloads, no delta/direction for single-snapshot trends, and empty signals for single-snapshot requests.
  - `docs/release-notes.json`
    - Added a user-facing release note describing the corrected portfolio CDC behavior.

## 2. Root Cause Analysis
### 2.1 Empty Snapshot Issue
- What caused the issue:
  - Empty snapshot persistence was supported by the snapshot model and persistence layer, but capture still depended on discovering at least one source from resolved work items.
- Where in code it originated:
  - `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs`
    - `GetLatestSourcesAsync` derives sources from `ResolvedWorkItems`.
  - `PoTool.Api/Services/PortfolioSnapshotCaptureOrchestrator.cs`
    - `CaptureLatestAsync` previously returned early when `sources.Count == 0`.
- Why previous implementation failed:
  - A truly empty owner has no resolved work items and therefore no source rows.
  - No source rows meant no capture source.
  - No capture source meant the orchestrator returned without creating a persisted snapshot header.
  - As a result, the empty business state remained historically invisible.

### 2.2 snapshotCount Issue
- Why `N = 1` was rewritten:
  - Historical selection previously normalized any `snapshotCount < 2` to `2`.
- Where this happened:
  - `PoTool.Api/Services/PortfolioReadModelStateService.cs`
  - The removed `NormalizeSnapshotCount` helper was applied inside `GetHistoryStateAsync`.
- Why this breaks correctness:
  - The public read contract said “latest `N` snapshots,” but the implementation silently changed `N = 1` into `N = 2`.
  - That changed what data was selected.
  - It also changed downstream semantics for trend deltas, direction, and signals.
  - The result was deterministic but not faithful to the caller’s explicit request.

## 3. Implemented Solution

### 3.1 Empty Snapshot Strategy
- Exact mechanism used:
  - An explicit empty-capture path was added in `PortfolioSnapshotCaptureOrchestrator`.
  - When normal source discovery returns no sources, the orchestrator now builds a deterministic fallback capture source instead of returning early.
- How `TimestampUtc` is determined:
  - First choice: the most recent dated sprint from `Sprints`, ordered by `EndDateUtc` descending and `Id` descending.
  - Fallback when no dated sprint exists: `DateTime.UnixEpoch`.
- How uniqueness is guaranteed:
  - Persistence still uses the existing logical uniqueness contract on `(ProductId, TimestampUtc, Source)`.
  - The fallback source is deterministic:
    - latest sprint name when a dated sprint exists
    - otherwise `"Empty portfolio"`
  - The timestamp is also deterministic for the same empty-owner state.
- Behavior on repeated capture:
  - Repeated capture resolves to the same logical key.
  - The persistence service detects the existing snapshot and treats the retry idempotently.
  - No duplicate logical snapshot is created.

### 3.2 snapshotCount Handling
- New behavior for:
  - `N = 0`
    - No normalization occurs.
    - The selector receives `0`, returns no snapshots, and `GetHistoryStateAsync` returns `null`.
  - `N = 1`
    - Exactly one snapshot is selected.
    - No hidden rewrite to `2` occurs.
  - `N > available`
    - The selector returns only the available persisted snapshots.
    - No synthetic history is created.
- How trends/signals behave with `N = 1`:
  - Trends:
    - one snapshot point is returned
    - `PreviousValue = null`
    - `Delta = null`
    - `Direction = null`
  - Signals:
    - no comparison baseline exists
    - `PortfolioDecisionSignalQueryService` returns an empty list
    - no invented change signal is produced

## 4. Behavioral Changes

### Before
- Empty owner:
  - A product owner with no resolved work items produced no capture source.
  - Capture returned `SourceCount = 0` and persisted nothing.
- `snapshotCount`:
  - Requests below `2` were silently rewritten to `2`.
  - `N = 1` did not actually mean “latest snapshot only.”
- Single-snapshot downstream behavior:
  - The contract could not be audited cleanly through the history query path because the request was rewritten before selection.

### After
- Empty owner:
  - A product owner with no discovered sources now still produces a persisted empty snapshot.
- `snapshotCount`:
  - The exact requested value is honored.
  - `N = 1` remains `1`.
  - `N > available` returns available only.
- Single-snapshot downstream behavior:
  - Trends return one point with explicit null delta semantics.
  - Signals return an empty list when no comparison snapshot exists.

## 5. Edge Case Validation
Explicitly confirm behavior for:

- empty owner
  - Confirmed fixed.
  - Capture now persists a deterministic empty snapshot even when no sources are discovered from `ResolvedWorkItems`.
- empty product
  - Confirmed still supported.
  - Existing behavior for an empty product within a non-empty owner remains intact: the orchestrator persists a header-only snapshot for that product when a source exists.
- `N = 1`
  - Confirmed fixed.
  - Exactly one snapshot is returned.
  - Trend delta and direction are `null`.
  - Signals are empty.
- `N > available`
  - Confirmed fixed by behavior.
  - Available persisted snapshots are returned without fabrication.
- repeated captures
  - Confirmed fixed for the empty-owner path.
  - Repeated empty capture resolves to the same logical snapshot key and does not duplicate rows.
- concurrent capture scenarios (if relevant)
  - Relevant only to idempotency of the logical snapshot key.
  - The change uses the existing uniqueness and deduplication path in persistence.
  - No new concurrency mechanism was added; the fix relies on the pre-existing logical uniqueness enforcement and idempotent reload behavior.

## 6. Determinism Check
Confirm:
- no write-on-read
  - Confirmed.
  - The changes are limited to explicit capture and read-side selection behavior.
  - No read path was changed to persist data.
- stable ordering
  - Confirmed.
  - Existing persisted ordering remains explicit by `TimestampUtc` and `SnapshotId`.
  - The new empty-source fallback only affects source creation when no source exists; it does not change read ordering rules.
- no hidden recomputation changes
  - Confirmed.
  - No aggregation formulas were changed.
  - No comparison formulas were changed.
  - No trend math was changed beyond allowing single-snapshot null semantics to flow through unchanged.

## 7. Test Coverage

### Added Tests
- `PortfolioSnapshotCaptureOrchestratorTests.CaptureLatestAsync_PersistsDeterministicEmptySnapshotWhenOwnerHasNoSources`
  - Validates that an owner with zero sources still persists an empty snapshot and that repeated capture is idempotent.
- `PortfolioReadModelStateServiceTests.GetHistoryStateAsync_HonorsSingleSnapshotCountExactly`
  - Validates that `N = 1` returns exactly one snapshot.
- `PortfolioReadModelStateServiceTests.GetHistoryStateAsync_ReturnsAvailableSnapshotsWhenRequestExceedsHistory`
  - Validates that `N > available` returns only the available snapshots.
- `PortfolioReadModelStateServiceTests.GetHistoryStateAsync_ReturnsNullWhenSnapshotCountIsNonPositive`
  - Documents explicit current behavior for `N <= 0`.
- `PortfolioQueryServicesTests.SnapshotQueryService_ReturnsValidEmptyPayloadForPersistedEmptySnapshot`
  - Validates that downstream snapshot queries return empty but valid data for persisted empty snapshots.
- `PortfolioQueryServicesTests.TrendQueryService_WithSingleSnapshot_ReturnsNoDeltaOrDirection`
  - Validates null delta and direction semantics for `N = 1`.
- `PortfolioQueryServicesTests.DecisionSignalQueryService_WithSingleSnapshot_ReturnsEmptySignals`
  - Validates empty signal output when no comparison baseline exists.

### Updated Tests
- Existing capture and read-model test files were extended rather than replaced:
  - `PoTool.Tests.Unit/Services/PortfolioSnapshotCaptureOrchestratorTests.cs`
  - `PoTool.Tests.Unit/Services/PortfolioReadModelStateServiceTests.cs`
  - `PoTool.Tests.Unit/Services/PortfolioQueryServicesTests.cs`

### Missing Coverage (if any)
- No dedicated test was added for the branch where an empty-owner fallback source uses the latest dated sprint instead of `UnixEpoch`.
- No new explicit concurrent-capture test was added for the fallback-source path; the implementation relies on the existing idempotent persistence contract.

## 8. Known Limitations / Follow-ups
- Non-positive `snapshotCount` is now explicit rather than silently normalized.
- Exact current behavior:
  - `SnapshotCount = 0` results in no selected snapshots and `GetHistoryStateAsync` returns `null`.
  - `SnapshotCount < 0` behaves the same way.
- Why this is acceptable for this fix:
  - The correctness issue was silent rewriting of the caller contract.
  - This change removes silent rewriting and preserves exact selection semantics.
- Follow-up consideration:
  - If the API contract should reject non-positive values explicitly, validation can be added later at the controller/query boundary.
  - That would be a separate contract decision, not part of this localized correctness fix.

## 9. Audit Alignment
Explain how this resolves:

- empty snapshot audit failure
  - The audit failure was that a truly empty owner could not produce a persisted snapshot because source discovery depended on resolved work items.
  - The new explicit empty-capture path removes that dependency at the orchestrator boundary.
  - Empty owner state is now persistable and queryable.
- snapshotCount audit failure
  - The audit failure was that `snapshotCount < 2` was rewritten to `2`, making `N = 1` semantically incorrect.
  - The new implementation passes the caller’s value through unchanged.
  - Trend and signal behavior for `N = 1` is now explicit and audit-safe:
    - no delta
    - no direction
    - no invented comparison signals

## 10. Notes for Reviewers
- Scope is intentionally limited to the two audited correctness defects only.
- No aggregation logic was changed.
- No comparison logic was changed.
- No new domain concepts were introduced.
- Empty-owner capture now uses a deterministic fallback source only when normal source discovery returns none.
- History selection now honors `snapshotCount` exactly and leaves `N <= 0` explicit for future contract validation decisions.
