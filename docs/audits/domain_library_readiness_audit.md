# PoTool Domain Library Extraction Readiness Audit

_Generated: 2026-03-14_

## Summary

### Files analyzed

- `docs/domain/domain_model.md`
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/propagation_rules.md`
- `docs/audits/metrics_audit.md`
- `docs/audits/hierarchy_propagation_audit.md`
- `docs/audits/projection_trend_pipeline_audit.md`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
- `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
- `PoTool.Api/Services/SprintCommitmentLookup.cs`
- `PoTool.Api/Services/FirstDoneDeliveryLookup.cs`
- `PoTool.Api/Services/SprintSpilloverLookup.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Services/StateClassificationLookup.cs`
- `PoTool.Api/Services/StateReconstructionLookup.cs`
- `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs`
- `PoTool.Shared/Metrics/SprintExecutionDtos.cs`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs`
- `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`
- `PoTool.Shared/WorkItems/WorkItemDto.cs`

### Verdict

**Needs fixes before CDC extraction**

The repository is semantically stable enough to extract a Canonical Domain Component, but it is not extraction-ready yet. The canonical sprint analytics rules are already implemented consistently through shared helpers and a shared story-point resolver, and DTOs in `PoTool.Shared` remain data-only. The remaining blockers are architectural: the core sprint-history helpers still live in `PoTool.Api` and depend directly on API persistence entities, and a small set of sprint-execution, hierarchy-rollup, and forecasting formulas are still owned by handlers or API services instead of a dedicated domain service surface.

Extraction therefore appears **feasible with targeted boundary refactors, not with a large redesign**, but the current code should not be moved into `PoTool.Core.Domain` or `PoTool.Domain` unchanged.

## Domain Rules Reviewed

- `docs/domain/domain_model.md` §§ 2.2-2.8, 3.3-3.9, 5.1-5.10, 7
- `docs/domain/rules/sprint_rules.md`
- `docs/domain/rules/metrics_rules.md`
- `docs/domain/rules/estimation_rules.md`
- `docs/domain/rules/hierarchy_rules.md`
- `docs/domain/rules/propagation_rules.md`

## Compliant Areas

- **Sprint commitment, first-Done delivery, and spillover semantics are already centralized in reusable helpers.**  
  `SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, and `SprintSpilloverLookup` are each consumed by more than one production path. `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, and `SprintTrendProjectionService` all reuse the same commitment and delivery reconstruction instead of maintaining competing sprint formulas.

- **Story-point resolution is already centralized in Core.**  
  `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs` is the authoritative implementation of `StoryPoints -> BusinessValue -> Missing`, the zero-point Done rule, and sibling-derived estimates. Both handler and projection code call this shared service rather than implementing alternative estimation rules.

- **`GetSprintMetricsQueryHandler` and `GetSprintTrendMetricsQueryHandler` are mostly orchestration-focused.**  
  `GetSprintMetricsQueryHandler` loads scope, delegates commitment and first-Done logic to the shared lookups, then sums resolved story points. `GetSprintTrendMetricsQueryHandler` reads or recomputes projections and maps projection rows into DTOs without recalculating alternate sprint metrics.

- **DTOs in `PoTool.Shared` do not execute business logic.**  
  `SprintExecutionDtos.cs`, `SprintTrendDtos.cs`, `EpicCompletionForecastDto.cs`, and `WorkItemDto.cs` are record contracts with data and XML documentation only. They describe formulas but do not compute them, so the repository does not currently embed calculation logic inside transport contracts.

- **The current implementation is already backed by prior semantic audits.**  
  `docs/audits/metrics_audit.md`, `docs/audits/hierarchy_propagation_audit.md`, and `docs/audits/projection_trend_pipeline_audit.md` all show the current sprint analytics behavior is canonically aligned. This readiness audit therefore focuses on extraction boundaries and ownership, not on re-opening already-fixed semantics.

## Violations Found

| Priority | File | Class | Method | Rule violated |
| --- | --- | --- | --- | --- |
| P0 | `PoTool.Api/Services/SprintCommitmentLookup.cs` | `SprintCommitmentLookup` | `BuildCommittedWorkItemIds`, `GetIterationPathAtTimestamp` | Canonical sprint-commitment reconstruction is reusable, but it still lives in `PoTool.Api` and depends directly on `ActivityEventLedgerEntryEntity`. This violates the audit requirement that domain services be extractable without API/persistence coupling and blocks direct movement into `PoTool.Core.Domain`. |
| P0 | `PoTool.Api/Services/FirstDoneDeliveryLookup.cs` | `FirstDoneDeliveryLookup` | `Build`, `GetFirstDoneTransitionTimestamp` | Canonical delivery attribution is centralized, but the helper still depends on API persistence entities and the API-local state lookup. This violates the cross-service dependency rule that extracted domain services should depend only on Core/minimal abstractions. |
| P0 | `PoTool.Api/Services/SprintSpilloverLookup.cs` | `SprintSpilloverLookup` | `BuildSpilloverWorkItemIds`, `GetNextSprintPath` | Canonical spillover detection is centralized, but it requires `SprintEntity` and `ActivityEventLedgerEntryEntity` from the API layer. The service cannot be moved into a shared domain library unchanged, so CDC extraction is blocked at the current boundary. |
| P1 | `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | `Handle` | The handler reconstructs the canonical work-item sets correctly, but it still computes `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate` inline. This violates the audit goal that handlers should orchestrate reusable domain services rather than remain the home of sprint-execution formulas. |
| P1 | `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `GetEpicCompletionForecastQueryHandler` | `RollupCanonicalScope`, `RollupFeatureScope`, `RollupPbiChildren`, `Handle` | Hierarchy rollup and remaining-sprints forecast formulas remain handler-owned. The same canonical rollup pattern also exists inside `SprintTrendProjectionService`, so the domain surface is not yet single-homed for extraction. |

## Architectural Risks

- **Supporting state helpers are still API-local.**  
  `StateClassificationLookup` and `StateReconstructionLookup` are internal API services used by the canonical sprint helpers. Even after moving the three sprint lookups, CDC extraction would still need equivalent state-mapping and point-in-time reconstruction abstractions outside `PoTool.Api`.

- **Hierarchy rollup orchestration is compliant but duplicated.**  
  `GetEpicCompletionForecastQueryHandler` and `SprintTrendProjectionService` both implement canonical PBI-to-Feature/Epic rollup orchestration around `CanonicalStoryPointResolutionService`. The logic is aligned today, but future rule changes would require coordinated edits unless that orchestration becomes a reusable domain service.

- **Current helper signatures are shaped around EF entities rather than domain inputs.**  
  `WorkItemEntity`, `SprintEntity`, and `ActivityEventLedgerEntryEntity` are passed directly through the sprint-history helpers. This means extraction requires introducing minimal domain inputs for work item snapshots, sprint windows, and field-change history instead of simply moving files.

- **A few APIs still preserve legacy naming that is awkward for a clean domain package.**  
  `EpicCompletionForecastDto` and feature/epic progress contracts retain `*Effort` names for story-point-based scope values for API compatibility. This does not break DTO separation, but it is a friction point if the CDC is intended to publish a clean domain-first vocabulary.

- **Canonical services are now enforced through DI in the current metrics/projection paths.**  
  `GetEpicCompletionForecastQueryHandler` and `SprintTrendProjectionService` now consume the shared canonical story-point and hierarchy rollup services through injected dependencies only, including the internal sprint projection helper paths.

## Recommended Fixes

1. **Introduce minimal domain abstractions for historical inputs.**  
   Define lightweight domain records or interfaces for sprint metadata, work item snapshots, and field-change history so the canonical sprint helpers no longer depend on `PoTool.Api.Persistence.Entities`.

2. **Move the canonical sprint-history helpers together with their state helpers.**  
   Extract `SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, `SprintSpilloverLookup`, `StateClassificationLookup`, and `StateReconstructionLookup` into `PoTool.Core.Domain` or `PoTool.Domain` as one coherent package, rather than moving only the first three.

