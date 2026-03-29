# Repository Stability Audit

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Environment audited: local clone at `/home/runner/work/PoCompanion/PoCompanion` on Ubuntu 24.04 with .NET SDK `10.0.201`

## Summary

Overall repository stability status:

- **Restore:** stable
- **Build:** stable
- **Unit tests:** unstable
- **Project/dependency graph:** consistent enough to restore/build, but missing repository-level pinning (`global.json`) and central package management
- **Recent architecture fallout:** visible mostly in tests and docs, not in restore/build

Observed baseline:

- `dotnet restore PoTool.sln` succeeded
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo` succeeded with `0` warnings and `0` errors
- `dotnet test PoTool.sln --configuration Release --no-build --nologo` failed because `PoTool.Tests.Unit` reported **94 failing tests** out of `1680`
- `PoTool.Core.Domain.Tests` passed (`1/1`)

Important context:

- The repository has **no `global.json`**, **no `NuGet.config`**, and **no `Directory.Packages.props`**
- All projects target **`net10.0`**
- The only recent failed GitHub Actions run visible through GitHub MCP was an automation run (`Running Copilot coding agent`, run `23695490187` on 2026-03-28), not a conventional CI pipeline; the job log content was not retrievable (`HTTP 404`)

Current state is therefore **test-baseline unstable**, not restore/build unstable.

## Restore Audit

### Commands run

- `dotnet restore PoTool.sln --nologo`
- `dotnet restore <each .csproj> --nologo`

### Per-project restore results

| Project | Restore status | Notes |
|---|---|---|
| `PoTool.Api/PoTool.Api.csproj` | Success | No package/feed/auth errors |
| `PoTool.Client/PoTool.Client.csproj` | Success | No workload or WASM restore errors |
| `PoTool.Core/PoTool.Core.csproj` | Success | No issues |
| `PoTool.Core.Domain/PoTool.Core.Domain.csproj` | Success | No issues |
| `PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` | Success | No issues |
| `PoTool.Integrations.Tfs/PoTool.Integrations.Tfs.csproj` | Success | No issues |
| `PoTool.Shared/PoTool.Shared.csproj` | Success | No issues |
| `PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` | Success | No issues |
| `PoTool.Tools.TfsRetrievalValidator/PoTool.Tools.TfsRetrievalValidator.csproj` | Success | No issues |

### Root-cause grouping

No restore failures were reproduced.

| Failure category | Status |
|---|---|
| Package source / feed related | None reproduced |
| Auth related | None reproduced |
| Package version conflict | None reproduced |
| Missing package | None reproduced |
| Lockfile / assets related | None reproduced |
| SDK mismatch | None reproduced in current environment |
| Environment-specific | **Potential minor risk only**: no `global.json`, so success depends on whichever .NET 10 SDK is installed |

### Restore assessment

The reported restore concern is **not reproducible in the current environment**. Restore is clean across the full solution and all individual projects.

## Build Audit

### Commands run

- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet build <each .csproj> --configuration Release --no-restore --nologo`

### Per-project build results

| Project | Build status | Notes |
|---|---|---|
| `PoTool.Api/PoTool.Api.csproj` | Success | |
| `PoTool.Client/PoTool.Client.csproj` | Success | Blazor output generated successfully |
| `PoTool.Core/PoTool.Core.csproj` | Success | |
| `PoTool.Core.Domain/PoTool.Core.Domain.csproj` | Success | |
| `PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` | Success | |
| `PoTool.Integrations.Tfs/PoTool.Integrations.Tfs.csproj` | Success | |
| `PoTool.Shared/PoTool.Shared.csproj` | Success | |
| `PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` | Success | Test assembly builds even though runtime tests fail |
| `PoTool.Tools.TfsRetrievalValidator/PoTool.Tools.TfsRetrievalValidator.csproj` | Success | |

### Solution build result

- **Succeeded**
- Warnings: `0`
- Errors: `0`
- First real failure: **none**
- Downstream/cascade failures: **none**

### Build assessment

The repository currently has a **clean compile baseline**. Any instability is happening after compilation, inside test execution and test expectations.

## Test Audit

### Commands run

