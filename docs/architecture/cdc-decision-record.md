# CDC Decision Record — Delivery Analytics & Planning Model

## Status
Accepted

---

# 1. Scope & Filtering Model

## Dimensions

Filtering is always applied BEFORE aggregation.

Supported dimensions:

- Product (primary)
- Project (mandatory)
- Work Package (optional)
- Time:
  - Single sprint
  - Linked sprint sequence (N sprints)
  - Date range

## Default

- All products
- All projects
- Current sprint (if applicable)

## Rules

- No project selected → not allowed
- No work package → aggregate all within project

---

## Mixed Mode

When multiple products use different estimation modes:

- Mode = Mixed

Behavior:

- SP-mode features:
  - weight = Story Points
- NoSPMode features:
  - weight = 1 (count)

Aggregation allowed.

UI must show:
- “Mixed mode — different estimation models combined”

---

# 2. Lifecycle vs Readiness

## Lifecycle (canonical states)

- New
- InProgress
- Done
- Removed

Configured via state classification mapping.

## Readiness

- Separate concept
- Not part of lifecycle
- Driven by:
  - refinement state (e.g. Approved)
  - PlanningQuality

---

# 3. Field Contract

## Fields

- ProjectNumber:
  - Rhodium.Funding.ProjectNumber
  - string
  - used on Epic

- WorkPackage:
  - Rhodium.Funding.ProjectElement
  - string
  - used on Epic

- Effort:
  - Microsoft.VSTS.Scheduling.Effort
  - double (hours)
  - used on:
    - Feature (primary)
    - Epic (optional, not leading)

- Override (Progress):
  - Microsoft.VSTS.Common.TimeCriticality
  - double (0–100)
  - used on Feature only

## Rules

- Fields may exist on all work items
- Only consumed where defined
- Others ignored

---

# 4. Progress Model

## Modes

### SP-mode
- Progress based on Story Points
- Weight = Story Points

### NoSPMode
- Progress based on item count
- Weight = count

---

## Feature

- CalculatedProgress (from PBIs)
- Override (optional)

EffectiveProgress = override ?? calculated

---

## Override Rules

- null → no override
- 0 → valid
- 100 → valid
- clamp to 0–100

### Validation

- Out of range → PQ warning
- 0.0–1.0 range → PQ warning (likely wrong scale)

---

## Epic

EpicProgress =
Σ (FeatureEffectiveProgress × FeatureWeight)
-------------------------------------------
Σ (FeatureWeight)

---

# 5. Forecast Model

## Feature

ForecastConsumed = EffectiveProgress × Effort  
ForecastRemaining = Effort − ForecastConsumed

---

## Epic (Variant A — enforced)

EpicForecastConsumed = Σ FeatureForecastConsumed  
EpicForecastRemaining = Σ FeatureForecastRemaining

Rules:

- No recalculation via Epic effort
- Feature is source of truth

---

## Invariants

- Forecast ≠ actual
- No mixing of Story Points and Effort

---

# 6. Snapshot Model

## Scope

Snapshot is defined by:

- Project (mandatory)
- Work Package (optional filter)

---

## Data

### Epic

- EpicId
- Progress
- ForecastConsumed
- ForecastRemaining

### Feature (mandatory)

- FeatureId
- EpicId
- EffectiveProgress
- CalculatedProgress
- Override
- ForecastConsumed
- ForecastRemaining
- Weight
- IsExcluded

---

## Properties

- Fully denormalized
- No recomputation required

---

## Budget

- Defined at Project level
- Stored in snapshot
- Mutable over time

BudgetRemaining = Budget − ForecastConsumed

---

## Delta

Between snapshots:

- DeltaConsumed
- DeltaRemaining
- DeltaProgress
- DeltaBudget

---

# 7. PlanningQuality

## Purpose

Improve:
- data quality
- forecast reliability

---

## Characteristics

- Non-blocking
- Warning-based
- Feature-driven

---

## Initial Rules

- PQ-2 Missing story points (SP-mode)
- PQ-3 Feature excluded (no weight)
- PQ-5 Override used
- PQ-8 Feature has PBIs but no effort
- PQ-10 Remaining increased

Additional:

- Override out of range
- Override likely wrong scale

---

## Score

- Calculated at Feature
- Aggregated upward:
  - Feature → Epic → Product → Project

---

# 8. UI Principles

- Score always visible
- Inline indicators
- Drilldown available

Show:

- Progress
- Forecast
- Planning Quality
- Delta

---

## Required signals

- Mixed mode
- Override usage
- Scope increase
- Budget changes

---

# 9. Implementation Strategy

Strict order:

1. Progress calculation (feature)
2. Aggregation (epic)
3. Snapshot model
4. Forecast
5. PlanningQuality
6. Score
7. Delta
8. API
9. UI (epic + feature)
10. UI aggregation

---

# 10. Key Decisions

- Feature is source of truth
- Forecast via aggregation (Variant A)
- Budget is mutable
- Mixed mode supported
- PlanningQuality replaces advisory concepts

---

# End of document
