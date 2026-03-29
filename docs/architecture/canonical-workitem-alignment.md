# Canonical Work Item Type Alignment

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Scope: analysis only; no code changes

## Summary

The repository currently has a **clear canonical domain contract** and a **separate raw TFS type vocabulary**, but canonicalization is applied **inconsistently at boundaries**.

- The canonical domain contract is strict by design: canonical domain models accept `PBI`, not raw `Product Backlog Item` or `User Story`.
- Several API adapters already normalize raw TFS work item types correctly before constructing canonical domain inputs.
- The remaining Cluster 1 failures happen where tests and replay fixtures **bypass those adapters** and pass raw types directly into strict domain constructors, or where tests still assert raw values after a mapper has already canonicalized them.
- The deeper design issue is **not** that the domain is too strict. The issue is that canonicalization is **correctly strict but inconsistently applied, and sometimes applied too late or skipped entirely at domain-entry boundaries**.

## Canonical Contract

### Canonical values

The canonical CDC/domain type vocabulary is defined in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`

Canonical values are:

- `Goal`
- `Objective`
- `Epic`
- `Feature`
- `PBI`
- `Bug`
- `Task`
- `Other`

Important aliases are intentionally normalized to canonical `PBI`:

- `CanonicalWorkItemTypes.ProductBacklogItem = Pbi`
- `CanonicalWorkItemTypes.PbiShort = Pbi`
- `CanonicalWorkItemTypes.UserStory = Pbi`

In practice, the domain treats **`PBI` as the single authoritative backlog-item type**.

### Raw TFS values

The raw/non-canonical application-layer constants are defined in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/WorkItems/WorkItemType.cs`

Notable raw values include:

- `Goal = "goal"`
- `Pbi = "Product Backlog Item"`
- `PbiShort = "PBI"`
- `UserStory = "User Story"`

So the repository currently has two different vocabularies:

- raw/application/TFS-facing constants (`Product Backlog Item`, `User Story`, `goal`)
- canonical/domain constants (`PBI`, `Goal`, etc.)

### Where enforcement happens

Strict canonical enforcement happens in domain-model constructors:

1. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Models/CanonicalWorkItem.cs:11-23`
   - constructor calls `CanonicalWorkItemTypes.EnsureCanonical(workItemType, nameof(workItemType))`
2. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs`
   - delivery-trend domain models also call `EnsureCanonical(...)`
3. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs:41-54`
   - `IsCanonical(...)` accepts only canonical names
   - `EnsureCanonical(...)` throws `ArgumentException` on any non-canonical value

### Which layers expect canonical vs raw

#### Layers that clearly expect canonical values

- `CanonicalWorkItem`
- delivery-trend domain inputs (`DeliveryTrendWorkItem`, `DeliveryTrendResolvedWorkItem`)
- CDC/domain logic that treats `CanonicalWorkItemTypes.Pbi` as authoritative
- adapter-produced canonical inputs such as:
  - `HistoricalSprintInputMapper.ToSnapshot(...)`
  - `CanonicalMetricsInputMapper.ToCanonicalWorkItem(...)`
  - `DeliveryTrendProjectionInputMapper.*`
  - `StateClassificationInputMapper.ToCanonicalDomainStateClassifications(...)`

#### Layers that still carry raw values

- `RealTfsClient` output (`WorkItemDto.Type`)
- EF persistence entities (`WorkItemEntity.Type`)
- `ResolvedWorkItemEntity.WorkItemType` as currently populated by `WorkItemResolutionService`
- tests and replay fixtures that directly use `PoTool.Core.WorkItems.WorkItemType`

#### Ambiguous / mixed layer

`PoTool.Core.Domain.Models.WorkItemSnapshot` is used by canonical sprint-history helpers, and comments imply canonical intent, but the record itself does **not** enforce canonical work item types in its constructor.

That means the current code relies on callers to do the right thing:

- adapters mostly canonicalize before building it
- some tests still use raw values directly

So `WorkItemSnapshot` is semantically canonical in most production flows, but not strictly enforced the way `CanonicalWorkItem` is.

## Raw Type Flow

### 1. Raw types enter through TFS ingestion

Raw TFS values first enter through `RealTfsClient`:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs:279-303`

`System.WorkItemType` is read directly from TFS fields and stored into `WorkItemDto.Type` without canonicalization.

This is appropriate for ingestion: the TFS client is still operating in the raw external vocabulary.

### 2. Raw types are persisted in work item entities

Raw values then continue into persistence:

- `WorkItemDto.Type`
- `WorkItemEntity.Type`

`WorkItemEntity.Type` therefore represents stored raw/source values, not guaranteed canonical values.

### 3. Some adapter boundaries canonicalize correctly

The repository already has a clear adapter pattern for normalizing raw values before entering strict domain models.

#### Historical sprint inputs

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/HistoricalSprintInputMapper.cs:10-25`

Both entity and DTO overloads do:

- `entity.Type.ToCanonicalWorkItemType()`
- `dto.Type.ToCanonicalWorkItemType()`

before creating `WorkItemSnapshot`.

#### Canonical metrics inputs

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs:9-35`

