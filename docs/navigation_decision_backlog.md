# Navigation Decision Backlog

**Version:** 1.1  
**Status:** Active  
**Last Updated:** 2026-01-27

---

## Document Purpose

This document tracks explicitly deferred decisions identified during the Beta Navigation implementation. These items represent design or functional questions that require product or stakeholder input before implementation can proceed.

**Important:** These decisions should NOT be implemented without explicit resolution. Guessing is forbidden per project rules.

---

## Resolved Decisions

### 2. Health Signal Thresholds ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Thresholds for health signal severity levels |
| **Resolution** | **Option A** - Fixed thresholds: 0 = healthy (green), 1-9 = low (info/blue), 10-49 = warning (yellow), 50+ = error (red) |
| **Implemented** | 2026-01-27 in BetaHome.razor, BetaHealthWorkspace.razor |

---

### 3. Time Horizon Default for Trends ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Default time range for Trends workspace |
| **Resolution** | **Option A** - Always 6 months as specified |
| **Implemented** | 2026-01-27 in BetaTrendsWorkspace.razor |

---

### 8. Planning Board Epic Ordering Persistence ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | How to persist epic ordering changes |
| **Resolution** | **Option C** - Local storage only (view preference) |
| **Implemented** | 2026-01-27 in BetaEpicOrderingService.cs |

---

### 9. Signal Click vs. Double-Click Behavior ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Interaction pattern for signal cards |
| **Resolution** | **Option A** - Single click = navigate immediately |
| **Implemented** | 2026-01-27 (already implemented, verified) |

---

### 10. Empty State Content ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Content to show when no issues exist |
| **Resolution** | **Option A** - Show card with "0 items" badge |
| **Implemented** | 2026-01-27 (already implemented, verified) |

---

### 12. Trend Chart Interaction ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Interaction behavior for trend charts |
| **Resolution** | **Option D** - Hover = highlight, click = filter to that period |
| **Implemented** | 2026-01-27 in BetaTrendChart.razor component |

---

### 13. Beta Feature Completeness Requirement ✅ RESOLVED

| Field | Value |
|-------|-------|
| **Title** | Minimum feature completeness for Beta visibility |
| **Resolution** | **Option A** - Show to all users immediately |
| **Implemented** | 2026-01-27 (already implemented, verified) |

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

### 11. Context Scope Reduction

| Field | Value |
|-------|-------|
| **Title** | Ability to widen scope after narrowing |
| **What is undecided** | Once a user has narrowed to a specific product/team, can they widen scope without returning to Beta Home. |
| **Options** | A) Must return to Beta Home to change scope<br>B) Provide "view all" link on each page<br>C) Breadcrumb-based scope navigation |
| **Impact** | All Beta workspaces |
| **Urgency** | 3 |

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

## Summary

| Status | Count |
|--------|-------|
| Resolved | 7 |
| Remaining | 7 |
| **Total** | **14** |

### Remaining by Urgency

| Urgency | Count |
|---------|-------|
| 2 | 2 |
| 3 | 4 |
| 4 | 1 |
| **Total Remaining** | **7** |

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
