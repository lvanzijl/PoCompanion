# PO Companion — Navigation Map

**Audience:** Product Owners and stakeholders  
**Scope:** All non-legacy, non-settings pages  
**Purpose:** Human-readable reference of available navigation and functionality; basis for improvement analysis  
**Last Updated:** 2026-03-06 (PR Delivery Insights §2.11: Team Improvement Tips + Diagnostics expander added)

---

## 1. Navigation Overview

After a Product Owner logs in, the application offers a workspace-driven model organised around four concerns: *What is wrong right now?* (Health), *What was delivered?* (Delivery), *What has been happening over time?* (Trends), and *What should we do next?* (Planning).

### Entry Flow

```
[First Visit]
     │
     ▼
/profiles  ──► (profile selected)
     │
     ▼
/sync-gate  ──► (cache ready)
     │
     ▼
/home  ──────────────────────────────────────────────────────────────┐
  │                                                                   │
  ├──► /home/backlog-overview  (Backlog Overview — primary workspace) │
  │       ├──► /workitems?rootWorkItemId={epicId}  (ready/refinement) │
  │       ├──► /home/validation-queue?category=SI  (integrity)        │
  │       ├──► /home/health  (cross-workspace)                        │
  │       ├──► /home/trends  (cross-workspace)                        │
  │       └──► /home/planning  (cross-workspace)                      │
  │                                                                   │
  ├──► /home/health   (Health — Now)                                  │
  │       ├──► /home/validation-triage  (Validation Triage — primary)  │
  │       │       ├──► /home/validation-queue?category=SI              │
  │       │       │       └──► /home/validation-fix?category=SI&ruleId=SI-* │
  │       │       ├──► /home/validation-queue?category=RR              │
  │       │       │       └──► /home/validation-fix?category=RR&ruleId=RR-* │
  │       │       ├──► /home/validation-queue?category=RC              │
  │       │       │       └──► /home/validation-fix?category=RC&ruleId=RC-* │
  │       │       └──► /home/validation-queue?category=EFF             │
  │       │               └──► /home/validation-fix?category=EFF&ruleId=RC-2 (EFF maps to RC-2) │
  │       ├──► /home/validation-queue?category=SI|RR|RC  (signal cards — direct queue entry) │
  │       ├──► /home/bugs                                              │
  │       ├──► /home/backlog-overview  (cross-workspace)               │
  │       ├──► /home/trends  (cross-workspace)                         │
  │       └──► /home/planning  (cross-workspace)                       │
  │                                                                   │
  ├──► /home/delivery  (Delivery)                                     │
  │       ├──► /home/delivery/sprint  (Sprint Delivery)               │
  │       │       └──► /home/delivery/sprint/activity/{id}            │
  │       └──► /home/delivery/portfolio  (Portfolio Delivery)          │
  │                                                                   │
  ├──► /home/trends  (Trends — Past)                                  │
  │       ├──► /home/bugs  (bug trend drilldown)                      │
  │       ├──► /home/pull-requests  (read-only insight)               │
  │       ├──► /home/pr-delivery-insights  (PR Delivery Insights)     │
  │       ├──► /home/pipeline-insights  (PO-first stability overview) │
  │       ├──► /home/portfolio-progress  (portfolio trend)            │
  │       ├──► /home/trends/delivery  (delivery trends)               │
  │       ├──► /home/backlog-overview  (cross-workspace)               │
  │       ├──► /home/health  (cross-workspace)                        │
  │       ├──► /home/delivery  (cross-workspace)                      │
  │       └──► /home/planning  (cross-workspace)                      │
  │                                                                   │
  ├──► /home/planning  (Planning — Future)                            │
  │       ├──► /home/delivery/sprint  (velocity drilldown)            │
  │       ├──► /workitems?rootWorkItemId={epicId}                     │
  │       ├──► /home/dependencies  (read-only)                        │
  │       ├──► /planning/product-roadmaps  (Product Roadmaps — read-only) │
  │       │       └──► /planning/product-roadmaps/{productId}  (Editor) │
  │       ├──► /home/backlog-overview  (cross-workspace)               │
  │       ├──► /home/health  (cross-workspace)                        │
  │       └──► /home/trends  (cross-workspace)                        │
  │                                                                   │
  ├──► /home/validation-triage  (Validation Triage — Quick Action)    │
  ├──► /bugs-triage  (Bug Triage — Quick Action)                      │
  ├──► /workitems  (Work Item Explorer — Advanced Tools)               │
  └──► /home/plan-board  (Plan Board — Quick Action)                  │
                                                                       │
Global header (available on every page) ◄─────────────────────────────┘
  ├──► /home  (Home button)
  ├──► Health button  (cross-workspace shortcut)
  ├──► Delivery button  (cross-workspace shortcut)
  ├──► Trends button  (cross-workspace shortcut)
  ├──► Planning button  (cross-workspace shortcut)
  ├──► ProfileSelector  (switch active profile, inline)
  ├──► Onboarding wizard  (Help button → dialog)
  ├──► Keyboard Shortcuts dialog  (? key or toolbar button)
  └──► /settings  (Settings button — excluded from this map)
```

---

## 2. Page-by-Page Functional Descriptions

---

### 2.1 Profiles Home — `/profiles`

**Purpose:** Lets users select which Product Owner profile to activate before entering the application.

| Functionality | Description |
|---|---|
| Profile grid | Displays all available Product Owner profiles as Netflix-style tiles (avatar, name). |
| Select profile | Clicking a tile activates that profile and navigates to `/home` (or returns to the original page via `returnUrl` parameter). |
| Return URL support | When redirected here due to missing profile context, the page returns to the original destination after selection. |

**Outgoing navigation:** `/home` (after selection), `/settings/tfs` (if error — settings, out of scope)

---

### 2.2 Sync Gate — `/sync-gate`

**Purpose:** Interstitial page that waits for the TFS cache sync to be ready before allowing the user to proceed. Prevents the rest of the app from opening with stale or empty data.

| Functionality | Description |
|---|---|
| Sync status display | Shows a spinner, title, and description text while sync is in progress. |
| Progress messaging | Updates the user with clear status messages (loading, syncing, ready, error). |
| Auto-advance | Navigates automatically to `/home` once the cache is ready. |
| Retry | If sync fails, offers a "Retry" button to attempt again. |
| Back to Profiles | Allows the user to return to `/profiles` if a different profile is needed. |

**Outgoing navigation:** `/home` (auto on success), `/profiles`, `/settings`

---

### 2.3 Home — `/home`

**Purpose:** Central workspace hub. Provides a high-level health overview, a product context filter, workspace entry cards (Backlog Overview, Health, Delivery, Trends, Planning), and quick-action buttons for task-driven navigation.

| Functionality | Description |
|---|---|
| Health signal summary | Shows three live signals: validation issue count (color-coded), active bug count (color-coded), and total work item count. Colors follow fixed thresholds (0 = green, 1–9 = blue, 10–49 = yellow, 50+ = red). |
| Sync Now button | Triggers a manual cache sync for the active profile and reloads health signals on completion. |
| Product context filter | Optional product selector (dropdown). Selecting a product propagates a `productId` query parameter to all downstream workspaces. Shows "All Products" chip when no filter is active. |
| Backlog Overview workspace card | **First card.** Click to enter the Backlog Overview workspace. Carries product context. Primary entry point for backlog maturity decisions. |
| Health (Now) workspace card | Click to enter the Health workspace. Carries product context. |
| Trends (Past) workspace card | Click to enter the Trends workspace. Carries product context. |
| Planning (Future) workspace card | Click to enter the Planning workspace. Carries product context. |
| Delivery workspace card | Click to enter the Delivery workspace. Focuses on what was delivered per sprint or period. |
| Validation Triage quick action | **Primary** validation quick action (filled button). Opens `/home/validation-triage` for structured validation work. |
| Bug Triage quick action | Opens the Bug Triage page (`/bugs-triage`). |
| Work Item Explorer (Advanced Tools) | Secondary action (text button, lower visual weight). Opens Work Item Explorer for all products and all teams. Explorer is now "advanced inspection" only; not the start of the validation workflow. |

