# Page Filter Contracts

## Purpose

This document defines how each page applies the canonical filter state model.

It maps:
- which filters are supported per page
- which filters are primary vs advanced
- which filters are not applicable
- which time modes are supported

This is a **binding document** for UI and backend behavior.

---

## 1. Filter Reference

### Global core
- Product
- Project

### Global remembered (conditional)
- Time

### Workspace/page
- Team
- Repository
- Validation Category
- Validation Rule
- Pipeline toggles
- Tags
- Search

---

## 2. Contract Structure

Each page must define:

- Supported filters
- Primary filters
- Advanced filters
- Not applicable filters
- Time mode support (if applicable)

---

## 3. Home (Landing)

### Supported
- Product (Primary)
- Project (Primary)

### Not Applicable
- Time
- Team
- Repository
- Validation Category
- Validation Rule
- Pipeline toggles
- Tags
- Search

### Summary
- Product (always)
- Project (always)

### Disabled Context
- Time (remembered, disabled)

---

## 4. Health Workspace

### Supported
- Product (Primary)
- Project (Primary)
- Validation Category (Advanced)
- Validation Rule (Advanced)

### Not Applicable
- Time
- Team
- Repository
- Pipeline toggles
- Tags
- Search

### Summary
- Product
- Project

### Disabled Context
- Time (remembered, disabled)

---

## 5. Delivery Workspace (Sprint / Execution)

### Supported
- Product (Primary)
- Project (Primary)
- Time (Primary + Advanced)
- Team (Primary or Advanced depending on page)
- Tags (Advanced)

### Time Modes
- Single Sprint (Primary)
- Sprint Range (Advanced)
- Date Range (Advanced)

### Summary
- Product
- Project
- Time (effective only)

### Disabled Context
- none (Time is applicable)

---

## 6. Portfolio / Trends

### Supported
- Product (Primary)
- Project (Primary)
- Time (Primary + Advanced)

### Optional
- Team (Advanced, if implemented later)

### Time Modes
- Single Sprint
- Sprint Range
- Date Range

### Summary
- Product
- Project
- Time

---

## 7. Planning Workspace

### Supported
- Product (Primary)
- Project (Primary)

### Optional
- Team (if planning becomes team-based later)

### Not Applicable
- Time
- Validation
- Pipeline
- Tags

### Summary
- Product
- Project

### Disabled Context
- Time (remembered, disabled)

---

## 8. Pipeline Insights

### Supported
- Product (Primary)
- Project (Primary)
- Time (Primary + Advanced)
- Repository (Primary)
- Pipeline toggles (Advanced)

### Time Modes
- Single Sprint
- Sprint Range
- Date Range

### Summary
- Product
- Project
- Time

---

## 9. Pull Request Insights

### Supported
- Product (Primary)
- Project (Primary)
- Time (Primary + Advanced)
- Repository (Primary)
- Team (Advanced)

### Time Modes
- Single Sprint
- Sprint Range
- Date Range

### Summary
- Product
- Project
- Time

---

## 10. Backlog / Refinement

### Supported
- Product (Primary)
- Project (Primary)
- Team (Primary or Advanced depending on usage)
- Tags (Advanced)
- Validation Category (Advanced)

### Not Applicable
- Time (optional future extension, but currently not used)

### Summary
- Product
- Project

### Disabled Context
- Time (remembered, disabled)

---

## 11. Rules Across All Pages

### 11.1 Always present
- Product
- Project

Even when:
- set to All
- not explicitly used in filtering

### 11.2 Time behavior
- Only visible in summary when supported
- Always remembered globally
- Appears in disabled context when not applicable

### 11.3 Team behavior
- Only shown on pages where meaningful
- Never part of global summary
- Default = None

### 11.4 Invalid filters
- Remain visible in summary
- Marked invalid
- Ignored in query building

### 11.5 Query rule
Each page must:
- only use supported filters
- only use EffectiveValue
- ignore invalid or not applicable filters

---

## 12. Extension Rules

When introducing a new page:

You must define:

- Supported filters
- Primary vs Advanced
- Not applicable filters
- Time support and modes
- Summary behavior

No page may:
- invent new filter semantics
- override canonical defaults
- reinterpret filter meaning

---

## 13. Binding Constraints

1. Product and Project are always present.
2. Time is never forced on pages where it does not apply.
3. Team is never global.
4. Pages define applicability, not filters themselves.
5. Canonical filter model must be respected everywhere.
