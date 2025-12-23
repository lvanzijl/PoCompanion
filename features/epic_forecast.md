## Feature: Epic/Feature Completion Forecast

### Goal

Provide velocity-based forecasting for Epic and Feature completion using historical sprint data. Enable Product Owners and Scrum Masters to predict when large work items will complete based on actual team velocity, with confidence levels and scenario planning.

This feature must explicitly reference and comply with:

* `docs/UX_PRINCIPLES.md`
* `docs/UI_RULES.md`
* `docs/ARCHITECTURE_RULES.md`
* `docs/PROCESS_RULES.md`

---

## Functional Description

### Overview

The Epic Forecast feature analyzes historical team velocity and remaining work to project completion dates for Epics and Features. It provides:

1. **Velocity calculation** - Team's average delivery rate from recent sprints
2. **Completion forecasting** - Estimated sprint and calendar date for completion
3. **Confidence levels** - High/Medium/Low confidence based on velocity consistency
4. **Scenario planning** - Best case, likely case, and worst case projections
5. **Sprint-by-sprint breakdown** - Burndown forecast showing expected progress per sprint

---

## Value Proposition

### Beyond TFS WebUI

TFS WebUI provides basic burndown charts but lacks:
- **Velocity-based forecasting** - TFS shows only actual progress, not predictive completion dates
- **Confidence analysis** - No indication of forecast reliability based on velocity variance
- **Multi-scenario planning** - No best/likely/worst case projections for risk management
- **Historical velocity trends** - Limited visibility into team delivery patterns
- **Sprint-level projections** - No granular forecast of expected completion per sprint

This feature adds:
- Predictive analytics using Monte Carlo-style velocity sampling
- Confidence scoring based on historical velocity standard deviation
- Risk-aware planning with multiple completion scenarios
- Visual burndown projection charts for stakeholder communication
- Configurable lookback period for velocity calculation

---

## Configuration Requirements

### Prerequisites

1. **Cached Work Items** - Epic/Feature and all child work items must be cached locally
2. **Effort Estimates** - Child work items must have effort/story points assigned
3. **State Data** - Work item states must be available to identify completed work
4. **Iteration Paths** - Historical sprints with iteration paths for velocity calculation
5. **Completed Sprints** - At least 2-3 historical sprints with completed work

### User Configuration

- **Epic/Feature ID** - User enters the work item ID to forecast
- **Historical Sprints** - User selects how many past sprints to use (default: 5, range: 1-20)

No global configuration required; all settings are per-analysis.

---

## Main View Structure and Components

### Access Point

- Navigation menu: **Metrics → Epic Forecast**
- Direct URL: `/epic-forecast` or `/epic-forecast/{epicId}`

### Input Controls

**Epic Selection Panel** (MudPaper with form controls):
- **Epic/Feature ID** - Numeric input field (MudNumericField)
  - Label: "Epic/Feature ID"
  - Helper text: "Enter Epic or Feature work item ID"
  - Validation: Min value = 1
- **Historical Sprints** - Numeric input field (MudNumericField)
  - Label: "Historical Sprints"
  - Helper text: "Sprints to use for velocity calculation"
  - Default: 5
  - Range: 1-20
- **Calculate Button** - Primary action button
  - Enabled only when valid Epic ID entered
  - Icon: Calculate/Analytics

### Summary Cards (4-card grid)

1. **Total Effort** - Sum of all child work item story points
   - Icon: Flag
   - Color: Primary
2. **Completed Effort** - Story points in "Done" state
   - Icon: CheckCircle
   - Color: Success
3. **Remaining Effort** - Story points not yet completed
   - Icon: HourglassEmpty
   - Color: Warning
4. **Sprints Remaining** - Calculated from velocity
   - Icon: CalendarToday
   - Color: Info

### Epic Information Card

Displays:
- **Epic Title** - Full work item title
- **Epic ID** - Work item identifier
- **Type** - Epic, Feature, or other
- **Area Path** - Team/product area
- **Confidence Level** - High/Medium/Low (color-coded chip)