**Outgoing navigation:** `/home/backlog-overview`, `/home/health`, `/home/delivery`, `/home/trends`, `/home/planning`, `/home/validation-triage`, `/bugs-triage`, `/workitems`, `/profiles` (if no profile)

---

### 2.3a Backlog Overview — `/home/backlog-overview`

**Purpose:** Product-centered backlog maturity view. Shows refinement readiness per Epic → Feature → PBI based on the Backlog State Model. Primary entry point for the PO to understand what is plan-ready, what needs refinement, and what requires structural maintenance.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Backlog Overview` — clear location context. |
| Product selector | Dropdown when multiple products exist; auto-selects when only one product is configured. Respects `?productId=` context from Home. |
| Ready for Implementation section | Lists Epics with score = 100%. Each Epic shown as a card with feature count. Click → Work Item Explorer scoped to that Epic. |
| Needs Refinement section | Lists Epics with score < 100%, sorted descending by score. Each Epic is an expandable panel showing its Features with score, owner badge (PO/Team/Ready), and progress bar. Click any Feature row → Work Item Explorer scoped to the parent Epic. |
| Integrity Maintenance section | Shows count of Structural Integrity findings (product-scoped). Chip turns red when > 0. "Open Validation Queue" button navigates to `/home/validation-queue?category=SI`. SI findings do **not** affect refinement scores. |
| Cross-workspace navigation | Buttons to Health (Now), Trends (Past), and Planning (Future). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/workitems?rootWorkItemId={epicId}`, `/home/validation-queue?category=SI`, `/home/health`, `/home/trends`, `/home/planning`, `/home`

---

**Purpose:** Shows the current-state health of the backlog via two separated signal sections and an embedded Backlog Health Analysis panel. Designed for identifying actionable problems that need attention today. Structural Integrity (SI) and Refinement signals are presented in separate sections per the Backlog State Model.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Health (Now)` — provides clear location context. |
| Validation Triage button | Primary action button. Navigates to `/home/validation-triage` for grouped validation issue overview. |
| **Refinement Signals section** | Groups signals that affect backlog readiness. |
| Refinement Readiness signal card | Count of work items blocking refinement readiness (RR-*). Orange/yellow when count > 0. Click navigates to `/home/validation-queue?category=RR`. |
| Refinement Completeness signal card | Count of work items that need refinement (RC-*). Orange/yellow when count > 0. Click navigates to `/home/validation-queue?category=RC`. |
| Bugs signal card | Count of all bug work items. Uses threshold-based color (0 = green, 1–9 = blue, 10–49 = yellow, 50+ = red). Click navigates to Bug Insights. |
| **Integrity (Maintenance) section** | Groups structural integrity signals. Explicitly labelled as maintenance — does not affect refinement scores. |
| Structural Integrity signal card | Count of work items with structural integrity errors (rule IDs: SI-*). Red when count > 0. Click navigates to `/home/validation-queue?category=SI`. |
| Backlog Health Analysis panel | Embeds the BacklogHealthPanel component, showing up to 3 recent iterations. "Backlog Overview" button navigates to `/home/backlog-overview`. |
| Cross-workspace navigation | Buttons to navigate directly to Backlog Overview, Trends (Past), and Planning (Future) workspaces. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/validation-triage`, `/home/validation-queue?category=SI|RR|RC`, `/home/bugs`, `/home/backlog-overview`, `/home/trends`, `/home/planning`, `/home`

---

### 2.4a Validation Triage — `/home/validation-triage`

**Purpose:** Grouped, read-only overview of validation issues by category (SI, RR, RC, EFF). Primary entry point for validation work in the Health workspace. Shows how many work items are affected per category and which rules contribute most, enabling the PO to choose where to focus.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Health (Now) › Validation Triage` — clear position in the Health path. |
| Product context chip | Shows active product filter (if any) with a clear button. |
| SI card | Total items with Structural Integrity violations + top 3 rule groups. "Open queue" button navigates to `/home/validation-queue?category=SI`. |
| RR card | Total items with Refinement Readiness violations + top 3 rule groups. "Open queue" button navigates to `/home/validation-queue?category=RR`. |
| RC card | Total items with Refinement Completeness violations + top 3 rule groups. "Open queue" button navigates to `/home/validation-queue?category=RC`. |
| EFF card | Total items missing effort estimates (RC-2 rule). "Open queue" button navigates to `/home/validation-queue?category=EFF`. |
| Health (Now) button | Returns to the Health workspace. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/validation-queue?category=SI|RR|RC|EFF`, `/home/health`, `/home`

---

### 2.4b Validation Queue — `/home/validation-queue`

**Purpose:** Lists "Fix Cards" grouped by rule ID for a selected validation category. Shows how many work items are affected by each rule, ordered by impact. Primary action per card is "Start fix session" which opens the guided Fix Session (Phase 3).

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Health (Now) › Validation Triage › {Category} Queue` — full context path. |
| Product context chip | Shows active product filter (if any) with a clear button. |
| Category summary header | Displays category icon, label, total item count, and total rule group count. |
| Rule group cards | One card per rule ID, sorted by item count descending. Shows RuleId, short title, and affected item count. |
| "Start fix session" button | Per card; navigates to `/home/validation-fix?category=...&ruleId=...`. Disabled when no items. |
| Validation Triage button | Back navigation to `/home/validation-triage`, preserving context. |
| Home button | Returns to `/home`. |
| Empty state | Shows a success alert when no issues exist in the selected category. |

**Outgoing navigation:** `/home/validation-fix?category=...&ruleId=...`, `/home/validation-triage`, `/home`

---

### 2.4c Validation Fix Session — `/home/validation-fix`

**Purpose:** Guided per-item review flow for a single validation rule. Shows one work item at a time with its violation context, allowing the PO to work through the list methodically. Session progress (dismissed items) is tracked client-side only and resets on page reload.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Health (Now) › Validation Triage › {Category} Queue › Fix Session` — full context path. |
| Product context chip | Shows active product filter (if any) with a clear button. |
| Rule context banner | Displays category icon, rule ID, rule title, and category label. Shows a progress chip: "N dismissed · M remaining of total". |
| Item card | Shows current item: Type chip, State chip, Effort chip (if set), TFS ID, Title, violation message, Area Path, Iteration Path, Parent TFS ID (if set), and Description (if present). |
| Previous / Next buttons | Navigate through the active (non-dismissed) items. Disabled at start/end of list. |
| "Done for now" button | Adds the current item to the session-local dismissed set and advances to the next item. |
| "Skip" button | Advances to the next item without dismissing. |
| Session complete state | Shown when all items are dismissed. Offers "Restart session" (clears dismissed set) and "Back to Queue". |
| Empty state | Shown when no items violate the selected rule. |
| Back to Queue button | Returns to `/home/validation-queue?category=...`, preserving context. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/validation-queue?category=...`, `/home`, `/home/validation-triage`

---

### 2.5 Trends (Past) Workspace — `/home/trends`

**Purpose:** Shows time-based structural behavior patterns over the last 6 months (configurable via sprint selection). Contains only pages whose primary axis is a sprint timeline. Designed for understanding "why is this happening?" from historical data.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past)`. |
| Team selector | Filters the sprint range selector to a specific team. When "All Teams" is selected, defaults to last 6 months. |
| From Sprint / To Sprint selectors | Appear once a team is selected. Allow the user to define a custom time window for trend analysis. Sprint name and start month are shown. |
| "Last 6 Months" chip | Shown when no sprint range is explicitly set. |
| Bug Trend signal card | Represents bug patterns over time. Click navigates to Bug Insights (Bug Overview). |
| PR Trend signal card | Read-only insight about pull request patterns. Click navigates to Pull Request Insights. |
| Pipeline Insights signal card | Read-only insight about pipeline stability per product. Click navigates to Pipeline Insights. |
| Portfolio Progress signal card | Represents strategic product-level progress over a sprint range. Click navigates to Portfolio Progress Trend. |
| Bug Trend chart (interactive) | Three-series chart (Total bugs, Fixed bugs, Added bugs) for the selected time range. Clicking a bar navigates to Bug Insights filtered to that period. Hovering highlights the bar. |
| Cross-workspace navigation | Buttons to Backlog Overview, Health (Now), Delivery, and Planning (Future). |
| Home button | Returns to `/home`. |

