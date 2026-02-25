Status: Draft

# Sprint Trends – Human Readable Functional Description (Rewritten)

## Purpose of the Feature

Sprint Trends is a historical and analytical view that shows how Products progress over time, based on actual work performed in sprints. It no longer compares "planned vs worked" using revision snapshots. Instead, it derives progression from activity events and the current work item state model.

The goal of the feature is to answer:

What meaningful progress was made in a sprint, at Epic, Feature, and PBI level — and how does that trend evolve across sprints and products?

The page is part of the Trends (Past) workspace and focuses on outcome and progression rather than planning accuracy.

---

# Core Questions It Must Answer

For a given sprint (default: current sprint) or a selected sprint range:

### 1. What Epics and Features were actively worked on?
- Determined by activity on underlying PBIs and Tasks.
- If PBIs or Tasks under a Feature/Epic had qualifying activity during the sprint, that Feature/Epic is considered "worked on".

---

### 2. What is the state of those Epics and Features?

State is calculated using effort-weighted completion:

Feature:
- Total effort = sum of effort of its direct child PBIs.
- Completed effort = sum of effort of PBIs in Done state.
- Feature % Done = Completed effort divided by Total effort.

Epic:
- Total effort = sum of effort of its direct child Features.
- Completed effort = sum of effort of completed child Features.
- Epic % Done = Completed effort divided by Total effort.

This makes Epic and Feature state mathematically derived from PBI-level effort.

---

### 3. What is the progression within that sprint?

Progression is calculated as a delta:

State at end of current sprint
minus
State at end of previous sprint

The difference is the progression made in the sprint.

This applies to:
- Feature progression
- Epic progression

The focus is not on "planned vs worked", but on measurable forward movement.

---

### 4. How much effort was completed?

This is primarily a PBI-level metric.

- Count of PBIs that transitioned to Done in the sprint.
- Sum of effort of those completed PBIs.

This is important because Features and Epics are often marked Done later than the underlying PBIs.

It must be clearly visible that this metric is calculated at PBI level.

---

### 5. Bug Information per Sprint

For the sprint timeline (based on dates, not iteration path):

- Number of new bugs created during the sprint.
- Number of bugs worked on during the sprint.
- Number of bugs closed during the sprint.

Definition of "Bug Worked On":
A bug is considered worked on if:
- Any of its child Tasks had a state change.

This decouples bug activity from just state transitions on the bug itself.

---

# Graph Requirements (Historical Trends)

The page must provide trend graphs covering the previous 10 sprints.

Per Product:

1. Progression per sprint  
   (Feature/Epic delta across sprints)

2. Completed PBIs (count) per sprint

3. Completed PBI effort (sum of effort) per sprint

When multiple products are selected → use line or bar graphs.

When showing a single sprint snapshot across multiple products or categories → use pie charts.

---

# Effort Completeness and Missing Effort Handling

Effort is required to calculate:

- Feature state
- Epic state
- Progression

If effort is missing on a PBI:

- A warning must be shown.
- The system may approximate missing effort using the average effort of sibling PBIs.

Example:

PBI1: 50  
PBI2: 100  
PBI3: missing  

Estimated effort PBI3 = (50 + 100) / 2 = 75

If such estimation is used:
- State and progression must be clearly marked as approximate.
- The warning must be visible.

Without effort, state cannot be calculated correctly.

---

# Time Range and Navigation Model (Rewritten)

The previous model (select team + from sprint + to sprint) is replaced.

Default Behavior:

- Always assume the current sprint.
- Display back and forward arrows to move to previous or next sprint.
- Disable navigation if sprint is not available.

Scope:

- Always assume all teams under the current Product Owner.
- Add filters to restrict to:
  - Specific Product
  - Specific Teams

Advanced Mode:

Provide an advanced sprint selection mode that allows:
- Selecting multiple sprints
- Viewing aggregated or comparative historical data

This mode enables the 10-sprint trend graphs.

---

# Hierarchy and Aggregation Model

Calculations are performed at:

- PBI level (completion and effort)
- Feature level (derived from PBIs)
- Epic level (derived from Features)

Product attribution is resolved via the resolved work item hierarchy.

Progression is grouped by:
- Sprint
- Product

---

# Visualization Rules

Trend across multiple sprints and products → Line or bar graph  
Single sprint snapshot across multiple products or categories → Pie chart  
Feature/Epic progress → Percentage / progress bars  
Missing effort affecting state → Warning indicator  

---

# Data Model Assumptions

For the feature to function correctly:

- WorkItems table must be populated.
- ActivityEventLedgerEntries must contain activity events.
- Sprints must contain valid start/end dates.
- Products must be resolved via ResolvedWorkItems.
- SprintMetricsProjections must be populated via ComputeProjectionsAsync().

The feature no longer depends on:
- RevisionHeaders
- RevisionFieldDeltas
- RevisionRelationDeltas
- WorkItemRevisions
- OData revision ingestion

All revision-based infrastructure has been removed.

---

# Summary

Sprint Trends is no longer a planned vs worked comparison tool.

It is now a:

- Progression analytics feature
- Effort-weighted state calculator
- Sprint-based delta analyzer
- Product-level trend visualizer
- Bug activity summarizer

It derives all metrics from activity events and the current hierarchy model, and it focuses on measurable forward movement across sprints.
