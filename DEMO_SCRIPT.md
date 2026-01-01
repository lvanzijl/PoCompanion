# PO Companion Demo Script

## Overview
This demo script provides a comprehensive walkthrough of all features in PO Companion using the expanded mock data. Each section includes the purpose, required data, and step-by-step instructions for demonstrating the features.

---

## Prerequisites
1. Application configured in Mock Data mode (Settings → Data Mode → Mock)
2. No specific goals selected in settings (to show all mock data)
3. Browser at http://localhost:5050 (or configured port)

---

## Quick Reference: All Views

| View | Route | Primary Purpose | Mock Data Used |
|------|-------|----------------|----------------|
| Home | `/` | Navigation hub | N/A |
| Work Items Explorer | `/workitems` | Hierarchical work item management | 2,050 work items |
| Velocity Dashboard | `/velocity` | Sprint velocity tracking | 20 sprints |
| Backlog Health | `/backlog-health` | Data quality monitoring | All work items |
| PR Insights | `/pr-insights` | Pull request analytics | 300+ PRs |
| Effort Distribution | `/effort-distribution` | Capacity planning | All work items |
| State Timeline | `/state-timeline` | Work item lifecycle | Individual items |
| Epic Forecast | `/epic-forecast` | Completion forecasting | Epics/Features |
| Dependency Graph | `/dependency-graph` | Dependency visualization | Work item links |
| TFS Config | `/tfsconfig` | Azure DevOps setup | N/A |
| Help | `/help` | Documentation | N/A |

---

## Demo Flow (20-30 minutes)

### 1. Home Page (2 minutes)
**Route:** `/`

**Purpose:** Introduce the application and its main features

**Script:**
> "Welcome to PO Companion! This is your command center for Azure DevOps work item management. From here, you can access all major features including work item exploration, pull request analytics, velocity tracking, and more. The expanded mock data includes 50 goals across 10 domains, providing a realistic environment for testing."

**Demo Actions:**
- Point out feature cards
- Mention the 10 domains (Product, Platform, Data, Mobile, Web, AI, Compliance, Operations, Quality, Architecture)
- Click "Go to Work Items" to proceed

---

### 2. Work Items Explorer (5 minutes)
**Route:** `/workitems`

**Mock Data:** 50 Goals, 100 Objectives, 200 Epics, 300 Features, ~200 PBIs, ~200 Bugs, ~1000 Tasks

**Purpose:** Demonstrate hierarchical work item management with enhanced visual representation

**Script:**
> "The Work Items Explorer shows all your work items in a hierarchical tree. Notice the improved visual design with MudBlazor components - colored chips, semantic icons, and smooth animations. With 50 goals across 10 domains, you can see realistic hierarchies from Goals down to Tasks."

**Demo Actions:**
1. **Show TreeView Enhancements**
   - Point out colored chips (Green=Goal, Orange=Epic, Red=Bug, etc.)
   - Show icons (Flag for Goal, Layers for Epic, BugReport for Bug)
   - Expand/collapse a few nodes to show animations
   - Hover over items to show hover effects

2. **Demonstrate Filtering**
   - Type "Product" in filter box → shows Product domain items
   - Clear and type "Performance" → shows Performance-related items
   - Clear filter

3. **Show Validation Features**
   - Toggle "Parent Progress Issues" filter
   - Toggle "Missing Effort Issues" filter
   - Point out validation icons (⚠️) on items
   - Hover over validation icon to show tooltip

4. **Selection Features**
   - Single-click a work item → detail panel appears
   - Ctrl+Click multiple items → multi-select
   - Click "Select All" button → all visible items selected
   - Show selection count
   - Click "Clear Selection"

**Key IDs to Try:** 1000 (Goal), 1002 (Epic), 1003 (Feature), 1004 (PBI)

---

### 3. Velocity Dashboard (4 minutes)
**Route:** `/velocity`

**Mock Data:** 20 sprints with varying velocity patterns

**Purpose:** Track team velocity and sprint metrics