> **Note:** Sprint Delivery (formerly Sprint Trend) has moved to the Delivery workspace. Velocity and predictability signals (median velocity, P25–P75 band, median predictability) are embedded in Sprint Delivery (calibration panel) and Planning (Capacity Confidence block).

**Outgoing navigation:** `/home/portfolio-progress`, `/home/trends/delivery`, `/home/bugs`, `/home/pull-requests`, `/home/pr-delivery-insights`, `/home/pipeline-insights`, `/home/delivery`, `/home/backlog-overview`, `/home/health`, `/home/planning`, `/home`

---

### 2.5b Delivery Trends — `/home/trends/delivery`

**Purpose:** Temporal analysis of delivery behavior across multiple sprints. Answers "are we delivering more PBIs?", "is our throughput growing?", "how is our completion rate trending?", and "is our bug inflow under control?". Always shows a sprint timeline on the X-axis; never a single-sprint snapshot.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › Delivery Trends`. |
| Team selector | Filters the sprint list to a specific team. |
| Product selector | Optional filter to a specific product. When "All Products" is selected, totals across all products are shown. |
| End sprint selector | Selects the most recent sprint shown in the range. |
| Sprints to show | Numeric field controlling how many sprints to include (minimum 2, default 6). |
| Range label | Caption showing the resolved sprint range (e.g., "Sprint 10 – Sprint 15"). |
| PBI Throughput Trend (primary) | Full-width bar chart showing completed PBIs per sprint. Slope badge (Improving / Stable / Worsening). Higher is better. |
| Effort Throughput Trend | Bar chart showing story points delivered per sprint. Slope badge. Higher is better. |
| Progress Trend | Line chart showing completed effort as a percentage of planned effort per sprint. Slope badge. Higher is better. |
| Bug Trend | Line chart with two series: bugs created and bugs closed per sprint. Slope badge for created (lower is better). |
| Drill-down table | Collapsed expansion panel showing per-sprint detail: completed PBIs, completed effort, planned effort, completion %, bugs created, bugs closed. |
| Back to Trends button | Returns to `/home/trends`. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/trends`, `/home`

---

### 2.5a Delivery Workspace — `/home/delivery`

**Purpose:** Inspection of what was delivered — per sprint or aggregated across products. Delivery focuses on outcomes (what got done) rather than time-based trends. It is the entry point for sprint-level and portfolio-level delivery views.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery`. |
| Sprint Delivery signal card | Represents planned vs. delivered per sprint. Click navigates to Sprint Delivery. |
| Portfolio Delivery signal card | Aggregated delivery view across products. Click navigates to Portfolio Delivery. |
| Cross-workspace navigation | Buttons to Backlog Overview, Health (Now), Trends (Past), and Planning (Future). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/delivery/sprint`, `/home/delivery/portfolio`, `/home/backlog-overview`, `/home/health`, `/home/trends`, `/home/planning`, `/home`

---

### 2.6 Planning (Future) Workspace — `/home/planning`

**Purpose:** Provides decision-making signals for upcoming work, focusing on capacity risks and backlog quality. Shows current iteration plus 3 future iterations.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Planning (Future)`. |
| "Current + 3 Iterations" chip | Communicates the time horizon of the planning view. |
| Epic Exceeds Velocity signal card | Counts epics whose remaining effort exceeds 3× average team velocity. Color-coded (green = all fine, yellow ≤ 2 at risk, red > 2). Average velocity is shown as a chip. Click navigates to Sprint Delivery (calibration context). |
| Epic Invalid Items signal card | Counts epics that contain child work items with validation issues. Click navigates to Work Item Explorer (all items). |
| Epic Dependencies signal card | Read-only view. Click navigates to Dependency Overview. |
| Epics Exceeding Velocity detail table | Shown when epics are at risk. Columns: ID, Title, State, Remaining Effort, Sprints to Complete, Confidence, and link to Sprint Delivery. |
| Epics with Invalid Items detail table | Shown when epics have validation issues. Columns: ID, Title, State, Invalid Item Count, and link to Work Item Explorer (scoped to that epic). |
| Capacity Confidence block | Shown when past sprint data is available. Displays median velocity (P50), P25–P75 band, median predictability, and a safe planning capacity (P25). Updates when product selection changes. |
| Planning Board section | Embeds the PlanningBoard component with product selector. "Full Release Planning" link to `/release-planning`. |
| Product Roadmaps button | Navigates to the Product Roadmaps overview (`/planning/product-roadmaps`). |
| Available Actions chips | Lists supported end-station actions: Epic Repositioning and Implicit Reprioritization. |
| Cross-workspace navigation | Buttons to Backlog Overview, Health (Now), and Trends (Past). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/delivery/sprint`, `/workitems` (scoped to epic or all), `/home/dependencies`, `/planning/product-roadmaps`, `/home/backlog-overview`, `/home/health`, `/home/planning`, `/release-planning`, `/home`

---

### 2.7 Bug Insights — `/home/bugs`

**Purpose:** Provides a detailed view of all active bugs, with filtering by product, team, and period. Read-oriented with a link to Bug Triage for action.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Bug Insights`. |
| Bug Triage button | Navigates to the full Bug Triage page for triaging and categorising bugs. |
| Product/Team filters | Allows filtering bugs by product and team context. |
| Period filter | When a `period` query parameter is provided (e.g., from clicking a bar on the Bug Trend chart), bugs are filtered to that time period. |
| Bug list / grid | Displays bugs with their key attributes (ID, title, state, severity, etc.). |
| View All toggle | Shows all bugs across all products and teams, regardless of context filter. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/bugs-triage`, `/home`, `/home/bugs/detail/{id}` (Bug Detail)

---

### 2.8 Bug Detail — `/home/bugs/detail/{id}`

**Purpose:** Detailed view for a single bug. Allows the PO to review and edit bug attributes (severity, tags).

| Functionality | Description |
|---|---|
| Bug metadata display | Shows all key fields: ID, title, state, severity, tags, description. |
| Severity edit | PO can change the severity of the bug (subject to pending backend API — currently blocked). |
| Triage tag edit | PO can assign or remove triage tags. |
| Save Changes button | Persists changes (currently blocked pending backend API). |
| Back navigation | Returns to Bug Insights. |

**Outgoing navigation:** `/home/bugs`

---

### 2.9 Bug Triage — `/bugs-triage`

