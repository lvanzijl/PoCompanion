# Phase 2 Decision Lock — Option A

## 1. Summary

- **VERIFIED:** Phase 1 established that the active planning flow is the TFS-driven stack built around roadmap membership via tags, roadmap ordering via backlog priority, and sprint assignment via iteration path.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-1-current-state-analysis.md`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`.

- **VERIFIED:** Phase 1 also established that the current timeline is forecast-derived and read-only, and that `ReleasePlanningBoard` exists separately from the active planning flow.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-1-current-state-analysis.md`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`.

- **PROPOSED (LOCKED):** Phase 2 engine will extend the active TFS-driven planning model and will **not** use `ReleasePlanningBoard`, `LaneDto`, `EpicPlacementDto`, `MilestoneLineDto`, or `IterationLineDto`.

- **PROPOSED (LOCKED):** Phase 2 engine is an internal PoTool planning engine. It owns planning state and recompute behavior, but it does **not** own TFS order, roadmap membership, or sprint-assignment fields.

## 2. Decision basis from Phase 1

### 2.1 Verified basis

1. **VERIFIED:** Current roadmap order is derived from roadmap-tagged epics sorted by backlog priority.  
   **Evidence:** `2026-04-18-phase-1-current-state-analysis.md`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapEpicDiscoveryAnalysis.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`.

2. **VERIFIED:** Current planning mutations already have clear TFS ownership:
   - roadmap membership → tags
   - roadmap order → backlog priority
   - sprint assignment → iteration path  
   **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`, endpoints `UpdateTags`, `UpdateBacklogPriority`, `UpdateIterationPath`.

3. **VERIFIED:** Current timeline data is forecast-derived only and has no editable start/duration model.  
   **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`.

4. **VERIFIED:** `ReleasePlanningBoard` is a separate row/placement model and is not the active planning path.  
   **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`; Phase 1 report.

### 2.2 Consequence of the basis

- **INFERRED:** The safest architecture for Option A is to add a new PoTool-owned planning state layer that sits beside the current TFS-driven roadmap flow instead of replacing the TFS order/membership model.

- **INFERRED:** The engine must consume an already-ordered roadmap epic list and produce internal planning outputs without taking a dependency on TFS field names or TFS write mechanics.

## 3. Final locked decisions

## Decision 1 — Authoritative source of truth

- **PROPOSED (LOCKED):** The following fields are authoritative in PoTool for the new planning engine:
  1. `PlannedStartSprintIndex` — user intent for earliest desired start
  2. `ComputedStartSprintIndex` — engine-resolved start after recompute
  3. `DurationInSprints` — user-controlled duration
  4. `TrackIndex` — lane assignment (`0` = main lane, `1..n` = parallel tracks)

- **PROPOSED (LOCKED):** The following remain authoritative in TFS and are **not** re-owned by the engine:
  1. roadmap membership
  2. roadmap order
  3. sprint assignment used by Plan Board

- **PROPOSED (LOCKED):** Planning dates and duration are stored only in PoTool in Phase 2. They are **not mirrored to TFS** in Phase 2.

### Rationale

- **VERIFIED:** The repository already uses TFS as the active source for roadmap membership/order and sprint assignment.  
  **Evidence:** `WorkItemsController.cs`; `ProductRoadmapEditor.razor`; Phase 1 report.

- **VERIFIED:** No current TFS-backed editable timeline model exists.  
  **Evidence:** `PlanningProjectionDtos.cs`; `RoadmapTimelineLayout.cs`.

### Impact

- The new engine can be implemented without changing existing roadmap membership/order ownership.
- Existing roadmap editor behavior remains compatible.
- Timeline planning becomes additive, not disruptive.

## Decision 2 — Exact TFS boundary for Option A

- **PROPOSED (LOCKED):**
  - roadmap membership remains mapped to the current roadmap tag path
  - roadmap order remains mapped to the current roadmap ordering path
  - Plan Board sprint assignment remains mapped to the current sprint-assignment path

