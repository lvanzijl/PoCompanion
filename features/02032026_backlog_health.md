# Backlog State Model Specification

## Status  
Concept — to be implemented  
Scope: Product-centered Backlog Overview

---

# PART A — Backlog State Model

# 1. Purpose

This document defines the Backlog State Model for the application.

The objective is to:

1. Shift the landing page from validator-category visibility to backlog maturity visibility.
2. Clearly separate:
   - Refinement responsibility (Product Owner)
   - Implementation readiness responsibility (Team)
   - Integrity (maintenance, separate stream)
3. Define a deterministic and explainable refinement percentage per:
   - Epic
   - Feature
   - Product Backlog Item (PBI)
4. Explicitly define ownership per Feature.
5. Define the user interaction flow for backlog decision-making.

This model builds on the existing hierarchical validation rules described in the validation system report :contentReference[oaicite:0]{index=0}.

---

# 2. Conceptual Separation

The application distinguishes three independent streams:

## 2.1 Backlog Readiness (Primary)

Answers:

- What can be planned?
- What needs refinement?
- How mature is this scope?

Based on:

- Refinement Readiness (RR)
- Refinement Completeness (RC)

Structural Integrity (SI) does not influence readiness scores.

## 2.2 Integrity (Maintenance)

Includes:

- SI-1
- SI-2
- SI-3 :contentReference[oaicite:1]{index=1}

Integrity:

- Does not influence refinement percentage
- Does not influence readiness sorting
- Is presented separately
- Is considered a quality signal, not a planning blocker

## 2.3 Scope

The Backlog Overview is:

- Product-centered
- Not team-centered
- Not sprint-centered

It answers:

What is the current maturity of my product backlog?

---

# 3. Ownership Model

## 3.1 Epic

Owner: Product Owner

Epics are strategic containers. Developers use them only for context.

## 3.2 Feature

Owner is dynamic:

- Product Owner when refinement is blocked
- Team when implementation readiness is incomplete
- Ready when fully refined

Ownership is explicitly shown only at Feature level.

## 3.3 PBI

Owner: Team

PBIs represent implementable work.

---

# 4. Epic Refinement Model

Epics represent scope maturity, not implementation readiness.

## 4.1 Refinement Gates

If Epic description is empty:
- Score = 0%

If Epic has no Features:
- Score = 30%

## 4.2 Refinement Score

If both gates pass:

Epic score = average of Feature refinement scores

Epic effort:

- Does not influence refinement percentage
- If missing, it is treated as an Integrity signal

Epic is 100% when all Features are 100%.

---

# 5. Feature Refinement Model

Features are the primary refinement and planning unit.

## 5.1 Refinement Blocker (PO)

If Feature description is empty:
- Score = 0%
- Owner = PO

## 5.2 Structural Completeness

If description exists but no PBIs:
- Score = 25%

## 5.3 Implementation Readiness

If description exists and PBIs exist:

Feature score = average of PBI readiness scores

Ownership:

- If description missing → Owner = PO
- Else if score < 100% → Owner = Team
- Else → Owner = Ready

---

# 6. PBI Readiness Model

PBIs represent implementable work.

## 6.1 Rules

- Description required
- Effort required :contentReference[oaicite:2]{index=2}

## 6.2 Readiness Score

If description is empty:
- Score = 0%

If description exists but effort missing:
- Score = 75%

If description and effort present:
- Score = 100%

Owner: Team

---

# 7. Landing Page Structure

The Backlog Overview shows:

READY FOR IMPLEMENTATION  
- Epics where all Features are 100%

NEEDS REFINEMENT  
- Epics sorted descending by refinement percentage  
- All Features are visible (including 100%)

INTEGRITY MAINTENANCE  
- Count of Structural Integrity findings  
- Link to Validation Queue

Sorting rule:

- Highest refinement percentage first

---

# PART B — User Journey & Interaction Flow

# 8. Entry Point

User opens the application and lands on:

Backlog Overview (Product-scoped)

No validator categories are shown.

The page communicates:

- What is plan-ready
- What needs refinement
- What requires maintenance

---

# 9. Ready for Implementation Flow

User sees:

READY FOR IMPLEMENTATION  
Epic A (100%)  
Epic B (100%)

User clicks Epic A.

System behavior:

- Opens Work Item Explorer
- RootWorkItemId = Epic A
- Epic itself hidden in tree
- Descendants (Features and PBIs) shown immediately
- Page title reflects: "Epic A – Ready Features"

User can:

- Inspect Features
- Plan via Plan Board
- Open items in TFS

This is the planning flow.

---

# 10. Needs Refinement Flow

User sees:

NEEDS REFINEMENT  
Epic C – 82%  
Epic D – 60%  
Epic E – 35%

Epics sorted descending by percentage.

User clicks Epic C.

System shows:

- All Features under Epic C
- Each Feature shows:
  - Percentage
  - Ownership badge (PO / Team / Ready)

User selects a Feature.

System behavior:

- Opens Work Item Explorer
- RootWorkItemId = Epic C
- Scrolls or highlights selected Feature subtree
- User refines directly in TFS or inspects in Explorer

No internal wizard is required.

After sync:

- Scores update automatically.

---

# 11. Feature Ownership Flow

If Feature shows:

Owner = PO  
User knows:
- Description missing
- Must clarify scope

If Owner = Team  
User knows:
- PBIs incomplete
- Effort or PBI details missing
- Refinement session needed with devs

If Owner = Ready  
Feature is planable.

---

# 12. Integrity Maintenance Flow

User sees:

INTEGRITY MAINTENANCE  
12 structural issues

User clicks.

System navigates to existing Validation Queue filtered to SI rules.

User resolves:

- State inconsistencies
- Parent/child violations

Integrity does not affect readiness scores.

---

# 13. Sync Behavior

After a sync:

- Refinement percentages are recalculated
- Ownership states are recalculated
- Landing page updates automatically

No manual recompute required.

---

# 14. Design Principles

1. Readiness is separate from integrity.
2. Refinement blockers and implementation blockers are not conflated.
3. Ownership is explicit only at Feature level.
4. Epics are PO responsibility.
5. PBIs are Team responsibility.
6. Percentages must be explainable.
7. The model must remain deterministic and independent of velocity or sprint data.
8. Explorer is a support tool, not the primary entry point.

---

End of specification.
