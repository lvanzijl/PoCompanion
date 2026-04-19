# Phase 10 — Operational hardening, diagnostics, and drift handling

## 1. Summary

- **IMPLEMENTED:** Added explicit operational diagnostics and drift classification to the existing product planning board read model without changing the planning engine, persistence shape, session model, or endpoint family.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **IMPLEMENTED:** Surfaced recovery, normalization, drift, and calendar blocker state through the existing API/controller and the existing `/planning/plan-board` UI route.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`

- **IMPLEMENTED:** Added focused automated coverage for drift detection, recovery diagnostics, blocker surfacing, and client/render-model handling.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`

## 2. Current operational gap addressed

- **VERIFIED:** Before this phase, internal planning intent could drift from current TFS `StartDate` / `TargetDate` after a prior successful write-back, and that stale projection was not surfaced unless a new mutation or recovery path ran. Persisted intent still won operationally, but the board did not tell the product owner that current TFS projected dates had gone stale.  
  **Evidence:** prior persisted-intent read path in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **VERIFIED:** Recovery failure causes such as legacy-invalid dates, insufficient future sprint coverage, and calendar ambiguity were either silently ignored on bootstrap/recovery or surfaced only as exceptions in narrower paths.  
  **Evidence:** recovery and calendar logic in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

## 3. Files added/changed

- **IMPLEMENTED:** Updated shared planning DTO contracts  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`

- **IMPLEMENTED:** Updated planning intent store enum ownership  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningIntentStore.cs`

- **IMPLEMENTED:** Updated application-layer planning bridge  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **IMPLEMENTED:** Updated API/controller surfacing  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductPlanningIntentEntity.cs`

- **IMPLEMENTED:** Updated client/service/rendering  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`

- **IMPLEMENTED:** Updated tests and release notes  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 4. Drift detection implemented

- **IMPLEMENTED:** Added explicit drift status classification for active in-scope epics with internal intent:
  - `NoDrift`
  - `MissingTfsDates`
  - `TfsProjectionMismatch`
  - `LegacyInvalidTfsDates`
  - `CalendarResolutionFailure`
  - `InsufficientFutureSprintCoverage`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **IMPLEMENTED:** Drift compares the canonical forward projection derived from internal intent against the current work-item `StartDate` / `TargetDate` read model and does not alter planning behavior.  
  **Evidence:** `DetermineDrift(...)` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **VERIFIED:** Drift coverage tests
  - internal intent matches projected TFS dates → `NoDrift`
  - internal intent differs from projected TFS dates → `TfsProjectionMismatch`
  - internal intent exists but TFS dates are missing → `MissingTfsDates`
  - internal intent exceeds available calendar coverage → `InsufficientFutureSprintCoverage`  
  **Evidence:** `BuildPlanningBoardAsync_PersistedIntentMatchingTfsDates_SurfacesNoDrift`, `BuildPlanningBoardAsync_PersistedIntentWinsOverDifferingTfsDates`, `BuildPlanningBoardAsync_PersistedIntentWithMissingTfsDates_SurfacesMissingDatesDrift`, `BuildPlanningBoardAsync_PersistedIntentBeyondCalendar_SurfacesCoverageDiagnostic` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`

## 5. Diagnostics model implemented

- **IMPLEMENTED:** Added machine-readable diagnostics via `PlanningBoardDiagnosticDto` and explicit epic metadata for:
  - intent source (`Bootstrap`, `Authored`, `Recovered`)
  - recovery status
  - drift status
  - reconciliation availability flag  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`

- **IMPLEMENTED:** Added diagnostics for:
  - `RecoveredExact`
  - `RecoveredWithNormalization`
  - `RecoveryFailed`
  - stale TFS projection while internal intent exists
  - canonical sprint calendar failure
  - insufficient future sprint coverage
  - invalid legacy dates ignored by the cutoff rule  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **VERIFIED:** Recovery and legacy-date coverage tests  
  **Evidence:** `BuildPlanningBoardAsync_RecoveryWithNormalization_PersistsIntentAndRewritesTfsDates`, `BuildPlanningBoardAsync_LegacyInvalidDates_AreSurfacedAndIgnored`, `BuildPlanningBoardAsync_AmbiguousCalendarWithoutPersistedIntent_SurfacesBlockingDiagnostic` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`

## 6. Reconciliation behavior implemented or explicitly deferred

- **NOT IMPLEMENTED:** An explicit reconciliation action that rewrites stale TFS dates from internal intent was not added in this phase.

- **BLOCKER:** Adding an explicit reconciliation action cleanly would require changing the locked planning API surface with a new mutation endpoint or overloading an existing action with non-equivalent semantics. This phase preserved the existing endpoint family exactly and therefore implemented detection/surfacing only.  
  **Evidence:** existing preserved controller surface in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`

