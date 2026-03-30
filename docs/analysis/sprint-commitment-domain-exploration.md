# Sprint Commitment Domain Exploration

_Generated: 2026-03-16_

This exploration inventories the current sprint-planning and sprint-execution semantics that already exist across Core, Api, shared DTOs, tests, and legacy repository documentation. The goal is domain discovery only: identify the canonical concepts already emerging in code, the conflicting interpretations that still remain, and the feasibility of extracting a dedicated Sprint Commitment slice into the Canonical Domain Core (CDC).

## Locations of Sprint Commitment Logic

| Location | Responsibility | Current semantic role |
| --- | --- | --- |
| `PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs` | `SprintCommitmentLookup` | Canonical commitment reconstruction at `SprintStart + 1 day` by replaying `System.IterationPath` changes backward from the current snapshot. |
| `PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs` | `SprintSpilloverLookup` | Canonical spillover detection: committed, not Done at sprint end, then direct move to the next sprint. |
| `PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs` | `FirstDoneDeliveryLookup` | Canonical first-Done attribution across reopen scenarios. |
| `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs` | `SprintDeliveryProjectionService` | Delivery-trend core that consumes committed IDs, first-Done lookup, next-sprint path, and projection inputs; falls back to `ResolvedSprintId` when committed IDs are not supplied. |
| `PoTool.Core.Domain/Domain/Metrics/SprintExecutionMetricsCalculator.cs` | `SprintExecutionMetricsCalculator` | Canonical formulas for `ChurnRate`, `CommitmentCompletion`, `SpilloverRate`, and `AddedDeliveryRate`. |
| `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs` | `GetSprintMetricsQueryHandler` | Historical sprint metrics using commitment reconstruction plus first-Done detection inside the sprint window. |
| `PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs` | `GetSprintExecutionQueryHandler` | Historical sprint execution model: committed scope, added scope, removed scope, spillover, completion order, plus application-only starved-work heuristic. |
| `PoTool.Api/Services/SprintTrendProjectionService.cs` | `SprintTrendProjectionService` | Orchestrates sprint projections and prepares canonical inputs for `SprintDeliveryProjectionService`. |
| `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs` | `GetSprintTrendMetricsQueryHandler` | Reads cached or recomputed sprint trend projections; semantic consumer, not semantic owner. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | `GetPortfolioProgressTrendQueryHandler` | Uses `PlannedEffort` as an `AddedEffort` proxy; adjacent analytics that should not define sprint commitment semantics. |
| `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` | `ResolvedSprintId` snapshot cache | Current sprint membership snapshot, authoritative for “now” only, not for historical commitment reconstruction. |
| `PoTool.Shared/Metrics/SprintExecutionDtos.cs` | Sprint execution DTOs | Exposes canonical story-point outputs (`CommittedSP`, `AddedSP`, `RemovedSP`, `DeliveredSP`, `SpilloverSP`) plus `StarvedPbis`. |
| `PoTool.Tests.Unit/Services/HistoricalSprintLookupTests.cs` | Helper coverage | Verifies commitment boundary behavior, first-Done attribution, and spillover edge cases. |
| `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs` | Handler coverage | Verifies commitment reconstruction, added scope, first-Done attribution, and no raw done fallback. |
| `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs` | Handler coverage | Verifies added scope, removed scope, direct-next-sprint spillover, backlog round-trip exclusion, and starved-work separation. |
| `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceTests.cs` | Projection coverage | Verifies committed planned scope, exclusion of items added after commitment, and spillover semantics in projections. |
| `docs/architecture/domain-model.md` / `docs/rules/sprint-rules.md` / `docs/rules/metrics-rules.md` / `docs/rules/source-rules.md` | Canonical docs | Current authoritative domain statements for sprint window, commitment timestamp, churn, spillover, and update-vs-snapshot truth. |
| `docs/architecture/repository-domain-discovery.md` | Repository discovery | Still documents older snapshot-oriented descriptions for some sprint surfaces and therefore contains semantic drift. |

## Sprint Commitment Detection

Current canonical commitment reconstruction is already explicit in code:

- `CommitmentTimestamp = SprintStart + 1 day`
- committed work = work items whose `IterationPath` equals the sprint path at that timestamp
- reconstruction source = update history, not current membership
- reconstruction algorithm = start from current snapshot iteration path and replay later iteration changes backward

Observed implementations:

1. `SprintCommitmentLookup.GetCommitmentTimestamp`
   - hardcodes the current canonical policy of `SprintStart + 1 day`
2. `SprintCommitmentLookup.BuildCommittedWorkItemIds`
   - resolves membership at the commitment timestamp