3. **Create a reusable sprint execution formula service.**  
   Move `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate` calculations out of `GetSprintExecutionQueryHandler` and into a domain calculator that accepts already-reconstructed story-point totals.

4. **Centralize hierarchy rollup and forecast-support logic.**  
   Extract the shared PBI → Feature → Epic rollup orchestration currently split between `GetEpicCompletionForecastQueryHandler` and `SprintTrendProjectionService` into one reusable domain service or helper.

5. **Keep `PoTool.Shared` as transport-only and adapt at the boundary.**  
   Do not move DTOs into the CDC. If API compatibility requires legacy names such as `TotalEffort`, keep those names in the API contract and map them from cleaner domain concepts inside the handler/service boundary.

6. **Preserve injected CDC service usage end to end.**  
   Future changes must keep the sprint projection helpers and handler orchestration on injected canonical services so decoration or alternate implementations remain possible during CDC extraction.

## Final Readiness Classification

**Needs fixes before CDC extraction**

### Readiness conclusion

- **Stable semantics:** Yes
- **Shared canonical services exist:** Partially
- **Handlers are pure orchestrators:** Not yet
- **DTOs are data-only:** Yes
- **Cross-layer extraction blockers remain:** Yes
- **Feasible without major redesign:** Yes, with targeted boundary refactors

