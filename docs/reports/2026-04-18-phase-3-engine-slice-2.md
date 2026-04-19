# Phase 3 Engine Slice 2

## 1. Summary

- **IMPLEMENTED:** Added the remaining locked shape-changing engine operations, `ReorderEpic` and `ShiftPlan`, by extending the existing slice-1 planning engine in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** Slice 2 preserved the slice-1 engine architecture, validation model, recompute model, and result model.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningRecomputeService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** The slice remains infrastructure-free and TFS-agnostic.  
  **Evidence:** all slice-2 code changes are confined to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

## 2. Files added/changed

### Changed

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`

### Added

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-3-engine-slice-2.md`

## 3. ReorderEpic implementation details

- **IMPLEMENTED:** `ReorderEpic(PlanningState state, int epicId, int targetRoadmapOrder)` reuses the existing operation pattern:
  - validate current state first
  - reject unknown epic
  - reject out-of-bounds target order
  - reorder only the roadmap sequence
  - renumber `RoadmapOrder` contiguously
  - preserve `PlannedStartSprintIndex`, `DurationInSprints`, and `TrackIndex`
  - recompute from `min(oldIndex, newIndex)` forward
  - return `State`, `ChangedEpicIds`, `AffectedEpicIds`, and `ValidationIssues`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.

- **VERIFIED:** Reordering can change computed starts through new roadmap predecessor floors without changing track assignment.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`:
  - `ReorderEpic_MovingEarlier_RenumbersContiguouslyAndPreservesTrackAndDuration`
  - `ReorderEpic_MovingLater_RecomputesFromEarliestAffectedIndex`
  - `ReorderEpic_ParallelEpicStillRespectsNewRoadmapPredecessorAndSameTrackRules`

## 4. ShiftPlan implementation details

- **IMPLEMENTED:** `ShiftPlan(PlanningState state, int epicId, int deltaSprints)` reuses the same slice-1 operation/result flow:
  - validate current state first
  - reject unknown epic
  - reject non-positive delta
  - add `deltaSprints` to the selected epic and every later roadmap epic
  - preserve all `TrackIndex` values
  - recompute from the selected roadmap index forward
  - return the unchanged slice-1 result model  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.

- **VERIFIED:** `ShiftPlan` is right-only, leaves earlier epics untouched, and preserves the requested-start shape of the shifted suffix.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`:
  - `ShiftPlan_ShiftsSuffixPreservesEarlierEpicsTracksAndRelativeRequestedShape`
  - `ShiftPlan_RejectsUnknownEpicAndNonPositiveDelta`

## 5. Tests added

- **IMPLEMENTED:** `ReorderEpic_MovingEarlier_RenumbersContiguouslyAndPreservesTrackAndDuration`  
  Verifies earlier reorder, contiguous renumbering, unchanged duration/track assignment, and affected-id reporting.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ReorderEpic_MovingLater_RecomputesFromEarliestAffectedIndex`  
  Verifies later reorder and forward recompute from the earliest affected index.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ReorderEpic_ParallelEpicStillRespectsNewRoadmapPredecessorAndSameTrackRules`  
  Verifies reorder + parallel interaction, roadmap start floors, and same-track non-overlap.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ShiftPlan_ShiftsSuffixPreservesEarlierEpicsTracksAndRelativeRequestedShape`  
  Verifies suffix-only right shift, earlier epic stability, track preservation, preserved planned-start deltas, and recompute behavior.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ReorderEpic_RejectsUnknownEpicAndInvalidTargetOrder`  
  Verifies operation error handling for unknown epic and invalid target order.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `ShiftPlan_RejectsUnknownEpicAndNonPositiveDelta`  
  Verifies operation error handling for unknown epic and invalid `delta <= 0`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **IMPLEMENTED:** `MixedScenario_Slice1AndSlice2OperationsPreserveFinalInvariants`  
  Verifies a mixed regression flow using move, parallel, reorder, and shift together, then checks final invariants.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

## 6. Verified locked rules

- **VERIFIED:** Slice 1 semantics remain intact; slice 2 extends the same immutable/state-returning operation style instead of introducing a second pattern.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** Recompute remains forward-only from the changed index for both new operations.  
  **Evidence:** `ReorderEpic` and `ShiftPlan` call the shared finalize path in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; verified by `ReorderEpic_MovingLater_RecomputesFromEarliestAffectedIndex` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** No track assignment is changed by `ReorderEpic` or `ShiftPlan`; only explicit track operations continue to change tracks.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; verified by `ReorderEpic_MovingEarlier_RenumbersContiguouslyAndPreservesTrackAndDuration` and `ShiftPlan_ShiftsSuffixPreservesEarlierEpicsTracksAndRelativeRequestedShape` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** Same-track non-overlap and roadmap start-order constraints remain enforced after reorder and shift.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`; `ReorderEpic_ParallelEpicStillRespectsNewRoadmapPredecessorAndSameTrackRules`; `MixedScenario_Slice1AndSlice2OperationsPreserveFinalInvariants` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

- **VERIFIED:** The result model was reused without extension.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.

## 7. Known gaps intentionally left for later slices

- **NOT IMPLEMENTED:** persistence, repositories, API endpoints, UI integration, TFS mapping, forecast integration, and undo/redo.  
  **Evidence:** no slice-2 changes outside `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/**`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/**`, and this report.

- **NOT IMPLEMENTED:** any new engine concepts beyond the locked slice-2 operations.  
  **Evidence:** slice-2 changes only add `ReorderEpic` and `ShiftPlan` to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`.

## 8. Risks or blockers

- **BLOCKER:** Repository-wide solution build remains blocked by unrelated pre-existing `PoTool.Tests.Unit` compile failures.  
  **Evidence:** `/tmp/copilot-tool-output-1776574396655-x1w6e6.txt`, including errors in:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Adapters/StateClassificationInputMapperTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs`

- **VERIFIED:** The blocker does not prevent this slice from being implemented cleanly because the targeted planning domain build and tests pass.  
  **Evidence:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`.

## 9. Recommendation for next slice

- **VERIFIED:** The engine now contains the locked operations from slices 1 and 2. The next slice should stay outside the engine core unless the next locked phase explicitly requires integration work.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`.

## Final section

### IMPLEMENTED

- `ReorderEpic`
- `ShiftPlan`
- focused MSTest coverage for reorder, shift, error handling, parallel interaction, and mixed regression flow
- reuse of the existing slice-1 result/diff model

### NOT IMPLEMENTED

- persistence
- API
- UI
- TFS mapping
- forecast integration
- undo/redo
- any new engine concepts beyond the locked slice-2 scope

### BLOCKERS

- repository-wide solution baseline remains red because of unrelated existing `PoTool.Tests.Unit` compile failures

### Evidence (files/tests)

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningOperationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningRecomputeService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/PlanningValidationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PlanningOperationServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-3-engine-slice-2.md`

### GO/NO-GO for next slice

- **GO** for the next locked slice, because the engine operations requested in slice 2 are implemented and the targeted planning test project passes.
