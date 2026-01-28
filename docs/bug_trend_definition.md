# Bug Trend Definition

This document defines how the Bug Trend chart on the Trends (Past) page calculates and displays bug metrics over a 6-month period.

## Overview

The Bug Trend chart displays three metrics for each of the last 6 months:

1. **Total bug count** - Bugs existing/active in that month
2. **Bugs fixed** - Bugs moved to Done/Completed in that month  
3. **Bugs added** - Bugs created in that month

## Metric Definitions

### 1. Total Bug Count

**Definition**: Number of bugs that were **open at month-end** (snapshot approach).

**Calculation**:
- At the end of each month, count all bugs where:
  - `CreatedDate <= end of month`
  - AND State classification is NOT "Done" or "Removed" at month-end

**Rationale**: 
- Provides a consistent snapshot of bug debt at the end of each month
- Easier to interpret than "bugs active at any time during month"
- Aligns with typical product health reporting

**Implementation Note**:
- This requires tracking state transitions over time
- For current implementation without state history, we use a simplified approach:
  - Count bugs where `CreatedDate` is in or before the month
  - AND current state is not "Done" or "Removed"
- Future enhancement: Track state history for accurate month-end snapshots

### 2. Bugs Fixed

**Definition**: Number of bugs that transitioned to a "Done" state during that month.

**State Classification**: 
- Uses the `StateClassification` enum from `WorkItemStateClassificationDto`
- States classified as `StateClassification.Done` count as fixed
- Common "Done" states: "Done", "Closed", "Resolved", "Completed"

**Calculation**:
- Count bugs where the state transition to "Done" occurred during the month
- Uses state history/transition data if available

**Current Limitation**:
- State transition history is not currently tracked in the cache
- Current implementation may not accurately capture when bugs were fixed
- Workaround: Show bugs currently in "Done" state that were created before/during the period

**Future Enhancement**: 
- Track state transition history in `WorkItemEntity`
- Store `CompletedDate` field to accurately capture when bugs were fixed
- See `docs/bug_trend_followups.md` for details

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
- ✅ Bugs Added: Accurate using `System.CreatedDate` from TFS
- ⚠️ Bugs Fixed: Approximate (uses current state, not transition date)
- ⚠️ Total Bug Count: Approximate (uses current state, not month-end snapshot)

### Known Limitations
1. No state transition history tracking
2. No "completed date" or "resolved date" field
3. Total count is current state, not historical snapshot

### Future Improvements
See `docs/bug_trend_followups.md` for planned enhancements.

## References

- TFS field: `System.CreatedDate` 
- State classifications: `PoTool.Shared.Settings.StateClassification`
- Work item entity: `PoTool.Api.Persistence.Entities.WorkItemEntity`
- Bug Overview page: `PoTool.Client/Pages/Beta/BetaBugOverview.razor`
- Bug Trend page: `PoTool.Client/Pages/Beta/BetaTrendsWorkspace.razor`
- Trend chart component: `PoTool.Client/Pages/Beta/Components/BetaTrendChart.razor`