Both entity and DTO overloads canonicalize before constructing `CanonicalWorkItem`.

#### Delivery trend inputs

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs:8-35`

Both `DeliveryTrendWorkItem` and `DeliveryTrendResolvedWorkItem` are created with canonicalized type values.

#### Canonicalized state-classification inputs

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/StateClassificationInputMapper.cs:23-31`

This explicitly builds canonical state-classification inputs by converting `classification.WorkItemType.ToCanonicalWorkItemType()`.

### 4. Some services canonicalize inline because upstream persistence is still raw

#### Portfolio flow

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs:261-274`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs:384-386`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioFlowProjectionService.cs:448-450`

`PortfolioFlowProjectionService` filters and constructs canonical inputs by repeatedly calling `ToCanonicalWorkItemType()`.

This is correct behavior, but it also shows the service is compensating for upstream raw values still present in persisted entities.

#### Sprint trend

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs:438-440`
- additional repeated canonicalization later in the same service (`:626`, `:759` from repository search)

`SprintTrendProjectionService` also canonicalizes resolved-item work item types inline before applying canonical PBI logic.

### 5. One important persistence path keeps raw types instead of canonicalizing them

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs:123-127`

`ResolvedWorkItemEntity.WorkItemType` is currently set from:

- `WorkItemType = wi.Type`

That means resolved/persisted work-item type data remains raw at storage time, and downstream services must remember to canonicalize it every time they read it.

This is not immediately wrong, but it is a strong source of inconsistency because it keeps canonicalization as a repeated read-time concern instead of a stable boundary decision.

## Failing Test Analysis

Current focused baseline for Cluster 1:

- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~HierarchyRollupServiceTests|FullyQualifiedName~CdcReplayFixtureValidationTests|FullyQualifiedName~SprintCommitmentCdcServicesTests|FullyQualifiedName~HistoricalSprintInputMapperTests" -v minimal`
- Result: **9 failed, 13 passed**

### 1. `HierarchyRollupServiceTests` failures

Affected tests:

- `RollupCanonicalScope_FeatureScope_UsesDirectPbiEstimates`
- `RollupCanonicalScope_EpicScope_RollsUpNestedFeatureChildren`
- `RollupCanonicalScope_ParentFallback_OnlyAppliesWhenChildPbisLackEstimates`
- `RollupCanonicalScope_ExcludesBugAndTaskStoryPoints`
- `RollupCanonicalScope_UsesFractionalDerivedEstimates`

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs:94-110`
- helper creates `new CanonicalWorkItem(...)` using `type` passed from tests
- tests pass `WorkItemType.Pbi`, `WorkItemType.Feature`, `WorkItemType.Epic`, `WorkItemType.Bug`, `WorkItemType.Task`

Current failure:

- `System.ArgumentException: Work item type 'Product Backlog Item' is not a canonical domain work item type.`

Classification:

- **incorrect setup**

Reason:

These tests construct strict canonical domain models directly, but they use raw/application constants instead of canonical constants. The constructor rejection is consistent with the domain contract.

### 2. `CdcReplayFixtureValidationTests` failures

Affected tests:

- `SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`
- `EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes`

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs:586-593`
- fixture does:
  - `new CanonicalWorkItem(workItem.TfsId, workItem.Type, ...)`
- `workItem.Type` comes from persisted `WorkItemEntity.Type`
- earlier fixture seeding uses raw `WorkItemType.Pbi` values:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs:489-504`

Current failure:

- `System.ArgumentException: Work item type 'Product Backlog Item' is not a canonical domain work item type.`

Classification:

- **incorrect setup**

Reason:

The replay fixture bypasses the repository’s adapter/canonicalization pattern and directly feeds raw persisted types into `CanonicalWorkItem`.

### 3. `SprintCommitmentCdcServicesTests` failure

Affected test:

- `SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals`

Evidence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs:227-234`
- constructs `CanonicalWorkItem` values with `WorkItemType.Pbi`

Current failure:

- `System.ArgumentException: Work item type 'Product Backlog Item' is not a canonical domain work item type.`

Classification:

- **incorrect setup**

Reason:

This test is directly constructing canonical domain inputs with raw constants.

Important nuance:

Other tests in the same file still use raw `WorkItemType.Pbi` inside `WorkItemSnapshot` and state lookup dictionaries and do not fail immediately, because those structures are not enforcing canonicality at construction time. So this file demonstrates the repository’s mixed boundary behavior very clearly.

### 4. `HistoricalSprintInputMapperTests` failure

Affected test:

- `ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`

Evidence:

- mapper implementation:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/HistoricalSprintInputMapper.cs:10-17`
  - explicitly calls `entity.Type.ToCanonicalWorkItemType()`
- test assertion:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs:27-29`
  - expects `snapshot.WorkItemType == WorkItemType.Pbi`

Current failure:

- expected `"Product Backlog Item"`
- actual `"PBI"`

Classification:

- **incorrect expectation**

Reason:

The mapper is doing exactly what the adapter layer is supposed to do. The test is asserting the raw value after canonicalization has already occurred.

