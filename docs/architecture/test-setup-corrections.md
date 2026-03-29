# Test Setup Corrections

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Scope: analysis only; no code changes

## Summary

Two unit-test areas have drifted away from current runtime expectations:

1. **Dependency graph tests** still construct `WorkItemDto` as if the handler parses relationship JSON from an old payload input, but the handler now reads the typed `workItem.Relations` collection directly.
2. **WorkItemSelectionService tests** still construct `TreeNode` instances without `JsonPayload`, but the service now deserializes the selected work item from `node.JsonPayload` before it can update selection state.

The result is broken setup, not broken production logic:

- dependency links, blockers, critical paths, and cycles are missing in tests because `Relations` is never populated
- selection remains empty or unchanged in tests because nodes do not carry the serialized work item payload the service expects

## Dependency Graph Tests

### Current runtime expectation

`GetDependencyGraphQueryHandler` now uses the typed `Relations` property on `WorkItemDto`:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs:94-129`

Relevant behavior:

- `var relations = workItem.Relations ?? new List<WorkItemRelation>();`
- dependency counts are computed from `relations`
- links are built from `relations`
- blocking detection is based on reverse dependency relations in `relations`

The handler no longer parses ad hoc JSON in tests.

### Current test setup

`GetDependencyGraphQueryHandlerTests` still uses a helper shaped around a `jsonPayload` argument:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs:334-349`

Helper signature:

- `CreateWorkItem(int tfsId, string type, string state, string areaPath, int? effort, string jsonPayload)`

But the helper does **not** use `jsonPayload` at all when constructing `WorkItemDto`.

That means all relation-bearing tests are actually creating work items with:

- `Relations == null`

### Exact mismatch

Tests assume:

- supplying a JSON string like `{"relations":[...]}` is enough to describe dependencies

Production code expects:

- `WorkItemDto.Relations` to already contain `WorkItemRelation` objects

So the mismatch is:

- **test setup uses obsolete input shape**
- **handler uses current typed relation shape**

### Affected tests

Focused local run:

- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~GetDependencyGraphQueryHandlerTests|FullyQualifiedName~WorkItemSelectionServiceTests" -v minimal`
- result includes **5 failing dependency graph tests**

Affected dependency-graph tests:

1. `Handle_WithBasicDependencies_BuildsGraphCorrectly`
2. `Handle_WithCircularDependencies_DetectsCircles`
3. `Handle_WithLongDependencyChain_FindsCriticalPaths`
4. `Handle_WithBlockingWorkItems_IdentifiesBlockers`
5. `Handle_WithParentChildLinks_CreatesHierarchyLinks`

These fail because they require relations to exist.

### Tests not actually broken by this drift

The following dependency-graph tests still pass because they do not depend on populated relations:

- `Handle_WithNoWorkItems_ReturnsEmptyGraph`
- `Handle_WithAreaPathFilter_FiltersCorrectly`
- `Handle_WithWorkItemTypeFilter_FiltersCorrectly`
- `Handle_WithWorkItemIdsFilter_FiltersCorrectly`
- `Handle_WithNoRelations_HandlesGracefully`
- `Handle_WithMissingTargetWorkItem_IgnoresLink`

Notably:

- `Handle_WithNoRelations_HandlesGracefully` still passes because `Relations == null` maps to the intended “no relations” path
- `Handle_WithMissingTargetWorkItem_IgnoresLink` currently passes for the wrong reason if relations are absent; once setup is corrected it should still pass, but for the intended reason (relation exists, target missing, link ignored)

### What is missing

The test helper must construct `WorkItemDto.Relations` explicitly, using `WorkItemRelation` instances that match handler expectations.

The `jsonPayload` helper parameter is now stale test scaffolding.

### Minimal valid fix

Update the local test helper in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`

Specifically:

1. Replace the `jsonPayload` parameter with a typed relation input, such as:
   - `IEnumerable<WorkItemRelation>? relations = null`
2. Pass `Relations: relations?.ToList()` into the `WorkItemDto` constructor
3. Rewrite relation-bearing test cases to create `new WorkItemRelation { LinkType = ..., TargetWorkItemId = ... }`
4. Keep relation-free tests passing `null` or an empty list

### Shared test utility needed?

Probably **no**.

Reason:

- the drift is localized to one test file
- the relation patterns are small and explicit
- a local helper update is enough to make the tests valid again without introducing extra abstraction

## Selection Service Tests

### Current runtime expectation

`WorkItemSelectionService` requires `TreeNode.JsonPayload` to be populated with serialized `WorkItemDto` content:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkItemSelectionService.cs:30-131`

Relevant behavior:

- `ToggleNodeSelection(...)` returns the current state unchanged if `node.JsonPayload` is null or empty
- otherwise it deserializes `JsonPayload` into `WorkItemDto`
- `SelectAllNodes(...)` adds IDs for all nodes, but only adds `SelectedWorkItems` when `JsonPayload` exists and deserializes successfully
- keyboard navigation depends on `ToggleNodeSelection(...)`, so movement-based selection also fails if payload is missing

### How nodes are created at runtime

