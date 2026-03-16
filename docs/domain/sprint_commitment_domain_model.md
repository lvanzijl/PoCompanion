# Sprint Commitment Domain Model

Status: Canonical definition for Sprint Commitment before CDC extraction  
Purpose: Define the canonical planning and commitment semantics for sprint scope, churn, completion, and spillover.

This document formalizes the Sprint Commitment slice as a canonical historical model. It does not describe current UI behavior or transport contracts; it defines what the sprint-planning concepts mean.

## Canonical Concepts

### SprintCommitment

SprintCommitment is the committed sprint scope at the canonical commitment boundary.

- definition: work items whose `IterationPath` equals the sprint path at `CommitmentTimestamp`
- commitment policy: `CommitmentTimestamp = SprintStart + 1 day`
- source: iteration-path history reconstructed from current snapshot plus later events

SprintCommitment is the planning baseline for churn, completion, and spillover.

### SprintScope

SprintScope is the total sprint scope considered for sprint execution analysis.

It combines:

- `SprintCommitment`
- `SprintScopeAdded`

It does not collapse the difference between committed scope and added scope.

### SprintScopeAdded

SprintScopeAdded is the set of work items that enter the sprint after commitment and before sprint end.

- definition: `IterationPath` changes into the sprint after `CommitmentTimestamp` and on or before `SprintEnd`
- source: `System.IterationPath` events

### SprintScopeRemoved

SprintScopeRemoved is the set of work items that leave the sprint after commitment and before sprint end.

- definition: `IterationPath` changes away from the sprint after `CommitmentTimestamp` and on or before `SprintEnd`
- source: `System.IterationPath` events

### SprintCompletion

SprintCompletion is a delivery fact for one work item.

- definition: the work item’s first canonical transition into Done inside the sprint window
- source: `System.State` history interpreted through canonical state mappings
- rule: reopen transitions do not create a second completion

### SprintThroughput

SprintThroughput is the delivery summary for the sprint window.

- definition: committed or added work items whose first canonical Done transition occurs within `SprintWindow`
- note: throughput includes delivered committed work and delivered added work

### SprintSpillover

SprintSpillover is the carry-over subset of committed scope.

- definition: committed work not Done at sprint end whose first post-sprint move is directly into the next sprint
- note: unfinished work still sitting in the same sprint path is not enough
- note: backlog round-trip behavior is not spillover

### SprintWorkItemSnapshot

SprintWorkItemSnapshot is the current work item state used as the reconstruction anchor for historical sprint semantics.

It provides:

- current `IterationPath`
- current `State`
- work item type

It does not override update history for historical questions.

### SprintPlanningEvent

SprintPlanningEvent is a timestamped field change relevant to sprint planning semantics.

The primary event family is:

- `System.IterationPath` changed

These events are used to reconstruct commitment, additions, removals, and direct next-sprint moves.

## Event Signals

The Sprint Commitment slice depends on a small set of canonical event signals.

### Sprint metadata

- `SprintStart`
- `SprintEnd`
- `SprintPath`
- team-local sprint ordering needed to resolve the next sprint

### Iteration-path signal

- field: `System.IterationPath`
- uses: commitment reconstruction, scope added, scope removed, direct next-sprint spillover move

### State-change signal

- field: `System.State`
- uses: first-Done attribution, state-at-sprint-end reconstruction, spillover exclusion for already-Done items

### Canonical state mapping signal

- source: configured per-work-item-type state classification
- uses: determine whether a raw state is canonical Done, InProgress, New, or Removed

### Snapshot anchor signal

- current work item snapshot
- uses: current values that the historical replay walks backward from

## Derived Metrics

The following metrics are derived from the canonical concepts above.

### Committed scope

`CommittedSP = sum(story points of committed PBIs at CommitmentTimestamp)`

Derived estimates are excluded from sprint commitment.

### Added scope

`AddedSP = sum(story points of PBIs added after CommitmentTimestamp)`

### Removed scope

`RemovedSP = sum(story points of PBIs removed after CommitmentTimestamp)`

### Throughput

`DeliveredSP = sum(story points of committed or added PBIs whose first Done transition occurs within SprintWindow)`

### Added throughput

`DeliveredFromAddedSP = sum(story points of added PBIs delivered within SprintWindow)`

### Spillover

`SpilloverSP = sum(story points of committed PBIs that spill directly into the next sprint)`

### SprintCompletionRate

`SprintCompletionRate = DeliveredSP / (CommittedSP - RemovedSP)`

This matches the current `CommitmentCompletion` formula.

### ChurnRate

`ChurnRate = (AddedSP + RemovedSP) / (CommittedSP + AddedSP)`

### SpilloverRate

`SpilloverRate = SpilloverSP / (CommittedSP - RemovedSP)`

### AddedDeliveryRate

`AddedDeliveryRate = DeliveredFromAddedSP / AddedSP`

### Remaining scope

`RemainingSP = CommittedSP - RemovedSP - DeliveredSP`

Remaining scope is distinct from spillover because not every remaining item necessarily moved directly into the next sprint.

## Relationship to Existing CDC Slices

Sprint Commitment is not an isolated model. It depends on existing CDC primitives and adjacent slices.

### Relationship to state semantics

Sprint Commitment consumes canonical state mappings and first-Done rules from the state slice.

Without canonical state mapping:

- `SprintCompletion` is ambiguous
- `SprintSpillover` cannot reliably exclude already-Done items

### Relationship to estimation semantics

Sprint Commitment consumes canonical story-point resolution rules from the estimation slice.

This affects:

- `CommittedSP`
- `AddedSP`
- `RemovedSP`
- `DeliveredSP`
- `SpilloverSP`

Derived estimates may support aggregation, but they must not silently change the meaning of committed delivery metrics.

### Relationship to sprint/time semantics

Sprint Commitment owns the planning-specific subset of sprint semantics:

- `SprintWindow`
- `CommitmentTimestamp`
- On sprint vs During sprint
- add/remove timing
- spillover timing

### Relationship to delivery trends

`SprintDeliveryProjectionService` already consumes:

- committed work item IDs
- first-Done timestamps
- next-sprint path
- spillover lookup results

That makes DeliveryTrends a downstream consumer of Sprint Commitment semantics, not their owner.

### Relationship to application analytics

The following should remain outside the canonical slice:

- `StarvedPbis` heuristic from sprint execution UI
- portfolio `AddedEffort` proxy from portfolio trend analytics
- DTO naming and presentation choices

## Migration Strategy

1. **Define the CDC boundary explicitly**
   - include `SprintCommitment`, `SprintScopeAdded`, `SprintScopeRemoved`, `SprintCompletion`, `SprintSpillover`, and metric formulas
   - exclude starved-work and portfolio proxy concepts

2. **Promote event-signal preparation into stable domain inputs**
   - sprint metadata
   - work item snapshots
   - iteration-path events
   - state events
   - canonical state mapping

3. **Remove or isolate snapshot fallback semantics**
   - `ResolvedSprintId` may remain a current-state cache
   - it should not remain an implicit fallback for historical commitment semantics in CDC entry points

4. **Make delivery-trend consumers depend on Sprint Commitment outputs**
   - projections should consume committed IDs and spillover results from the Sprint Commitment slice
   - they should not reconstruct or guess those semantics independently

5. **Retire outdated repository discovery wording**
   - repository discovery and future audits should describe `GetSprintMetricsQueryHandler` and sprint projections as commitment-based, not snapshot-based

6. **Keep migration incremental**
   - first centralize the canonical domain interfaces and records
   - then update downstream services to consume those outputs
   - finally trim obsolete snapshot-oriented descriptions and helper entry points
