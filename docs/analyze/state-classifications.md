# State Classifications & Refinement Gating Analysis

## 1. Canonical classification model

### Canonical states

The codebase defines one canonical lifecycle model with four classifications:

- `New`
- `InProgress`
- `Done`
- `Removed`

Primary definitions:

- `PoTool.Core.Domain/Models/StateClassificationModels.cs:1-23`
- `PoTool.Shared/Settings/WorkItemStateClassificationDto.cs:1-86`
- `docs/domain/rules/state_rules.md:1-84`

The domain rules explicitly state that PoTool should not rely on raw TFS states directly; every TFS state must map to exactly one canonical lifecycle state, and mappings are per work item type.

### Default mappings

The fallback mappings are defined in code in:

- `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs:8-64`

Important defaults:

- Epic/Feature: `New -> New`, `Active -> InProgress`, `Resolved/Closed -> Done`, `Removed -> Removed`
- Product Backlog Item: `New -> New`, `Approved -> New`, `Committed -> InProgress`, `Done -> Done`, `Removed -> Removed`
- Bug: `New -> New`, `Approved -> New`, `Committed -> InProgress`, `Done -> Done`, `Removed -> Removed`
- Task: `To Do -> New`, `In Progress -> InProgress`, `Done -> Done`, `Removed -> Removed`

This is the key current behavior for the issue: **`Approved` already exists as a raw TFS state, but it is mapped to canonical `New`, not to a separate refinement-ready concept.**

## 2. Where classifications are defined

### Code

Canonical enum and default mappings are defined in code:

- `PoTool.Core.Domain/Models/StateClassificationModels.cs:1-23`
- `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs:8-64`
- `PoTool.Core.Domain/Domain/Sprints/StateClassificationLookup.cs:8-116`

### Settings configuration / persistence

The repository also supports project-scoped configurable mappings:

- Interface: `PoTool.Core/Contracts/IWorkItemStateClassificationService.cs:5-62`
- Service: `PoTool.Api/Services/WorkItemStateClassificationService.cs:17-210`
- Entity: `PoTool.Api/Persistence/Entities/WorkItemStateClassificationEntity.cs:1-43`
- Migration: `PoTool.Api/Migrations/20260119144056_AddWorkItemStateClassifications.cs:9-45`

Current behavior:

1. `WorkItemStateClassificationService.GetClassificationsAsync()` loads project-specific mappings from the database.
2. If none exist, it falls back to the code defaults from `StateClassificationDefaults`.
3. Results are cached in memory per TFS project.

So the effective source of truth is:

- **configured DB settings when present**
- otherwise **code defaults**

### UI / settings surface

The mappings are editable in Settings:

- `PoTool.Client/Pages/Settings/WorkItemStates.razor:1-260`
- `PoTool.Api/Handlers/Settings/GetStateClassificationsQueryHandler.cs:9-39`
- `PoTool.Api/Handlers/Settings/SaveStateClassificationsCommandHandler.cs:8-46`
- `PoTool.Client/Services/StateClassificationService.cs:5-118`

The UI exposes only the same four canonical choices:

- New
- In Progress
- Done
- Removed

There is **no separate configurable classification for Approved or Refinement Ready** in the UI.

## 3. Mapping and lookup mechanics

The main lookup helper is:

- `PoTool.Core.Domain/Domain/Sprints/StateClassificationLookup.cs:8-116`

Relevant behavior:

- builds a case-insensitive `(WorkItemType, StateName) -> StateClassification` lookup
- exposes `GetClassification(...)`
- exposes `IsDone(...)`
- exposes `GetStatesForClassification(...)`
- defaults unknown or blank states to `New`

The API service mirrors similar behavior:

- `PoTool.Api/Services/WorkItemStateClassificationService.cs:160-199`

It also defaults unknown mappings to `StateClassification.New`.

## 4. How classifications are used

### 4.1 Validation and backlog health / integrity checks

#### Legacy hierarchical validation rules

- `PoTool.Core/WorkItems/Validators/HierarchicalValidationRuleBase.cs:59-73`

