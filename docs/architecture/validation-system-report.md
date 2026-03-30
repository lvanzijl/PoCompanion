# Validation System Comprehensive Report

## Overview

The PoCompanion validation system uses a **hierarchical rule-evaluation system** as the primary engine
for all rule-based checks. The hierarchical-to-legacy compatibility adapter bridges hierarchical validation
results into the legacy compatibility query surface.

### Architecture

```
Legacy compatibility query surface
└── Hierarchical rule-evaluation contract
        ├── SI rules: SI-1, SI-2, SI-3
        ├── RR rules: RR-1, RR-2, RR-3
        └── RC rules: RC-1, RC-2, RC-3
```

The legacy compatibility query surface is consumed by:
- `GetAllWorkItemsWithValidationQueryHandler` (powers ValidationQueue and Fix Session pages)
- `GetValidationViolationHistoryQueryHandler`
- `GetValidationImpactAnalysisQueryHandler`
- `GetWorkItemByIdWithValidationQueryHandler`
- `SyncChangesSummaryService`

The hierarchical rule-evaluation contract is also consumed directly by:
- `GetBacklogHealthQueryHandler` (health metrics)
- `GetMultiIterationBacklogHealthQueryHandler` (health metrics)
- `ValidationComputeStage` (sync pipeline, stores `CachedValidationResults` indicators)

---

## Validation Categories

The system uses three validation categories that map to different stakeholder responsibilities:

### 1. Structural Integrity (SI)
- **Responsibility**: Product Owner / Process
- **Purpose**: Detect logically inconsistent work item trees
- **Evaluation**: Always evaluated; never suppressed
- **Maps to**: `ValidationCategory.StructuralIntegrity`
- **Severity in pipeline**: Error

### 2. Refinement Readiness (RR)
- **Responsibility**: Product Owner
- **Purpose**: Ensure intent and context exist before refinement proceeds
- **Evaluation**: After Structural Integrity; **suppresses all RC (Refinement Completeness) rules** when any RR violation exists
- **Maps to**: `ValidationCategory.RefinementReadiness`
- **Severity in pipeline**: Warning

### 3. Refinement Completeness (RC)
- **Responsibility**: Development Team
- **Purpose**: Assess whether PBIs are ready for implementation
- **Evaluation**: **Only if zero RR violations exist for the tree**
- **Maps to**: `ValidationCategory.RefinementCompleteness`
- **Severity in pipeline**: Warning

---

## All Validation Rules

### Structural Integrity Rules (Hierarchical)

#### SI-1: Done Parent with Unfinished Descendants
- **Rule ID**: `SI-1`
- **Consequence**: `BacklogHealthProblem`
- **Category**: Structural Integrity
- **Applies When**: A parent work item is in "Done" state
- **Validates**: All descendants (recursive) must be in "Done" or "Removed" state
- **Severity**: Error
- **Suppresses**: Nothing (SI rules never suppress other categories)
- **File**: `DoneParentWithUnfinishedDescendantsRule.cs`

#### SI-2: Removed Parent with Unfinished Descendants
- **Rule ID**: `SI-2`
- **Consequence**: `BacklogHealthProblem`
- **Category**: Structural Integrity
- **Applies When**: A parent work item is in "Removed" state
- **Validates**: All descendants (recursive) must be in "Done" or "Removed" state
- **Severity**: Error
- **Suppresses**: Nothing
- **File**: `RemovedParentWithUnfinishedDescendantsRule.cs`

#### SI-3: New Parent with In-Progress Descendants
- **Rule ID**: `SI-3`
- **Consequence**: `BacklogHealthProblem`
- **Category**: Structural Integrity
- **Applies When**: A parent work item is in "New" state
- **Validates**: No descendant should be in "In Progress" state
- **Severity**: Error
- **Suppresses**: Nothing
- **Overlap**: Same scenario caught by SI-3 from a structural perspective
- **File**: `NewParentWithInProgressDescendantsRule.cs`

