# Exploratory Test Plan — PoCompanion

## Overview

This document provides a comprehensive test plan for exploratory testing of the PoCompanion application. The goal is to verify that all major features work correctly when the application is running live with mock data.

## Test Environment

### Prerequisites
- .NET 10.0 SDK installed
- Windows, macOS, or Linux operating system
- Web browser (Chrome, Edge, Firefox, or Safari)
- Mock TFS client enabled in `appsettings.Development.json`

### Environment Setup

1. **Verify Mock Data Mode**
   - Open `PoTool.Api/appsettings.Development.json`
   - Ensure `TfsIntegration.UseMockClient` is set to `true`
   ```json
   {
     "TfsIntegration": {
       "UseMockClient": true
     }
   }
   ```

2. **Start the Application**
   - **Option A (Automated):** Use the provided startup script
     - Windows: `.\start-exploratory-testing.ps1`
     - Linux/Mac: `./start-exploratory-testing.sh`
   
   - **Option B (Manual):**
     1. Build: `dotnet build PoTool.sln --configuration Release`
     2. Start API: `cd PoTool.Api && dotnet run --configuration Release --urls http://localhost:5000`
     3. Start Client (new terminal): `cd PoTool.Client && dotnet run --configuration Release`
     4. Open browser to `http://localhost:5001`

3. **Verify Application Started**
   - API health endpoint: `http://localhost:5000/health` should return 200 OK
   - Client loads: `http://localhost:5001` should display the home page

## Feature Test Scenarios

### 1. Home Page
**Route:** `/` (root)  
**Purpose:** Landing page with navigation cards to all features

#### Test Steps
1. Navigate to `http://localhost:5001`
2. Verify page loads without errors
3. Check navigation card layout
4. Verify all feature cards are visible
5. Test navigation by clicking each card

#### Expected Results
- ✅ Page renders with no console errors
- ✅ All feature cards display with icons and descriptions
- ✅ Dark mode theme is applied (if default)
- ✅ Navigation cards are clickable and route correctly
- ✅ Layout is responsive and centered

#### Screenshots to Capture
- `01-home-page.png` - Full home page view
- `01-home-page-cards.png` - Close-up of navigation cards

---

### 2. TFS Configuration Page
**Route:** `/tfsconfig`  
**Purpose:** Configure TFS/Azure DevOps connection settings

#### Test Steps
1. Navigate to `/tfsconfig`
2. Verify form renders empty initially
3. Fill in mock TFS data:
   - Organization URL: `https://dev.azure.com/mock-org`
   - Project Name: `MockProject`
   - PAT: `mock-pat-token-12345`
   - Area Path: `MockProject\Area`
4. Verify PAT field is masked (password type)
5. Click "Test Connection" button
6. Click "Save" button
7. Verify saved data persists on page reload

#### Expected Results
- ✅ Form fields render correctly
- ✅ PAT field is password-masked
- ✅ Validation messages show for required fields
- ✅ Test Connection works with mock client (always succeeds)
- ✅ Save button persists configuration
- ✅ Page shows success message after save
- ✅ Configuration loads on page refresh

#### Screenshots to Capture
- `02-tfs-config-empty.png` - Empty configuration form
- `03-tfs-config-filled.png` - Form with mock data entered
- `04-tfs-config-saved.png` - Success message after save
- `05-tfs-config-validation.png` - Validation messages (if applicable)

---

### 3. Work Items Explorer
**Route:** `/workitems` (if implemented)  
**Purpose:** Hierarchical tree view of work items

#### Test Steps
1. Navigate to `/workitems`
2. If page exists, verify initial state
3. Click "Pull & Cache" button to load mock data
4. Verify hierarchical tree displays (Goal → Objective → Epic → Feature → PBI → Task)
5. Test expand/collapse functionality
6. Test search/filter functionality
7. Click on a work item to view details
8. Verify type icons (colored squares) display correctly
9. Check status column shows correct states

#### Expected Results
- ✅ Page loads without errors
- ✅ "Pull & Cache" button loads mock data
- ✅ Tree structure displays correctly with proper hierarchy
- ✅ Expand/collapse icons work
- ✅ Type icons are color-coded by work item type
- ✅ Status column shows current state
- ✅ Detail panel shows selected work item info
- ✅ Search/filter narrows visible items

