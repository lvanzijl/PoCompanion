# Phase 11 explicit reconciliation endpoint and UI

## 1. Summary

- **VERIFIED:** The existing planning-board implementation already preserved the locked design basis in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.
- **IMPLEMENTED:** Added one explicit reconciliation mutation on the existing product planning board surface so stale TFS planning dates can be rewritten from existing internal intent without changing internal intent.
- **IMPLEMENTED:** Added one visible per-epic UI action on `/planning/plan-board` that appears only when the board already marks an epic as drifted and reconcilable.
- **VERIFIED:** The focused planning test suites passed after the change:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` → Passed: 41 / Failed: 0
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj` → Passed: 33 / Failed: 0
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` → Passed: 2088 / Failed: 0
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.sln` → Build succeeded, 0 warnings, 0 errors

## 2. Chosen reconciliation surface and rationale

- **IMPLEMENTED:** Reused the existing controller family at `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs` and added:
  - `POST /api/products/{productId}/planning-board/reconcile`
- **VERIFIED:** This is the smallest consistent API extension because the controller already owns all explicit plan-board mutations (`move`, `adjust-spacing`, `run-in-parallel`, `return-to-main`, `reorder`, `shift-plan`).
- **VERIFIED:** No broader endpoint redesign was needed because the existing controller already maps:
  - `null` board results to `404 NotFound`
  - `InvalidOperationException` to `409 Conflict`
  - `ProductPlanningBoardDto` to the normal success payload
- **VERIFIED:** No new engine or persistence contract was introduced; the response still uses `ProductPlanningBoardDto` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`.
- **IMPLEMENTED:** Reused the existing single-epic request contract `ProductPlanningEpicRequest` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs` to avoid duplicating an identical `EpicId` payload shape.

## 3. Files added/changed

- **IMPLEMENTED**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PlanBoardReconciliationAuditTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-11-explicit-reconciliation-endpoint-ui.md`

## 4. Application-layer reconciliation implemented

- **IMPLEMENTED:** Added `ExecuteReconcileProjectionAsync` to `IProductPlanningBoardService` and `ProductPlanningBoardService` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.
- **IMPLEMENTED:** The reconciliation path now:
  1. loads the current planning context
  2. requires internal intent for the target epic
  3. requires drift to be present and reconcilable
  4. recomputes the projected TFS date pair from the existing intent
  5. writes both TFS fields together through `ITfsClient.UpdateWorkItemPlanningDatesAsync`
  6. leaves persisted internal intent untouched
  7. returns an updated board model with cleared drift for the reconciled epic
- **IMPLEMENTED:** Explicit failure handling in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` covers:
  - product missing
  - epic missing from internal intent scope
  - no internal intent present
  - no drift present
  - calendar resolution blocked
  - insufficient future sprint coverage
  - TFS write failure
- **VERIFIED:** Reconciliation availability remains narrow because `CanReconcileProjection` is only set for:
  - `MissingTfsDates`
  - `TfsProjectionMismatch`
  - `LegacyInvalidTfsDates`
- **NOT IMPLEMENTED:** No engine-rule changes
- **NOT IMPLEMENTED:** No new persistence fields
- **NOT IMPLEMENTED:** No background or automatic reconciliation

## 5. API reconciliation endpoint implemented

