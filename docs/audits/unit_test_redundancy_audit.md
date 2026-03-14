# PoTool Unit Test Redundancy Audit

## Summary
- **Likely redundant tests:** the clearest duplication occurs when CDC semantics are re-asserted in handler and projection tests with nearly identical scenarios, rather than within the CDC files themselves. The biggest overlap clusters are:
  - first-Done / canonical Done behavior across `HistoricalSprintLookupTests`, `GetSprintMetricsQueryHandlerTests`, `GetSprintExecutionQueryHandlerTests`, and `SprintTrendProjectionServiceTests`
  - story-point resolution and fallback behavior across `CanonicalStoryPointResolutionServiceTests`, `GetSprintMetricsQueryHandlerTests`, `GetEpicCompletionForecastQueryHandlerTests`, and `SprintTrendProjectionServiceTests`
  - hierarchy rollup rules across `HierarchyRollupServiceTests`, `GetEpicCompletionForecastQueryHandlerTests`, and `SprintTrendProjectionServiceTests`
- **Likely low-value tests:** the thinnest coverage is simple mapper/property-copy tests, constructor/injected-double tests, and DI-resolution tests that prove resolvability more than business behavior.
- **Likely fragile tests:** the most fragile tests are the large handler/projection scenarios that assert many output fields at once after large arrangements. They protect meaningful paths, but they are expensive to maintain and likely to fail on harmless refactors.
- **Audit stance:** no tests were deleted or modified as part of this issue. This report only classifies likely cleanup targets.

## Safe Removal Candidates
- **file:** `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
  - **test name:** `ToDefinition_MapsSprintEntityToMinimalDomainInput`
  - **reason:** Pure property-copy coverage with no normalization or domain-rule behavior. If the DTO/domain contract changes, compile errors and consuming tests will catch it.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
  - **test name:** `ToCanonicalWorkItem_MapsWorkItemEntityToMinimalMetricsDomainInput`
  - **reason:** Verifies direct field assignment only. The semantic rules around canonical story points are already exercised much more meaningfully in CDC, handler, and projection tests.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
  - **test name:** `ToCanonicalWorkItem_MapsWorkItemDtoToMinimalMetricsDomainInput`
  - **reason:** Another mechanical mapping test with no rule interpretation. It adds maintenance cost without protecting important behavior.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/StateClassificationInputMapperTests.cs`
  - **test name:** `ToDomainStateClassification_MapsTransportClassificationIntoDomainInput`
  - **reason:** Verifies trivial transport-to-domain copying. The meaningful classification semantics are covered in `HistoricalSprintLookupTests` and downstream consuming tests.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/StateClassificationInputMapperTests.cs`
  - **test name:** `ToDto_MapsDomainDefaultClassificationBackToTransportContract`
  - **reason:** Symmetric property-mapping test with no behavioral signal beyond "fields are assigned".
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
  - **test name:** `Resolve_ReturnsExpectedEstimateSourceClassification`
  - **reason:** Fully overlaps the individual tests in the same file that already prove Real, Fallback, Missing, and Derived classification separately. It is a summary assertion, not new coverage.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - **test name:** `Constructor_AllowsInjectedHierarchyRollupDouble`
  - **reason:** Verifies constructor shape/injectability rather than observable behavior. DI registration and behavioral forecast tests already cover more meaningful regression boundaries.
  - **confidence level:** High
- **file:** `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - **test name:** `ComputeProjectionsAsync_AllowsInjectedCanonicalServiceDoubles`
  - **reason:** Another injected-double/constructor-shape test that only proves the service can be created and return an empty result for empty input. It does not protect projection semantics.
  - **confidence level:** Medium

## Merge Candidates
- **file:** `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - **related overlapping tests:**
    - `AddPoToolApiServices_RegistersTfsConfigurationService_AsConcreteType`
    - `AddPoToolApiServices_RegistersWorkItemStateClassificationService_Successfully`
  - **suggested consolidation:** Replace the duplicated setup/build/resolve pattern with one parameterized DI resolution contract test. Keep a single focused assertion that the registration graph builds and the intended service types resolve.
- **file:** `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - **related overlapping tests:**
    - `Handle_WithLowHistoricalData_ReturnsLowConfidence`
    - `Handle_WithMediumHistoricalData_ReturnsMediumConfidence`
    - `Handle_WithHighHistoricalData_ReturnsHighConfidence`
  - **suggested consolidation:** Convert to one data-driven test over iteration-count thresholds. The behavioral rule is a simple threshold table, not three meaningfully different scenarios.
