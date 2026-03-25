# Progress Model & Override Integration Analysis

## 1. Summary

This report traces how progress is currently calculated across the repository and evaluates
where a `ProgressMode` (SP vs. count), a manual override (`TimeCriticality`), and an
effective-progress concept can be integrated.

Current state:

- Progress is always story-point based. No count-based mode exists.
- No manual override field is wired end to end. `Microsoft.VSTS.Common.TimeCriticality` is
  absent from the entire stack (see `docs/analyze/field-contract.md` §5).
- There is no `EffectiveProgress` concept in any DTO, domain model, or service.
- All progress arithmetic is performed in the backend domain layer. The frontend consumes
  pre-computed `ProgressPercent` values from DTOs and renders them directly.

## 2. Current progress calculations

### 2.1 Feature-level progress

**Service**: `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs`
`DeliveryProgressRollupService.ComputeFeatureProgress`

**Formula**:

```
rawPercent = round(featureScope.Completed / featureScope.Total × 100)
progressPercent = featureIsDone ? 100 : min(rawPercent, 90)
```

`featureScope.Total` and `featureScope.Completed` are produced by
`HierarchyRollupService.RollupCanonicalScope` (see §3). The 90 % cap prevents a feature from
reaching 100 % unless its canonical state is resolved to Done.

**Ordering**: results are sorted descending by `ProgressPercent`, then ascending by
`FeatureTitle`.

### 2.2 Epic-level progress

**Service**: `DeliveryProgressRollupService.ComputeEpicProgress`

**Formula**:

```
totalScopeStoryPoints = sum(feature.TotalScopeStoryPoints)
deliveredStoryPoints  = sum(feature.DeliveredStoryPoints)
rawPercent            = round(deliveredStoryPoints / totalScopeStoryPoints × 100)
progressPercent       = epicIsDone ? 100 : min(rawPercent, 90)
```

Epic progress is derived purely from child `FeatureProgress` rollups; no epic-level PBI
scope is read directly.

### 2.3 Sprint progression delta

**Service**: `DeliveryProgressRollupService.ComputeProgressionDelta`

Computes the average per-feature progress contribution for a single sprint:

```
For each feature with sprint activity:
    contribution = (featureScope.Completed / featureScope.Total) × 100
progressionDelta = round(sum(contribution) / featureCount, 2)
```

Used as a sprint-by-sprint signal in `SprintTrendProjectionService` and exposed as
`ProgressionDelta.Percentage` on feature and epic DTOs.

### 2.4 Portfolio completion percentage

**DTO**: `PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs`
`PortfolioSprintProgressDto.CompletionPercent`

A nullable `double?` representing the percentage of the portfolio stock completed as of the
end of each sprint. Null when stock is zero or unavailable. Computed outside the domain
service; not produced by `DeliveryProgressRollupService`.

### 2.5 Sprint time-elapsed percentage

**Handler**: `PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs`
`CalculateSprintProgressPercentage`

A purely time-based helper — unrelated to work item delivery:

```
elapsed   = now − sprintStart
duration  = sprintEnd − sprintStart
percentage = clamp(elapsed / duration × 100, 0, 100)
```

## 3. StoryPoints usage across the stack

### 3.1 Field retrieval

`Microsoft.VSTS.Scheduling.StoryPoints` is listed in
`PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` as a required retrieval field and in
`PoTool.Core/RevisionFieldWhitelist.cs` so that historical changes are preserved in the
activity ledger.

### 3.2 Persistence and transport

- Persisted as `StoryPoints` on `PoTool.Api/Persistence/Entities/WorkItemEntity.cs`.
- Exposed as `StoryPoints` on `PoTool.Shared/WorkItems/WorkItemDto.cs` and
  `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs`.
- Mapped in both directions by `PoTool.Api/Repositories/WorkItemRepository.cs`.
- Carried into domain services via `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`.

### 3.3 Canonical resolution chain

`CanonicalStoryPointResolutionService` (
`PoTool.Core.Domain/Domain/Estimation/CanonicalStoryPointResolutionService.cs`) resolves
an estimate for a PBI in the following precedence:

1. `StoryPoints` (source: `Real`) — only for authoritative PBI types.
2. `BusinessValue` (source: `Fallback`) — when `StoryPoints` is absent or zero on a
   non-done PBI.