### 5. Related validation test that clarifies intended design

Relevant supporting test:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs:114-123`

This test explicitly verifies that raw `PoTool.Core.WorkItems.WorkItemType.Pbi` is rejected by `CanonicalWorkItem`.

Classification:

- **real domain rule / correct behavior**

Reason:

This test expresses the current intended design: raw PBI aliases are not allowed inside strict canonical domain models.

## Root Cause

The root cause is:

> **Canonicalization is correctly strict in the domain, but it is inconsistently applied at boundaries. Raw TFS/application work item types still flow through persistence and tests, while strict domain constructors require already-canonical values.**

This means the problem is **not** that `EnsureCanonical(...)` is too strict.

It is:

- **correctly strict but inconsistently applied**
- **sometimes applied too late**
- **missing at some domain-entry boundaries and fixtures**

More specifically:

1. The domain contract is unambiguous: canonical models must receive canonical types.
2. Some API adapters respect that contract.
3. Some services still operate on raw persisted types and canonicalize inline on read.
4. Some tests and replay fixtures bypass canonicalization entirely.

That inconsistency produces the observed failures.

## Fix Strategy

### Recommended design decision

The domain should **not** accept raw types.

All inputs must be canonical **before** entering strict domain models.

That matches the current explicit enforcement in `CanonicalWorkItem` and other canonical-domain inputs, and it matches the existing adapter strategy already present in the codebase.

### Exact locations where canonicalization should occur

#### Already-correct canonicalization points

These boundaries are correct and should remain canonicalizing boundaries:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/HistoricalSprintInputMapper.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/StateClassificationInputMapper.cs` (canonical variant)

#### Missing or inconsistent canonicalization points

1. **Replay fixture boundary**
   - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/CdcReplayFixtureValidationTests.cs:588-590`
   - this should canonicalize before constructing `CanonicalWorkItem`

2. **Direct canonical-domain test helpers**
   - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs:94-110`
   - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs:227-234`
   - these tests should use `CanonicalWorkItemTypes.*` when constructing `CanonicalWorkItem`

3. **Resolved work item persistence boundary (design improvement)**
   - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs:123-127`
   - today it stores raw `wi.Type`
   - if `ResolvedWorkItemEntity.WorkItemType` is meant to feed canonical analytics paths, canonicalizing here would remove repeated downstream normalization and reduce drift risk

### What should not be done

The domain should **not** be relaxed to accept raw aliases like `Product Backlog Item` or `User Story` inside `CanonicalWorkItem`.

Why not:

- the current domain contract is explicit and already tested
- relaxing it would blur the raw/canonical boundary again
- it would turn domain constructors into another mapping layer instead of a validation boundary
- several adapters already do the correct normalization before domain entry

### Concrete affected-test strategy

#### Tests that should switch to canonical constants for direct canonical-domain construction

- `HierarchyRollupServiceTests.*` failing helper path
- `SprintCommitmentCdcServicesTests.SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals`
- `CdcReplayFixtureValidationTests` canonical-work-item reconstruction path

#### Tests that should keep raw values because they are testing pre-canonical layers

- tests operating on raw `WorkItemEntity`, `WorkItemDto`, or TFS client payloads
- tests explicitly validating mapper behavior from raw to canonical

#### Tests that should change only expected values

- `HistoricalSprintInputMapperTests.ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`

## Impacted Tests

Current directly impacted tests in Cluster 1:

1. `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_FeatureScope_UsesDirectPbiEstimates`
2. `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_EpicScope_RollsUpNestedFeatureChildren`
3. `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_ParentFallback_OnlyAppliesWhenChildPbisLackEstimates`
4. `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_ExcludesBugAndTaskStoryPoints`
5. `PoTool.Tests.Unit.Services.HierarchyRollupServiceTests.RollupCanonicalScope_UsesFractionalDerivedEstimates`
6. `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests.SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover`
7. `PoTool.Tests.Unit.Services.CdcReplayFixtureValidationTests.EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes`
8. `PoTool.Tests.Unit.Services.SprintCommitmentCdcServicesTests.SprintFactService_BuildSprintFactResult_ReturnsCanonicalSprintTotals`
9. `PoTool.Tests.Unit.Services.HistoricalSprintInputMapperTests.ToSnapshot_MapsWorkItemEntityToMinimalDomainInput`

Relevant supporting/intent-defining test:

- `PoTool.Tests.Unit.DomainWorkItemFieldSemanticsTests.CanonicalWorkItem_RawPbiType_IsRejectedBecauseDomainRequiresCanonicalType`

## Final Verdict

**Correct design / needs boundary adjustment**

The current design is fundamentally correct:

- canonical domain models should stay strict
- raw TFS types should be normalized before domain entry

What needs adjustment is **boundary discipline**, not domain permissiveness.

Final judgment:

- **Canonicalization is not too strict.**
- **It is correctly strict but inconsistently applied.**
- **The correct fix direction is to canonicalize all inputs before entering strict domain models and to update tests/fixtures so they stop bypassing that boundary.**