#### Screenshots to Capture
- `06-workitems-initial.png` - Initial empty state
- `07-workitems-loaded.png` - After loading mock data
- `08-workitems-expanded.png` - Hierarchical tree fully expanded
- `09-workitems-detail.png` - Selected work item with detail panel
- `10-workitems-search.png` - Search/filter in action

**Note:** If Work Items Explorer is not yet implemented, skip this section and note it in test results.

---

### 4. Backlog Health
**Route:** `/backlog-health`  
**Purpose:** Display health metrics and validation warnings for backlog items

#### Test Steps
1. Navigate to `/backlog-health`
2. Verify health metrics dashboard loads
3. Check that cards render with data:
   - Total items
   - Validation warnings
   - Health score
4. Test filter controls (if present)
5. Verify any charts/graphs display properly
6. Check for validation warnings/errors

#### Expected Results
- ✅ Dashboard loads with mock data
- ✅ Health metric cards show correct values
- ✅ Validation warnings display (if any in mock data)
- ✅ Charts render without errors
- ✅ Filters work correctly
- ✅ No console errors

#### Screenshots to Capture
- `11-backlog-health-dashboard.png` - Main dashboard view
- `12-backlog-health-warnings.png` - Validation warnings (if displayed)
- `13-backlog-health-filters.png` - Filter controls in use

---

### 5. Effort Distribution
**Route:** `/effort-distribution`  
**Purpose:** Heat map visualization of effort across work items

#### Test Steps
1. Navigate to `/effort-distribution`
2. Verify heat map loads with mock data
3. Test filter controls
4. Check analytics tabs:
   - Imbalance
   - Trends
   - Concentration
5. Verify data loads and displays correctly
6. Test interactive controls

#### Expected Results
- ✅ Heat map renders with mock data
- ✅ Color gradients indicate effort levels
- ✅ Filters adjust displayed data
- ✅ Analytics tabs switch correctly
- ✅ Interactive elements respond to clicks
- ✅ No rendering errors

#### Screenshots to Capture
- `14-effort-distribution-heatmap.png` - Main heat map view
- `15-effort-distribution-filters.png` - Filter controls
- `16-effort-distribution-imbalance.png` - Imbalance tab
- `17-effort-distribution-trends.png` - Trends tab
- `18-effort-distribution-concentration.png` - Concentration tab

---

### 6. PR Insights
**Route:** `/pr-insights`  
**Purpose:** Pull request metrics and analytics dashboard

#### Test Steps
1. Navigate to `/pr-insights`
2. Verify PR metrics dashboard loads
3. Check graphs/charts showing PR data:
   - PR status distribution
   - Time to merge
   - PR activity over time
4. Test filtering options
5. Verify mock PR data displays correctly
6. Check that metrics are calculated properly

#### Expected Results
- ✅ Dashboard loads with mock PR data
- ✅ Charts render correctly (status, time, activity)
- ✅ Metrics show accurate calculations
- ✅ Filters adjust displayed data
- ✅ All graphs are interactive
- ✅ No data loading errors

#### Screenshots to Capture
- `19-pr-insights-dashboard.png` - Main PR dashboard
- `20-pr-insights-status-chart.png` - PR status chart
- `21-pr-insights-time-chart.png` - Time to merge chart
- `22-pr-insights-filters.png` - Filtering options in use

---

### 7. State Timeline
**Route:** `/state-timeline`  
**Purpose:** Visualize work item state transitions over time

#### Test Steps
1. Navigate to `/state-timeline`
2. Enter a work item ID (use mock ID: 1004, 1005, etc.)
3. Click "View Timeline"
4. Verify timeline visualization renders
5. Check time-in-state calculations display
6. Look for bottleneck indicators

#### Expected Results
- ✅ Input form renders correctly
- ✅ Timeline loads for mock work item ID
- ✅ State transitions are visualized chronologically
- ✅ Time-in-state durations are calculated
- ✅ Bottleneck warnings show (if applicable)
- ✅ Chart is readable and properly formatted

#### Screenshots to Capture
- `23-state-timeline-input.png` - Work item ID input form
- `24-state-timeline-visualization.png` - Timeline for a work item
- `25-state-timeline-bottleneck.png` - Bottleneck indicators (if shown)

---

### 8. Epic Forecast
**Route:** `/epic-forecast`  
**Purpose:** Forecast completion dates for epics based on velocity