3. `GetSprintMetricsQueryHandler`
   - uses committed IDs for planned story points
4. `GetSprintExecutionQueryHandler`
   - uses committed IDs for initial scope and spillover basis
5. `SprintTrendProjectionService.ComputeProjectionsAsync`
   - prepares committed IDs and passes them into delivery projections

This means the repository already treats **Sprint Commitment** as a historical reconstruction problem, not a current-snapshot lookup.

## Scope Change Detection

Current canonical scope-change detection is based on `System.IterationPath` events after commitment:

- **SprintScopeAdded** = first or later entry into the sprint after `CommitmentTimestamp` and before `SprintEnd`
- **SprintScopeRemoved** = exit from the sprint after `CommitmentTimestamp` and before `SprintEnd`
- source of truth = `ActivityEventLedgerEntries`
- evaluation window = after commitment, within the dated sprint window

Observed implementations:

- `GetSprintExecutionQueryHandler`
  - builds `AddedDuringSprint` from iteration events where `NewValue == sprint.Path`
  - builds `RemovedDuringSprint` from iteration events where `OldValue == sprint.Path`
- `GetSprintMetricsQueryHandler`
  - includes committed scope plus work added after commitment in total sprint scope
- `SprintExecutionDtos`
  - surfaces `AddedSP`, `RemovedSP`, `AddedDuringSprint`, and `RemovedDuringSprint`

Important edge cases already encoded in tests:

- a work item added after commitment does not belong to initial scope
- a committed item moved away after commitment still belongs to reconstructed initial scope
- backlog round-trip behavior is different from direct spillover

## Spillover Detection

Current spillover logic is more specific than “unfinished at sprint end”:

- item must be part of `SprintCommitment`
- item must not be in canonical Done at `SprintEnd`
- item must move directly from Sprint N to Sprint N+1 as its first post-sprint iteration move

Observed implementations:

- `SprintSpilloverLookup.BuildSpilloverWorkItemIds`
  - reconstructs state at sprint end
  - excludes already-Done items
  - checks the first post-sprint iteration move
- `GetSprintExecutionQueryHandler`
  - exposes spillover in detail and summary DTOs
- `SprintDeliveryProjectionService`
  - consumes the spillover lookup to populate projection counts and story points

This confirms that **SprintSpillover** is not the same as:

- unfinished committed scope still sitting in the same sprint path
- work that went back to backlog and later entered the next sprint
- “starved work” heuristics

## Planning vs Execution Concepts

The codebase currently separates planning-time and execution-time concepts more clearly than older docs suggest.

### Planning concepts

- `SprintCommitment`
- `SprintScope`
- `SprintWorkItemSnapshot`
- `SprintPlanningEvent`
- committed planned story points

These depend primarily on:

- sprint metadata
- iteration-path history
- current snapshot only as the reconstruction anchor

### Execution concepts

- `SprintScopeAdded`
- `SprintScopeRemoved`
- `SprintCompletion`
- `SprintThroughput`
- `SprintSpillover`
- `SprintCompletionRate`

These depend on:

- commitment reconstruction
- state history and canonical state mapping
- first-Done attribution
- next-sprint resolution

### Application-level interpretation

- `StarvedPbis` in `SprintExecutionDtos`
- portfolio `AddedEffort` proxy in `GetPortfolioProgressTrendQueryHandler`

These are useful UI or analytics concepts, but they are not core sprint commitment primitives.

## Inconsistencies Found

The strongest remaining inconsistencies are no longer in the main canonical handlers; they are in fallback paths, adjacent analytics, DTO wording, and repository documentation.

1. **Snapshot fallback still exists in the delivery projection core**
   - `SprintDeliveryProjectionService` falls back to `ResolvedSprintId == SprintId` when `CommittedWorkItemIds` are not supplied.
   - `ComputeProjectionsAsync` supplies committed IDs today, but the fallback means the underlying service still accepts a non-canonical path.

2. **`ResolvedSprintId` is still widely used as test setup shorthand**
   - many `SprintTrendProjectionServiceTests` rows seed `ResolvedSprintId` as if it were the planning truth.
   - that is acceptable for projection plumbing, but semantically weaker than explicit commitment reconstruction.

3. **`docs/architecture/repository-domain-discovery.md` still describes older snapshot semantics**
   - it documents `GetSprintMetricsQueryHandler` as a snapshot model
   - it documents `SprintTrendProjectionService` as planned scope via `ResolvedSprintId`
   - those descriptions now lag the current canonical implementation path

4. **DTO naming still mixes canonical and heuristic concepts**
   - `InitialScopeCount` in `SprintExecutionSummaryDto` is actually reconstructed committed scope
   - `StarvedPbis` is exposed next to canonical concepts but is heuristic application logic