- **PROPOSED (LOCKED):** In engine contracts and engine state, these are represented only as:
  - `IsRoadmapMember`
  - `RoadmapOrder`
  - `AssignedSprintReference` or equivalent adapter-level input/output

- **PROPOSED (LOCKED):** The engine itself must not mention TFS field names or transport concerns.

### Rationale

- **VERIFIED:** Current live endpoints already isolate these behaviors through `WorkItemsController`.  
  **Evidence:** `UpdateTags`, `UpdateBacklogPriority`, `UpdateIterationPath` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`.

### Impact

- Engine implementation stays testable and infrastructure-free.
- Phase 3 can add adapter logic without changing engine rules.

## Decision 3 — Internal planning model

- **PROPOSED (LOCKED):** The engine model for each roadmap epic is:

```text
PlanningEpicState
  EpicId: int
  RoadmapOrder: int
  PlannedStartSprintIndex: int
  ComputedStartSprintIndex: int
  DurationInSprints: int
  TrackIndex: int
```

- **PROPOSED (LOCKED):**
  - `RoadmapOrder` is input order, already resolved outside the engine
  - `TrackIndex = 0` means main lane
  - `TrackIndex >= 1` means parallel track `P{TrackIndex}`
  - epic interval is a half-open range:  
    `Interval = [ComputedStartSprintIndex, ComputedStartSprintIndex + DurationInSprints)`

### Rationale

- **VERIFIED:** Phase 1 shows roadmap order already exists and timeline start/duration do not.  
  **Evidence:** Phase 1 report; `PlanningProjectionDtos.cs`.

### Impact

- Engine state is minimal and implementation-ready.
- No dependency on forecast DTO shape is required.

## Decision 4 — Main lane and parallel-track rules

- **PROPOSED (LOCKED):**
  1. Main lane is `TrackIndex = 0`.
  2. Parallel tracks are positive integers `1..n`.
  3. No overlap is allowed inside the same track.
  4. Track reuse is top-first: the engine always chooses the lowest valid track index.
  5. There are no anchors, swimlane locks, or manual absolute row reservations.
  6. Roadmap order constraint is:  
     `ComputedStartSprintIndex(E[i]) >= ComputedStartSprintIndex(E[i-1])`

- **PROPOSED (LOCKED):** Same-track overlap is defined as intersection of half-open intervals:
  - overlap exists when  
    `A.Start < B.End` **and** `B.Start < A.End`

### Rationale

- **VERIFIED:** Phase 1 found no active timeline lane model, so the engine must introduce one without relying on `ReleasePlanningBoard`.  
  **Evidence:** Phase 1 report; `ReleasePlanningDtos.cs`.

### Impact

- The engine has deterministic track allocation.
- Track behavior is bounded and implementation-ready.

## Decision 5 — Sprint model

- **PROPOSED (LOCKED):**
  - Internal sprint representation is a zero-based integer index.
  - `SprintIndex = 0` means the first planning slot in engine scope.
  - Engine math uses only integer indices.
  - Mapping from `SprintIndex` to real sprint identifiers, dates, or iteration paths is **deferred** and is not part of Phase 2 engine logic.

### Rationale

- **VERIFIED:** Phase 1 showed real sprint representation already exists in `SprintDto`/`SprintEntity`, but current timeline rendering derives from forecast rather than editable planning slots.  
  **Evidence:** Phase 1 report; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/SprintDto.cs`.

### Impact

- Phase 2 engine remains independent of real sprint calendars.
- Phase 3 can map engine indices onto real sprint windows without changing engine rules.

## Decision 6 — Duration model

- **PROPOSED (LOCKED):**
  - `DurationInSprints` is an integer.
  - Minimum value is `1`.
  - Default value is `1`.
  - Users control duration directly.
  - `EndSprintIndexExclusive = ComputedStartSprintIndex + DurationInSprints`

### Rationale

- **VERIFIED:** Current forecast view already expresses schedule in sprint-based duration (`SprintsRemaining`).  
  **Evidence:** `PlanningProjectionDtos.cs`; `RoadmapTimelineLayout.cs`.