**Purpose:** Focused tool for triaging all open bugs. Provides a tree-grid view of bugs grouped by product hierarchy, with triage tagging capabilities.

| Functionality | Description |
|---|---|
| Bug tree grid | Displays all open bugs in a collapsible tree view organised by product hierarchy. Shows total bug count and untriaged count. |
| Untriaged count | Prominently shows how many bugs still need triage attention. |
| Triage tags | Allows assigning triage tags (e.g., "Won't Fix", "Next Sprint", "Deferred") to individual bugs. |
| Product/profile filtering | Bugs are filtered to the active profile's products. |

**Outgoing navigation:** (self-contained; accessed from Home quick actions or Bug Insights)

---

### 2.10 Pull Request Insights — `/home/pull-requests`

**Purpose:** PO-focused, read-only PR workflow friction analysis for a selected team. Insight-only; no editing.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › Pull Request Insights`. |
| Team selector | Scopes PR data to all products linked to the selected team. |
| Sprint selector | Optional. Appears once a team is selected. Choosing a sprint automatically sets the From/To date range to that sprint's boundaries and triggers a reload. Selecting "Custom range" reverts to manual date editing. |
| Date range selector | Limits displayed PRs to a date window (default: last 6 months). Automatically filled when a sprint is selected. |
| Date quick-presets | One-click chips for 1M / 3M / 6M / 1Y / 2Y. Active preset is highlighted. Selecting a preset clears the sprint selection. |
| Repository filter | Optional filter to a single repository. |
| Global Summary chips | Total PRs, Merge %, Abandon %, Rework %, Median lifetime, P90 lifetime. |
| Top 3 Friction PRs | Cards ranking PRs by composite score (lifetime 40%, review cycles 30%, files 20%, comments 10%). Clicking highlights the PR in the scatter chart. Each card includes an "Open in Azure DevOps" link when TFS configuration is available. |
| PR Scatter chart | Pure-SVG `PullRequestScatterSvg` component: X = creation date, Y = lifetime (hours). Color: green (merged clean), yellow (merged after rework), red (abandoned). Shape per repository. Hover tooltip; clicking a point highlights/dims and opens the PR Detail Drawer. Median and P90 overlay lines. Author filter chip shown when an author is selected. |
| PR Detail Drawer | Right-anchored `MudDrawer` opened by clicking a scatter point. Shows: status chip (with rework badge), PR title, author, repository, creation date, lifetime, review cycles, files changed, comment count, and an "Open in Azure DevOps" button (visible when TFS configuration is available). Closes via ×-button or when filters are changed. |
| Longest PR table | Top 20 PRs ordered by lifetime descending. Columns: PR title (clickable link to Azure DevOps when TFS configuration is available), repository, author, lifetime, review cycles, files changed, comments, status. |
| Repository breakdown | Collapsible table showing per-repository workflow behaviour: PR count, merge %, abandon %, median lifetime, P90 lifetime, average review cycles. Sorted by PR count descending. |
| Author breakdown | Collapsible table showing per-author workflow behaviour: PR count, merge %, abandon %, rework %, median lifetime, average review cycles. Sorted by PR count descending. Clicking a row filters the scatter chart to that author's PRs only (toggle). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home`, `/home/trends`, Azure DevOps (external, when TFS configuration is available)

---

### 2.11 PR Delivery Insights — `/home/pr-delivery-insights`

