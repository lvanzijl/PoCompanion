# DTO Contract Cleanup — Canonical Naming

## Scope

This audit captures the shared DTO contract surfaces that still expose legacy effort-shaped names and records the canonical story-point aliases introduced for backward-compatible cleanup.

The cleanup is intentionally additive:

- existing transport fields remain available for current consumers
- canonical aliases are introduced only where the underlying values already represent story-point semantics
- true effort-hour fields are documented but not relabeled as story points

## Legacy DTO fields

The following shared DTOs expose the legacy field names called out by the cleanup issue.

| DTO | Legacy field | Current semantic meaning | Notes |
| --- | --- | --- | --- |
| `EpicCompletionForecastDto` | `TotalEffort` | total canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `EpicCompletionForecastDto` | `CompletedEffort` | delivered canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `EpicCompletionForecastDto` | `RemainingEffort` | remaining canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `PortfolioSprintProgressDto` | `RemainingEffort` | compatibility alias for `RemainingScopeStoryPoints` | Already canonicalized in the DTO surface |
| `FeatureProgressDto` | `TotalEffort` | total canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `EpicProgressDto` | `TotalEffort` | total canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `FeatureDeliveryDto` | `TotalEffort` | total canonical story-point scope | Compatibility alias; deprecated in future contract revision |
| `PortfolioDeliverySummaryDto` | `TotalCompletedEffort` | effort-hours aggregated from `CompletedPbiEffort` | Not aliased to story points because the underlying value is not story-point based |
| `ProductDeliveryDto` | `CompletedEffort` | effort-hours aggregated from `CompletedPbiEffort` | Not aliased to story points because the underlying value is not story-point based |
| `SprintExecutionSummaryDto` | `CompletedEffort` | effort-hours aggregated from `WorkItem.Effort` | Canonical story-point delivery is exposed separately |
| `ProductSprintMetricsDto` | `PlannedEffort` | effort-hours planned for the sprint-product | Canonical story-point scope already exists as `PlannedStoryPoints` |

Related legacy story-point transport names outside the exact issue search set are also part of the current compatibility surface:

- `FeatureProgressDto.DoneEffort`
- `FeatureProgressDto.SprintCompletedEffort`
- `EpicProgressDto.DoneEffort`
- `EpicProgressDto.SprintCompletedEffort`
- `FeatureDeliveryDto.SprintCompletedEffort`
- `SprintForecast.ExpectedCompletedEffort`
- `SprintForecast.RemainingEffortAfterSprint`

## Canonical aliases introduced

The cleanup adds the following canonical aliases without removing existing fields:

| DTO | Canonical alias | Backing value |
| --- | --- | --- |
| `SprintExecutionSummaryDto` | `CommittedStoryPoints` | `CommittedSP` |
| `SprintExecutionSummaryDto` | `AddedStoryPoints` | `AddedSP` |
| `SprintExecutionSummaryDto` | `DeliveredStoryPoints` | `DeliveredSP` |
| `SprintExecutionSummaryDto` | `RemainingStoryPoints` | `CommittedSP + AddedSP - RemovedSP - DeliveredSP` |
| `SprintExecutionSummaryDto` | `SpilloverStoryPoints` | `SpilloverSP` |
| `EpicCompletionForecastDto` | `DeliveredStoryPoints` | `CompletedEffort` |
| `EpicCompletionForecastDto` | `RemainingStoryPoints` | `RemainingEffort` |
| `FeatureProgressDto` | `DeliveredStoryPoints` | `DoneEffort` |
| `EpicProgressDto` | `DeliveredStoryPoints` | `DoneEffort` |
| `FeatureDeliveryDto` | `DeliveredStoryPoints` | `SprintCompletedEffort` |

Canonical aliases intentionally were **not** added for DTO fields that still carry true effort-hour metrics:

- `PortfolioDeliverySummaryDto.TotalCompletedEffort`
- `ProductDeliveryDto.CompletedEffort`
- `SprintExecutionSummaryDto.CompletedEffort`
- `ProductSprintMetricsDto.PlannedEffort`

Those fields require upstream computation changes, not just transport renaming, before a story-point alias would be correct.

## Compatibility strategy

The compatibility strategy for this cleanup is:

1. Keep legacy fields available for existing API and client consumers.
2. Add canonical story-point aliases only when the backing value already uses story-point semantics.
3. Mark legacy transport names in XML documentation as a **Compatibility alias** and **Deprecated in future contract revision**.
4. Avoid adding story-point aliases on true effort-hour fields, even when nearby UI or transport naming is misleading today.

This approach keeps the contract backward compatible while giving new consumers an explicit canonical surface to target for future migrations.
