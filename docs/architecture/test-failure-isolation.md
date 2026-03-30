# Remaining Test Failure Isolation

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Environment audited: local clone at `/home/runner/work/PoCompanion/PoCompanion` on Ubuntu 24.04 with .NET SDK `10.0.201`

## Summary

Current failing-unit-test landscape is **narrower and more structured** than the earlier broad failure baseline.

Observed current baseline:

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --nologo` ✅
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo` ✅
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal` ❌
- Current `PoTool.Tests.Unit` result: **26 failed, 1654 passed, 0 skipped, 1680 total**

The remaining failures cluster into a small number of shared causes:

1. **Canonical work-item type mismatch across CDC/domain tests**
2. **Dependency-graph tests using incomplete `WorkItemDto` setup**
3. **Selection-service tests building `TreeNode` inputs without required JSON payloads**
4. Smaller isolated clusters in TFS hierarchy mocks, audit-document drift, and a small number of likely real behavioral regressions

GitHub Actions inspection did not expose a conventional failing unit-test workflow for this branch. Recent runs were dynamic Copilot agent runs, not a dedicated unit-test CI signal, so the local test run is the authoritative failure source for this isolation report.

## Failure Clusters

### Cluster 1 — Canonical work-item type mismatch in CDC/domain tests

**Affected tests (9):**

- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_EpicScope_RollsUpNestedFeatureChildren`
- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_ExcludesBugAndTaskStoryPoints`
- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_ParentFallback_OnlyAppliesWhenChildPbisLackEstimates`
- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_UsesFractionalDerivedEstimates`
- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_FeatureScope_UsesDirectPbiEstimates`
- `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests.SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`
- `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests.EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes`
- `PoTool.Tests.Unit.Services.SprintCommitmentCdcServicesTests.SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals`
- `PoTool.Tests.Unit.Services.HistoricalSprintInputMapperTests.ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`

**Failure type / messages:**

- 8 tests throw `System.ArgumentException: Work item type 'Product Backlog Item' is not a canonical domain work item type.`
- 1 test asserts the old non-canonical value and fails with:
  - expected: `"Product Backlog Item"`
  - actual: `"PBI"`

**Suspected root cause:**

There is a direct constant mismatch between non-canonical work-item constants and canonical domain constants:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/WorkItems/WorkItemType.cs:12-14`
  - `Pbi = "Product Backlog Item"`
  - `PbiShort = "PBI"`
  - `UserStory = "User Story"`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs:12-16`
  - `Pbi = "PBI"`
  - `ProductBacklogItem = Pbi`
  - `UserStory = Pbi`

The failing tests construct canonical-domain objects using raw/non-canonical values, or still assert the raw string instead of the canonical alias.

Example evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs:46-54`
  - `EnsureCanonical(...)` throws when the type is not one of the canonical names.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs:584-588`
  - replay fixture construction feeds raw work-item types into canonical models.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs:28`
  - test still expects `WorkItemType.Pbi` (`"Product Backlog Item"`) while the mapper returns canonical `"PBI"`.

**Type:** mixed, but primarily **incorrect test expectation / missing-misaligned test setup** after canonicalization tightening

**Why this classification:**

The canonical domain intentionally enforces canonical values; the tests are the parts still supplying or expecting raw aliases. The behavior is consistent with the current domain contract rather than a fresh runtime failure in production code.

**Confidence:** **high**

---

### Cluster 2 — Dependency-graph tests create `WorkItemDto` objects without `Relations`

**Affected tests (5):**

- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests.Handle_WithBlockingWorkItems_IdentifiesBlockers`
- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests.Handle_WithLongDependencyChain_FindsCriticalPaths`
- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests.Handle_WithParentChildLinks_CreatesHierarchyLinks`
- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests.Handle_WithBasicDependencies_BuildsGraphCorrectly`
- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests.Handle_WithCircularDependencies_DetectsCircles`

**Failure type / messages:**

- empty `Links`
- empty `CriticalPaths`
- empty `BlockedWorkItemIds`
- empty `CircularDependencies`

Representative messages:

- `Assert.HasCount failed. Expected collection of size 2. Actual: 0. 'collection' expression: 'result.Links'.`
- `Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. 'collection' expression: 'result.CriticalPaths'.`

**Suspected root cause:**

