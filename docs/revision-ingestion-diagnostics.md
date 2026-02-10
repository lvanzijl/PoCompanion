# Revision Ingestion Diagnostics (Instrumentation-Only)

This instrumentation makes the ingestion pipeline explain exactly where revisions go when a run executes. All changes are logging-only.

## What is logged
- **Database snapshots**: `Revision ingestion DB snapshot` at start/end of a run with total rows, distinct work items, and min/max `ChangedDate`. Shows what already exists vs what was added.
- **Page 1 raw slice**: `Reporting revisions page 1 raw snapshot` logs raw count, distinct work items, min/max `ChangedDate`, and how many raw rows are already in scope. Confirms whether the first reporting page is a narrow recent slice or broader history.
- **Per-page drop accounting**: `Revision ingestion drop accounting` records raw, scoped, candidate, and persisted counts plus explicit drop reasons (already exists, outside window, missing required field, DB constraint, other). No silent drops.
- **Dedup proof**: `Revision deduplication key ...` declares the exact key (`WorkItemId+RevisionNumber`), whether `RevisionNumber`/`ChangedDate` participate, and how many rows were skipped as duplicates.
- **Window coverage**: `Revision ingestion window raw range` + warning when no raw revision falls inside the window. Shows window bounds and min/max raw `ChangedDate` observed.
- **Creation snapshot detection**: Run-level `Creation snapshot coverage` reports how many scoped work items had a creation-like revision observed (either `ChangedDate == CreatedDate` or the earliest `ChangedDate` seen) versus missing.

## How to read the logs
- Compare **start vs end DB snapshots** to see whether new rows were inserted and the overall date span widened.
- Use **page 1 raw snapshot** to decide if the reporting API is returning only recent revisions or a representative slice.
- For any page, inspect **drop accounting** to see if revisions were filtered out before persistence and why; cross-check **dedup proof** when `AlreadyExists` drops are non-zero.
- **Window raw range** warnings highlight windows that are empty by construction (no raw revisions fall inside the window), indicating pagination/window misalignment.
- After a run, **creation snapshot coverage** shows whether creation revisions ever appeared for scoped work items.

## Questions each log answers
- **“Are revisions never fetched or fetched then dropped?”** → DB snapshots + drop accounting.
- **“Is pagination stalling immediately?”** → Page 1 raw snapshot and drop accounting on early pages.
- **“Is the window definition empty?”** → Window raw range warnings.
- **“Are duplicates collapsing history?”** → Dedup proof counts and key definition.
- **“Do creation revisions show up at all?”** → Creation snapshot coverage summary.
