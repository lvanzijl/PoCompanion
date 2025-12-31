# Feature: Release Planning Board (Agent-Ready Specification)

> This document is written to be consumed by **Copilot coding agents**.  
> Scope is fully defined. No assumptions are allowed beyond what is written here.

---

## 0. Agent Implementation Notes (READ FIRST)

- This feature is **not** a TFS replacement.
- The board is a **persisted planning artifact** stored in the tool’s own database.
- TFS is **read-only**, except for **Epic creation during Epic Split**.
- Layout, connectors, indicators, previews, and visuals are **derived**, not persisted.
- Do **not** infer dates, iteration logic, or dependencies beyond defined rules.

### UI Principle (MANDATORY)
If two elements have different semantics, they **MUST be visually distinguishable without relying on labels alone**.

---

## 1. Canonical Terminology (MANDATORY)

The following terms are canonical and MUST be used consistently:

- **Lane** → represents an **Objective**
- **Row** → a planning level (integer, vertical order)
- **EpicPlacement** → an Epic placed at `(Lane, Row)`
- **Unplanned Epic** → Epic not present in any Lane
- **Connector** → visual ordering flow (derived, never persisted)
- **Milestone Line** → horizontal global marker (e.g. Release)
- **Iteration Line** → horizontal global iteration boundary

No synonyms are allowed in code, comments, or tests.

---

## 2. Ownership & Persistence Contract

### 2.1 Persisted (Local Database Only)

- Lane (Objective reference)
- EpicPlacement:
  - EpicId
  - LaneId
  - RowIndex
  - OrderInRow
- Milestone Lines (type, label, vertical position)
- Iteration Lines (type, label, vertical position)
- Export configurations (last-used options)
- Cached validation results (warnings/errors)

### 2.2 Explicitly NOT Persisted

- Connectors
- Drag placeholders
- Layout previews
- Tooltip content
- Validation UI state

---

## 3. Invariants (MUST HOLD)

- An Epic exists in **at most one Lane**
- An Epic exists in **at most one Row**
- A Row contains **1..N Epics**
- Rows are **contiguous integers** (no gaps)
- Epics are always **snapped to Rows** (never between Rows)
- Dragging Epics across Lanes is **forbidden**
- Connectors are **derived only**
- Connector graph is **acyclic**
- Board never mutates TFS ordering, iteration, or dates

---

## 4. Lanes

- A **Lane represents one Objective**
- Multiple Lanes are shown side-by-side
- Each Lane contains Epics whose parent Objective matches the Lane
- Lane membership is immutable via the board UI

---

## 5. Rows (Planning Levels)

- Rows represent **relative time**
- RowIndex increases top → bottom
- Epics in the same Row are considered **parallel**
- RowIndex is authoritative; pixel position is **derived**
- During drag, placement **snaps to Rows only**

---

## 6. Epic Placement

- Only **Epics** can be placed on the board
- Each EpicPlacement defines:
  - Lane
  - RowIndex
  - OrderInRow
- Vertical position = RowIndex
- Horizontal alignment across Lanes is **visual only**

### 6.1 Parallel Epics in a Row (UI Rules)
- `OrderInRow` defines **left-to-right order**
- Epics in the same Row MUST:
  - Never overlap
  - Use uniform horizontal spacing
- Layout must be deterministic across renders

---

## 7. Drag & Drop Rules

### 7.1 Allowed
- Reorder Epics within a Row
- Move Epics between Rows within the same Lane

### 7.2 Forbidden
- Moving Epics across Lanes
- Creating duplicate EpicPlacements
- Dragging Epics back to the Unplanned Epic list

### 7.3 Drag Preview Contract
- Drag preview MUST represent the **final persisted state**
- Preview uses the **same layout algorithm** as committed state
- During preview:
  - Explicit drop placeholders are shown
  - All Epics across all Lanes shift vertically
  - Connectors update live

---

## 8. Unplanned Epic Source

### 8.1 Unplanned Epic List
- Located **above the board**
- Shows only Epics **not yet on the board**
- Ordered using **TFS ordering**
- Shows ~4–5 Epics, scrollable
- **Source-only list** (one-way drag to board)