---

### Refinement Readiness Rules (Hierarchical)

> **Suppression rule**: If **any** of the following RR rules fire for a tree,
> **all RC rules are suppressed** for that entire tree.

#### RR-1: Epic Description Empty
- **Rule ID**: `RR-1`
- **Consequence**: `RefinementBlocker`
- **Category**: Refinement Readiness
- **Applies When**: Work item type is "Epic" AND state is not Done/Removed
- **Validates**: Description must be at least 10 characters long
- **Severity**: Warning
- **Suppresses**: All RC rules (RC-1, RC-2, RC-3) for this tree when violated
- **File**: `EpicDescriptionEmptyRule.cs`

#### RR-2: Feature Description Empty
- **Rule ID**: `RR-2`
- **Consequence**: `RefinementBlocker`
- **Category**: Refinement Readiness
- **Applies When**: Work item type is "Feature" AND state is not Done/Removed
- **Validates**: Description must be at least 10 characters long
- **Severity**: Warning
- **Suppresses**: All RC rules for this tree when violated
- **File**: `FeatureDescriptionEmptyRule.cs`

#### RR-3: Epic Without Features
- **Rule ID**: `RR-3`
- **Consequence**: `RefinementBlocker`
- **Category**: Refinement Readiness
- **Applies When**: Work item type is "Epic" AND state is not Done/Removed
- **Validates**: Epic must have at least one direct child of type "Feature"
- **Severity**: Warning
- **Suppresses**: All RC rules for this tree when violated
- **File**: `EpicWithoutFeaturesRule.cs`

---

### Refinement Completeness Rules (Hierarchical)

> These rules are **suppressed** (not evaluated) when any RR violation exists for the tree.

#### RC-1: PBI Description Empty
- **Rule ID**: `RC-1`
- **Consequence**: `IncompleteRefinement`
- **Category**: Refinement Completeness
- **Applies When**: Work item type is "Product Backlog Item" AND state is not Done/Removed
- **Validates**: Description must not be empty
- **Severity**: Warning
- **Not applicable when**: Any RR rule fires for the parent tree (suppressed)
- **File**: `PbiDescriptionEmptyRule.cs`

#### RC-2: PBI Effort Empty
- **Rule ID**: `RC-2`
- **Consequence**: `IncompleteRefinement`
- **Category**: Refinement Completeness
- **Applies When**: Work item type is "Product Backlog Item" AND state is not Done/Removed
- **Validates**: Effort must not be null or zero
- **Severity**: Warning
- **Not applicable when**: Any RR rule fires for the parent tree (suppressed)
- **File**: `PbiEffortEmptyRule.cs`

#### RC-3: Feature Without Children
- **Rule ID**: `RC-3`
- **Consequence**: `IncompleteRefinement`
- **Category**: Refinement Completeness
- **Applies When**: Work item type is "Feature" AND state is not Done/Removed AND Feature description is valid (≥ 10 chars)
- **Validates**: Feature must have at least one direct child of type "Product Backlog Item"
- **Severity**: Warning
- **Not applicable when**:
  1. Any RR rule fires for the parent tree (suppressed), OR
  2. The Feature itself has an empty/short description (RC-3 explicitly skips such features, since RR-2 already covers them)
- **File**: `FeatureWithoutChildrenRule.cs`

---

## Suppression Logic Summary

```
Evaluation order per tree:
  Phase 1: SI rules (always run, never suppressed, never suppress others)
  Phase 2: RR rules (always run after SI, never suppressed)
  Phase 3: RC rules (only run if Phase 2 produced ZERO violations)
```

### Override Table

