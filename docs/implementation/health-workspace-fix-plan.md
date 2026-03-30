# Health Workspace Fix Plan

## Discovery Phase Results

### 1. ValidationCategory Counting Analysis

#### How Work Item Explorer Determines Counts

**Location:** `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`

**Method:**
1. Loads all work items with validation via `WorkItemService.GetAllWithValidationAsync(productIds)`
2. Calls `FilteringService.CountWorkItemsByValidationFilterAsync()` for each category
3. Passes filter ID as string: "1" (SI), "2" (RR), "3" (RC)
4. Backend endpoint: `POST api/Filtering/count-by-validation-filter`

**Backend Logic:** `PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs`
- Maps filter IDs to ValidationCategory enum
- Accepts both numeric ("1", "2", "3") and named ("StructuralIntegrity", etc.) filter IDs
- Uses `RuleCategoryMap` dictionary to map rule IDs to categories:
  ```csharp
  { "SI-1", ValidationCategory.StructuralIntegrity },
  { "SI-2", ValidationCategory.StructuralIntegrity },
  { "SI-3", ValidationCategory.StructuralIntegrity },
  { "RR-1", ValidationCategory.RefinementReadiness },
  { "RR-2", ValidationCategory.RefinementReadiness },
  { "RR-3", ValidationCategory.RefinementReadiness },
  { "RC-1", ValidationCategory.RefinementCompleteness },
  { "RC-2", ValidationCategory.RefinementCompleteness },
  ```
- Filters work items by checking if any ValidationIssue has a RuleId mapping to target category
- Falls back to `InferCategoryFromMessage()` for legacy issues without RuleId

#### How Health Workspace Determines Counts

**Location:** `PoTool.Client/Pages/Beta/BetaHealthWorkspace.razor`

**Method:**
1. Loads all work items with validation via `WorkItemService.GetAllWithValidationAsync()`
2. **Client-side counting** in `LoadValidationCountsAsync()`:
   - Creates Dictionary<ValidationCategory, HashSet<int>>
   - Iterates through all work items and their validation issues
   - Uses `GetCategoryFromRuleId()` to map rule IDs to categories
   - Tracks unique work item IDs (TfsId) per category using HashSet
   - Counts: `workItemsByCategory[category].Count`

**Key Difference:**
- **Explorer**: Uses backend API endpoint for counting (server-side)
- **Health**: Uses client-side logic with inline rule mapping

**Rule Mapping in Health:**
```csharp
private ValidationCategory? GetCategoryFromRuleId(string? ruleId)
{
    if (ruleId.StartsWith("SI-", StringComparison.OrdinalIgnoreCase))
        return ValidationCategory.StructuralIntegrity;
    else if (ruleId.StartsWith("RR-", StringComparison.OrdinalIgnoreCase))
        return ValidationCategory.RefinementReadiness;
    else if (ruleId.StartsWith("RC-", StringComparison.OrdinalIgnoreCase))
        return ValidationCategory.RefinementCompleteness;
    return null;
}
```

### 2. Root Cause Analysis: Why Counts Are Incorrect

#### Issue 1: Structural Integrity Mismatch (Explorer=27, Health=0)

**Hypothesis:**
- Health workspace might be filtering by product/profile differently
- Health workspace calls `WorkItemService.GetAllWithValidationAsync()` without product IDs
- Explorer calls `WorkItemService.GetAllWithValidationAsync(productIds)` with product scoping
- If Health doesn't get the same work items, counts will differ

**Data Flow:**
- Explorer: `GetAllWithValidationAsync(productIds)` → backend filters by product hierarchies
- Health: `GetAllWithValidationAsync()` → backend might use different default filtering

#### Issue 2: Refinement Readiness and Completeness Both Show 0

**Hypothesis:**
- Validation rules might not be generating issues with RR-* or RC-* rule IDs
- Or validation issues exist but don't have RuleId set (legacy format)
- Or the work-item rule engine is not running these categories

**Need to verify:**
- Are ValidationIssues being created with proper RuleId values?
- Are RR-* and RC-* validation rules being executed?
- Check validation rule implementation

### 3. Sprint Selection Logic for Backlog Health Analysis

**Location:** `PoTool.Core/Metrics/Services/SprintWindowSelector.cs`

**Current Behavior:**
- `GetBacklogHealthWindow()` returns exactly 3 slots: current + 2 future
- `GetIssueComparisonWindow()` returns exactly 6 slots: 3 past + current + 2 future
- Uses date-based selection: `StartDate <= today < EndDate`
- Fills missing future slots with placeholders (message: "newer sprints aren't available")