`TreeBuilderService` populates `JsonPayload` when building nodes:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TreeBuilderService.cs:28-47`

Runtime node creation includes:

- `JsonPayload = System.Text.Json.JsonSerializer.Serialize(dto)`

So production tree nodes are expected to carry serialized work item data.

### Current test setup

`WorkItemSelectionServiceTests` uses this helper:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs:22-48`

The helper creates:

- a `WorkItemDto` object in memory
- a `TreeNode` with `Id`, `Title`, `Type`, `State`, `Children`

But it never assigns:

- `node.JsonPayload`

So the helper builds nodes that do **not** match runtime tree-builder output.

### Exact mismatch

Tests assume:

- `TreeNode.Id`, `Title`, `Type`, and `State` are enough for selection operations

Production code expects:

- `TreeNode.JsonPayload` to contain a serialized `WorkItemDto`

So the mismatch is:

- **test nodes are structurally incomplete for current selection logic**
- **service behavior is now payload-driven, while tests still use a pre-payload node shape**

### Why selection state remains empty

Because `JsonPayload` is missing:

- `ToggleNodeSelection(...)` exits early and returns the original state unchanged
- `SelectAllNodes(...)` records selected IDs, but never materializes `SelectedWorkItems`
- arrow-key navigation calls `ToggleNodeSelection(...)` on the target node, so primary selection never advances

This matches the current failures exactly.

### Affected tests

Focused local run shows **4 failing WorkItemSelectionService tests**:

1. `ToggleNodeSelection_SelectsNewNode`
2. `SelectAllNodes_SelectsAllVisibleNodes`
3. `HandleKeyboardNavigation_ArrowDown_SelectsNextItem`
4. `HandleKeyboardNavigation_ArrowUp_SelectsPreviousItem`

### Tests not affected

The remaining selection-service tests still pass because they do not require deserialization from `JsonPayload`:

- `SelectAllNodes_EmptyList_ReturnsEmptyState`
- `ClearSelection_ClearsAllSelections`
- `HandleKeyboardNavigation_ArrowRight_OnCollapsedNode_ReturnsNodeToToggle`
- `HandleKeyboardNavigation_ArrowLeft_OnExpandedNode_ReturnsNodeToToggle`
- `BuildFlatNodeList_FlattensExpandedTree`
- `BuildFlatNodeList_ExcludesCollapsedChildren`

These tests mostly exercise tree navigation or flattening mechanics rather than selection materialization.

### Minimal valid fix

Update the local helper in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs`

Specifically:

1. Keep building the `WorkItemDto`
2. Serialize it using `JsonSerializer.Serialize(workItem)`
3. Assign the serialized string to `node.JsonPayload`
4. Ensure child nodes created through the same helper also get payloads automatically

That is sufficient to make the helper match runtime `TreeBuilderService` output.

### Shared test utility needed?

Probably **no**.

Reason:

- only one test file is drifting here
- the existing local helper already centralizes node construction
- adding `JsonPayload` in that helper is the smallest valid correction

## Root Cause

The unified root cause is:

> **Both test suites are constructing outdated input shapes after production code moved to strongly typed/runtime-populated fields.**

Specifically:

- dependency graph tests still behave as if relationship data is supplied indirectly through JSON-like input, but the handler now consumes `WorkItemDto.Relations`
- selection service tests still behave as if tree nodes are self-sufficient from visible fields alone, but the service now consumes `TreeNode.JsonPayload`

This is test drift caused by production refactoring toward typed/runtime-prepared data.

There is no evidence here of a deeper production bug.

## Fix Plan

### 1. Correct dependency-graph test helper

File:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`

Required changes:

1. Replace the obsolete `jsonPayload` helper argument with typed relations input
2. Construct `WorkItemDto.Relations` explicitly in the helper
3. Update the 5 relation-dependent tests to pass `WorkItemRelation` objects instead of JSON strings
4. Leave non-relation tests using `null`/empty relations

Suggested shape:

- helper accepts `IEnumerable<WorkItemRelation>? relations = null`
- helper assigns `Relations: relations?.ToList()`

### 2. Correct selection-service node helper

File:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs`

Required changes:

1. Serialize the test `WorkItemDto`
2. Set `node.JsonPayload` in `CreateTestNode(...)`
3. Keep the rest of the helper unchanged

Suggested shape:

- `JsonPayload = JsonSerializer.Serialize(workItem)`

### 3. Do not introduce shared test utilities unless another file needs the same fix

At current scope, shared utilities are **not required**.

Why:

- both drift points are local to one helper per file
- the minimum valid change is to repair each existing helper in place
- introducing new shared builders would be larger than necessary for the current failure cluster

If additional files are later found to repeat the same stale construction pattern, then shared builders may become worthwhile.

## Impact

Expected directly fixed tests:

- **5 dependency-graph tests**
- **4 WorkItemSelectionService tests**
- **9 tests total**

Files affected by the eventual test-only fix:

1. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs`
2. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs`

Recent workflow context:

- recent GitHub Actions runs visible from MCP are successful or in progress; no failed job logs were available in the most recent completed run checked
- the actionable evidence for this prompt comes from the focused local unit-test run above

## Final Verdict

**Test drift resolved / no deeper change required**

The failures are explained by stale test setup, not by incorrect runtime behavior.

- dependency graph tests need typed `Relations`
- selection service tests need `JsonPayload`
- local helper corrections should be enough
- no production-code change is indicated by the evidence gathered here
