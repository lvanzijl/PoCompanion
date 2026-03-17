# UI Story Point Adoption Audit

## Scope

This audit tracks Phase 2 UI adoption work for canonical story-point transport aliases and closely related UI consumer models.

The goal is to move application usage away from legacy `Effort`-named fields when those fields are explicit story-point aliases, without changing effort-hour semantics or sprint execution workflows.

## Updated components and consumers

The following UI surfaces now use canonical story-point fields:

| File | Updated usage |
| --- | --- |
| `PoTool.Client/Components/Forecast/ForecastPanel.razor` | Replaced `TotalEffort`, `CompletedEffort`, and `RemainingEffort` bindings with `TotalStoryPoints`, `DeliveredStoryPoints`, and `RemainingStoryPoints` on `EpicCompletionForecastDto` |
| `PoTool.Client/ApiClient/ApiClient.Extensions.cs` | Added a partial compatibility shim so the generated `EpicCompletionForecastDto` exposes canonical story-point aliases without editing generated code |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | Replaced forecast consumer usage of `RemainingEffort` with `RemainingStoryPoints` for at-risk threshold evaluation |
| `PoTool.Client/Pages/Home/ProductRoadmaps.razor` | Renamed local roadmap epic story-point fields from `TotalEffort` / `DeliveredEffort` / `RemainingEffort` to `TotalStoryPoints` / `DeliveredStoryPoints` / `RemainingStoryPoints` |

## Remaining legacy bindings

The following legacy UI bindings remain intentionally unchanged in this phase:

| File | Legacy field | Reason |
| --- | --- | --- |
| `PoTool.Client/Pages/Home/SprintTrend.razor` | `DoneEffort` | Sprint execution / sprint trend surface is explicitly out of scope for this phase |
| `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor` | `TotalEffort` | Effort diagnostics surface; values are intentional effort-hour metrics |

## Blockers and follow-up

- Sprint execution and planning surfaces still expose legacy `Effort`-named fields alongside canonical story-point aliases, but those pages were excluded by the issue constraints.
- Portfolio flow and other mixed-semantic pages require separate semantic review before any further renaming, even where compatibility aliases already exist.
- No DTO fields were removed in this phase; compatibility aliases remain available for existing consumers.
