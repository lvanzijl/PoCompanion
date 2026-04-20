# Phase 4 Read Model + Application Layer Bridge

## 1. Summary

- **IMPLEMENTED:** Added a stateless application-layer bridge that bootstraps active product/work-item inputs into the existing planning engine and returns a product planning board read model.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **VERIFIED:** The planning engine itself stayed infrastructure-free and unchanged for this phase.  
  **Evidence:** only `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs` changed; no files under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/` changed.

- **VERIFIED:** The bridge is deterministic, in-memory, and suitable for future higher-layer callers without adding persistence, controllers, or UI components.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`; `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`.

## 2. Chosen application-layer location and rationale

- **IMPLEMENTED:** Placed the orchestration service in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.

- **VERIFIED:** This location is correct because `PoTool.Core` already owns application-layer orchestration that composes Core contracts with domain services without depending on infrastructure implementation details.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; existing application-service precedent in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/BacklogQuality/BacklogQualityAnalysisService.cs`.

- **VERIFIED:** Engine code did not need to move because the new service only consumes the existing domain engine through `PlanningRecomputeService` and `PlanningOperationService`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`; `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`; `ProductPlanningBoardServiceTests.ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape`.

- **VERIFIED:** Persistence was intentionally not introduced because the bootstrap rule rebuilds current planning state from active product/work-item inputs on every call, which satisfies the phase goal without schema or repository changes.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`.

## 3. Files added/changed

### Added

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-4-read-model-application-layer.md`

### Changed

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`

## 4. Read-model contracts added

- **IMPLEMENTED:** `ProductPlanningBoardDto` for a single product planning board result, including top-level issues plus changed/affected epic ids.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`.

- **IMPLEMENTED:** `PlanningBoardTrackDto` to represent the main lane and parallel tracks without UI-specific styling.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`.

- **IMPLEMENTED:** `PlanningBoardEpicItemDto` to surface:
  - `EpicId`
  - `EpicTitle`
  - `RoadmapOrder`
  - `TrackIndex`
  - `PlannedStartSprintIndex`
  - `ComputedStartSprintIndex`
  - `DurationInSprints`
  - `EndSprintIndexExclusive`
  - per-epic issues
  - changed/affected flags  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_MapsRoadmapOrderAndDerivedEnds`.

- **IMPLEMENTED:** `PlanningBoardIssueDto` to carry engine validation issues through the application layer without UI-specific interpretation.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`.

## 5. Bootstrap rule implemented

- **IMPLEMENTED:** The bootstrap strategy is:
  1. load the product through `IProductRepository`
  2. load active work items through `IWorkItemReadProvider.GetByRootIdsAsync`
  3. keep only roadmap epics (`Epic` + `roadmap` tag)
  4. order them by `BacklogPriority`, then `TfsId`
  5. assign contiguous `RoadmapOrder`
  6. set `PlannedStartSprintIndex` to the zero-based roadmap sequence
  7. default `DurationInSprints` to `1`
  8. place every epic on `TrackIndex = 0`
  9. derive `ComputedStartSprintIndex` through the existing engine recompute service  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_MapsRoadmapOrderAndDerivedEnds`.

- **VERIFIED:** The bootstrap is deterministic and does not depend on persistence or TFS date-field mapping.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`; `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`.

## 6. Orchestration methods implemented

- **IMPLEMENTED:** `BuildPlanningBoardAsync` and `GetPlanningBoardAsync`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull`.

- **IMPLEMENTED:** `ExecuteMoveEpicBySprintsAsync` and `ExecuteAdjustSpacingBeforeAsync`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`.

- **IMPLEMENTED:** `ExecuteRunInParallelAsync` and `ExecuteReturnToMainAsync`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`.

- **IMPLEMENTED:** `ExecuteReorderEpicAsync` and `ExecuteShiftPlanAsync`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape`.

- **IMPLEMENTED:** API DI registration for future callers through the existing composition root.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`; `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --no-restore`.