The handler now reads `workItem.Relations` directly, but the tests still populate only JSON payload text and do not materialize `Relations` on the `WorkItemDto` instances.

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs:96-99`
  - `var relations = workItem.Relations ?? new List<WorkItemRelation>();`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs:67-70`
  - tests pass JSON strings containing `relations`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs` helper setup does not convert that JSON into `WorkItemDto.Relations`

So the handler sees no relations, and every downstream graph assertion collapses to empty results.

**Type:** **missing/misaligned test setup**

**Why this classification:**

The tests are building objects in a way that no longer matches the handler contract. This is a setup drift issue, not strong evidence of a dependency-graph regression in runtime behavior.

**Confidence:** **high**

---

### Cluster 3 — Selection-service tests build `TreeNode` inputs without `JsonPayload`

**Affected tests (4):**

- `PoTool.Tests.Unit.Services.WorkItemSelectionServiceTests.HandleKeyboardNavigation_ArrowDown_SelectsNextItem`
- `PoTool.Tests.Unit.Services.WorkItemSelectionServiceTests.SelectAllNodes_SelectsAllVisibleNodes`
- `PoTool.Tests.Unit.Services.WorkItemSelectionServiceTests.ToggleNodeSelection_SelectsNewNode`
- `PoTool.Tests.Unit.Services.WorkItemSelectionServiceTests.HandleKeyboardNavigation_ArrowUp_SelectsPreviousItem`

**Failure type / messages:**

- `SelectedWorkItems` / `SelectedIds` counts are zero when tests expect populated selection state
- keyboard navigation remains on the current item instead of moving to the expected item

Representative messages:

- `Assert.HasCount failed. Expected collection of size 1. Actual: 0. 'collection' expression: 'newState.SelectedIds'.`
- `Assert.AreEqual failed. Expected:<2>. Actual:<1>.`
- `Assert.AreEqual failed. Expected:<1>. Actual:<2>.`

**Suspected root cause:**

`WorkItemSelectionService` requires `TreeNode.JsonPayload` so it can deserialize a `WorkItemDto`. The test helper creates `TreeNode` instances without setting `JsonPayload`, so selection toggles and keyboard navigation return the existing state.

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkItemSelectionService.cs:32-37`
  - returns `currentState` immediately when `node.JsonPayload` is null/empty
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkItemSelectionService.cs:118-124`
  - `SelectAllNodes(...)` only populates `SelectedWorkItems` when `JsonPayload` is present
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs:22-48`
  - `CreateTestNode(...)` does not set `JsonPayload`

**Type:** **missing/misaligned test setup**

**Why this classification:**

The service behavior is internally consistent with its current contract. The tests are constructing incomplete UI nodes.

**Confidence:** **high**

---

### Cluster 4 — TFS ancestor-completion tests use mock payloads that do not match the current hierarchy client path

**Affected tests (2):**

- `PoTool.Tests.Unit.Services.WorkItemAncestorCompletionTests.GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations`
- `PoTool.Tests.Unit.Services.WorkItemAncestorCompletionTests.GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents`

**Failure type / messages:**

- `System.Collections.Generic.KeyNotFoundException: The given key was not present in the dictionary.`
- stack starts at `System.Text.Json.JsonElement.GetProperty(String propertyName)`
- failure hits `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs:274`

**Suspected root cause:**

The batch-field-response mock data in the tests appears to be out of alignment with the current hierarchy-fetch expectations. The client unconditionally reads `fieldsDoc.RootElement.GetProperty("value")`, so any response-shape mismatch in the mocked sequence breaks before the ancestor assertions even run.

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs:271-277`
  - current implementation expects `RootElement["value"]`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemAncestorCompletionTests.cs:104-211`
  - test depends on a tightly ordered queue of mocked WIQL / relations / fields responses
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemAncestorCompletionTests.cs:262-285`
  - second test intentionally omits `relations`, but still depends on the current client’s later batch response parsing remaining compatible with the mock sequence

**Type:** **missing/misaligned test setup**

**Why this classification:**

The failure occurs in mocked response parsing before the business assertions. That points more strongly to brittle mock sequence/shape drift than to a confirmed production regression.

**Confidence:** **medium**

---

### Cluster 5 — Audit-document tests are stale relative to current code/docs

**Affected tests (3):**

- `PoTool.Tests.Unit.Audits.CdcUsageCoverageDocumentTests.CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors`
- `PoTool.Tests.Unit.Audits.BuildQualityMissingIngestionBuild168570CodeAnalysisReportDocumentTests.BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis`
- `PoTool.Tests.Unit.Audits.CdcGeneratedDomainMapDocumentTests.GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource`

**Failure type / messages:**

Representative mismatches reproduced in the current run:

- expected handler anchor `new GetSprintMetricsQuery(path)` not found in current handler source
- expected document content `"Line" or "Lines"` not found in the build-quality analysis report
- expected generated-domain-map symbol ``IEpicAggregationService`` not found in `docs/architecture/cdc-domain-map-generated.md`

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcUsageCoverageDocumentTests.cs:83-100`
  - asserts multiple exact source anchors against current handler files
