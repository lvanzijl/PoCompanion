# Navigation Follow-up Actions

**Version:** 1.0  
**Status:** Active  
**Last Updated:** 2026-01-27

---

## Document Purpose

This document tracks missing or incomplete capabilities identified during the Beta Navigation implementation. Each item represents functionality that needs to be built or completed to fully realize the new workspace-based navigation model.

---

## Follow-up Actions

### 1. Work Item Explorer — Root Item Parameter Support ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Work Item Explorer — Support root item via parameters |
| **Description** | The Work Item Explorer must accept a work item ID as root and show all descendants. Currently, the `rootWorkItemId` parameter is parsed but not used to filter the work item tree. |
| **Used in** | Planning → Epic → Invalid items navigation path |
| **Complexity** | 3 |
| **Status** | ✅ Implemented - Now filters work items to show descendants of root item |

---

### 2. Work Item Explorer — Validation Type Filtering ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Work Item Explorer — Implement validation type filtering |
| **Description** | The explorer receives `validationType` parameter but needs actual filtering logic to show only work items matching the validation issue type (missing-effort, missing-description, invalid-state, missing-tags). |
| **Used in** | Health → Validation category signal navigation |
| **Complexity** | 3 |
| **Status** | ✅ Implemented - Now loads and filters work items by validation type |

---

### 3. Work Item Explorer — Backlog Depth Exceeded Filter ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Work Item Explorer — Implement backlog depth exceeded filter |
| **Description** | When `backlogDepthExceeded=true` is passed, the explorer should filter to show only work items that exceed the configured backlog depth threshold. Requires integration with backlog health calculation service. |
| **Used in** | Health → Backlog too deep signal |
| **Complexity** | 4 |
| **Status** | ✅ Implemented - Calculates hierarchy depth for each work item. Threshold is 3 levels (depth > 3 = too deep). Shows Depth column when filter is active. Health workspace now shows count on signal card. |

---

### 4. Bug Overview — Real Data Integration ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Bug Overview — Connect to actual bug data |
| **Description** | Replace sample bug data with actual bug work items from the work item service. Filter by Bug work item type and apply relevant filters. |
| **Used in** | Health → Bug signal, Trends → Bug trend, Bug Triage task entry |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Now loads bugs from WorkItemService and filters by Bug type |

---

### 5. Bug Overview — Period Filtering ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Bug Overview — Implement period-based filtering |
| **Description** | When `period` parameter is provided (e.g., period=January-2026), filter bugs to show only those created or resolved within that time period. |
| **Used in** | Trends → Bug spike navigation |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Basic period filtering using the period query parameter |

---

### 6. Bug Detail — Save Changes Implementation

| Field | Value |
|-------|-------|
| **Title** | Bug Detail — Implement save changes to backend |
| **Description** | Connect the save button to the work item update API to persist severity and tag changes to TFS. **Note:** Backend currently lacks a direct work item field update API. This requires backend changes before frontend implementation. |
| **Used in** | Bug Detail end-station |
| **Complexity** | 2 (frontend) + 3 (backend) |
| **Status** | ⏸️ Blocked — Requires backend API changes |

---

### 7. PR Overview — Real Data Integration ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | PR Overview — Connect to actual PR data |
| **Description** | Replace sample PR data with actual pull request data from the PR service. This is a read-only view. |
| **Used in** | Trends → PR trend navigation |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Now loads PRs from PullRequestService with time-open calculation |

---

### 8. Pipeline Overview — Real Data Integration ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Pipeline Overview — Connect to actual pipeline data |
| **Description** | Replace sample pipeline data with actual pipeline run data from the pipeline service. This is a read-only view. |
| **Used in** | Trends → Pipeline failures navigation |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Now loads pipeline metrics from PipelineService with success rate calculation |

---

### 9. Beta Home — Real Health Signals ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Beta Home — Display actual health signals |
| **Description** | Replace placeholder health indicators with real-time signals from the health calculation service. Show aggregate health status, trend indicators, and sync state. |
| **Used in** | Beta Home overview section |
| **Complexity** | 3 |
| **Status** | ✅ Implemented - Shows real validation issue counts, active bug counts, and total work items |

---

### 10. Health Workspace — Validation Issue Counts ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Health Workspace — Show issue counts on signal cards |
| **Description** | Display actual counts of validation issues on each health signal card (e.g., "Missing Effort (47 items)"). |
| **Used in** | Health workspace signal grid |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Signal cards now show real validation issue counts from WorkItemService |

---

