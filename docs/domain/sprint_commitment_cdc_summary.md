# Sprint Commitment CDC Summary

_Generated: 2026-03-16_

Reference documents:

- `docs/domain/domain_model.md`
- `docs/rules/sprint-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/source-rules.md`

## Canonical concepts

The Sprint Commitment CDC now exposes the sprint-planning concepts as domain records under `PoTool.Core.Domain.Cdc.Sprints`.

- `SprintCommitment`
  - committed sprint membership for one work item
  - identifiers: `SprintId`, `WorkItemId`
  - timestamp: `CommitmentTimestamp`
- `SprintScopeAdded`
  - one work item entering the sprint after commitment
  - identifiers: `SprintId`, `WorkItemId`
  - timestamp: `AddedAt`
- `SprintScopeRemoved`
  - one work item leaving the sprint after commitment
  - identifiers: `SprintId`, `WorkItemId`
  - timestamp: `RemovedAt`
- `SprintCompletion`
  - the first canonical Done transition inside the sprint window
  - identifiers: `SprintId`, `WorkItemId`
  - timestamp: `CompletedAt`
- `SprintSpillover`
  - committed work that moved directly into the next sprint
  - identifiers: `SprintId`, `WorkItemId`
  - timestamp: `SpilloverAt`

Canonical commitment policy remains:

- `CommitmentTimestamp = SprintStart + 1 day`

Snapshot membership is not commitment logic. Current snapshot fields remain useful as reconstruction anchors and current-state cache, but historical sprint commitment comes from update history.

## Event signals

The CDC uses a narrow set of canonical input signals:

- sprint metadata
  - `SprintStart`
  - `SprintEnd`
  - `SprintPath`
  - team-local sprint ordering for next-sprint resolution
- `System.IterationPath` events
  - commitment reconstruction
  - scope added / removed
  - direct next-sprint move detection
- `System.State` events
  - first Done attribution
  - state-at-end reconstruction for spillover exclusion
- canonical state mapping
  - Done / InProgress / New / Removed semantics per work item type
- work item snapshots
  - current `IterationPath`
  - current `State`
  - work item type

## Derived metrics

The Sprint Commitment CDC owns the canonical inputs that downstream metrics use:

- `CommittedSP`
- `AddedSP`
- `RemovedSP`
- `DeliveredSP`
- `DeliveredFromAddedSP`
- `SpilloverSP`

The canonical formulas remain:

- `CommitmentCompletion = DeliveredSP / (CommittedSP - RemovedSP)`
- `ChurnRate = (AddedSP + RemovedSP) / (CommittedSP + AddedSP)`
- `SpilloverRate = SpilloverSP / (CommittedSP - RemovedSP)`
- `AddedDeliveryRate = DeliveredFromAddedSP / AddedSP`

The CDC execution metrics wrapper keeps these formulas available through the sprint execution metrics calculator service without coupling the slice to transport DTOs.

## CDC service interfaces

The Sprint Commitment CDC now exposes four service interfaces plus the execution metrics wrapper:

- `ISprintCommitmentService`
  - returns the canonical commitment timestamp
  - reconstructs committed work item IDs
  - projects `SprintCommitment` records
- `ISprintScopeChangeService`
  - detects `SprintScopeAdded`
  - detects `SprintScopeRemoved`
- `ISprintCompletionService`
  - reconstructs first-Done timestamps
  - projects `SprintCompletion` records within the sprint window
- `ISprintSpilloverService`
  - resolves the next sprint path
  - detects committed spillover IDs
  - projects `SprintSpillover` records
- `ISprintExecutionMetricsCalculator`
  - applies the canonical execution formulas to already reconstructed story-point totals

These services are implemented by wrapping the existing canonical helpers so the semantics stay stable while the application layer migrates to the CDC slice.

## Relationship to delivery trends and forecasting

Delivery trends remain downstream consumers. `SprintDeliveryProjectionService` no longer infers commitment from `ResolvedSprintId`; `CommittedWorkItemIds` is now required and delivery trends consume committed IDs, completion signals, and spillover outputs from the CDC.

This preserves the intended boundary:

- Sprint Commitment CDC reconstructs historical sprint-planning facts
- Delivery trends consume those facts to produce projections and trend slices
- Forecasting consumes historical delivery outputs and does not own sprint commitment semantics
