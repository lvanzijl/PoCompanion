## Feature: Work Item State Timeline

### Goal

Provide historical state transition analysis for work items, enabling teams to identify workflow bottlenecks, understand cycle time vs lead time, and optimize delivery processes. Track how work items move through states over time and compare with team averages.

This feature must explicitly reference and comply with:

* `docs/UX_PRINCIPLES.md`
* `docs/UI_RULES.md`
* `docs/ARCHITECTURE_RULES.md`
* `docs/PROCESS_RULES.md`

---

## Functional Description

### Overview

The State Timeline feature analyzes work item revision history to visualize state transitions and identify process bottlenecks. It provides:

1. **State transition history** - Complete timeline of state changes with timestamps
2. **Time in state analysis** - Duration spent in each workflow state
3. **Bottleneck detection** - Automatic identification of abnormal delays
4. **Cycle time calculation** - Total time from "In Progress" to "Done"
5. **Lead time calculation** - Total time from "Created" to "Done"
6. **Visual timeline** - Interactive timeline showing state progression

---

## Value Proposition

### Beyond TFS WebUI

TFS WebUI provides basic history but lacks:
- **Bottleneck detection** - No automatic identification of workflow delays
- **Visual timeline** - History is flat table, not visual progression
- **Time-in-state analysis** - No aggregation or duration calculations
- **Comparison with averages** - No context for whether durations are normal
- **Cycle time vs lead time** - No distinction between active work time vs total time
- **Process optimization insights** - No actionable guidance on workflow improvements

This feature adds:
- Automated bottleneck detection with severity levels
- Visual timeline showing state progression
- Time spent in each state with duration formatting
- Color-coded indicators for normal vs abnormal durations
- Clear distinction between cycle time (active) and lead time (total)
- Detailed analysis for retrospectives and process optimization

---

## Configuration Requirements

### Prerequisites

1. **Cached Work Items** - Work item must be cached locally with revision history
2. **State History** - Azure DevOps automatically tracks all state changes
3. **Change Timestamps** - Each state transition has associated timestamp
4. **Created Date** - Work item creation date for lead time calculation

### User Configuration

- **Work Item ID** - User enters the work item ID to analyze

No global configuration required; analysis is per work item.

---

## Main View Structure and Components

### Access Point

- Navigation menu: **Metrics → State Timeline** (or under Work Items section)
- Direct URL: `/state-timeline` or `/state-timeline/{workItemId}`

### Input Controls

**Work Item Selection Panel** (MudPaper with form controls):
- **Work Item ID** - Numeric input field (MudNumericField)
  - Label: "Work Item ID"
  - Helper text: "Enter work item ID to analyze"
  - Validation: Min value = 1
- **Analyze Button** - Primary action button
  - Enabled only when valid work item ID entered
  - Icon: Search/Analytics

### Work Item Information Card

Displays:
- **Work Item Title** - Full title
- **Work Item ID** - Identifier
- **Type** - Epic, Feature, PBI, Task, Bug, etc.
- **Total Cycle Time** - Time from first "In Progress" to "Done"
- **Time In Progress** - Total active work time
- **Analysis Timestamp** - When analysis was performed

### Bottleneck Alert Section

**If bottlenecks detected** (MudAlert - Warning):
- Alert title: "⚠️ {Count} Bottleneck(s) Identified"
- List of bottlenecks with:
  - **State Name** - Which state had the delay
  - **Time Spent** - Duration in that state
  - **Reason** - Why it's considered a bottleneck
  - **Severity** - Critical/High/Medium (color-coded chip)

**If no bottlenecks** (MudAlert - Success):
- Success message: "✓ No Significant Bottlenecks"
- Text: "Work item progressed through states at a normal pace."

### State Transition Timeline

**MudTimeline component** showing chronological progression:

For each transition:
- **Timeline Item** with color-coded dot
- **Timestamp** - Date and time of transition
- **State Change** - "From State" → "To State"
- **Changed By** - User who made the change
- **Time in Previous State** - Duration before transition

Color coding:
- Green: Normal progression (< 3 days)
- Blue: Moderate duration (3-7 days)
- Orange: Long duration (7-14 days)
- Red: Very long duration (> 14 days)

