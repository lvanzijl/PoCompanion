# Phase A Corrections

## Summary

Applied a targeted hardening pass on top of Phase A to remove domain ambiguity, complete revision-history coverage for the new fields, add an explicit runtime guard for non-default estimation modes, strengthen TFS verification with sample-payload validation, and isolate roadmap snapshot persistence from the CDC/analytics domain boundary.

No UI changes, no new endpoints, and no Phase B behavior were introduced.

## Corrections applied per section

### 1. Revision ingestion completeness

Applied:
- Added the following fields to `PoTool.Core/RevisionFieldWhitelist.cs`:
  - `Rhodium.Funding.ProjectNumber`
  - `Rhodium.Funding.ProjectElement`
  - `Microsoft.VSTS.Common.TimeCriticality`
- Added OData selection/parse mappings for the same fields through `RevisionFieldWhitelist.BuildODataRevisionSelectionSpec(...)`.
- Verified activity/revision ingestion accepts and persists these fields without interpreting their values.

Verification:
- `ActivityEventIngestionServiceTests.IngestAsync_PhaseACorrectionFields_ArePersistedToActivityLedger`
- `RevisionFieldWhitelistTests.Fields_IncludePhaseACorrectionFields`
- `RevisionFieldWhitelistTests.BuildODataRevisionSelectionSpec_ContainsScalarMappingsForPhaseACorrectionFields`

### 2. Enforce semantic relevance by WorkItemType

Applied:
- Added `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs` to centralize canonical field relevance rules.
- Enforced domain normalization in:
  - `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
  - `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs` (`DeliveryTrendWorkItem`)

Rules now enforced in domain models:
- `ProjectNumber` / `ProjectElement` only survive on `Epic`
- `TimeCriticality` only survives on `Feature`
- Irrelevant combinations are normalized to `null`

Verification:
- `DomainWorkItemFieldSemanticsTests.CanonicalWorkItem_EpicPreservesProjectFields`
- `DomainWorkItemFieldSemanticsTests.CanonicalWorkItem_FeatureUsesTimeCriticalityAndIgnoresProjectFields`
- `DomainWorkItemFieldSemanticsTests.DeliveryTrendWorkItem_EpicIgnoresTimeCriticality`
- `DomainWorkItemFieldSemanticsTests.DeliveryTrendWorkItem_FeatureIgnoresProjectFieldsAndPreservesTimeCriticality`

### 3. Remove “passive metadata” ambiguity

Applied:
- Replaced passive carry-through in domain models with explicit normalization.
- Domain mappers still pass raw DTO/entity values, but domain constructors now decide whether a field is relevant or ignored.
- Removed the unused `DeclaredEstimationMode` field from `DeliveryFeatureProgressRequest` so it no longer exists as inactive domain metadata.

Result:
- DTO/persistence layers can still store/expose fields generically.
- Domain layer no longer carries unsupported combinations as inert metadata.

### 4. EstimationMode runtime guard

Applied:
- Added a runtime guard in `PoTool.Api/Services/SprintTrendProjectionService.cs`.
- When a product is configured with non-default `EstimationMode`, the service logs a warning containing:
  - `productId`
  - selected estimation mode
- Calculation behavior remains StoryPoints-only, as required.

Verification:
- `SprintTrendProjectionServiceTests.ComputeFeatureProgressAsync_NonDefaultEstimationMode_LogsWarning`

### 5. Verification strengthening (data-level)

Applied:
- Extended `VerifyWorkItemFieldsAsync` beyond schema presence checks.
- After validating field existence, the verifier now:
  - runs a small WIQL query to fetch sample work item ids
  - loads sample work items through `workitemsbatch`
  - validates payload presence and expected JSON types for:
    - `Rhodium.Funding.ProjectNumber` → string
    - `Rhodium.Funding.ProjectElement` → string
    - `Microsoft.VSTS.Common.TimeCriticality` → numeric
- Added warning logging for null-only sampled fields.
- Fails verification for:
  - missing sampled payload fields
  - sampled type mismatches

Verification:
- `RealTfsClientVerificationTests.VerifyCapabilitiesAsync_AllChecksPass_ReturnsSuccessReport`
- `RealTfsClientVerificationTests.VerifyCapabilitiesAsync_MissingAnalyticsField_FailsWorkItemFieldVerification`
- `RealTfsClientVerificationTests.VerifyCapabilitiesAsync_WrongFieldTypeInSamplePayload_FailsWorkItemFieldVerification`
- `RealTfsClientVerificationTests.VerifyCapabilitiesAsync_NullOnlySampleValues_LogsWarningButSucceeds`

### 6. Enforce TimeCriticality scope (hard rule)

Applied:
- `TimeCriticality` is forcibly normalized to `null` in domain work item models when `WorkItemType != Feature`.
- No exceptions or fallback coercion were introduced.

Verification:
- Covered by the domain field semantics tests above for both canonical and delivery-trend domain models.

### 7. Protect RoadmapSnapshot boundary

Applied:
- Moved roadmap snapshot persistence entities into UI-specific namespace:
  - `PoTool.Api.Persistence.Entities.UiRoadmap`
- Added explicit comments on roadmap snapshot entities:
  - `Not part of the CDC / analytics domain.`
- Updated `PoToolDbContext` and `RoadmapSnapshotService` references accordingly.

Verification:
- No CDC/domain-layer code references the UI-roadmap namespace.
- Only persistence/db-context/UI-roadmap service paths reference these entities.

## Files changed

- `PoTool.Core/RevisionFieldWhitelist.cs`
- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
- `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs`
- `PoTool.Api/Persistence/Entities/RoadmapSnapshotEntity.cs`
- `PoTool.Api/Persistence/Entities/RoadmapSnapshotItemEntity.cs`
- `PoTool.Api/Persistence/PoToolDbContext.cs`
- `PoTool.Api/Services/RoadmapSnapshotService.cs`
- `PoTool.Tests.Unit/RevisionFieldWhitelistTests.cs`
- `PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs`
- `PoTool.Tests.Unit/Services/ActivityEventIngestionServiceTests.cs`
- `PoTool.Tests.Unit/Services/RealTfsClientVerificationTests.cs`
- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`

## Test coverage added

Added/updated coverage for:
- revision whitelist field inclusion
- OData revision selection mappings
- activity ledger persistence for the three hardening fields
- canonical/domain relevance normalization by work item type
- estimation mode runtime warning guard
- verification sample-payload success/failure/null-only paths

## Build/test results

### Build

- `dotnet restore PoTool.sln` — passed
- `dotnet build PoTool.sln --configuration Release` — passed

### Relevant unit tests

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~RevisionFieldWhitelistTests|FullyQualifiedName~DomainWorkItemFieldSemanticsTests|FullyQualifiedName~ActivityEventIngestionServiceTests|FullyQualifiedName~RealTfsClientVerificationTests|FullyQualifiedName~SprintTrendProjectionServiceTests" -v minimal` — passed

## Remaining risks (if any)

- The revision whitelist now contains OData mappings for the new fields, but actual server-side availability of those OData properties still depends on the target TFS/Azure DevOps deployment exposing them in its analytics model.
- The runtime guard only warns today; Phase B still needs the explicit decision that maps non-default product `EstimationMode` into actual calculation behavior.
- Historical ingestion currently persists field changes generically; later phases that consume these fields from history must still define the exact CDC business behavior for those historical values.
