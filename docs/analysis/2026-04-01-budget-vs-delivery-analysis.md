# Budget vs Delivery Analysis

Date: 2026-04-01

## Scope

This report analyzes how budget consumption could be derived from the current delivery model. It focuses on epic effort and story-point semantics, epic-to-project mapping, sprint-based evolution, and the limits of using delivery data as a proxy for budget burn.

## 1. Current data model relevant to budget consumption

### 1.1 Raw work-item data

The current work-item contract already carries the core raw inputs needed for a budget-vs-delivery interpretation:

- `Effort`
- `StoryPoints`
- `ProjectNumber`
- `ProjectElement`

Current persisted and exposed fields:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-100`
- `PoTool.Shared/WorkItems/WorkItemDto.cs:15-30`
- `PoTool.Api/Repositories/WorkItemRepository.cs:135-160`
- `PoTool.Api/Repositories/WorkItemRepository.cs:196-213`
- `PoTool.Api/Adapters/DeliveryTrendProjectionInputMapper.cs:8-24`

TFS ingestion currently requests and parses:

- `Microsoft.VSTS.Scheduling.Effort`
- `Microsoft.VSTS.Scheduling.StoryPoints`
- `Rhodium.Funding.ProjectNumber`
- `Rhodium.Funding.ProjectElement`

See:

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:25-59`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`
- `PoTool.Core/RevisionFieldWhitelist.cs:14-35`

### 1.2 Domain semantics

The domain normalizes project/funding fields to **Epic-only semantics**:

- `WorkItemFieldSemantics.IsProjectNumberRelevant` returns true only for epics
- `WorkItemFieldSemantics.IsProjectElementRelevant` returns true only for epics

See:

- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs:8-35`
- `PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs:10-90`

This is important because it means:

- epic → project/work-package mapping is explicit
- features and PBIs do **not** carry independent project-number semantics in the canonical model
- budget grouping is expected to happen at epic/project level, not at lower work-item levels

## 2. Epic effort and story-point storage and aggregation

### 2.1 Effort (hours)

### Raw storage

Effort is stored as an hours-like estimate on work items:

- `WorkItemEntity.Effort` is persisted as `int?` (`PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-72`)
- `WorkItemDto.Effort` is exposed as `int?` (`PoTool.Shared/WorkItems/WorkItemDto.cs:15-17`)
- `CanonicalWorkItem.Effort` is carried as `double?` in the domain model (`PoTool.Core.Domain/Models/CanonicalWorkItem.cs:11-20`, `51`)
- `DeliveryTrendWorkItem.Effort` is carried as `int?` in sprint-delivery inputs (`PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs:12-25`, `56`)

### Feature-level use

Feature progress is effort-aware:

- `FeatureProgress.Effort` stores the feature effort (`PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs:33-35`, `135`)
- `FeatureProgress.Weight` stores the weight used in rollups (`FeatureProgress.cs:33`, `131`)

Feature forecast consumption is computed directly from effort and progress:

- if effort or effective progress is missing, consumed/remaining forecast is `null`
- otherwise:
  - `ForecastConsumedEffort = Effort × EffectiveProgress`
  - `ForecastRemainingEffort = max(0, Effort − ForecastConsumedEffort)`

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs:22-43`

### Epic-level aggregation

Epic progress is weighted by feature effort:

- `EpicProgressService` computes epic progress as the weighted average of feature effective progress using `TotalEffort` as the weight
- features with zero total effort are excluded from the included-feature count

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs:34-62`

Epic forecast effort is then aggregated by summing feature forecast values:

- `EpicForecastConsumed = sum(feature.ForecastConsumedEffort)`
- `EpicForecastRemaining = sum(feature.ForecastRemainingEffort)`

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs:37-63`

The final epic read model carries:

- `ForecastConsumedEffort`
- `ForecastRemainingEffort`
- `SprintEffortDelta`
- `TotalWeight`

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs:22-32`, `101-121`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:241-262`

### 2.2 Story points

### Raw storage

Story points are stored separately from effort:

- `WorkItemEntity.StoryPoints` (`PoTool.Api/Persistence/Entities/WorkItemEntity.cs:73-76`)
- `WorkItemDto.StoryPoints` (`PoTool.Shared/WorkItems/WorkItemDto.cs:25-30`)
- `CanonicalWorkItem.StoryPoints` (`PoTool.Core.Domain/Models/CanonicalWorkItem.cs:15-18`, `43`)
- `DeliveryTrendWorkItem.StoryPoints` (`PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs:19-20`, `58`)

### Aggregation semantics

Story points are used for delivery scope and delivery progress, not for budget weighting:

- feature rollups carry `TotalScopeStoryPoints` and `DeliveredStoryPoints`
- epic rollups sum those feature-level story-point totals
- sprint projections track planned, completed, and spillover story points

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs:18-24`, `101-109`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs:16-24`, `89-105`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:232-254`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs:247-285`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs:43-48`, `79-98`, `121-138`

