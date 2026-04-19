# Phase 6 API Layer Planning

## 1. Summary

- **IMPLEMENTED:** Exposed the existing product planning board through thin backend endpoints without changing planning business logic.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`.

- **VERIFIED:** The API layer continues to use the existing planning application bridge and in-memory session state rather than introducing a second orchestration path.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`; `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`.

- **VERIFIED:** Targeted API and planning validation passes after the phase-6 changes.  
  **Evidence:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`.

## 2. Chosen API surface and rationale

- **IMPLEMENTED:** Added a dedicated controller at `api/products/{productId}/planning-board` instead of hiding the feature inside an unrelated controller.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`.

- **VERIFIED:** This route aligns with current API conventions because it keeps the planning board product-scoped while remaining a dedicated planning endpoint surface, similar to other resource-specific controllers and product-scoped routes.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.MissingProductMutationEndpoints_ReturnNotFound`.

- **VERIFIED:** No persistence or TFS mapping was introduced because the controller only delegates to the existing planning application service and reuses existing read models/contracts.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.InvalidPlanningOperations_ReturnOkBoardWithValidationIssues`.

## 3. Files added/changed

### Added

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/Properties/AssemblyInfo.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-phase-6-api-layer-planning.md`

### Changed

- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`
- **IMPLEMENTED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
- **IMPLEMENTED:** lock files updated for affected test projects:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/packages.lock.json`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/packages.lock.json`

## 4. API contracts added

- **IMPLEMENTED:** Added minimal shared request contracts for epic-only, epic-plus-delta, and reorder operations.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`.

- **VERIFIED:** The API response reuses the existing `ProductPlanningBoardDto` read model instead of exposing engine-internal types.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`.

- **VERIFIED:** Product identity remains part of the endpoint contract through the route, while mutation payloads carry only the additional operation fields required beyond `productId`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.MissingProductMutationEndpoints_ReturnNotFound`.

## 5. Endpoints added

- **IMPLEMENTED:** `GET /api/products/{productId}/planning-board`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`; `ProductPlanningBoardControllerTests.GetPlanningBoard_MissingProduct_ReturnsNotFound`.

- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/reset`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.ResetPlanningBoard_ReturnsFreshBoardAfterSessionChanges`.

- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/move`  
- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/adjust-spacing`  
- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/run-in-parallel`  
- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/return-to-main`  
- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/reorder`  
- **IMPLEMENTED:** `POST /api/products/{productId}/planning-board/shift-plan`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`; `ProductPlanningBoardControllerTests.InvalidPlanningOperations_ReturnOkBoardWithValidationIssues`.

## 6. Tests added

- **IMPLEMENTED:** Added a dedicated API test project because controller tests were not feasible in the existing `PoTool.Tests.Unit` baseline, which already fails compilation for unrelated reasons.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`; full-solution blocker log `/tmp/copilot-tool-output-1776596738413-ov00pj.txt`.

- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.GetPlanningBoard_MissingProduct_ReturnsNotFound`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.ResetPlanningBoard_ReturnsFreshBoardAfterSessionChanges`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.InvalidPlanningOperations_ReturnOkBoardWithValidationIssues`  
- **IMPLEMENTED:** `ProductPlanningBoardControllerTests.MissingProductMutationEndpoints_ReturnNotFound`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`.

- **VERIFIED:** Existing planning service tests remain green after the API changes and the extracted shared test fixture.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`.

## 7. Verified preserved planning semantics

- **VERIFIED:** The controller remains thin and does not duplicate engine or application-layer planning logic; every endpoint delegates directly to `IProductPlanningBoardService`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`.

- **VERIFIED:** Existing in-memory session continuity is preserved through the API layer.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`; `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`.

- **VERIFIED:** Invalid planning operations still surface as planning-board validation issues instead of being converted into ad hoc API-specific error payloads.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.InvalidPlanningOperations_ReturnOkBoardWithValidationIssues`.

- **VERIFIED:** Missing products still map to `404 Not Found`, matching current API conventions for null application-layer results.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_MissingProduct_ReturnsNotFound`; `ProductPlanningBoardControllerTests.MissingProductMutationEndpoints_ReturnNotFound`.

## 8. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** database persistence, repositories, EF entities/migrations, UI pages/components, and TFS planning date-field mapping.  
  **Evidence:** changed files are limited to controller/contracts/tests/report/solution artifacts listed above; no changed files under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/`, or `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/`.

- **NOT IMPLEMENTED:** auth- or user-scoped session partitioning changes, cross-process durability, or new planning business rules.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`.

- **NOT IMPLEMENTED:** alternate API orchestration paths, mediator handlers, or controller-side planning transforms.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`.

## 9. Risks or blockers

- **BLOCKER:** Full solution build is still blocked by unrelated pre-existing `PoTool.Tests.Unit` compile failures.  
  **Evidence:** `/tmp/copilot-tool-output-1776596738413-ov00pj.txt`, including failures in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Adapters/StateClassificationInputMapperTests.cs`, and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemResolutionServiceTests.cs`.

- **VERIFIED:** The new API slice itself is green despite the unrelated full-solution blockers.  
  **Evidence:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`; `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`.

## 10. Recommendation for next phase

- **VERIFIED:** The next phase can consume these endpoints from the client without redesigning the planning engine, application bridge, or session behavior because the API now exposes the existing read model and operations directly.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`; `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`; `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`.

## Final section

### IMPLEMENTED

- dedicated product-scoped planning board API controller
- shared API request contracts for planning mutations
- endpoints for get, reset, move, adjust-spacing, run-in-parallel, return-to-main, reorder, and shift-plan
- endpoint behavior that preserves planning validation issues and changed/affected ids
- dedicated API controller test project plus controller tests
- extracted shared planning test fixture reused by service and API tests

### NOT IMPLEMENTED

- database persistence or EF changes
- repositories or alternate orchestration paths
- UI pages/components
- TFS date-field mapping
- auth/session partitioning redesign
- cross-process durability

### BLOCKERS

- unrelated pre-existing `PoTool.Tests.Unit` compile failures still prevent a clean full solution build

### Evidence (files/tests)

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductPlanningBoardController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardApiContracts.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
- `ProductPlanningBoardControllerTests.GetPlanningBoard_ValidProduct_ReturnsOkBoard`
- `ProductPlanningBoardControllerTests.GetPlanningBoard_MissingProduct_ReturnsNotFound`
- `ProductPlanningBoardControllerTests.ResetPlanningBoard_ReturnsFreshBoardAfterSessionChanges`
- `ProductPlanningBoardControllerTests.MutationEndpoints_ReturnUpdatedBoardsAndPreserveChangedAffectedIds`
- `ProductPlanningBoardControllerTests.SequentialApiCalls_ReuseSessionState_AndResetClearsIt`
- `ProductPlanningBoardControllerTests.InvalidPlanningOperations_ReturnOkBoardWithValidationIssues`
- `ProductPlanningBoardControllerTests.MissingProductMutationEndpoints_ReturnNotFound`

### GO/NO-GO for next phase

- **GO** for the next phase, with the unchanged caveat that the repository baseline still has unrelated `PoTool.Tests.Unit` build failures.