### State Duration Chart

**MudChart (Bar chart)** showing:
- X-axis: State names (ordered by total time spent)
- Y-axis: Days spent in each state
- Bars: Height represents duration
- Sorted: Longest duration states first

### Detailed Transition Table

**MudTable** with columns:

| Column | Description |
|--------|-------------|
| From State | Previous state |
| To State | New state (bold) |
| Transition Date | When change occurred |
| Changed By | User who made the change |
| Time in Previous State | Duration in previous state |

Sortable and filterable for detailed analysis.

---

## User Interactions and Behavior

### Work Item ID Entry
- User enters work item ID
- ID validation: Must be positive integer
- Analyze button enables when valid ID present

### Analyze Timeline
- Click "Analyze" button to trigger analysis
- Loading indicator shows during API call
- URL updates to `/state-timeline/{workItemId}` (without page reload)
- Results replace previous analysis or empty state

### View Timeline
- Scroll through state transition timeline
- Review bottleneck alerts at top
- Analyze time-in-state chart
- Examine detailed transition table

### Interpret Bottlenecks
- Critical severity: > 2x team average for state
- High severity: > 1.5x team average
- Medium severity: > 1.2x team average
- Hover/click for additional context

### Error Handling
- Work item not found: Error display with clear message
- No state history: Info alert explaining no transitions found
- Incomplete data: Warning about missing revision information

### URL Navigation
- Direct link to analysis: `/state-timeline/{workItemId}`
- URL updates after successful analysis
- Page state persists during navigation (no full reload)

---

## Filters and Search Capabilities

**No filtering required** - This is a single-item analysis feature.

User specifies:
- One work item ID at a time

No search, filter, or multi-item selection needed.

Future enhancement could add:
- Compare multiple work items
- Filter timeline to specific date ranges
- Search for specific state transitions

---

## Data Requirements and Caching Strategy

### Data Requirements

#### Work Item Fields
- **State History** - Azure DevOps revision history for State field
- **Changed Date** - Timestamp of each field change (automatically captured)
- **Changed By** - User who made each change
- **Created Date** - Work item creation date for lead time
- **Closed Date** - Completion date for cycle time (if applicable)
- **Title** - Display name for work item
- **Type** - Work item type (for context)

#### Revision Data
- All revisions where State field changed
- Timestamp of each revision
- State value before and after change
- Identity of user who made change

### Caching Strategy

Per `docs/ARCHITECTURE_RULES.md`:

1. **Local Cache** - Work items cached in SQLite with revision history
   - Retrieved via "Pull & Cache" command
   - Includes full revision history with field changes
2. **Read-Only Analysis** - Timeline analysis reads from cache
3. **No Timeline Caching** - Timeline computed on-demand each time
4. **Staleness Handling** - User must manually refresh cache before analyzing

### Data Flow

1. User triggers timeline analysis
2. API receives work item ID
3. Handler queries local cache for:
   - Work item basic data
   - Complete revision history
   - All state transitions
4. Handler computes:
   - Chronological state transition sequence
   - Time spent in each state
   - Total cycle time and lead time
   - Bottleneck detection (compare to thresholds)
   - Transition summary statistics
5. DTO returned to UI
6. Blazor component renders timeline

### Bottleneck Detection Algorithm

**Threshold-Based Detection:**
1. Calculate time spent in each state
2. Compare to predefined thresholds:
   - **Critical**: > 14 days in any non-final state
   - **High**: > 7 days in "In Progress" or "Blocked"
   - **Medium**: > 7 days in any non-final state
3. Create bottleneck record with:
   - State name
   - Time spent
   - Severity
   - Reason (e.g., "Extended time in 'Code Review'")

**Future Enhancement:**
- Team average comparison (requires aggregated metrics)
- Historical baseline comparison
- Workflow-specific thresholds

---

## Architecture Compliance

### Layer Separation

- **Core Layer**: Query and DTO definitions
  - `GetWorkItemStateTimelineQuery.cs`
  - `WorkItemStateTimelineDto.cs`
  - `StateTransition.cs`
  - `Bottleneck.cs`
  - `BottleneckSeverity` enum