**Script:**
> "The Velocity Dashboard shows your team's completed story points per sprint. Our mock data spans 20 sprints with realistic patterns - Sprint 1 has lower velocity during ramp-up, Sprint 3 shows peak velocity, and Sprint 5 has a dip when a team member was out. This historical data helps forecast future capacity."

**Demo Actions:**
1. **Summary Cards**
   - Point out Average Velocity
   - Show Last 3 Sprints average
   - Highlight highest/lowest velocity

2. **Velocity Chart**
   - Show line chart with trend
   - Point out variations in sprint velocity
   - Explain how this helps with forecasting

3. **Sprint Details Table**
   - Scroll through sprint data
   - Show breakdown by PBIs, Bugs, Tasks
   - Point out completed story points per sprint

4. **Forecasting Impact**
   - Explain how consistent velocity improves forecasting accuracy
   - Show standard deviation indicator

---

### 4. Backlog Health Dashboard (4 minutes)
**Route:** `/backlog-health`

**Mock Data:** Multi-iteration health analysis

**Purpose:** Monitor validation issues and backlog quality

**Script:**
> "Backlog Health monitors data quality across your iterations. With our expanded mock data, you can see health scores, validation issues, and effort estimation coverage across multiple sprints. The trend indicators help you track whether backlog quality is improving or needs attention."

**Demo Actions:**
1. **Trend Summary**
   - Show overall health trend
   - Point out total work items and issues
   - Explain effort and validation trends

2. **Iteration Health Table**
   - Show health scores per iteration
   - Point out work item counts
   - Highlight issue breakdowns
   - Show effort coverage percentages

3. **Filters**
   - Adjust max iterations slider
   - Try area path filter (e.g., "Product")
   - Show how data updates

4. **Charts** (if available)
   - Health score over time
   - Issue distribution
   - Effort coverage trends

---

### 5. PR Insights Dashboard (4 minutes)
**Route:** `/pr-insights`

**Mock Data:** 300+ pull requests across 20 sprints

**Purpose:** Analyze pull request metrics and identify bottlenecks

**Script:**
> "PR Insights helps identify bottlenecks in your code review process. Our mock data includes PRs across 20 sprints with varying patterns. You can see which PRs are taking too long, who your top contributors are, and track metrics like average time to merge."

**Demo Actions:**
1. **Summary Metrics**
   - Total PRs
   - Average time open
   - Average iterations
   - Average file changes

2. **Charts**
   - Status distribution (Completed/Active/Abandoned)
   - Time open distribution histogram
   - Top contributors chart

3. **Date Range Filter**
   - Try "Last 30 Days"
   - Try "Last 90 Days"
   - Show custom date range option

4. **Insights**
   - Identify PRs open too long
   - See most active contributors
   - Spot review bottlenecks

---

### 6. Effort Distribution Heat Map (3 minutes)
**Route:** `/effort-distribution`

**Mock Data:** Effort across 10 domains and 20 sprints

**Purpose:** Visual capacity planning

**Script:**
> "The Effort Distribution Heat Map helps with capacity planning. Each cell shows effort for a specific domain and sprint. Green indicates healthy capacity (75-85%), yellow is caution, and red indicates problems - either over-allocation or under-utilization."

**Demo Actions:**
1. **Summary Cards**
   - Total effort
   - Number of area paths
   - Number of iterations

2. **Heat Map**
   - Explain color coding:
     - Green: 75-85% capacity (ideal)
     - Yellow: 50-75% or 85-100% (caution)
     - Red: <50% or >100% (concern)
   - Point out over-allocated areas
   - Identify under-utilized capacity

3. **Capacity Planning Use**
   - Balance workload across teams
   - Spot potential burnout risks
   - Find available capacity

---

### 7. State Timeline (3 minutes)
**Route:** `/state-timeline`

**Purpose:** Analyze individual work item lifecycle

**Script:**
> "State Timeline shows the lifecycle of individual work items. Enter a work item ID to see how it progressed through states. You'll see cycle time (active work time) vs lead time (total time), and can identify bottlenecks where work gets stuck."

