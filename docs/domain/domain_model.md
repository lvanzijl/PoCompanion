# PoTool Domain Model

Status: Draft  
Purpose: Define the canonical analytics interpretation of TFS / Azure DevOps data used by PoTool.

This document defines how PoTool interprets work item data so that planning, delivery, trend analysis, and health metrics use a single consistent domain model.

If implementation differs from this document, the implementation is considered a deviation until explicitly reviewed.

---

# 1. Purpose

PoTool is a Product Owner analytics tool built on top of TFS / Azure DevOps data.

Its goal is not to mirror TFS, but to provide a consistent interpretation of work item data for Product Owners and teams.

PoTool supports analysis and decision making for:

- backlog health
- sprint planning
- sprint execution
- delivery tracking
- trend analysis
- roadmap planning

The tool derives metrics and insights from work item data using a defined domain interpretation.

The purpose of this document is:

- to create a canonical interpretation of TFS data
- to prevent metric drift across PoTool features
- to enable consistent analytics across products and teams
- to guide implementation, audits, and future extensions

---

# 2. Work Item Hierarchy and Scope

## 2.1 Theoretical hierarchy

The theoretical hierarchy in TFS may include:

Goal  
Objective  
Epic  
Feature  
PBI / Bug  
Task

Not all levels are equally relevant for PoTool analytics.

---

## 2.2 Operational hierarchy

For planning and delivery analytics the operational hierarchy is:

Epic  
Feature  
PBI

Tasks exist below PBIs but are not planning units.

---

## 2.3 Tasks

Tasks represent implementation work.

Tasks:

- may generate activity signals
- may generate work signals
- never contribute to story points
- never contribute to effort rollups
- never count as delivery units

---

## 2.4 Bugs

Bugs behave structurally similar to PBIs but follow different estimation rules.

Bugs:

- do not contribute to story points
- do not contribute to velocity
- may contribute to activity signals
- may contribute to work signals

Bug workload may be analyzed separately.

---

## 2.5 Product scope

The primary analytical scope boundary is the **Product**.

A Product may contain multiple backlog roots.

PoTool aggregates all backlog roots belonging to a product into one logical product backlog.

Analytics operate at the **product level**, not per backlog root.

---

## 2.6 Parent-child structure

Expected hierarchy:

Objective → Epic  
Epic → Feature  
Feature → PBI  
PBI → Task

Direct relations skipping levels may exist but are considered structural anomalies.

Analytics must remain resilient to these anomalies.

---

## 2.7 Delivery unit

The fundamental delivery unit in PoTool is the **PBI**.

Implications:

- Story points originate at PBI level
- Velocity is based on PBIs
- Sprint delivery metrics originate at PBIs

Higher levels receive derived metrics but maintain independent lifecycle states.

---

## 2.8 Parent completion independence

Completion of all PBIs under a Feature does **not automatically mean** the Feature should be Done.

Feature and Epic completion remain explicit management decisions.

---

# 3. Estimation and Effort Semantics

## 3.1 Effort concept

Effort represents estimated implementation hours.

Effort is primarily used for reporting and analytical insight.

Teams typically do not plan directly using Effort.

Effort may appear on:

Epic  
Feature  
PBI

---

## 3.2 Effort rollup

Effort uses child precedence.

Rules:

1. If no immediate children contain Effort → use parent Effort  
2. If children contain Effort → sum child Effort  
3. Once children contain Effort → parent Effort is ignored

Rollup stops at the first level where child Effort exists.

---

## 3.3 Story Points concept

Story Points represent relative complexity of deliverable work.

Story Points are used for:

- sprint planning
- velocity
- delivery analytics
- forecasting

Story Points are team-relative.

---

## 3.4 Story Point field resolution

PoTool resolves story points using:

1. StoryPoints
2. BusinessValue
3. Missing estimate

Special rule:

0 SP and not Done → treated as missing estimate  
0 SP and Done → valid zero-point completion

---

## 3.5 Valid story point origin

Authoritative Story Points exist only on PBIs.

Story points on Bugs, Tasks, or other types are ignored.

---

## 3.6 Story Point rollup

Rules:

1. If PBIs contain SP → sum PBIs  
2. If PBIs contain no SP → parent estimate may be fallback  
3. Once any PBI has SP → parent estimate ignored

Removed PBIs do not contribute to active scope rollups.

---

## 3.7 Missing Story Points

Missing estimates must remain visible.

They must never silently become zero.

---

## 3.8 Derived estimates

If some PBIs have estimates and others do not:

DerivedSP = average(sibling PBIs with estimates)

Derived estimates:

- remain fractional
- are marked as derived
- used only for aggregation and forecasting
- never used for velocity

Same rule applies to Effort.

---

## 3.9 Parent estimate fallback

Parent estimates may be used only if no PBIs contain estimates.

