# PoTool Unit Test Speed Audit

## Summary
- Baseline measurement on 2026-03-14 used `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --logger "trx;LogFileName=unit-speed.trx"` and recorded **1,176 executed tests** with **36 pre-existing failures** outside this audit.
- **Total suspiciously slow tests:** **16** tests exceeded the 1 second threshold.
- **Estimated top slowest classes by summed TRX duration:**
  - `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs` — **118.7s** summed duration across 12 tests, including 8 tests over 1 second
  - `PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs` — **19.8s** across 3 tests
  - `PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs` — **17.1s** across 13 tests
  - `PoTool.Tests.Unit/WorkItemExplorerTests.cs` — **3.8s** across 10 tests
  - `PoTool.Tests.Unit/TfsClientTests.cs` — **3.3s** across 11 tests
- **Overall suite drag assessment:** severe but highly concentrated. The slowest behavior is not spread across core analytics tests; it is dominated by a small cluster of mock-data and TFS-client-adjacent tests. The Battleship/mock-data path alone contributes about **159.4s of 176.9s summed test duration (~90%)**, so developers pay most of the speed tax in a narrow, non-core area.

## Tests Over 1 Second
- **file:** `PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs`
  - **test name:** `GenerateHierarchy_Should_Enforce_Area_Path_Inheritance_From_Epic` — **15.960s**
  - **likely cause:** Large dataset setup plus expensive recursive descendant traversal. The test regenerates the full Battleship hierarchy, and the generator creates roughly 20k work items through deep nested loops (`PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs:24-241`). The test then recursively walks descendants for every epic (`PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs:125-141`, `:209-221`).
  - **recommendation:** **Should be optimized.** This is generator-specific behavior, but it should be proven with a much smaller fixture or a cached hierarchy instead of a full graph audit.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_Hierarchy_Integrity` — **15.924s**
  - **likely cause:** Full hierarchy generation followed by validator-wide scanning of the entire graph. The test calls `GenerateHierarchy()` and then `ValidateWorkItems(...)`, which runs multiple full-list validations (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:45-58`; `PoTool.Api/Services/MockData/MockDataValidator.cs:16-52`, `:132-155`).
  - **recommendation:** **Should be optimized.** The assertion is legitimate, but re-running the full validator on the largest possible fixture is too expensive for a unit test.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_Hierarchy_Quantities` — **15.298s**
  - **likely cause:** Duplicate broad-scenario coverage. The test regenerates the same full hierarchy and runs the same validator pass just to assert quantity flags (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:26-42`; `PoTool.Api/Services/MockData/MockDataValidator.cs:20-31`, `:120-130`).
  - **recommendation:** **Should be removed.** Generator count/range behavior is already tested directly in `BattleshipWorkItemGeneratorTests`, so this repeats a whole-graph scenario at very high cost.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_Area_Path_Consistency` — **15.207s**
  - **likely cause:** Full hierarchy generation plus the validator's recursive descendant scan per epic. `ValidateAreaPathConsistency(...)` calls `GetDescendants(...)` for each epic and repeatedly filters the full item list (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:61-74`; `PoTool.Api/Services/MockData/MockDataValidator.cs:157-187`).
  - **recommendation:** **Should be optimized.** This is one of the clearest O(N²)-style hotspots in the suite.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_States` — **14.811s**
  - **likely cause:** Large dataset setup plus broad validator pass over every generated item (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:77-90`; `PoTool.Api/Services/MockData/MockDataValidator.cs:200-224`).
  - **recommendation:** **Should be optimized.** A smaller representative fixture would exercise state validation much faster.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_Fibonacci_Estimation` — **14.760s**
  - **likely cause:** Full hierarchy generation plus full PBI/bug scan for estimation checks (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:93-104`; `PoTool.Api/Services/MockData/MockDataValidator.cs:226-244`).
  - **recommendation:** **Should be optimized.** The behavior is narrow, but the setup is extremely broad.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Validate_Battleship_Theme` — **14.731s**
  - **likely cause:** Duplicate use of the full generated hierarchy for a lightweight content check (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:121-132`; `PoTool.Api/Services/MockData/MockDataValidator.cs:246-260`).
  - **recommendation:** **Should be removed.** Theme compliance is low-value compared with the runtime cost and is already indirectly asserted by generator/theme tests.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidateWorkItems_Should_Check_Unestimated_Percentage` — **14.526s**
  - **likely cause:** Full hierarchy generation and broad validation to check one percentage field (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:107-118`; `PoTool.Api/Services/MockData/MockDataValidator.cs:229-238`).
  - **recommendation:** **Should be optimized.** This is valuable only if kept as a much narrower data-driven check.