### 8.2 Objective Modal
- Modal dialog shows **all Epics for an Objective**
- Clearly indicates which Epics are not yet planned

---

## 9. Connector Graph (Git-Style)

### 9.1 Nature
- Connectors represent **ordering flow**, not dependencies
- Connectors exist **only within a Lane**
- Connectors are derived, never persisted

### 9.2 Derivation Algorithm

For each Lane:
1. Group EpicPlacements by RowIndex
2. Sort Rows ascending
3. For each adjacent Row pair `(N, N+1)`:
   - Let `A = epics in Row N`
   - Let `B = epics in Row N+1`
   - If `|A| = 1` and `|B| > 1`: split
   - If `|A| > 1` and `|B| = 1`: merge
   - If `|A| = |B|`: parallel continuation
   - Draw connectors from all A → all B as required

### 9.3 Visual Constraints (MANDATORY)
- Connectors flow **top-to-bottom only**
- No connector may cross another connector within a Lane
- Split and merge shapes must be visually unambiguous
- Connector style must be consistent across UI and export

---

## 10. Epic Split

### 10.1 Trigger
- Context menu option only

### 10.2 Split Dialog
- Two Epics:
  - **Original Epic**
  - **Extracted Epic**
- User assigns Features to either Epic
- Dialog explicitly shows which Epic remains

### 10.3 Result
- Original Epic:
  - Unchanged placement
- Extracted Epic:
  - Same parent Objective
  - Not placed on board
  - Appears directly below Original Epic in Epic list

---

## 11. Effort Handling on Split

### 11.1 Definitions
- EpicEffort (integer)
- Feature effort may exist or not

### 11.2 Case A – Features have effort
- Calculate ratios based on Feature effort
- Distribute EpicEffort proportionally

### 11.3 Case B – Features have no effort
- Temporarily distribute EpicEffort evenly across Features
- Apply Case A

### 11.4 Rounding
- Round **up** to whole integers
- Preserve total EpicEffort exactly

---

## 12. Validation Indicators

### 12.1 Meaning
- Indicators reflect **cached validation warnings/errors**
- Not based on “work not done”

### 12.2 Data Source
- Validation results are cached
- Not recalculated on every board change

### 12.3 Refresh Triggers
- Pull & Cache work items
- Manual Revalidate action
- Opening the board (with loading dialog)

### 12.4 UI Rules
- Validation indicator is **always visible** on Epic card
- Indicators must not disappear due to scaling, zoom, or export
- Errors visually dominate warnings

### 12.5 Drill-through
- Context menu option:
  - Opens Work Item Explorer
  - Selects the Epic
  - Expands tree so all invalid descendants are visible

---

## 13. Horizontal Lines

### 13.1 Types
- Milestone Lines (e.g. Release)
- Iteration Lines

### 13.2 Scope
- Global across all Lanes

### 13.3 Behavior
- Draggable vertically
- During drag:
  - All Epics shift to preview inclusion/exclusion

### 13.4 Visual Differentiation (MANDATORY)
- Milestone Lines and Iteration Lines MUST be visually distinguishable
- Differentiation MUST include ALL of:
  - Different line style (e.g. solid vs dashed)
  - Different label styling
  - Different color category
- Milestone Lines MUST visually dominate Iteration Lines
- Distinction MUST remain visible in:
  - Board UI
  - Drag preview
  - Export

---

## 14. Export Subfeature

### 14.1 Formats
- PNG
- PDF

### 14.2 Orientation
- Landscape only

### 14.3 Scope Selection
- Objectives
- Milestone range
- Iteration range or next N iterations

### 14.4 Layout
- A4 or A3
- Fit-to-page or multi-page
- Page count preview

### 14.5 Rendering Mode
- Export uses a **non-interactive render mode**
- Drag affordances, tooltips, and hover UI are excluded
- Styling switches to **printer-friendly palette**

### 14.6 Metadata
- Title
- Export date
- Scope description

---

## 15. Explicitly Out of Scope

- Dates
- Scheduling logic
- TFS iteration assignment
- Cross-lane connectors
- Undo/redo
- Collaboration features

---

END OF SPEC