### Prioritized extraction-prep list

1. Decouple canonical sprint helpers from `PoTool.Api.Persistence.Entities`.
2. Extract the missing sprint-execution formula surface from `GetSprintExecutionQueryHandler`.
3. Centralize hierarchy rollup/forecast orchestration that is currently split between the forecast handler and sprint trend projection service.
4. Keep API DTOs as adapters around the CDC rather than treating them as the domain contract.

## Fix Progress — Domain Input Model Boundary

- Added minimal historical domain input models in `PoTool.Core/Metrics/Models/HistoricalSprintInputs.cs`:
  - `WorkItemSnapshot`
  - `SprintDefinition`
  - `FieldChangeEvent`
- Kept persistence-to-domain mapping inside the API layer with `PoTool.Api/Services/HistoricalSprintInputMapper.cs`, including adapters from:
  - `WorkItemEntity` and `WorkItemDto` to `WorkItemSnapshot`
  - `SprintEntity` and `SprintDto` to `SprintDefinition`
  - `ActivityEventLedgerEntryEntity` to `FieldChangeEvent`
- Updated canonical sprint-history helpers to consume the new domain-facing inputs instead of `PoTool.Api.Persistence.Entities`:
  - `SprintCommitmentLookup`
  - `FirstDoneDeliveryLookup`
  - `SprintSpilloverLookup`
  - `StateReconstructionLookup`
  - `StateClassificationLookup` now also supports `WorkItemSnapshot`
- Updated current API callers to supply mapped domain inputs without changing sprint analytics behavior:
  - `GetSprintMetricsQueryHandler`
  - `GetSprintExecutionQueryHandler`
  - `SprintTrendProjectionService`
- Added focused unit coverage for:
  - persistence-to-domain input mapping correctness
  - canonical helper behavior using the new domain inputs

## Fix Progress — Coherent Domain Surface for Sprint History

- Grouped the canonical sprint-history helpers into `PoTool.Core.Domain/Domain/Sprints` so the domain surface now collects:
  - `SprintCommitmentLookup`
  - `FirstDoneDeliveryLookup`
  - `SprintSpilloverLookup`
  - `StateClassificationLookup`
  - `StateReconstructionLookup`
- Reduced API coupling by moving the fallback state-classification source into `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs`; the helper group no longer depends on `PoTool.Api.Services.WorkItemStateClassificationService`.
- Updated existing API consumers to use the Core domain helper namespace without changing reconstruction semantics:
  - `GetSprintMetricsQueryHandler`
  - `GetSprintExecutionQueryHandler`
  - `SprintTrendProjectionService`
  - `WorkItemStateClassificationService` now reuses the Core fallback classification source instead of owning a duplicate API-local copy