- **API Layer**: Handler and controller
  - `GetWorkItemStateTimelineQueryHandler.cs`
  - `WorkItemsController.cs` (state timeline endpoint)

- **Client Layer**: Blazor UI component
  - `StateTimeline.razor`

### API Endpoint

**GET /api/workitems/{workItemId}/state-timeline**

Response: `WorkItemStateTimelineDto`

Status Codes:
- 200 OK - Timeline calculated successfully
- 404 Not Found - Work item not found in cache
- 400 Bad Request - Invalid work item ID
- 500 Internal Server Error - Analysis error

### Data Model

```csharp
public sealed record WorkItemStateTimelineDto
{
    int WorkItemId;
    string Title;
    string Type;
    IReadOnlyList<StateTransition> StateHistory;
    IReadOnlyList<Bottleneck> Bottlenecks;
    TimeSpan TotalCycleTime;      // In Progress → Done
    TimeSpan TotalTimeInProgress; // Sum of all "In Progress" states
    DateTimeOffset AnalysisTimestamp;
}

public sealed record StateTransition
{
    string FromState;
    string ToState;
    DateTimeOffset TransitionDate;
    string ChangedBy;
    TimeSpan TimeInPreviousState;
}

public sealed record Bottleneck
{
    string State;
    TimeSpan TimeSpent;
    BottleneckSeverity Severity;
    string Reason;
}

public enum BottleneckSeverity
{
    Medium,   // Moderate delay
    High,     // Significant delay
    Critical  // Severe delay
}
```

### Timeline Analysis Algorithm

1. **Load Work Item Revisions**
   - Retrieve work item by ID
   - Get all revisions from cache
   - Filter to revisions where State field changed

2. **Build Transition Sequence**
   - Sort revisions by Changed Date (ascending)
   - For each revision:
     - Extract FromState (previous State value)
     - Extract ToState (new State value)
     - Extract TransitionDate (Changed Date)
     - Extract ChangedBy (user identity)
     - Calculate TimeInPreviousState (difference from previous transition)

3. **Calculate Cycle Time Metrics**
   - Total Cycle Time = Time from first "In Progress" to "Done"
   - Total Lead Time = Time from Created Date to Closed Date
   - Time In Progress = Sum of durations in "In Progress" states

4. **Detect Bottlenecks**
   - For each state:
     - Sum total time spent in that state
     - Compare to thresholds (7 days, 14 days)
     - Determine severity based on duration
     - Create bottleneck record if threshold exceeded

5. **Format and Return**
   - Create WorkItemStateTimelineDto
   - Return to client

---

## UI Rules Compliance

Per `docs/UI_RULES.md`:

- **Platform**: Blazor WebAssembly ✓
- **Components**: MudBlazor only (MudPaper, MudCard, MudTable, MudChart, MudAlert, MudTimeline, MudNumericField, MudButton, MudChip) ✓
- **No JavaScript**: All logic in C# ✓
- **Dark Theme**: Follows app-wide dark theme ✓
- **CSS Isolation**: Component-specific styles in `.razor.css` ✓
- **API Client**: Uses NSwag-generated `IWorkItemsClient` ✓
- **Error Handling**: Centralized via `ErrorMessageService` and `ErrorDisplay` component ✓
- **Validation**: Input validation via MudBlazor component properties ✓

---

## UX Principles Compliance

Per `docs/UX_PRINCIPLES.md`:

- **Clarity**: Clear labels, helper text, and section headers
- **Consistency**: Follows same layout pattern as other analysis pages
- **Minimal**: Only essential input control (work item ID)
- **Feedback**: Loading indicators, error displays, bottleneck alerts
- **Context-Aware Help**: PageHelp component with data requirements, best practices, and common issues
- **Visual Hierarchy**: Bottleneck alert → timeline → chart → detailed table
- **Color Coding**: Consistent use of colors for severity and duration

---

## Testing Requirements

### Integration Tests

Located in: `PoTool.Tests.Integration/Features/StateTimeline.feature`