## 7. Tests added

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`  
  Verifies deterministic bootstrap, main-lane-only initialization, and default duration `1`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_MapsRoadmapOrderAndDerivedEnds`  
  Verifies mapping from active roadmap inputs into roadmap order, planned starts, computed starts, and derived end sprint.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`  
  Verifies operation execution, changed/affected propagation, and validation issue surfacing.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`  
  Verifies non-main track output, concurrent start surfacing, and return-to-main idempotence under stateless bootstrap.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape`  
  Verifies reorder and shift behavior through the application layer.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`  
  Verifies repeated execution stays deterministic.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

- **IMPLEMENTED:** `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull`  
  Verifies product-missing handling without introducing persistence or controller behavior.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

## 8. Verified preserved engine semantics

- **VERIFIED:** The application layer delegates to the existing engine’s recompute and operation services instead of re-implementing scheduling logic.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`; `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`; `ProductPlanningBoardServiceTests.ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape`.

- **VERIFIED:** Engine validation issues, changed ids, and affected ids are preserved in the read model.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`.

- **VERIFIED:** The phase did not introduce a second planning engine, persistence placeholders, controller endpoints, or UI-specific payloads.  
  **Evidence:** changed implementation files are limited to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`; `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`.

## 9. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** persistence, repositories for planning state, EF entities, migrations, API controllers/endpoints, UI pages/components, TFS date-field mapping, undo/redo persistence, and ReleasePlanningBoard integration.  
  **Evidence:** no changed files under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/`, or `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/`.

- **NOT IMPLEMENTED:** optional forecast metadata on the planning board read model. This phase intentionally stayed on the active product/work-item input contracts and did not add forecast-specific persistence coupling.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.

- **NOT IMPLEMENTED:** cross-call accumulation of operations. Every operation rebuilds the board from the same active inputs because the phase is intentionally stateless and in-memory only.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`; `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`.

## 10. Risks or blockers

- **BLOCKER:** Repository-wide solution build remains red because of unrelated pre-existing `PoTool.Tests.Unit` compile failures outside this phase.  
  **Evidence:** `/tmp/copilot-tool-output-1776587087505-mrpvmf.txt`, including failures in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Adapters/StateClassificationInputMapperTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs`.

- **VERIFIED:** Targeted builds and tests for the changed planning bridge pass cleanly.  
  **Evidence:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`; `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --no-restore`.

## 11. Recommendation for next phase

- **VERIFIED:** The next phase can add a higher-layer entry point or persistence-backed session state on top of this bridge without redesigning the engine, because the current phase already provides deterministic bootstrapping, read-model projection, and in-memory operation execution.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`; `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`.

## Final section

### IMPLEMENTED

- product planning board read-model DTOs
- Core application-layer orchestration service
- deterministic no-persistence bootstrap from active product/work-item inputs
- in-memory execution for all existing planning engine operations
- issue and changed/affected propagation into the read model
- API DI registration for the new bridge
- focused tests for bootstrap, mapping, orchestration, parallel behavior, reorder/shift behavior, and determinism

### NOT IMPLEMENTED

- persistence/session state for planning changes
- API controllers/endpoints
- UI pages/components
- planning-state database schema or repositories
- TFS date-field mapping
- forecast metadata on the read model
- ReleasePlanningBoard integration

### BLOCKERS

- unrelated pre-existing `PoTool.Tests.Unit` compile failures still block a clean solution-wide build

### Evidence (files/tests)

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
- `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_BootstrapsDeterministicallyOnMainLaneWithDefaultDuration`
- `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_MapsRoadmapOrderAndDerivedEnds`
- `ProductPlanningBoardServiceTests.ExecuteMoveAndAdjustSpacingBeforeAsync_PropagateChangedAffectedAndValidation`
- `ProductPlanningBoardServiceTests.ExecuteRunInParallelAndReturnToMainAsync_SurfaceTrackChangesInReadModel`
- `ProductPlanningBoardServiceTests.ExecuteReorderAndShiftPlanAsync_ReturnUpdatedRoadmapOrderAndSuffixShape`
- `ProductPlanningBoardServiceTests.ExecuteOperations_WithSameInputs_RemainDeterministic`
- `ProductPlanningBoardServiceTests.BuildPlanningBoardAsync_WhenProductMissing_ReturnsNull`

### GO/NO-GO for next phase

- **GO** for the next phase, with the unchanged caveat that solution-wide baseline failures still live in unrelated `PoTool.Tests.Unit` code outside this slice.
