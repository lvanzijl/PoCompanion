# Navigation Decision Backlog

**Version:** 1.0  
**Status:** Active  
**Last Updated:** 2026-01-27

---

## Document Purpose

This document tracks explicitly deferred decisions identified during the Beta Navigation implementation. These items represent design or functional questions that require product or stakeholder input before implementation can proceed.

**Important:** These decisions should NOT be implemented without explicit resolution. Guessing is forbidden per project rules.

---

## Deferred Decisions

### 1. Beta Navigation Promotion Path

| Field | Value |
|-------|-------|
| **Title** | When and how to promote Beta to production navigation |
| **What is undecided** | The criteria and process for transitioning from the Beta navigation to replacing the existing navigation. |
| **Options** | A) Feature flag based rollout<br>B) Date-based cutover<br>C) User preference setting<br>D) Complete replacement once validated |
| **Impact** | All Beta workspaces and routes; Landing page |
| **Urgency** | 3 |

---

### 2. Health Signal Thresholds

| Field | Value |
|-------|-------|
| **Title** | Thresholds for health signal severity levels |
| **What is undecided** | At what counts or percentages should health signals show warning vs. error states. |
| **Options** | A) Fixed thresholds (e.g., 10 items = warning, 50 = error)<br>B) Percentage-based (e.g., 10% of backlog)<br>C) User-configurable per profile<br>D) Historical baseline comparison |
| **Impact** | Health workspace, Beta Home overview |
| **Urgency** | 4 |

---

### 3. Time Horizon Default for Trends

| Field | Value |
|-------|-------|
| **Title** | Default time range for Trends workspace |
| **What is undecided** | Whether to always show full 6 months or allow different defaults. |
| **Options** | A) Always 6 months (as specified)<br>B) Default to 3 months with option to expand<br>C) Smart default based on data availability |
| **Impact** | Trends workspace |
| **Urgency** | 2 |

---

### 4. Bug Severity Edit Permissions

| Field | Value |
|-------|-------|
| **Title** | Who can edit bug severity through Beta navigation |
| **What is undecided** | Whether all users can edit severity or if this should follow TFS permissions. |
| **Options** | A) Follow TFS field-level permissions<br>B) Allow PO to edit any bug in their products<br>C) Read-only display with edit via TFS link |
| **Impact** | Bug Detail page, Bug Overview batch operations |
| **Urgency** | 3 |

---

### 5. Cross-Workspace State Preservation

| Field | Value |
|-------|-------|
| **Title** | How to handle unsaved changes when navigating between workspaces |
| **What is undecided** | Whether to warn, auto-save, or discard unsaved edits when user navigates away. |
| **Options** | A) Always warn with confirmation dialog<br>B) Auto-save as draft<br>C) Discard silently (match TFS behavior)<br>D) Block navigation until saved |
| **Impact** | All editable end-stations |
| **Urgency** | 4 |

---

### 6. Batch Edit Size Limits

| Field | Value |
|-------|-------|
| **Title** | Maximum number of items for batch operations |
| **What is undecided** | Whether to limit batch edit selection and what the limit should be. |
| **Options** | A) No limit (respect TFS rate limits naturally)<br>B) Fixed limit (e.g., 100 items)<br>C) Warn but allow (soft limit)<br>D) Queue-based processing for large batches |
| **Impact** | Work Item Explorer, Bug Overview batch operations |
| **Urgency** | 3 |

---

### 7. Epic Dependency Visualization Scope

| Field | Value |
|-------|-------|
| **Title** | Scope of dependencies shown in Dependency Overview |
| **What is undecided** | Whether to show all dependencies, only blocking dependencies, or allow filtering. |
| **Options** | A) All dependency types equally<br>B) Predecessor/successor only<br>C) Cross-team dependencies only<br>D) User-selectable filter |
| **Impact** | Dependency Overview, Planning workspace |
| **Urgency** | 2 |

---

### 8. Planning Board Epic Ordering Persistence

