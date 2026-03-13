# Domain Rules — Metrics

These rules define the core PoTool metrics.

## Velocity

Velocity = sum of Story Points of PBIs whose first Done transition occurs within SprintWindow.

Velocity includes:

- committed work finished
- work added during sprint and finished

Velocity excludes:

- Bugs
- Tasks
- Removed PBIs
- PBIs without Story Points

## Committed scope

CommittedSP = sum of Story Points of PBIs whose IterationPath equals the sprint at CommitmentTimestamp.

CommitmentTimestamp = SprintStart + 1 day.

Removed items are excluded from committed scope.

## Commitment completion

CommitmentCompletion = DeliveredSP / (CommittedSP − RemovedSP)

Removed scope does not penalize completion.

## Churn rate

ChurnRate = (AddedSP + RemovedSP) / (CommittedSP + AddedSP)

AddedSP = story points added after commitment

RemovedSP = story points removed after commitment

## Spillover rate

SpilloverRate = SpilloverSP / (CommittedSP − RemovedSP)

SpilloverSP represents committed PBIs not finished in the sprint and moved to the next sprint.

## Added delivery rate

AddedDeliveryRate = DeliveredFromAddedSP / AddedSP

This metric indicates how much added scope was actually completed.

## Unestimated delivery

PBIs delivered without Story Points are counted as:

UnestimatedDeliveryCount

These items do not contribute to velocity.

## Remaining scope

RemainingSP represents committed scope not finished at SprintEnd.