- **file:** `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - **related overlapping tests:**
    - `Handle_UsesBusinessValueFallbackForSprintStoryPoints`
    - `Handle_TreatsZeroDonePbiAsValidZeroPointDelivery`
    - `Handle_TreatsZeroNonDonePbiAsMissingEstimate`
  - **suggested consolidation:** Reduce to one handler-level integration regression proving the handler consumes canonical story-point resolution, and leave detailed precedence/zero-value semantics to `CanonicalStoryPointResolutionServiceTests`.
- **file:** `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - **related overlapping tests:**
    - `Handle_UsesCanonicalDoneMapping_ForCompletedItems`
    - `Handle_DoesNotUseRawFallbackDoneStates_WhenClassificationMissing`
    - `Handle_DoesNotCountSecondDone_WhenFirstDoneWasBeforeSprint`
    - `Handle_CountsFirstDoneWithinSprint_WhenItemIsNoLongerInSprintIteration`
  - **suggested consolidation:** Keep one happy-path done-mapping regression and one historical first-Done regression. The fine-grained state/timeline rules already belong to `HistoricalSprintLookupTests`.
- **file:** `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - **related overlapping tests:**
    - `Handle_UsesFeatureFallbackOnlyWhenChildPbisLackEstimates`
    - `Handle_ExcludesBugAndTaskStoryPointsFromForecastScope`
    - `Handle_UsesFractionalDerivedStoryPointsInForecast`
  - **suggested consolidation:** Keep one forecast-level regression that proves the handler wires hierarchy rollups correctly, and rely on `HierarchyRollupServiceTests` for the detailed estimation/rollup permutations.
- **file:** `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - **related overlapping tests:**
    - `ComputeProductSprintProjection_UsesCanonicalDoneMappingForResolvedStates`
    - `ComputeProductSprintProjection_DoneReopenedDone_CountsOnlyFirstDoneDelivery`
    - `ComputeProductSprintProjection_DoneBeforeSprint_ReopenedDuringSprint_DoesNotCountDelivery`
    - `ComputeProductSprintProjection_FirstDoneInsideSprint_CountsCanonicalDelivery`
  - **suggested consolidation:** Retain one projection regression for canonical Done handling and one for first-Done delivery attribution. The rest largely repeat CDC timeline semantics through a heavier consuming path.
- **file:** `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - **related overlapping tests:**
    - `ComputeFeatureProgress_WithSprintCompletedPbiIds_ComputesSprintMetrics`
    - `ComputeFeatureProgress_WithNoSprintCompletedPbiIds_SprintMetricsAreZero`
    - `ComputeFeatureProgress_WithSprintCompletedPbiIds_EmptySet_SprintMetricsAreZero`
    - `ComputeFeatureProgress_WhenFeatureClosedInSprint_SetsSprintCompletedInSprint`
    - `ComputeFeatureProgress_WithNullSprintFilter_SprintCompletedCountsAreZero`
  - **suggested consolidation:** Collapse into a data-driven matrix for sprint-completion inputs. These tests appear to be exercising the same small decision surface with repeated setup.

## Keep but Simplify
- **file:** `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`
  - **test name:** `ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`
  - **why it is valuable:** It is the one mapper test in the file that verifies boundary normalization (`State` and `IterationPath` trimming), which is more than plain field copying.
  - **how it could be simplified:** Narrow the assertions to the normalization behavior only and avoid re-asserting every unchanged field.
- **file:** `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`
  - **test name:** `AddPoToolApiServices_RegistersCanonicalMetricsServices_ForDiConsumers`
  - **why it is valuable:** This protects the post-CDC extraction DI boundary: canonical services must remain resolvable by handler/projection consumers.
  - **how it could be simplified:** Share the repeated service-collection setup in a helper and assert only the minimum set of registrations that represent the CDC boundary.
- **file:** `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - **test name:** `Handle_UsesHistoricalCommitmentAndFirstDoneSemantics`
  - **why it is valuable:** This is a meaningful cross-layer regression because it proves the handler composes historical commitment and first-Done logic correctly against persisted events.
  - **how it could be simplified:** Reduce the scenario to the smallest arrangement that still proves one committed item, one added item, and one pre-sprint Done item instead of asserting many output fields at once.