Story-point resolution is also explicitly canonicalized for PBIs in sprint delivery:

- `SprintTrendProjectionService.ResolvePbiStoryPointEstimate`
- `SprintDeliveryProjectionService`

See:

- `PoTool.Api/Services/SprintTrendProjectionService.cs:407-423`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`

## 3. Can effort be used as a reliable proxy for budget consumption?

### 3.1 Short answer

**Partially, but not fully reliably.**

Effort is the strongest available proxy in the current implementation, but it is still a proxy for budget consumption rather than an explicit budget ledger.

### 3.2 Why effort is the best current proxy

Effort is the only field that currently behaves like an hours-based consumption input across the delivery model:

- it is retrieved from TFS
- it is persisted and exposed end to end
- it is used as the weight for epic progress rollups
- it is converted into consumed/remaining forecast values
- sprint projections expose planned/worked/completed/spillover effort totals

Relevant files:

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:25-59`
- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-76`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs:26-43`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs:51-56`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs:39-57`, `74-98`

If budget is interpreted as **planned engineering effort in hours**, then:

- feature forecast consumed effort is a direct budget-consumption approximation
- epic forecast consumed effort is the rollup of that approximation
- sprint effort deltas can be read as a sprint-by-sprint approximation of budget burn

### 3.3 Why effort is not fully reliable as budget consumption

Effort is still not a complete or authoritative budget measure for several reasons.

### A. The current persisted portfolio snapshot has no explicit budget amount

Current portfolio snapshot rows store:

- `ProjectNumber`
- `WorkPackage`
- `Progress`
- `TotalWeight`
- `LifecycleState`

There is **no stored `Budget`, `BudgetRemaining`, `ForecastConsumed`, or money value** in the current persisted snapshot model.

See:

- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs:23-52`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:53-97`
- `PoTool.Api/Services/PortfolioReadModelMapper.cs:41-79`

### B. The architecture record describes budget semantics that the code does not yet persist

The CDC decision record describes a future-oriented snapshot model where budget is defined at project level and `BudgetRemaining = Budget − ForecastConsumed`, but the live persistence model has not implemented that budget field yet.

See:

- `docs/architecture/cdc-decision-record.md:186-244`

So the repository currently supports:

- project/work-package grouping
- progress
- weight

but not an explicit stored budget baseline.

### C. Effort is not the same thing as money

The code treats effort as a work estimate. It does **not** model:

- hourly cost rates
- team cost differences
- external spend
- non-delivery budget categories
- budget approvals or reallocations

Nothing in the current entities or DTOs converts effort into financial cost.

### D. Missing effort creates null forecast consumption

If effort or effective progress is missing, `FeatureForecastService` returns `null` forecast consumed/remaining values rather than inferring usage.

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs:26-33`

This makes effort-based budget consumption incomplete when estimation coverage is incomplete.

### E. Story points and effort are intentionally separate semantics

The architecture record explicitly warns against mixing story points and effort (`docs/architecture/cdc-decision-record.md:180-182`), so story points cannot safely “fill in” budget consumption where effort is missing.

## 4. Epic → Project → Budget mapping

### 4.1 Epic → Project

The canonical epic-to-project mapping is:

- epic carries `ProjectNumber`
- epic may carry `ProjectElement`
- those values are normalized away for non-epic types

See:

- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs:8-35`
- `PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs:10-90`

In portfolio snapshot capture:

1. feature progress is computed
2. epic progress is computed from feature progress
3. the corresponding epic work items are loaded from `WorkItems`
4. `ProjectNumber` is required
5. `ProjectElement` becomes the optional work-package key

See:

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:84-162`

Important current rule:

- snapshot capture fails if any included epic is missing `ProjectNumber`

See:

- `PortfolioSnapshotCaptureDataService.cs:128-141`

### 4.2 Project → Budget

This mapping is only **implicit / architectural** today, not fully implemented in persistence.

Current code supports:

- project grouping by `ProjectNumber`
- optional work-package grouping by `ProjectElement`
- progress and weight tracking over time

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:53-97`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs:151-160`, `206-218`

Current code does **not** support:

- an explicit project budget amount
- budget remaining
- budget delta as a persisted field

So the true current mapping is:

**Epic → ProjectNumber / ProjectElement → Portfolio snapshot grouping**

not yet:

**Epic → Project → explicit stored budget**

## 5. How budget consumption can evolve per sprint

### 5.1 Available sprint signals

The sprint model already exposes several effort-based signals that can be interpreted as budget-consumption proxies:

- `TotalPlannedEffort`
- `TotalWorkedEffort`
- `TotalCompletedPbiEffort`
- `TotalSpilloverEffort`
- epic/feature `SprintEffortDelta`

See:

- `PoTool.Shared/Metrics/SprintTrendDtos.cs:34-143`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/FeatureProgress.cs:22-25`, `109-117`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/EpicProgress.cs:22-25`, `101-109`

### 5.2 How those sprint signals are built

At feature level:

- `SprintEffortDelta` is the sum of sprint effort deltas for child PBIs

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:141-178`

