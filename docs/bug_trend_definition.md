# Bug Trend Definition

This document defines how the Bug Trend chart on the Trends (Past) page calculates and displays bug metrics over a 6-month period.

## Overview

The Bug Trend chart displays three metrics for each of the last 6 months:

1. **Total bug count** - Bugs existing/active in that month
2. **Bugs fixed** - Bugs moved to Done/Completed in that month  
3. **Bugs added** - Bugs created in that month

## Metric Definitions

### 1. Total Bug Count

**Current Implementation (v1)**: Approximate count of open bugs at each month using current state.

**Calculation**:
- For each month, count all bugs where:
  - `CreatedDate < start of next month` (bug existed by end of the month)
  - AND current state is NOT "Done" or "Removed"

**Limitation**: This uses the bug's *current* state, not its historical state at month-end. For example, a bug created in January and fixed in March will NOT appear in January or February counts, even though it was open then.

**Ideal Definition** (future): Number of bugs that were **open at month-end** (snapshot approach).

**Rationale for current approach**: 
- Simple to implement without state history tracking
- Provides a reasonable approximation for recent trends
- Always shows a consistent view based on current data

### 2. Bugs Fixed

**Current Implementation (v1)**: Approximate count based on bugs currently in "Done" state.

**Calculation**:
- For each month, count bugs where:
  - `CreatedDate < start of next month` (bug existed by end of the month)
  - AND current state is classified as "Done"

**Limitation**: This counts the *current* population of fixed bugs that existed in each month, not bugs that transitioned to "Done" during that specific month. The same fixed bug appears in all months from its creation onward.

**Ideal Definition** (future): Number of bugs that transitioned to a "Done" state during that month.

**State Classification**: 
- Uses the `StateClassification` enum from `WorkItemStateClassificationDto`
- States classified as `StateClassification.Done` count as fixed
- Common "Done" states: "Done", "Closed", "Resolved", "Completed"

**Why this approximation?**:
- State transition history is not currently tracked in the cache
- Provides visibility into the fixed bug population trend
- Better than no data, useful for seeing overall fixed/open ratios

### 3. Bugs Added

**Definition**: Number of bugs created during that month.

**Calculation**:
- Count bugs where `CreatedDate` (from TFS field `System.CreatedDate`) falls within the month
- Uses `WorkItemDto.CreatedDate` property
- Falls back to `RetrievedAt` for older cached data without `CreatedDate`

**Data Source**:
- Primary: `System.CreatedDate` from TFS/Azure DevOps
- Fallback: `RetrievedAt` (cache timestamp) for backward compatibility

## State Categories

The application uses a state classification system defined in `PoTool.Shared.Settings.StateClassification`:

```csharp
public enum StateClassification
{
    New = 0,        // Not started (e.g., "New", "Proposed")
    InProgress = 1, // Actively being worked (e.g., "Active", "In Progress", "Committed")
    Done = 2,       // Complete (e.g., "Done", "Closed", "Resolved")
    Removed = 3     // Cancelled/Removed (e.g., "Removed", "Cancelled")
}
```

**For Bug Trend Metrics**:
- **Open/Active bugs**: `New` or `InProgress` states
- **Fixed bugs**: `Done` state
- **Excluded bugs**: `Removed` state (not counted in any metric)

## Chart Display Requirements

### Time Period
- Show exactly 6 months (most recent 6 calendar months)
- Display format: "MMM" (e.g., "Jan", "Feb", "Mar")
- Data key format: "YYYY-MM" (e.g., "2026-01")

### Chart Scaling
- Calculate `maxValue = max(max(Total), max(Fixed), max(Added))` across all 6 months
- Use this maximum to scale the Y-axis
- Ensures all three series are visible and proportionate
- Minimum bar height: 5% of chart height for visibility

### Series Display
- Each series shown as separate visual element (bar, line, or area)
- Distinct colors for each series:
  - Total: Primary color
  - Fixed: Success/green color  
  - Added: Error/red color
- Hover to highlight, click to filter (per Decision #12)

## Data Accuracy Notes

### Current State (v1)
- ✅ **Bugs Added**: Accurate using `System.CreatedDate` from TFS
- ⚠️ **Bugs Fixed**: Approximate - shows cumulative fixed bugs using current state
- ⚠️ **Total Bug Count**: Approximate - uses current state, not month-end snapshot

### Known Limitations
1. No state transition history tracking
2. No "completed date" or "resolved date" field
3. Fixed and Total counts show the same bugs across multiple months (cumulative view)
4. Total and Fixed counts may not reflect historical reality due to state changes over time

### What This Means for Users
- **Added** series is reliable for tracking bug creation trends
- **Total** and **Fixed** series show the current state of bugs, projected back in time
- Trends are still valuable for seeing overall patterns, but specific monthly values should be interpreted carefully
- For example: A bug created in January and fixed in March will NOT appear in January/February Total counts

### Future Improvements
See `docs/bug_trend_followups.md` for planned enhancements.

## References

- TFS field: `System.CreatedDate` 
- State classifications: `PoTool.Shared.Settings.StateClassification`
- Work item entity: `PoTool.Api.Persistence.Entities.WorkItemEntity`
- Bug Overview page: `PoTool.Client/Pages/Beta/BetaBugOverview.razor`
- Bug Trend page: `PoTool.Client/Pages/Beta/BetaTrendsWorkspace.razor`
- Trend chart component: `PoTool.Client/Pages/Beta/Components/BetaTrendChart.razor`