5. **Portfolio flow still uses planning proxies**
   - `GetPortfolioProgressTrendQueryHandler` labels `AddedEffort` from `PlannedEffort`
   - that should remain isolated from sprint commitment semantics

## Candidate Domain Concepts

### SprintCommitment

- **definition**: work items whose iteration path equals the sprint at `CommitmentTimestamp`
- **inputs**: sprint metadata, current work item snapshot, iteration-path history
- **derived signals**: committed work item IDs, committed scope counts, committed story points
- **dependencies on CDC primitives**: sprint window, update history truth, state-independent membership reconstruction

### SprintScope

- **definition**: all work items considered part of sprint scope, split into committed scope and post-commitment additions
- **inputs**: `SprintCommitment`, `SprintScopeAdded`
- **derived signals**: total sprint scope count, total scope story points
- **dependencies on CDC primitives**: sprint window, commitment timestamp, iteration-path events

### SprintScopeAdded

- **definition**: work items entering the sprint after `CommitmentTimestamp` and before `SprintEnd`
- **inputs**: sprint path, iteration-path change events, sprint dates
- **derived signals**: added item IDs, added timestamps, added story points
- **dependencies on CDC primitives**: sprint window, commitment timestamp, update history truth

### SprintScopeRemoved

- **definition**: work items leaving the sprint after `CommitmentTimestamp` and before `SprintEnd`
- **inputs**: sprint path, iteration-path change events, sprint dates
- **derived signals**: removed item IDs, removed timestamps, removed story points
- **dependencies on CDC primitives**: sprint window, commitment timestamp, update history truth

### SprintSpillover

- **definition**: committed work not Done at sprint end whose first post-sprint move is directly into the next sprint
- **inputs**: committed work item IDs, state history, iteration-path history, next-sprint path
- **derived signals**: spillover IDs, spillover count, spillover story points
- **dependencies on CDC primitives**: commitment, canonical Done state, state-at-timestamp reconstruction, next-sprint ordering

### SprintCompletion

- **definition**: a work item’s first canonical transition into Done inside the sprint window
- **inputs**: state history, canonical state mapping, sprint window
- **derived signals**: first-Done timestamp, completion order, delivered story points
- **dependencies on CDC primitives**: canonical state mapping, first-Done rule, update history truth

### SprintWorkItemSnapshot

- **definition**: current work item state used only as the reconstruction anchor for point-in-time sprint semantics
- **inputs**: current `IterationPath`, current `State`, work item type
- **derived signals**: initial state for backward replay
- **dependencies on CDC primitives**: snapshot truth for “now”, hybrid analysis boundary

### SprintPlanningEvent

- **definition**: timestamped iteration-path change relevant to sprint membership and commitment semantics
- **inputs**: `System.IterationPath` ledger entries
- **derived signals**: entered sprint, left sprint, direct next-sprint move
- **dependencies on CDC primitives**: update history truth, sprint window

## Semantic Conflicts

| Location | Current behavior | Canonical alternative |
| --- | --- | --- |
| `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs` | Accepts a fallback where planned scope comes from `ResolvedSprintId` if `CommittedWorkItemIds` is absent. | Require explicit committed IDs or move the fallback behind an explicitly non-canonical adapter. |
| `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs` | Stores current sprint membership as `ResolvedSprintId`, which is useful for “now” but easy to misuse for historical planning semantics. | Keep `ResolvedSprintId` as snapshot-only and never treat it as `SprintCommitment`. |
| `docs/architecture/repository-domain-discovery.md` | Still describes `GetSprintMetricsQueryHandler` and projection logic with snapshot-oriented semantics. | Update repository discovery docs to match the current commitment/first-Done/spillover helpers. |
| `PoTool.Shared/Metrics/SprintExecutionDtos.cs` | Places heuristic `StarvedPbis` next to canonical commitment metrics. | Keep starved work as application-level interpretation, separate from canonical sprint commitment concepts. |
| `PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs` | Uses `PlannedEffort` as an `AddedEffort` proxy in portfolio flow. | Keep that proxy explicitly outside sprint commitment semantics and avoid reusing it as canonical scope-added meaning. |

## Canonical Sprint Model

The current repository behavior supports the following canonical proposal.

### SprintCommitment

- **definition**: work items present in the sprint iteration at `CommitmentTimestamp`
- **policy**: `CommitmentTimestamp = SprintStart + 1 day`
- **notes**: this is the repository’s current canonical commitment rule; using pure `SprintStart` would be a policy change, not a restatement of current behavior

### SprintScopeAdded

