# Phase 2 Decision-Lock Correction — Option A2

## 1. Summary of the semantic correction

- **VERIFIED:** The current Option A decision lock extends the active TFS-driven planning model and explicitly rejects `ReleasePlanningBoard`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-2-decision-lock-option-a.md`, Decisions 1, 2, 3, and 17.

- **VERIFIED:** The current Option A decision lock also made recompute globally end-based:

```text
start(E[i]) = max(end(E[i-1]), requestedStart(E[i]))
```

  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-2-decision-lock-option-a.md`, Decision 7.

- **VERIFIED:** Under that end-based formula, a later epic cannot start before an earlier epic ends, even when assigned to a parallel track.  
  **Evidence:** current Option A Decision 7 and Decision 11.

- **INFERRED:** That makes parallel tracks visual-only, not real execution tracks.

- **PROPOSED (LOCKED):** Option A2 corrects only the decisions that are inconsistent with real concurrency. All unaffected Option A decisions remain locked and unchanged.

- **PROPOSED (LOCKED):** Under Option A2:
  - parallel tracks represent real concurrent work
  - roadmap priority remains authoritative for start-order
  - roadmap order constrains start-to-start order, not end-to-start sequencing
  - non-overlap remains a per-track rule, not a global rule

## 2. Verified correction basis

1. **VERIFIED:** Phase 1 established that current planning is TFS-driven through roadmap tags, backlog priority, and iteration path.  
   **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-1-current-state-analysis.md`.

2. **VERIFIED:** Phase 1 established that the current timeline is forecast-derived and read-only, so the new planning engine must introduce a new internal scheduling model.  
   **Evidence:** Phase 1 report; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`.

3. **VERIFIED:** Phase 1 established that `ReleasePlanningBoard` is separate from the active planning flow and must not be used for Option A / A2.  
   **Evidence:** Phase 1 report; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`.

4. **VERIFIED:** The current Option A decision lock already fixed the source-of-truth boundary, sprint model, duration ownership, and TFS boundary.  
   **Evidence:** current Option A Decisions 1, 2, 5, 6, and 17.

## 3. Exact list of corrected decisions

## Correction 1 — Parallel-track semantics

- **VERIFIED:** Current Option A Decision 11 defines `RunInParallel` as track assignment only and explicitly states it is not a start-date acceleration operation.  
  **Evidence:** current Option A Decision 11.

- **PROPOSED (LOCKED):** This is corrected.

- **PROPOSED (LOCKED):** Parallel tracks are real execution tracks.

- **PROPOSED (LOCKED):**
  1. A later roadmap epic may start before an earlier roadmap epic ends.
  2. This is valid only when:
     - start-order remains satisfied, and
     - same-track non-overlap remains satisfied.
  3. Parallel tracks are therefore not visual-only lanes.

### What changes

- The old global sequential interpretation is removed.
- A later epic is no longer globally blocked by the earlier epic’s end.

### What remains unchanged

- Roadmap order still matters.
- Main lane is still `TrackIndex = 0`.
- Parallel tracks are still positive integers.
- No overlap is still allowed inside any single track.

### Impact

- Real concurrent execution is now possible within a product.
- Track assignment now has execution meaning, not just rendering meaning.

## Correction 2 — Start-order constraint

- **VERIFIED:** Current Option A Decision 4 already states `ComputedStartSprintIndex(E[i]) >= ComputedStartSprintIndex(E[i-1])`, but current Decision 7 reintroduced a global end-based dependency.  
  **Evidence:** current Option A Decisions 4 and 7.

- **PROPOSED (LOCKED):** The authoritative roadmap-order rule is:

```text
Start(E[i]) >= Start(E[i-1])
```

- **PROPOSED (LOCKED):** The roadmap-order rule does **not** imply:

```text
Start(E[i]) >= End(E[i-1])
```

- **PROPOSED (LOCKED):** An end-based lower bound applies only when both epics compete for the same track through same-track non-overlap.

### What changes

- Global end-to-start sequencing is removed.

### What remains unchanged

- Roadmap order remains authoritative for ordering semantics.
- Start-order is still monotonic across the roadmap sequence.

### Impact

- The model is now consistent with real concurrency and the original start-order intent.

## Correction 3 — Recompute model and exact formulas

- **VERIFIED:** Current Option A Decision 7 is invalid under real concurrency because it uses the previous roadmap epic’s end as a global lower bound.  
  **Evidence:** current Option A Decision 7.

- **PROPOSED (LOCKED):** The engine uses these exact definitions for epic `E[i]` in roadmap order:

```text
RequestedStart(E[i]) = PlannedStartSprintIndex(E[i])
RoadmapStartFloor(E[0]) = 0
RoadmapStartFloor(E[i]) = Start(E[i-1])                      for i > 0
SameTrackPredecessor(E[i]) = nearest earlier E[j] with j < i and Track(E[j]) = Track(E[i])
TrackAvailabilityFloor(E[i]) = 0                             when no SameTrackPredecessor exists
TrackAvailabilityFloor(E[i]) = End(SameTrackPredecessor(E[i]))
Start(E[i]) = max(
  RequestedStart(E[i]),
  RoadmapStartFloor(E[i]),
  TrackAvailabilityFloor(E[i]))