- **file:** `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - **test name:** `Handle_ComputesCanonicalStoryPointAggregatesAndRates`
  - **why it is valuable:** It proves the handler wires canonical estimation, churn, spillover, and calculator output together correctly.
  - **how it could be simplified:** Assert only a few representative summary fields and leave exhaustive rate math to `SprintExecutionMetricsCalculatorTests`.
- **file:** `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - **test name:** `ComputeProductSprintProjection_MultiSprint_ProducesCorrectResults`
  - **why it is valuable:** Multi-sprint projection behavior is a real regression boundary and not fully replaced by CDC unit tests.
  - **how it could be simplified:** Focus assertions on the cross-sprint invariants only, and move repeated fixture creation into reusable builders/helpers.
- **file:** `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - **test name:** `ComputeFeatureProgress_WithActivityFilter_IncludesFeatureWhenOnlyTaskDescendantIsActive`
  - **why it is valuable:** It protects upward activity propagation, which is easy to break during refactors.
  - **how it could be simplified:** Use a smaller hierarchy fixture and assert just the inclusion boundary instead of multiple secondary fields.

## Keep as Strategic Regression Tests
- `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`
  - `BuildCommittedWorkItemIds_ReconstructsIterationFromDomainInputs`
  - `Build_FirstDoneDelivery_UsesMappedFieldChangeEvents`
  - `GetClassification_UsesCanonicalStateMappingsCaseInsensitively`
  - `GetStateAtTimestamp_ReconstructsHistoricalStateFromLaterEvents`
  - `BuildSpilloverWorkItemIds_UsesMappedSnapshotsAndSprintDefinitions`
  - These are the canonical low-level tests for sprint-history and state-reconstruction rules and should remain the main source of truth for those semantics.
- `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
  - Keep the focused individual tests for real points, `BusinessValue` fallback, missing estimates, zero-on-Done, zero-on-non-Done, derived estimates, and parent fallback.
  - They are compact, semantic, and much cheaper to maintain than repeating the same logic through handlers.
- `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`
  - Keep the formula tests as the authoritative place for sprint-execution math. They are direct and should stay stronger than handler-level formula assertions.
- `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`
  - Keep the direct rollup coverage for fallback, exclusion, and fractional derived estimates. These tests are more valuable than repeating the same rollups through forecast/projection paths.
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
  - `ComputeProjectionsAsync_WithSqlite_ExecutesWithoutTranslationFailure`
  - `Sqlite_ModelHasNoIndexedDateTimeOffsetProperties`
  - These look narrow, but they guard infrastructure-specific failures that CDC tests cannot catch.
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - `ComputeProductSprintProjection_ActivityBubblesUpToParentFeature`
  - `EpicVisibility_AcceptanceCriteria1_EpicAppearsWhenDescendantHasNonNoiseActivity_EvenIfDeliveredIsZero`
  - `EpicVisibility_NoiseExclusion_ChangedByAndChangedDateAreExcludedByProjectionService`
  - Keep these because they protect projection-specific visibility and propagation behavior that is not fully covered by the CDC service tests alone.
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - `Handle_CountsCommittedPbiMovedDirectlyToNextSprint_AsSpillover`
  - Keep as a handler-level regression because it proves the application path consumes historical spillover rules correctly from persisted sprint data.
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - `Handle_MapsLegacyEffortFieldsFromCanonicalStoryPointScope`
  - Keep because it protects a compatibility boundary in the outward DTO contract, not just domain semantics.

## Recommended Cleanup Order
1. **Remove pure mapping and constructor-shape tests first**
   - The safe-removal list in the mapper files, the duplicated estimate-source classification test, and the injected-double constructor tests are the lowest-risk cleanup.
2. **Merge duplicated DI and threshold tests**
   - Consolidate `ServiceCollectionTests` overlap and the low/medium/high forecast-confidence trio into parameterized tests.
3. **Reduce handler-level re-tests of CDC estimation and first-Done semantics**
   - Start with `GetSprintMetricsQueryHandlerTests` and `GetSprintExecutionQueryHandlerTests`, keeping one smoke/regression per rule cluster.
4. **Consolidate forecast rollup permutations**
   - Trim duplicate hierarchy/estimation permutations from `GetEpicCompletionForecastQueryHandlerTests` after confirming direct `HierarchyRollupServiceTests` coverage remains strong.
5. **Refactor the large projection suites last**
   - `SprintTrendProjectionServiceTests.cs` has the highest-value regression coverage, but also the most maintenance cost. Clean it last, conservatively, and only after direct CDC tests remain the authoritative source for duplicated semantics.
