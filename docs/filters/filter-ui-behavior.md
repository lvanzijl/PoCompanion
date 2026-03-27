# Filter UI Behavior

## Purpose

This document defines the UI behavior of the filtering system.

It translates the canonical filter state model into:
- visual structure
- interaction patterns
- user feedback rules

This is a **binding UI behavior specification**.  
It does not define styling details (colors, spacing), but it defines behavior and structure.

---

## 1. UI Layers

The filter system consists of four layers:

1. Collapsed Summary (always visible)
2. Primary Filters (one level deep)
3. Advanced Filters
4. Disabled Context (not applicable filters)

---

## 2. Collapsed Summary

## 2.1 Purpose

Provide a compact, always-visible overview of the active filtering context.

## 2.2 Contents

Always shown:
- Product
- Project

Conditionally shown:
- Time (only if applicable on page)

Never shown:
- Team
- Repository
- Validation filters
- Any not-applicable filters

## 2.3 Display Rules

Each filter is shown as:

- Label: Value

Examples:
- Product: All Products
- Project: Payments
- Time: Sprint 12

## 2.4 Invalid State

If a filter is invalid:

- The selected value is still shown
- The value is visually marked invalid
- The system does not hide or auto-correct it

Example:
- Project: Payments (invalid)

## 2.5 Effective Value Visibility

The summary shows the **selected value**, not the effective fallback.

The user must see what they selected, even if it is not used.

---

## 3. Expanding Filters

Clicking the summary opens the filter panel.

The panel contains:

- Primary section
- Advanced section
- Disabled context section (collapsed by default)

---

## 4. Primary Filters

## 4.1 Purpose

Expose the most commonly used filters with minimal friction.

## 4.2 Default Primary Filters

- Product
- Project
- Sprint (if time is supported)

## 4.3 Behavior

- Always visible when expanded
- Immediate interaction (no extra navigation)
- Changes apply instantly or on close (implementation choice)

---

## 5. Advanced Filters

## 5.1 Purpose

Expose less common or more complex filtering options.

## 5.2 Entry

Accessed via:

- "Advanced filters"
- or "Advanced time..."

## 5.3 Time UX

## Mode Selection

User must explicitly choose one mode:

- Single Sprint
- Sprint Range
- Date Range

Only one mode is active at a time.

## Interaction Model

- Mode selector at top (radio or segmented control)
- Selecting a mode reveals its controls
- Other modes are visible but inactive

## Examples

Single Sprint:
- Sprint dropdown

Sprint Range:
- From Sprint
- To Sprint

Date Range:
- Start date
- End date

## 5.4 Exit Behavior

- Closing advanced view keeps selected mode active
- Summary reflects effective time window only

---

## 6. Disabled Context Section

## 6.1 Purpose

Show remembered filters that are not applicable on the current page.

## 6.2 Behavior

- Collapsed by default
- Expandable section
- All controls are disabled
- Values are visible but not editable

## Example

On Health page:

Disabled Context:
- Time: Sprint 12 (disabled)

## 6.3 Rules

- Not shown in main summary
- Must not affect filtering
- Exists only for user awareness

---

## 7. Invalid State Behavior

## 7.1 Definition

A filter is invalid when:

- it conflicts with another filter
- or selection is not allowed in current context

## 7.2 Visual Rules

Invalid filters must:

- remain visible
- be clearly marked (color/icon/text)
- not block user interaction

## 7.3 Interaction

- user can keep invalid value
- user can correct it manually
- system does not auto-reset

## 7.4 Filtering Rule

Invalid values are ignored in backend queries.

---

## 8. Constraint Behavior (Product / Project)

## 8.1 Selection Order

User may select:
- Product first
- Project first

## 8.2 Constraint

Selecting one:

- constrains available options in the other
- disables invalid options (greyed out)

## 8.3 Invalid Combination

If user forces invalid combination:

- both values remain visible
- invalid one is marked
- filtering falls back to default for that axis

---

## 9. Time Visibility Rules

## 9.1 When Applicable

- visible in summary
- visible in primary (sprint)
- advanced available

## 9.2 When Not Applicable

- not visible in summary
- shown only in disabled context section

---

## 10. Team Behavior

## 10.1 Visibility

- only on pages where relevant
- never in global summary

## 10.2 Default

- None

## 10.3 Interaction

- page decides:
  - required selection
  - optional selection
  - All Teams option

---

## 11. Backend Feedback (Optional UX)

If needed, UI may show:

- "filter not supported on this page"
- "filter currently unavailable"

But:

- unsupported filters should preferably be hidden
- only remembered filters appear in disabled context

---

## 12. Scaling Rules

## 12.1 Adding Filters

New filters must:

- be classified (global / conditional / page)
- be placed in primary or advanced
- respect canonical state model

## 12.2 UI Consistency

No page may:

- introduce unique filter behavior
- redefine meaning of existing filters
- bypass summary structure

---

## 13. Binding Rules

1. Summary always shows Product and Project
2. Summary shows Time only when applicable
3. Invalid filters remain visible and marked
4. Advanced time uses mutually exclusive modes
5. Disabled context shows remembered but not applicable filters
6. Backend queries use effective values only
7. UI must not auto-correct user selections