- **IMPLEMENTED:** Added `ReconcileProjection` to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`.
- **VERIFIED:** The endpoint remains thin and delegates directly to `ExecuteReconcileProjectionAsync`.
- **VERIFIED:** Response conventions remain aligned with the existing controller:
  - `200 OK` with `ProductPlanningBoardDto` on success
  - `404 NotFound` when the product does not exist
  - `409 Conflict` with message payload for explicit reconcile failures

## 6. UI reconciliation action implemented

- **IMPLEMENTED:** Added a visible `Reconcile TFS projection` action to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`.
- **VERIFIED:** The action is only rendered behind `@if (epic.CanReconcileProjection)`, so it is unavailable when no drift exists or when drift is blocked rather than safely reconcilable.
- **IMPLEMENTED:** The page now calls `ProductPlanningBoardClientService.ReconcileProjectionAsync` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`.
- **VERIFIED:** The success feedback message explicitly says the TFS projection was reconciled from existing internal intent, rather than implying a planning-intent change.
- **NOT IMPLEMENTED:** No board redesign
- **NOT IMPLEMENTED:** No bulk reconcile action
- **NOT IMPLEMENTED:** No ReleasePlanningBoard integration

## 7. Tests added

- **IMPLEMENTED — application layer**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
  - Added tests covering:
    - successful reconcile with drift
    - product missing
    - no internal intent
    - no drift
    - epic missing
    - ambiguous calendar / blocked resolution
    - insufficient future sprint coverage
    - TFS write failure

- **IMPLEMENTED — API layer**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
  - Added tests covering:
    - reconcile success returns updated board
    - missing product returns `NotFound`
    - no-drift reconcile returns `Conflict`

- **IMPLEMENTED — client/UI wiring**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - Added test proving the client posts `ProductPlanningEpicRequest` to `/api/products/{productId}/planning-board/reconcile`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PlanBoardReconciliationAuditTests.cs`
  - Added a source audit proving the plan-board page:
    - gates the action on `epic.CanReconcileProjection`
    - labels the action explicitly
    - calls `ProductPlanningBoardClientService.ReconcileProjectionAsync`

## 8. Verified preserved planning semantics

- **VERIFIED:** Internal planning intent remains authoritative and unchanged during reconcile because the reconcile path reads existing intent and does not call `PersistPlanningIntentAsync`.
- **VERIFIED:** TFS `StartDate` and `TargetDate` remain the only rewritten projection fields because reconcile still routes through `ITfsClient.UpdateWorkItemPlanningDatesAsync` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs`.
- **VERIFIED:** Changed and affected highlights are not falsely reported as intent changes because the reconcile result uses empty `ChangedEpicIds` and `AffectedEpicIds` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.
- **VERIFIED:** Track derivation, computed timing, recovery status, and planning engine behavior remain unchanged because no changes were made to `PlanningRecomputeService`, `PlanningOperationService`, or persistence entities.

## 9. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** No multi-epic or product-wide reconcile action
- **NOT IMPLEMENTED:** No background drift correction
- **NOT IMPLEMENTED:** No new diagnostics banner or board redesign beyond the single explicit action
- **NOT IMPLEMENTED:** No special persisted “reconciled” flag; the board simply returns the refreshed diagnostics state

## 10. Risks or blockers

- **BLOCKER:** None for this phase
- **VERIFIED:** The returned board clears drift immediately by rebuilding the read model with the just-written projected TFS dates for the reconciled epic in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.
- **Risk:** If an external TFS read path lags the mutation path in a future environment, a later reload could temporarily surface stale upstream dates again until that source catches up. This phase does not introduce background retry or cache invalidation because those are out of scope.

## 11. Recommendation for next phase

- **IMPLEMENTED recommendation:** Keep reconciliation explicit and per-epic until production usage proves a broader workflow is needed.
- **IMPLEMENTED recommendation:** If later phases need richer drift workflows, prefer extending the current product-planning-board surface incrementally rather than creating a parallel planning API/controller family.
- **IMPLEMENTED recommendation:** If upstream read/write lag becomes a real issue, address it as a separate bounded phase focused on projection freshness or cache invalidation rather than changing planning semantics.

## Final section

### IMPLEMENTED
- One explicit backend reconcile mutation on the existing product planning board controller
- One application-layer reconcile operation in `ProductPlanningBoardService`
- One explicit per-epic reconcile action in `PlanBoard.razor`
- Focused service, controller, client, and UI audit tests
- Release note entry in `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

### NOT IMPLEMENTED
- No engine redesign
- No persistence schema change
- No new TFS fields
- No background auto-fix
- No drag-and-drop or ReleasePlanningBoard work
- No bulk product-wide reconcile action

### BLOCKERS
- None

### Evidence (files/tests)
- **Files**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PlanBoard.razor`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductPlanningBoardClientService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PlanBoardReconciliationAuditTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`
- **Tests**
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`

### GO/NO-GO for next phase
- **GO** for the next phase. The reconcile flow is explicit, minimal, tested, and does not change planning intent semantics or persistence shape.
