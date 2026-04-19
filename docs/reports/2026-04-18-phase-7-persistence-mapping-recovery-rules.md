# Phase 7 Persistence Mapping + Recovery Rules

## 1. Summary

- **VERIFIED:** The active planning implementation today consists of an application-layer bridge over the planning engine plus product-scoped in-memory session state; there is no durable planning-intent persistence yet.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningSessionStore.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-5-in-memory-planning-session-state.md`.

- **VERIFIED:** The current planning engine state is expressed as `EpicId`, `RoadmapOrder`, `PlannedStartSprintIndex`, `ComputedStartSprintIndex`, `DurationInSprints`, and `TrackIndex`, while the read model exposes track and computed timing as derived output.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`.

- **PROPOSED:** This phase locks a durable planning-intent model that persists only PoTool-owned authoring intent (`ProductId`, `EpicId`, selected start sprint, duration, optional recovery metadata), writes only `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate` to TFS, treats TFS as fallback/recovery storage when internal state is absent, and never persists track or computed timing.  
  **Locked now by this phase.**

- **PROPOSED:** Recovery precedence is locked as: internal PoTool intent wins when present; TFS dates are recovery-only fallback; existing deterministic bootstrap remains the final fallback when both internal intent and valid TFS recovery are absent.  
  **Evidence:** existing bootstrap/session behavior in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; locked now by this phase.

## 2. Verified basis and locked assumptions

- **VERIFIED:** The bridge currently bootstraps active roadmap epics from product roots, roadmap-tag filtering, backlog priority ordering, default duration `1`, and main-lane placement.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`.

- **VERIFIED:** Session continuity is currently product-scoped in memory and resettable.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningSessionStore.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-5-in-memory-planning-session-state.md`.

- **VERIFIED:** Product scope already includes linked teams, and sprint metadata already exists as synced team iterations with start/end windows.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`.

- **VERIFIED:** Current sprint logic elsewhere in the repo consistently treats sprint windows as `[StartDateUtc, EndDateUtc)`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/PullRequests/PrSprintTrendsDto.cs`.

- **VERIFIED:** Current TFS work-item read/write contracts do not yet include planning-date fields or planning-date update methods.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`.

- **PROPOSED:** The TFS planning-date projection fields are locked as `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate`.  
  **Locked now by this phase from the provided design basis.**

## 3. Internal persistence model (locked)

- **PROPOSED:** PoTool persists one planning-intent row per active `(ProductId, EpicId)` pair.  
  **Locked now by this phase.**

- **PROPOSED:** The durable intent row stores exactly:
  - `ProductId`
  - `EpicId`
  - `StartSprintStartDateUtc` — the UTC first-day boundary of the selected canonical start sprint
  - `DurationInSprints`
  - `RecoveryStatus` — nullable, set only when the row originated from TFS recovery
  - `UpdatedAtUtc` — persistence/audit timestamp  
  **Locked now by this phase.**

- **INFERRED:** `StartSprintStartDateUtc` is the correct durable sprint identity for product-scoped planning because products can span multiple teams, sprint persistence is team-scoped, and `SprintEntity.Path` is team-specific while planning is product-scoped.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`.  
  **Locked now by this phase.**

- **PROPOSED:** `RoadmapOrder` is **not** part of durable PoTool planning intent because roadmap order remains externally owned by active roadmap input resolution and is already reconstructed from backlog priority/tagged roadmap scope during bootstrap.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`.  
  **Locked now by this phase.**

- **PROPOSED:** `TrackIndex` is **not persisted**.  
  **Evidence:** `TrackIndex` is part of engine/read-model output only in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; the locked basis for this phase explicitly states track is derived presentation.  
  **Locked now by this phase.**

- **PROPOSED:** `ComputedStartSprintIndex` and `EndSprintIndexExclusive` are **not persisted**.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; locked basis for this phase explicitly states computed start/end are derived.  
  **Locked now by this phase.**

## 4. TFS write-back model (locked)

- **PROPOSED:** PoTool writes only:
  - `Microsoft.VSTS.Scheduling.StartDate`
  - `Microsoft.VSTS.Scheduling.TargetDate`  
  **Locked now by this phase.**

- **PROPOSED:** Field meanings are locked as:
  - `StartDate` = the first calendar day of the selected start sprint
  - `TargetDate` = the final calendar day of the last sprint covered by the epic duration  
  **Locked now by this phase.**

- **PROPOSED:** PoTool always overwrites both fields together from one forward projection; partial writes are forbidden.  
  **Rationale:** start and end form one normalized projection pair; persisting only one would create an internally inconsistent recovery surface.  
  **Locked now by this phase.**

- **PROPOSED:** Write-back is always normalized to sprint boundaries from the canonical product sprint calendar; PoTool never writes mid-sprint or user-entered free-form dates for planning intent.  
  **Locked now by this phase.**

- **PROPOSED:** Both fields are treated as **date-only business values**, not time-of-day scheduling instants. The implementation phase should patch them as UTC-normalized calendar dates (`yyyy-MM-dd`) and compare them by calendar date only.  
  **UNKNOWN:** the repository does not currently prove the final wire-format serializer for these two specific fields because planning-date write support does not exist yet.  
  **Evidence:** absence of planning-date methods in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`.  
  **Locked now by this phase.**

