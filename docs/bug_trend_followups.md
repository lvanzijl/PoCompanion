# Bug Trend Follow-up Actions

This document tracks enhancements needed to fully implement accurate bug trend metrics as defined in `bug_trend_definition.md`.

## Current Limitations

### 1. State Transition History Not Tracked

**Problem**: 
We cannot accurately determine when a bug transitioned to "Done" state because state history is not cached.

**Impact**:
- "Bugs Fixed" metric is approximate, using current state instead of transition date
- "Total Bug Count" cannot show historical month-end snapshots accurately

**Required Enhancement**:
- Add state transition tracking to `WorkItemEntity`
- Store state change events with timestamps
- Option A: Add `StateHistory` JSON field with transition log
- Option B: Create separate `WorkItemStateTransition` entity/table

**Estimated Effort**: Medium (1-2 days)
- Database migration for new field/table
- Update sync stage to track state changes
- Update DTOs and queries

### 2. No "Completed Date" Field

**Problem**:
TFS/Azure DevOps has a "Closed Date" or "Resolved Date" field that we're not currently extracting.

**Impact**:
- Cannot accurately determine when bugs were fixed
- Must rely on state transition history (see #1) or approximate using current state

**Required Enhancement**:
- Extract `Microsoft.VSTS.Common.ClosedDate` or `System.ResolvedDate` from TFS
- Add `CompletedDate` or `ClosedDate` field to `WorkItemEntity`
- Update `WorkItemDto` to include this field
- Update sync stage to populate from TFS payload

**Estimated Effort**: Small (2-4 hours)
- Database migration for new field
- Update TFS client field list
- Update entity/DTO mapping
- Test with real TFS data

**TFS Fields to Consider**:
- `Microsoft.VSTS.Common.ClosedDate` - When work item was closed
- `Microsoft.VSTS.Common.ResolvedDate` - When bug was resolved
- `Microsoft.VSTS.Common.StateChangeDate` - Last state change timestamp

### 3. Historical Snapshot Calculation

**Problem**:
To show accurate "Total Bug Count" at month-end, we need to know what the state was at that specific point in time.

**Impact**:
- Current implementation shows bugs with current state, not historical state
- Cannot accurately represent bug debt at end of each past month

**Required Enhancement**:
- Implement temporal query capability
- Query bugs by their state at a specific date
- Requires either:
  - State transition history (see #1), or
  - Periodic snapshots of bug counts

**Estimated Effort**: Medium-Large (2-3 days)
- Depends on state history implementation (#1)
- Complex temporal queries
- Performance optimization needed

**Alternative Workaround**:
- Accept current limitation and document it
- Show current state with disclaimer: "Based on current state, not historical snapshot"
- Sufficient for trend analysis if state changes are infrequent

## Recommended Implementation Order

### Phase 1: Quick Win (Recommended for v1)
✅ Already completed:
- Use `System.CreatedDate` for "Bugs Added" metric
- Use current state approximation for other metrics
- Document limitations clearly

### Phase 2: Extract Completed Date (Recommended Next)
Priority: **High**  
Effort: **Small**  
Impact: **High**

Extract `Microsoft.VSTS.Common.ClosedDate` from TFS:
1. Add `CompletedDate` field to `WorkItemEntity` and `WorkItemDto`
2. Update `RealTfsClient.WorkItems.cs` to include `Microsoft.VSTS.Common.ClosedDate` in field list
3. Update `ParseWorkItemFromJson` to extract and map this field
4. Update sync stage to persist this field
5. Use `CompletedDate` for "Bugs Fixed" metric

Benefits:
- Accurate "Bugs Fixed" count without state history
- Simple, well-defined field from TFS
- No complex temporal queries needed

### Phase 3: State Transition History (Future Enhancement)
Priority: **Medium**  
Effort: **Medium**  
Impact: **Medium**

Only needed if:
- Accurate historical snapshots are required
- Need to show state changes over time
- Want full audit trail of work item state changes

Consider deferring unless explicitly requested.

## Migration Strategy

### Backward Compatibility
All enhancements must maintain backward compatibility:
- New fields should be nullable
- Fallback to existing approximations if new data unavailable
- No breaking changes to existing DTOs or APIs

### Data Backfill
When adding new fields:
- New syncs will populate the fields
- Existing cached data will have null values
- Charts should gracefully handle mixed data (some with field, some without)
- Consider one-time backfill sync if accurate historical data is critical

## Decision Points

### Question 1: Is approximate "Bugs Fixed" acceptable?
- **Yes**: Use current state approximation (done in v1)
- **No**: Implement Phase 2 (extract ClosedDate)

### Question 2: Is accurate month-end "Total Bug Count" required?
- **Yes**: Implement Phase 3 (state history)
- **No**: Document limitation and use current state (recommended for v1)

### Question 3: What is the primary use case?
- **Trend analysis**: Current approximation is sufficient
- **Audit/compliance**: Need accurate historical data (implement Phases 2-3)

## Testing Recommendations

When implementing enhancements:
1. Test with real TFS data (various bug workflows)
2. Verify field availability across different TFS/Azure DevOps versions
3. Test migration with existing cached data
4. Verify chart display with partial data (some bugs with field, some without)
5. Performance test with large datasets (1000+ bugs)

## References

- Main definition: `docs/bug_trend_definition.md`
- TFS API documentation: [Azure DevOps REST API - Work Items](https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/work-items)
- State classifications: `PoTool.Shared.Settings.StateClassification`
- Sync implementation: `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`
- TFS client: `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
