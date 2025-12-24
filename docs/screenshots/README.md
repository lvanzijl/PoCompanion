# Screenshot Index — PoCompanion Exploratory Testing

This directory contains screenshots captured during exploratory testing of the PoCompanion application with mock data.

## Test Information

**Test Date:** _To be filled during testing_  
**Application Version:** _Current version_  
**Test Mode:** Mock Data  
**Environment:** _OS and browser info_

## Screenshot Inventory

### Home Page
- `01-home-page.png` - Landing page with navigation cards
- `01-home-page-cards.png` - Close-up of feature navigation cards

### TFS Configuration
- `02-tfs-config-empty.png` - Empty TFS configuration form
- `03-tfs-config-filled.png` - Form filled with mock data
- `04-tfs-config-saved.png` - Success message after saving
- `05-tfs-config-validation.png` - Validation messages (if applicable)

### Work Items Explorer
- `06-workitems-initial.png` - Initial empty state
- `07-workitems-loaded.png` - After loading mock data
- `08-workitems-expanded.png` - Hierarchical tree fully expanded
- `09-workitems-detail.png` - Selected work item with detail panel
- `10-workitems-search.png` - Search/filter functionality

**Note:** If Work Items Explorer is not implemented, these screenshots will be skipped.

### Backlog Health
- `11-backlog-health-dashboard.png` - Main dashboard with health metrics
- `12-backlog-health-warnings.png` - Validation warnings displayed
- `13-backlog-health-filters.png` - Filter controls in use

### Effort Distribution
- `14-effort-distribution-heatmap.png` - Main heat map visualization
- `15-effort-distribution-filters.png` - Filter controls
- `16-effort-distribution-imbalance.png` - Imbalance analytics tab
- `17-effort-distribution-trends.png` - Trends analytics tab
- `18-effort-distribution-concentration.png` - Concentration analytics tab

### PR Insights
- `19-pr-insights-dashboard.png` - Main PR metrics dashboard
- `20-pr-insights-status-chart.png` - PR status distribution chart
- `21-pr-insights-time-chart.png` - Time to merge chart
- `22-pr-insights-filters.png` - Filtering options in use

### State Timeline
- `23-state-timeline-input.png` - Work item ID input form
- `24-state-timeline-visualization.png` - Timeline visualization for a work item
- `25-state-timeline-bottleneck.png` - Bottleneck indicators (if shown)

### Epic Forecast
- `26-epic-forecast-input.png` - Epic ID input form
- `27-epic-forecast-results.png` - Forecast results with velocity chart
- `28-epic-forecast-confidence.png` - Confidence levels and scenarios

### Dependency Graph
- `29-dependency-graph-full.png` - Full dependency graph view
- `30-dependency-graph-filtered.png` - Graph with filters applied
- `31-dependency-graph-critical.png` - Critical path highlighted

### Velocity Dashboard
- `32-velocity-dashboard.png` - Main velocity dashboard
- `33-velocity-chart.png` - Velocity chart with trend lines
- `34-velocity-filters.png` - Filter controls (if present)

### Settings
- `35-settings-modal.png` - Settings dialog opened
- `36-settings-profile.png` - Profile configuration section
- `37-settings-data-mode.png` - Data mode selection (Mock/TFS)

### Help/Documentation
- `38-help-page.png` - Main help/documentation page
- `39-help-content.png` - Sample help content section

**Note:** If Help page is not implemented, these screenshots will be skipped.

## Screenshot Naming Convention

Screenshots follow this naming pattern:
```
[sequence]-[feature]-[description].png
```

**Examples:**
- `01-home-page.png` - First screenshot, home page feature
- `14-effort-distribution-heatmap.png` - 14th screenshot, effort distribution feature, heatmap view

## Viewing Screenshots

To view all screenshots:
1. Open this directory in your file explorer
2. Use an image viewer that supports quick navigation (e.g., Windows Photos, macOS Preview)
3. Or open in a web browser for easy navigation

To reference in documentation:
```markdown
![Feature Name](screenshots/01-home-page.png)
```

## Screenshot Guidelines

**Resolution:** 1920x1080 recommended for consistency  
**Format:** PNG for quality  
**Size:** Compress if > 2MB  
**Content:** Include browser chrome to show URL context

## Test Coverage

This screenshot collection provides visual proof that:
- ✅ All major features render correctly
- ✅ Mock data displays as expected
- ✅ Navigation and interactions work
- ✅ UI components are properly styled
- ✅ Charts and visualizations render

## Related Documents

- [Exploratory Test Plan](../EXPLORATORY_TEST_PLAN.md) - Complete test plan and procedures
- [Test Results](../TEST_RESULTS.md) - Detailed test execution results
- [Startup Scripts](../../start-exploratory-testing.ps1) - Scripts to launch the application

## Notes

Screenshots are captured during manual exploratory testing. They represent the state of the application at the time of testing with mock data enabled.

For automated screenshot capture in the future, consider using:
- Playwright for browser automation
- Selenium for cross-browser testing
- Puppeteer for Chromium-based browsers

---

**Last Updated:** _To be filled during testing_  
**Total Screenshots:** _To be counted after testing_  
**Test Status:** ⏳ Pending / ✅ Complete