`IsFinishedState()` classifies a work item state via `IWorkItemStateClassificationService` and treats only `Done` and `Removed` as terminal. This is used by older validation rules to skip finished items.

#### Canonical backlog quality rules

- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs:42-45`
- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs:161-178`
- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs:196-213`
- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs:231-249`

The canonical rules use `WorkItemSnapshot.StateClassification` directly:

- active items are `not Done and not Removed`
- structural integrity checks look for:
  - done parents with unfinished descendants
  - removed parents with unfinished descendants
  - new parents with started descendants

#### Backlog readiness scoring

- `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs:18-32`
- `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs:69-128`
- `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs:130-162`
- `PoTool.Core/Health/BacklogStateComputationService.cs:34-140`
- `PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs:72-112`

The backlog-state flow uses classification in two ways:

- `Removed` items are excluded from scoring and display
- `Done` items are hidden from display but contribute `100` to score averages

This is used to drive the backlog refinement state UI, not workflow transitions.

### 4.2 Filtering, reporting, and projections

#### Delivery and sprint reporting

- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs:116-149`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs:165-183`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:100-114`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:190-197`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Services/DeliveryProgressRollupService.cs:271-276`

These services use `StateClassificationLookup.IsDone(...)` to determine:

- whether PBIs count as delivered
- whether feature/epic progress is 100%
- whether story point estimates count as delivered scope
- whether a state transition event should count as completed work

#### Projection orchestration

- `PoTool.Api/Services/SprintTrendProjectionService.cs:132-183`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs:146-180`

These orchestration services:

- load `System.State` change history from the activity ledger
- build a canonical state lookup
- pass that lookup into domain services for delivery, sprint, and portfolio projections

#### Raw state reconstruction

- `PoTool.Core.Domain/Domain/Sprints/StateReconstructionLookup.cs:5-56`

This helper replays raw `System.State` changes over time to reconstruct the state at a point in time. It works on the raw field history, then other logic can interpret those states canonically.

### 4.3 UI logic

#### Settings UI

- `PoTool.Client/Pages/Settings/WorkItemStates.razor:1-260`

The UI lets users map each raw work item state to one of the four canonical lifecycle classifications.

#### Work item tree visibility

- `PoTool.Client/Services/WorkItemVisibilityService.cs:17-139`

The client hides completed hierarchy nodes only when:

- the item is classified `Done`
- the parent is also `Done`
- all siblings are `Done`
- no validation issues remain

This is a direct UI behavior built on state classification.

### 4.4 Direct `System.State` usage

The repository still uses raw `System.State` directly in a number of places, especially for history/event processing:

- `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs:30-40`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:327-327`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:708-708`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs:281-281`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs:693-693`
- `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:42-118`
- `PoTool.Api/Handlers/WorkItems/GetWorkItemStateTimelineQueryHandler.cs:185-194`
- `PoTool.Api/Services/SprintTrendProjectionService.cs:146-160`
- `PoTool.Api/Services/PortfolioFlowProjectionService.cs:146-152`

This usage is mostly about:

- retrieving raw state values from TFS
- storing history
- detecting that a state change happened at all

That is different from canonical lifecycle interpretation, but it means the codebase currently mixes:

- raw state history handling
- canonical state classification

## 5. Existing concepts related to “Approved” or “Refinement Ready”

### Approved

There is an existing raw TFS state called `Approved` for at least:

- Product Backlog Item
- Bug

Verified in:

- `PoTool.Core.Domain/Domain/Sprints/StateClassificationDefaults.cs:43-55`
- `PoTool.Tests.Unit/RefinementReadinessRulesTests.cs:461-487`

Current semantic treatment:

- `Approved` is considered a **New** state
- it is **not** treated as a terminal state
- it is **not** treated as a special readiness gate

### Refinement ready / ready

There is an existing concept of “ready,” but it is **not a workflow/state-classification concept**.

It appears in backlog readiness ownership/status models:

- `PoTool.Shared/Health/FeatureOwnerState.cs:3-23`
- `PoTool.Core.Domain/BacklogQuality/Models/ReadinessOwnerState.cs:3-11`
- `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs:95-127`