End(E[i]) = Start(E[i]) + DurationInSprints(E[i])            exclusive
```

- **PROPOSED (LOCKED):** Recompute remains forward-only and starts at the earliest changed roadmap index.

- **PROPOSED (LOCKED):** Recompute semantics on the main lane:
  - main lane is just `TrackIndex = 0`
  - because many epics may share track `0`, main-lane recompute often behaves sequentially
  - that sequentiality comes from `TrackAvailabilityFloor`, not from roadmap order alone

- **PROPOSED (LOCKED):** Recompute semantics on parallel tracks:
  - parallel tracks use the same formula
  - a parallel-track epic is constrained by the previous roadmap epic’s **start**
  - it is constrained by the previous same-track epic’s **end**
  - it is **not** constrained by the previous roadmap epic’s end unless that epic is also its same-track predecessor

- **PROPOSED (LOCKED):** No left-compaction still applies:
  - recompute never moves an unchanged epic earlier than its own `RequestedStart`
  - recompute never changes track assignment unless the invoked operation explicitly changes track assignment

### What changes

- The global `previous end` dependency is replaced with:
  - `previous roadmap start`, and
  - `same-track predecessor end`

### What remains unchanged

- Recompute is still forward-only.
- Recompute still starts from the changed roadmap position.
- Planning state is still deterministic and engine-owned.

### Impact

- Main-lane behavior stays stable.
- Parallel tracks now enable true overlap in calendar time while preserving roadmap start-order.

## Correction 4 — Spacing model

- **VERIFIED:** Current Option A Decision 8 defines spacing relative to the previous epic’s end, which is inconsistent with Option A2.  
  **Evidence:** current Option A Decision 8.

- **PROPOSED (LOCKED):** Spacing remains implicit and is still not persisted as a separate entity.

- **PROPOSED (LOCKED):** The controlled spacing concept is corrected to start-based roadmap spacing:

```text
RequestedSpacingBefore(E[0]) = PlannedStartSprintIndex(E[0])
RequestedSpacingBefore(E[i]) = max(0, PlannedStartSprintIndex(E[i]) - Start(E[i-1]))   for i > 0
```

- **PROPOSED (LOCKED):** Effective roadmap spacing is:

```text
EffectiveSpacingBefore(E[0]) = Start(E[0])
EffectiveSpacingBefore(E[i]) = Start(E[i]) - Start(E[i-1])   for i > 0
```

- **PROPOSED (LOCKED):** Same-track visible gap is a separate derived consequence, not a stored planning concept:

```text
TrackGapBefore(E[i]) = Start(E[i]) - End(SameTrackPredecessor(E[i]))   when SameTrackPredecessor exists
```

- **PROPOSED (LOCKED):** If `RequestedStart` is earlier than either the roadmap start floor or the same-track availability floor, the requested spacing is implicitly consumed by recompute.

### What changes

- Spacing is no longer modeled against the previous roadmap epic’s end.

### What remains unchanged

- Spacing is still implicit.
- Spacing is still controlled via epic-level actions.
- Spacing is still not persisted as entities.

### Impact

- Spacing semantics now match real concurrency.

## Correction 5 — `MoveEpicBySprints`

- **VERIFIED:** Current Option A Decision 9 remains structurally valid, but its computed outcome was defined against the old global end-based rule.  
  **Evidence:** current Option A Decision 9.

- **PROPOSED (LOCKED):** `MoveEpicBySprints` is corrected as follows:
  - **Input:** `EpicId`, `DeltaSprints`
  - **Output:** updated planning state and affected suffix range
  - **Side effects inside engine:** none
  - **Behavior:**
    1. `PlannedStartSprintIndex = max(0, PlannedStartSprintIndex + DeltaSprints)`
    2. recompute from the moved epic’s roadmap index forward using the corrected formula
    3. `TrackIndex` remains unchanged
  - **Constraints:**
    - epic must exist
    - `DeltaSprints != 0`

### What changes

- The recompute outcome now depends on:
  - roadmap predecessor start
  - same-track predecessor end

### What remains unchanged

- The operation still edits only requested start.
- The operation still recomputes from the changed point onward.
- The operation still preserves track assignment.

### Impact

- Moving a main-lane epic can shift a suffix.
- Moving a parallel epic can preserve concurrency if its track remains free.

## Correction 6 — `ReorderEpic`

- **VERIFIED:** Current Option A Decision 10 remains structurally valid, but its recompute consequences must use the corrected formula.  
  **Evidence:** current Option A Decision 10.

- **PROPOSED (LOCKED):** `ReorderEpic` remains:
  - **Input:** `EpicId`, `TargetRoadmapOrder`
  - **Output:** reordered and recomputed planning state
  - **Side effects inside engine:** none
  - **Side effects outside engine:** external roadmap-order persistence remains outside the engine

- **PROPOSED (LOCKED):** Corrected behavior:
  1. reorder the roadmap sequence
  2. renumber `RoadmapOrder` contiguously
  3. preserve each epic’s `PlannedStartSprintIndex`, `DurationInSprints`, and `TrackIndex`
  4. recompute from the earliest affected roadmap index forward using the corrected formula

### What changes

- Reorder consequences are now start-based for roadmap dependency and end-based only within the same track.

### What remains unchanged

- Roadmap order is still externally owned outside the engine.
- Reordering still does not auto-change duration or track assignment.

### Impact

- Reordering can change concurrency because roadmap predecessor start floors may change.

## Correction 7 — `RunInParallel`

- **VERIFIED:** Current Option A Decision 11 is invalid under Option A2 because it makes parallel placement visual-only.  
  **Evidence:** current Option A Decision 11.

- **PROPOSED (LOCKED):** `RunInParallel` is corrected as a real execution-track operation.

- **PROPOSED (LOCKED):**
  - **Input:** `EpicId`
  - **Output:** updated planning state and affected suffix range
  - **Side effects inside engine:** none
  - **Behavior:**
    1. compute the epic’s concurrency target floor:

```text
ConcurrencyTargetStart(E[i]) = max(
  PlannedStartSprintIndex(E[i]),
  Start(E[i-1]))     for i > 0