### Impact

- Duration math is simple and engine-safe.
- Consumers can render bars and validate overlap without date arithmetic.

## Decision 7 — Exact recompute rule

- **PROPOSED (LOCKED):** For the first epic in roadmap order:

```text
ComputedStartSprintIndex(E[0]) = max(0, PlannedStartSprintIndex(E[0]))
```

- **PROPOSED (LOCKED):** For every later epic:

```text
ComputedStartSprintIndex(E[i]) = max(
  EndSprintIndexExclusive(E[i-1]),
  PlannedStartSprintIndex(E[i]))
```

- **PROPOSED (LOCKED):**
  - recompute is forward-only
  - recompute starts at the earliest changed roadmap position
  - epics before the changed position are not recomputed
  - no left-compaction occurs
  - any spacing inserted before an epic is consumed implicitly by its `PlannedStartSprintIndex`

### Rationale

- **VERIFIED:** Current timeline is derived, not manually scheduled. A deterministic forward-only recompute rule is required to add internal planning without replacing current TFS order.  
  **Evidence:** Phase 1 report; `RoadmapTimelineLayout.cs`.

### Impact

- The engine produces a stable schedule after every mutation.
- Moving an earlier epic later can push the suffix forward.
- Moving an epic left never pulls it earlier than the previous epic’s end.

## Decision 8 — Spacing model

- **PROPOSED (LOCKED):**
  - Spacing is not its own entity.
  - Spacing before `E[i]` is implicit in `PlannedStartSprintIndex(E[i])`.
  - Derived visible spacing before `E[i]` is:

```text
SpacingBefore(E[i]) = max(0, PlannedStartSprintIndex(E[i]) - EndSprintIndexExclusive(E[i-1]))
```

- **PROPOSED (LOCKED):** When spacing is reduced below the previous epic’s end, recompute consumes it and the effective visible spacing becomes `0`.

### Rationale

- **VERIFIED:** The current roadmap flow already uses implicit positional math rather than persisted spacing entities.  
  **Evidence:** `ProductRoadmapEditor.razor`, method `CalculateInsertPriority`; Phase 1 report.

### Impact

- No extra persistence model is required for gaps.
- Gap edits are implemented by changing one field: `PlannedStartSprintIndex`.

## Decision 9 — Operation: `MoveEpicBySprints`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`, `DeltaSprints`
  - **Preconditions:**
    - epic exists in engine state
    - `DeltaSprints != 0`
  - **Behavior:**
    1. `PlannedStartSprintIndex = max(0, PlannedStartSprintIndex + DeltaSprints)`
    2. recompute from the moved epic forward
    3. `TrackIndex` is unchanged
  - **Output:** updated planning state and affected epic range
  - **Side effects:** none inside the engine
  - **Constraints:** cannot produce negative planned start

### Rationale

- **VERIFIED:** Current roadmap editing already supports movement/order changes, but not planned-start movement.  
  **Evidence:** `ProductRoadmapEditor.razor`.

### Impact

- This is the base mutation for moving an epic left or right in the planning horizon.
- Negative movement may change intent without changing actual computed start if the previous epic still blocks it.

## Decision 10 — Operation: `ReorderEpic`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`, `TargetRoadmapOrder`
  - **Preconditions:**
    - epic exists
    - target order is within bounds
  - **Behavior:**
    1. reorder the roadmap sequence
    2. renumber `RoadmapOrder` as contiguous integers
    3. recompute from `min(oldIndex, newIndex)` forward
    4. keep each epic’s `PlannedStartSprintIndex`, `DurationInSprints`, and `TrackIndex`
  - **Output:** reordered and recomputed planning state
  - **Side effects inside engine:** none
  - **Side effects outside engine:** the application layer must persist the resulting roadmap order through the existing roadmap-order integration path

### Rationale

