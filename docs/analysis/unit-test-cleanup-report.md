> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# PoTool Unit Test Cleanup Report

## Summary
- tests removed: 16
- tests merged: 3 forecast-confidence tests into 1 data-driven test
- tests simplified: none beyond removing redundant assertions from the unit suite
- tests reclassified/flagged for integration: `WorkItemHierarchyRetrievalTests`
- CDC tests added or strengthened: 2 direct `CanonicalStoryPointResolutionServiceTests` edge-case tests

## Safe Deletions Performed
For each:
- file: `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
  - test name(s):
    - `ToDefinition_MapsSprintEntityToMinimalDomainInput`
    - `ToCanonicalWorkItem_MapsWorkItemEntityToMinimalMetricsDomainInput`
    - `ToCanonicalWorkItem_MapsWorkItemDtoToMinimalMetricsDomainInput`
  - reason: pure property-copy coverage with no normalization or canonical analytics semantics
- file: `PoTool.Tests.Unit/Services/StateClassificationInputMapperTests.cs`
  - test name(s):
    - `ToDomainStateClassification_MapsTransportClassificationIntoDomainInput`
    - `ToDto_MapsDomainDefaultClassificationBackToTransportContract`
  - reason: trivial mapper symmetry already protected by compile-time contracts and stronger semantic tests
- file: `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
  - test name(s):
    - `Resolve_ReturnsExpectedEstimateSourceClassification`
  - reason: summary assertion fully overlapped by the focused source-specific tests in the same CDC suite
- file: `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - test name(s):
    - `Constructor_AllowsInjectedHierarchyRollupDouble`
  - reason: constructor-shape coverage did not protect behavior
- file: `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - test name(s):
    - `ComputeProjectionsAsync_AllowsInjectedCanonicalServiceDoubles`
  - reason: injected-double smoke coverage did not protect projection semantics
- file: `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - test name(s):
    - `ValidateWorkItems_Should_Validate_Hierarchy_Quantities`
    - `ValidateWorkItems_Should_Validate_Hierarchy_Integrity`
    - `ValidateWorkItems_Should_Validate_Battleship_Theme`
    - `ValidationReport_GetSummary_Should_Return_Formatted_Report`
  - reason: slow full-hierarchy assertions with low additional signal relative to generator coverage and other validator checks
- file: `PoTool.Tests.Unit/WorkItemExplorerTests.cs`
  - test name(s):
    - `Filter_Includes_Ancestors_For_Match`
  - reason: expensive mock-data setup with only a non-empty assertion

## Merges and Simplifications
For each:
- file: `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - old tests affected:
    - `Handle_WithLowHistoricalData_ReturnsLowConfidence`
    - `Handle_WithMediumHistoricalData_ReturnsMediumConfidence`
    - `Handle_WithHighHistoricalData_ReturnsHighConfidence`
  - new consolidated form: `Handle_WithHistoricalDataThresholds_ReturnsExpectedConfidence`
  - reason: confidence is a simple threshold table; a single data-driven test keeps the handler regression while removing repeated setup

## Slow Test Reductions
For each:
- file: `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - test name(s):
    - `ValidateWorkItems_Should_Validate_Hierarchy_Quantities`
    - `ValidateWorkItems_Should_Validate_Hierarchy_Integrity`
    - `ValidateWorkItems_Should_Validate_Battleship_Theme`
    - `ValidationReport_GetSummary_Should_Return_Formatted_Report`
  - action taken: deleted from the unit suite
  - expected impact: removes repeated full Battleship hierarchy generation for low-signal assertions
- file: `PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs`
  - test name(s):
    - `MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromGoal`
    - `MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromObjective_NotParentGoal`
    - `MockTfsClient_GetWorkItemsByRootIdsAsync_IncrementalSync_StillDiscoversUnchangedDescendants`
  - action taken: removed from the unit suite and flagged as integration-style coverage
  - expected impact: removes expensive DI + full-graph traversal tests from the fast unit path
- file: `PoTool.Tests.Unit/WorkItemExplorerTests.cs`
  - test name(s):
    - `Filter_Includes_Ancestors_For_Match`
  - action taken: deleted from the unit suite
  - expected impact: removes a high-cost/low-signal mock-data test

## Coverage Rebalancing
Coverage shifted slightly toward the CDC by strengthening `CanonicalStoryPointResolutionServiceTests` for derived-estimate edge cases before deleting duplicate higher-layer assertions. The remaining handler and projection tests continue to verify orchestration and regression-critical consuming paths, while the removed tests were mostly transport copying, constructor shape, or slow mock-data plumbing.

## Remaining Intentional Redundancy
List any tests intentionally kept at higher layers as regression guards and explain why.
- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - kept to preserve one persisted-input regression for historical commitment and first-Done consumption through the handler path
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - kept to preserve orchestration coverage for canonical Done mapping, spillover, and added/removed scope over stored sprint data
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - kept as the main projection regression net for realistic event-driven consumption, hierarchy propagation, and visibility behavior the CDC tests cannot see alone

## Final Assessment
State whether the unit suite is now:
- leaner
- faster
- more focused on critical semantics
