# Phase 3 Engine Slice 1

## 1. Summary

- **IMPLEMENTED:** Slice 1 adds a pure planning-engine domain layer for recompute, hard-constraint validation, and explicit track operations.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningRecomputeService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.

- **VERIFIED:** The slice remains infrastructure-free and TFS-agnostic.  
  **Evidence:** the added planning files above contain only domain types, pure services, and validation logic; no API, persistence, UI, or TFS dependencies were introduced.

- **VERIFIED:** The slice is covered by MSTest unit tests for recompute formulas, forward-only suffix recompute, main-lane sequencing, parallel-track reuse, return-to-main delay, and invalid-input rejection.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

## 2. Engine location and rationale

- **IMPLEMENTED:** The engine was placed in `PoTool.Core.Domain/Domain/Planning`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`.

- **VERIFIED:** This is the correct placement for the locked design because the slice is:
  - pure business logic
  - deterministic
  - infrastructure-free
  - independent from API, persistence, UI, and TFS mapping  
  **Evidence:** existing repository structure under `PoTool.Core.Domain/Domain/*`; new planning files in `PoTool.Core.Domain/Domain/Planning/*`.

## 3. Files added/changed

### Added

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningRecomputeService.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningStateOrdering.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`

### Changed

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`  
  Added the domain project reference required to compile the new tests.

### Removed

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/Test1.cs`  
  Removed the empty placeholder test in favor of real slice coverage.

## 4. Model implemented

- **IMPLEMENTED:** `PlanningEpicState` with:
  - `EpicId`
  - `RoadmapOrder`
  - `PlannedStartSprintIndex`
  - `ComputedStartSprintIndex`
  - `DurationInSprints`
  - `TrackIndex`
  - derived `EndSprintIndexExclusive`  
  **Evidence:** `PlanningModels.cs`.

- **IMPLEMENTED:** `PlanningState` as the headless roadmap scope container.  
  **Evidence:** `PlanningModels.cs`.

- **IMPLEMENTED:** `PlanningOperationResult` exposing:
  - `State`
  - `ChangedEpicIds`
  - `AffectedEpicIds`
  - `ValidationIssues`  
  **Evidence:** `PlanningModels.cs`.

- **IMPLEMENTED:** `PlanningValidationIssue` and `PlanningValidationIssueCode` for stable hard-constraint reporting.  
  **Evidence:** `PlanningModels.cs`.

## 5. Operations implemented

- **IMPLEMENTED:** `MoveEpicBySprints`  
  Updates planned start, clamps at zero, recomputes from the changed roadmap index, and returns change/affected sets.  
  **Evidence:** `PlanningOperationService.cs`.

- **IMPLEMENTED:** `AdjustSpacingBefore`  
  Uses the same planned-start mutation structure as `MoveEpicBySprints`, preserving the locked “no spacing entities” decision.  
  **Evidence:** `PlanningOperationService.cs`.

- **IMPLEMENTED:** `RunInParallel`  
  Assigns the lowest valid positive track using top-first reuse and recomputes the suffix, allowing computed start to change when main-lane blocking is removed.  
  **Evidence:** `PlanningOperationService.cs`; verified by `PlanningOperationServiceTests.RunInParallel_AllowsCrossTrackOverlapAndReusesLowestTrack`.

- **IMPLEMENTED:** `ReturnToMain`  
  Sets `TrackIndex = 0`, recomputes the suffix, and delays the epic on the main lane when required instead of failing on overlap.  
  **Evidence:** `PlanningOperationService.cs`; verified by `PlanningOperationServiceTests.ReturnToMain_DelaysEpicUntilMainLaneIsAvailable`.

## 6. Tests added

- **IMPLEMENTED:** `RecomputeFrom_UsesPlannedRoadmapAndTrackFloors`  
  Verifies the locked `max(plannedStart, roadmapStartFloor, trackAvailability)` formula.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `RecomputeFrom_PreservesEarlierPrefixWhenRecomputingSuffix`  
  Verifies forward-only recompute from the changed index.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `RecomputeFrom_MainLaneStaysSequentialBecauseTrackCannotOverlap`  
  Verifies main-lane sequential behavior arises from same-track non-overlap.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `RunInParallel_AllowsCrossTrackOverlapAndReusesLowestTrack`  
  Verifies real concurrency across tracks and top-first track reuse.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ReturnToMain_DelaysEpicUntilMainLaneIsAvailable`  
  Verifies return-to-main recompute behavior.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `Validation_RejectsInvalidInputsAndConstraintViolations`  
  Verifies duplicate IDs, invalid duration, negative planned start, invalid track, invalid roadmap order, start-order violation, and same-track overlap.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `MoveEpicBySprints_RejectsUnknownEpic`  
  Verifies deterministic invalid-input rejection for operations.  
  **Evidence:** `PlanningOperationServiceTests.cs`.

## 7. Verified rules

- **VERIFIED:** Recompute uses all three locked floors:
  - planned start
  - roadmap predecessor start
  - same-track predecessor end  
  **Evidence:** `PlanningRecomputeService.cs`; `PlanningOperationServiceTests.RecomputeFrom_UsesPlannedRoadmapAndTrackFloors`.

- **VERIFIED:** Recompute is forward-only from the changed roadmap index and leaves the earlier prefix unchanged.  
  **Evidence:** `PlanningRecomputeService.cs`; `PlanningOperationServiceTests.RecomputeFrom_PreservesEarlierPrefixWhenRecomputingSuffix`.

- **VERIFIED:** Main-lane sequentiality is enforced by same-track non-overlap, not by a global end-based roadmap rule.  
  **Evidence:** `PlanningRecomputeService.cs`; `PlanningOperationServiceTests.RecomputeFrom_MainLaneStaysSequentialBecauseTrackCannotOverlap`.

- **VERIFIED:** Parallel tracks behave as real execution tracks and may enable an earlier computed start than the main lane.  
  **Evidence:** `PlanningOperationService.cs`; `PlanningOperationServiceTests.RunInParallel_AllowsCrossTrackOverlapAndReusesLowestTrack`.

- **VERIFIED:** Hard constraints cover duplicate epic IDs, invalid duration, negative planned start, invalid track, roadmap-order consistency, roadmap start-order, and same-track overlap.  
  **Evidence:** `PlanningValidationService.cs`; `PlanningOperationServiceTests.Validation_RejectsInvalidInputsAndConstraintViolations`.

## 8. Known gaps

- **NOT IMPLEMENTED:** `ReorderEpic`  
  This slice explicitly excluded it.  
  **Evidence:** no implementation added outside the four scoped operations.

- **NOT IMPLEMENTED:** `ShiftPlan`  
  This slice explicitly excluded it.  
  **Evidence:** no implementation added outside the four scoped operations.

- **NOT IMPLEMENTED:** persistence, repositories, API endpoints, UI integration, TFS mapping, and forecast integration.  
  **Evidence:** all added files are under `PoTool.Core.Domain` and `PoTool.Core.Domain.Tests` only.

## 9. Risks

- **VERIFIED:** Baseline repository-wide build/test remains blocked by unrelated existing `PoTool.Tests.Unit` failures.  
  **Evidence:** baseline `dotnet build PoTool.sln --no-restore` failed in `PoTool.Tests.Unit` before this slice due missing/inaccessible members such as `MockTfsClient`, `ResolveAncestry`, and mapping extension methods.

- **INFERRED:** Later slices must preserve the current result/validation model when adding `ReorderEpic` and `ShiftPlan` so operation behavior stays consistent.

- **INFERRED:** Adapter layers will still need a dedicated read/write contract for requested versus computed start once integration work begins.

## 10. Recommendation for next slice

- **PROPOSED:** Implement `ReorderEpic` and `ShiftPlan` next, reusing the same recompute and validation services instead of adding parallel scheduling logic elsewhere.

## 11. Final section (mandatory)

### IMPLEMENTED

- Core planning engine types
- Deterministic recompute service
- Hard-constraint validation service
- `MoveEpicBySprints`
- `AdjustSpacingBefore`
- `RunInParallel`
- `ReturnToMain`
- Result/diff model
- MSTest coverage for the locked slice

### NOT IMPLEMENTED

- `ReorderEpic`
- `ShiftPlan`
- persistence
- API
- UI
- TFS mapping
- undo/redo
- forecast integration beyond placeholders

### BLOCKERS

- **BLOCKER:** Repository-wide baseline remains red because `PoTool.Tests.Unit` already contains unrelated compile failures.  
  **Evidence:** baseline solution build output before this slice.

- **VERIFIED:** This blocker did not prevent clean implementation of the scoped domain slice because the targeted project and targeted tests build and pass.  
  **Evidence:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`.

### Evidence (files/tests)

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningRecomputeService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningStateOrdering.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`

### GO/NO-GO for next slice

- **GO** for the next engine slice.
- **Reason:** the scoped domain slice is implemented, deterministic, tested, and isolated from the unrelated baseline failures outside `PoTool.Core.Domain`.