- **file:** `PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
  - **test name:** `ValidationReport_GetSummary_Should_Return_Formatted_Report` — **13.457s**
  - **likely cause:** Duplicate scenario coverage. The test regenerates and validates the entire hierarchy only to check that the output string contains a few headings (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:203-217`; `PoTool.Api/Services/MockData/MockDataValidator.cs:356+`).
  - **recommendation:** **Should be removed.** The formatting assertion does not justify a 13-second full-graph setup.

- **file:** `PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs`
  - **test name:** `MockTfsClient_GetWorkItemsByRootIdsAsync_IncrementalSync_StillDiscoversUnchangedDescendants` — **9.754s**
  - **likely cause:** Real DI/container bootstrapping plus heavy graph discovery over the full Battleship hierarchy. Each test builds a `ServiceCollection`, resolves a real `BattleshipMockDataFacade`, generates the full hierarchy, and then runs recursive descendant discovery twice (`PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs:181-234`; `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs:67-89`; `PoTool.Api/Services/MockTfsClient.cs:87-146`).
  - **recommendation:** **Should be converted to integration test.** It exercises multiple real services together and behaves more like a component/integration path than a focused unit test.

- **file:** `PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs`
  - **test name:** `MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromGoal` — **7.790s**
  - **likely cause:** Same heavy DI setup and full hierarchy traversal from a root goal (`PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs:30-83`; `PoTool.Api/Services/MockTfsClient.cs:97-130`).
  - **recommendation:** **Should be converted to integration test.** It is valuable as an end-to-end graph discovery check, but it is too broad and expensive for the unit suite.

- **file:** `PoTool.Tests.Unit/WorkItemExplorerTests.cs`
  - **test name:** `Filter_Includes_Ancestors_For_Match` — **3.266s**
  - **likely cause:** Large setup for a very weak assertion. The test constructs a full mock-data facade, loads all items from `DevWorkItemRepository`, and walks parent chains across the full graph, but only asserts that `toInclude.Any()` (`PoTool.Tests.Unit/WorkItemExplorerTests.cs:25-42`, `:73-108`).
  - **recommendation:** **Should be removed.** This is the clearest example of high cost and very low signal.

- **file:** `PoTool.Tests.Unit/Services/RealTfsClientErrorHandlingTests.cs`
  - **test name:** `ExecuteWithRetryAsync_RetriesOnServerError` — **2.982s**
  - **likely cause:** Excessive async waiting. The production retry helper uses exponential backoff with jitter and the test invokes the real private retry method via reflection, so the test appears to wait for the real retry delay (`PoTool.Tests.Unit/Services/RealTfsClientErrorHandlingTests.cs:57-77`, `:105-113`; `PoTool.Integrations.Tfs/Clients/RealTfsClient.Infrastructure.cs:97-160`).
  - **recommendation:** **Should be optimized.** Unit tests should not sleep for production backoff timing.

- **file:** `PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs`
  - **test name:** `MockTfsClient_GetWorkItemsByRootIdsAsync_FetchesDescendantsFromObjective_NotParentGoal` — **2.257s**
  - **likely cause:** Same integration-like container/facade setup with full hierarchy generation for a narrower descendant assertion (`PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs:90-138`; `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs:67-89`).
  - **recommendation:** **Should be converted to integration test.** The cost comes from exercising the full service graph rather than isolated unit logic.

- **file:** `PoTool.Tests.Unit/TfsClientTests.cs`
  - **test name:** `GetWorkItemsAsync_ParsesParentId_WhenParentExists` — **1.550s**
  - **likely cause:** Fake-but-heavy persistence and request setup. Each test builds a real in-memory `PoToolDbContext`, real `TfsConfigurationService`, real `TfsRequestThrottler`, real `TfsRequestSender`, and a `RealTfsClient` before making mocked HTTP calls (`PoTool.Tests.Unit/TfsClientTests.cs:28-69`, `:80-158`).
  - **recommendation:** **Should be optimized.** The parsing behavior is unit-scale, but the setup is much heavier than the assertion.

- **file:** `PoTool.Tests.Unit/TfsClientTests.cs`
  - **test name:** `GetWorkItemsAsync_HandlesMultipleLevelsOfHierarchy` — **1.548s**
  - **likely cause:** Same heavy client/config/service bootstrapping as the previous test, plus larger mocked hierarchy payloads (`PoTool.Tests.Unit/TfsClientTests.cs:28-69`, `:160-259`).
  - **recommendation:** **Should be optimized.** Keep the behavior covered, but not through a full client stack per test.

