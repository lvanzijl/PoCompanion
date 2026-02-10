# Revision Reporting Window Strategy

## Window processing
- Revisions are ingested in chronological windows defined by `[WindowStartUtc, WindowEndUtc)`. Windows start at the inferred backfill start date (earliest cached CreatedDate - 1 day, clamped to 2000-01-01) and end at `UtcNow`.
- Initial window size is 30 days. Windows grow gradually (up to 120 days) when a window finishes cleanly and shrink when a stall is detected. The minimum chunk processed is 6 hours (preferred minimum 1 day).
- Pagination always queries the reporting endpoint with the current window start. Each page is filtered to the window by `ChangedDate >= WindowStartUtc` and `< WindowEndUtc`. When pages move entirely outside the window (older than start or newer than end after overlap), the window completes.

## Stall detection
- A window is stalled when any of the following occurs:
  - RawRevisionCount == 0 **and** HasMoreResults == true
  - Continuation token repeats
  - Token does not advance while HasMoreResults == true
  - MaxTotalPages safety limit hit
- The current window stops immediately; ingested rows remain persisted.

## Splitting and continuation
- Stalled windows are split deterministically:
  - If duration > preferred minimum (1 day), split in half.
  - If duration > minimum (6 hours) but <= preferred minimum, split into two halves of the current span.
  - If duration <= minimum, mark the window `MarkedUnretrievableAtMinimumChunk` and continue.
- Split windows are processed before later windows so history continues even if an earlier range stalls.

## Completion criteria
- Backfill is considered complete when every window in `[BackfillStartUtc, UtcNow)` is processed and each window outcome is one of:
  - `CompletedNormally`
  - `CompletedRawEmpty`
  - `MarkedUnretrievableAtMinimumChunk` (bounded anomaly)
- Presence of unretrievable windows marks the run outcome as `CompletedWithPaginationAnomaly` but still sets backfill complete once all windows are bounded.

## Logging
- **Per page:** window start/end, page index, raw/scoped/persisted counts, min/max raw ChangedDate, HasMoreResults, token hash, token advanced/repeated flags, total persisted (window/run).
- **Per window:** start/end, pages processed, persisted count, outcome, stall reason (TokenRepeated | RawZeroWithHasMore | TokenNotAdvancing | MaxPageLimit).
- **Run summary:** BackfillStartUtc, BackfillEndUtc, windows processed, windows marked unretrievable, total persisted, BackfillComplete flag.
