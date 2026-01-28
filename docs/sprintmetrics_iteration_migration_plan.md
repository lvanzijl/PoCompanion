# Sprint Metrics Iteration Migration Plan

**Version:** 1.0  
**Status:** In Progress  
**Created:** 2026-01-28  
**Last Updated:** 2026-01-28

---

## Executive Summary

This document outlines the migration from lexicographic iteration path sorting to proper date-based sprint selection using `SprintMetricsDto` and `SprintDto` for determining current, past, and future sprints in the Health workspace.

---

## Current State

### SprintMetricsDto Definition
**Location:** `PoTool.Shared/Metrics/SprintMetricsDto.cs`

```csharp
public sealed record SprintMetricsDto(
    string IterationPath,
    string SprintName,
    DateTimeOffset? StartDate,  // Currently populated as null
    DateTimeOffset? EndDate,    // Currently populated as null
    int CompletedStoryPoints,
    int PlannedStoryPoints,
    int CompletedWorkItemCount,
    int TotalWorkItemCount,
    int CompletedPBIs,
    int CompletedBugs,
    int CompletedTasks
);
```

**Issue:** StartDate and EndDate are currently set to null in `GetSprintMetricsQueryHandler` (line 118-119).

### SprintDto Definition
**Location:** `PoTool.Shared/Settings/SprintDto.cs`

```csharp
public sealed record SprintDto(
    int Id,
    int TeamId,
    string? TfsIterationId,
    string Path,
    string Name,
    DateTimeOffset? StartUtc,   // Available from TFS
    DateTimeOffset? EndUtc,     // Available from TFS
    string? TimeFrame,          // "current", "past", "future"
    DateTimeOffset LastSyncedUtc
);
```

**Availability:** Managed by `ISprintRepository` with methods:
- `GetSprintsForTeamAsync(int teamId)` - Returns all sprints ordered by start date
- `GetCurrentSprintForTeamAsync(int teamId)` - Returns current sprint based on dates or TimeFrame

---

## Problem Areas

### 1. GetMultiIterationBacklogHealthQueryHandler
**File:** `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`  
**Lines:** 137-143

**Current Implementation:**
```csharp
var iterationPaths = allWorkItems
    .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
    .Select(wi => wi.IterationPath)
    .Distinct()
    .OrderByDescending(path => path) // Most recent first (WRONG - lexicographic)
    .Take(query.MaxIterations)
    .ToList();
```

**Issues:**
- Uses lexicographic path sorting as a proxy for chronological order
- Assumes path format follows lexicographic pattern (e.g., "2025\Q4\Sprint12" > "2025\Q4\Sprint11")
- No guarantee of correctness with different path naming schemes
- Doesn't respect "current sprint" concept

**Impact:**
- Backlog Health Analysis shows incorrect iterations
- Issue Comparison Across Iterations shows incorrect timeframe

---

## Migration Plan

### Phase 1: Populate Sprint Dates in SprintMetricsDto ✅

**Changes Required:**
1. Update `GetSprintMetricsQueryHandler` to populate StartDate and EndDate
2. Match IterationPath from work items to Sprint records via ISprintRepository
3. Handle missing sprint records gracefully (keep nullable dates)

**Implementation:**
- Inject `ISprintRepository` into `GetSprintMetricsQueryHandler`
- Query all sprints for all teams
- Match by Path field
- Populate StartDate/EndDate from matched Sprint

---

### Phase 2: Create Sprint Window Selector Service

**Location:** `PoTool.Core/Metrics/Services/SprintWindowSelector.cs`

**Definitions:**
```csharp
public class SprintWindowSelector
{
    // Current sprint: StartDate <= today < EndDate
    // If no current exists:
    //   - Use earliest future sprint as "current"
    //   - Else use latest past sprint as "current"
    
    // Future sprints: StartDate > today, ordered ascending by StartDate
    // Past sprints: EndDate <= today, ordered descending by EndDate
    
    public IReadOnlyList<SprintMetricsDto> GetBacklogHealthSprints(
        IEnumerable<SprintMetricsDto> allSprints, 
        DateTimeOffset today)
    {
        // Returns: current + 2 future (3 total, no past)
    }
    
    public IReadOnlyList<SprintMetricsDto> GetIssueComparisonSprints(
        IEnumerable<SprintMetricsDto> allSprints, 
        DateTimeOffset today)
    {
        // Returns: 3 past + current + 2 future (6 total)
    }
}
```

