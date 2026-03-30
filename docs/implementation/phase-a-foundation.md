# Phase A Foundation Implementation

## Summary

Implemented the Phase A CDC foundation contracts needed for funding-field plumbing, explicit estimation mode configuration, backend override plumbing continuity, and hardened TFS capability verification.

The change set keeps lifecycle classification unchanged and avoids Phase B+ behavior such as snapshot creation, Planning Quality integration, forecast changes, or new UI logic.

## Files changed

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs`
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
- `PoTool.Api/Repositories/WorkItemRepository.cs`
- `PoTool.Api/Services/CachedWorkItemReadProvider.cs`
- `PoTool.Api/Services/SyncChangesSummaryService.cs`
- `PoTool.Api/Services/Sync/ValidationComputeStage.cs`
- `PoTool.Api/Services/Sync/WorkItemSyncStage.cs`
- `PoTool.Api/Handlers/WorkItems/GetAllWorkItemsWithValidationQueryHandler.cs`
- `PoTool.Api/Handlers/WorkItems/GetWorkItemByIdWithValidationQueryHandler.cs`
- `PoTool.Shared/WorkItems/WorkItemDto.cs`
- `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Api/Adapters/CanonicalMetricsInputMapper.cs`
- `PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs`
- `PoTool.Shared/Settings/EstimationMode.cs`
- `PoTool.Core.Domain/Models/EstimationMode.cs`
- `PoTool.Shared/Settings/ProductDto.cs`
- `PoTool.Api/Persistence/Entities/ProductEntity.cs`
- `PoTool.Core/Contracts/IProductRepository.cs`
- `PoTool.Core/Settings/Commands/CreateProductCommand.cs`
- `PoTool.Core/Settings/Commands/UpdateProductCommand.cs`
- `PoTool.Api/Handlers/Settings/Products/CreateProductCommandHandler.cs`
- `PoTool.Api/Handlers/Settings/Products/UpdateProductCommandHandler.cs`
- `PoTool.Api/Repositories/ProductRepository.cs`
- `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`
- `PoTool.Api/Services/MockData/MockConfigurationSeedHostedService.cs`
- `PoTool.Api/Migrations/20260326141717_PhaseAFoundationContracts.cs`
- `PoTool.Api/Migrations/20260326141717_PhaseAFoundationContracts.Designer.cs`
- `PoTool.Api/Migrations/PoToolDbContextModelSnapshot.cs`
- `PoTool.Tests.Unit/WorkItemRepositoryTests.cs`
- `PoTool.Tests.Unit/TfsClientTests.cs`
- `PoTool.Tests.Unit/Services/RealTfsClientVerificationTests.cs`
- `PoTool.Tests.Unit/Repositories/ProductTeamLinkRepositoryTests.cs`

## Field contract implementation status

### `Rhodium.Funding.ProjectNumber`

Status: Implemented for retrieval, persistence, DTO exposure, repository mapping, and backend/domain plumbing.

Completed:
- Added to TFS required work item field retrieval.
- Parsed from TFS responses in work-item retrieval paths.
- Added to `WorkItemEntity`.
- Added to `WorkItemDto` and `WorkItemWithValidationDto`.
- Added to repository and sync/read-model mappings.
- Added to backend canonical/delivery-trend work-item models as passive metadata for future CDC phases.

Semantic boundary:
- Field is allowed on generic contracts but no new business logic consumes non-Epic values.

### `Rhodium.Funding.ProjectElement`

Status: Implemented for retrieval, persistence, DTO exposure, repository mapping, and backend/domain plumbing.

Completed:
- Added to TFS required work item field retrieval.
- Parsed from TFS responses in work-item retrieval paths.
- Added to `WorkItemEntity`.
- Added to `WorkItemDto` and `WorkItemWithValidationDto`.
- Added to repository and sync/read-model mappings.
- Added to backend canonical/delivery-trend work-item models as passive metadata for future CDC phases.

Semantic boundary:
- Field is allowed on generic contracts but no new business logic consumes non-Epic values.

### `Microsoft.VSTS.Common.TimeCriticality`

Status: Completed/finalized in the remaining Phase A plumbing gaps.

Completed in this phase:
- Verified the field was already present in current retrieval/persistence/progress paths.
- Preserved null semantics (`null` = no override, `0` = explicit zero).
- Extended backend canonical work-item plumbing so the override field can continue flowing through backend CDC models without UI inference.
- Included the field in strengthened capability verification.

## EstimationMode implementation status

Status: Implemented as an explicit contract without changing current calculation semantics.

Completed:
- Added explicit enum values:
  - `StoryPoints`
  - `EffortHours`
  - `Mixed`
  - `NoSpMode`
- Added shared/API settings contract in `PoTool.Shared/Settings/EstimationMode.cs`.
- Added domain-side CDC contract in `PoTool.Core.Domain/Models/EstimationMode.cs`.
- Added `EstimationMode` to `ProductDto`.
- Added persisted `EstimationMode` to `ProductEntity` and `ProductRepository` create/update/read mapping.
- Added command/repository plumbing so product settings remain the source of truth.
- Added migration `20260326141717_PhaseAFoundationContracts` for persistence.

Intentionally unchanged:
- Existing feature-progress calculation behavior remains unchanged.
- No mixed-mode heuristics were introduced.
- No null/non-null inference was added on the client.

## Verification hardening status

Status: Implemented.

Completed:
- `VerifyWorkItemFieldsAsync` now validates the full runtime field contract from `RequiredWorkItemFields` instead of only four generic fields.
- Verification now covers at least:
  - `Microsoft.VSTS.Scheduling.Effort`
  - `Rhodium.Funding.ProjectNumber`
  - `Rhodium.Funding.ProjectElement`
  - `Microsoft.VSTS.Common.TimeCriticality`
- Updated verification tests to cover both success and missing-analytics-field failure scenarios.

## Architectural notes

- `StateClassification` was left unchanged and remains limited to `New`, `InProgress`, `Done`, and `Removed`.
- No readiness classification was merged into lifecycle mapping.
- No snapshot creation logic was added.
- No Planning Quality integration was added.
- No override dictionaries were introduced as a source of truth.
- No UI-side recomputation or new UI rendering logic was introduced.
- Product estimation mode is persisted explicitly, but existing progress services still use their current calculation path; Phase B can map the declared setting into later CDC behavior without semantic drift.
- Revision whitelist/history ingestion was not expanded in this phase because the added fields are not yet consumed by any current historical Phase A pipeline. The current implementation focuses on the required retrieval → persistence → DTO → backend-model contract.

## Deviations from prompt, if any

- No behavioral deviation was introduced.
- The only scoped omission is revision-whitelist expansion for the new fields. The prompt made that conditional on later-phase history needs; current Phase A plumbing does not yet require these fields in revision-history ingestion, so this was intentionally left unchanged to avoid speculative historical contract changes.

## Build/test results

### Required build

- `dotnet build PoTool.sln --configuration Release` — passed

### Targeted tests for touched areas

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~WorkItemRepositoryTests|FullyQualifiedName~TfsClientTests|FullyQualifiedName~RealTfsClientVerificationTests|FullyQualifiedName~ProductTeamLinkRepositoryTests" -v minimal` — passed

### Migration validation

Validated generated migration with a temporary SQLite database:
- Applied `20260326141717_PhaseAFoundationContracts`
- Rolled back to `20260325213949_AddTimeCriticalityToWorkItems`
- Reapplied `20260326141717_PhaseAFoundationContracts`

## Risks or follow-up items for Phase B

- Phase B still needs an explicit mapping from persisted product `EstimationMode` into runtime CDC progress-mode decisions.
- `Mixed` and `EffortHours` are now represented explicitly but intentionally do not change current delivery-progress behavior yet.
- If a later phase requires historical change tracking for `ProjectNumber`, `ProjectElement`, or `TimeCriticality`, `RevisionFieldWhitelist` and the historical ingestion path should be expanded deliberately through the active revision APIs rather than inferred speculatively.