- current handler file:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs:160-216`
  - contains `ComputeFeatureProgressAsync` / `ComputeEpicProgressAsync`, so several parts of the audit remain current, but the failing anchor set is at least partly outdated
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcGeneratedDomainMapDocumentTests.cs:124-137`
  - dynamically detects public interfaces and expects every one to appear in the generated document
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs:9-15`
  - `IEpicAggregationService` exists in source
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md:153-180`
  - current generated map does not include ``IEpicAggregationService``
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/buildquality_missing_ingestion_build_168570_code_analysis_report.md:1-120`
  - report exists and mostly matches, but not every exact expected phrase remains present

**Type:** **incorrect test expectation / documentation drift**

**Why this classification:**

These tests validate documentation strings and generated-report content, not runtime behavior. The reports or source-anchor expectations have drifted.

**Confidence:** **high**

---

### Cluster 6 — Small likely real behavioral regressions in handler/service behavior

**Affected tests (3):**

- `PoTool.Tests.Unit.Handlers.GetPipelineInsightsScatterPointTests.Handle_ScatterPoints_OrderedByStartTimeAscending`
- `PoTool.Tests.Unit.Handlers.GetAreaPathsFromTfsQueryHandlerTests.Handle_CallsTfsClientWithNullDepth`
- `PoTool.Tests.Unit.Helpers.InputValidatorTests.SanitizeFilter_RemovesSQLInjectionAttempts`

**Failure type / messages:**

- scatter points come back out of ascending order
- handler calls TFS with non-null depth when the test expects null
- sanitizer output no longer matches test substring expectations

**Suspected root causes:**

#### 6a. Pipeline insights scatter ordering

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetPipelineInsightsQueryHandlerTests.cs:663-683`
  - seeds runs out of order and asserts ascending `StartTime`
- current failure message:
  - `Actual value <02/08/2026 09:00:00 +00:00> is not less than or equal to expected value <02/05/2026 09:00:00 +00:00>`

This looks like a **real regression** or a real missing sort in the final returned scatter-point collection.

**Type:** **real regression**  
**Confidence:** **medium-high**

#### 6b. GetAreaPathsFromTfs handler depth parameter

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Handlers/GetAreaPathsFromTfsQueryHandlerTests.cs:123-126`
  - expects `GetAreaPathsAsync(null, ...)`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs:33-36`
  - currently calls `GetAreaPathsAsync(depth: 5, ...)`

This is an explicit implementation/test divergence.

**Type:** **incorrect test expectation or intentional implementation drift**  
**Confidence:** **high**

#### 6c. InputValidator SQL-injection sanitization test

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Helpers/InputValidator.cs:26-29`
  - removes disallowed characters `<`, `>`, `;`, `"`, `'`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Helpers/InputValidatorTests.cs:87-94`
  - expects sanitized output to still contain `"test"` and `"DROP"`
- current failure text shows the actual output collapsed to `"test"`

This needs careful interpretation: either sanitization became stricter than the old test expected, or the test’s `Assert.Contains(...)` usage/expectation is stale. The failure is more consistent with **expectation drift** than with a security regression.

**Type:** **incorrect test expectation**  
**Confidence:** **medium**

---

### Cluster 7 — Isolated dependency/selection-adjacent tail failures already explained by the main setup drifts

This report does **not** break these into additional standalone clusters because they are already accounted for in Clusters 2 and 3:

- dependency-graph empty links / empty blockers / empty paths / empty cycles are all consequences of missing `Relations`
- selection count and keyboard-navigation failures are all consequences of missing `TreeNode.JsonPayload`

These are not independent root causes.

## Top 3 Clusters

### 1. Cluster 1 — Canonical work-item type mismatch in CDC/domain tests

**Why top priority:**

- largest cluster tied to domain/CDC correctness (**9 failures**)
- touches multiple important canonical slices (`HierarchyRollup`, replay fixtures, sprint facts, historical mapping)
- highest likelihood of continuing to obscure real domain regressions if left unresolved

**Priority assessment:** **highest**

---

### 2. Cluster 2 — Dependency-graph tests using incomplete `Relations` setup

**Why top priority:**

- second-largest functional cluster (**5 failures**)
- all failures collapse to one shared setup issue
- very good return on effort because one test-helper correction should clear most or all of the cluster

**Priority assessment:** **high**

---

### 3. Cluster 6a / Cluster 6 overall — Remaining real-behavior suspects

**Why top priority:**

- small cluster by count, but highest remaining chance of being a **real product regression**
- especially `Handle_ScatterPoints_OrderedByStartTimeAscending`, which fails on an explicit observable output ordering guarantee

**Priority assessment:** **high**, despite smaller count

## Recommended Next Step

Fix or rebaseline **Cluster 1** first, then rerun `PoTool.Tests.Unit` and reassess the remaining suite. That cluster is the largest, the most cross-cutting, and the most likely to be hiding the true post-fix baseline for the rest of the repository.