**Demo Actions:**
1. **Select Work Item**
   - Enter ID: 1004 (or any from 1000-3000 range)
   - Click "Analyze"

2. **View Timeline**
   - Show state transitions
   - Point out time in each state
   - Highlight longest durations (bottlenecks)

3. **Explain Metrics**
   - Lead time: Created → Done (total time)
   - Cycle time: In Progress → Done (active work)
   - Compare with team averages

**Key IDs to Try:** 1004, 1010, 1021, 2004, 2010

---

### 8. Epic/Feature Completion Forecast (3 minutes)
**Route:** `/epic-forecast`

**Purpose:** Forecast when epics/features will complete

**Script:**
> "Epic Forecast predicts completion dates based on your historical velocity. Select an epic, and the system calculates best, likely, and worst-case scenarios. This uses actual team velocity data to give realistic estimates."

**Demo Actions:**
1. **Select Epic**
   - Enter Epic ID: 1002
   - Set Historical Sprints: 5
   - Click "Forecast"

2. **View Results**
   - Best case scenario
   - Likely case scenario
   - Worst case scenario
   - Remaining effort
   - Velocity calculation basis

3. **Progress Visualization**
   - Completed vs remaining bar chart
   - Burn-down projection

4. **Reliability Indicators**
   - Velocity standard deviation
   - Confidence level
   - Scope stability notes

**Key Epic IDs to Try:** 1002, 1020, 1101, 1102, 1120, 2002, 2020, 2101, 3002

---

### 9. Dependency Chain Visualization (3 minutes)
**Route:** `/dependency-graph`

**Purpose:** Visualize work item dependencies

**Script:**
> "Dependency Chain Visualization shows how work items relate to each other. Filter by area path or specific IDs to see dependency graphs. This helps identify critical paths and potential bottlenecks in your release planning."

**Demo Actions:**
1. **Apply Filters**
   - Area Path: "Product"
   - Work Item Types: "Epic,Feature"
   - Max Depth: 3
   - Click "Load Graph"

2. **View Graph**
   - Nodes = work items
   - Edges = dependencies
   - Colors = work item types
   - Size may indicate complexity

3. **Identify Patterns**
   - Critical paths
   - Blocking relationships
   - Dependency chains

4. **Use Cases**
   - Release planning
   - Risk identification
   - Work sequencing

---

### 10. TFS Configuration (2 minutes)
**Route:** `/tfsconfig`

**Purpose:** Configure Azure DevOps connection

**Script:**
> "For this demo, we're using Mock data mode, which is perfect for testing and learning. In production, you'd configure your Azure DevOps connection here with your organization URL, project name, and Personal Access Token. The PAT is stored securely on your device, never on the server."

**Demo Actions:**
1. **Point Out Mock Mode**
   - Explain we're in demo/test mode
   - Show where real credentials would go

