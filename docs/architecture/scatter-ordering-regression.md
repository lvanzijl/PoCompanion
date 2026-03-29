# Scatter Ordering Regression Analysis

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Scope: analysis only; no code changes

## Summary

This is **not a real regression**.

`GetPipelineInsightsScatterPointTests.Handle_ScatterPoints_OrderedByStartTimeAscending` expects scatter points to be ordered by start time ascending, and the handler already enforces that ordering. The failure comes from the test using `Assert.IsLessThanOrEqualTo` with arguments reversed, so the test is asserting the opposite of the intended ordering.

## Expected Behavior

From the test:

- File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs:661-683`
- Test name: `Handle_ScatterPoints_OrderedByStartTimeAscending`
- Description: `ScatterPoints are ordered by start time ascending`

The test seeds three runs with start times inserted out of order:

- `2026-02-10 09:00 UTC`
- `2026-02-05 09:00 UTC`
- `2026-02-08 09:00 UTC`

So the expected result is:

1. first scatter point: `2026-02-05 09:00 UTC`
2. second scatter point: `2026-02-08 09:00 UTC`
3. third scatter point: `2026-02-10 09:00 UTC`

The ordering field is clearly:

- `StartTime`

as exposed on `PipelineScatterPointDto` and asserted in the test.

## Actual Behavior

From the handler:

- File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:345-347`
- Scatter points are built in `BuildProductSection(...)` via:
  - `var scatterPoints = BuildScatterPoints(currentRuns, defByDbId);`

The actual ordering logic is in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs:454-490`

Implementation details:

1. Runs without a start time are filtered out:
   - `.Where(r => r.CreatedDateOffset.HasValue)`
2. Remaining runs are explicitly sorted:
   - `.OrderBy(r => r.CreatedDateOffset)`
3. The sorted value is projected to DTO `StartTime`:
   - `StartTime = r.CreatedDateOffset`

So the handler is already doing:

- ascending ordering
- on the same field family used by the DTO (`CreatedDateOffset` → `StartTime`)

That means ordering is neither missing nor applied at the wrong point.

## Root Cause

The precise mismatch is in the test assertion, not in production code.

The failing test uses:

- `Assert.IsLessThanOrEqualTo(scatter[0].StartTime!.Value, scatter[1].StartTime!.Value, ...)`
- `Assert.IsLessThanOrEqualTo(scatter[1].StartTime!.Value, scatter[2].StartTime!.Value, ...)`

In MSTest, `Assert.IsLessThanOrEqualTo(upperBound, value)` checks:

- `value <= upperBound`

So the first assertion is effectively checking:

- `scatter[1].StartTime <= scatter[0].StartTime`

which is the reverse of ascending order.

The focused test failure confirms this:

- `scatter[0]` was `02/05/2026 09:00:00 +00:00`
- `scatter[1]` was `02/08/2026 09:00:00 +00:00`
- the failure message says `02/08/2026` is not less than or equal to `02/05/2026`

That output proves the handler returned points in correct ascending order:

- `02/05` then `02/08`

The test fails only because it asserts the opposite comparison.

## Classification

**Test issue**

This is not a confirmed runtime regression because:

- the handler explicitly sorts scatter points ascending by start time
- the observed returned values are already in ascending order
- the failure is caused by reversed assertion semantics in the test

So this should be classified as:

- **incorrect test expectation / incorrect assertion usage**

not:

- regression in handler ordering logic

## Fix Approach

The minimal fix should be applied in the test, not in the handler.

Exact place:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs:681-682`

Minimal strategy:

1. Keep handler logic unchanged
2. Fix the ordering assertions so they actually verify ascending order
3. Use either:
   - `Assert.IsLessThanOrEqualTo(scatter[1].StartTime!.Value, scatter[0].StartTime!.Value, ...)` if staying with MSTest’s `(upperBound, value)` signature, or preferably
   - `Assert.IsTrue(scatter[0].StartTime!.Value <= scatter[1].StartTime!.Value, ...)`
   - `Assert.IsTrue(scatter[1].StartTime!.Value <= scatter[2].StartTime!.Value, ...)`
4. Optionally make the expectation even clearer by asserting the exact ordered timestamps or ordered run IDs/build numbers

No handler-side ordering change is needed.

## Final Verdict

**Not a regression**

The handler already enforces ascending scatter-point ordering by start time. The failing test is miswritten and reverses the comparison semantics of `Assert.IsLessThanOrEqualTo`, causing a false failure.
