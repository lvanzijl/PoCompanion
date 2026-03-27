# Canonical Filter State Model

## Purpose

This document defines the canonical filter state model for the application.

Its goal is to ensure that:
- filter meaning is consistent across pages
- selected values can persist across navigation
- invalid or not-applicable filters are still represented correctly
- only effective filters are used for backend queries

This document defines state only.  
It does not define final UI layout or backend endpoint redesign.

---

## 1. Filter Classification

### 1.1 Global core filters
These define the main business scope of the application.

- Product
- Project

### 1.2 Global remembered, conditionally applicable filters
These are remembered globally, but only used on pages that support them.

- Time

### 1.3 Workspace/page filters
These are local to a workspace or page and are not part of the core global context.

- Team
- Repository
- Validation Category
- Validation Rule
- Pipeline toggles
- Tags
- Search

---

## 2. Canonical State Shape

Each filter must be represented with the same logical model.

## 2.1 Generic filter state

Each filter has:

- `SelectedValue`
- `EffectiveValue`
- `Status`
- `Visibility`

### SelectedValue
The value chosen by the user, even if it is invalid or not applicable on the current page.

### EffectiveValue
The value actually used for filtering/query execution on the current page.

### Status
One of:

- `Valid`
- `Invalid`
- `NotApplicable`

### Visibility
One of:

- `Summary`
- `Primary`
- `Advanced`
- `Hidden`
- `DisabledContext`

---

## 3. State Semantics

## 3.1 Valid
The selected value is supported on the current page and forms a valid combination with other active filters.

Rule:
- `EffectiveValue = SelectedValue`

## 3.2 Invalid
The selected value is remembered, shown to the user, but is not currently valid because of filter interaction.

Examples:
- Product and Project combination is not valid
- Selected time mode conflicts with current page rules

Rule:
- `EffectiveValue = DefaultValue`
- `SelectedValue` remains visible
- UI must indicate invalid state clearly

## 3.3 NotApplicable
The filter is remembered, but the current page does not support it.

Rule:
- `EffectiveValue = None`
- Filter is not shown in main summary
- Filter appears only in a separate disabled context section after expansion

---

## 4. Default Values

The canonical defaults are:

- Product = `All Products`
- Project = `All Projects`
- Time = `Current Sprint`
- Team = `None`

### Default semantics

#### All Products
No product narrowing is active.

#### All Projects
No project narrowing is active.

#### Current Sprint
Default time context for pages that support time.

#### None
No team filter is active by default.

---

## 5. Canonical Filters

## 5.1 Product
Classification:
- Global core

Default:
- `All Products`

Rules:
- Always shown in summary
- Always available in primary filtering
- May constrain Project options
- May become invalid based on Project selection

## 5.2 Project
Classification:
- Global core

Default:
- `All Projects`

Rules:
- Always shown in summary
- Always available in primary filtering
- May constrain Product options
- May become invalid based on Product selection

## 5.3 Time
Classification:
- Global remembered, conditionally applicable

Default:
- `Current Sprint`

Rules:
- Remembered globally
- Only shown in summary on pages where time is applicable
- On pages where time is not applicable:
  - not shown in main summary
  - shown in expanded disabled-context area
- Primary mode is Sprint
- Advanced modes are:
  - Sprint Range
  - Date Range

---

## 6. Time Model

Time is one canonical filter with mutually exclusive modes.

## 6.1 Allowed modes

- `SingleSprint`
- `SprintRange`
- `DateRange`

Only one mode may be active at a time.

## 6.2 Mode behavior

When one mode is active:
- the other modes are ignored
- only the active mode contributes to `EffectiveValue`

## 6.3 Primary vs advanced

### Primary
- Single Sprint only

### Advanced
- Sprint Range
- Date Range

Pages may support:
- only Single Sprint
- Single Sprint + Sprint Range
- all three modes

Unsupported modes must not be shown as available.

---

## 7. Constraint Rules

## 7.1 Product and Project interaction

Product and Project are independent starting points.