| Scenario | Rules That Fire | Rules Suppressed |
|----------|-----------------|------------------|
| Epic description < 10 chars | SI-* (if applicable), **RR-1** | RC-1, RC-2, RC-3 |
| Feature description < 10 chars | SI-* (if applicable), **RR-2** | RC-1, RC-2, RC-3 |
| Epic has no Feature children | SI-* (if applicable), **RR-3** | RC-1, RC-2, RC-3 |
| Parent (New) with InProgress child | **SI-3** (structural) | None |
| Parent (Done) with non-Done child | **SI-1** | None |
| Parent (Removed) with non-Done child | **SI-2** | None |
| PBI description empty (no RR issues) | **RC-1** | None |
| PBI effort empty (no RR issues) | **RC-2** | None |
| Feature has no PBIs (no RR issues, valid desc) | **RC-3** | None |
| PBI description empty (RR issue present) | *(suppressed)* | RC-1 not shown |
| Feature without PBIs (RR issue present) | *(suppressed)* | RC-3 not shown |

---

## Summary Table

| Rule ID | Name | Category | Applies To | Severity | System | Suppresses |
|---------|------|----------|------------|----------|--------|------------|
| SI-1 | Done parent w/ unfinished descendants | Structural Integrity | Any parent in Done | Error | Hierarchical | — |
| SI-2 | Removed parent w/ unfinished descendants | Structural Integrity | Any parent in Removed | Error | Hierarchical | — |
| SI-3 | New parent w/ InProgress descendants | Structural Integrity | Any parent in New | Error | Hierarchical | — |
| RR-1 | Epic description empty | Refinement Readiness | Epic (non-terminal) | Warning | Hierarchical | RC-1, RC-2, RC-3 |
| RR-2 | Feature description empty | Refinement Readiness | Feature (non-terminal) | Warning | Hierarchical | RC-1, RC-2, RC-3 |
| RR-3 | Epic without Features | Refinement Readiness | Epic (non-terminal) | Warning | Hierarchical | RC-1, RC-2, RC-3 |
| RC-1 | PBI description empty | Refinement Completeness | PBI (non-terminal) | Warning | Hierarchical | — |
| RC-2 | PBI effort empty | Refinement Completeness | PBI (non-terminal) | Warning | Hierarchical | — |
| RC-3 | Feature without children (PBIs) | Refinement Completeness | Feature (non-terminal, valid desc) | Warning | Hierarchical | — |

---

## State Classification

All rules use `IWorkItemStateClassificationService` to classify work item states:

| Classification | Default States |
|----------------|---------------|
| New | "New", "Proposed", "Approved" |
| InProgress | "In Progress", "Active", "Committed" |
| Done | "Done", "Closed", "Resolved" |
| Removed | "Removed" |

Rules skip work items in terminal states (Done/Removed) unless the rule explicitly checks them
(e.g., SI-1 checks Done parents specifically).

---

## Bug History

### Bug: Hierarchical Rule Violations Not Shown in Validation Queue
**Symptom**: The Validation Queue and Fix Session pages showed no SI, RR, or RC violations.
Health cards showed counts but clicking through showed empty queues.

**Root Cause**: `GetAllWorkItemsWithValidationQueryHandler` used only the legacy compatibility query surface
(the legacy composite rule component), which only ran a now-removed legacy rule component. The
hierarchical rules were only used in health/metrics endpoints.

**Fix**: Introduced the hierarchical-to-legacy compatibility adapter, which implements the legacy compatibility query surface
by delegating to the hierarchical rule-evaluation contract and converting `HierarchicalValidationResult`
violations to `ValidationIssue` objects.

### Bug: RuleId Collision on Legacy Rule Component
**Symptom**: The UI showed "Epic must have at least one Feature child" for violations that were
actually from the legacy rule component. Violation counts and descriptions were mixed.

**Root Cause**: Both `EpicWithoutFeaturesRule` (hierarchical) and the now-removed legacy rule component
used the same RuleId `"RR-3"`.

**Fix**: Renamed the legacy rule component's RuleId to avoid collision. The legacy rule component was
subsequently removed entirely.

---

**Report Status**: ✅ Accurate as of current codebase