## Structural Causes of Slowness
- **heavy setup patterns**
  - `BattleshipWorkItemGenerator.GenerateHierarchy()` builds roughly 20k work items using deep nested loops every time it is called (`PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs:24-241`).
  - Many slow tests call it directly in the test body instead of reusing a shared fixture or a smaller targeted dataset (`PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs:26-32`, `:45-51`, `:61-67`, `:77-83`, `:93-99`, `:107-113`, `:121-127`; `PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs:20-206`).

- **repeated bootstrapping**
  - `WorkItemHierarchyRetrievalTests` rebuilds a real `ServiceCollection` and service provider in each test (`PoTool.Tests.Unit/Services/WorkItemHierarchyRetrievalTests.cs:33-45`, `:93-105`, `:182-194`).
  - `TfsClientTests` rebuilds a real in-memory EF configuration service, real throttler, real request sender, and `RealTfsClient` in each setup (`PoTool.Tests.Unit/TfsClientTests.cs:28-69`).

- **fake integration behavior**
  - `WorkItemHierarchyRetrievalTests` and `WorkItemExplorerTests` behave like component/integration tests because they wire multiple real services and use the full Battleship graph instead of isolated fixtures.
  - `TfsClientTests` are nominally unit tests but still exercise an in-memory database plus multiple production infrastructure components.
  - `RealTfsClientErrorHandlingTests` uses the real retry helper with real delay behavior, which is integration-like timing inside a unit test.

- **unnecessary breadth**
  - `MockDataValidatorTests` repeats the same full-hierarchy setup for many narrow assertions and re-validates the whole graph every time.
  - Some tests provide very little signal relative to their cost:
    - `ValidationReport_GetSummary_Should_Return_Formatted_Report`
    - `ValidateWorkItems_Should_Validate_Hierarchy_Quantities`
    - `ValidateWorkItems_Should_Validate_Battleship_Theme`
    - `Filter_Includes_Ancestors_For_Match`
  - The area-path checks are especially expensive because both test and production helper recurse and repeatedly search the full list for descendants (`PoTool.Api/Services/MockData/MockDataValidator.cs:157-187`; `PoTool.Tests.Unit/Services/MockData/BattleshipWorkItemGeneratorTests.cs:125-141`, `:209-221`).

## Best Candidates for Optimization or Removal
1. **`MockDataValidatorTests` full-hierarchy validations**
   - Highest impact by far: ~67% of summed suite duration on one class.
   - Best starting point: remove/merge the low-value full-graph checks and stop regenerating the full Battleship hierarchy per assertion.
2. **`BattleshipWorkItemGeneratorTests.GenerateHierarchy_Should_Enforce_Area_Path_Inheritance_From_Epic`**
   - A single 15.96s test driven by full-graph descendant recursion.
3. **`WorkItemHierarchyRetrievalTests`**
   - Three tests cost ~19.8s total and are really end-to-end graph discovery checks over a fully wired mock service stack.
4. **`WorkItemExplorerTests.Filter_Includes_Ancestors_For_Match`**
   - Very weak assertion for a 3.27s runtime.
5. **`RealTfsClientErrorHandlingTests.ExecuteWithRetryAsync_RetriesOnServerError`**
   - Almost 3 seconds spent waiting on retry/backoff in a unit test.
6. **`TfsClientTests` relation-parsing tests**
   - Individually just above threshold, but their setup pattern suggests further growth will keep adding drag unless slimmed down.

## Recommended Speed Strategy
- **Trim the Battleship/mock-data audit path first.** That area is responsible for the overwhelming majority of slow-test cost and is not the main business-critical analytics surface.
- **Stop using the full 20k-item hierarchy for narrow assertions.** Replace full-hierarchy tests with smaller targeted fixtures, or reuse a cached static fixture where a large graph is actually needed.
- **Collapse duplicate validator coverage.** Keep one or two broad smoke tests at most; move quantity/theme/summary assertions off the full-hierarchy path or remove them.
- **Move integration-like graph-discovery tests out of the unit suite.** `WorkItemHierarchyRetrievalTests` is the clearest candidate.
- **Eliminate real waiting in unit tests.** Retry tests should assert policy without sleeping for production backoff.
- **Reduce infrastructure bootstrapping inside parsing tests.** TFS client relation parsing should not need a full EF-backed configuration service and production request stack per test.