At epic level:

- `SprintEffortDelta` is the sum of feature `SprintEffortDelta` values

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:241-255`

At sprint projection level:

- planned/worked/completed/spillover effort totals are persisted for each sprint-product combination

See:

- `PoTool.Api/Services/SprintTrendProjectionService.cs:347-405`
- `PoTool.Api/Persistence/Entities/SprintMetricsProjectionEntity.cs`

### 5.3 Interpreting sprint evolution as budget consumption

If budget consumption is approximated from delivery data, the strongest sprint interpretations are:

- **planned budget exposure** ≈ `TotalPlannedEffort`
- **actual sprint work burn** ≈ `TotalWorkedEffort`
- **budget tied to completed scope** ≈ `TotalCompletedPbiEffort`
- **unfinished consumed scope / carried risk** ≈ `TotalSpilloverEffort`
- **epic sprint burn trend** ≈ `EpicProgress.SprintEffortDelta`

This gives a workable per-sprint budget-consumption trend, but it remains an effort-based proxy, not a financial accounting model.

## 6. Does partial completion affect budget usage?

**Yes, if budget usage is derived from forecast-consumed effort.**

Feature forecast math explicitly accounts for partial completion:

- 50% effective progress consumes 50% of effort
- 100% effective progress consumes all effort
- remaining effort is the remainder

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/FeatureForecastService.cs:35-37`

At epic level, partial completion therefore affects:

- `ForecastConsumedEffort`
- `ForecastRemainingEffort`
- aggregated progress

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicAggregationService.cs:48-60`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/EpicProgressService.cs:51-56`

However, there are two different interpretations:

1. **Forecast-consumption interpretation**
   - partial progress should consume part of the budget proxy
   - this matches the feature forecast model

2. **Done-only delivery interpretation**
   - only completed PBIs/scope count as “consumed”
   - this matches sprint delivered/completed metrics more closely

The current code supports both kinds of signals, but it does not choose one canonical budget rule.

## 7. Edge cases

### 7.1 Epics without effort

An epic does not need its own effort field to participate in forecast consumption because epic consumption is aggregated bottom-up from features.

But if the underlying feature effort coverage is weak:

- weighted epic progress becomes less representative
- forecast consumed/remaining effort may be partially null or entirely null

Evidence:

- zero-effort features are excluded from included-feature counts (`EpicProgressService.cs:51-56`)
- missing feature forecasts are skipped from epic forecast sums (`EpicAggregationService.cs:48-60`)
- if neither effort nor effective progress exists, feature forecast is null (`FeatureForecastService.cs:26-33`)

### 7.2 Epics spanning multiple projects

The canonical model does **not** support a single epic belonging to multiple projects:

- `ProjectNumber` is a single scalar
- `ProjectElement` is a single optional scalar
- both are epic-only semantic fields

See:

- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs:10-23`
- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs:15-20`, `47-51`

So in the current model:

- one epic maps to at most one project number
- one epic maps to at most one project element / work package

If business reality allows a single epic to consume budget from multiple projects, the current model cannot represent that directly.

### 7.3 Budget not aligned with delivery scope

Budget/project grouping may diverge from delivery scope in several ways:

- product is the main delivery analytics boundary
- project number is an epic funding/grouping boundary
- sprints are resolved by product/sprint membership

This means the same delivery view can be:

- product-scoped for sprint delivery
- project-scoped for portfolio snapshot grouping

Those scopes overlap, but they are not identical.

See:

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:143-161`
- `PoTool.Shared/Metrics/SprintTrendDtos.cs:29-31`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs:151-160`

As a result, budget may not align neatly with:

- sprint commitments
- delivered PBI boundaries
- product reporting slices

### 7.4 Missing project number

Portfolio snapshot capture fails hard when an included epic has no `ProjectNumber`.

See:

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:128-141`

This means budget/project analysis cannot be produced for those epics without data cleanup.

### 7.5 Historical budget truth is not persisted

Current snapshot rows persist progress and weight, but not explicit budget values. If budget is added later, historical comparisons will require captured budget-at-the-time values, not just current joins.

The current model already preserves immutable snapshot rows, but budget itself is not yet one of those row values.

See:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:53-97`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs:23-52`

## 8. Practical conclusion

Budget consumption can currently be derived from delivery data only as an **effort-based approximation**:

- effort is stored end to end
- forecast consumed/remaining effort is computed from progress
- epic consumption is rolled up from features
- project/work-package grouping is available through epic `ProjectNumber` and `ProjectElement`
- sprint delivery already exposes effort-based trend signals

But the current implementation still lacks a true explicit budget model:

- no persisted budget amount
- no monetary conversion
- no canonical rule choosing between forecast-consumed effort and done-only effort for budget burn
- no support for multi-project epics

So the most accurate statement is:

> The repository can approximate budget consumption from effort-weighted delivery progress and sprint effort metrics, but it does not yet persist or enforce an explicit budget accounting model.
