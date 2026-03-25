# Field Contract & Usage Analysis

## 1. Summary

This report traces the repository support for four fields:

- `Rhodium.Funding.ProjectNumber`
- `Rhodium.Funding.ProjectElement`
- `Microsoft.VSTS.Scheduling.Effort`
- `Microsoft.VSTS.Common.TimeCriticality`

The current contract is uneven:

- `Microsoft.VSTS.Scheduling.Effort` is implemented across retrieval, revision ingestion, persistence, DTOs, and analytics.
- `Rhodium.Funding.ProjectNumber`, `Rhodium.Funding.ProjectElement`, and `Microsoft.VSTS.Common.TimeCriticality` have no references in the current codebase.
- The capability verification path only checks a small generic field set, so even the supported custom/analytics fields are not explicitly validated during connection verification.

## 2. Rhodium.Funding.ProjectNumber

### Where used

No references were found for `Rhodium.Funding.ProjectNumber` anywhere in the repository.

### Where missing

- Not included in the work-item retrieval field list in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`.
- Not included in the revision whitelist in `PoTool.Core/RevisionFieldWhitelist.cs`.
- Not mapped on `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
- Not exposed on `PoTool.Shared/WorkItems/WorkItemDto.cs` or `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
- Not mapped by `PoTool.Api/Repositories/WorkItemRepository.cs`.
- Not used by `PoTool.Api/Services/ActivityEventIngestionService.cs`, because ingestion only keeps fields from `RevisionFieldWhitelist.Fields`.
- Not used in handlers, validators, UI, analytics, tests, or repository documentation.

### Work item types where it is expected but not used

No current domain rule, validator, or DTO contract defines a supported work item type for `Rhodium.Funding.ProjectNumber`. In practice, that means the field is unsupported for all current work item types (`Goal`, `Objective`, `Epic`, `Feature`, `Product Backlog Item`, `Bug`, `Task`).

## 3. Rhodium.Funding.ProjectElement

### Where used

No references were found for `Rhodium.Funding.ProjectElement` anywhere in the repository.

### Where missing

- Not included in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` `RequiredWorkItemFields`.
- Not included in `PoTool.Core/RevisionFieldWhitelist.cs`.
- Not mapped on `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
- Not exposed on `PoTool.Shared/WorkItems/WorkItemDto.cs` or `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
- Not mapped by `PoTool.Api/Repositories/WorkItemRepository.cs`.
- Not eligible for historical ingestion in `PoTool.Api/Services/ActivityEventIngestionService.cs`.
- Not used in any logic layer, validation path, or tests.

### Work item types where it is expected but not used

As with `Rhodium.Funding.ProjectNumber`, the repository contains no type-specific contract for `Rhodium.Funding.ProjectElement`. The field is therefore unsupported for every currently modeled work item type.

## 4. Microsoft.VSTS.Scheduling.Effort

### Where used

#### Retrieval and field whitelists

- Declared as `TfsFieldEffort` and included in `RequiredWorkItemFields` in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`.
- Included in the revision whitelist as `Scalar("Microsoft.VSTS.Scheduling.Effort", "Effort")` in `PoTool.Core/RevisionFieldWhitelist.cs`.
- Historical revision ingestion uses that whitelist through `PoTool.Api/Services/ActivityEventIngestionService.cs`.

#### DTO and entity mapping

- Persisted as `Effort` in `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
- Exposed in `PoTool.Shared/WorkItems/WorkItemDto.cs`.
- Exposed in `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
- Mapped in both directions by `PoTool.Api/Repositories/WorkItemRepository.cs`.
- Used in the backlog-quality snapshot model as `Effort` in `PoTool.Core.Domain/BacklogQuality/Models/WorkItemSnapshot.cs`.

#### Logic usage

- The domain rules in `docs/domain/domain_model.md` and `docs/domain/rules/estimation_rules.md` define Effort as an hours-based metric that may appear on `Epic`, `Feature`, and `PBI`.
- `PoTool.Api/Services/SprintTrendProjectionService.cs` tracks effort change history with `EffortFieldRef = "Microsoft.VSTS.Scheduling.Effort"`.
- The backlog-quality slice carries Effort in `WorkItemSnapshot`.
- A legacy validation path still checks for missing effort in `PoTool.Core/WorkItems/Validators/WorkItemInProgressWithoutEffortValidator.cs`, although the class is explicitly marked deprecated and unregistered.

### Where missing

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` does not verify Effort availability. `VerifyWorkItemFieldsAsync` only checks `System.Id`, `System.Title`, `System.State`, and `System.WorkItemType`.
- No dedicated connection-time capability check confirms that `Microsoft.VSTS.Scheduling.Effort` is available before analytics depending on it run.