- **VERIFIED:** Phase 1 established that roadmap order is still externally owned and already mutable in the current roadmap editor.  
  **Evidence:** Phase 1 report; `ProductRoadmapEditor.razor`; `WorkItemsController.UpdateBacklogPriority`.

### Impact

- Reordering remains compatible with the current TFS-driven roadmap model.
- Engine stays agnostic to how external order persistence is performed.

## Decision 11 — Operation: `RunInParallel`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`
  - **Preconditions:**
    - epic exists
    - epic already has valid `ComputedStartSprintIndex` and `DurationInSprints`
  - **Behavior:**
    1. keep `PlannedStartSprintIndex` unchanged
    2. keep `ComputedStartSprintIndex` unchanged
    3. assign the lowest positive `TrackIndex` whose interval does not overlap the epic’s current interval
  - **Output:** updated epic with a new `TrackIndex`
  - **Side effects:** none inside the engine
  - **Constraints:** if the current interval is valid on `TrackIndex = 1`, use `1`; otherwise use the next free positive track

- **PROPOSED (LOCKED):** `RunInParallel` is a **track-assignment operation**, not a start-date acceleration operation.

### Rationale

- **VERIFIED:** The exact recompute rule locked in Decision 7 makes the schedule forward-only by roadmap order.  
  **Evidence:** this report, Decision 7.

- **INFERRED:** To remain consistent with the mandated recompute rule, `RunInParallel` may change track placement but must not change computed start.

### Impact

- Phase 2 engine remains unambiguous.
- “Parallel” in Phase 2 means structural separation into a parallel track, not earlier-than-sequential execution.

## Decision 12 — Operation: `ReturnToMain`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`
  - **Preconditions:** epic exists
  - **Behavior:**
    1. set `TrackIndex = 0`
    2. revalidate same-track overlap on main lane
    3. do not change `PlannedStartSprintIndex`, `ComputedStartSprintIndex`, or `DurationInSprints`
  - **Output:** updated epic state
  - **Side effects:** none inside the engine
  - **Hard-fail condition:** if imported or pre-existing state causes a main-lane overlap, reject the action

### Rationale

- **INFERRED:** The main lane is the default track and should remain reachable without recalculating roadmap order.

### Impact

- Track movement stays orthogonal to schedule recompute.

## Decision 13 — Operation: `AdjustSpacingBefore`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`, `DeltaSprints`
  - **Preconditions:** epic exists
  - **Behavior:**
    1. `PlannedStartSprintIndex = max(0, PlannedStartSprintIndex + DeltaSprints)`
    2. recompute from the selected epic forward
    3. do not change `TrackIndex`
  - **Output:** updated planning state
  - **Side effects:** none inside the engine

- **PROPOSED (LOCKED):**
  - positive `DeltaSprints` adds spacing
  - negative `DeltaSprints` removes spacing
  - removing spacing never pulls an epic left of the previous epic’s computed end

### Rationale

- **VERIFIED:** Spacing is not currently a first-class entity in the active roadmap model.  
  **Evidence:** Phase 1 report; `ProductRoadmapEditor.razor`.

### Impact

- A single mutation path handles both adding and removing gaps.

