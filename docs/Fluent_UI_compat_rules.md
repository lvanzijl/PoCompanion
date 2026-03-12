# UI Density Rules — Fluent UI Compact Aligned

## Purpose
This document defines the **authoritative UI density rules** for the application, aligned with **Fluent UI Compact** principles as used in Azure DevOps, Azure Portal, and other Microsoft enterprise tools.

Goal:  
**Maximum information density with sustained readability**, optimized for mouse/keyboard, professional desktop use, and data-heavy workflows.

These rules are **binding**.

---

## Design Philosophy (Fluent Compact)

1. **Efficiency over comfort**  
   UI is optimized for scanning, comparison, and decision-making.

2. **Compact internals, clear structure**  
   Components are tight internally; logical sections are visually separated.

3. **Predictable rhythm**  
   Consistent heights, spacing, and alignment reduce cognitive load.

4. **Density is systemic**  
   Density is not tuned per screen or per developer.

---

## Spacing System (Fluent-based)

### Base Units
Fluent UI Compact is built on a **4px grid**.

| Token | Value | Usage |
|----|----|----|
| `--d1` | 4px | Internal padding, label gaps |
| `--d2` | 8px | Control padding, cell padding |
| `--d3` | 12px | Group spacing |
| `--d4` | 16px | Section separation |

Rules:
- All spacing derives from these units.
- No arbitrary pixel values allowed.

---

## Component Dimensions (Fluent Compact)

### Heights
| Component | Height |
|---------|--------|
| Text inputs / selects | 32px |
| Buttons | 28px |
| Icon buttons | 28px |
| Table rows | 28–32px |
| List / tree items | 28–32px |

- Heights are **fixed**, not content-driven.
- Vertical growth inside rows is prohibited.

---

## Padding Rules

| Area | Padding |
|----|--------|
| Input horizontal | 8px |
| Input vertical | 4px |
| Table cell | 4px 8px |
| List / tree item | 4px 8px |
| Dialog body | 12px |
| Dialog header/footer | 8px 12px |

Padding increases only at **structural boundaries**, never inside rows.

---

## Typography (Fluent Compact)

| Element | Rule |
|------|-----|
| Base font size | 13–14px |
| Line height | ~1.3 |
| Labels | Tight, single-line |
| Section headers | Clear hierarchy, not oversized |

Rules:
- Typography defines density more than padding.
- Do not compensate layout issues by increasing font size.

---

## MudBlazor Configuration Rules

### Mandatory Defaults
All MudBlazor components **must** use:

- `Dense="true"`
- `Margin="Margin.Dense"` (inputs)
- `Size="Size.Small"` (buttons, icon buttons)

These defaults reflect Fluent Compact behavior.

### Prohibited
- Mixing dense and non-dense components in one view
- Using default-sized buttons in compact layouts
- Inline spacing overrides or per-component tuning

---

## Wrapper Component Policy

### Required
Core components must be wrapped:

- `CompactTextField`
- `CompactSelect`
- `CompactButton`
- `CompactIconButton`
- `CompactTable`
- `CompactList`
- `CompactTreeView`

Wrappers:
- Hardcode compact defaults
- Expose opt-out only via explicit parameters
- Prevent accidental density drift

Raw MudBlazor components require justification.

### Button compatibility with UI hierarchy

Button density rules defined in this document must not override the UI hierarchy defined in UI_RULES.md.

Fluent compatibility rules control physical rendering only:
- spacing
- density
- height
- padding
- typography

They must not change button emphasis or semantic role.

Button roles are defined in UI_RULES.md and must remain intact:

Utility buttons  
Action buttons  
Critical buttons

Compact wrappers such as CompactButton must support these roles without forcing all buttons into a single visual style.

Examples of allowed adjustments:
- reduced padding
- smaller height
- compact icon spacing

Examples of forbidden adjustments:
- increasing border strength of utility buttons
- forcing all buttons to use the same variant
- visually promoting buttons above cards or navigation tiles

Compact design must preserve the hierarchy:

Navigation tiles  
Cards and dashboards  
Buttons

If a compact wrapper increases the visual prominence of buttons beyond cards or tiles, the wrapper implementation is incorrect.

---

## Layout & Composition Rules

1. **Internal vs external spacing**
   - Inside components: `--d1` / `--d2`
   - Between groups: `--d3`
   - Between sections: `--d4`

2. **Alignment**
   - Labels align across forms
   - Icons have uniform size and baseline
   - Tables align column headers and rows precisely

3. **Section separation**
   - Use headers, dividers, or grouping
   - Never large padding blocks

---

## Tables, Lists & Tree Views

- Row height strictly follows compact rules
- Expand/collapse affects indentation, not height
- Icons do not add vertical padding
- Multi-line content inside rows is discouraged

Tables are **scan-first**, not content-first.

---

## Dialogs & Overlays

- Compact by default
- Same density as main UI
- Primary action is visually clear, not larger
- No mobile-style padding or touch spacing

---

## CSS Rules

### Token-based Only
Global density CSS:
- Uses spacing tokens exclusively
- Contains no magic numbers
- Applies consistently across all components

Component-local CSS is discouraged and reviewed strictly.

---

## Review & Guardrails

### Pull Request Checklist
- Compact wrappers used
- No ad-hoc spacing
- Heights and spacing match Fluent Compact rules
- Any deviation is explicit and justified

### Density Reference Page
A permanent reference page must exist showing:
- Forms
- Tables
- Lists
- Tree views
- Dialogs

Density changes are verified visually against this page.

---

## Non-goals

These rules do **not**:
- Optimize for touch or mobile
- Increase padding for “friendliness”
- Support multiple density modes

This application is **compact by design**.

---

## Summary

- Fluent UI Compact is the baseline
- 4px grid, fixed heights, tight internals
- Structure provides breathing room, not padding
- Density is enforced, not negotiated