### Completion Forecast Alert

**If forecast available** (MudAlert - Info):
- **Estimated Completion Date** - Calendar date projection
- **Average Velocity** - Points per sprint used in calculation

**If forecast unavailable** (MudAlert - Warning):
- Error message explaining why (insufficient data, no remaining work, etc.)

### Progress Overview

- **Progress Bar** - Visual indicator of completion percentage
- **Percentage Text** - Exact completion percentage (1 decimal place)

### Sprint-by-Sprint Forecast Table

MudTable showing projected burndown:

| Column | Description |
|--------|-------------|
| Sprint | Sprint name/number |
| Start Date | Sprint start date |
| End Date | Sprint end date |
| Expected Completed | Cumulative completed effort by sprint end |
| Remaining After | Remaining effort after sprint |
| Progress | Progress bar and percentage |

### Burndown Chart

MudChart (Line chart) showing:
- X-axis: Sprint labels (Now, Sprint 1, Sprint 2, ...)
- Y-axis: Remaining effort (story points)
- Line: Projected burndown to zero
- Visual trend from current state to completion

---

## User Interactions and Behavior

### Epic ID Entry
- User enters Epic or Feature work item ID
- ID validation: Must be positive integer
- Calculate button enables when valid ID present

### Historical Sprints Selection
- User adjusts sprint count for velocity calculation
- Default: 5 sprints (balance of recent vs stable)
- Range: 1-20 sprints

### Calculate Forecast
- Click "Calculate" button to trigger analysis
- Loading indicator shows during API call
- URL updates to `/epic-forecast/{epicId}` (without page reload)
- Results replace previous forecast or empty state

### View Results
- Scroll through forecast sections (cards, table, chart)
- Review confidence level and completion date
- Analyze sprint-by-sprint breakdown
- Use burndown chart for visual communication

### Error Handling
- Epic not found: Error display with clear message
- No child work items: Warning about missing data
- Insufficient historical data: Guidance on minimum requirements
- Missing effort estimates: Alert about data quality issues

### URL Navigation
- Direct link to forecast: `/epic-forecast/{epicId}`
- URL updates after successful forecast calculation
- Page state persists during navigation (no full reload)

---

## Filters and Search Capabilities

**No filtering required** - This is a single-item analysis feature.

User specifies:
- One Epic/Feature ID at a time
- Historical sprint lookback period

No search, filter, or multi-item selection needed.

---

## Data Requirements and Caching Strategy

### Data Requirements

#### Work Item Fields
- **Parent-Child Links** - Hierarchy to identify all child work items
- **Effort/Story Points** - Numeric effort values on child items
- **State** - Current workflow state (to identify completed work)
- **Iteration Path** - Sprint assignment for completed work
- **Title** - Display name for Epic/Feature
- **Type** - Work item type (Epic, Feature, PBI, Task, etc.)
- **Area Path** - Team/product organizational path

#### Historical Data
- **Completed Work per Sprint** - Sum of completed effort by iteration
- **Sprint Date Ranges** - Start and end dates for iterations
- **Velocity Variance** - Standard deviation for confidence calculation

### Caching Strategy

Per `docs/ARCHITECTURE_RULES.md`:

1. **Local Cache** - All work items cached in SQLite
   - Retrieved via "Pull & Cache" command
   - Includes hierarchy, effort, state, and iteration data
2. **Read-Only Analysis** - Forecast calculation reads from cache
3. **No Forecast Caching** - Forecast computed on-demand each time
4. **Staleness Handling** - User must manually refresh cache before forecasting

### Data Flow

1. User triggers forecast calculation
2. API receives Epic ID and sprint count
3. Handler queries local cache for:
   - Epic/Feature work item
   - All descendant work items (via Parent-Child links)
   - Historical sprint data (completed work per iteration)
4. Handler computes:
   - Total, completed, and remaining effort
   - Average velocity from historical sprints
   - Velocity standard deviation (for confidence)
   - Remaining sprints calculation
   - Sprint-by-sprint burndown projection
   - Estimated completion date