- Added focused unit coverage in `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` for:
  - canonical state mapping
  - point-in-time state reconstruction
  - existing commitment, first-Done, and spillover reconstruction behavior

## Fix Progress — Sprint Execution Formula Extraction

- Moved canonical sprint execution formulas out of `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` for:
  - `ChurnRate`
  - `CommitmentCompletion`
  - `SpilloverRate`
  - `AddedDeliveryRate`
- Added reusable Core calculator `PoTool.Core/Metrics/Services/SprintExecutionMetricsCalculator.cs` that accepts reconstructed story-point totals and returns the canonical derived rates with the existing safe zero-denominator behavior.
- Simplified `GetSprintExecutionQueryHandler` so it still reconstructs committed, added, removed, delivered, and spillover totals, but now delegates formula calculation to the reusable calculator before mapping `SprintExecutionSummaryDto`.
- Added focused calculator coverage in `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`, while keeping the existing handler-level sprint execution tests in place.

## Fix Progress — Centralized Hierarchy Rollup Orchestration

- Removed duplicated hierarchy rollup orchestration from:
  - `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
- Introduced shared Core service `PoTool.Core/Metrics/Services/HierarchyRollupService.cs` to centralize:
  - PBI-based feature scope
  - feature-based epic scope
  - canonical parent fallback behavior
  - bug/task exclusion from story-point rollups
- Updated the epic forecast handler and sprint trend projection service to delegate canonical hierarchy scope calculation to the shared service while preserving the existing story-point resolution semantics.
- Added focused rollup coverage in `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs` for:
  - feature scope rollups
  - epic scope rollups
  - parent fallback behavior
  - bug/task exclusion
  - derived estimate handling

## Fix Progress — DI Enforcement for Canonical Services

- Removed direct `new CanonicalStoryPointResolutionService()` and `new HierarchyRollupService(...)` fallback paths from `PoTool.Api/Services/SprintTrendProjectionService.cs`.
- Kept `GetEpicCompletionForecastQueryHandler` on constructor-injected shared services only; no direct canonical resolver construction remains in the handler.
- Confirmed DI registrations already provide the shared canonical services consumed by current metrics/projection paths:
  - `ICanonicalStoryPointResolutionService`
  - `IHierarchyRollupService`
  - `SprintTrendProjectionService`
- Added focused test coverage for DI enforcement and injectable doubles:
  - `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`
  - `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`

## Re-Audit Results — CDC Extraction Readiness

### Summary of improvements

- **Canonical sprint-history helpers are now extraction-safe domain code.**  
  `PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs`, `FirstDoneDeliveryLookup.cs`, `SprintSpilloverLookup.cs`, `StateClassificationLookup.cs`, and `StateReconstructionLookup.cs` now operate on `WorkItemSnapshot`, `SprintDefinition`, and `FieldChangeEvent` from `PoTool.Core.Domain/Models/HistoricalSprintInputs.cs` instead of `PoTool.Api.Persistence.Entities`.

- **API persistence translation is isolated at the boundary.**  
  `PoTool.Api/Services/HistoricalSprintInputMapper.cs` keeps `WorkItemEntity`, `SprintEntity`, `ActivityEventLedgerEntryEntity`, and DTO translation in the API layer, so the canonical sprint helpers no longer need API or EF types to run.

- **Sprint execution formulas are now reusable Core logic.**  
  `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` delegates canonical `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate` calculation to `PoTool.Core/Metrics/Services/SprintExecutionMetricsCalculator.cs` through `ISprintExecutionMetricsCalculator`.

- **Hierarchy rollup orchestration is now centralized.**  
  `PoTool.Core/Metrics/Services/HierarchyRollupService.cs` is the shared implementation for canonical feature/epic scope rollups, and both `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` and `PoTool.Api/Services/SprintTrendProjectionService.cs` now consume it instead of maintaining competing rollup implementations.

- **Canonical services are consumed through DI and DTOs remain transport-only.**  
  `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` registers `ICanonicalStoryPointResolutionService`, `IHierarchyRollupService`, and `ISprintExecutionMetricsCalculator`, while `PoTool.Shared/Metrics/SprintExecutionDtos.cs`, `PoTool.Shared/Metrics/EpicCompletionForecastDto.cs`, and `PoTool.Shared/WorkItems/WorkItemDto.cs` remain data contracts only.

### Remaining blockers

- **No extraction-blocking redesign issues remain in the canonical domain surface audited in the original report.**  
  The previously blocking concerns around persistence-coupled sprint helpers, handler-owned sprint execution formulas, duplicated hierarchy rollups, and direct canonical-service construction have been cleared.

- **Minor cleanup only:**  
  `PoTool.Api/Services/HistoricalSprintInputMapper.cs` should stay an API-side adapter when the CDC package is created, or be moved to an adjacent adapter namespace during the extraction PR if that improves packaging clarity.  
  `PoTool.Api/Services/SprintTrendProjectionService.cs` still contains API orchestration and persistence access, but its canonical scope and estimation logic now flow through injected shared services; that remaining API orchestration does not block extraction of the CDC itself.

### Test coverage check

- **Domain input model boundaries:** covered by `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs` and `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs`.
- **Extracted sprint execution formulas:** covered by `PoTool.Tests.Unit/Services/SprintExecutionMetricsCalculatorTests.cs`, with handler-level regression coverage in `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`.
- **Centralized hierarchy rollups:** covered by `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`, with consuming-path coverage in `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs` and `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`.
- **DI-based canonical service consumption:** covered by `PoTool.Tests.Unit/Configuration/ServiceCollectionTests.cs`, plus injectable-double coverage in `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs` and `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs`.

### Final readiness verdict

**Mostly ready with minor cleanup**

## CDC Extraction Progress — Domain Models Moved

- Created the pure domain project `PoTool.Core.Domain/PoTool.Core.Domain.csproj` and added it to `PoTool.sln`.
- Moved the canonical domain input models into the CDC package:
  - `PoTool.Core.Domain/Models/HistoricalSprintInputs.cs`
    - `WorkItemSnapshot`
    - `SprintDefinition`
    - `FieldChangeEvent`
  - `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`
- Updated Core, API, and unit-test references/usings so canonical domain model consumption now flows through `PoTool.Core.Domain.Models`, while API mappers remain in the API layer.
- Tests passing:
  - `dotnet build PoTool.sln --no-restore`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~HistoricalSprintInputMapperTests|FullyQualifiedName~HistoricalSprintLookupTests|FullyQualifiedName~CanonicalStoryPointResolutionServiceTests|FullyQualifiedName~HierarchyRollupServiceTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~GetEpicCompletionForecastQueryHandlerTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~SprintTrendProjectionServiceTests" -v minimal`

