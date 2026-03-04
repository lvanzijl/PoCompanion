# PO Companion — Navigation Map

**Audience:** Product Owners and stakeholders  
**Scope:** All non-legacy, non-settings pages  
**Purpose:** Human-readable reference of available navigation and functionality; basis for improvement analysis  
**Last Updated:** 2026-03-04 (Delivery workspace introduced; Sprint Trend moved to Delivery as Sprint Delivery)

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
  │       └──► /home/delivery/portfolio  (Portfolio Delivery — placeholder) │
  │                                                                   │
  ├──► /home/trends  (Trends — Past)                                  │
  │       ├──► /home/bugs  (bug trend drilldown)                      │
  │       ├──► /home/pull-requests  (read-only insight)               │
  │       ├──► /home/pipelines  (read-only insight)                   │
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
| Pipeline Trend signal card | Read-only insight about build and deployment health. Click navigates to Pipeline Trend. |
| Portfolio Progress signal card | Represents strategic product-level progress over a sprint range. Click navigates to Portfolio Progress Trend. |
| Bug Trend chart (interactive) | Three-series chart (Total bugs, Fixed bugs, Added bugs) for the selected time range. Clicking a bar navigates to Bug Insights filtered to that period. Hovering highlights the bar. |
| Cross-workspace navigation | Buttons to Backlog Overview, Health (Now), Delivery, and Planning (Future). |
| Home button | Returns to `/home`. |

> **Note:** Sprint Delivery (formerly Sprint Trend) has moved to the Delivery workspace. Velocity and predictability signals (median velocity, P25–P75 band, median predictability) are embedded in Sprint Delivery (calibration panel) and Planning (Capacity Confidence block).

**Outgoing navigation:** `/home/portfolio-progress`, `/home/trends/delivery`, `/home/bugs`, `/home/pull-requests`, `/home/pipelines`, `/home/delivery`, `/home/backlog-overview`, `/home/health`, `/home/planning`, `/home`

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
| Portfolio Delivery signal card | Aggregated delivery view across products (placeholder). |
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
| Available Actions chips | Lists supported end-station actions: Epic Repositioning and Implicit Reprioritization. |
| Cross-workspace navigation | Buttons to Backlog Overview, Health (Now), and Trends (Past). |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/delivery/sprint`, `/workitems` (scoped to epic or all), `/home/dependencies`, `/home/backlog-overview`, `/home/health`, `/home/planning`, `/release-planning`, `/home`

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

**Purpose:** Read-only view of pull request metrics and trends. Insight-only; no editing.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Pull Request Insights`. |
| Product/Team filter | Filters PR metrics by selected product or team. |
| PR status chart | Shows PRs by status (active, completed, abandoned). |
| PR time-open chart | Shows how long PRs remain open before resolution. |
| PR user chart | Shows PR activity per team member. |
| Summary panel | Aggregated totals and averages for the selected context. |
| Date range filter | Limits the displayed PRs to a specific date window. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home`

---

### 2.11 Pipeline Trend — `/home/pipelines`

**Purpose:** PO-facing trend view of CI/CD pipeline health signals, sprint-bucketed. Insight-only; no editing.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Trends (Past) › Pipeline Trend`. |
| Sprint range selector | Team, product, end-sprint, and sprint count selectors. Defaults to last 6 sprints ending at the active/latest sprint. |
| Reliability Trend chart | Success rate % per sprint (higher-is-better). Slope badge included. |
| Time-to-Green Trend chart | Median pipeline duration (h) per sprint — fallback metric (no PR/commit association). Lower-is-better. Slope badge included. |
| Tail Risk Trend chart | P90 pipeline duration (h) per sprint. Null/gap when fewer than 3 runs. Lower-is-better. Slope badge included. |
| Flakiness Trend chart | % of distinct pipelines with both successes and failures in the same sprint. Lower-is-better. Slope badge included. |
| Advanced drill-down panel | Collapsed by default. Contains a per-sprint metrics table. |
| Error/Retry handling | If data cannot be loaded, shows an error message with a Retry button. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/trends`, `/home`

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

**Purpose:** Detailed sprint-by-sprint inspection of planned versus delivered metrics. Shows PBI completion, effort progression, bug counts per sprint, and Feature/Epic progress. In multi-sprint mode, also shows a Calibration panel with velocity distribution and predictability signals. Located in the Delivery workspace.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery › Sprint Delivery`. |
| Sprint navigation arrows | Navigate backwards and forwards one sprint at a time through the sprint history. |
| Advanced mode toggle | Switches from single-sprint view to a multi-sprint trend graph (last N sprints, configurable). |
| Product filter | Filters metrics to a specific product. |
| Team filter | Filters metrics to a specific team. |
| Sprint Delivery metrics | In single-sprint mode: planned PBI count, completed PBI count, effort progression, bug count, and Feature/Epic-level breakdown with a pie chart. |
| Multi-sprint trend graphs | In advanced mode: progression over last N sprints (PBI completion, effort, bug counts) as separate charts. |
| Calibration panel | In advanced mode (multi-sprint): shows median velocity (P50), P25–P75 volatility band, median predictability, and safe planning capacity (P25). Updates when sprint range or product selection changes. |
| Epic/Feature drilldown | Clicking an Epic or Feature ID in the sprint detail table navigates to the Work Item Activity page for that item. |
| Stale data warning | If the calculated sprint metrics are older than the latest data sync, shows a warning with a "Recompute" button to refresh the analysis. |
| Back to Delivery button | Returns to `/home/delivery`. |
| Home button | Returns to `/home`. |

