# Domain Rules — Propagation

These rules define how signals move through the hierarchy.

## Origin levels

Task or PBI origin:

Activity  
Work

PBI origin only:

StoryPoints  
Effort  
Delivery  
Velocity  
Spillover

Higher levels receive derived signals.

## Activity propagation

Activity propagates upward as a **boolean presence signal**.

If any descendant has activity during a sprint, the parent also has activity.

Propagation path:

Task → PBI → Feature → Epic → Objective

Multiple events do not increase the signal.

## Work propagation

Work also propagates upward as a boolean presence signal.

If any descendant performs meaningful work during a sprint, the parent has work.

Propagation path:

Task → PBI → Feature → Epic → Objective

Work indicates where engineering effort occurred.

## Delivery propagation

Delivery does NOT automatically roll up from children.

Delivery definitions:

PBI delivered  
= first transition to Done

Feature delivered  
= Feature state transitions to Done

Epic delivered  
= Epic state transitions to Done

Parent completion is state-driven.

## Story Point propagation

Story Points propagate upward from PBIs.

Propagation path:

PBI → Feature → Epic → Objective

Tasks and Bugs never contribute.

## Effort propagation

Effort follows the rollup precedence defined in estimation rules.

Effort may originate from:

PBI  
Feature  
Epic

## Removed scope behavior

Removed items:

- do not contribute to velocity
- remain visible in historical scope
- contribute to churn analytics
