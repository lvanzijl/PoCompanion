## Feature: Dependency Graph Visualization

### Goal

Provide a comprehensive visualization of work item dependencies and relationships, enabling users to identify critical paths, circular dependencies, and blocking work items. This helps with release planning, risk identification, and understanding work sequencing.

This feature must explicitly reference and comply with:

* `docs/UX_PRINCIPLES.md`
* `docs/UI_RULES.md`
* `docs/ARCHITECTURE_RULES.md`

---

## Functional Description

### Overview

The Dependency Graph feature analyzes work item relationships from cached TFS data and presents:

1. **Dependency nodes** - All work items with their relationships
2. **Dependency links** - Connections between work items (depends-on, blocks, parent-child)
3. **Critical paths** - Longest dependency chains that determine minimum delivery time
4. **Circular dependencies** - Cycles that create deadlocks and must be resolved
5. **Blocking work items** - Items that prevent progress on dependent work

---

## Visualization Approach

Per `docs/UI_RULES.md`, the UI **MUST** use only approved open-source Blazor component libraries (MudBlazor, Radzen, Fluent UI). None of these libraries provide native graph visualization components.

Therefore, the implementation uses a **hierarchical list-based view** with:

- Summary cards showing key metrics
- Filterable tables for nodes and links
- Highlighted alerts for critical paths and circular dependencies
- Color-coded status indicators

This approach complies with all architectural rules while providing clear, actionable insights.

---

## Data Model

### DependencyGraphDto

The core data structure returned by the API:

```csharp
public sealed record DependencyGraphDto(
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<DependencyLink> Links,
    IReadOnlyList<DependencyChain> CriticalPaths,
    IReadOnlyList<int> BlockedWorkItemIds,
    IReadOnlyList<CircularDependency> CircularDependencies,
    DateTimeOffset AnalysisTimestamp
);
```

### Dependency Types

| Type | Description | TFS Link Type |
|------|-------------|---------------|
| **DependsOn** | Work item depends on another | System.LinkTypes.Dependency-Forward |
| **Blocks** | Work item blocks another | System.LinkTypes.Dependency-Reverse |
| **Parent** | Hierarchical parent | System.LinkTypes.Hierarchy-Reverse |
| **Child** | Hierarchical child | System.LinkTypes.Hierarchy-Forward |
| **RelatedTo** | General relationship | Other link types |

---

## User Interface

### Access Point

The Dependency Graph is accessible via:
- Navigation menu: **Metrics → Dependency Graph**
- Direct URL: `/dependency-graph`

### Filter Controls

Users can filter the graph by:

1. **Area Path** - Filter by team or product area
   - Input: Text field with partial matching
   - Example: "Project\TeamA"

2. **Work Item IDs** - Focus on specific work items
   - Input: Comma-separated list of IDs
   - Example: "1001, 1003, 1005"

3. **Work Item Types** - Show only specific types
   - Input: Comma-separated list of types
   - Example: "Epic, Feature, Task"

### Summary Cards

Four cards display key metrics:

1. **Total Nodes** - Count of work items in the graph
2. **Total Links** - Count of dependencies
3. **Critical Paths** - Count of high-risk dependency chains
4. **Blocked Items** - Count of blocking work items (red warning)

### Critical Paths Section

Displays the top 5 longest dependency chains:

- **Risk Level** - Critical, High, Medium, or Low (color-coded)
- **Chain Length** - Number of items in the chain
- **Total Effort** - Sum of story points across the chain
- **Work Items** - Sequence of IDs showing the path

Example:
```
⚠️ Critical Risk
Chain Length: 5 items
Total Effort: 58 points
Work Items: 1001 → 1002 → 1003 → 1004 → 1005
```

### Circular Dependencies Alert

If circular dependencies are detected:

- **Error-level alert** (red) with clear warning
- List of all detected cycles
- Description of each cycle path

Example:
```
🔄 Circular Dependencies Detected
1 circular dependency cycle(s) found. These create deadlocks and must be resolved immediately.
• Circular dependency detected: 5001 → 5002 → 5003 → 5001
```

### Blocking Work Items Alert

If blocking items are found:

- **Error-level alert** listing all blocking work item IDs
- Indication that these items prevent progress on others

### Work Items Table

Displays all work items with dependencies:

| Column | Description |
|--------|-------------|
| ID | Work item TFS ID |
| Title | Work item title |
| Type | Epic, Feature, Task, etc. |
| State | Current workflow state |
| Effort | Story points (if available) |
| Dependencies | Count of items this depends on |
| Dependents | Count of items depending on this |
| Status | Visual indicator (Blocking/Has Dependencies/Normal) |

### Dependency Links Table

Shows all relationships between work items:

| Column | Description |
|--------|-------------|
| Source ID | ID of the work item with the dependency |
| Target ID | ID of the work item being referenced |
| Link Type | Type of relationship (color-coded chip) |
| Description | Full TFS link type name |

---

## Analysis Algorithms

### Critical Path Detection