2. **Explain Real Mode** (don't demonstrate)
   - Organization URL field
   - Project Name field
   - PAT authentication
   - Test connection button

3. **Security Notes**
   - PAT stored locally, not on server
   - Secure storage mechanism
   - Can switch between modes

---

### 11. Settings (1 minute)
**Access:** Via ⚙️ button in Work Items Explorer

**Purpose:** Configure application preferences

**Script:**
> "Settings let you choose between Mock and Real data, and select which goals to track. For focused work, select specific goals. For this demo, we've left all goals visible to show the full hierarchy."

**Demo Actions:**
1. **Data Mode**
   - Show Mock vs Real toggle
   - Explain switching clears state

2. **Goal Selection**
   - Show goal dropdown
   - Explain filtering by goals
   - Note: All visible when none selected

3. **Save/Cancel**
   - Changes persist across sessions

---

## Mock Data Summary

### Work Items (Total: ~2,050)
| Type | Count | Description |
|------|-------|-------------|
| Goals | 50 | Across 10 domains |
| Objectives | 100 | 2 per goal |
| Epics | 200 | 2 per objective |
| Features | 300 | ~1.5 per epic |
| PBIs | ~200 | Distributed across features |
| Bugs | ~200 | Distributed across features |
| Tasks | ~1,000 | 2-3 per PBI/Bug |

### Pull Requests (~300-500)
- Across 20 sprints
- Various sizes and complexities
- Multiple authors and reviewers
- Different statuses (completed, active, abandoned)

### Domains (10)
1. Product
2. Platform
3. Data
4. Mobile
5. Web
6. AI
7. Compliance
8. Operations
9. Quality
10. Architecture

### Time Coverage
- **Quarters:** Q1, Q2, Q3, Q4
- **Sprints:** 1-20
- **Historical:** ~330 days of data

---

## Visual Design Highlights

### Color Coding
- **Goal:** Green (#4CAF50)
- **Objective:** Light Green (#8BC34A)
- **Epic:** Orange (#FF9800)
- **Feature:** Purple (#9C27B0)
- **PBI:** Blue (#2196F3)
- **Bug:** Red (#F44336)
- **Task:** Yellow (#FFEB3B)

### MudBlazor Components Used
- MudIcon for semantic icons
- MudChip for colored badges
- MudIconButton for smooth interactions
- MudTooltip for contextual help
- MudPaper for visual grouping
- MudProgressCircular for loading states

---

## Common Demo Scenarios

### Scenario 1: Sprint Planning
1. **Velocity Dashboard** → Review historical capacity
2. **Backlog Health** → Check data quality
3. **Effort Distribution** → See current allocation
4. **Epic Forecast** → Plan commitments

### Scenario 2: Release Planning
1. **Work Items Explorer** → See big picture
2. **Dependency Graph** → Identify critical paths
3. **Epic Forecast** → Estimate timelines
4. **Backlog Health** → Assess readiness

### Scenario 3: Retrospective
1. **Velocity Dashboard** → Sprint performance
2. **PR Insights** → Code review bottlenecks
3. **State Timeline** → Specific item analysis
4. **Backlog Health** → Improvement areas

### Scenario 4: Daily/Weekly Review
1. **Work Items Explorer** → Current state
2. **PR Insights** → PRs needing attention
3. **Backlog Health** → Emerging issues
4. **Velocity Dashboard** → Quick burndown check

---

## Troubleshooting

### No Data Showing
- ✓ Check Settings → Data Mode = "Mock"
- ✓ Ensure no specific goals selected (or select goals)
- ✓ Try page refresh
- ✓ Click "Full Sync" in Work Items Explorer

### Charts Not Loading
- ✓ Mock data should populate immediately
- ✓ Check browser console for errors
- ✓ Verify correct port (default: 5050)

### TreeView Empty
- ✓ Open Settings (⚙️ button)
- ✓ Confirm "Mock (Test Data)" selected
- ✓ Save if changed
- ✓ Click "Full Sync"

---

## Feature-Data Mapping

| Feature | Mock Data | Sample IDs |
|---------|-----------|------------|
| Work Items Explorer | All 2,050 items | 1000-3000+ |
| Velocity Dashboard | 20 sprints | Auto-populated |
| Backlog Health | All work items | Auto-populated |
| PR Insights | 300+ PRs | Auto-populated |
| Effort Distribution | All work items | Auto-populated |
| State Timeline | Individual items | 1004, 1010, 1021, 2004 |
| Epic Forecast | Epics/Features | 1002, 1020, 1101, 2002 |
| Dependency Graph | Hierarchy links | Filter by domain |

---

## Next Steps After Demo

1. **Evaluation:** Continue exploring with mock data
2. **Production:** Configure Azure DevOps in TFS Config
3. **Learning:** Review Help page for best practices
4. **Feedback:** Note features that work well or need improvement

---

## Conclusion

This comprehensive demo script covers all major features of PO Companion using the expanded mock data. Each view has realistic data to showcase its capabilities across 10 domains and 20 sprints. The enhanced visual design with MudBlazor components provides a professional, polished experience.

For additional information, refer to the Help page (`/help`) or the documentation in the repository.