Used only for:

- roadmap sizing
- backlog sizing
- forecasting

Never for velocity.

---

## 3.10 Bug estimation policy

Bugs never contribute to story point analytics.

---

## 3.11 Task estimation policy

Tasks are ignored for both SP and Effort analytics.

---

## 3.12 Hours per Story Point

PoTool may calculate:

HoursPerSP = DeliveredEffort / DeliveredSP

Purpose:

- validate sprint planning load
- calibrate forecasting

Diagnostic metric only.

---

# 4. State Classification Model

PoTool does not rely on raw TFS states.

Instead states are mapped to canonical states.

---

## 4.1 Canonical states

New  
InProgress  
Done  
Removed

All TFS states map to exactly one canonical state.

---

## 4.2 Per work item type mapping

Each work item type has its own mapping.

Structure:

WorkItemType → TfsState → CanonicalState

Mappings are configurable.

---

## 4.3 Configuration source

Mappings are stored in **PoTool Settings configuration**.

They are not hardcoded.

---

## 4.4 Purpose

Canonical states allow PoTool to interpret lifecycle behavior independent of the TFS process template.

Used for:

- work detection
- delivery detection
- churn detection
- spillover detection

---

## 4.5 Delivery definition

Delivery occurs when canonical state transitions to:

Done

---

## 4.6 Removed semantics

Transition to Removed represents scope removal.

Removed items:

- do not contribute to velocity
- remain visible in churn
- remain visible historically

---

## 4.7 Canonical state transition semantics

### Work start

New → InProgress

Work has started.

---

### Delivery

InProgress → Done

Only the **first Done transition** counts as delivery.

---

### Reopen

Done → InProgress

Represents rework.

Does not create new delivery.

---

### Scope removal

AnyState → Removed

Represents scope removal.

---

### Non-meaningful transitions

Transitions where canonical state does not change are ignored.

---

# 5. Sprint and Time Semantics

## 5.1 Sprint window

SprintWindow = [SprintStart, SprintEnd]

---

## 5.2 On sprint vs During sprint

On sprint:

IterationPath == SprintPath

During sprint:

EventTimestamp ∈ SprintWindow

---

## 5.3 Commitment timestamp

CommitmentTimestamp = SprintStart + 1 day

Committed scope = items on sprint at that timestamp.

---

## 5.4 Delivery detection

Delivery = first transition to Done within SprintWindow.

IterationPath is irrelevant.

---

## 5.5 First Done rule

Delivery counted only once.

---

## 5.6 Late delivery

Items completed just after SprintEnd may still count as late delivery.

---

## 5.7 Activity detection

Activity triggered by monitored changes except metadata fields.

Discussion counts as activity.

---

## 5.8 Work detection

Work requires meaningful state transitions.

---

## 5.9 Churn detection

Churn = IterationPath changes after commitment.

Added churn = item enters sprint  
Removed churn = item leaves sprint

---

## 5.10 Spillover

Spillover occurs when:

- committed item
- not Done at SprintEnd
- moves to next sprint

---

## 5.11 Historical delivery integrity

Once delivery is attributed to a sprint it remains historically valid.

---

# 6. Hierarchy Propagation Rules

## 6.1 Propagation origins

Task/PBI origin:

Activity  
Work

PBI origin:

StoryPoints  
Effort  
Delivery  
Velocity  
Spillover

---

## 6.2 Activity propagation

Activity propagates upward as boolean presence.

---

## 6.3 Work propagation

Work propagates upward as boolean presence.

---

## 6.4 Delivery propagation

Feature and Epic delivery are state-driven, not child rollups.

---

## 6.5 Story point propagation

StoryPoints originate only from PBIs.

---

## 6.6 Effort propagation

Effort follows precedence rules defined earlier.

---

## 6.7 Removed scope

Removed items remain visible historically.

---

# 7. Metric Definitions

## Velocity

Velocity = sum of SP of PBIs whose first Done occurs in SprintWindow.

---

## CommittedSP

SP of PBIs on sprint at CommitmentTimestamp.

---

## CommitmentCompletion

DeliveredSP / (CommittedSP − RemovedSP)

---

## ChurnRate

(AddedSP + RemovedSP) / (CommittedSP + AddedSP)

---

## SpilloverRate

SpilloverSP / (CommittedSP − RemovedSP)

---

## AddedDeliveryRate

DeliveredFromAddedSP / AddedSP

---

## UnestimatedDelivery

Count of delivered PBIs without SP.

---

# 8. Source Model

## Snapshot truth

Snapshots answer questions about **current state**.

---

## Update truth

Updates answer questions about **what happened over time**.

---

## Hybrid analyses

Some analytics combine both sources.

---

## Conflict resolution

If snapshot and update reconstruction disagree for current-state questions:

Snapshot is authoritative.
