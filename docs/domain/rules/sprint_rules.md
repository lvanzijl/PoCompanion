# Domain Rules — Sprint Semantics

These rules define how PoTool interprets sprint timelines.

## Sprint window

SprintWindow = [SprintStart, SprintEnd]

All sprint analytics operate within this window.

## On sprint vs During sprint

PoTool distinguishes two concepts.

On sprint:

IterationPath == SprintPath

Used for planning and commitment reconstruction.

During sprint:

EventTimestamp within SprintWindow

Used for delivery, activity, work, churn and spillover.

## Commitment timestamp

CommitmentTimestamp = SprintStart + 1 day

Reason:

Teams typically fill and adjust sprint scope on the first day.

Committed scope equals:

Items whose IterationPath equals the sprint at CommitmentTimestamp.

## Delivery detection

Delivery occurs when the **first transition to Done** happens within SprintWindow.

IterationPath must NOT determine delivery.

## First Done rule

Delivery is counted only once.

If a work item transitions:

Done → InProgress → Done

Only the first Done transition counts.

## Activity detection

Activity means something happened to the item during the sprint.

Activity is triggered by monitored changes except metadata fields.

Discussion updates count as activity.

History text entries do not count separately.

## Work detection

Work requires meaningful state transitions.

Examples:

New → InProgress  
InProgress → Done  
Any → Removed  
Done → InProgress

Non meaningful transitions are ignored.

## Churn detection

Churn is based on IterationPath changes after commitment.

Added churn:

item enters sprint after CommitmentTimestamp

Removed churn:

item leaves sprint after CommitmentTimestamp

## Spillover

Spillover occurs when:

- item was committed
- item is not Done at SprintEnd
- item moves to the next sprint