**Outcome: CDC extraction ready after minor cleanup**

The repository now satisfies the target CDC readiness conditions for the canonical sprint helpers, sprint execution formulas, hierarchy rollup service, DTO boundaries, and DI consumption. Extraction to `PoTool.Core.Domain` or `PoTool.Domain` is now feasible without redesign; the remaining work is packaging and boundary cleanup, not another round of semantic refactoring.

### Recommended next step

Create the CDC package and move the canonical services into it:

- `PoTool.Core.Domain/Domain/Sprints/*`

## CDC Extraction Progress — Sprint Domain Services Moved

- **Services moved:**  
  `SprintCommitmentLookup`, `FirstDoneDeliveryLookup`, `SprintSpilloverLookup`, `StateClassificationLookup`, `StateReconstructionLookup`, and supporting fallback mappings in `StateClassificationDefaults` now physically live under `PoTool.Core.Domain/Domain/Sprints`.

- **Consumers updated:**  
  `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, `SprintTrendProjectionService`, and `WorkItemStateClassificationService` continue consuming the same `PoTool.Core.Domain.Sprints` namespace, so no behavior change was required when the files moved into the CDC assembly.

- **DI updated:**  
  `PoTool.Core.Domain/PoTool.Core.Domain.csproj` now references `PoTool.Shared` so the moved CDC services can keep using `WorkItemStateClassificationDto` without leaking API dependencies. No additional runtime DI registrations were required because the moved sprint helpers remain static domain services.

- **Tests passing:**  
  Verified with `dotnet restore PoTool.sln`, `dotnet build PoTool.sln --no-restore`, and `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~HistoricalSprintLookupTests|FullyQualifiedName~HistoricalSprintInputMapperTests|FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`.
- `PoTool.Core/Metrics/Models/HistoricalSprintInputs.cs`
- `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs`
- `PoTool.Core/Metrics/Services/SprintExecutionMetricsCalculator.cs`
- `PoTool.Core/Metrics/Services/HierarchyRollupService.cs`

Keep API-specific mappers, handlers, EF queries, and projection orchestration in `PoTool.Api` as boundary adapters around the extracted CDC package.

## Fix Progress — DTO Boundary Cleanup for Canonical Metrics Services

- Identified one remaining CDC extraction blocker after the prior re-audit: `PoTool.Core/Metrics/Services/CanonicalStoryPointResolutionService.cs` and `PoTool.Core/Metrics/Services/HierarchyRollupService.cs` still depended on `PoTool.Shared/WorkItems/WorkItemDto`, which is a transport DTO rather than a domain input model.
- Introduced a minimal Core metrics input model in `PoTool.Core/Metrics/Models/CanonicalWorkItem.cs` with only the fields required by the canonical metrics services:
  - `WorkItemId`
  - `WorkItemType`
  - `ParentWorkItemId`
  - `BusinessValue`
  - `StoryPoints`
- Kept the DTO/entity → domain mapping boundary in the API layer with `PoTool.Api/Services/CanonicalMetricsInputMapper.cs`, so:
  - `WorkItemDto` is translated before invoking Core metrics services
  - `WorkItemEntity` is translated before invoking Core metrics services
  - canonical Core services no longer depend on API or transport contracts
- Updated current consumers to map into `CanonicalWorkItem` before invoking the canonical services, without changing semantics:
  - `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