ConcurrencyTargetStart(E[0]) = max(0, PlannedStartSprintIndex(E[0]))
```

    2. choose the lowest positive track index whose `TrackAvailabilityFloor <= ConcurrencyTargetStart`
    3. if no existing positive track satisfies that condition, allocate the next positive track
    4. assign that `TrackIndex`
    5. recompute from the selected epic forward using the corrected formula

- **PROPOSED (LOCKED):** `RunInParallel` may change `ComputedStartSprintIndex`.

- **PROPOSED (LOCKED):** When the epic is moved off the main lane, its computed start may move earlier because the main-lane same-track end constraint is removed.

### What changes

- `RunInParallel` is no longer track-only.
- `RunInParallel` now has real scheduling effect.

### What remains unchanged

- Top-first reuse still applies.
- The operation still does not change requested start or duration.

### Impact

- This operation is now the explicit way to preserve an earlier start when the current track is blocked.

## Correction 8 — `ReturnToMain`

- **VERIFIED:** Current Option A Decision 12 is incomplete under Option A2 because it does not define recompute consequences for a truly concurrent epic returning to the main lane.  
  **Evidence:** current Option A Decision 12.

- **PROPOSED (LOCKED):** `ReturnToMain` is corrected as follows:
  - **Input:** `EpicId`
  - **Output:** updated planning state and affected suffix range
  - **Side effects inside engine:** none
  - **Behavior:**
    1. set `TrackIndex = 0`
    2. recompute from that epic’s roadmap index forward using the corrected formula
  - **Constraints:** epic must exist

- **PROPOSED (LOCKED):** `ReturnToMain` does **not** fail merely because the epic would overlap at its old concurrent start.

- **PROPOSED (LOCKED):** Instead, recompute moves the epic to the earliest main-lane-valid start:

```text
Start(E[i]) = max(
  PlannedStartSprintIndex(E[i]),
  Start(E[i-1]),
  End(MainLaneSameTrackPredecessor(E[i])))
