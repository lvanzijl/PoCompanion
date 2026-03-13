# Domain Rules — Hierarchy

These rules define how PoTool interprets work item hierarchy.

## Canonical operational hierarchy

PoTool analytics operate primarily on the following hierarchy:

Epic  
Feature  
PBI  

Tasks exist below PBIs but are not planning units.

## Full theoretical hierarchy

Goal  
Objective  
Epic  
Feature  
PBI / Bug  
Task  

Not all levels are used for analytics.

## Delivery unit

The fundamental delivery unit is the **PBI**.

Implications:

- Story Points originate at PBI level
- Velocity originates at PBI level
- Sprint delivery metrics originate at PBI level

Higher levels receive derived metrics.

## Product scope

A **Product** is the primary analytics boundary.

A product may contain multiple backlog roots.

PoTool aggregates all backlog roots belonging to a product into a single logical product backlog.

Analytics operate at the **product level**, not per backlog root.

## Parent-child expectations

Expected hierarchy:

Objective → Epic  
Epic → Feature  
Feature → PBI  
PBI → Task

Direct relations skipping levels may exist but are considered structural anomalies.

Analytics must remain resilient to these anomalies.

## Parent completion independence

Completion of all PBIs under a Feature does NOT automatically imply the Feature should be Done.

Feature and Epic completion remain explicit management decisions.

PoTool must not auto-derive Feature/Epic completion from child completion.
