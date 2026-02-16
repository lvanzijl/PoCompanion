## What changed
- Expanded reporting pagination logs with `IsLastBatch`, `NextLinkPresent`, and `ContinuationTokenSource`.
- Added per-item page logs with `skip/top`, `returnedCount`, and `cumulativeCount`.
- Added regression-style tests for token precedence, completion behavior, and per-item loop termination.

## Why
- Additional observability makes infinite-loop and partial-read scenarios easier to detect and diagnose offline and in production logs.

## How tests validate correctness
- Tests cover payload precedence, header fallback behavior, and per-item pagination safety guard behavior with deterministic mocked responses.