A user may select:
- Product first
- Project first

Selecting one must constrain the selectable options of the other.

### Constraint behavior
- Non-applicable options remain visible
- Non-applicable options are disabled or greyed out
- `All Products` and `All Projects` always remain selectable

## 7.2 Invalid combination behavior

If the user creates or keeps an invalid Product/Project combination:

- the invalid selected value remains stored
- the invalid selected value remains visible in summary
- the invalid selected value is marked visually invalid
- filtering falls back to default for that axis

Example:
- Selected Product = Product A
- Selected Project = Project X
- Project X is not valid for Product A

Then:
- invalid value remains visible
- the invalid axis uses default as `EffectiveValue`

---

## 8. Summary Rules

## 8.1 Always shown in summary
- Product
- Project

These must always be visible, even when set to default.

Examples:
- `Product: All Products`
- `Project: All Projects`

## 8.2 Conditionally shown in summary
- Time

Show Time in summary only if the current page supports time.

If shown:
- show only the effective time window
- do not mention whether it comes from primary or advanced mode

## 8.3 Invalid summary behavior
If a filter is invalid:
- show the selected value
- mark it visually invalid
- do not hide it
- filtering still uses default effective value

## 8.4 Hidden from main summary
Filters that are not applicable on the current page must not appear in the main summary.

They belong in the disabled-context section only.

---

## 9. Expanded View Rules

## 9.1 Primary section
The primary section contains the most common controls.

Default primary filters:
- Product
- Project
- Sprint

## 9.2 Advanced section
The advanced section contains less common or more complex controls.

Default advanced filters:
- Sprint Range
- Date Range

## 9.3 Disabled context section
The disabled context section contains remembered filters that are not applicable on the current page.

Rules:
- collapsed by default
- shown only after expansion
- controls are disabled/read-only
- values remain visible for context

Example:
- Time shown on Health page as remembered but not applicable

---

## 10. Page Contract

Each page must declare a filter contract.

A page contract defines:

- which filters are supported
- which filters are primary
- which filters are advanced
- which filters are not applicable
- which time modes are supported

## 10.1 Required page declarations

Each page must declare for each canonical filter:

- `Supported = true/false`
- `Primary = true/false`
- `Advanced = true/false`
- `DefaultValue`
- `SupportedModes` for Time if applicable

Example:

- Health Overview
  - Product = Supported, Primary
  - Project = Supported, Primary
  - Time = NotApplicable

- Portfolio Trend
  - Product = Supported, Primary
  - Project = Supported, Primary
  - Time = Supported
  - Time modes = SingleSprint, SprintRange, DateRange

---

## 11. Query-Building Rule

Backend queries must only use `EffectiveValue`.

Never send:
- invalid selected values
- not-applicable values
- unsupported filter modes

### Query rule
For every request:
- build query from canonical page contract
- include only supported filters
- include only effective values

This guarantees:
- invalid remembered state never corrupts query behavior
- pages remain stable even when filters persist globally

---

## 12. Persistence Rules

## 12.1 Product
- persists globally

## 12.2 Project
- persists globally

## 12.3 Time
- persists globally as remembered state
- only becomes effective on pages that support time

## 12.4 Workspace/page filters
- persist only locally where required
- do not participate in global core summary

---

## 13. Rendering Intent

This state model exists to support:

- collapsed summary view
- primary filter layer
- advanced filter layer
- disabled remembered-context layer

This document does not prescribe exact component visuals, but the state model must support all four.

---

## 14. Non-Goals

This specification does not decide:

- final visual design
- exact advanced time interaction flow
- backend endpoint redesign
- migration strategy

Those are separate documents.

---

## 15. Binding Rules

1. Product and Project are the only global core filters.
2. Time is globally remembered but only effective where applicable.
3. Team is not a global filter.
4. Invalid selections remain visible but never affect query execution.
5. Not-applicable remembered filters are not shown in the main summary.
6. Queries use effective values only.
7. Every page must declare its filter contract.