- Added focused coverage for the new API-side mapping boundary in `PoTool.Tests.Unit/Services/HistoricalSprintInputMapperTests.cs`, and updated canonical Core service tests to run against the new domain input model:
  - `PoTool.Tests.Unit/Services/CanonicalStoryPointResolutionServiceTests.cs`
  - `PoTool.Tests.Unit/Services/HierarchyRollupServiceTests.cs`

## Re-Audit Addendum — Canonical Metrics Service Independence

### Finding

- **Previously missed DTO coupling in Core metrics services:**  
  The earlier re-audit correctly cleared the sprint-history helpers, sprint execution calculator, and DI construction issues, but it overstated DTO-boundary readiness for the canonical metrics services. `CanonicalStoryPointResolutionService` and `HierarchyRollupService` still accepted `WorkItemDto`, which would have forced the CDC package to reference `PoTool.Shared` transport contracts.

### Result after cleanup

- **Canonical Core metrics services are now domain-input only.**  
  `CanonicalStoryPointResolutionService` and `HierarchyRollupService` now operate on `CanonicalWorkItem` from `PoTool.Core/Metrics/Models/CanonicalWorkItem.cs`, while API handlers/services translate `WorkItemDto` and `WorkItemEntity` at the boundary.

- **API → domain mapping remains explicit.**  
  The repository now consistently applies the same boundary pattern already used by `HistoricalSprintInputMapper`:
  - infrastructure/transport types in `PoTool.Api`
  - minimal domain inputs in `PoTool.Core`
  - canonical services consume only domain inputs

