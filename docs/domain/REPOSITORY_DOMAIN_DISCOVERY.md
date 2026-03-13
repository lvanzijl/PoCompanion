# Repository Domain Discovery

## 1 Purpose

This document records the implicit domain model currently implemented in the repository code.

It is repository archaeology only: the findings below describe what the code does today, not what the domain should become later.

The analysis focuses on how the code currently interprets:

- work items
- hierarchy
- effort
- sprint attribution
- activity
- delivery
- planning
- validation

## 2 Domain Logic Locations

| File | Class | Domain Concern | Description |
|-----|------|------|------|
| `PoTool.Shared/WorkItems/WorkItemDto.cs` | `WorkItemDto` | Canonical work item shape | Shared work item model with `Type`, `ParentTfsId`, `AreaPath`, `IterationPath`, `State`, `Effort`, `Description`, `CreatedDate`, `ClosedDate`, `Severity`, `Tags`, `ChangedDate`, and `BacklogPriority`. |
| `PoTool.Core/WorkItems/WorkItemType.cs` | `WorkItemType` | Hierarchy vocabulary | Defines the repository-wide work item hierarchy constants: `goal`, `Objective`, `Epic`, `Feature`, `Product Backlog Item`, `Bug`, `Task`. |
| `PoTool.Core/WorkItems/WorkItemHierarchyHelper.cs` | `WorkItemHierarchyHelper` | Hierarchy traversal | Traverses descendants by `ParentTfsId`; used to treat work items as a rooted tree in memory. |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs` | `RealTfsClient` | TFS field mapping | Declares the TFS field set actually loaded for work items, including `Microsoft.VSTS.Scheduling.Effort`, fallback `StoryPoints`, `System.IterationPath`, `ClosedDate`, `Severity`, `Tags`, and `BacklogPriority`. |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` | `RealTfsClient` | Hierarchy retrieval + effort parsing | Builds full hierarchies from root IDs via recursive `Hierarchy-Forward` links, completes ancestors, and parses effort by preferring `Effort` and falling back to `StoryPoints`. |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemUpdates.cs` | `RealTfsClient` | Activity source | Reads per-work-item update history from TFS and exposes field-level changes used by activity ingestion. |
| `PoTool.Api/Persistence/Entities/WorkItemEntity.cs` | `WorkItemEntity` | Cached domain state | Persists the current work item snapshot, including hierarchy, effort, state, timestamps, severity, tags, relations, and backlog priority. |
| `PoTool.Api/Persistence/Entities/SprintEntity.cs` | `SprintEntity` | Sprint catalog | Persists team iteration metadata: `Path`, `Name`, `StartUtc`, `EndUtc`, `TimeFrame`, and sync timestamps. |
| `PoTool.Api/Services/Sync/TeamSprintSyncStage.cs` | `TeamSprintSyncStage` | Sprint synchronization | Fetches team iterations from TFS and upserts the sprint catalog per team. |
| `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` | `ResolvedWorkItemEntity` | Cached hierarchy resolution | Stores resolved product, epic, feature, and sprint IDs for each work item; sprint resolution is explicitly based on the current iteration path. |
| `PoTool.Api/Services/WorkItemResolutionService.cs` | `WorkItemResolutionService` | Product/feature/epic/sprint attribution | Walks backlog roots, resolves epic/feature ancestry by parent chain, and resolves sprint membership by matching `IterationPath` to `SprintEntity.Path`. |
| `PoTool.Api/Services/WorkItemStateClassificationService.cs` | `WorkItemStateClassificationService` | State semantics | Supplies configurable and default state classifications (`New`, `InProgress`, `Done`, `Removed`) per work item type; many handlers use this as the source of truth for terminal state logic. |
| `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs` | `HierarchicalWorkItemValidator` | Validation framework | Evaluates validation rules in phases: structural integrity, refinement readiness, refinement completeness, and missing effort. |
| `PoTool.Core/WorkItems/Validators/Rules/*.cs` | Validation rule classes | Backlog health rules | Implements concrete rule checks such as missing epic/feature descriptions, missing children, missing effort, and invalid parent/child state combinations. |
| `PoTool.Core/Health/BacklogStateComputationService.cs` | `BacklogStateComputationService` | Planning/readiness scoring | Computes PBI, Feature, and Epic refinement scores and feature ownership states from the loaded work item graph. |
| `PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs` | `GetProductBacklogStateQueryHandler` | Backlog state projection | Builds product backlog state DTOs from hierarchy plus state classifications; removed items are excluded, done items count in scoring but are hidden from display. |
| `PoTool.Api/Persistence/Entities/ActivityEventLedgerEntryEntity.cs` | `ActivityEventLedgerEntryEntity` | Activity ledger | Stores field-level activity events with timestamp, iteration path, and resolved hierarchy context. |
| `PoTool.Api/Services/ActivityEventIngestionService.cs` | `ActivityEventIngestionService` | Activity semantics | Converts TFS update history into ledger entries, filtering to whitelisted fields and resolving parent/feature/epic context at ingest time. |
| `PoTool.Core/RevisionFieldWhitelist.cs` | `RevisionFieldWhitelist` | Activity/revision field scope | Defines the canonical set of fields considered relevant for revision ingestion and activity comparison. |
| `PoTool.Api/Handlers/Metrics/GetWorkItemActivityDetailsQueryHandler.cs` | `GetWorkItemActivityDetailsQueryHandler` | Activity reporting | Returns activity for a selected work item plus descendants, excluding `ChangedBy` and `ChangedDate` from the reported activity feed. |
| `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | `GetSprintMetricsQueryHandler` | Sprint delivery snapshot | Computes sprint totals by filtering current work items whose current `IterationPath` equals the requested sprint path. |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | Sprint trend, delivery, activity rollup | Combines resolved hierarchy, current work item snapshots, and activity events to project planned work, worked work, completed PBIs, bug flow, feature progress, and epic progress per sprint. |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | Sprint trend aggregation | Returns sprint trend rows from cached or recomputed projections and enriches them with feature and epic progress. |
| `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | Sprint execution model | Reconstructs initial scope, additions, removals, completion order, and starved work from current work items plus iteration-path activity events. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs` | `GetPortfolioDeliveryQueryHandler` | Portfolio delivery | Aggregates sprint projections across a sprint range and ranks top feature contributors by completed effort. |
| `PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs` | `GetEpicCompletionForecastQueryHandler` | Forecast/planning | Forecasts epic completion from descendant effort and historical sprint metrics derived from child iteration paths. |
| `PoTool.Api/Handlers/WorkItems/GetDependencyGraphQueryHandler.cs` | `GetDependencyGraphQueryHandler` | Dependency planning | Interprets work item relations as dependencies, critical chains, blocked items, and circular dependencies. |
| `PoTool.Api/Handlers/ReleasePlanning/GetObjectiveEpicsQueryHandler.cs` | `GetObjectiveEpicsQueryHandler` | Objective → Epic planning | Treats Objective as the parent of Epics for release-planning views and combines that with persisted board placements. |
| `PoTool.Api/Handlers/ReleasePlanning/GetEpicFeaturesQueryHandler.cs` | `GetEpicFeaturesQueryHandler` | Epic → Feature planning | Treats Feature as the child of Epic when splitting or inspecting epics in planning flows. |
| `PoTool.Api/Handlers/ReleasePlanning/GetUnplannedEpicsQueryHandler.cs` | `GetUnplannedEpicsQueryHandler` | Release planning candidate selection | Returns Epics not yet placed on the board, excluding done and removed epics via state classifications. |
| `PoTool.Client/Services/PlanBoardWorkItemRules.cs` | `PlanBoardWorkItemRules` | Sprint planning UI model | Builds an `Epic → Feature → PBI/Bug` candidate tree for the plan board, with leaf eligibility based on state and sprint assignment. |
| `PoTool.Client/Services/RoadmapWorkItemRules.cs` | `RoadmapWorkItemRules` | Roadmap discovery | Treats `Objective` and `Epic` as roadmap-relevant types and requires a `roadmap` tag for discovery. |
| `PoTool.Client/Services/RoadmapAnalyticsService.cs` | `RoadmapAnalyticsService` | Roadmap analytics | Computes epic analytics locally from descendants and reuses existing API endpoints for forecast, backlog health, and dependencies. |
| `PoTool.Client/Services/BugTreeBuilderService.cs` | `BugTreeBuilderService` | Bug triage structure | Builds bug triage trees with synthetic groups for `New / Untriaged`, dynamic severity groups, and `Missing/Invalid Severity`. |
| `PoTool.Client/Services/BugInsightsCalculator.cs` | `BugInsightsCalculator` | Bug delivery/triage metrics | Calculates open bugs, created/resolved bugs, resolution time, and severity distribution from cached bug snapshots. |
| `PoTool.Client/Services/TriageTagService.cs` | `TriageTagService` | Bug triage tags | Manages the tag catalog used to mark triaged bugs and filter them in the bug triage UI. |

## 3 Work Item Hierarchy Model

### Repository-wide hierarchy levels

The shared/core hierarchy vocabulary is:

1. `goal` (the implemented `WorkItemType.Goal` constant is lowercase)
2. `Objective`
3. `Epic`
4. `Feature`
5. `Product Backlog Item` and `Bug`
6. `Task`

This is implemented in `PoTool.Core/WorkItems/WorkItemType.cs`.

### Are Goals and Objectives implemented?

Yes, both are implemented in code, but unevenly:

- `Goal` and `Objective` are first-class work item types in shared/core code.
- `GetAllGoalsQueryHandler`, `GetGoalsFromTfsQueryHandler`, and `GetGoalHierarchyQueryHandler` explicitly load or query goals.
- Release planning also uses `Objective` directly: `GetObjectiveEpicsQueryHandler` treats Objective as the immediate parent of Epic.

However, several downstream models ignore those upper levels:

- `WorkItemResolutionService` only resolves upward to `Feature` and `Epic`.
- `ResolvedWorkItemEntity` stores product, feature, epic, and sprint IDs, but no goal or objective IDs.
- `PlanBoardWorkItemRules` only builds `Epic → Feature → PBI/Bug`.

### How parent-child relationships are retrieved

The repository uses two main mechanisms:

1. **Current work item snapshots**
   - The shared DTO and cached entity both carry `ParentTfsId`.
   - Core helpers and handlers traverse trees by repeatedly matching `ParentTfsId`.

2. **TFS hierarchy link discovery**
   - `RealTfsClient.WorkItemsHierarchy.cs` queries `System.LinkTypes.Hierarchy-Forward` recursively from configured root IDs.
   - It then completes missing ancestors so the resulting graph is connected.
   - The discovered parent links are converted into `ParentTfsId` values on `WorkItemDto`.

### Are hierarchy rollups implemented?

Yes, but only in selected areas:

- `WorkItemResolutionService` rolls ancestry upward to resolved epic and feature IDs.
- `PlanBoardWorkItemRules` rolls child effort upward from PBIs/Bugs into Feature and Epic display totals.
- `SprintTrendProjectionService` rolls child activity up to Feature and Epic progress.
- `BacklogStateComputationService` rolls PBI readiness into Feature scores and Feature scores into Epic scores.

There is no general-purpose repository-wide rollup that includes Goal/Objectives in the same resolved model.

## 4 Effort Model

### Effective field model

The repository has one main effort concept on the work item snapshot: `Effort`.

Implementation details:

- `WorkItemDto` exposes nullable `Effort`.
- `WorkItemEntity` persists nullable `Effort`.
- The TFS client reads `Microsoft.VSTS.Scheduling.Effort` first and falls back to `Microsoft.VSTS.Scheduling.StoryPoints`.

So the effective model is:

- repository consumers use one normalized integer effort field
- the TFS integration may source it from either `Effort` or `StoryPoints`

### Which work item types carry effort

Effort is used differently in different places:

- validation rule `PbiEffortEmptyRule` enforces effort on non-finished `Epic`, `Feature`, and `Product Backlog Item`
- plan board leaves display own effort on `PBI` and `Bug`
- sprint metrics sum effort for any sprint work item that has effort
- roadmap analytics only sums effort on descendant `PBI` and `Bug`
- feature/epic delivery projections compute totals from child `PBI` effort

No code path was found that uses `RemainingWork` or `OriginalEstimate` as a first-class field.

### Parent effort vs child rollup

The repository does **not** use one consistent parent-effort model.

Observed patterns:

- `GetEpicCompletionForecastQueryHandler` sums the `Effort` present on all descendants directly, including whatever descendant types have effort.
- `PlanBoardWorkItemRules` ignores parent effort fields and rolls up only eligible descendant `PBI/Bug` effort.
- `RoadmapAnalyticsService` defines remaining effort as the sum of **active descendant `PBI/Bug` effort** and delivered effort as the sum of **terminal descendant `PBI/Bug` effort**.
- `SprintTrendProjectionService` computes feature and epic totals from child PBIs and approximates missing PBI effort by sibling average in some progress calculations.

### Effective repository conclusion

The codebase does not treat effort as a single globally consistent concept.

What exists today is:

- a normalized `Effort` field on work item snapshots
- TFS ingestion fallback from `StoryPoints`
- multiple downstream interpretations:
  - current planned effort
  - delivered effort
  - remaining effort
  - average-based substitute effort for missing PBIs in feature progress

## 5 Sprint Model

### Sprint catalog

Sprint metadata is stored in `SprintEntity` and synced from team iterations by `TeamSprintSyncStage`.

Important fields:

- `Path` is the stable sprint key used by several handlers
- `StartUtc` / `EndUtc` provide sprint boundaries
- `TimeFrame` stores TFS `past` / `current` / `future`

### How sprint membership is determined

There are multiple active strategies.

#### Strategy A — current iteration-path membership

Used by:

- `WorkItemResolutionService`
- `ResolvedWorkItemEntity`
- `GetSprintMetricsQueryHandler`
- `GetSprintExecutionQueryHandler` current sprint item loading
- `PlanBoardWorkItemRules`

Rule:

- a work item belongs to a sprint when its **current** `IterationPath` equals `SprintEntity.Path`

#### Strategy B — sprint activity window

Used by:

- `ActivityEventIngestionService`
- `GetWorkItemActivityDetailsQueryHandler`
- `SprintTrendProjectionService`
- `GetSprintExecutionQueryHandler` for additions/removals

Rule:

- a work item is considered active in a sprint when activity events fall between sprint `StartUtc` and `EndUtc`

#### Strategy C — resolved sprint cache plus activity enrichment

Used by:

- `SprintTrendProjectionService`

Rule:

- planned work is tied to `ResolvedSprintId`
- worked/completed/progress signals come from activity events that occurred inside the sprint date range

### How sprint activity is determined

Sprint activity is revision-driven:

- activity events come from `GetWorkItemUpdatesAsync`
- only field changes from whitelisted fields are ingested
- trend and activity views ignore `System.ChangedBy` and `System.ChangedDate`

For sprint execution:

- addition to sprint = `System.IterationPath` changed **to** sprint path during sprint window
- removal from sprint = `System.IterationPath` changed **from** sprint path during sprint window

For sprint trend projections:

- worked item = any non-excluded activity in the sprint window
- parent items can become “worked” when child items had activity

### How sprint completion is determined

Different handlers use different completion tests:

- `GetSprintMetricsQueryHandler`: current state is classified as done
- `SprintTrendProjectionService`: PBIs completed in sprint only when a `System.State` event changed to `Done` during the sprint window
- `GetSprintExecutionQueryHandler`: completed set is current sprint items whose **current** state is done, then ordered by `ClosedDate`
- `GetPortfolioDeliveryQueryHandler`: uses previously projected `CompletedPbiEffort` from sprint trend projections

### Multiple strategies exist

Yes. Sprint attribution is one of the clearest areas where the repository currently uses more than one model:

- current membership by iteration path
- historical activity by sprint date window
- resolved sprint cache for planned work

## 6 Activity Model

### What counts as activity

Implemented activity is **field-change activity** from TFS work item updates.

The pipeline is:

1. `RealTfsClient.GetWorkItemUpdatesAsync` loads update payloads
2. `ActivityEventIngestionService` keeps only whitelisted field changes
3. each event is stored in `ActivityEventLedgerEntryEntity`

The whitelist comes from `RevisionFieldWhitelist` and includes fields such as:

- `System.State`
- `System.IterationPath`
- `System.AreaPath`
- `System.CreatedDate`
- `System.ChangedDate`
- `System.ChangedBy`
- `Microsoft.VSTS.Common.ClosedDate`
- `Microsoft.VSTS.Scheduling.Effort`
- `BusinessValue`
- `Tags`
- `Severity`

### Whether comments count

No explicit comment ingestion model was found.

The activity pipeline iterates `update.FieldChanges`; it does not separately persist a comment concept, discussion concept, or review-note concept for work items.

### Whether link changes count

No explicit link change ingestion model was found for work item activity.

Dependency and hierarchy links are loaded from the current `Relations` snapshot for dependency analysis, but the activity ledger does not store a dedicated relation-change event type.

### Whether automated updates count

Yes, if they appear as normal TFS field changes on the work item update stream.

There is no filter distinguishing manual from automated field updates.

### Reported activity vs stored activity

Stored activity and reported activity are not identical:

- the ledger may contain `ChangedBy` and `ChangedDate`
- `GetWorkItemActivityDetailsQueryHandler` excludes those fields from the returned activity feed
- `SprintTrendProjectionService` also excludes those two fields when interpreting functional activity

## 7 Validation System

### Validation framework

The main validation system is the hierarchical validator in `PoTool.Core/WorkItems/Validators`.

Evaluation phases:

1. `StructuralIntegrity`
2. `RefinementReadiness`
3. `RefinementCompleteness`
4. `MissingEffort`

Suppression rule:

- refinement completeness issues are suppressed when refinement readiness blockers exist
- missing effort is always evaluated

### Concrete implemented rules

Implemented rules include:

- `DoneParentWithUnfinishedDescendantsRule`
- `RemovedParentWithUnfinishedDescendantsRule`
- `NewParentWithInProgressDescendantsRule`
- `EpicDescriptionEmptyRule`
- `FeatureDescriptionEmptyRule`
- `EpicWithoutFeaturesRule`
- `FeatureWithoutChildrenRule`
- `PbiDescriptionEmptyRule`
- `PbiEffortEmptyRule`

The shared `10`-character minimum (`ValidationRuleConstants.MinimumDescriptionLength`) is applied by the Epic and Feature description readiness rules. The PBI description rule is looser: it checks only for an empty/whitespace description.

### Severity / consequence model

The validation system does not expose a separate “severity” enum for these rules. Instead it classifies outcomes by consequence:

- `BacklogHealthProblem`
- `RefinementBlocker`
- `IncompleteRefinement`

Responsibility is also modeled:

- `ProductOwner`
- `DevelopmentTeam`
- `Process`

### Structural problems checked

The currently implemented structural checks include:

- done or removed parents with unfinished descendants
- new parents with in-progress descendants
- epics without features
- features without child PBIs
- missing descriptions
- missing effort

### Backlog health scoring separate from validation

There is a second, simpler backlog-health model:

- `BacklogHealthCalculator` computes a numeric health score from counts of effort gaps, parent progress issues, and blocked items
- `GetBacklogHealthQueryHandler` combines iteration filtering with hierarchical validation counts and heuristic blocked/in-progress counts

This is distinct from `BacklogStateComputationService`, which calculates refinement readiness scores for Epic/Feature/PBI structures.

## 8 Planning Model

### Plan Board

`PlanBoardWorkItemRules` defines a very specific planning model:

- visible planning hierarchy is `Epic → Feature → PBI/Bug`
- only `PBI` and `Bug` are draggable planning leaves
- leaves are excluded if they are done/removed
- leaves are excluded if already assigned to one of the visible sprint columns
- ordering uses `BacklogPriority` ascending, then `TfsId`
- feature/epic displayed effort is the sum of eligible descendant leaf effort

This is a sprint-assignment planner, not a general backlog planner.

### Release Planning board

Release-planning handlers use hierarchy, but differently:

- Objective is parent of Epic
- Epic is parent of Feature
- persisted board placements are stored separately from the work item hierarchy
- “unplanned epics” are epics not already placed and not in done/removed states
- cached validation indicators are attached to planning candidates

### Roadmaps

Roadmap discovery is tag-based plus type-based:

- `RoadmapWorkItemRules` recognizes `Objective` and `Epic`
- `HasRoadmapTag` requires the `roadmap` tag

Roadmap analytics reuse other domain models:

- descendant traversal for local epic metrics
- dependency graph for dependency signals
- epic forecast endpoint for forward projection
- product backlog state endpoint for refinement signals

### Dependency planning

`GetDependencyGraphQueryHandler` treats `Relations` as explicit planning dependencies:

- `Dependency-Forward` becomes `DependsOn`
- `Dependency-Reverse` becomes `Blocks`
- risk is based on dependency-chain length and total effort
- circular dependency detection is explicit

## 9 Delivery Model

### Sprint delivery snapshot

`GetSprintMetricsQueryHandler` defines a simple snapshot model:

- filter current work items by current `IterationPath`
- done classification determines completion
- completed and planned effort are sums of current `Effort`
- counts are broken out for PBIs, bugs, and tasks

### Sprint execution

`GetSprintExecutionQueryHandler` defines a historical execution model:

- initial scope = current sprint items not added during sprint, plus removed items not added during sprint
- added work = iteration changed to sprint during sprint window
- removed work = iteration changed away from sprint during sprint window
- completed items = current sprint items currently in a done state
- starved work = unfinished initial-scope items when later-added items completed

### Sprint trend / delivery projections

`SprintTrendProjectionService` defines the richest delivery model:

- planned PBIs = items whose `ResolvedSprintId` equals the sprint
- worked items = items with functional activity during the sprint window
- child activity can make parent PBI/Feature/Epic count as worked
- completed PBIs in sprint = state transition to `Done` during sprint window
- bug created = `CreatedDate` within sprint window
- bug closed = bug state change to `Done` or `Closed` during sprint window
- bug worked = any child task had a state change during sprint
- missing-effort PBIs are counted, and sibling averages can mark the result as approximate

### Feature and epic delivery

Feature and epic progress use descendant PBIs:

- total effort = sum of child PBI effort, with sibling-average substitution for missing estimates in progress calculations
- done effort = effort on PBIs whose current state is `Done`
- progress is capped at `90%` unless the Feature/Epic itself is done
- sprint completed effort = child PBIs that transitioned to `Done` during the sprint
- sprint effort delta = summed effort-field changes during the sprint

### Portfolio delivery

`GetPortfolioDeliveryQueryHandler` aggregates sprint projections across a sprint range:

- per-product totals sum completed PBIs, completed effort, bug counts, and progression deltas
- portfolio summary totals aggregate those product rows
- top features are ranked by sprint completed effort
- feature effort share is the feature’s completed effort divided by total completed effort in the range

## 10 Domain Inconsistencies

### 1. Hierarchy breadth is inconsistent across features

The repository-wide type model includes `Goal` and `Objective`, but many important features stop at `Epic` and `Feature`:

- resolution cache stores no goal/objective IDs
- sprint planning uses only `Epic → Feature → PBI/Bug`
- roadmap discovery uses `Objective` and `Epic`
- release planning uses `Objective → Epic`, but delivery models usually start at `Epic` or `Feature`

There is also a smaller naming inconsistency in the base type model itself: `WorkItemType.Goal` is implemented as lowercase `goal`, while the other built-in type constants are title-cased.

### 2. Effort semantics are inconsistent

Different parts of the code interpret effort differently:

- TFS ingestion normalizes `Effort` with fallback to `StoryPoints`
- the XML comment on `PoTool.Api/Persistence/Entities/WorkItemEntity.Effort` says `Effort estimate in hours (nullable)`
- many DTOs and handlers label the same data as story points
- roadmap analytics interprets active PBI/Bug effort as remaining work
- feature progress substitutes missing PBI effort with sibling averages

### 3. Sprint attribution uses multiple incompatible strategies

Observed strategies:

- current `IterationPath` equality
- resolved sprint cache
- sprint-window activity
- current done state vs historical done transition

This means “planned”, “worked”, “completed”, and “in sprint” are not derived from one single model across the application.

### 4. Sprint date handling is inconsistent

- `GetSprintMetricsQueryHandler` looks up sprint dates from the sprint repository
- `GetBacklogHealthQueryHandler` returns `null` sprint dates via a placeholder helper
- epic forecast extrapolates future sprints as fixed 14-day iterations after the last historical sprint

### 5. Activity semantics differ between ingestion and reporting

- ingestion whitelists `ChangedBy` and `ChangedDate`
- activity details and sprint trend interpretation exclude those two fields as non-functional

So the stored activity ledger is broader than the “meaningful activity” model used by reporting.

### 6. Bug triage is mostly a client-side interpretation, not a unified backend model

Bug triage currently depends on:

- synthetic grouping nodes (`New / Untriaged`, severity groups, missing severity)
- triage tags
- severity values from work item snapshots

But no repository-wide server-side triage workflow model was found. The strongest bug domain logic is in client-side grouping and metric calculation.

### 7. Backlog health has two overlapping models

There are two distinct interpretations of backlog quality:

- hierarchical validation consequences
- backlog-state refinement scoring (`0/75/100`, `0/25/average`, `0/30/average`)

They are related, but they are not the same model and are not derived from the same code path.