1. Build adjacency list from dependency links (DependsOn type only)
2. Perform depth-first search (DFS) from each node
3. Track all complete paths through the graph
4. Calculate total effort for each path
5. Determine risk level based on:
   - Chain length ≥ 5 or effort ≥ 50: **Critical**
   - Chain length ≥ 4 or effort ≥ 30: **High**
   - Chain length ≥ 3 or effort ≥ 15: **Medium**
   - Otherwise: **Low**
6. Return top 5 longest chains

### Circular Dependency Detection

1. Build adjacency list from dependency and blocking links
2. Use DFS with recursion stack to detect cycles
3. When a back edge is found (node in recursion stack):
   - Extract the cycle path
   - Deduplicate cycles
   - Create CircularDependency record
4. Return all unique cycles

### Blocking Item Identification

Work items are marked as blocking if:
- They have at least one Dependency-Reverse link (blocks another item)
- They have at least one item depending on them

---

## API Endpoints

### GET /api/workitems/dependency-graph

Returns the complete dependency graph with optional filters.

**Query Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| areaPathFilter | string | No | Filter by area path (partial match) |
| workItemIds | string | No | Comma-separated list of work item IDs |
| workItemTypes | string | No | Comma-separated list of work item types |

**Response:** `DependencyGraphDto`

**Status Codes:**
- 200 OK - Graph returned successfully
- 400 Bad Request - Invalid filter parameters
- 500 Internal Server Error - Processing error

---

## Architecture Compliance

### Layer Separation

- **Core Layer**: Query and DTO definitions
  - `GetDependencyGraphQuery.cs`
  - `DependencyGraphDto.cs`

- **API Layer**: Handler and controller
  - `GetDependencyGraphQueryHandler.cs`
  - `WorkItemsController.cs` (dependency-graph endpoint)

- **Client Layer**: Blazor UI component
  - `DependencyGraph.razor`

### Data Flow

1. User applies filters in Blazor UI
2. UI calls API client (generated via NSwag)
3. API controller validates and forwards to Mediator
4. Handler queries repository for work items
5. Handler applies filters (area path, IDs, types)
6. Handler builds graph structure:
   - Parse JSON relations from work items
   - Build nodes with dependency counts
   - Create links between work items
   - Detect critical paths
   - Detect circular dependencies
   - Identify blocking items
7. Handler returns DependencyGraphDto
8. UI renders results in hierarchical view

### Caching Strategy

- Work items are cached locally per `docs/ARCHITECTURE_RULES.md`
- Dependency graph is computed on-demand from cached data
- No additional caching of graph results
- Graph analysis timestamp included in response

---

## Testing Requirements

### Integration Tests

Located in: `PoTool.Tests.Integration/Features/DependencyGraphController.feature`

Scenarios covered:
- Basic dependency graph retrieval
- Area path filtering
- Work item type filtering
- Work item ID filtering
- Circular dependency detection
- Critical path calculation
- Blocking item identification
- Invalid parameter handling

### Unit Tests

Located in: `PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`

Scenarios covered:
- Empty work item set
- Basic dependencies
- Area path filtering
- Work item type filtering
- Work item ID filtering
- Circular dependency detection
- Long dependency chains (critical paths)
- Blocking work item identification
- Parent-child hierarchy links
- Invalid JSON payload handling
- Missing target work items

### Test Data Requirements

Tests use:
- Minimal JSON payloads with TFS relation structures
- Work items with known dependency patterns
- Circular dependency scenarios
- Long chains for critical path testing

---

## User Guidance

### Context-Aware Help

The page includes:

1. **Data Requirements**
   - Parent-Child Links (hierarchy)
   - Related Links (predecessor/successor)
   - State (for blocking analysis)
   - Effort/Story Points (for critical path calculation)

2. **Best Practices**
   - Keep chains shallow (max 4-5 levels)
   - Use predecessor links for explicit blocking
   - Prioritize high-blocker-impact items
   - Review graph during release planning
   - Break circular dependencies immediately

3. **Common Issues**
   - Very long chains (>5 levels) - break down work
   - Many blocking items - reduce coupling
   - Critical path longer than expected - parallelize work
   - No dependencies visible - ensure links are set in TFS

---

## Future Enhancements

If approved in the future:

1. **Graph visualization library** - If an approved OSS Blazor library adds graph support
2. **Hierarchical tree view** - Expandable/collapsible dependency tree
3. **Interactive filtering** - Click on node to show only related items
4. **Export capabilities** - Export graph data to CSV or JSON
5. **Historical trends** - Track how dependency metrics change over time

All future enhancements must comply with `docs/UI_RULES.md` and `docs/ARCHITECTURE_RULES.md`.

---

## References

- UX Principles: `docs/UX_PRINCIPLES.md`
- UI Rules: `docs/UI_RULES.md`
- Architecture Rules: `docs/ARCHITECTURE_RULES.md`
- Approved Component Libraries: MudBlazor (MIT), Radzen Blazor (MIT), Fluent UI Blazor (MIT)