**Outgoing navigation:** `/home/delivery/sprint/activity/{id}`, `/home/delivery`, `/home`

---

### 2.15 Work Item Activity — `/home/delivery/sprint/activity/{workItemId}`

> **Route alias:** `/home/sprint-trend/activity/{workItemId}` (legacy route, kept for backward compatibility)

**Purpose:** Shows the activity history (revision events) for a single work item (Feature or Epic) within the context of Sprint Delivery analysis.

| Functionality | Description |
|---|---|
| Breadcrumb | `Home › Delivery › Sprint Delivery › Work Item Activity`. |
| Work item metadata | Displays type, ID, title, and relevant time period. |
| Activity timeline | Lists revision events (state changes, effort updates, assignment changes) in chronological order. |
| Back to Sprint Delivery button | Returns to the Sprint Delivery page. |

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
| Portfolio Delivery | `/home/delivery/portfolio` | Delivery workspace | Placeholder | `/home/delivery` |
| Trends (Past) | `/home/trends` | Home workspace card | Click trend signal | `/home/portfolio-progress`, `/home/trends/delivery`, `/home/bugs`, `/home/pull-requests`, `/home/pipelines`, `/home/delivery`, `/home/health`, `/home/planning` |
| Delivery Trends | `/home/trends/delivery` | Trends workspace | Select sprint range | `/home/trends`, `/home` |
| Planning (Future) | `/home/planning` | Home workspace card | Click planning signal | `/home/delivery/sprint`, `/workitems`, `/home/dependencies`, `/release-planning`, `/home/health` |
| Bug Insights | `/home/bugs` | Health signal, Trends chart click | View/filter bugs | `/bugs-triage`, `/home/bugs/detail/{id}`, `/home` |
| Bug Detail | `/home/bugs/detail/{id}` | Bug Insights | Edit severity/tags | `/home/bugs` |
| Bug Triage | `/bugs-triage` | Home quick action, Bug Insights | Triage tags | (self-contained) |
| PR Insights | `/home/pull-requests` | Trends workspace | View metrics | `/home` |
| Pipeline Trend | `/home/pipelines` | Trends workspace | View metrics | `/home/trends`, `/home` |
| Dependency Overview | `/home/dependencies` | Planning workspace | View dependencies | `/home`, `/dependency-graph` |
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