Scenarios:
- Basic state timeline analysis
- Work item with multiple state transitions
- Work item with bottlenecks detected
- Work item with no state history
- Work item in single state (no transitions)
- Work item with rapid state changes
- Work item with backward transitions (rework)
- Work item not found in cache
- Invalid work item ID handling

### Unit Tests

Located in: `PoTool.Tests.Unit/Handlers/GetWorkItemStateTimelineQueryHandlerTests.cs`

Scenarios:
- Parse revision history into state transitions
- Calculate time in each state
- Detect bottlenecks based on thresholds
- Calculate total cycle time
- Calculate total lead time
- Handle missing Created Date
- Handle missing state transitions
- Handle null revision data
- Edge case: work item with single revision
- Edge case: work item never moved to "In Progress"

---

## User Guidance

### Context-Aware Help

Provided via PageHelp component:

**Data Requirements:**
- **State History** - Azure DevOps automatically tracks all state changes with timestamps
- **Changed Date** - Timestamp of each field change, automatically captured by TFS
- **Created Date** - Work item creation date for lead time calculation
- **Closed Date** - Completion date for total cycle time measurement (optional)

**Best Practices:**
- Analyze completed items during retrospectives to identify bottlenecks
- Compare individual items with team averages to spot outliers
- Items taking 2x team average indicate specific problems needing investigation
- Track lead time for forecasting (includes wait time before starting work)
- Track cycle time for capacity planning (actual work time)
- Use bottleneck analysis to optimize team workflow and processes
- Short cycle times (<2 weeks for PBIs) indicate efficient workflow
- Document learnings from long-running items to improve future estimates

**Common Issues:**
- **Very long "In Progress" state (2x+ team average)**: May indicate scope larger than estimated, technical complexity, or external dependencies. Break down large items, identify blockers early, or improve estimation accuracy.
- **Long wait times before "In Progress" (high lead time)**: Work sitting in backlog too long. Review prioritization, reduce WIP limits, or improve sprint planning to start work sooner.
- **Extended time in review/testing states**: Reviewer capacity issues or insufficient test coverage. Schedule dedicated review time, reduce PR size, or improve automated testing.
- **Multiple transitions back to earlier states**: Indicates rework or unclear acceptance criteria. Improve definition of done, enhance PR review quality, or clarify requirements upfront.

---

## Future Enhancements

If approved in the future:

1. **Team Average Comparison** - Compare work item against team baseline
2. **Historical Trending** - Track how cycle time changes over sprints
3. **Bulk Analysis** - Analyze multiple work items at once
4. **State Category Analysis** - Group by "New", "Active", "Resolved", "Closed"
5. **Workflow Efficiency Score** - Numeric score for workflow health
6. **Export Timeline** - Export to CSV, PDF, or image for reporting
7. **Custom Bottleneck Thresholds** - User-configurable delay definitions
8. **Anomaly Detection** - ML-based detection of unusual patterns

All enhancements must comply with `docs/UI_RULES.md` and `docs/ARCHITECTURE_RULES.md`.

---

## Use Cases

### Retrospective Analysis
Product Owner reviews completed work items to identify:
- Which states cause the most delays
- Whether code review is a bottleneck
- If testing takes longer than expected

### Process Optimization
Scrum Master analyzes patterns across multiple items to:
- Identify systemic workflow problems
- Adjust WIP limits based on state durations
- Propose workflow changes to reduce cycle time

### Stakeholder Communication
Development Manager uses timeline to:
- Explain why work item took longer than expected
- Demonstrate process improvements over time
- Set realistic expectations for future work

### Continuous Improvement
Team uses insights to:
- Reduce time in "Code Review" by adding reviewers
- Minimize time in "Blocked" by addressing dependencies earlier
- Improve definition of done to reduce rework

---

## References

- UX Principles: `docs/UX_PRINCIPLES.md`
- UI Rules: `docs/UI_RULES.md`
- Architecture Rules: `docs/ARCHITECTURE_RULES.md`
- Process Rules: `docs/PROCESS_RULES.md`
- Implementation: `PoTool.Client/Pages/Metrics/StateTimeline.razor`
- Query: `PoTool.Core/WorkItems/Queries/GetWorkItemStateTimelineQuery.cs`