5. DTO returned to UI
6. Blazor component renders forecast

### Real-time Updates

**Not applicable** - Forecast is a point-in-time calculation.

User must:
- Refresh local cache to get latest TFS data
- Re-run forecast calculation to update predictions

---

## Architecture Compliance

### Layer Separation

- **Core Layer**: Query and DTO definitions
  - `GetEpicCompletionForecastQuery.cs`
  - `EpicCompletionForecastDto.cs`
  - `SprintForecastDto.cs`
  - `ForecastConfidence` enum

- **API Layer**: Handler and controller
  - `GetEpicCompletionForecastQueryHandler.cs`
  - `MetricsController.cs` (epic forecast endpoint)

- **Client Layer**: Blazor UI component
  - `EpicForecast.razor`

### API Endpoint

**GET /api/metrics/epic-forecast/{epicId}**

Query Parameters:
- `maxSprintsForVelocity` (optional, default: 5) - Historical sprint lookback

Response: `EpicCompletionForecastDto`

Status Codes:
- 200 OK - Forecast calculated successfully
- 404 Not Found - Epic/Feature not found in cache
- 400 Bad Request - Invalid Epic ID or parameter values
- 500 Internal Server Error - Calculation error

### Data Model

```csharp
public sealed record EpicCompletionForecastDto
{
    int EpicId;
    string Title;
    string Type;
    string AreaPath;
    int TotalEffort;
    int CompletedEffort;
    int RemainingEffort;
    double EstimatedVelocity;
    double VelocityStandardDeviation;
    ForecastConfidence Confidence;
    int SprintsRemaining;
    DateTimeOffset? EstimatedCompletionDate;
    IReadOnlyList<SprintForecastDto> ForecastByDate;
    DateTimeOffset AnalysisTimestamp;
}

public sealed record SprintForecastDto
{
    string SprintName;
    DateTimeOffset SprintStartDate;
    DateTimeOffset SprintEndDate;
    int ExpectedCompletedEffort;
    int RemainingEffortAfterSprint;
    double ProgressPercentage;
}

public enum ForecastConfidence
{
    High,    // Velocity StdDev < 20% of mean
    Medium,  // Velocity StdDev 20-40% of mean
    Low      // Velocity StdDev > 40% of mean
}
```

### Forecast Algorithm

1. **Load Epic and Children**
   - Retrieve Epic/Feature by ID
   - Get all descendant work items via Parent-Child links
   - Filter to relevant child types (PBI, Task, Bug, etc.)

2. **Calculate Effort Totals**
   - Total Effort = Sum of all child effort values
   - Completed Effort = Sum of children in "Done" states
   - Remaining Effort = Total - Completed

3. **Compute Historical Velocity**
   - Group completed children by Iteration Path
   - Take last N sprints (configurable)
   - Calculate sum of completed effort per sprint
   - Average Velocity = Mean of sprint totals
   - Velocity StdDev = Standard deviation of sprint totals

4. **Determine Confidence Level**
   - Coefficient of Variation = StdDev / Mean
   - High: CV < 0.20 (consistent velocity)
   - Medium: 0.20 ≤ CV < 0.40
   - Low: CV ≥ 0.40 (volatile velocity)

5. **Project Completion**
   - Sprints Remaining = Ceiling(Remaining Effort / Average Velocity)
   - Estimated Date = Current Sprint End + (Sprints Remaining * Sprint Length)

6. **Generate Sprint-by-Sprint Forecast**
   - For each future sprint:
     - Expected Completed = Average Velocity
     - Remaining After = Previous Remaining - Expected Completed
     - Progress % = Cumulative Completed / Total Effort

---

## UI Rules Compliance

Per `docs/UI_RULES.md`:

- **Platform**: Blazor WebAssembly ✓
- **Components**: MudBlazor only (MudPaper, MudCard, MudTable, MudChart, MudAlert, MudNumericField, MudButton) ✓
- **No JavaScript**: All logic in C# ✓
- **Dark Theme**: Follows app-wide dark theme ✓
- **CSS Isolation**: Component-specific styles in `.razor.css` ✓
- **API Client**: Uses NSwag-generated `IMetricsClient` ✓
- **Error Handling**: Centralized via `ErrorMessageService` and `ErrorDisplay` component ✓
- **Validation**: Input validation via MudBlazor component properties ✓

---

## UX Principles Compliance

Per `docs/UX_PRINCIPLES.md`:

- **Clarity**: Clear labels, helper text, and section headers
- **Consistency**: Follows same layout pattern as other metric pages
- **Minimal**: Only essential input controls (Epic ID, sprint count)
- **Feedback**: Loading indicators, error displays, success alerts
- **Context-Aware Help**: PageHelp component with data requirements, best practices, and common issues
- **Visual Hierarchy**: Summary cards → forecast alert → detailed breakdown → chart

---

## Testing Requirements

### Integration Tests

Located in: `PoTool.Tests.Integration/Features/EpicForecast.feature`

Scenarios:
- Basic epic forecast calculation
- Forecast with configurable sprint lookback
- Epic with no child work items
- Epic with incomplete effort data
- High confidence forecast (consistent velocity)
- Low confidence forecast (volatile velocity)
- Epic not found in cache
- Invalid Epic ID handling

### Unit Tests

Located in: `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`

Scenarios:
- Calculate velocity from historical sprints
- Compute confidence levels (high/medium/low)
- Project completion date from velocity
- Generate sprint-by-sprint forecast
- Handle zero remaining effort
- Handle missing iteration paths
- Handle missing effort values
- Edge case: single sprint history
- Edge case: zero velocity

---

## User Guidance

### Context-Aware Help

Provided via PageHelp component:

**Data Requirements:**
- Effort (Story Points) - Required for all child items
- State - Used to determine completed vs remaining work
- Parent-Child Links - Required to identify Epic children
- Iteration Path - Used for historical velocity calculation

**Best Practices:**
- Use 3-5 historical sprints for velocity calculation
- Plan to "likely case" scenario, but buffer for "worst case"
- Re-forecast weekly as work progresses
- Freeze scope before forecasting for accuracy
- High confidence (>75%) indicates consistent velocity - trust the forecast
- Low confidence (<60%) means volatile velocity - add buffer time
- Track forecast vs actual completion to calibrate estimates
- Use for stakeholder communication and release planning

**Common Issues:**
- **Low confidence (<60%)**: Velocity inconsistent. Review team capacity, scope changes, or estimation accuracy. Use more historical sprints or stabilize team size.
- **Forecast date keeps moving**: Scope changing or velocity declining. Freeze scope, investigate velocity drops, communicate changes early.
- **Large gap between best/worst case**: High velocity variability. Review estimation practices, check dependencies, investigate team disruptions.
- **No forecast shown**: Ensure Epic has children, children have effort estimates, historical sprints have completed work, iteration paths set correctly.

---

## Future Enhancements

If approved in the future:

1. **Monte Carlo Simulation** - Probabilistic forecasting with confidence intervals
2. **Best/Likely/Worst Case Scenarios** - Multiple projections based on velocity percentiles
3. **Velocity Trend Analysis** - Is velocity improving, declining, or stable?
4. **Historical Forecast Accuracy** - Compare past forecasts to actuals
5. **What-If Analysis** - Adjust scope or velocity to see impact on completion date
6. **Bulk Forecasting** - Forecast multiple Epics at once for portfolio planning

All enhancements must comply with `docs/UI_RULES.md` and `docs/ARCHITECTURE_RULES.md`.

---

## References

- UX Principles: `docs/UX_PRINCIPLES.md`
- UI Rules: `docs/UI_RULES.md`
- Architecture Rules: `docs/ARCHITECTURE_RULES.md`
- Process Rules: `docs/PROCESS_RULES.md`
- Implementation: `PoTool.Client/Pages/Metrics/EpicForecast.razor`
- Query: `PoTool.Core/Metrics/Queries/GetEpicCompletionForecastQuery.cs`