This `Ready` concept means:

- a Feature is fully refined
- or a readiness score reached 100

It is derived from description/child/effort completeness, not from `System.State`.

## 6. Gaps vs desired “Approved = refinement ready” behavior

### Gap 1 — Approved is not distinct from New

Today:

- `Approved -> New`

Therefore the system cannot distinguish:

- “new and not yet approved”
- “approved and refinement-ready”

using canonical state classification alone.

### Gap 2 — No configurable refinement-ready classification exists

The settings model, DTOs, enum, and Settings UI only allow:

- New
- InProgress
- Done
- Removed

There is no fifth state or parallel readiness gate.

### Gap 3 — Existing readiness logic is content-driven, not state-driven

Backlog refinement readiness today is derived from:

- description presence/quality
- child existence
- effort completeness

not from a workflow state such as `Approved`.

That means introducing “Approved = refinement ready” would be a product decision that changes semantics, not just a label rename.

### Gap 4 — Some reporting still hardcodes raw state names

- `PoTool.Api/Handlers/Metrics/BacklogHealthDtoFactory.cs:43-67`

This file counts:

- `"In Progress"`
- `"Active"`
- `"Blocked"`
- `"On Hold"`

using raw string comparisons instead of canonical mappings. That makes part of the reporting surface less consistent with configurable state classification.

## 7. Recommended extension points

### Option A — Add a new canonical classification

Possible extension point:

- extend `StateClassification` in:
  - `PoTool.Core.Domain/Models/StateClassificationModels.cs:6-12`
  - `PoTool.Shared/Settings/WorkItemStateClassificationDto.cs:6-28`

Then update:

- `StateClassificationDefaults`
- `StateClassificationLookup`
- `WorkItemStateClassificationService`
- settings persistence/UI
- consumers that assume only four canonical states

This would make “Approved” first-class, but it would have broader impact because many rules currently assume active work is everything except `Done` and `Removed`.

### Option B — Keep lifecycle classification unchanged and add a parallel refinement gate

This is the safer extension point if the intent is specifically **refinement gating**, not lifecycle tracking.

Likely integration points:

- backlog readiness/domain services:
  - `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs:18-128`
  - `PoTool.Core/Health/BacklogStateComputationService.cs:34-140`
  - `PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs:72-112`
- UI/read models:
  - `PoTool.Shared/Health/FeatureOwnerState.cs:3-23`
  - `PoTool.Shared/Health/BacklogStateDtos.cs:29-92`

Under this approach:

- `Approved` could remain canonically `New`
- a separate readiness or approval flag could express “ready for refinement/planning”
- existing delivery/sprint semantics would stay stable

### Option C — Treat Approved as a derived readiness signal in specific handlers only

The smallest behavioral change would be to keep the canonical model intact and interpret raw `Approved` only in a narrow readiness flow, for example inside:

- `GetProductBacklogStateQueryHandler`
- backlog readiness services
- specific refinement reports

This is lower-impact, but it risks semantic duplication if multiple features start interpreting `Approved` independently.

## 8. Conclusion

### What already exists

- A real canonical state-classification system exists.
- It is defined in code, optionally overridden in project-scoped settings stored in the database, and exposed in a Settings UI.
- Classifications are used in validation, backlog quality analysis, delivery/sprint reporting, and some UI visibility rules.

### What does not exist

- There is **no existing canonical classification equivalent to “Approved” or “Refinement Ready.”**
- There is **no current behavior where `Approved` means refinement-ready.**

### Current semantics

- `Approved` already exists as a raw TFS state.
- It is currently treated as canonical `New`.
- “Ready” exists only as a derived backlog-readiness/owner-state concept, not as a `System.State` classification.

### Recommended direction

If the desired behavior is specifically **“Approved = refinement ready”**, the cleanest extension point is likely a **parallel readiness/approval concept** layered on top of the existing four-state lifecycle model, rather than overloading lifecycle classification that is already used by delivery and sprint analytics.