- `dotnet test PoTool.sln --configuration Release --no-build --nologo`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo`

### Per-test-project status

| Test project | Status | Result |
|---|---|---|
| `PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` | Pass | `1 passed, 0 failed` |
| `PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` | Fail | `1586 passed, 94 failed` |

### Failure categories and counts

| Category | Count | Classification |
|---|---:|---|
| Logger proxy / internals accessibility failures | 46 | Regression / infrastructure test break |
| Canonical work item type mismatch failures | 18 | Regression from domain canonicalization/test fixture drift |
| Portfolio/sprint projection failures | 10 | Likely regression from recent analytical/domain refactors |
| Backlog health multi-product failures | 5 | Likely regression from recent loader/filter boundary changes |
| Dependency graph failures | 5 | Stale tests or handler behavior drift |
| Selection behavior failures | 4 | Stale tests vs current client service contract |
| Doc/audit/isolated expectation failures | 6 | Stale tests/docs or isolated behavior drift |
| **Total** | **94** | |

### Failing tests grouped by cause

#### 1. Logger proxy / internals accessibility failures (`46`)

Representative exact error:

> `Can not create proxy for type Microsoft.Extensions.Logging.ILogger<RealTfsClient> because type PoTool.Integrations.Tfs.Clients.RealTfsClient is not accessible. Make it public, or internal and mark your assembly with [assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=...")]`

Affected classes:

- `PoTool.Tests.Unit.TfsClientTests` (`11`)
- `PoTool.Tests.Unit.Services.RealTfsClientVerificationTests` (`10`)
- `PoTool.Tests.Unit.Services.RealTfsClientRequestTests` (`8`)
- `PoTool.Tests.Unit.Services.WorkItemTagUpdateTests` (`6`)
- `PoTool.Tests.Unit.Services.RealTfsClientErrorHandlingTests` (`4`)
- `PoTool.Tests.Unit.Services.MockTfsClientTests` (`3`)
- `PoTool.Tests.Unit.Services.WorkItemAncestorCompletionTests` (`2`)
- `PoTool.Tests.Unit.Services.WorkItemHierarchyBacklogPriorityTests` (`1`)
- `PoTool.Tests.Unit.Services.RealTfsClientPagingTests` (`1`)

Exact failing tests:

- `GetWorkItemsAsync_ParsesParentId_WhenParentExists`
- `GetWorkItemsByRootIdsAsync_DoesNotFallbackStoryPointsIntoEffort`
- `GetWorkItemsAsync_PreservesStoryPointsEffortAndBusinessValueSeparately`
- `GetWorkItemsAsync_SpecialCharactersInFields_HandledCorrectly`
- `GetWorkItemsAsync_MixedValidAndInvalidData_ProcessesValidItems`
- `GetWorkItemsAsync_UsesUtcForChangedDateFilter`
- `GetWorkItemsAsync_HandlesEmptyRelations`
- `GetWorkItemsAsync_HandlesInvalidRelationUrl`
- `GetWorkItemsAsync_VeryLargeWorkItemCount_ReturnsAllItems`
- `GetWorkItemsAsync_NullFields_HandledGracefully`
- `GetWorkItemsAsync_EmptyResponse_ReturnsEmptyList`
- `VerifyCapabilitiesAsync_WithWriteChecks_IncludesWriteVerification`
- `VerifyCapabilitiesAsync_NullOnlySampleValues_LogsWarningButSucceeds`
- `VerifyCapabilitiesAsync_ServerUnreachable_ReturnsFailureReport`
- `VerifyCapabilitiesAsync_AuthenticationFailure_ReturnsAuthFailureCategory`
- `VerifyCapabilitiesAsync_WrongFieldTypeInSamplePayload_FailsWorkItemFieldVerification`
- `VerifyCapabilitiesAsync_MissingAnalyticsField_FailsWorkItemFieldVerification`
- `VerifyCapabilitiesAsync_IncludesAllCapabilityIds`
- `VerifyCapabilitiesAsync_AllChecksPass_ReturnsSuccessReport`
- `VerifyCapabilitiesAsync_FailedChecksIncludeResolutionGuidance`
- `GetTestRunsByBuildIdsAsync_UsesBuildUriQueryShapeWithSupportedApiVersion`
- `GetAreaPathsAsync_UsesConfiguredTimeout`
- `GetCoverageByBuildIdsAsync_UsesSupportedCoverageEndpointShapeWithoutBuildMetadataGate`
- `GetPullRequestsAsync_UsesUtcForMinAndMaxTime`
- `GetCoverageByBuildIdsAsync_LogsPerBuildWarningsForMalformedCoveragePayloads`
- `GetCoverageByBuildIdsAsync_UsesFirstValidLinesCoverageAndLogsMultipleEntries`
- `UpdateWorkItemTagsAndReturnAsync_DuplicateTags_Deduplicates`
- `UpdateWorkItemTagsAndReturnAsync_AddingTags_SendsCorrectPatchDocument`
- `UpdateWorkItemTagsAndReturnAsync_EmptyTagsInList_FiltersThemOut`
- `UpdateWorkItemTagsAndReturnAsync_RemovingSomeTags_SendsCorrectPatchDocument`
- `UpdateWorkItemTagsAndReturnAsync_TagsWithWhitespace_NormalizesCorrectly`
- `UpdateWorkItemTagsAndReturnAsync_RemovingAllTags_SendsEmptyString`
- `HandleHttpErrorsAsync_500_ThrowsTfsExceptionWithStatusCode`
- `ExecuteWithRetryAsync_RetriesOnServerError`
- `HandleHttpErrorsAsync_429_ThrowsRateLimitExceptionWithRetryAfter`
- `HandleHttpErrorsAsync_429_WithRetryAfterDate_UsesRemainingDelay`
- `GetBuildQualityFactsForIncidentResponseControl_ReturnsLinkedMixedScenario`
- `GetPipelineDefinitionsForRepositoryAsync_ReturnsDefinitionsWithMatchingRuns`
- `GetWorkItemsByTypeAsync_WithProjectRootAreaPath_ReturnsGoals`
- `GetWorkItemsByRootIdsAsync_HandlesItemsWithMissingRelations`
- `GetWorkItemsByRootIdsAsync_CompletesAncestors_WhenRootHasParents`
- `GetWorkItemsByRootIdsAsync_MapsBacklogPriorityFromHierarchyFields`
- `GetGitRepositoriesAsync_AccumulatesPagedResults`

Assessment:

- This cluster is a **broad infrastructure-level test failure**
- It does **not** indicate package restore/build instability
- It is tightly coupled to the TFS access boundary/internal visibility setup

#### 2. Canonical work item type mismatch (`18`)

Representative exact error:

> `Work item type 'Product Backlog Item' is not a canonical domain work item type.`

Affected classes:

- `PoTool.Tests.Unit.Services.CanonicalStoryPointResolutionServiceTests` (`9`)
- `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests` (`5`)
- `PoTool.Tests.Unit.Services.SprintCommitmentCdcServicesTests` (`1`)
- `PoTool.Tests.Unit.Services.HistoricalSprintInputMapperTests` (`1`)
- `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests` (`2` of the 3 failures in that class share this theme)

Exact failing tests:

- `Resolve_UsesBusinessValueFallbackWhenStoryPointsAreMissing`
- `Resolve_UsesStoryPointsWhenPresent`
- `Resolve_KeepsDerivedEstimateFractional`
- `Resolve_ReturnsMissingWhenAllFeatureSiblingsLackCanonicalEstimates`
- `Resolve_TreatsZeroStoryPointsOnDoneItemAsValidRealEstimate`
- `Resolve_TreatsZeroStoryPointsOnNonDoneItemAsMissing`
- `Resolve_DerivesMissingEstimateFromSameFeatureSiblingAverage`
- `Resolve_ReturnsMissingWhenNoEstimateExists`
- `Resolve_IgnoresBugAndTaskSiblingsWhenDerivingEstimates`
- `RollupCanonicalScope_UsesFractionalDerivedEstimates`
- `RollupCanonicalScope_FeatureScope_UsesDirectPbiEstimates`
- `RollupCanonicalScope_ExcludesBugAndTaskStoryPoints`
- `RollupCanonicalScope_EpicScope_RollsUpNestedFeatureChildren`
- `RollupCanonicalScope_ParentFallback_OnlyAppliesWhenChildPbisLackEstimates`
- `SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals`
- `ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`
- `EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes`
- `SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`

Assessment:

- This cluster strongly suggests **test data / fixture drift after canonicalization changes**
- It is directly related to recent domain/analytics refactors

#### 3. Portfolio/sprint projection regression (`10`)

Representative failures:

- `Expected value <8> and actual value <0>`
- `Expected:<Expanding>. Actual:<Stable>`

Affected classes:

- `PoTool.Tests.Unit.Services.PortfolioFlowProjectionServiceTests` (`6`)
- `PoTool.Tests.Unit.Services.SprintTrendProjectionServiceSqliteTests` (`3`)
- `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests` (`1` remaining portfolio-flow failure)

Exact failing tests:

- `ComputeProductSprintProjection_UsesHistoricalEstimateAtFirstDone_AndSprintEndEstimateForStock`
- `ComputeProductSprintProjection_UsesFirstDoneForThroughput_WhenPbiIsReopened`
- `ComputeProductSprintProjection_CountsInflowAndThroughput_WhenPbiEntersAndCompletesInSameSprint`
- `ComputeProductSprintProjection_ReconstructsStockRemainingAndInflow_ForMidSprintPortfolioEntry`
- `ComputeProductSprintProjection_TreatsResolvedProductChangeAsPortfolioInflow_WhenPbiMovesIntoPortfolio`
- `ComputeProductSprintProjection_ComputesCompletionPercentFromStockAndRemainingScope`
- `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionInTheSprintPipeline`
- `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates`
- `PortfolioFlow_ReplayFixture_ReconstructsStockInflowAndThroughputDeterministically`
- `LegacyAndPortfolioFlowRepresentativeDataset_ProduceExplainableDifferences`

Assessment:

- This is the most concerning **actual analytics regression cluster**
- The failures indicate zeroed portfolio-flow metrics and changed trajectory semantics

#### 4. Backlog health multi-product regression (`5`)

Representative failures:

- `Expected:<6>. Actual:<0>. 'healthForSprint1.TotalWorkItems'`
- expected `AnalyzeAsync(...)` invocation was not observed

Affected class:

- `PoTool.Tests.Unit.Handlers.GetMultiIterationBacklogHealthQueryHandlerMultiProductTests`

Exact failing tests:

- `Handle_UsesBacklogQualityAnalysisServiceForRealSprintSlots`
- `Handle_WithSingleProduct_BehavesLikeOriginal`
- `Handle_WithTwoDisjointProducts_ReturnsCumulativeTotals`
- `Handle_WithOverlappingProducts_DeduplicatesWorkItems`
- `Handle_WithNonExistentProduct_SkipsInvalidProducts`

Assessment:

- Likely tied to **filter/loader path changes** in `SprintScopedWorkItemLoader`
- This looks like a real runtime behavior regression, not just a stale assertion

#### 5. Dependency graph regression or stale relation-shape tests (`5`)

Representative failure:

> `Assert.HasCount failed. Expected collection of size 2. Actual: 0. 'result.Links'.`

Affected class:

- `PoTool.Tests.Unit.Handlers.GetDependencyGraphQueryHandlerTests`

Exact failing tests:

- `Handle_WithParentChildLinks_CreatesHierarchyLinks`
- `Handle_WithLongDependencyChain_FindsCriticalPaths`
- `Handle_WithBasicDependencies_BuildsGraphCorrectly`
- `Handle_WithCircularDependencies_DetectsCircles`
- `Handle_WithBlockingWorkItems_IdentifiesBlockers`

Assessment:

- The handler now reads `WorkItemDto.Relations` directly, while these tests construct relation data in `jsonPayload` strings only
- This cluster is best classified as **stale tests against an older relation-loading shape**, not necessarily a production regression

#### 6. Selection behavior changed / stale client tests (`4`)

Representative failures:

- `Expected collection of size 1. Actual: 0. 'newState.SelectedIds'`
- `Expected:<2>. Actual:<1>`

Affected class:

- `PoTool.Tests.Unit.Services.WorkItemSelectionServiceTests`

Exact failing tests:

- `HandleKeyboardNavigation_ArrowDown_SelectsNextItem`
- `ToggleNodeSelection_SelectsNewNode`
- `SelectAllNodes_SelectsAllVisibleNodes`
- `HandleKeyboardNavigation_ArrowUp_SelectsPreviousItem`

Assessment:

- `WorkItemSelectionService` now requires `TreeNode.JsonPayload` to deserialize a selected work item
- The tests build `TreeNode` objects without `JsonPayload`
- This is most likely **stale test setup**, not an architecture regression

#### 7. Doc/audit/isolated expectation failures (`6`)

Exact failing tests:

- `CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors`
- `GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource`
- `BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis`
- `Handle_ScatterPoints_OrderedByStartTimeAscending`
- `SanitizeFilter_RemovesSQLInjectionAttempts`
- `Handle_CallsTfsClientWithNullDepth`

Assessment by item:

- `CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors` — stale audit document / handler anchor mismatch
- `GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource` — stale generated map expectations
- `BuildQualityMissingIngestionBuild168570CodeAnalysisReport_ReportExistsWithRequiredSectionsAndDiagnosis` — stale report-content expectation
- `Handle_ScatterPoints_OrderedByStartTimeAscending` — isolated behavior regression or changed seeding/order assumptions
- `SanitizeFilter_RemovesSQLInjectionAttempts` — likely stale expectation; sanitizer now strips injection text more aggressively
- `Handle_CallsTfsClientWithNullDepth` — stale expectation; handler currently hardcodes `depth: 5`

### Root cause vs cascade assessment

- There are **no build cascades**
- The failing tests split into a few independent clusters
- The broadest single blocker is the **logger proxy / internal visibility** issue
- The most architecture-relevant behavior regressions are:
  - canonical work item type drift
  - portfolio/sprint projection zeros
  - backlog health multi-product zeros

## Architecture Fallout

### 1. TFS boundary fallout is visible in the test suite

Evidence:

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.cs` declares `internal partial class RealTfsClient`
- `PoTool.Integrations.Tfs/AssemblyInfo.cs` exposes internals only to `PoTool.Tests.Unit`

Effect:

- A large TFS-focused unit-test cluster now fails when Moq/Castle tries to proxy `ILogger<RealTfsClient>`
- This is consistent with **recent TFS access boundary tightening** leaving tests partially behind

### 2. Canonical domain enforcement is ahead of older test fixtures

Evidence:

- `PoTool.Core.WorkItems/WorkItemType.cs` still exposes TFS-facing `"Product Backlog Item"`
- `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs` requires canonical `"PBI"`

Effect:

- Domain/CDC tests written against pre-canonicalization fixture helpers now fail immediately
- This is a clear side effect of recent analytical/domain refactoring

### 3. Some tests still assume pre-consolidation data shapes

Evidence:

- `PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs` uses `WorkItemDto.Relations`
- `PoTool.Tests.Unit/Handlers/GetDependencyGraphQueryHandlerTests.cs` encodes relations into `jsonPayload` instead

Effect:

- Dependency graph tests are written against an older representation path

### 4. Some UI/service tests assume an older client-side selection contract

Evidence:

- `PoTool.Client/Services/WorkItemSelectionService.cs` returns unchanged state when `TreeNode.JsonPayload` is empty
- `PoTool.Tests.Unit/Services/WorkItemSelectionServiceTests.cs` creates `TreeNode` instances without `JsonPayload`

Effect:

- The failures reflect stale tests rather than broad UI instability

### 5. Historical docs now contradict the current code body

Evidence:

- `docs/architecture/pull-request-persistence-abstraction-validation.md` starts with a historical note, but its body still states that:
  - `GetFilteredPullRequestsQueryHandler`
  - `GetPullRequestMetricsQueryHandler`
  - `GetPRReviewBottleneckQueryHandler`
  
  use `IPullRequestReadProvider`, which is no longer true

Effect:

- Documentation baseline is partially stale, even though the current code and the newer consolidation docs are aligned

### 6. No obvious dead DI registrations were found

Evidence:

- Current PR analytics registrations still map cleanly:
  - default `IPullRequestReadProvider`
  - `IPullRequestQueryStore`
  - keyed live/cached providers

Effect:

- Architectural fallout is currently concentrated in tests/docs, not dead service wiring

## Dependency Graph Issues

### Solution/project graph

Projects in solution:

- `PoTool.Client`
- `PoTool.Tests.Unit`
- `PoTool.Core`
- `PoTool.Api`
- `PoTool.Shared`
- `PoTool.Integrations.Tfs`
- `PoTool.Tools.TfsRetrievalValidator`
- `PoTool.Core.Domain`
- `PoTool.Core.Domain.Tests`

### Project reference observations

| Observation | Assessment |
|---|---|
| `PoTool.Api` references `PoTool.Client` | Acceptable for hosted Blazor setup; not currently breaking build |
| `PoTool.Tests.Unit` references API, Client, Integrations, Tools, Core, Core.Domain | Broad test-project scope increases blast radius and makes the baseline noisy |
| `PoTool.Tools.TfsRetrievalValidator` references `PoTool.Api` | Acceptable but couples tool stability to API architecture |

### Package/version observations

- No central package management file exists (`Directory.Packages.props` absent)
- Package versions are declared inline in each `.csproj`
- Core Microsoft packages are consistently on `10.0.1`
- No version conflicts were reproduced during restore/build

### Dependency/version sanity conclusions

| Issue | Severity | Notes |
|---|---|---|
| No `global.json` | Minor | Environment can drift to a different .NET 10 SDK |
| No central package management | Minor | Versions are duplicated across projects, but not currently conflicting |
| Broad `PoTool.Tests.Unit` project scope | Major | Makes unit baseline sensitive to cross-layer refactors and stale fixtures |
| Wrong-layer references causing restore/build break | None found | |

## Severity Classification

### CRITICAL

1. **`PoTool.Tests.Unit` baseline is broken by 94 failures**
   - Blocks a reliable test baseline for further refactors

2. **Logger proxy / internal visibility cluster (`46` tests)**
   - Breaks most TFS-facing unit coverage before test bodies even run

3. **Canonical work item type mismatch cluster (`18` tests)**
   - Breaks CDC/domain analytical validation immediately

### MAJOR

4. **Portfolio/sprint projection failures (`10` tests)**
   - Indicates likely analytical regression in current domain/projection behavior

5. **Backlog health multi-product failures (`5` tests)**
   - Suggests real behavior drift in multi-product loading/aggregation

6. **Broad unit-test project scope**
   - Cross-layer tests amplify fallout from architecture changes and make it harder to isolate failures

7. **Dependency graph tests are stale against current relation representation**
   - Not necessarily a production bug, but the current test baseline is unreliable

### MINOR

8. **Historical PR architecture document body is stale**
   - `docs/architecture/pull-request-persistence-abstraction-validation.md`

9. **Selection-service tests are stale against current `JsonPayload` requirement**
   - Localized test-contract drift

10. **Isolated expectation drifts**
    - sanitizer expectation
    - area-path depth expectation
    - audit document content expectations
    - pipeline scatter ordering expectation

11. **No `global.json` and no central package management**
    - Cleanup/hardening issue only; not a reproduced failure

## Recommended Recovery Order

### 1. First fix

Restore the **broad test harness baseline**:

- fix the logger proxy / internal visibility issue around `RealTfsClient`
- this should collapse the largest failure cluster first and expose any remaining real TFS-client behavior regressions cleanly

### 2. Second fix

Align the **domain/CDC tests** with the current canonical model:

- update canonical work-item fixtures/helpers to use canonical domain types where required
- this should remove the current `Product Backlog Item` vs `PBI` mismatch noise

### 3. Third fix

Investigate the **actual analytics behavior regressions**:

- portfolio/sprint projection zeros
- backlog health multi-product zeros

These are the highest-value runtime regressions after the test harness and fixture noise are removed.

## Final Verdict

**Unstable baseline**

Restore and build are healthy, but the repository does **not** currently have a stable unit-test baseline. The current failure load is large enough, and architecture-related enough, that further refactoring without first re-establishing a trustworthy test baseline would be high risk.
