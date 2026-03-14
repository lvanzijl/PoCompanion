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

- **Canonical services are not always consumed through DI end to end.**  
  `GetEpicCompletionForecastQueryHandler` has a constructor overload that instantiates `CanonicalStoryPointResolutionService` directly, and `SprintTrendProjectionService` falls back to `new CanonicalStoryPointResolutionService()` when no service is injected. That is semantically correct today, but it weakens the extraction story if the CDC later needs decoration or alternate implementations.

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

6. **Prefer injected CDC services over direct construction.**  
   Once the CDC exists, remove fallback `new CanonicalStoryPointResolutionService()` creation paths so all consumers use the extracted domain service surface consistently.

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

- Grouped the canonical sprint-history helpers into `PoTool.Core/Domain/Sprints` so the domain surface now collects:
  - `SprintCommitmentLookup`
  - `FirstDoneDeliveryLookup`
  - `SprintSpilloverLookup`
  - `StateClassificationLookup`
  - `StateReconstructionLookup`
- Reduced API coupling by moving the fallback state-classification source into `PoTool.Core/Domain/Sprints/StateClassificationDefaults.cs`; the helper group no longer depends on `PoTool.Api.Services.WorkItemStateClassificationService`.
- Updated existing API consumers to use the Core domain helper namespace without changing reconstruction semantics:
  - `GetSprintMetricsQueryHandler`
  - `GetSprintExecutionQueryHandler`
  - `SprintTrendProjectionService`
  - `WorkItemStateClassificationService` now reuses the Core fallback classification source instead of owning a duplicate API-local copy
- Added focused unit coverage in `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` for:
  - canonical state mapping
  - point-in-time state reconstruction
  - existing commitment, first-Done, and spillover reconstruction behavior