**Handler:** `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
- Determines window based on `MaxIterations` parameter:
  - MaxIterations ≤ 3: Uses GetBacklogHealthWindow (3 slots)
  - MaxIterations > 3: Uses GetIssueComparisonWindow (6 slots)

**Current Issue:**
- BacklogHealthPanel passes `MaxIterations=6` from BetaHealthWorkspace
- This triggers the 6-slot window (3 past + current + 2 future)
- **Should only show current + 2 future for Health workspace**

**Fix Required:**
- Change `BacklogHealthPanel` in BetaHealthWorkspace to use `MaxIterations=3`
- Or modify to always use GetBacklogHealthWindow for Health workspace context
- No past sprints should be shown in Health workspace

### 4. Area Path and Max Iterations Filters Location

**BacklogHealthFilters Component:**
`PoTool.Client/Pages/Metrics/SubComponents/BacklogHealthFilters.razor`

**Embedded in:**
- `BacklogHealthPanel.razor` (line 37-39)
- Used in BetaHealthWorkspace via BacklogHealthPanel

**Current State:**
- Area Path: Autocomplete with distinct area paths
- Max Iterations: Numeric field (min=1, max=100)
- Both have two-way binding to BacklogHealthPanel

**Removal Required:**
- Remove BacklogHealthFilters component usage from BacklogHealthPanel
- Remove area path and max iterations parameters/state
- BacklogHealthPanel should always use fixed MaxIterations=3 for Health context

---

## Implementation Approach

### Phase 2: Fix ValidationCategory Counting

**Root Cause:**
- Health workspace doesn't pass product IDs to `GetAllWithValidationAsync()`
- Explorer passes product IDs based on active profile
- Different data sets → different counts

**Solution:**
1. Update BetaHealthWorkspace to:
   - Get active profile
   - Get products for that profile (like Explorer does)
   - Pass product IDs to `GetAllWithValidationAsync()`
2. Ensure both use identical data loading approach
3. Verify RuleId is set on all validation issues

**Alternative Solution (Better):**
- Create shared service method that returns validation counts per category
- Both Health and Explorer call the same service
- Single source of truth

### Phase 3: Update Sprint Window

**Changes:**
1. BetaHealthWorkspace.razor: Change `<BacklogHealthPanel MaxIterations="6" />` to `MaxIterations="3"`
2. Verify SprintWindowSelector.GetBacklogHealthWindow() already implements correct logic
3. Test that placeholders appear with correct message

### Phase 4: Remove Filters

**Changes:**
1. Remove BacklogHealthFilters usage from BacklogHealthPanel
2. Remove area path and max iterations parameters from BacklogHealthPanel
3. BacklogHealthPanel should use fixed MaxIterations=3 internally
4. Keep area path parameter for non-Health contexts (backward compatibility)

### Phase 5: Severity Styling

**Changes in BetaHealthWorkspace.razor:**
1. Replace `GetHealthColor()` method:
   - Add severity parameter per category
   - Structural Integrity: Use Color.Error for count > 0
   - Refinement Readiness: Use Color.Warning for count > 0
   - Refinement Completeness: Use Color.Warning for count > 0
   - Count == 0: Use Color.Default (neutral gray)
2. Update labels:
   - "Structural Integrity" → "Structural integrity errors"
   - "Refinement Readiness" → "Refinement blockers"
   - "Refinement Completeness" → "Refinement needed"
3. Update icons if needed to match severity

---

## Testing Strategy

1. **ValidationCategory Counts:**
   - Load Health workspace with known invalid items
   - Verify counts match Explorer counts exactly
   - Test with different profiles/products
   - Verify all three categories show non-zero when appropriate

2. **Sprint Window:**
   - Verify only 3 rows appear in Backlog Health Analysis table
   - Verify no past sprints are shown
   - Test with < 2 future sprints (should show placeholders)
   - Verify placeholder message: "newer sprints aren't available"

3. **Filter Removal:**
   - Verify no filter controls appear in Health workspace
   - Verify BacklogHealthPanel still works in other contexts
   - Check for dead code/unused parameters

4. **Severity Styling:**
   - Verify error icon/color for Structural Integrity when count > 0
   - Verify warning icon/color for RR and RC when count > 0
   - Verify neutral (gray) styling when count == 0
   - Verify labels are updated

---

## Timeline Estimate

- Phase 2 (ValidationCategory): 2-3 hours
- Phase 3 (Sprint Window): 30 minutes
- Phase 4 (Filter Removal): 1 hour
- Phase 5 (Severity Styling): 1 hour
- Phase 6 (Documentation): 1 hour
- Testing: 2 hours

**Total: ~8 hours**