## 5. Forward mapping rules (locked)

- **PROPOSED:** The next implementation phase must resolve a **canonical product sprint calendar** as:
  1. load all sprint records for every `TeamId` linked to the product
  2. keep only sprints with non-null `StartUtc` and non-null `EndUtc`
  3. normalize to UTC date boundaries
  4. deduplicate identical `[StartUtc.Date, EndUtc.Date)` windows across teams
  5. order ascending by `StartUtc.Date`  
  **INFERRED:** this is the minimal product-scoped calendar consistent with existing product/team ownership and sprint persistence.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`.  
  **Locked now by this phase.**

- **BLOCKER:** If the product cannot produce an unambiguous canonical sprint calendar because the linked teams expose conflicting overlapping windows that cannot be deduplicated, forward mapping and recovery for that product must fail explicitly; the implementation must not guess.  
  **Locked now by this phase.**

- **PROPOSED:** Forward projection from persisted intent is:
  - resolve the start sprint by exact match on `StartSprintStartDateUtc`
  - resolve the final sprint as `DurationInSprints - 1` positions later in the canonical calendar
  - `StartDate = startSprint.StartUtc.Date`
  - `TargetDate = finalSprint.EndUtc.Date.AddDays(-1)`  
  **Evidence:** repo sprint-window convention `[StartDateUtc, EndDateUtc)` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/PullRequests/PrSprintTrendsDto.cs`; locked basis states `StartDate = first day of selected start sprint` and `TargetDate = last day of final sprint`.  
  **Locked now by this phase.**

- **PROPOSED:** Internal representation remains sprint-count based (`DurationInSprints`, end exclusive inside the engine); external TFS representation is inclusive on `TargetDate` because it stores the last day of the final sprint, not the exclusive boundary.  
  **Evidence:** internal exclusive-end model in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; locked basis for this phase.  
  **Locked now by this phase.**

- **BLOCKER:** If the resolved canonical calendar does not contain enough future sprint windows to cover the persisted duration, PoTool may persist the internal intent but must not partially project TFS dates. The next implementation phase must surface this as a write-back failure, not as a guessed target date.  
  **Locked now by this phase.**

## 6. Reverse recovery rules (locked)

- **PROPOSED:** Recovery is attempted only when internal PoTool planning intent for an active `(ProductId, EpicId)` is missing.  
  **Evidence:** current internal-state-first behavior in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; locked now by this phase.

- **PROPOSED:** Recovery input is the raw TFS `StartDate`/`TargetDate` pair plus the canonical product sprint calendar for the product.  
  **Locked now by this phase.**

- **PROPOSED:** Start-date recovery rule:
  - truncate TFS `StartDate` to calendar date
  - find the sprint window whose date interval contains that date
  - recovered start sprint is that containing sprint
  - recovered persisted `StartSprintStartDateUtc` is the containing sprint’s first day  
  **Locked now by this phase.**

- **PROPOSED:** Target-date recovery rule:
  - truncate TFS `TargetDate` to calendar date
  - find the sprint window whose inclusive day range contains that date
  - recovered final sprint is that containing sprint  
  **Locked now by this phase.**

- **PROPOSED:** Derived duration is `endSprintIndex - startSprintIndex + 1`.  
  **Locked now by this phase.**

- **PROPOSED:** Dates that fall inside a sprint window but not on exact normalized boundaries are still recoverable; PoTool maps them to the containing sprint(s), derives duration from those sprint positions, and marks the result `RecoveredWithNormalization`.  
  **Locked now by this phase.**

- **PROPOSED:** Dates that fall outside all sprint windows produce `RecoveryFailed`.  
  **Locked now by this phase.**

- **PROPOSED:** Derived duration `< 1`, or any recovered end sprint earlier than the recovered start sprint, produces `RecoveryFailed`.  
  **Locked now by this phase.**

- **PROPOSED:** When recovery succeeds, PoTool persists the recovered internal intent immediately before continuing with normal planning orchestration.  
  **Locked now by this phase.**

