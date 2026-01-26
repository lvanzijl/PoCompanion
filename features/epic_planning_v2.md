# GitHub Copilot Prompt — Implement New Planning Board (Table-Based, Products as Columns)

You are working in the PoTool / PoCompanion codebase.

Implement a **new Planning Board from scratch** using a table/grid approach.  
Assume there is **no valid existing Planning Board**.  
Any existing planning-related code that does not fit this specification must be removed.

Backward compatibility is NOT required.

---

## Phase 0 — Inventory & Cleanup Plan (MANDATORY)
Before implementing new functionality:

1) Locate all code related to any existing planning or roadmap functionality:
   - UI components, pages, routes
   - layout logic (SVG, canvas, absolute positioning, etc.)
   - drag & drop logic
   - models, DTOs, persistence
   - navigation/menu items
   - tests

2) Classify everything as:
   - REMOVE (obsolete)
   - REUSE (generic utilities only)
   - REPLACE (conceptually similar but reimplemented)

3) Write a short decommission checklist (comments or markdown):
   - what will be deleted
   - what will be replaced
   - what persisted data (if any) will be dropped

Do not start implementing the new board until this step is complete.

---

## Phase 1 — Remove Obsolete Code
- Remove all obsolete planning UI and logic.
- Remove dead routes, menu entries, and navigation links.
- Remove unused models/services.
- Drop any existing planning persistence that does not match the new model.
- Ensure the application builds and runs cleanly after removal.

---

## Phase 2 — Planning Board Concepts & Persistence

### Core Concepts (canonical naming)
Use these concepts consistently:

- **ProductColumn**
  - Represents a Product.
  - Columns on the board.

- **BoardRow**
  - Ordered row representing an abstract time slot.
  - Has no calendar, sprint, or date meaning.

- **MarkerRow**
  - Special BoardRow type.
  - Two kinds:
    - Iteration Line
    - Release Line

- **EpicPlacement**
  - An Epic placed on the board.
  - Defined by:
    - EpicId
    - ProductId
    - RowIndex (or RowId)
    - OrderInCell (deterministic order within a cell)

- **Unplanned Epic**
  - Epic not placed anywhere on the board.

---

### Persistence Rules
Persist the following (per board configuration):

- Ordered list of BoardRows (including MarkerRows)
- EpicPlacements (EpicId, ProductId, Row, OrderInCell)
- Product column visibility (shown/hidden)
- Last selected board scope:
  - All products
  - Single product + selected product

Do NOT persist:
- transient drag state
- hover state
- temporary selection (except during the session)

Rows do NOT need to be contiguous.
Empty rows ARE allowed.

---

## Phase 3 — Board UI & Behavior

### 3.1 Board Scope Selector
Add a scope selector:

- **All products**
- **Single product** (dropdown)

Behavior:
- All products → multiple product columns (only visible ones)
- Single product → exactly one product column

---

### 3.2 Products as Columns + Column Picker
- Each column represents a Product.
- Add a column picker UI to toggle product visibility.
- Always show an indicator if ≥1 product is hidden (e.g. badge: “1 hidden”).

Blocking rule:
- If a product column contains any **selected epics**, hiding that product is blocked.
- Show clear feedback explaining why the action is blocked.

---

### 3.3 Rows & Default Layout
Rows are abstract ordered slots.

Default board layout when no state exists:
1. 3 empty rows
2. 1 Iteration Line row
3. 3 empty rows
4. 1 Release Line row
5. 3 empty rows

Row insertion:
- On hover at the LEFT side of a row:
  - “+” above → insert row above
  - “+” below → insert row below
- Rows are NEVER created by dropping epics.

Row deletion:
- Right-click context menu on row indicator (left side).
- A row may be removed ONLY if it is empty (no epics in any product column).
- If not empty, block deletion and show feedback.

---

### 3.4 Marker Rows (Iteration & Release)
- Marker rows span the full width of all visible product columns.
- They are visually distinct from normal rows.
- Release Line must visually dominate Iteration Line.
- Marker rows are draggable vertically to reorder them.

Marker rows have no automatic behavior; they are purely manual markers.

---

### 3.5 Unplanned Epics Panel
This is the only source list.

- All-products scope:
  - Show unplanned epics grouped per product.
- Single-product scope:
  - Show only epics of the selected product.

Drag rules:
- Unplanned → Board: allowed
- Board → Unplanned: forbidden (no drop target)

---

### 3.6 What Can Be Planned
Only **Epics** can appear on the board.

Each Epic belongs to exactly one Product.
That Product determines the column.

---

### 3.7 Drag & Drop Rules
Allowed:
- Move epic to a different row within its product column.
- Reorder epics within the same cell.

Forbidden:
- Moving an epic to another product column.
- Placing an epic more than once.
- Dragging an epic back to Unplanned.

Drop behavior:
- Drop only into existing cells.
- If user attempts cross-product drop, block and show feedback.

Multiple epics in one cell:
- Render stacked/centered.
- No overlap.
- Order determined by OrderInCell and persisted.

---

### 3.8 Selection & Keyboard Delete
- Epics on the board are selectable with visible selection state.
- Support multi-select (Ctrl / Shift).
- Pressing `Delete`:
  - Removes selected epics from the board.
  - Epics return to Unplanned.
  - ONLY works when the board itself has focus (not while typing in inputs).

---

### 3.9 Scrolling
- Board supports horizontal and vertical scrolling.

---

### 3.10 Screen Space Cleanup
The Planning Board UI must NOT include:
- “related dashboards”
- “validation and conflicts”
- “actions”

Remove these entirely and do not replace them.

---

## Phase 4 — Explicitly Out of Scope
Do NOT implement:

- Any SVG/canvas-based layout
- Connectors between epics
- Export (PDF/PNG)
- Validation indicators or drill-through
- Date, sprint, or week semantics
- Automatic planning or prediction logic

---

## Phase 5 — Verification
Manually verify:

- No obsolete planning code remains
- Default rows and marker rows appear correctly
- Row insert/delete rules enforced
- Marker rows draggable
- Unplanned epics grouped per product in all-products scope
- Cross-product drag blocked
- Multi-select + Delete works only when board focused
- Column picker works and hide blocking behaves correctly
- Removed link sections are gone

---

## Definition of Done
- Planning Board implemented exactly as described above
- No references to deprecated planning logic
- Clean build, no dead code
- Behavior is fully explainable using this prompt alone