| Field | Value |
|-------|-------|
| **Title** | How to persist epic ordering changes |
| **What is undecided** | Whether ordering changes are saved to TFS backlog order, local storage, or a separate priority field. |
| **Options** | A) Update TFS backlog priority field<br>B) Update TFS stack rank<br>C) Local storage only (view preference)<br>D) Custom priority field |
| **Impact** | Planning workspace, Plan Board |
| **Urgency** | 5 |

---

### 9. Signal Click vs. Double-Click Behavior

| Field | Value |
|-------|-------|
| **Title** | Interaction pattern for signal cards |
| **What is undecided** | Whether single click should navigate or show preview, with double-click for navigation. |
| **Options** | A) Single click = navigate immediately<br>B) Single click = preview/tooltip, double-click = navigate<br>C) Single click = expand inline, button = navigate |
| **Impact** | Health workspace, Trends workspace, Planning workspace |
| **Urgency** | 2 |

---

### 10. Empty State Content

| Field | Value |
|-------|-------|
| **Title** | Content to show when no issues exist |
| **What is undecided** | What to display when a signal category has zero items (e.g., no missing effort issues). |
| **Options** | A) Show card with "0 items" badge<br>B) Hide card entirely<br>C) Show collapsed/grayed card<br>D) Show celebratory "all clear" message |
| **Impact** | All signal cards in Beta workspaces |
| **Urgency** | 2 |

---

### 11. Context Scope Reduction

| Field | Value |
|-------|-------|
| **Title** | Ability to widen scope after narrowing |
| **What is undecided** | Once a user has narrowed to a specific product/team, can they widen scope without returning to Beta Home. |
| **Options** | A) Must return to Beta Home to change scope<br>B) Provide "view all" link on each page<br>C) Breadcrumb-based scope navigation |
| **Impact** | All Beta workspaces |
| **Urgency** | 3 |

---

### 12. Trend Chart Interaction

| Field | Value |
|-------|-------|
| **Title** | Interaction behavior for trend charts |
| **What is undecided** | Whether clicking on a specific bar in a trend chart should filter the detail view to that period. |
| **Options** | A) Bars are not interactive<br>B) Click bar = filter to that period<br>C) Click bar = show tooltip only<br>D) Hover = highlight, click = filter |
| **Impact** | Trends workspace, all trend charts |
| **Urgency** | 2 |

---

### 13. Beta Feature Completeness Requirement

| Field | Value |
|-------|-------|
| **Title** | Minimum feature completeness for Beta visibility |
| **What is undecided** | Whether Beta should be visible to all users now or gated until more features are complete. |
| **Options** | A) Show to all users immediately (current implementation)<br>B) Feature flag for early adopters only<br>C) Admin-only visibility initially<br>D) Wait until follow-up actions are complete |
| **Impact** | Landing page Beta entry point |
| **Urgency** | 5 |

---

### 14. Mobile/Responsive Behavior

| Field | Value |
|-------|-------|
| **Title** | Beta navigation behavior on small screens |
| **What is undecided** | How the workspace cards and signal grids should adapt to mobile/tablet viewports. |
| **Options** | A) Stack vertically, maintain all cards<br>B) Collapse to list view<br>C) Show abbreviated version with "see all" link<br>D) Not supported (desktop only) |
| **Impact** | All Beta pages |
| **Urgency** | 2 |

---

## Summary by Urgency

| Urgency | Count |
|---------|-------|
| 1 | 0 |
| 2 | 6 |
| 3 | 4 |
| 4 | 2 |
| 5 | 2 |
| **Total** | **14** |

---

## Urgency Scale

- **1** — Can be decided later, no current blocker
- **2** — Should decide before general availability  
- **3** — Should decide before significant user testing
- **4** — Decision needed soon to guide implementation
- **5** — Critical decision blocking further progress

---

## Resolution Process

1. Product Owner reviews decision items
2. Options are discussed with stakeholders as needed
3. Decision is documented with rationale
4. This document is updated to mark item as resolved
5. Implementation proceeds based on decision