- **PROPOSED:** When recovery fails, the next implementation phase must fall back to the existing deterministic bootstrap rules from active roadmap inputs rather than guessing dates or durations from incomplete TFS data.  
  **Evidence:** existing deterministic bootstrap in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`.  
  **Locked now by this phase.**

## 7. Legacy/invalid date handling (locked)

- **PROPOSED:** A `StartDate` earlier than `2021-04-19` (older than five years from the locked current-date context `2026-04-19`) is treated as legacy-invalid and ignored for planning recovery.  
  **Locked now by this phase from the provided design basis.**

- **PROPOSED:** Missing `StartDate` yields `RecoveryFailed`.  
  **Locked now by this phase.**

- **PROPOSED:** Missing `TargetDate` yields `RecoveryFailed`.  
  **Locked now by this phase.**

- **PROPOSED:** `TargetDate` earlier than a valid `StartDate` yields `RecoveryFailed`.  
  **Locked now by this phase.**

- **PROPOSED:** Ignored or invalid raw TFS values are preserved only as external source data; they must remain excluded from planning recovery logic but available to diagnostics/logging in the implementation phase.  
  **Locked now by this phase.**

- **PROPOSED:** Time components on TFS date values, if present, are ignored for planning recovery; recovery compares the normalized UTC calendar date only.  
  **Locked now by this phase.**

## 8. Precedence rules (locked)

- **PROPOSED:** When internal PoTool planning intent is present for an active epic, internal intent wins unconditionally.  
  **Locked now by this phase.**

- **PROPOSED:** When internal intent is missing, TFS dates are consulted only as fallback recovery input.  
  **Locked now by this phase.**

- **PROPOSED:** When internal intent is present but TFS dates disagree with the forward projection, TFS is treated as a stale projection, not as source-of-truth planning input.  
  **Locked now by this phase.**

- **PROPOSED:** When TFS dates were changed externally while internal intent still exists, PoTool does **not** merge those external changes back into internal intent. Internal intent remains authoritative and the next write-back/reconciliation pass rewrites TFS again from internal state.  
  **Locked now by this phase.**

- **PROPOSED:** PoTool rewrites both TFS planning-date fields together:
  - after successful planning-intent persistence on user-driven planning mutations
  - after `RecoveredWithNormalization`
  - during explicit drift reconciliation when internal intent exists and current TFS dates differ from forward projection  
  **Locked now by this phase.**

- **PROPOSED:** `RecoveredExact` does not require an immediate TFS rewrite when the current TFS pair already matches the normalized forward projection exactly.  
  **Locked now by this phase.**

## 9. Recovery status model (locked)

- **PROPOSED:** `RecoveredExact` means:
  - both TFS dates were present and valid
  - `StartDate` exactly matched the first day of the recovered start sprint
  - `TargetDate` exactly matched the last day of the recovered final sprint
  - derived duration was valid  
  **Next-layer action:** persist the recovered internal intent; no normalization rewrite required if TFS already matches.  
  **Locked now by this phase.**

- **PROPOSED:** `RecoveredWithNormalization` means:
  - both TFS dates were present and valid
  - both dates mapped into valid sprint windows
  - at least one date needed normalization to the canonical sprint boundary pair
  - derived duration was still valid  
  **Next-layer action:** persist the recovered internal intent and rewrite both TFS fields with the normalized boundary dates.  
  **Locked now by this phase.**

- **PROPOSED:** `RecoveryFailed` means:
  - one or both dates were missing, ignored, legacy-invalid, or outside all sprint windows, **or**
  - the derived start/end sprint ordering or duration was invalid, **or**
  - the product sprint calendar was ambiguous/unresolvable for this epic  
  **Next-layer action:** do not create recovered intent from TFS; fall back to existing deterministic bootstrap and surface diagnostic evidence.  
  **Locked now by this phase.**

## 10. Delete/reconciliation interaction

- **VERIFIED:** Active planning scope already comes from current active roadmap input (`ProductId` roots + roadmap-tagged epic filtering), not from persisted planning state.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`.

- **PROPOSED:** If an epic no longer exists in active roadmap input for its product, its internal planning-intent row is stale and must be removed from the live planning-intent set during reconciliation.  
  **Locked now by this phase.**

- **PROPOSED:** If TFS planning dates still exist on an epic that disappeared from active roadmap input, PoTool does not clear those dates automatically. They are outside active planning scope, and roadmap membership remains externally owned.  
  **Evidence:** active-input ownership in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; locked now by this phase.

- **PROPOSED:** Stale internal planning intent for an epic that disappeared upstream must not be loaded back into active planning state even if the stale row still exists temporarily before cleanup.  
  **Locked now by this phase.**

- **PROPOSED:** If the epic later re-enters active roadmap scope and no internal row exists, normal precedence applies again: internal absent → attempt TFS recovery → otherwise bootstrap defaults.  
  **Locked now by this phase.**

