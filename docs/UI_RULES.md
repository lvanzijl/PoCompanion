# \# UI Rules – PO Tool

# 

# This document defines the binding UI rules for the PO Tool.  

# All screens, components, and interactions must comply with these rules.  

# UX intent is defined in `docs/ux-principles.md`; this document translates that intent into concrete UI constraints.

# 

# ---

# 

# \## 1. Global Layout Rules

# 

# \- The application uses a \*\*two-column base layout\*\*:

# &nbsp; - \*\*Left\*\*: permanent navigation menu for main views.

# &nbsp; - \*\*Right\*\*: active view content.

# \- A \*\*top menu bar\*\* is always visible and reserved for global actions (e.g. Pull \& Cache).

# \- No view may override or hide the left menu or top bar.

# 

# ---

# 

# \## 2. Navigation Rules

# 

# \- Navigation is \*\*view-based\*\*, not page-based.

# \- Switching views never resets application state unnecessarily.

# \- No modal navigation flows; navigation must always remain visible and predictable.

# \- Breadcrumbs are optional but must be read-only indicators, never primary navigation.

# 

# ---

# 

# \## 3. Overview → Detail Pattern (Mandatory)

# 

# \- Every view follows the same interaction model:

# &nbsp; 1. \*\*Overview\*\* (tree, table, chart)

# &nbsp; 2. \*\*Detail panel\*\* (right side or overlay)

# \- Details never open in a full-page replacement.

# \- Closing a detail view restores the overview \*\*with all filters, selections, and expansion state intact\*\*.

# 

# ---

# 

# \## 4. Component Usage Rules

# 

# \### TreeViews

# \- Used only for hierarchical data.

# \- Indentation and icons must be subtle.

# \- Expand/collapse affordances must be clear.

# \- Selection highlights only the active item.

# \- Filtering hides non-matching branches entirely.

# \- Matching text inside nodes must be highlighted.

# 

# \### DataTables

# \- Used for large or flat datasets.

# \- Must support:

# &nbsp; - sorting

# &nbsp; - filtering

# &nbsp; - column visibility

# \- Inline actions are allowed but must appear only on hover or selection.

# \- No dense grids or spreadsheet-like visuals.

# 

# \### Charts

# \- Charts are interactive only if interaction adds insight.

# \- Clicking a chart element must lead to a deterministic result.

# \- Charts must never be decorative.

# \- Color usage must be limited and consistent across the tool.

# 

# ---

# 

# \## 5. Filtering \& Search Rules

# 

# \- Filters apply immediately; no “Apply” buttons.

# \- Filtering must never destroy navigation context.

# \- Highlight matching text inside visible items.

# \- Filtering logic must be predictable and explainable:

# &nbsp; - No fuzzy or AI-driven interpretation unless explicitly stated.

# 

# ---

# 

# \## 6. Global Actions

# 

# \- Global actions (e.g. Pull \& Cache Work Items):

# &nbsp; - Must live in the \*\*top menu bar\*\*.

# &nbsp; - Must be directly accessible.

# &nbsp; - Must use clear, recognizable icons.

# \- Global actions must never be hidden behind view-specific UI.

# 

# ---

# 

# \## 7. Configuration UI Rules

# 

# \- Configuration screens are isolated from operational views.

# \- Configuration changes never partially apply:

# &nbsp; - Either fully applied or not applied at all.

# \- Sensitive fields (PATs, secrets):

# &nbsp; - Are masked.

# &nbsp; - Are never re-displayed after entry.

# &nbsp; - Are stored encrypted.

# 

# ---

# 

# \## 8. Visual Style Constraints

# 

# \- Minimal borders.

# \- Consistent spacing.

# \- No decorative shadows or gradients.

# \- No “raw” technical controls.

# \- Typography hierarchy must be consistent across all views.

# 

# ---

# 

# \## 9. State \& Feedback Rules

# 

# \- Loading states must be explicit but unobtrusive.

# \- Errors must be actionable and factual.

# \- No blocking spinners without context.

# \- Long-running operations must show progress or status.

# 

# ---

# 

# \## 10. Consistency Rule (Non-Negotiable)

# 

# If a pattern exists:

# \- It must be reused.

# \- It must not be reinterpreted per view.

# 

# Any deviation from these rules requires explicit justification and documentation.

# 

# ---

# 

# This document is \*\*binding\*\*.  

# All UI implementation must conform to this file, `docs/ux-principles.md`, and `docs/ARCHITECTURE\_RULES.md`.



