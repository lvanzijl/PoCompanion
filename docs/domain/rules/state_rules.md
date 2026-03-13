# Domain Rules — State Classification

PoTool does not rely on raw TFS states.

Instead, states are mapped to canonical lifecycle states.

## Canonical lifecycle states

New  
InProgress  
Done  
Removed  

All TFS states must map to exactly one canonical state.

## Per work item type mapping

Each work item type has its own state mapping.

Structure:

WorkItemType → TfsState → CanonicalState

Mappings may differ between work item types.

## Configuration source

State mappings are stored in PoTool **Settings configuration**.

They are not hardcoded in domain logic.

Analytics must always resolve the canonical state using this mapping.

## Delivery definition

Delivery occurs when canonical state transitions to:

Done

Only the **first transition to Done** counts as delivery.

## Reopen semantics

Transition:

Done → InProgress

Represents rework.

This must NOT generate a new delivery event.

## Scope removal

Transition to:

Removed

Represents scope removal.

Removed items:

- do not contribute to velocity
- remain visible in churn analysis
- remain visible historically

## Work start

Transition:

New → InProgress

Represents the start of work.

## Non meaningful transitions

Transitions where canonical state does not change are ignored.

Examples:

New → New  
InProgress → InProgress  
Done → Done  
Removed → Removed