3. Sibling-derived average (source: `Derived`) — when both direct fields are absent and
   siblings have estimates.
4. `Missing` — when no estimate can be determined.

For non-PBI parents (Feature, Epic), `ResolveParentFallback` applies the same `StoryPoints →
BusinessValue` chain without the PBI-type restriction.

### 3.4 Scope rollup

`HierarchyRollupService.RollupCanonicalScope` (
`PoTool.Core.Domain/Domain/Hierarchy/HierarchyRollupService.cs`) traverses the PBI children
of a Feature and accumulates:

```
total     = sum(resolvedEstimate for each child PBI)
completed = sum(resolvedEstimate for each Done child PBI)
```

Bugs and Tasks are excluded from story-point scope because only
`CanonicalWorkItemTypes.IsAuthoritativePbi` types qualify. When no PBI estimates are
available, `ResolveParentFallback` supplies a Feature-level estimate from the Feature's own
`StoryPoints` or `BusinessValue`.

## 4. Progress fields and percentage rendering

### 4.1 Domain model

| Type | Field | Semantics |
|---|---|---|
| `FeatureProgress` | `ProgressPercent` | Capped SP percentage (0–90 open, 100 done) |
| `FeatureProgress` | `TotalScopeStoryPoints` | Total canonical scope |
| `FeatureProgress` | `DeliveredStoryPoints` | Completed canonical scope |
| `EpicProgress` | `ProgressPercent` | Same cap rule applied to epic-level aggregation |
| `EpicProgress` | `TotalScopeStoryPoints` | Sum of feature totals |
| `EpicProgress` | `DeliveredStoryPoints` | Sum of feature completed |

### 4.2 Transport DTOs

`PoTool.Shared/Metrics/SprintTrendDtos.cs` carries `FeatureProgressDto` and `EpicProgressDto`
with the same `ProgressPercent`, `TotalStoryPoints`, and `DoneStoryPoints` fields.

`PoTool.Shared/Metrics/PortfolioProgressTrendDtos.cs` carries
`PortfolioSprintProgressDto.CompletionPercent` for portfolio-level views.

### 4.3 Frontend rendering

The Blazor client reads pre-computed values directly:

- `PoTool.Client/Pages/Home/ProductRoadmaps.razor` renders `epic.ProgressPercent` as a
  `MudProgressLinear` and displays `epic.DeliveredStoryPoints / epic.TotalStoryPoints`.
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` renders
  `PortfolioSprintProgressDto.CompletionPercent`.
- `PoTool.Client/Pages/Home/SprintTrend.razor` renders per-product SP distributions using
  bar chart helpers.

No progress arithmetic is performed in the frontend. The client is a pure consumer of
server-computed values.

## 5. Manual override — current state

### 5.1 TimeCriticality

`Microsoft.VSTS.Common.TimeCriticality` has no presence in the repository:

- Not in `RequiredWorkItemFields` in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`.
- Not in `PoTool.Core/RevisionFieldWhitelist.cs`.
- Not persisted in `WorkItemEntity`, not surfaced in `WorkItemDto` or
  `WorkItemWithValidationDto`, not mapped by `WorkItemRepository`.
- Not ingested by `ActivityEventIngestionService`.
- Not used in any domain service, handler, UI component, or test.

### 5.2 Manual override fields

No field, entity property, DTO property, or service method represents a manually supplied
progress value or override anywhere in the codebase.

### 5.3 ProgressMode

No concept named `ProgressMode`, count-based mode, or SP/count toggle exists anywhere in
domain models, services, DTOs, or configuration.

## 6. Gaps vs desired model

| Desired capability | Current state | Gap |
|---|---|---|
| `ProgressMode` (SP vs. count) | SP-only; no mode switch | No mode discriminator; no count-based rollup path |
| Manual override (via `TimeCriticality`) | Field absent end to end | Full stack plumbing required (see field-contract.md §7) |
| `EffectiveProgress` (calculated or overridden) | Not modeled | No concept exists in any layer |
| Count-based `ProgressPercent` | Not implemented | `HierarchyRollupService` and `DeliveryProgressRollupService` only support SP scope |
| Override injection point | Absent | No hook in `ComputeFeatureProgress` or `ComputeEpicProgress` |

### Gap details

**Gap 1 — ProgressMode**