### 11. Trends Workspace — Velocity Chart Integration ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Trends Workspace — Embed velocity chart |
| **Description** | Integrate the existing velocity chart component into the Trends workspace with a maximum of 10 bars as specified. |
| **Used in** | Trends workspace velocity overview |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Embedded VelocityPanel component with MaxSprints=10 |

---

### 12. Planning Workspace — Epic Velocity Analysis ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Planning Workspace — Epic exceeds velocity detection |
| **Description** | Implement logic to detect when an epic's total effort exceeds the team's historical velocity and provide navigation to velocity trends. |
| **Used in** | Planning → Epic over velocity signal |
| **Complexity** | 4 |
| **Status** | ✅ Implemented - Loads velocity trend via IMetricsClient, gets epic forecasts to calculate sprints remaining. Epics needing >3 sprints or exceeding 3x average velocity are flagged as "at risk". Shows count on signal card and detailed table with remaining effort, sprints, and confidence level. |

---

### 13. Planning Workspace — Epic Invalid Items Detection ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Planning Workspace — Epic invalid items detection |
| **Description** | Implement logic to detect epics containing work items with validation issues and provide navigation to the Work Item Explorer with epic as root. |
| **Used in** | Planning → Epic invalid items signal |
| **Complexity** | 3 |
| **Status** | ✅ Implemented - Shows epics with child items that have validation issues, navigates to Work Item Explorer with rootWorkItemId |

---

### 14. Batch Edit Functionality

| Field | Value |
|-------|-------|
| **Title** | Work Item Explorer — Batch edit support |
| **Description** | Implement multi-select capability and batch edit operations for work item state, effort, and tags. End-station requirement. |
| **Used in** | Health workspace, Work Item Explorer, Bug Overview |
| **Complexity** | 5 |

---

### 15. Deep Link Return URL Handling ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Profile Selection — Implement return URL handling |
| **Description** | When redirected to profile selection due to missing PO context, implement return URL handling to resume navigation after profile selection. |
| **Used in** | All Beta pages when accessed without profile |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - ProfilesHome now parses returnUrl and navigates back after profile selection |

---

### 16. Context Propagation in Beta Navigation ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Beta Navigation — Product/Team context propagation |
| **Description** | When navigating from Beta Home with a specific product/team filter applied, propagate that context to downstream pages. Once set, context should be fixed during the navigation session. |
| **Used in** | All cross-workspace navigation |
| **Complexity** | 3 |
| **Status** | ✅ Implemented - Product filter on Beta Home propagates to all workspaces via productId query parameter |

---

### 17. "All Products/Teams" Toggle ✅ COMPLETED

| Field | Value |
|-------|-------|
| **Title** | Deep Navigation — Add "view all" toggle |
| **Description** | When navigating with product/team scope, provide a link to open the same view with "all products / all teams" scope as specified in the requirements. |
| **Used in** | Work Item Explorer, Bug Overview |
| **Complexity** | 2 |
| **Status** | ✅ Implemented - Added "View All" button to Bug Overview and Work Item Explorer |

---

## Summary

| Complexity | Count | Completed |
|------------|-------|-----------|
| 1 | 0 | 0 |
| 2 | 8 | 7 |
| 3 | 5 | 5 |
| 4 | 2 | 2 |
| 5 | 1 | 0 |
| **Total** | **17** | **14** |

**Completed Items:**
- #1 Work Item Explorer — Root Item Parameter Support ✅
- #2 Work Item Explorer — Validation Type Filtering ✅
- #3 Work Item Explorer — Backlog Depth Exceeded Filter ✅
- #4 Bug Overview — Real Data Integration ✅
- #5 Bug Overview — Period Filtering ✅
- #7 PR Overview — Real Data Integration ✅
- #8 Pipeline Overview — Real Data Integration ✅
- #9 Beta Home — Real Health Signals ✅
- #10 Health Workspace — Validation Issue Counts ✅
- #11 Trends Workspace — Velocity Chart Integration ✅
- #12 Planning Workspace — Epic Velocity Analysis ✅
- #13 Planning Workspace — Epic Invalid Items Detection ✅
- #15 Profile Selection — Return URL Handling ✅
- #16 Context Propagation — Product Filter in Navigation ✅
- #17 "All Products/Teams" Toggle ✅

**Blocked Items:**
- #6 Bug Detail — Save Changes (Requires backend API changes)

**Remaining Items:**
- #14 Batch Edit Functionality (Complexity 5)

---

## Notes

- Complexity ratings are on a scale of 1-5, where 1 is trivial and 5 is significant effort
- Items are not prioritized in this document — prioritization is a product decision
- Some items may have dependencies on others
