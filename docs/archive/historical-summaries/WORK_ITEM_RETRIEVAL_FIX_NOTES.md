# Work Item Retrieval Fix - Analysis Notes

## Issue Description
Current RealTfsClient is reported to fetch **ancestors (parents)** when it should fetch **descendants (children)** starting from product root work item IDs.

## Analysis Findings

### MockTfsClient (CONFIRMED CORRECT)
**Location**: `PoTool.Api/Services/MockTfsClient.cs`
**Method**: `GetWorkItemsByRootIdsAsync`

**Implementation**:
```csharp
void CollectHierarchy(int parentId)
{
    var item = allWorkItems.FirstOrDefault(wi => wi.TfsId == parentId);
    if (item != null)
    {
        results.Add(item);
        
        // Find children where ParentTfsId == parentId
        var children = allWorkItems.Where(wi => wi.ParentTfsId == parentId);
        foreach (var child in children)
        {
            CollectHierarchy(child.TfsId);  // Recurse into children
        }
    }
}
```

✅ **Tests confirm**: Fetches descendants only, does NOT fetch ancestors

### RealTfsClient (NEEDS VERIFICATION)
**Location**: `PoTool.Api/Services/RealTfsClient.cs`
**Method**: `GetWorkItemsByRootIdsAsync` (lines 709-1022)

**Implementation** (lines 788-822):
```csharp
// WIQL Query
var wiql = new
{
    query = $"SELECT [System.Id] FROM WorkItemLinks WHERE " +
            $"([Source].[System.Id] IN ({idList})) AND " +
            $"([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward'){dateFilter} " +
            $"MODE (Recursive)"
};

// Extract target IDs from relations
if (relation.TryGetProperty("target", out var target) && 
    target.TryGetProperty("id", out var idElement))
{
    var childId = idElement.GetInt32();
    if (allWorkItemIds.Add(childId) && !processedIds.Contains(childId))
    {
        idsToProcess.Enqueue(childId);  // Add to queue for further processing
    }
}
```

## Link Type Semantics (Azure DevOps)

Based on Azure DevOps documentation:
- `System.LinkTypes.Hierarchy-Forward`: Represents parent → child direction
- `System.LinkTypes.Hierarchy-Reverse`: Represents child → parent direction

In a `WorkItemLinks` WIQL query:
- `Source`: The work item on the "from" side of the link
- `Target`: The work item on the "to" side of the link

For `Hierarchy-Forward`:
- Source = Parent work item
- Target = Child work item

Therefore, the query `WHERE ([Source].[System.Id] IN ({idList}))` with `Hierarchy-Forward` should return links where:
- The source (parent) is in our list
- The target is the child

**This SHOULD be correct for fetching descendants!**

## Possible Issues

### 1. Link Direction Misunderstanding
The naming might be counter-intuitive. Maybe:
- `Hierarchy-Forward` actually means "forward TO parent" (child→parent)?
- Or the API behavior differs from documentation?

**Fix**: Try using `Hierarchy-Reverse` instead:
```csharp
$"([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Reverse'){dateFilter} "
```

### 2. Source/Target Interpretation
Maybe in Azure DevOps WIQL, when you specify `Source.Id IN (list)`:
- It finds items that LINK TO those IDs?
- Rather than items LINKED FROM those IDs?

**Fix**: Try querying for targets instead:
```csharp
$"([Target].[System.Id] IN ({idList})) AND " +
$"([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Reverse'){dateFilter} "
```

### 3. MODE (Recursive) Interpretation
The `MODE (Recursive)` might return ALL ancestors automatically, not descendants.

**Fix**: Remove `MODE (Recursive)` or verify its behavior with Azure DevOps docs.

## Recommended Testing Approach

1. Add logging to RealTfsClient to trace:
   - Input root IDs
   - WIQL query being executed
   - IDs returned from each batch
   - Final collected IDs

2. Test with a known hierarchy in real Azure DevOps:
   ```
   Root (ID: 100)
   ├── Child1 (ID: 101)
   │   └── Grandchild1 (ID: 102)
   └── Child2 (ID: 103)
   ```
   
   Call `GetWorkItemsByRootIdsAsync([100])`:
   - **Expected**: Should return 100, 101, 102, 103
   - **If fetching ancestors**: Would only return 100 (or parents of 100 if they exist)

3. Compare with Mock behavior which is proven correct.

## Implementation Strategy

Given uncertainty about Azure DevOps API behavior:

1. Add comprehensive logging to trace actual IDs being fetched
2. Try link direction swap (`Hierarchy-Reverse`)
3. Try source/target swap
4. Verify `MODE (Recursive)` behavior
5. Document findings and correct implementation

## Files to Modify

- `PoTool.Api/Services/RealTfsClient.cs` - Fix WIQL query and/or traversal logic
- Add logging for debugging
- Update comments to clarify correct link direction

## Tests Added

- ✅ `WorkItemHierarchyRetrievalTests.cs` - Confirms MockTfsClient works correctly
- ⏳ Need similar tests for RealTfsClient (requires real TFS/mock WIQL responses)