**Purpose:** PO-focused, read-only view that classifies Pull Requests by their linked work items and aggregates metrics at Epic and Feature level. Answers the question: *"Which Epics and Features are generating the most PR friction?"* Also provides actionable Team Improvement Tips derived from signal detection on the PR data. All data is read from the local cache — no live Azure DevOps calls.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › PR Delivery Insights`. |
| Team selector | Scopes PR data to products linked to the selected team. |
| Sprint selector | Optional. Choosing a sprint sets the date range to sprint boundaries and triggers a reload. |
| Date range selector | Limits PRs to a creation-date window (default: last 6 months). Overridden when a sprint is selected. |
| Category summary chips | Four chips showing: DeliveryMapped (PR traces to Feature/Epic), Bug (PR linked to Bug with no Feature/Epic), Disturbance (PBI without Feature parent), Unmapped (no work item link). Each chip shows count and percentage. |
| Epic breakdown table | Per-Epic metrics: Epic name, PR count, median lifetime, P90 lifetime, abandoned %, average review cycles. Sorted by PR count descending. |
| Feature breakdown table | Per-Feature metrics: Feature name, Epic name, PR count, PR-per-PBI ratio, median lifetime. Sorted by PR count descending. |
| Delivery scatter chart | Scatter plot (X = PR creation date, Y = lifetime in hours). Points are coloured by category (DeliveryMapped / Bug / Disturbance / Unmapped) and shaped by Epic. |
| Outlier PR table | Top 20 longest-lived PRs: PR title, repository, status, lifetime, files changed, review cycles, Epic name, Feature name, category. |
| Team Improvement Tips | Up to three rule-based tips derived from signal detection. Each tip shows: Signal (detected metric pattern), Interpretation (what the pattern likely indicates), and PO Message (concise message for the team). |
| Diagnostics expander | Optional, hidden by default. Shows Feature complexity table (PR/PBI ratios) and Bug PR distribution per Epic context. |
| Home button | Returns to `/home`. |

**PR Classification Rules (priority order):**

| Category | Condition |
|---|---|
| DeliveryMapped | Any linked work item traces to a Feature or Epic through its parent hierarchy. |
| Bug | PR linked to a Bug work item where no Feature/Epic ancestor can be resolved. |
| Disturbance | PR linked to a PBI without a Feature parent. |
| Unmapped | PR has no usable work item link. |

When multiple linked work items resolve to different categories, the highest-priority category is used.

**Team Improvement Tips — Signal Detection Rules:**

| Signal | Condition | Threshold |
|---|---|---|
| Long PR Lifetimes | Global median lifetime of completed PRs exceeds baseline. | median > 24 h |
| High Review Churn | High percentage of completed PRs required multiple review iterations. | > 30 % of PRs with ReviewCycles > 1 |
| High Bug PR Share | Large proportion of PRs linked to Bug work items. | BugPct > 20 % |
| High Disturbance Share | Many PRs linked to PBIs without Feature parents. | DisturbancePct > 20 % |
| Epic-Specific Friction | Single Epic's median lifetime is more than 2× the global median (minimum 3 PRs). | epicMedian > 2 × globalMedian |

**Outgoing navigation:** `/home`, `/home/trends`

---

### 2.12 Dependency Overview — `/home/dependencies`

**Purpose:** Read-only visual overview of work item dependencies between epics and teams.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Dependency Overview`. |
| Read-only notice | Prominent alert informing the user that this is an insight-only view. Links to the full `/dependency-graph` for management. |
| Dependencies panel | Embedded DependenciesPanel component showing cross-team and cross-epic dependencies. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home`, `/dependency-graph` (full management, separate page)

---

### 2.12a Product Roadmaps — `/planning/product-roadmaps`

**Purpose:** Read-only overview of product roadmaps across all products. Displays horizontal product lanes with vertically stacked roadmap epics. Products are ordered by the root **Objective work item's TFS BacklogPriority** (`Microsoft.VSTS.Common.BacklogPriority`). Roadmap epics within each lane are ordered by the **Epic work item's TFS BacklogPriority**. Only epics tagged with **"roadmap"** are shown. Epic cards display order number, title, TFS ID, and a link to open the epic in TFS.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Planning (Future) › Product Roadmaps`. |
| Read-only chip | Indicates this is a read-only view. Epic editing is done in the Product Roadmap editor page. |
| Product lanes | Horizontal scrollable container with one lane per product. Each lane shows the product name and epic count. |
| Product lane ordering | Derived from the product's root Objective work item BacklogPriority in TFS. TfsId used as stable tie-breaker when priorities collide. |
| Move Earlier/Later buttons | Swap Objective BacklogPriority with the neighbouring product in TFS. Disabled at boundaries (first/last) and during reorder operations. After reorder: writes to TFS → refreshes cache → reloads page from cache. |
| Roadmap epic ordering | Derived from each Epic work item's BacklogPriority in TFS. TfsId used as stable tie-breaker when priorities collide. |
| Roadmap epic cards | Each card shows: order number (#1, #2, …), epic title, TFS ID, and an "Open in TFS" icon link. |
| Empty lane placeholder | When a product has no roadmap epics, an informational message is displayed. |
| Home button | Returns to `/home`. |
| Cross-workspace navigation | Buttons to Planning (Future) and Health (Now). |
| Reporting menu | Dropdown menu (disabled when no products are loaded) with actions: **Generate Visual Roadmap** (copies a Markdown roadmap to clipboard), **Export Structured Data (JSON)** (copies structured JSON to clipboard), and **AI Prompt Templates** (Executive Roadmap, Customer-Facing Roadmap, Milestone Infographic — each copies a ready-to-use AI prompt with embedded roadmap data to clipboard). All reporting actions are read-only and never modify TFS data. |
| Snapshots menu | Dropdown menu with snapshot actions: **Create Snapshot** (captures the current roadmap state — product order, epic order, titles, TFS IDs — into browser localStorage; disabled when no products are loaded), **View Snapshots** (opens a dialog listing all stored snapshots with timestamp, description, product/epic counts, and actions to compare or delete). Snapshots are stored application-side and never modify TFS data. |
| Drift detection | From the snapshot list, the PO can compare any snapshot against the current roadmap. The comparison dialog shows per-product drift: unchanged epics, epics moved earlier or later, newly added epics, and removed epics. Visual drift indicators use color-coded chips (green=unchanged, blue=earlier, orange=later, green-filled=added, red=removed). |

**Outgoing navigation:** `/home`, `/home/planning`, `/home/health`

---

### 2.12b Product Roadmap Editor — `/planning/product-roadmaps/{productId}`

**Purpose:** Per-product roadmap editor that allows a Product Owner to curate roadmap epics. Supports both **drag-and-drop** (via MudBlazor DropContainer/DropZone) and **button-based** controls for adding, removing, and reordering epics. Drag-and-drop allows transferring epics between Available Epics and Roadmap Epics lists, and reordering within the roadmap by dragging to the desired position. Each epic card has a dedicated drag handle to prevent accidental drag starts. The editor operates on a single product roadmap and persists changes immediately to TFS via the TFS write → cache refresh → reload pattern. Roadmap membership is determined by the lowercase **"roadmap"** tag on epics.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Planning (Future) › Product Roadmaps › {ProductName}`. |
| Two-column layout | Left column: Roadmap Epics (ordered by BacklogPriority). Right column: Available Epics (with search/filter). |
| Roadmap epics | Epics under the product that contain the lowercase "roadmap" tag, ordered by Epic BacklogPriority. TfsId used as tie-breaker. |
| Available epics | Epics under the product that do not contain the roadmap tag. Ordered alphabetically by title. |
| Epic cards (roadmap) | Each card shows: drag handle, order number (#1, #2, …), epic title, TFS ID, "Open in TFS" link. Actions: Move Earlier, Move Later, Remove from roadmap, Edit. Draggable to reorder within roadmap or to remove by dragging to Available Epics. |
| Epic cards (available) | Each card shows: drag handle, epic title, TFS ID, "Open in TFS" link. Actions: Add to Roadmap, Edit. Draggable to add to roadmap by dragging to Roadmap Epics. |
| Drag-and-drop | MudBlazor DropContainer/DropZone foundation. Drag epics between lists to add/remove from roadmap. Drag within Roadmap Epics to reorder. Dedicated drag handle prevents accidental drags. Drop zone highlighting and placeholder indicators provide clear visual feedback. All drag-and-drop changes follow the same TFS persistence flow as button actions. |
| Move Earlier/Later | Swaps Epic BacklogPriority with the neighbouring epic. Normalizes priorities if inconsistent or duplicated — normalized values are persisted to TFS for all roadmap epics. Writes to TFS → refreshes cache → reloads editor. |
| Add to roadmap | Via button: appends the "roadmap" tag, assigns BacklogPriority (first epic: 1000, subsequent: max + 1000), appends to end. Via drag-and-drop: inserts at drop position with calculated priority. Writes to TFS → refreshes cache → reloads editor. |
| Remove from roadmap | Removes the "roadmap" tag from the epic's tags. Preserves other tags. Works via button click or by dragging to Available Epics. Writes to TFS → refreshes cache → reloads editor. |
| Search/filter | Text filter for available epics by title or TFS ID. Search field is part of the sticky header area and remains visible during scrolling. |
| Right-side drawer | Single drawer for epic preview and editing. Displays TFS ID, "Open in TFS" link, editable Title and Description fields. Save button persists changes via TFS write → cache refresh → reload. |
| Save feedback | A status chip in the header shows "Saving…" during persistence, "Saved to TFS" on success, or "Failed to update TFS" on failure. Auto-clears after 3 seconds. Does not interrupt editing flow. |
| Loading feedback | A progress bar appears during roadmap data reload from cache after persistence. |
| Empty roadmap guidance | When the roadmap contains zero epics, a centered helper message with icon guides the user to add epics via drag-and-drop or button. |
| Roadmap size indicator | A chip in the Roadmap Epics header shows the current epic count (e.g., "Epics: 8"). Updates dynamically on add/remove. |
| Epic highlight | Newly added epic cards briefly highlight with a fade animation to indicate where the epic appeared. |
| Epic metadata area | A reserved area on each epic card for future metadata indicators (PBI count, effort totals, health warnings). |
| Sticky headers | Both column headers (Roadmap Epics, Available Epics) are sticky and remain visible during vertical scrolling. |
| Concurrency | Latest-state-wins: after each persistence operation the editor reloads from cache, accepting the newest TFS state. |
| All Roadmaps button | Returns to the Product Roadmaps overview page (`/planning/product-roadmaps`). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home`, `/planning/product-roadmaps`

---

### 2.13 Plan Board — `/home/plan-board`

**Purpose:** Focused planning board showing epics and features organised by iteration. Accessed as a Quick Action from Home.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Plan Board`. |
| "All Products" chip | Shown when accessed from the Home quick action (no product filter applied). |
| Product selector | Allows narrowing the board to a specific product. |
| Planning board | Embeds the PlanningBoard component with the selected product context. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home`

---

### 2.14 Sprint Delivery — `/home/delivery/sprint`

> **Route alias:** `/home/sprint-trend` (legacy route, kept for backward compatibility)

**Purpose:** Detailed single-sprint inspection of delivery signals. Shows what was delivered (Delivered pts), how scope changed (Δ Effort pts), PBI completion, and bug counts per product and epic. Use navigation arrows to move between sprints. Drill down from epics into a feature modal, and from features into activity history. For multi-sprint trend analysis, use Delivery Trends (`/home/trends/delivery`). Located in the Delivery workspace.

**Navigation hierarchy:**
```
Sprint Delivery
  → Epic table (where effort landed)
      → Feature modal (how work distributed within an epic)
          → Activity history (what exactly changed)
```

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery › Sprint Delivery`. |
| Sprint navigation arrows | Navigate backwards and forwards one sprint at a time through the sprint history. |
| Product filter | Filters metrics to a specific product. |
| Team filter | Filters metrics to a specific team. |
| Product sections | One collapsible panel per product. Products with no delivery signal (Delivered = 0, Δ Effort = 0, no activity) are hidden automatically. |
| Collapsed product summary | When collapsed, each product shows compact chips: Delivered (pts), Δ Effort (pts), PBIs, and Bugs (Created / Worked / Closed). |
| Epic table | Visible when a product panel is expanded. Columns: Epic (ID + title), Progress (completed/total effort), Delivered (pts completed this sprint), Δ Effort (pts scope change this sprint), Features ✓ (features that reached Done during this sprint), PBIs ✓ (PBIs that transitioned to Done during this sprint), Actions. Epics with Delivered = 0, Δ Effort = 0, and no PBI activity are hidden. |
| Epic column definitions | **Progress** = completed effort / total effort. **Delivered** = effort of PBIs completed in this sprint. **Δ Effort** = effort_end_of_sprint − effort_start_of_sprint (absolute pts, positive = scope added, negative = scope reduced). **Features ✓** = count of features whose state transitioned to Done during this sprint. **PBIs ✓** = count of PBIs that transitioned to Done during this sprint. |
| Feature modal | Opened via the list icon on an epic row. Modal columns: Feature (ID + title), Progress, Delivered (pts), Δ Effort (pts), PBIs ✓. PBIs ✓ = count of PBIs that transitioned to Done during the sprint under this feature. Feature progress bars are thinner and slightly dimmer than epic bars. Features with zero activity are hidden. |
| Activity history link | History icon on each epic row navigates to the Work Item Activity page for that epic. |
| All Products sprint summary | Panel showing total sprint metrics across all products (Completed PBIs, Effort delivered, Bugs). |
| Stale data warning | If the calculated sprint metrics are older than the latest data sync, shows a warning with a "Recompute" button to refresh the analysis. |
| Back to Delivery button | Returns to `/home/delivery`. |
| Home button | Returns to `/home`. |

**Removed:** Sprint Δ% column (replaced by Δ Effort in absolute pts). The ✓ symbol always means *completed during this sprint*.

**Outgoing navigation:** `/home/delivery/sprint/activity/{id}`, `/home/delivery`, `/home`

---

### 2.14b Portfolio Delivery — `/home/delivery/portfolio`

**Purpose:** Aggregated delivery snapshot across products for the selected sprint range. Answers "What did the organisation deliver across products in the selected sprint(s)?". Works for a single sprint or multiple sprints aggregated. No time-series charts — all visualisations show composition or distribution. Located in the Delivery workspace.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery › Portfolio Delivery`. |
| Sprint range selector | Chip-based popover selectors for Team, From Sprint and To Sprint. Auto-defaults to current sprint + 4 past sprints. Single sprint or multi-sprint range is supported. |
| Aggregation indicator | Shows "Single sprint snapshot" or "Aggregated snapshot — N sprints" above the summary section. |
| Portfolio summary metrics | Six KPI tiles: Completed PBIs, Effort Delivered (story points), Avg. Progress %, Bugs Created, Bugs Worked, Bugs Closed — aggregated across all products in the sprint range. |
| Product contribution chart | Horizontal progress bar chart showing each product's share of total delivered effort (%). Ordered by descending effort. |
| Feature contribution chart | Top feature contributors by delivered effort — horizontal progress bar chart. Ordered by descending effort, limited to top 10. Epic title shown as subtitle. |
| Bug distribution summary | Table showing Bugs Created, Bugs Worked, Bugs Closed, and Net (Created − Closed) per product. Only shown when at least one product has bug activity. |
| Back to Delivery button | Returns to `/home/delivery`. |

**Outgoing navigation:** `/home/delivery`, `/home`

---

### 2.15 Sprint Activity — `/home/delivery/sprint/activity/{workItemId}`

> **Route alias:** `/home/sprint-trend/activity/{workItemId}` (legacy route, kept for backward compatibility)

**Purpose:** Shows the sprint activity history for a work item (Feature or Epic) and its descendants within the context of Sprint Delivery analysis. The page focuses on readability: creation events are collapsed, changes are classified by type, and events are grouped by work item.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery › Sprint Delivery › Activity #ID`. |
| Work item metadata | Displays type, ID, title, and the sprint period in use. |
| Activity Summary block | Six KPI tiles derived from the loaded events: Work items created, Work items completed, State transitions, Effort changes, Scope increases, Scope decreases. |
| Grouped activity view | Events are grouped by work item into collapsible sections (MudExpansionPanel). Each section shows the work item type, ID, title, and an event count. Expand-all / Collapse-all controls are provided. |
| Change Type column | Derived column classifying each event as one of: `Workflow`, `Scope`, `Creation`, `Metadata`, or `Structure`. High-importance types (Workflow, Scope, Creation) are shown at full opacity; low-importance types (Metadata, Structure) are visually dimmed. |
| Creation event collapsing | When multiple events for the same work item share the same timestamp and all fields belong to the creation-indicator set (System.Id, System.WorkItemType, System.State, System.Reason, System.CreatedDate, System.AreaPath, System.IterationPath), they are collapsed into a single `(created)` row. |
| Table columns | Timestamp, Change Type, Work Item, Source, Field, Old Value, New Value. |
| Back to Sprint Delivery button | Returns to the Sprint Delivery page. |

**Change Type classification rules**

| Field pattern | Change Type |
|---|---|
| `System.State`, `Microsoft.VSTS.Common.ClosedDate` | Workflow |
| `Microsoft.VSTS.Scheduling.*` | Scope |
| `System.Title` | Metadata |
| `System.AreaPath`, `System.IterationPath` | Structure |
| `System.CreatedDate`, `System.WorkItemType`, `System.Id`, `System.Reason` | Creation |
| All other fields | Metadata |

**Outgoing navigation:** `/home/delivery/sprint`

---

### 2.17 Portfolio Progress Trend — `/home/portfolio-progress`

**Purpose:** Strategic, product-level progress insight over a user-selected sprint range. Answers "how far are we overall?", "is progress accelerating or stalling?", and "is remaining effort decreasing?".

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › Portfolio Progress`. |
| Product selector | Optional dropdown defaulting to "All Products". Filters the data to a specific product. Selecting a different product reloads all charts and the summary. |
| Team selector | Required to load available sprints for the sprint range selector. |
| From Sprint / To Sprint selectors | Appear once a team is selected. Define the analysis time window. Both selectors are optional; omitting one uses the first/last sprint in the team's history. |
| Summary block | Shows: Progress (X% → Y%), Scope Change (N/A — current snapshot baseline), Remaining Effort Change (±N pts), and Sprint count. Includes a trajectory badge: Improving / Stable / At Risk. |
| % Done Over Time chart | Line chart showing cumulative done effort / total scope per sprint. Higher-is-better slope badge. |
| Total Scope Over Time chart | Bar chart showing total scope effort per sprint. Rising late in the range signals risk (slope badge). |
| Remaining Effort Trend chart | Line chart showing remaining effort per sprint (long-horizon burndown). Lower-is-better slope badge. |
| Throughput per Sprint chart | Bar chart showing effort completed per sprint. Higher-is-better slope badge. |
| Back to Trends button | Returns to `/home/trends`. |

**Outgoing navigation:** `/home/trends`

---

### 2.18 Pipeline Insights — `/home/pipeline-insights`

**Purpose:** PO-first pipeline stability overview for a single selected sprint, showing aggregated health metrics, a build stability scatter chart, and a per-pipeline breakdown table per product. Phase 1–4: ranking by failure rate with delta vs. previous sprint, per-product TimeScatterSvg scatter (X=start time, Y=duration), a collapsible per-pipeline breakdown table with half-sprint trend indicators, and UX navigation polish (auto-sprint selection on team change; scroll-to-product from global top-3 cards). All data sourced from local cache only.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › Pipeline Insights`. |
| Team selector | Selects a team whose sprint list is used for the sprint selector. |
| Sprint selector | Selects the sprint to analyse. Populated once a team is selected. |
| Include partial success toggle | When enabled (default ON), partiallySucceeded runs are counted as completed and shown as warnings. |
| Include canceled toggle | When enabled (default OFF), canceled runs are counted in the total and completed build counts. |
| Global summary chips | Total builds, failure rate % (with count), warning rate % (with count), P90 duration. Aggregated across all PO products. |
| Global Top 3 in trouble | Three most problematic pipelines globally, ranked by failure rate (descending). Each card shows: pipeline name, product name, failure rate %, failed/completed count, delta vs. previous sprint (n/a when no previous sprint data). |
| Per-product sections | One section per product owned by the active Product Owner, ordered by product name. Each section shows product name, per-product top-3 in trouble (click to highlight pipeline on scatter), pipeline stability scatter (TimeScatterSvg, X=build start time, Y=duration minutes), and product summary chips (failure rate, warning rate, success rate, median duration, P90 duration). Empty state when no cached runs in the selected sprint. |
| Pipeline Stability Scatter | Per-product SVG scatter chart (TimeScatterSvg). Dots colored by result (green=succeeded, yellow=partial, red=failed). Median and P90 duration overlay lines. Optional SLO duration input. Highlight a pipeline by clicking its top-3 entry; non-highlighted points dim. |
| Build Summary Drawer | Opens when a scatter dot is clicked. Shows: build number, pipeline name, result, start time, finish time, duration, branch, and an Azure DevOps link (when URL is cached). |
| Per-pipeline breakdown | Collapsible MudExpansionPanel per product section showing all pipelines (not just top 3) with columns: Pipeline, Runs, Success%, Failure%, Median duration, P90, Δ Failure, Half-Sprint Trend. Ordered by failure rate descending. Scrollable when > 8 pipelines. |
| Half-Sprint Trend chip | Per-pipeline trend derived from comparing failure rates in first vs. second half of the sprint. Improving (green, ≥ 10 pp drop), Degrading (red, ≥ 10 pp rise), Stable (gray), Insufficient (—, < 2 completed runs in a half). Tooltip shows first-half and second-half failure rates. |
| Auto-sprint selection | When a team is selected, the current sprint is automatically selected (the sprint whose window covers today; if none, the most recently ended sprint). No extra click required. |
| Scroll-to-product | Clicking a global top-3 card smoothly scrolls to the corresponding per-product section. |
| Empty state | When no sprint is selected, a prompt guides the user to select a team and sprint. |
| Error handling | Network/cache errors show an alert with a Retry button. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/trends`, `/home`

---

### 2.16 Work Item Explorer — `/workitems`

**Purpose:** Hierarchical explorer for all work items in the active profile's products. Primary tool for reviewing and filtering the backlog by validation issues, type, or scope.

| Functionality | Description |
|---|---|
| Toolbar | Shows selected item count and offers Select All / Clear Selection actions. |
| Validation Summary Panel | Collapsible panel showing all validation issues across loaded work items, with auto-fix suggestions where available. |
| Validation History Panel | Collapsible panel showing past validation runs and their results. |
| Validation filter checkboxes | Quick-filter the tree by validation category (Structural Integrity, Refinement Readiness, Refinement Completeness). Counts shown per filter. |
| Work Item Tree (left pane) | Hierarchical tree/grid of all work items. Supports text search, expand/collapse, and multi-select (click, Shift+click, Ctrl+click). Keyboard navigation supported. |
| Work Item Detail Panel (right pane) | Shows full details of the selected work item: metadata, description, validation issues, and activity timeline (revision history via MudTimeline). |
| Resizable splitter | User can drag the divider between tree and detail panes. Position is persisted per session. |
| Root item scoping | When `rootWorkItemId` is provided via URL, the tree starts from that item and shows its descendants only. |
| Validation category filtering | When `validationCategory` is provided (1=SI, 2=RR, 3=RC), filters the displayed items to those with issues in that category. |
| All Products / All Teams toggle | When `allProducts=true` is passed, loads work items from all products regardless of profile scope. |

**Outgoing navigation:** (self-contained detail page; accessed from Health, Planning, and Home quick action)

---

## 3. Navigation Map Summary Table

| Page | Route | Entry Points | Key Actions | Outgoing Links |
|---|---|---|---|---|
| Profiles Home | `/profiles` | App start, redirect | Select profile | `/home` |
| Sync Gate | `/sync-gate` | Post-profile-select | Wait/Retry | `/home`, `/profiles` |
| Home | `/home` | Global header, direct | Choose workspace, filter, sync | `/home/health`, `/home/delivery`, `/home/trends`, `/home/planning`, `/workitems`, `/bugs-triage`, `/home/plan-board` |
| Health (Now) | `/home/health` | Home workspace card | Click signal card → queue; open Validation Triage | `/home/validation-triage`, `/home/validation-queue?category=SI\|RR\|RC`, `/home/bugs`, `/home/trends`, `/home/planning` |
| Validation Triage | `/home/validation-triage` | Health workspace Validation Triage button | Open queue per category | `/home/validation-queue?category=SI\|RR\|RC\|EFF`, `/home/health`, `/home` |
| Validation Queue | `/home/validation-queue` | Validation Triage "Open queue" | Start fix session per rule | `/home/validation-fix?category=...&ruleId=...`, `/home/validation-triage`, `/home` |
| Validation Fix Session | `/home/validation-fix` | Validation Queue "Start fix session" | Review items one-by-one, dismiss or skip | `/home/validation-queue?category=...`, `/home` |
| Delivery | `/home/delivery` | Home workspace card, global header | Click delivery view | `/home/delivery/sprint`, `/home/delivery/portfolio`, `/home/health`, `/home/trends`, `/home/planning` |
| Sprint Delivery | `/home/delivery/sprint` | Delivery workspace, Planning workspace | Navigate sprints | `/home/delivery/sprint/activity/{id}`, `/home/delivery`, `/home` |
| Portfolio Delivery | `/home/delivery/portfolio` | Delivery workspace | Select sprint range, view aggregated delivery snapshot | `/home/delivery` |
| Trends (Past) | `/home/trends` | Home workspace card | Click trend signal | `/home/portfolio-progress`, `/home/trends/delivery`, `/home/bugs`, `/home/pull-requests`, `/home/pr-delivery-insights`, `/home/pipelines`, `/home/pipeline-insights`, `/home/delivery`, `/home/health`, `/home/planning` |
| Delivery Trends | `/home/trends/delivery` | Trends workspace | Select sprint range | `/home/trends`, `/home` |
| Planning (Future) | `/home/planning` | Home workspace card | Click planning signal | `/home/delivery/sprint`, `/workitems`, `/home/dependencies`, `/planning/product-roadmaps`, `/release-planning`, `/home/health` |
| Bug Insights | `/home/bugs` | Health signal, Trends chart click | View/filter bugs | `/bugs-triage`, `/home/bugs/detail/{id}`, `/home` |
| Bug Detail | `/home/bugs/detail/{id}` | Bug Insights | Edit severity/tags | `/home/bugs` |
| Bug Triage | `/bugs-triage` | Home quick action, Bug Insights | Triage tags | (self-contained) |
| PR Insights | `/home/pull-requests` | Trends workspace | View metrics | `/home` |
| PR Delivery Insights | `/home/pr-delivery-insights` | Trends workspace | Select team/sprint, view PR classification | `/home/trends`, `/home` |
| Pipeline Insights | `/home/pipeline-insights` | Trends workspace | Select team/sprint, view per-product health | `/home/trends`, `/home` |
| Dependency Overview | `/home/dependencies` | Planning workspace | View dependencies | `/home`, `/dependency-graph` |
| Product Roadmaps | `/planning/product-roadmaps` | Planning workspace | View roadmap lanes, reorder products | `/home`, `/home/planning`, `/home/health`, `/planning/product-roadmaps/{productId}` |
| Product Roadmap Editor | `/planning/product-roadmaps/{productId}` | Product Roadmaps | Add/remove/reorder epics via drag-and-drop or buttons, edit title/description | `/home`, `/planning/product-roadmaps` |
| Plan Board | `/home/plan-board` | Home quick action | View/filter board | `/home` |
| Portfolio Progress Trend | `/home/portfolio-progress` | Trends workspace | Select product, team, sprint range | `/home/trends` |
| Work Item Activity | `/home/delivery/sprint/activity/{id}` | Sprint Delivery drilldown | View activity | `/home/delivery/sprint` |
| Work Item Explorer | `/workitems` | Home "Advanced Tools", Planning signals, Fix Session | Filter, explore, validate (advanced inspection) | (self-contained) |

---

## 4. Ten Suggestions for Navigation Improvement

The following suggestions are derived from analysing the current navigation structure, the open decision backlog (`navigation_decision_backlog.md`), and the follow-up actions log (`navigation_followup_actions.md`).

---

### Suggestion 1 — Promote the "Return to workspace" breadcrumb consistently

**Current situation:** Some pages show breadcrumbs (`Home › Health (Now) › ...`), but several leaf pages (Bug Triage, Plan Board, PR Insights, Pipeline Trend) do not. The user must rely on the global "Home" button or the browser back button.

**Suggestion:** Add a consistent breadcrumb trail to every page that reflects the path by which it was reached. This gives the user a mental model of where they are and allows them to step back one level without losing context. Breadcrumbs should carry product/team context forward.

**Implemented:** Added `Home › Bug Triage` breadcrumb and a Home button to the Bug Triage toolbar (`/bugs-triage`). All other home-workspace pages already had breadcrumbs.

---

### Suggestion 2 — Expose cross-workspace navigation on all leaf pages

**Current situation:** Health (Now), Trends (Past), and Planning (Future) each have a "Navigate to Other Workspaces" section, but leaf pages (Bug Insights, Sprint Trend, Work Item Explorer) do not. The user must navigate back to a workspace to switch.

**Suggestion:** Add a compact cross-workspace navigation bar (or persistent sidebar strip) showing Health / Trends / Planning entry points on all pages. This removes unnecessary back-and-forth navigation and mirrors the three-workspace mental model throughout the entire session.

---

### Suggestion 3 — Replace "Quick Actions" section on Home with workspace-scoped entry

**Current situation:** Home has two distinct navigation patterns side-by-side: workspace cards (Health/Trends/Planning) and quick-action buttons (Work Item Explorer, Bug Triage, Plan Board). These represent different mental models (temporal vs. task-based) and compete for the user's attention without a clear hierarchy.

**Suggestion:** Move the quick-action buttons inside the relevant workspaces instead of displaying them redundantly on Home. For example, "Bug Triage" belongs inside the Health workspace as a primary action, and "Plan Board" belongs inside Planning. This gives every entry point a clear *why*.

---

### Suggestion 4 — Resolve the scope-widening problem (Decision #11)

**Current situation:** Once a product context filter is applied on Home, all workspaces inherit it. However, there is no way to widen the scope from within a workspace — the user must return to Home and clear the filter. This is an unnecessary interruption.

**Suggestion:** Add a small "All Products" toggle or a product context chip with a clear ("×") button on every workspace header. Clicking it resets the product filter for that workspace visit without requiring a full round-trip to Home.

---

### Suggestion 5 — Make the Dependency Overview a first-class entry point in Planning

**Current situation:** Dependency Overview (`/home/dependencies`) is currently marked "Read-only" and offered as a secondary card in Planning workspace. The read-only notice points users to a separate `/dependency-graph` page for management. This creates a confusing two-page split.

**Suggestion:** Merge the read-only dependency overview and the full dependency management into a single contextual view, toggled by an "Edit Dependencies" mode switch. Alternatively, replace the read-only card with a direct link to the full `/dependency-graph`, eliminating the intermediate read-only page entirely.

---

### Suggestion 6 — Surface the Bug Triage page from the Bug Insights page more prominently

**Current situation:** Bug Insights (`/home/bugs`) has a "Bug Triage" button in the header, but it is visually equivalent to the "Home" button. A product owner visiting Bug Insights to understand bug state will often want to immediately triage, but the call-to-action is easy to miss.

**Suggestion:** Add a contextual action panel at the bottom of Bug Insights with a prominent "Go to Triage" button, ideally indicating how many bugs are still untriaged. This creates a natural progression from insight to action.

---

### Suggestion 7 — Add a "What changed since last sync?" entry point on Home

**Current situation:** Home shows a health snapshot (validation issues, active bugs, total items) and a "Sync Now" button, but there is no way to quickly see *what changed* since the previous sync. The user must inspect individual workspaces or the Work Item Explorer manually.

**Suggestion:** Add a "What's New Since Last Sync" section to Home (or a dedicated `/home/changes` route) that shows: newly introduced validation issues, bugs opened or closed, and sprint completions since the last successful sync. This gives the PO immediate situational awareness after opening the application.

---

### Suggestion 8 — Give Sprint Trend a clearer path back to its team/product context

**Current situation:** Sprint Trend (`/home/sprint-trend`) is accessed from the Trends workspace, where the user may have selected a team and sprint range. After drilling into Work Item Activity (`/home/sprint-trend/activity/{id}`) and returning via "Back to Sprint Trend", the team and sprint context may not be preserved in the URL.

**Suggestion:** Ensure all sprint/team/product filter selections are consistently encoded in the URL query string on every Sprint Trend page and propagated to the back-navigation link. This prevents losing the selected context on drill-down/drill-up.

---

### Suggestion 9 — Introduce a "Focus Mode" for Health: one validation category at a time

**Current situation:** The Health workspace shows all three validation signal categories (Structural Integrity, Refinement Readiness, Refinement Completeness) simultaneously and immediately forwards to the Work Item Explorer with a filter. Once in the explorer, navigating to a second category requires returning to Health and clicking again.

**Suggestion:** Add a persistent validation category filter chip strip at the top of the Work Item Explorer when it is entered from a Health signal. This lets the user switch between SI / RR / RC work directly within the explorer without round-tripping through the Health workspace, enabling a focused review session.

---

### Suggestion 10 — Resolve the Beta Navigation promotion path (Decision #1)

**Current situation:** The current navigation (`/home` and its workspaces) is the production navigation, but the legacy intent-based navigation (`/legacy`) is still accessible via a subtle footer link. This creates two parallel navigation systems. The legacy system serves no active use case in the current implementation.

**Suggestion:** Define and implement a promotion plan for the current navigation as the sole navigation model. Remove the `/legacy` entry point from the Home page footer. This reduces confusion, eliminates dead code, and presents a single coherent user experience to the product owner.

---

*End of Navigation Map*