- **No remaining production direct construction was found for the audited canonical services.**  
  A repository-wide search confirmed that direct construction patterns remain only in unit tests; production paths continue to use DI registrations in `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`.

### Updated readiness verdict

**Ready for CDC extraction**

The final remaining architectural coupling risk from the `domain_library_readiness` cleanup sprint has been removed. The canonical sprint-history helpers, sprint execution calculator, canonical story-point resolver, and hierarchy rollup service now all satisfy the extraction requirement that domain services depend only on Core domain input models, with API-side mapping and DI boundaries preserved.

## CDC Extraction Progress — Metrics and Hierarchy Services Moved

- **Services moved:**  
  `CanonicalStoryPointResolutionService`, `SprintExecutionMetricsCalculator`, and `HierarchyRollupService` now physically live under `PoTool.Core.Domain/Domain/Estimation`, `PoTool.Core.Domain/Domain/Metrics`, and `PoTool.Core.Domain/Domain/Hierarchy`, with CDC namespaces:
  - `PoTool.Core.Domain.Estimation`
  - `PoTool.Core.Domain.Metrics`
  - `PoTool.Core.Domain.Hierarchy`
  - supporting domain-only helper `PoTool.Core.Domain/Domain/WorkItems/CanonicalWorkItemTypes.cs`

- **Consumers updated:**  
  API handlers and services now reference the moved CDC namespaces without changing DTO or adapter boundaries:
  - `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs`
  - `PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

- **DI confirmed:**  
  Existing singleton registrations continue to resolve the moved CDC services through their interfaces:
  - `ICanonicalStoryPointResolutionService`
  - `ISprintExecutionMetricsCalculator`
  - `IHierarchyRollupService`
  The registrations remain in the API composition root while the implementations now live in the CDC assembly.

- **Tests passing:**  
  Verified with `dotnet build PoTool.sln --no-restore` and `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CanonicalStoryPointResolutionServiceTests|FullyQualifiedName~SprintExecutionMetricsCalculatorTests|FullyQualifiedName~HierarchyRollupServiceTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~GetEpicCompletionForecastQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~ServiceCollectionTests" -v minimal`.

## CDC Extraction Progress — API Boundary Cleanup

- **Adapter boundaries confirmed:**  
  `HistoricalSprintInputMapper` and `CanonicalMetricsInputMapper` remain the API-side translation seam for EF entities, DTOs, and API-shaped inputs. State-classification transport models now follow the same pattern via `PoTool.Api/Services/StateClassificationInputMapper.cs`, which maps `PoTool.Shared.Settings.WorkItemStateClassificationDto` into the CDC-owned `PoTool.Core.Domain.Models.WorkItemStateClassification` input before invoking `StateClassificationLookup`.

- **Handler and projection responsibilities cleaned:**  
  `GetSprintMetricsQueryHandler`, `GetSprintExecutionQueryHandler`, and `SprintTrendProjectionService` now stop at orchestration boundaries for state classification lookup construction:
  - fetch API/persistence data
  - map transport classifications into CDC inputs
  - call CDC sprint/history helpers and metrics services
  - continue mapping results into DTOs/projections
  No canonical state-classification input types remain sourced directly from Shared contracts inside `PoTool.Core.Domain`.

- **Project references verified:**  
  `PoTool.Core.Domain/PoTool.Core.Domain.csproj` no longer references `PoTool.Shared`. The CDC now depends only on its own domain models, while `PoTool.Api` remains the adapter layer that references both the CDC and shared transport contracts.

- **Tests passing:**  
  Verified with `dotnet build PoTool.sln --no-restore` and `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~StateClassificationInputMapperTests|FullyQualifiedName~HistoricalSprintLookupTests|FullyQualifiedName~HistoricalSprintInputMapperTests|FullyQualifiedName~GetSprintMetricsQueryHandlerTests|FullyQualifiedName~GetSprintExecutionQueryHandlerTests|FullyQualifiedName~SprintTrendProjectionServiceTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~WorkItemStateClassificationServiceTests" -v minimal`.
