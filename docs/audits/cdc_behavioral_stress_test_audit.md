# CDC Behavioral Stress-Test Audit

## Summary
- scenarios already covered: most mixed state transitions, story point fallback chains, hierarchy rollup rules, and cross-rule orchestration are already covered strongly in existing CDC and thin handler regression tests
- scenarios added: exact commitment-cutoff membership, exact state-reconstruction boundary behavior, exact sprint-end spillover boundary behavior, removed-only churn assertion, and undelivered added-scope rate
- scenarios intentionally not added because already covered elsewhere: reopen/second-Done behavior, bug/task story-point exclusion, derived-estimate exclusion from committed velocity, business-value fallback, parent fallback rollups, and nested hierarchy rollups

## Existing Strong Coverage
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - `Handle_DoesNotCountSecondDone_WhenFirstDoneWasBeforeSprint`
  - `Handle_TreatsItemAddedAfterCommitment_AsAddedScope`
  - `Handle_KeepsCommittedItemInInitialScope_WhenMovedAwayAfterCommitment`
  - `Handle_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover`
  - `Handle_DoesNotCountBacklogRoundTripToNextSprint_AsSpillover`
  - `Handle_UsesDerivedEstimatesOnlyForAddedAndRemovedScope`
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - `Handle_UsesHistoricalCommitmentAndFirstDoneSemantics`
  - `Handle_DoesNotCountSecondDoneTransition_WhenFirstDoneWasBeforeSprint`
  - `Handle_ExcludesBugAndTaskStoryPointsFromSprintTotals`
  - `Handle_UsesBusinessValueFallbackForSprintStoryPoints`
  - `Handle_ExcludesDeliveredPBIsWithoutEstimatesFromVelocity`
  - `Handle_TreatsZeroDonePbiAsValidZeroPointDelivery`
  - `Handle_TreatsZeroNonDonePbiAsMissingEstimate`
- `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
  - already covers StoryPoints → BusinessValue → Missing, zero-on-Done vs zero-on-not-Done, sibling-derived estimates, fractional derived estimates, and parent fallback
- `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`
  - already covers feature-only PBI rollups, epic-to-feature nested rollups, parent fallback only when child PBIs lack estimates, excluded bugs/tasks, and derived estimates inside rollups
- `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`
  - already covers canonical formulas, zero-denominator safety, removed-scope denominator reduction, and positive added-delivery scenarios

## New Tests Added
- file: `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
  - test name: `BuildCommittedWorkItemIds_UsesBoundaryStateAtExactCommitmentTimestamp`
  - scenario covered: item moved onto sprint exactly at commitment cutoff and item moved off sprint exactly at commitment cutoff
  - why it was missing: existing coverage reconstructed commitment membership before/after the cutoff, but not the exact equality boundary that distinguishes `>` replay semantics
- file: `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
  - test name: `GetStateAtTimestamp_UsesBoundaryStateAtExactTimestamp`
  - scenario covered: state transition exactly at the reconstruction timestamp
  - why it was missing: existing coverage only proved replay for later events, not the exact boundary that drives reopened/Done attribution correctness
- file: `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
  - test name: `BuildSpilloverWorkItemIds_CountsDirectMoveAtSprintEndBoundary`
  - scenario covered: spillover direct move on the sprint-end boundary
  - why it was missing: existing tests covered moves after sprint end, but not the `>= sprintEnd` boundary used by the spillover helper
- file: `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`
  - test name: `Calculate_ReturnsZeroAddedDeliveryRate_WhenAddedScopeIsNotDelivered`
  - scenario covered: added scope with no delivered added scope
  - why it was missing: existing formula tests only covered successful added-scope delivery and zero-denominator safety
- file: `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`
  - test name: existing `Calculate_UsesRemovedScopeToReduceCompletionAndSpilloverDenominators` expanded with a churn assertion
  - scenario covered: churn with only removed items
  - why it was missing: the test already had the right setup, but it was not asserting the removed-only churn behavior explicitly

## Gaps Still Deferred
- first-Done just outside the sprint window: intentionally left at handler level because sprint-window inclusion/exclusion is orchestration behavior already covered by `GetSprintExecutionQueryHandlerTests` and `GetSprintMetricsQueryHandlerTests`
- `New → InProgress → Removed` and other multi-transition activity/work propagations: deferred because the primary owner is broader sprint metrics/activity orchestration rather than the small CDC helpers audited here
- broader hierarchy anomaly graphs: deferred because the current issue targets high-value canonical sizing behavior, and the existing rollup tests already cover the intended operational hierarchy with fallback/derived semantics

## Final Assessment
CDC behavioral stress coverage is now: **strong**
