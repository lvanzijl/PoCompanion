# Iteration Path Sorting Audit

**Version:** 1.0  
**Status:** Complete  
**Created:** 2026-01-28  
**Last Updated:** 2026-01-28

---

## Executive Summary

This document audits all usages of iteration path sorting in the codebase and documents whether they use date-based selection or lexicographic path ordering. The goal is to identify and migrate all path-based sorting to proper date-based selection using `SprintMetricsDto` or `SprintDto`.

---

## Methodology

Search patterns used:
- `OrderByDescending.*IterationPath`
- `OrderBy.*IterationPath`
- `OrderByDescending.*path`
- `.OrderBy*(` near iteration-related code

---

## Findings

### 1. GetMultiIterationBacklogHealthQueryHandler ⚠️ NEEDS MIGRATION

**File:** `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`  
**Lines:** 137-143

**Current Implementation:**
```csharp
var iterationPaths = allWorkItems
    .Where(wi => !string.IsNullOrWhiteSpace(wi.IterationPath))
    .Select(wi => wi.IterationPath)
    .Distinct()
    .OrderByDescending(path => path) // Most recent first
    .Take(query.MaxIterations)
    .ToList();
```

**Issue:**
- Uses lexicographic string ordering as a proxy for chronological ordering
- Assumes iteration path format follows a sortable pattern
- No guarantee of correctness across different naming schemes

**Recommended Fix:**
1. Build `SprintMetricsDto` for each distinct iteration path
2. Populate StartDate/EndDate from SprintRepository
3. Use `SprintWindowSelector.GetBacklogHealthSprints()` or `GetIssueComparisonSprints()`
4. Remove lexicographic sorting

**Complexity:** 3/5  
**Impact:** High - Affects Health workspace iteration selection  
**Status:** Not Migrated  
**Priority:** P0 - This is the primary migration target

---

### 2. LivePullRequestReadProvider ✅ ACCEPTABLE

**File:** `PoTool.Api/Services/LivePullRequestReadProvider.cs`  
**Lines:** Multiple occurrences

**Current Implementation:**
```csharp
var latestIteration = iterations.OrderByDescending(i => i.IterationNumber).FirstOrDefault();
```

**Analysis:**
- Uses `IterationNumber` (integer), NOT `IterationPath` (string)
- Numeric ordering is correct and does not need migration
- Not related to sprint/iteration date-based selection

**Complexity:** N/A  
**Status:** No Migration Needed  
**Rationale:** Uses numeric iteration number, not iteration path

---

### 3. SprintRepository ✅ CORRECT

**File:** `PoTool.Api/Repositories/SprintRepository.cs`  
**Method:** `GetSprintsForTeamAsync`

**Current Implementation:**
```csharp
var entities = await _context.Sprints
    .Where(s => s.TeamId == teamId)
    .OrderBy(s => s.StartUtc.HasValue ? s.StartUtc : DateTimeOffset.MaxValue)
    .ThenBy(s => s.Name)
    .ToListAsync(cancellationToken);
```

**Analysis:**
- Already uses date-based ordering (`StartUtc`)
- This is the canonical sprint ordering implementation
- No migration needed

**Status:** No Migration Needed  
**Rationale:** Already uses date-based ordering

---

### 4. WorkItemDto / IterationPath Field Usage

**Multiple Files:** Various handlers and services

**Usage Pattern:**
```csharp
wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase)
```

**Analysis:**
- Equality comparisons for filtering work items by iteration
- Not using path for ordering/sorting
- No migration needed

**Status:** No Migration Needed  
**Rationale:** Equality comparison, not ordering

---

## Migration Roadmap

### Priority 0 (Required)
1. **GetMultiIterationBacklogHealthQueryHandler**
   - Estimated effort: 2-3 hours
   - Blocking Health workspace date-based selection
   - See detailed migration plan in `sprintmetrics_iteration_migration_plan.md`

### Priority 1 (Optional Improvements)
None identified at this time.

---

## Migration Completion Criteria

- [ ] GetMultiIterationBacklogHealthQueryHandler migrated to use SprintWindowSelector
- [ ] StartDate/EndDate populated in SprintMetricsDto
- [ ] Health workspace displays correct sprints based on dates
- [ ] No lexicographic iteration path sorting used for chronological selection
- [ ] All tests passing
- [ ] Documentation updated

---

## Summary Statistics

| Category | Count |
|----------|-------|
| **Total Usages Found** | 4 |
| **Needs Migration** | 1 |
| **Already Correct** | 2 |
| **Not Applicable** | 1 |

---

## Recommendations

1. **Immediate Action:** Migrate GetMultiIterationBacklogHealthQueryHandler as described in Phase 3 of the migration plan

2. **Future Prevention:** Add coding guidelines:
   - ❌ Do NOT sort iteration paths lexicographically for chronological selection
   - ✅ DO use SprintDto/SprintMetricsDto with date-based sorting
   - ✅ DO use SprintWindowSelector for standard iteration window selection

3. **Testing:** Ensure all sprint selection logic has unit tests covering edge cases:
   - No current sprint
   - Insufficient past/future sprints
   - Sprints with null dates
   - Multiple sprints on same dates

4. **Documentation:** Update architecture docs to specify SprintDto/SprintMetricsDto as canonical sprint representations

---

## Appendix: Search Commands Used

```bash
# Find OrderByDescending on IterationPath
grep -r "OrderByDescending.*IterationPath" --include="*.cs"

# Find OrderBy on path variables near iteration code
grep -r "OrderBy.*path" --include="*.cs" | grep -i iteration

# Find any ordering on iteration-related fields
grep -r "\.OrderBy\|\.OrderByDescending" --include="*.cs" | grep -i "iteration\|sprint"
```

---

## Change History

| Date | Version | Changes |
|------|---------|---------|
| 2026-01-28 | 1.0 | Initial audit completed |