- **definition**: work items entering the sprint iteration after `CommitmentTimestamp` and on or before `SprintEnd`

### SprintScopeRemoved

- **definition**: work items removed from the sprint iteration after `CommitmentTimestamp` and on or before `SprintEnd`

### SprintThroughput

- **definition**: committed or added work items whose first canonical Done transition occurs within the sprint window

### SprintSpillover

- **definition**: committed items not Done at sprint end whose first post-sprint iteration move is directly into the next sprint

### SprintCompletionRate

- **definition**: `DeliveredSP / (CommittedSP - RemovedSP)`

### Supporting formulas

- `CommittedSP = sum(story points of committed PBIs, excluding derived estimates)`
- `AddedSP = sum(story points of PBIs added after commitment)`
- `RemovedSP = sum(story points of PBIs removed after commitment)`
- `DeliveredFromAddedSP = sum(story points delivered from added scope)`
- `SpilloverSP = sum(story points of spillover PBIs)`
- `ChurnRate = (AddedSP + RemovedSP) / (CommittedSP + AddedSP)`
- `SpilloverRate = SpilloverSP / (CommittedSP - RemovedSP)`
- `AddedDeliveryRate = DeliveredFromAddedSP / AddedSP`

# Sprint Commitment CDC Feasibility

| Concept | Classification | Required signals | Projection dependencies | Data availability from revision history |
| --- | --- | --- | --- | --- |
| `SprintCommitment` | **Core CDC concept** | sprint metadata, current snapshot, iteration-path history | `SprintCommitmentLookup` | Available now from `ActivityEventLedgerEntries` + work item snapshot |
| `SprintScopeAdded` | **Core CDC concept** | iteration-path entry events after commitment | `GetSprintExecutionQueryHandler`, `SprintExecutionDtos` | Available now from `System.IterationPath` history |
| `SprintScopeRemoved` | **Core CDC concept** | iteration-path exit events after commitment | `GetSprintExecutionQueryHandler`, `SprintExecutionDtos` | Available now from `System.IterationPath` history |
| `SprintCompletion` | **Core CDC concept** | state events, canonical state mapping | `FirstDoneDeliveryLookup` | Available now from `System.State` history + state mappings |
| `SprintSpillover` | **Core CDC concept** | committed IDs, state-at-end, next-sprint move | `SprintSpilloverLookup` | Available now when sprint dates and adjacent sprint metadata exist |
| `SprintWorkItemSnapshot` | **Core CDC concept** | current iteration path, current state, work item type | all point-in-time reconstruction helpers | Available now from cached work item snapshots |
| `SprintPlanningEvent` | **Core CDC concept** | timestamped iteration-path change events | commitment/add/remove/spillover helpers | Available now from `ActivityEventLedgerEntries` |
| `SprintThroughput` | **Derived CDC metric** | first-Done timestamps for committed and added items | `GetSprintMetricsQueryHandler`, `SprintDeliveryProjectionService` | Fully derivable today |
| `SprintCompletionRate` | **Derived CDC metric** | `CommittedSP`, `RemovedSP`, `DeliveredSP` | `SprintExecutionMetricsCalculator` | Fully derivable today |
| `ChurnRate` | **Derived CDC metric** | `CommittedSP`, `AddedSP`, `RemovedSP` | `SprintExecutionMetricsCalculator` | Fully derivable today |
| `SpilloverRate` | **Derived CDC metric** | `CommittedSP`, `RemovedSP`, `SpilloverSP` | `SprintExecutionMetricsCalculator` | Fully derivable today |
| `AddedDeliveryRate` | **Derived CDC metric** | `DeliveredFromAddedSP`, `AddedSP` | `SprintExecutionMetricsCalculator` | Fully derivable today |
| `StarvedWork` | **Application-level metric** | committed scope, unfinished scope, later-added completions | `GetSprintExecutionQueryHandler` | Available today, but heuristic rather than canonical |
| Portfolio `AddedEffort` proxy | **Application-level metric** | `PlannedEffort` from projections | `GetPortfolioProgressTrendQueryHandler` | Available today, but semantically separate from sprint commitment |

Feasibility conclusion:

- the repository already has the core event signals needed for a Sprint Commitment CDC slice
- the strongest CDC-ready primitives already live in `PoTool.Core.Domain/Domain/Sprints`
- the main extraction risk is not missing data; it is semantic leakage from snapshot fallbacks and adjacent application-level heuristics
- a dedicated CDC slice is feasible now if the extraction boundary is kept strict: commitment, add/remove, first-Done completion, spillover, and rate formulas belong inside CDC; starved work and portfolio flow proxies do not
