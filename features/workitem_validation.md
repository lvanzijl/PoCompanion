# Feature: Hierarchical Work Item Validation with Explicit Consequences

---

## 1. Purpose

Introduce a **deterministic, hierarchical validation model** for work items that distinguishes between:

- structural backlog health issues,
- refinement blockers caused by missing intent,
- incomplete refinement at implementation level.

The goal is to make backlog quality **measurable, explainable, and actionable**, while clearly separating **responsibility** between Product Owner and Development Team.

This feature **interprets** existing TFS data.  
It does **not** modify TFS work items, states, or workflows.

---

## 2. Core Principles

- Validation is **hierarchical**, not flat.
- Not all validation errors have the same consequences.
- Higher-level incompleteness suppresses lower-level rules.
- Every validation rule has:
  - a clear scope,
  - a responsible role,
  - an explicit consequence.

---

## 3. Validation Categories and Rules

### 3.1 Structural Integrity  
**Classification:** Backlog Health Problems  
**Responsibility:** PO / Process  
**Evaluation:** Always evaluated

These rules detect **logically inconsistent work item trees**.  
They do **not** block refinement or implementation.

#### Rules

- **SI-1**  
  A parent in `Done` state with any descendant (recursive) not in `Done` or `Removed` is invalid.

- **SI-2**  
  A parent in `Removed` state with any descendant (recursive) not in `Done` or `Removed` is invalid.

- **SI-3**  
  A parent in `New` state with any descendant in `In Progress` is invalid.

#### Consequence
- Report as **Backlog Health Problem**
- Never suppresses other validation categories

---

### 3.2 Refinement Readiness  
**Classification:** Refinement Blockers  
**Responsibility:** Product Owner  
**Evaluation:** After Structural Integrity, before PBI-level rules

These rules ensure **intent and context** exist before refinement proceeds.

#### Rules

- **RR-1**  
  Epic description is empty → invalid

- **RR-2**  
  Feature description is empty → invalid

#### Consequence
- Tree is **not ready for refinement**
- Classified as **Refinement Blocker**
- **Suppresses all PBI-level validation rules**

Rationale:  
Evaluating PBIs without clear Epic/Feature intent is meaningless.

---

### 3.3 Refinement Completeness  
**Classification:** Incomplete Refinement  
**Responsibility:** Development Team  
**Evaluation:** Only if all Refinement Readiness rules pass

These rules assess whether PBIs are **ready for implementation**.

#### Rules

- **RC-1**  
  PBI description is empty → invalid

- **RC-2**  
  PBI effort is empty → invalid

#### Consequence
- Classified as **Incomplete Refinement**
- Blocks implementation readiness

---

## 4. Evaluation Order (Explicit)

Validation **must** be evaluated in the following order:

1. Structural Integrity  
2. Refinement Readiness  
3. Refinement Completeness  

Lower categories must **not** execute if higher-level blockers apply.

---

## 5. Resulting System Signals

A single work item tree may produce:

- Backlog Health Problems
- Refinement Blockers  
- Incomplete Refinement  

However:
- **Refinement Blockers** and **Incomplete Refinement** can never coexist for the same tree.

This enables unambiguous reporting, filtering, and ownership attribution.

---

## 6. Responsibility Model

| Level   | Rule Type               | Responsible Party |
|--------|--------------------------|-------------------|
| Parent | Structural consistency   | PO / Process     |
| Epic   | Description completeness | Product Owner    |
| Feature| Description completeness | Product Owner    |
| PBI    | Description completeness | Dev Team         |
| PBI    | Effort completeness      | Dev Team         |

---

## 7. Out of Scope

- No changes to TFS work item states or workflows
- No automatic fixing of invalid items
- No UI redesign (presentation handled separately)
- No new lifecycle states

---

---

# GitHub Copilot — Implementation Planning Prompt

```markdown
# Goal
Create a concrete, step-by-step **implementation plan** (not code) for implementing the hierarchical work item validation model defined in this feature.

The plan must be:
- Technically actionable
- Incremental and phased
- Suitable for execution over multiple PRs
- Written in Markdown

Do **not** implement code yet.

---

## Validation Model (Authoritative)

### Validation Categories (in order)

1) Structural Integrity → Backlog Health Problems  
2) Refinement Readiness → Refinement Blockers (PO responsibility)  
3) Refinement Completeness → Incomplete Refinement (Dev responsibility)

Lower categories must not execute if higher-level blockers apply.

---

### Rules

**Structural Integrity**
- Done parent + unfinished descendants → invalid  
- Removed parent + unfinished descendants → invalid  
- New parent + in-progress descendants → invalid  

**Refinement Readiness**
- Epic description empty → invalid  
- Feature description empty → invalid  

**Refinement Completeness** (only if Epic + Feature valid)
- PBI description empty → invalid  
- PBI effort empty → invalid  

---

## What the Plan Must Cover

1. **Domain model changes**
   - Representation of validation rules
   - Representation of consequences
   - Responsibility attribution

2. **Rule evaluation flow**
   - Enforcing hierarchical order
   - Suppression logic
   - Recursive tree evaluation

3. **Validation result model**
   - Aggregation of multiple rule results
   - Final status derivation per tree
   - Categorization of outcomes

4. **Integration points**
   - Where validation is triggered
   - How existing rules are reused/refactored
   - Impact on current dashboards or metrics

5. **Testing strategy**
   - Unit tests per rule
   - Tests for suppression logic
   - Mixed-scenario tests

6. **Incremental rollout**
   - Phasing strategy
   - Backward compatibility considerations

---

## Constraints

- Respect existing architecture and layering
- Do not invent new workflow states
- Do not collapse rules into boolean flags
- Prefer explicit rule objects over hard-coded conditionals

---

## Output Format

- Markdown
- Clear headings
- Ordered phases
- Each phase ends with a **Definition of Done**

END
