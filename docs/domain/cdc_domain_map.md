# CDC Domain Map

This document describes the complete Canonical Domain Core map in text form so the diagram can be redrawn without reinterpreting ownership.

## Nodes

Use these nodes in the diagram:

1. Raw Work-Item Snapshots
   - current hierarchy
   - current state
   - current story points
   - current effort hours
   - current product membership
2. Raw Work-Item History
   - `System.IterationPath` changes
   - `System.State` changes
   - point-in-time estimate changes
   - portfolio-entry transitions
3. Sprint Metadata
   - sprint path
   - sprint window
   - team-local sprint ordering
4. Core Concepts
   - hierarchy rules
   - estimation rules
   - state rules
   - sprint rules
   - source rules
   - propagation rules
5. BacklogQuality
   - current-state validation
   - readiness
   - implementation readiness
6. SprintCommitment
   - commitment
   - scope added
   - scope removed
   - completion
   - spillover
7. DeliveryTrends
   - sprint delivery projection
   - progress rollups
   - progression deltas
   - product delivery summaries
8. Forecasting
   - delivery forecast
   - completion projection
   - velocity calibration
   - forecast distribution
9. EffortDiagnostics
    - effort imbalance
    - concentration risk
10. EffortPlanning
    - effort distribution
    - effort estimation quality
    - effort estimation suggestions
11. PortfolioFlow
     - stock
     - inflow
     - throughput
     - remaining scope
     - completion percent
12. Shared Statistics
     - mean
     - median
     - variance
     - standard deviation
     - percentile semantics
13. Application Adapters
     - handlers
     - DTO mappers
     - compatibility adapters
14. Projection Persistence
     - sprint metrics projections
     - portfolio flow projections
15. UI and Client Consumers
     - dashboards
     - portfolio pages
     - trend pages
     - forecast pages

## Edges

Draw the following required edges:

- Raw Work-Item Snapshots -> Core Concepts
- Raw Work-Item History -> Core Concepts
- Sprint Metadata -> Core Concepts
- Raw Work-Item Snapshots -> BacklogQuality
- Core Concepts -> BacklogQuality
- Raw Work-Item History -> SprintCommitment
- Sprint Metadata -> SprintCommitment
- Core Concepts -> SprintCommitment
- SprintCommitment -> DeliveryTrends
- Core Concepts -> DeliveryTrends
- DeliveryTrends -> Forecasting
- Shared Statistics -> Forecasting
- Raw Work-Item Snapshots -> EffortDiagnostics
- Core Concepts -> EffortDiagnostics
- Shared Statistics -> EffortDiagnostics
- Raw Work-Item Snapshots -> EffortPlanning
- Core Concepts -> EffortPlanning
- Shared Statistics -> EffortPlanning
- Raw Work-Item Snapshots -> PortfolioFlow
- Raw Work-Item History -> PortfolioFlow
- Sprint Metadata -> PortfolioFlow
- Core Concepts -> PortfolioFlow
- SprintCommitment -> PortfolioFlow
- BacklogQuality -> Application Adapters
- SprintCommitment -> Application Adapters
- DeliveryTrends -> Application Adapters
- Forecasting -> Application Adapters
- EffortDiagnostics -> Application Adapters
- EffortPlanning -> Application Adapters
- PortfolioFlow -> Application Adapters
- SprintCommitment -> Projection Persistence
- DeliveryTrends -> Projection Persistence
- PortfolioFlow -> Projection Persistence
- Application Adapters -> UI and Client Consumers
- Projection Persistence -> Application Adapters

No reverse edge should be drawn from Application Adapters, Projection Persistence, or UI and Client Consumers back into any CDC slice.

## Layering

The map should be drawn in five layers from bottom to top:

1. Source layer
   - Raw Work-Item Snapshots
   - Raw Work-Item History
   - Sprint Metadata
2. Canonical interpretation layer
   - Core Concepts
   - Shared Statistics
3. CDC slice layer
   - BacklogQuality
   - SprintCommitment
   - DeliveryTrends
   - Forecasting
   - EffortDiagnostics
   - EffortPlanning
   - PortfolioFlow
4. Application materialization layer
   - Projection Persistence
   - Application Adapters
5. Consumer layer
   - UI and Client Consumers

## Dependency Rules

The diagram must preserve these rules:

- semantics flow upward from source data through Core Concepts into CDC slices
- Shared Statistics may support a slice but does not own slice semantics
- SprintCommitment is the historical sprint-facts provider and must not depend on DeliveryTrends, Forecasting, or PortfolioFlow
- DeliveryTrends may consume SprintCommitment but must not redefine commitment or spillover
- Forecasting may consume delivery-trend history but must not reconstruct sprint history itself
- EffortPlanning may consume shared statistical helpers but must not leave effort analytics formulas in handlers
- PortfolioFlow may consume first-Done completion and sprint windows but owns stock, inflow, throughput, and remaining scope semantics
- BacklogQuality remains snapshot-driven and does not depend on historical delivery slices
- application, persistence, and UI layers consume CDC outputs and must not feed semantics back into the CDC
- compatibility naming may appear only in Application Adapters and UI and Client Consumers

## Interpretation Flow

Interpretation from raw work-item history to portfolio insight must be read in this order:

1. resolve raw snapshots and raw history into the Core Concepts vocabulary
2. classify hierarchy, state, story points, effort hours, sprint windows, and source-truth usage
3. build snapshot-driven backlog signals in BacklogQuality
4. replay sprint history in SprintCommitment to reconstruct commitment, spillover, and first-Done completion
5. transform sprint facts into delivery-trend outputs in DeliveryTrends
6. transform delivery-trend history into forecast outputs in Forecasting
7. combine hierarchy, state, story-point scope, portfolio-entry transitions, sprint windows, and completion attribution into PortfolioFlow stock, inflow, throughput, and remaining scope
8. compute effort imbalance and concentration in EffortDiagnostics when effort-hour diagnostics are needed
9. compute effort distribution, effort-quality, and effort-suggestion outputs in EffortPlanning when effort-hour planning analytics are needed
10. materialize selected outputs in Projection Persistence where downstream consumers need stable read models
11. expose CDC-backed outputs through Application Adapters to UI and Client Consumers without changing CDC meaning

## Diagram Notes

- label arrows with ownership verbs when helpful, such as defines, reconstructs, consumes, materializes, or adapts
- keep Core Concepts visually separate from Shared Statistics so pure math is not mistaken for domain ownership
- show SprintCommitment as the upstream source for DeliveryTrends and a supporting source for PortfolioFlow
- show Forecasting downstream of DeliveryTrends, not parallel to SprintCommitment
- show compatibility concerns only in Application Adapters and UI and Client Consumers
- when redrawing PortfolioFlow, label the node with story points and not effort hours
