# Sprint Commitment CDC Extraction

_Generated: 2026-03-16_

Reference documents:

- `docs/analysis/sprint-commitment-domain-exploration.md`
- `docs/architecture/sprint-commitment-domain-model.md`
- `docs/architecture/sprint-commitment-cdc-summary.md`

## Files moved or wrapped

The extraction kept the change surgical by wrapping the existing canonical helpers instead of rewriting their semantics.

Wrapped into the CDC sprint slice:

- `PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs`
- `PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs`
- `PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs`

New CDC ownership surface:

- `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCommitmentModels.cs`
- `PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs`

Updated consumer:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
- `PoTool.Api/Services/SprintTrendProjectionService.cs`
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

## Fallbacks removed

`CommittedWorkItemIds is now required` on `SprintDeliveryProjectionRequest`.

Removed behavior:

- `ResolvedSprintId fallback removed from SprintDeliveryProjectionService`
- snapshot membership no longer acts as implicit commitment logic inside delivery projections

Retained behavior:

- callers may still use snapshots as reconstruction anchors
- application services may still compute committed IDs before invoking delivery projections

## CDC interfaces introduced

The extraction introduces the canonical service interfaces requested for the application layer:

- `ISprintCommitmentService`
- `ISprintScopeChangeService`
- `ISprintCompletionService`
- `ISprintSpilloverService`

The CDC also exposes a sprint execution metrics calculator service so the existing formulas are available under the sprint CDC namespace.

## Remaining migration tasks for application layer

This extraction establishes the CDC surface, but some application-layer migration remains:

1. `GetSprintMetricsQueryHandler` still calls the legacy static helpers directly and can be migrated to the CDC interfaces.
2. `GetSprintExecutionQueryHandler` still reconstructs commitment, add/remove, and spillover semantics inline around the legacy helpers and can be progressively moved to the CDC services.
3. Existing exploration and domain-model documents can be refreshed further once the remaining handlers stop referencing the legacy helper namespace directly.
4. Additional handler-level tests can be updated over time to assert CDC interface usage explicitly rather than only semantic outputs.