### Work item types where it is expected but not used

The documented domain contract says Effort may exist on `Epic`, `Feature`, and `PBI`. Current code clearly supports Effort end to end, but there is no separate repository-level contract that limits or validates Effort by work item type at the TFS retrieval boundary. The only explicit type-aware expectation surfaced by the docs is the domain rule itself.

## 5. Microsoft.VSTS.Common.TimeCriticality

### Where used

No references were found for `Microsoft.VSTS.Common.TimeCriticality` anywhere in the repository.

### Where missing

- Not included in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` `RequiredWorkItemFields`.
- Not included in `PoTool.Core/RevisionFieldWhitelist.cs`.
- Not mapped on `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
- Not exposed on `PoTool.Shared/WorkItems/WorkItemDto.cs` or `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
- Not mapped by `PoTool.Api/Repositories/WorkItemRepository.cs`.
- Not ingested by `PoTool.Api/Services/ActivityEventIngestionService.cs`.
- Not used in domain rules, handlers, UI, reporting, or tests.

### Work item types where it is expected but not used

No domain rule or application contract in the repository declares which work item types should carry `Microsoft.VSTS.Common.TimeCriticality`. As implemented today, the field is unsupported across all modeled work item types.

## 6. Risks / gaps

1. **Funding and time-criticality fields are absent end to end**  
   `Rhodium.Funding.ProjectNumber`, `Rhodium.Funding.ProjectElement`, and `Microsoft.VSTS.Common.TimeCriticality` are not retrieved, persisted, surfaced, or analyzed. Any downstream feature expecting them will silently have no data source.

2. **Revision-history coverage is incomplete for unsupported fields**  
   `ActivityEventIngestionService` only keeps fields from `RevisionFieldWhitelist.Fields`, so unsupported fields cannot appear in the activity ledger or any history-based analytics.

3. **Connection verification is narrower than the actual runtime contract**  
   `VerifyWorkItemFieldsAsync` validates only four generic fields. That means the TFS capability check can pass even when `Effort` or any future custom required fields are unavailable.

4. **No work-item-type contract exists for the missing fields**  
   The codebase does not define which types should carry `ProjectNumber`, `ProjectElement`, or `TimeCriticality`, so adding them later will require an explicit domain decision instead of a mechanical plumbing change.

## 7. Required changes for full support

### For `Rhodium.Funding.ProjectNumber`

1. Add the field to `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` `RequiredWorkItemFields`.
2. Add the field to `PoTool.Core/RevisionFieldWhitelist.cs` if historical changes must be tracked.
3. Add a property to `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
4. Add DTO fields to `PoTool.Shared/WorkItems/WorkItemDto.cs` and `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
5. Update `PoTool.Api/Repositories/WorkItemRepository.cs` mappings.
6. Define the supported work item types and any analytics/reporting semantics before adding logic.

### For `Rhodium.Funding.ProjectElement`

1. Add the field to `RequiredWorkItemFields`.
2. Add it to `RevisionFieldWhitelist` if history is needed.
3. Persist and map it through `WorkItemEntity`, `WorkItemDto`, `WorkItemWithValidationDto`, and `WorkItemRepository`.
4. Define whether the field is display-only metadata or participates in analytics before using it in logic.

### For `Microsoft.VSTS.Common.TimeCriticality`

1. Add the field to `RequiredWorkItemFields`.
2. Add it to `RevisionFieldWhitelist` if historical trend or activity use is required.
3. Persist and map it through `WorkItemEntity`, DTOs, and `WorkItemRepository`.
4. Define the domain meaning and supported work item types before introducing calculations or validation.

### For the existing Effort contract

1. Extend `PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` so `VerifyWorkItemFieldsAsync` checks the runtime-required analytics fields, not only the four generic display fields.
2. Keep `Effort` separate from any future funding/time-criticality additions; current usage already treats it as an established field contract.