`DeliveryFeatureProgressRequest` and `DeliveryEpicProgressRequest` carry no mode selector.
`HierarchyRollupService` always accumulates SP values. To support count-based progress, a
mode parameter would need to propagate from the request through the rollup service to the
scope accumulator.

**Gap 2 — TimeCriticality as override source**

Before `TimeCriticality` can serve as an override, it must be wired end to end (field
retrieval → entity → DTO → domain model). This is a prerequisite that is currently
unsatisfied. The field-contract analysis (§7 "For `Microsoft.VSTS.Common.TimeCriticality`")
describes the required plumbing steps.

**Gap 3 — EffectiveProgress**

`FeatureProgress`, `EpicProgress`, `FeatureProgressDto`, and `EpicProgressDto` expose only
one `ProgressPercent` value. There is no separation between a calculated percentage and a
final effective percentage that may incorporate an override. Adding this concept requires a
new field on the domain model, the DTOs, and corresponding mapping logic.

**Gap 4 — Override injection point**

`DeliveryProgressRollupService.ComputeFeatureProgress` computes `progressPercent` in a
single expression and passes it directly to the `FeatureProgress` constructor. There is no
step at which an externally supplied value could replace or blend with the calculated
result.

## 7. Integration strategy

All integration points belong to the backend. Frontend changes are limited to reading a new
`EffectiveProgressPercent` DTO field instead of `ProgressPercent` where overrides must be
displayed.

### Step 1 — Wire TimeCriticality (prerequisite)

Follow the steps in `docs/analyze/field-contract.md` §7 to add `TimeCriticality` to:

1. `RequiredWorkItemFields` in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`.
2. `RevisionFieldWhitelist` if historical change tracking is needed.
3. `WorkItemEntity`, `WorkItemDto`, `WorkItemWithValidationDto`, and `WorkItemRepository`.
4. `CanonicalWorkItem` in `PoTool.Core.Domain/Models/CanonicalWorkItem.cs`.

Until this step is complete, `TimeCriticality` cannot serve as a data source for any
override.

### Step 2 — Add ProgressMode to request models

Extend `DeliveryFeatureProgressRequest` with a `ProgressMode` discriminator (e.g.,
`StoryPoints` or `Count`). Pass the mode through `DeliveryProgressRollupService` to
`HierarchyRollupService`. Add a count-based accumulation path in `RollupPbiChildren` that
counts done PBIs instead of summing SP estimates.

The mode should default to `StoryPoints` so existing behaviour is preserved.

### Step 3 — Add EffectiveProgressPercent to domain model and DTOs

Add `EffectiveProgressPercent` to:

- `FeatureProgress` and `EpicProgress` in `PoTool.Core.Domain/Domain/DeliveryTrends/Models/`.
- `FeatureProgressDto` and `EpicProgressDto` in `PoTool.Shared/Metrics/SprintTrendDtos.cs`.

Initially `EffectiveProgressPercent` is identical to `ProgressPercent`. The separation
allows the override step to supply a different value without breaking existing consumers that
still read `ProgressPercent`.

### Step 4 — Inject calculated progress

After the rollup, `DeliveryProgressRollupService.ComputeFeatureProgress` should populate
`EffectiveProgressPercent` from the calculated `progressPercent`. At this stage the two
values are equal; the field is ready for override injection in the next step.

### Step 5 — Inject override from TimeCriticality

After `TimeCriticality` is available in `CanonicalWorkItem` (Step 1):

1. Add an optional `IReadOnlyDictionary<int, double?> OverrideProgressByWorkItemId` to
   `DeliveryFeatureProgressRequest`.
2. In `ComputeFeatureProgress`, after computing `progressPercent`, check whether an override
   is present for `feature.WorkItemId`.
3. If an override exists, set `EffectiveProgressPercent = overrideValue`; otherwise keep
   `EffectiveProgressPercent = progressPercent`.
4. Apply the same pattern in `ComputeEpicProgress` using the override dictionary keyed by
   epic ID, or by aggregating feature-level effective values.

### Step 6 — Frontend adoption

Replace `ProgressPercent` references with `EffectiveProgressPercent` in components that
must reflect the override:

- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`

Components that display raw calculated progress (e.g., diagnostics or audit views) may
continue to reference `ProgressPercent`.