## 7. API/application/UI surfacing implemented

- **IMPLEMENTED:** Application-layer surfacing now attaches operational diagnostics and intent/drift metadata to the existing board read model.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **IMPLEMENTED:** The API controller now converts unrecoverable operational blockers (for example ambiguous calendar + persisted intent) into an explicit `409 Conflict` payload with a readable message instead of an opaque `500`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`

- **IMPLEMENTED:** The client service now extracts `message` / `detail` / `title` from non-success JSON responses so blocker messaging is readable in the existing page.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`

- **IMPLEMENTED:** The existing `/planning/plan-board` UI now shows:
  - recovered epic counts
  - drifted epic counts
  - board-level operational diagnostics
  - per-epic recovery/drift chips
  - per-epic diagnostic alerts
  - blocking operational status in the board header  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/ProductPlanningBoardRenderModel.cs`

## 8. Tests added

- **IMPLEMENTED:** Added/extended API-side service tests for drift, recovery, legacy dates, and calendar blockers.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`

- **IMPLEMENTED:** Added/extended controller tests for conflict/blocker surfacing.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`

- **IMPLEMENTED:** Added/extended client/render-model tests for blocker-message parsing and operational diagnostic summary behavior.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`

- **VERIFIED:** Executed validation:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --no-restore`  
  **Evidence:** successful local runs during implementation

## 9. Verified preserved planning semantics

- **VERIFIED:** Planning engine behavior was not redesigned; drift detection is read-only and does not alter recompute semantics.  
  **Evidence:** no changes under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/**`

- **VERIFIED:** Persistence shape was not changed.  
  **Evidence:** no new planning persistence entity fields or migrations; only namespace ownership changed for the shared recovery-status enum in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductPlanningIntentEntity.cs`

- **VERIFIED:** No new TFS fields were introduced; diagnostics rely on the existing `StartDate` / `TargetDate` projection only.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`

- **VERIFIED:** Existing planning UI route and explicit interaction model were preserved.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`

## 10. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** Explicit reconcile/resync mutation endpoint for stale TFS projections.
- **NOT IMPLEMENTED:** Full browser-level UI automation; strongest feasible coverage remains service/controller/render-model level under current repo conventions.
- **NOT IMPLEMENTED:** Recovery of boards with persisted internal intent when the canonical calendar is blocked on first load without a previously valid session; the API now surfaces a clear conflict message instead of silently guessing.

## 11. Risks or blockers

- **BLOCKER:** Explicit reconciliation was deferred because preserving the locked API surface prevented adding a clean dedicated resync action in this phase.

- **VERIFIED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` still has a pre-existing NuGet restore issue unrelated to this phase, so executable automated coverage for this work remains concentrated in `PoTool.Api.Tests`.  
  **Evidence:** baseline restore failure during phase setup

- **VERIFIED:** Parallel client builds can still hit transient Blazor static-asset file locks; sequential client builds succeed.  
  **Evidence:** successful sequential `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --no-restore`

## 12. Recommendation for next phase

- **RECOMMENDATION:** If the locked API surface can be expanded in a later phase, add one explicit “reconcile TFS projection from internal intent” mutation that is only enabled when drift is present and leaves internal intent unchanged.

## Final section

### IMPLEMENTED

- Drift detection for authored/recovered in-scope epics
- Recovery, normalization, legacy-date, calendar, and coverage diagnostics
- Existing API/controller surfacing for readable operational blockers
- Existing UI surfacing for recovery/drift/blocker state
- Focused automated coverage for the new operational behavior

### NOT IMPLEMENTED

- Explicit reconciliation mutation/entry point
- Broad UI redesign
- New endpoint family
- New persistence fields or TFS fields

### BLOCKERS

- Explicit reconciliation was deferred to preserve the locked existing planning API surface

### Evidence (files/tests)

- **Core/shared/client/api files:** all files listed in sections 3–8
- **Executed tests/builds:** all commands listed in section 8

### GO/NO-GO for next phase

- **GO:** operational drift/recovery diagnostics are now visible and test-covered without changing the locked planning semantics.