## Decision 14 — Operation: `ShiftPlan`

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`, `DeltaSprints`
  - **Preconditions:**
    - epic exists
    - `DeltaSprints > 0`
  - **Behavior:**
    1. add `DeltaSprints` to `PlannedStartSprintIndex` of the selected epic
    2. add `DeltaSprints` to `PlannedStartSprintIndex` of every later roadmap epic
    3. recompute from the selected epic forward
    4. keep all `TrackIndex` values unchanged
  - **Output:** updated suffix planning state
  - **Side effects:** none inside the engine

### Rationale

- **INFERRED:** “Shift plan” must preserve the relative shape of the suffix while moving the schedule to the right.

### Impact

- This is the explicit user-approved right-shift operation.
- It resolves right-side pressure without altering earlier epics.

## Decision 15 — Conflict handling

- **PROPOSED (LOCKED):** The engine never auto-resolves a detected overlap or equivalent hard scheduling conflict.

- **PROPOSED (LOCKED):** When a requested action would leave the target epic interval invalid in the selected track, the engine returns a conflict result containing exactly these allowed next actions:
  1. `RunInParallel`
  2. `ShiftPlan`

- **PROPOSED (LOCKED):** The engine does not apply either action automatically.

### Rationale

- **VERIFIED:** Phase 1 found no existing editable planning engine, so deterministic conflict behavior must be explicit from the start.

### Impact

- User intent stays explicit.
- Engine behavior remains deterministic and testable.

## Decision 16 — Validation model

- **PROPOSED (LOCKED):** Hard constraints are engine-blocking:
  1. epic not found
  2. duplicate `EpicId`
  3. `RoadmapOrder` not contiguous after reorder normalization
  4. `DurationInSprints < 1`
  5. `PlannedStartSprintIndex < 0`
  6. same-track interval overlap after an operation
  7. invalid `TrackIndex < 0`

- **PROPOSED (LOCKED):** Soft signals are warnings only:
  1. duration differs materially from external forecast input, when forecast input is supplied by the caller
  2. duration has no corroborating forecast input
  3. track is non-zero, indicating a parallel-track placement

- **PROPOSED (LOCKED):** If no forecast metadata is supplied to the engine, the engine may emit no duration-related soft signal and still be valid.

### Rationale

- **VERIFIED:** Current repository already distinguishes between blocking correctness and advisory analytics in multiple areas.  
  **Evidence:** Phase 1 report; `ReleasePlanningDtos.cs` validation indicator pattern; current forecast/backlog-health warnings in planning pages.

### Impact

- Core engine invariants remain strict.
- Advisory signals stay optional and integration-friendly.

## Decision 17 — Data boundary

- **PROPOSED (LOCKED):**
  - The engine has no TFS knowledge.
  - The engine uses no TFS field names.
  - The engine performs no HTTP/API work.
  - The engine consumes only normalized internal planning inputs.
  - TFS mapping happens later, outside the engine.

### Rationale

- **VERIFIED:** Repository rules and current layering require business logic to remain isolated from infrastructure.  
  **Evidence:** repository rules; Phase 1 evidence that current TFS mutation paths already live outside the client planning logic.

### Impact

- Phase 2 engine can live in a non-infrastructure layer.
- Phase 3 adapters can translate between engine state and the current TFS-driven planning flow.

## 4. Edge cases explicitly handled

1. **PROPOSED (LOCKED):** First epic with negative requested movement clamps to `0`.
2. **PROPOSED (LOCKED):** Moving an epic left past the previous epic’s end changes intent but not effective computed start.
3. **PROPOSED (LOCKED):** Increasing duration of an earlier epic pushes all later epics through forward recompute.
4. **PROPOSED (LOCKED):** Reordering an epic earlier recomputes from the earliest affected index only.
5. **PROPOSED (LOCKED):** Removing spacing below zero is not allowed; spacing saturates at `0` after recompute.
6. **PROPOSED (LOCKED):** Parallel-track numbering is re-evaluated top-first whenever `RunInParallel` is executed.
7. **PROPOSED (LOCKED):** `ShiftPlan` is right-only; left-shift of a suffix is not part of this operation.
8. **PROPOSED (LOCKED):** If external/imported state already violates same-track overlap, the first mutating operation must fail hard rather than silently normalize it.

## 5. Mapping to Phase 1 findings

| Locked decision | Phase 1 basis | Evidence |
|---|---|---|
| Use Option A, not `ReleasePlanningBoard` | Active planning flow is TFS-driven; separate release-planning model is inactive | Phase 1 report; `ReleasePlanningDtos.cs` |
| PoTool owns planned/computed start and duration | No editable timeline model exists today | `PlanningProjectionDtos.cs`; `RoadmapTimelineLayout.cs` |
| TFS retains order/membership/sprint assignment | Current mutations already use separate work-item endpoints | `WorkItemsController.cs` |
| Integer sprint index | Current real sprint objects exist but are external to editable timeline planning | `SprintDto.cs`; Phase 1 report |
| Forward-only recompute | Current timeline is derived; new engine must add deterministic internal scheduling | Phase 1 report; `RoadmapTimelineLayout.cs` |
| No spacing entities | Current roadmap flow uses implicit positional logic | `ProductRoadmapEditor.razor` |

## 6. Risks remaining

1. **INFERRED:** Under the locked recompute rule, `RunInParallel` is track-only and does not accelerate schedule relative to the previous roadmap epic.
2. **INFERRED:** Because roadmap order remains externally owned, Phase 3 adapter work must keep roadmap-order persistence and engine-order inputs synchronized.
3. **INFERRED:** Multi-product timeline pages will require a new read-model projection of engine-owned planning state because the current pages use forecast-only DTOs.
4. **VERIFIED:** Existing active pages and the inactive `ReleasePlanningBoard` model still coexist in the repository, so implementation scope control remains important.  
   **Evidence:** Phase 1 report.

## 7. Explicit list of what is NOT decided yet

- **UNKNOWN:** Exact persistence schema names for PoTool-owned planning state
- **UNKNOWN:** Exact API contract shape for Phase 3 adapter endpoints
- **UNKNOWN:** Exact UI gestures and control layouts
- **UNKNOWN:** Whether planning dates will ever be mirrored to TFS in a later phase
- **UNKNOWN:** Exact read-model DTO shape for multi-product timeline consumption after engine integration

## 8. Confirmation for next phase

- **PROPOSED (LOCKED):** Phase 3 engine implementation may start with this exact contract:
  - input order already resolved
  - engine owns `PlannedStartSprintIndex`, `ComputedStartSprintIndex`, `DurationInSprints`, `TrackIndex`
  - recompute is forward-only
  - TFS mapping stays outside the engine

## 9. Final section (mandatory)

### Findings

1. **VERIFIED:** The active planning flow is TFS-driven and must remain the outer integration model for Option A.
2. **VERIFIED:** Timeline planning data does not currently exist as editable internal state.
3. **PROPOSED (LOCKED):** The new engine will add PoTool-owned planning state without taking over TFS roadmap order/membership/sprint assignment ownership.
4. **PROPOSED (LOCKED):** The engine is index-based, forward-recomputed, spacing-implicit, and infrastructure-free.

### Decisions (locked)

1. PoTool owns `PlannedStartSprintIndex`, `ComputedStartSprintIndex`, `DurationInSprints`, `TrackIndex`.
2. TFS ownership remains external for roadmap membership, roadmap order, and Plan Board sprint assignment.
3. Planning dates are stored only in PoTool in Phase 2.
4. Main lane is `TrackIndex = 0`; parallel tracks are `1..n`.
5. Sprint representation is zero-based integer index only.
6. Duration is integer, minimum `1`, default `1`.
7. Recompute formula is:
   - first epic: `max(0, plannedStart)`
   - later epic: `max(previousEnd, plannedStart)`
8. Recompute is forward-only and never left-compacts.
9. Spacing is implicit and never stored as a separate entity.
10. `MoveEpicBySprints`, `ReorderEpic`, `RunInParallel`, `ReturnToMain`, `AdjustSpacingBefore`, and `ShiftPlan` are locked exactly as defined above.
11. Hard constraints block; soft signals warn only.
12. Engine has no TFS knowledge.

### Evidence references

- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-1-current-state-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmapEditor.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/WorkItemsController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`

### Risks

- Parallel-track semantics are schedule-neutral under the locked recompute rule.
- Adapter synchronization with external roadmap order will matter.
- New read models will be needed for planning pages that currently consume forecast DTOs only.

### Open questions

- **UNKNOWN:** Exact persistence/API/read-model shapes for the later integration phase.

### GO/NO-GO for Phase 3 (engine implementation)

- **GO**
- **Reason:** The engine boundary, data ownership, core model, recompute rule, operation semantics, validation rules, and explicit non-decisions are now locked and implementation-ready for Option A.