**Unit Tests Required:**
- Standard case: past/current/future all exist
- No current sprint (only future)
- No current sprint (only past)
- Insufficient future sprints (< 2)
- Insufficient past sprints (< 3)
- Empty sprint list
- All sprints have null dates (fallback to path ordering warning)

---

### Phase 3: Migrate GetMultiIterationBacklogHealthQueryHandler

**Changes:**
1. Get all SprintMetrics for work items (with dates populated)
2. Use SprintWindowSelector based on query context:
   - For Backlog Health Analysis: `GetBacklogHealthSprints`
   - For Issue Comparison: `GetIssueComparisonSprints`
3. Remove `.OrderByDescending(path => path).Take(n)` logic

**New Flow:**
```
1. Load all work items (existing logic)
2. Get distinct iteration paths from work items
3. Build SprintMetricsDto for each path (with dates)
4. Pass to SprintWindowSelector.GetBacklogHealthSprints() or GetIssueComparisonSprints()
5. Calculate health for selected sprints
6. Return results
```

**Backward Compatibility:**
- If sprint dates are unavailable (null), log warning and fall back to path ordering
- Document this fallback behavior

---

### Phase 4: Update Client Components

**Changes Required:**

1. **BacklogHealthPanel.razor**
   - Currently passes `MaxIterations="6"` to API
   - API should internally decide sprint window based on query type
   - May need separate endpoints or query parameter to specify window type

2. **BetaHealthWorkspace.razor**
   - Currently shows first 3 iterations in table, all 6 in chart
   - After migration, API returns correct sprints; no client-side filtering needed

---

## Other Path-Sorting Usages Found

### 1. LivePullRequestReadProvider
**File:** `PoTool.Api/Services/LivePullRequestReadProvider.cs`  
**Lines:** 2 occurrences

```csharp
var latestIteration = iterations.OrderByDescending(i => i.IterationNumber).FirstOrDefault();
```

**Context:** Pull request iteration selection  
**Impact:** Low - uses IterationNumber (integer), not IterationPath  
**Action:** No migration needed (already using numeric ordering)

---

## Success Criteria

- [ ] SprintMetricsDto has StartDate/EndDate populated from SprintRepository
- [ ] SprintWindowSelector service created with unit tests
- [ ] GetMultiIterationBacklogHealthQueryHandler uses date-based selection
- [ ] Backlog Health Analysis shows current + 2 future sprints (date-based)
- [ ] Issue Comparison shows 3 past + current + 2 future sprints (date-based)
- [ ] No lexicographic path sorting used for sprint selection
- [ ] Audit document created listing all path-sorting usages

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Sprint dates not synced | High | Fallback to path ordering with warning log |
| Multiple teams with different sprint schedules | Medium | Use team-specific sprint queries when team context available |
| Sprint path mismatch between work items and sprint records | Medium | Normalize paths (case-insensitive, trim whitespace) |
| Performance impact from additional sprint queries | Low | Cache sprint data, query once per handler invocation |

---

## Timeline

| Phase | Estimated Effort | Status |
|-------|-----------------|---------|
| Phase 1: Populate Sprint Dates | 2 hours | In Progress |
| Phase 2: Create Selector Service | 3 hours | Not Started |
| Phase 3: Migrate Health Handler | 2 hours | Not Started |
| Phase 4: Update Client | 1 hour | Not Started |
| Testing & Documentation | 2 hours | Not Started |
| **Total** | **10 hours** | **10% Complete** |

---

## Notes

- SprintDto uses `Path` field; SprintMetricsDto uses `IterationPath` - ensure matching logic handles this
- TimeFrame field ("current", "past", "future") can be used as a hint but should be validated against actual dates
- DateTimeOffset comparison should use UTC consistently
- Consider timezone implications for "today" determination (use server timezone)