#### Test Steps
1. Navigate to `/epic-forecast`
2. Enter an epic ID (use mock ID: 1002, 1101, etc.)
3. Submit the form
4. Verify forecast results display:
   - Velocity charts
   - Projected completion date
   - Confidence levels
   - Best/worst case scenarios
5. Check that charts render properly

#### Expected Results
- ✅ Input form renders correctly
- ✅ Forecast calculation runs for mock epic
- ✅ Velocity chart displays historical velocity
- ✅ Projected dates are shown
- ✅ Confidence intervals are calculated
- ✅ Scenarios (optimistic/pessimistic) are displayed
- ✅ Charts render without errors

#### Screenshots to Capture
- `26-epic-forecast-input.png` - Epic ID input form
- `27-epic-forecast-results.png` - Forecast results with velocity chart
- `28-epic-forecast-confidence.png` - Confidence levels and scenarios

---

### 9. Dependency Graph
**Route:** `/dependency-graph`  
**Purpose:** Visualize dependencies between work items

#### Test Steps
1. Navigate to `/dependency-graph`
2. Verify dependency visualization loads
3. Test filter controls
4. Check critical paths are highlighted
5. Verify node and link data displays correctly
6. Look for circular dependency warnings (if any)

#### Expected Results
- ✅ Graph renders with mock dependency data
- ✅ Nodes represent work items
- ✅ Links show dependencies
- ✅ Critical paths are highlighted (if applicable)
- ✅ Filters adjust displayed graph
- ✅ Circular dependencies are flagged (if any in mock data)
- ✅ Graph is interactive (zoom, pan)

#### Screenshots to Capture
- `29-dependency-graph-full.png` - Full dependency graph
- `30-dependency-graph-filtered.png` - Graph with filters applied
- `31-dependency-graph-critical.png` - Critical path highlighted

---

### 10. Velocity Dashboard
**Route:** `/velocity-dashboard`  
**Purpose:** Display team velocity trends and sprint metrics

#### Test Steps
1. Navigate to `/velocity-dashboard`
2. Verify velocity charts load with mock data
3. Check sprint-by-sprint velocity display
4. Test date range filters (if present)
5. Verify trend lines and averages display
6. Check that data is consistent with mock sprints

#### Expected Results
- ✅ Dashboard loads with mock velocity data
- ✅ Chart shows velocity per sprint
- ✅ Trend lines are calculated correctly
- ✅ Average velocity is displayed
- ✅ Filters adjust time range
- ✅ Data matches mock sprint data (12 sprints)

#### Screenshots to Capture
- `32-velocity-dashboard.png` - Main velocity dashboard
- `33-velocity-chart.png` - Velocity chart with trends
- `34-velocity-filters.png` - Filter controls (if present)

---

### 11. Settings Modal
**Route:** Accessible from navigation or settings button  
**Purpose:** Application settings and configuration

#### Test Steps
1. Open settings dialog (look for settings button/icon)
2. Verify settings modal renders
3. Check available settings:
   - Profile configuration
   - Data mode selection (Mock vs TFS)
   - Theme settings (if available)
4. Make changes to settings
5. Save settings
6. Verify changes persist

#### Expected Results
- ✅ Settings modal opens correctly
- ✅ All settings options are visible
- ✅ Profile fields can be edited
- ✅ Data mode can be toggled (Mock/TFS)
- ✅ Save button persists changes
- ✅ Settings load on next app launch

#### Screenshots to Capture
- `35-settings-modal.png` - Settings dialog opened
- `36-settings-profile.png` - Profile configuration section
- `37-settings-data-mode.png` - Data mode selection

---

### 12. Help/Documentation
**Route:** `/help` (if implemented)  
**Purpose:** User documentation and help content

#### Test Steps
1. Navigate to `/help` or click help button
2. Verify documentation page loads
3. Check that content is readable
4. Test navigation within help content (if multi-section)
5. Verify links and examples work

#### Expected Results
- ✅ Help page loads without errors
- ✅ Content is well-formatted and readable
- ✅ Navigation works (if multi-page)
- ✅ Examples and links are functional
- ✅ Search/filter works (if available)

#### Screenshots to Capture
- `38-help-page.png` - Main help/documentation page
- `39-help-content.png` - Sample help content section

---

## General Testing Guidelines

### What to Look For