## 11. Risks and remaining unknowns

- **UNKNOWN:** The repository does not yet prove whether all linked teams for a product always share one aligned sprint calendar.  
  **Evidence:** products are multi-team capable in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`, while sprint persistence is team-scoped in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`.  
  **Locked mitigation:** ambiguous calendars are explicit blocker conditions, never guessed.

- **UNKNOWN:** The exact TFS PATCH serialization/round-tripping behavior for `StartDate`/`TargetDate` is not yet implemented in this repository.  
  **Evidence:** no date-field update methods exist in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs` or `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`.  
  **Locked mitigation:** treat both fields as date-only UTC calendar values and patch both together.

- **VERIFIED:** Existing persisted planning projections are forecast/reporting outputs, not durable planning-intent storage, and must not be reused as the intent source for this next slice.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`.

## 12. Recommendation for the next implementation phase

- **PROPOSED:** The next coding phase should add:
  1. a durable planning-intent persistence model in the API/persistence layer
  2. a Core planning-intent store abstraction plus recovery/forward-mapping orchestration
  3. TFS contract updates to read/write `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate`
  4. reconciliation logic for stale internal rows and stale TFS projections  
  **Locked now by this phase.**

- **PROPOSED:** The next coding phase should touch only:
  - `PoTool.Core` for planning-intent abstractions and orchestration
  - `PoTool.Api` for EF entities/repositories/services/composition
  - `PoTool.Integrations.Tfs` for field read/write support
  - `PoTool.Shared` only as needed to extend boundary contracts for planning-date read data  
  **Locked now by this phase.**

- **PROPOSED:** The next coding phase must **not** touch:
  - the planning engine in `PoTool.Core.Domain/Domain/Planning`
  - `ReleasePlanningBoard`/release-planning slices
  - UI pages/components
  - unrelated TFS mapping outside the two locked planning-date fields  
  **Evidence:** current engine boundary in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; current active planning bridge in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.  
  **Locked now by this phase.**

- **PROPOSED:** Required tests for the next coding phase are:
  - exact forward-mapping tests
  - exact recovery tests
  - normalization recovery tests
  - legacy-invalid date tests (`StartDate < 2021-04-19`)
  - missing/partial date recovery-failure tests
  - precedence tests for internal-vs-TFS disagreement
  - ambiguous-calendar blocker tests
  - stale-epic reconciliation tests
  - TFS client patch tests proving both fields are written together and never partially  
  **Locked now by this phase.**

## Final section

### Findings

- **VERIFIED:** The repository already has the planning engine, product-scoped application bridge, in-memory continuity, product/team ownership, and sprint persistence needed to support the next durable planning slice.
- **VERIFIED:** The repository does **not** yet have planning-date TFS contracts or durable planning-intent persistence.
- **INFERRED:** Product-scoped planning requires a canonical product sprint calendar built from linked-team sprint windows.

### Locked decisions

- **PROPOSED:** Persist only `(ProductId, EpicId, StartSprintStartDateUtc, DurationInSprints, optional RecoveryStatus, UpdatedAtUtc)`.
- **PROPOSED:** Do **not** persist `TrackIndex`, `ComputedStartSprintIndex`, `EndSprintIndexExclusive`, or `RoadmapOrder`.
- **PROPOSED:** Write only `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate`.
- **PROPOSED:** Always write both TFS fields together from normalized sprint-boundary projection; never partially.
- **PROPOSED:** Internal PoTool intent wins when present; TFS dates are fallback/recovery only.
- **PROPOSED:** Recovery statuses are exactly `RecoveredExact`, `RecoveredWithNormalization`, and `RecoveryFailed`.
- **PROPOSED:** Disappeared upstream epics lose live internal intent; PoTool does not auto-clear lingering TFS dates for out-of-scope epics.

### Evidence references

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningSessionStore.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/Products/GetProductPlanningProjectionsQueryHandler.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-5-in-memory-planning-session-state.md`

### Risks

- **BLOCKER:** Product-specific recovery/write-back must fail explicitly when the linked teams do not yield one unambiguous canonical sprint calendar.
- **UNKNOWN:** TFS wire-format behavior for the two planning-date fields is not yet proven in code.

### Remaining unknowns

- **UNKNOWN:** Whether every product’s linked teams always share aligned sprint windows in real data.
- **UNKNOWN:** Whether legacy TFS planning-date values include inconsistent time components or timezone offsets; the locked rule is to ignore time-of-day semantics and normalize by UTC calendar date.

### GO/NO-GO for the next implementation phase

- **GO:** The next implementation phase can proceed. The core persistence shape, TFS projection model, recovery derivation rules, precedence rules, and failure behavior are now locked without architectural ambiguity.
