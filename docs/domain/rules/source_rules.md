# Domain Rules — Data Sources

These rules define when PoTool uses snapshots versus update history.

## Snapshot truth

Snapshots represent the current state of work items.

Snapshots must be used for questions about the present.

Examples:

- backlog health
- current hierarchy
- current state
- current estimates
- current parent relations

Snapshots are authoritative for "now".

## Update truth

Work item updates represent historical change events.

Updates must be used for questions about what happened over time.

Examples:

- delivery attribution
- activity detection
- work detection
- churn detection
- spillover detection
- sprint analytics
- commitment reconstruction

Updates represent the event timeline.

## Hybrid analyses

Some analytics require both sources.

Typical pattern:

Updates determine when something happened.  
Snapshots determine current hierarchy or context.

Examples:

- historical delivery aggregated by current product
- activity mapped onto current hierarchy

## Conflict resolution

If snapshot state and reconstructed update history disagree for a current-state question:

Snapshot state is authoritative.

Reason:

Snapshots represent the actual current TFS state.