1. **UI Rendering**
   - All components render without layout issues
   - Colors and typography are consistent
   - Icons and images load correctly
   - Responsive layout adapts to window size

2. **Functionality**
   - Buttons and controls are clickable and responsive
   - Forms submit and validate correctly
   - Navigation between pages works
   - Mock data loads and displays properly

3. **Performance**
   - Pages load within reasonable time (< 3 seconds)
   - Interactions are smooth and responsive
   - No lag or freezing

4. **Errors**
   - No console errors (F12 developer tools)
   - No API errors in network tab
   - No missing resources (404s)
   - No unhandled exceptions

5. **Data Accuracy**
   - Mock data displays correctly
   - Calculations are accurate
   - Charts reflect the data properly
   - Filters and searches work as expected

### Screenshot Best Practices

1. **Capture Full Context**
   - Include browser chrome (address bar) to show URL
   - Capture entire page or relevant section
   - Use consistent window size (1920x1080 recommended)

2. **Highlight Important Elements**
   - Use browser zoom if needed for clarity
   - Capture both overview and detail shots
   - Include hover states if relevant

3. **Naming Convention**
   - Use sequential numbers: `01-`, `02-`, etc.
   - Use descriptive names: `home-page`, `tfs-config-filled`
   - Keep filenames concise but clear

4. **Format**
   - PNG format preferred for quality
   - Compress if file size > 2MB
   - Store in `docs/screenshots/` directory

### Documentation During Testing

As you test, document:

1. **Feature Status:** Pass/Fail for each test scenario
2. **Issues Found:** Any bugs, glitches, or unexpected behavior
3. **Performance Notes:** Slow loading, lag, or freezing
4. **Missing Features:** Features in the plan but not implemented
5. **Suggestions:** Ideas for improvement or better UX

Record findings in `docs/TEST_RESULTS.md` as you go.

---

## Test Execution Checklist

Use this checklist to track progress:

- [ ] Environment setup complete
- [ ] API started successfully
- [ ] Client started successfully
- [ ] Home page tested and captured
- [ ] TFS Config tested and captured
- [ ] Work Items Explorer tested (or noted as not implemented)
- [ ] Backlog Health tested and captured
- [ ] Effort Distribution tested and captured
- [ ] PR Insights tested and captured
- [ ] State Timeline tested and captured
- [ ] Epic Forecast tested and captured
- [ ] Dependency Graph tested and captured
- [ ] Velocity Dashboard tested and captured
- [ ] Settings tested and captured
- [ ] Help page tested (if available)
- [ ] All screenshots organized in `docs/screenshots/`
- [ ] Screenshot index created (`docs/screenshots/README.md`)
- [ ] Test results documented in `docs/TEST_RESULTS.md`

---

## Troubleshooting

### API Won't Start
- Check port 5000 is not in use: `lsof -i :5000` (Mac/Linux) or `netstat -ano | findstr :5000` (Windows)
- Verify `appsettings.Development.json` is valid JSON
- Check `api.log` for error messages
- Ensure .NET 10.0 SDK is installed

### Client Won't Start
- Check port 5001 is not in use
- Verify API is running and healthy
- Clear browser cache
- Check browser console for errors

### Mock Data Not Loading
- Verify `UseMockClient` is `true` in appsettings
- Restart API after changing settings
- Check API logs for TFS client messages
- Ensure MockDataProvider and MockPullRequestDataProvider exist

### Page Not Found (404)
- Verify route is correct
- Check that feature is implemented
- Ensure navigation links are correct
- Review browser console for routing errors

---

## Success Criteria

Testing is successful when:

✅ All implemented features load and render correctly  
✅ Mock data displays properly in all pages  
✅ No critical console or API errors  
✅ Navigation works between all pages  
✅ Forms and interactions respond correctly  
✅ Screenshots captured for all tested features  
✅ Test results documented with findings  
✅ Performance is acceptable (pages load < 3 sec)

---

## Next Steps

After completing exploratory testing:

1. Review all screenshots and organize them
2. Complete `docs/TEST_RESULTS.md` with findings
3. Create screenshot index in `docs/screenshots/README.md`
4. Report any critical bugs found
5. Provide feedback on UX/UI improvements
6. Archive test artifacts for future reference

---

**Document Version:** 1.0  
**Last Updated:** 2024-12-24  
**Author:** PoCompanion Development Team