```

- **PROPOSED (LOCKED):** Returning to main may therefore push the epic later and may propagate suffix changes.

### What changes

- Returning to main now has explicit recompute semantics.

### What remains unchanged

- The main lane remains the default track.
- The operation still does not change requested start or duration.

### Impact

- Real concurrency can be removed safely without invalid stored overlap.

## Correction 9 — `AdjustSpacingBefore`

- **VERIFIED:** Current Option A Decision 13 remains structurally valid, but its spacing semantics must be corrected from end-based to start-based.  
  **Evidence:** current Option A Decision 13.

- **PROPOSED (LOCKED):** `AdjustSpacingBefore` is corrected as follows:
  - **Input:** `EpicId`, `DeltaSprints`
  - **Output:** updated planning state and affected suffix range
  - **Side effects inside engine:** none
  - **Behavior:**
    1. `PlannedStartSprintIndex = max(0, PlannedStartSprintIndex + DeltaSprints)`
    2. recompute from the selected epic’s roadmap index forward using the corrected formula
    3. `TrackIndex` remains unchanged

- **PROPOSED (LOCKED):**
  - positive `DeltaSprints` adds requested start-spacing
  - negative `DeltaSprints` removes requested start-spacing
  - reducing requested spacing never violates start-order or same-track non-overlap because recompute applies the corrected floors

### What changes

- Spacing is now interpreted against roadmap predecessor start, not previous end.

### What remains unchanged

- The operation still changes only requested start.
- The operation still does not create spacing entities.

### Impact

- Start-spacing can be adjusted without breaking concurrency semantics.

## Correction 10 — `ShiftPlan`

- **VERIFIED:** Current Option A Decision 14 remains mostly valid, but its recompute semantics must reference the corrected formula.  
  **Evidence:** current Option A Decision 14.

- **PROPOSED (LOCKED):** `ShiftPlan` remains:
  - **Input:** `EpicId`, `DeltaSprints`
  - **Output:** updated suffix planning state
  - **Side effects inside engine:** none
  - **Constraints:** epic must exist and `DeltaSprints > 0`

- **PROPOSED (LOCKED):** Corrected behavior:
  1. add `DeltaSprints` to `PlannedStartSprintIndex` of the selected epic
  2. add `DeltaSprints` to `PlannedStartSprintIndex` of every later roadmap epic
  3. preserve all `TrackIndex` values
  4. recompute from the selected epic forward using the corrected formula

### What changes

- The suffix now remains concurrency-aware after the shift.

### What remains unchanged

- `ShiftPlan` is still right-only.
- `ShiftPlan` still preserves the relative requested-start shape of the suffix.

### Impact

- Bulk right-shifts remain compatible with real parallel execution.

## Correction 11 — Conflict handling

- **VERIFIED:** Current Option A Decision 15 already requires explicit user-facing resolution options and forbids silent auto-resolution.  
  **Evidence:** current Option A Decision 15.

- **PROPOSED (LOCKED):** This remains valid and is clarified.

- **PROPOSED (LOCKED):** The engine never auto-selects:
  - `RunInParallel`
  - `ShiftPlan`

- **PROPOSED (LOCKED):** If a caller requests an earlier effective start that cannot be achieved on the current track without violating same-track non-overlap, the engine must surface conflict guidance rather than silently changing track or bulk-shifting the suffix.

- **PROPOSED (LOCKED):** The allowed alternatives remain exactly:
  1. `RunInParallel`
  2. `ShiftPlan`

- **PROPOSED (LOCKED):** Ordinary recompute on a chosen track is still automatic and deterministic. The conflict is about alternative strategies, not about core recompute itself.

### What changes

- Conflict handling is now explicitly tied to real concurrency semantics.

### What remains unchanged

- The engine still never auto-picks between the two alternative resolution strategies.

### Impact

- The model remains deterministic without making hidden execution decisions for the user.

## Correction 12 — Hard constraints

- **VERIFIED:** Current Option A Decision 16 needs correction because “overlap” must now be interpreted per track, not globally.  
  **Evidence:** current Option A Decision 16.

- **PROPOSED (LOCKED):** Hard constraints are:
  1. epic must exist for all targeted operations
  2. `EpicId` must be unique in engine state
  3. `RoadmapOrder` must be contiguous after normalization
  4. `DurationInSprints >= 1`
  5. `PlannedStartSprintIndex >= 0`
  6. `TrackIndex >= 0`
  7. for every `i > 0`, `Start(E[i]) >= Start(E[i-1])`
  8. for every epic with same-track predecessor `P`, `Start(E[i]) >= End(P)`

- **PROPOSED (LOCKED):** Soft signals remain warnings only, including unrealistic duration signals when forecast metadata is available.

### What changes

- Hard constraints are now explicitly aligned to:
  - global start-order
  - per-track non-overlap

### What remains unchanged

- Track validity remains enforced.
- Duration validity remains enforced.
- Soft signals remain non-blocking.

### Impact

- Validation now matches real concurrency instead of contradicting it.

## 4. Exact list of unchanged decisions

1. **PROPOSED (LOCKED, UNCHANGED):** Extend the active TFS-driven planning model; do not use `ReleasePlanningBoard`.  
   **Evidence:** Phase 1 report; current Option A summary and Decisions 1–3.

2. **PROPOSED (LOCKED, UNCHANGED):** PoTool remains authoritative for:
   - `PlannedStartSprintIndex`
   - `ComputedStartSprintIndex`
   - `DurationInSprints`
   - `TrackIndex`

3. **PROPOSED (LOCKED, UNCHANGED):** TFS remains authoritative for:
   - roadmap membership
   - roadmap order
   - Plan Board sprint assignment

4. **PROPOSED (LOCKED, UNCHANGED):** Planning dates remain stored only in PoTool in this phase.

5. **PROPOSED (LOCKED, UNCHANGED):** Internal sprint representation remains zero-based integer index only; real sprint mapping remains deferred.

6. **PROPOSED (LOCKED, UNCHANGED):** `DurationInSprints` remains integer, minimum `1`, default `1`, and user-controlled.

7. **PROPOSED (LOCKED, UNCHANGED):** There are no anchors, swimlane locks, or persisted spacing entities.

8. **PROPOSED (LOCKED, UNCHANGED):** The engine remains infrastructure-free and has no TFS field knowledge; TFS mapping remains outside the engine.

## 5. Corrected edge cases

1. **PROPOSED (LOCKED):** Main-lane movement causing suffix changes  
   If a main-lane epic’s requested start moves right, later roadmap items may shift because:
   - roadmap predecessor start floors may increase
   - main-lane same-track availability floors may increase

2. **PROPOSED (LOCKED):** Moving an epic into parallel to preserve earlier start  
   If the main lane blocks the epic through same-track occupancy, `RunInParallel` may move it to the lowest reusable positive track that allows the concurrency target start.

3. **PROPOSED (LOCKED):** Returning a parallel epic to main when it would overlap  
   `ReturnToMain` does not fail for overlap alone; recompute moves the epic to the earliest valid main-lane start and then recomputes the suffix.

4. **PROPOSED (LOCKED):** Multiple parallel tracks with top-first reuse  
   `RunInParallel` checks positive tracks in ascending order and chooses the first track that can support the concurrency target start; otherwise it allocates the next track index.

5. **PROPOSED (LOCKED):** Earlier start request blocked by current track  
   If the current track cannot satisfy the requested effective start and the caller wants to preserve that earlier start rather than accept same-track delay, the engine surfaces conflict guidance: `RunInParallel` or `ShiftPlan`.

6. **PROPOSED (LOCKED):** Reordering a parallel epic earlier  
   Reordering changes roadmap predecessor start floors. The epic may keep real concurrency, but its earliest valid start cannot become earlier than its new predecessor’s start.

## 6. Risks remaining

1. **INFERRED:** The corrected model is precise, but Phase 3 will still need a clear adapter contract to expose both requested start and computed start to read models.

2. **INFERRED:** Multi-product planning pages currently consume forecast-oriented DTOs, so an engine-driven planning projection will still be needed later.

3. **INFERRED:** Because roadmap order remains externally owned, integration work must keep TFS order persistence synchronized with engine recompute inputs.

## 7. Explicit list of what is not decided yet

- **UNKNOWN:** Exact persistence schema names for the PoTool-owned planning state
- **UNKNOWN:** Exact API contract shapes for later engine mutation/read endpoints
- **UNKNOWN:** Exact UI gestures that will invoke `MoveEpicBySprints`, `RunInParallel`, `ReturnToMain`, and `ShiftPlan`
- **UNKNOWN:** Whether a later phase will mirror selected planning fields to TFS

## 8. Confirmation that Phase 3 engine implementation can start

- **PROPOSED (LOCKED):** Phase 3 engine implementation may start.

- **PROPOSED (LOCKED):** The corrected implementation contract is now:
  - active TFS-driven planning model remains the outer system
  - roadmap order is a start-order floor, not a global end dependency
  - same-track occupancy is the only source of end-based blocking
  - parallel tracks represent real concurrency
  - recompute remains forward-only and deterministic
  - track changes occur only through explicit operations

## 9. Final section (mandatory)

### Findings

1. **VERIFIED:** The current Option A decision lock contains one critical semantic flaw: global end-based recompute makes parallel tracks visual-only.
2. **VERIFIED:** That flaw is localized to recompute, track semantics, spacing semantics, and the operations that depend on them.
3. **PROPOSED (LOCKED):** Option A2 corrects those decisions while preserving the existing source-of-truth, TFS boundary, sprint model, and duration model.

### Corrected decisions (locked)

1. Parallel tracks are real execution tracks.
2. Roadmap order constrains start-to-start order only.
3. Recompute uses:
   - requested start
   - roadmap predecessor start
   - same-track predecessor end
4. `RunInParallel` may change computed start and is no longer visual-only.
5. `ReturnToMain` recomputes and may push the epic later on the main lane.
6. `MoveEpicBySprints`, `ReorderEpic`, `AdjustSpacingBefore`, and `ShiftPlan` remain valid with corrected concurrency-aware recompute semantics.
7. Hard constraints are per-track non-overlap plus global roadmap start-order.

### Unchanged decisions (still locked)

1. Do not use `ReleasePlanningBoard`.
2. Extend the active TFS-driven planning model.
3. PoTool owns requested start, computed start, duration, and track.
4. TFS owns roadmap membership, roadmap order, and Plan Board sprint assignment.
5. Planning dates remain PoTool-only in this phase.
6. Sprint representation remains integer index.
7. Duration remains integer, default `1`, user-controlled.
8. Engine remains TFS-agnostic and infrastructure-free.

### Evidence references

- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-1-current-state-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-18-phase-2-decision-lock-option-a.md`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/PlanningProjectionDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/ReleasePlanning/ReleasePlanningDtos.cs`

### Risks

- Phase 3 still needs a read-model contract that exposes both requested and computed planning state.
- Future UX will need to decide when to accept same-track delay automatically versus when to surface conflict guidance.
- Integration must keep externally persisted roadmap order aligned with engine inputs.

### Open questions

- **UNKNOWN:** Exact persistence/API/read-model shapes for later implementation phases.

### GO/NO-GO for Phase 3 engine implementation

- **GO**
- **Reason:** Real concurrency behavior, recompute formulas, track semantics, corrected operations, and corrected hard constraints are now explicitly locked without ambiguity.
