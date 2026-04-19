# Phase 8 Persistence + TFS Write-Back + Recovery Implementation

## 1. Summary

- **IMPLEMENTED:** Durable product-scoped planning intent persistence now exists through `ProductPlanningIntentEntity`, `IProductPlanningIntentStore`, and `ProductPlanningIntentStore`.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductPlanningIntentEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningIntentStore.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductPlanningIntentStore.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260419212844_AddProductPlanningIntentPersistence.cs`.

- **IMPLEMENTED:** The existing planning bridge now loads durable intent before bootstrap, recovers missing intent from TFS planning dates, reconciles stale rows, and writes normalized planning dates back to TFS on mutations and normalized recoveries without changing engine rules.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.

- **IMPLEMENTED:** TFS read/write support for `Microsoft.VSTS.Scheduling.StartDate` and `Microsoft.VSTS.Scheduling.TargetDate` now flows through shared DTOs, cached work-item persistence, real/mock/gateway TFS clients, and the real PATCH write path.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/WorkItemEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsAccessGateway.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`.

## 2. Chosen persistence-layer location and rationale

- **VERIFIED:** Durable planning intent was added in the API persistence layer, consistent with the repository’s existing EF ownership pattern for durable state and the rule that infrastructure/persistence stays out of Core.  
  **Evidence:** existing persistence conventions in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`; new entity/repository files listed above.

- **IMPLEMENTED:** Core owns only the planning-intent abstraction (`IProductPlanningIntentStore`, `ProductPlanningIntentRecord`, `ProductPlanningRecoveryStatus`), while API owns EF mapping and repository implementation.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningIntentStore.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductPlanningIntentStore.cs`.

## 3. Files added/changed

- **IMPLEMENTED:** Added
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningIntentStore.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductPlanningIntentEntity.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductPlanningIntentStore.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260419212844_AddProductPlanningIntentPersistence.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260419212844_AddProductPlanningIntentPersistence.Designer.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/ProductPlanningIntentStoreTests.cs`

- **IMPLEMENTED:** Updated
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/PoToolDbContextModelSnapshot.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/WorkItemEntity.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemQueryMapping.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SyncChangesSummaryService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/ValidationComputeStage.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsAccessGateway.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RealTfsClientRequestTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/WorkItemRepositoryTests.cs`

## 4. Internal persistence model implemented

- **IMPLEMENTED:** Persisted exactly the locked durable intent shape:
  - `ProductId`
  - `EpicId`
  - `StartSprintStartDateUtc`
  - `DurationInSprints`
  - `RecoveryStatus` (nullable)
  - `UpdatedAtUtc`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/ProductPlanningIntentEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningIntentStore.cs`.

- **VERIFIED:** Track and computed timing are still not persisted.  
  **Evidence:** no such columns/properties were added to `ProductPlanningIntentEntity`; write model projection is still derived inside `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.

## 5. TFS read/write changes implemented

- **IMPLEMENTED:** Added planning-date fields to `WorkItemDto` and persisted cached work-item storage.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/WorkItemDto.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/WorkItemEntity.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/WorkItemRepository.cs`.

- **IMPLEMENTED:** Added the two fields to the real TFS required-field list and extraction logic for direct, batch, hierarchy, and PATCH-response parsing.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`.

- **IMPLEMENTED:** Added a paired planning-date write method that always writes both fields together and uses date-only payload values.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ITfsClient.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/TfsAccessGateway.cs`; `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsUpdate.cs`.

## 6. Canonical sprint-calendar resolver implemented

- **IMPLEMENTED:** The planning bridge now resolves product calendars by loading linked-team sprints, discarding null-boundary rows, normalizing to UTC dates, deduplicating identical windows, ordering by start date, and failing on overlapping distinct windows.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`ResolveCalendarAsync`).

- **IMPLEMENTED:** Ambiguous calendars now fail explicitly instead of being guessed.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`RequireCalendar`, `ResolvePersistedStartIndex`, `ResolveCalendarAsync`); test coverage in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

## 7. Forward mapping implemented

- **IMPLEMENTED:** Planning intent now projects to TFS as:
  - `StartDate = start sprint first day`
  - `TargetDate = final sprint last day (inclusive)`  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`TryCreatePlanningDateWriteRequest`, `PersistPlanningIntentAsync`).

- **IMPLEMENTED:** Missing start-sprint boundaries and insufficient future sprint coverage now fail explicitly during durable projection.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`MapToIntentRecord`, `TryCreatePlanningDateWriteRequest`); tests in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`.

## 8. Reverse recovery implemented

- **IMPLEMENTED:** Missing internal intent now tries TFS-date recovery using containing sprint windows, exact-vs-normalized detection, duration derivation, legacy invalid start-date rejection (`< 2021-04-19`), and fallback to bootstrap on failure.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`TryRecoverIntent`, `TryFindSprintContainingDate`).

- **IMPLEMENTED:** Successful recovery persists internal intent immediately; normalized recovery also rewrites normalized TFS dates.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`BuildPlanningContextAsync`).

## 9. Precedence/reconciliation behavior implemented

- **IMPLEMENTED:** Internal durable intent wins over differing TFS dates when present.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`BuildPlanningContextAsync`, persisted-intent branch); executable coverage in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`.

- **IMPLEMENTED:** Missing internal intent falls back to TFS-date recovery, then deterministic bootstrap if recovery fails.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`; executable coverage in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`.

- **IMPLEMENTED:** Stale internal rows for out-of-scope epics are removed before the live planning state is built.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`DeleteMissingEpicsAsync` call); `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductPlanningIntentStore.cs`.

- **NOT IMPLEMENTED:** A separate explicit drift-reconciliation trigger for rewriting stale TFS dates when internal intent exists but no mutation/recovery occurs was not added in this phase. Mutation-time projection and normalization-time rewrite are implemented.  
  **Evidence:** no separate controller/job/command was added; write-back entry points remain in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` mutation and recovery flows only.

## 10. Session integration behavior implemented

- **IMPLEMENTED:** Durable planning intent is now the base state; session state remains an in-memory overlay on top. Reset clears only the session layer and rehydrates from the durable/recovered/bootstrap base.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`GetOrLoadSessionState`, `ResetPlanningBoardAsync`, `BuildPlanningContextAsync`).

- **IMPLEMENTED:** Session state is invalidated when the active roadmap epic set changes, preventing stale removed epics from surviving only in memory.  
  **Evidence:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs` (`SessionStateMatchesActiveScope`).

## 11. Tests added

- **IMPLEMENTED:** API-side executable planning persistence/recovery tests:  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardPersistenceTests.cs`

- **IMPLEMENTED:** Updated controller tests for durable-reset semantics:  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/ProductPlanningBoardControllerTests.cs`

- **IMPLEMENTED:** Added planning-intent repository tests:  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/ProductPlanningIntentStoreTests.cs`

- **IMPLEMENTED:** Added real-client planning-date read/write tests:  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RealTfsClientRequestTests.cs`

- **IMPLEMENTED:** Added work-item cache round-trip planning-date test:  
  `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/WorkItemRepositoryTests.cs`

## 12. Verified preserved engine semantics

- **VERIFIED:** The planning engine itself was not redesigned; changes stayed in the application/persistence/integration layers.  
  **Evidence:** no changes under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/Domain/Planning/**`.

- **VERIFIED:** Track remains derived/session-only and is not stored durably.  
  **Evidence:** no track column in `ProductPlanningIntentEntity`; mutation persistence maps only `PlannedStartSprintIndex` + `DurationInSprints` via `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Planning/ProductPlanningBoardService.cs`.

## 13. Known gaps intentionally left for later phases

- **NOT IMPLEMENTED:** No UI changes.
- **NOT IMPLEMENTED:** No planning-engine rule changes.
- **NOT IMPLEMENTED:** No auth or user/session partition redesign.
- **NOT IMPLEMENTED:** No `ReleasePlanningBoard` reuse or integration.
- **NOT IMPLEMENTED:** No automatic TFS-date clearing for out-of-scope epics.

## 14. Risks or blockers

- **BLOCKER:** `PoTool.Core.Domain.Tests` still has a pre-existing restore failure (`NU1102` on `Microsoft.Extensions.DependencyInjection.Abstractions`) that prevented execution of the expanded service tests in that project.  
  **Evidence:** baseline and follow-up command failure while running `dotnet build PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`.

- **BLOCKER:** `PoTool.Tests.Unit` still has pre-existing unrelated compile failures in many adapter/service tests, so the new unit tests added there could not be executed in this phase.  
  **Evidence:** `dotnet build PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore` output showed unrelated errors in files such as `TfsAccessBoundaryArchitectureTests.cs`, `StateClassificationInputMapperTests.cs`, and `WorkItemResolutionServiceTests.cs`.

- **VERIFIED:** A concurrent build attempt also hit an unrelated file-lock in `PoTool.Client` Blazor build outputs; sequential API build/test runs succeeded afterward.  
  **Evidence:** sequential `dotnet build PoTool.Api/PoTool.Api.csproj --no-restore` and `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore` succeeded.

## 15. Recommendation for next phase

- **IMPLEMENTED:** This phase delivered the locked persistence, recovery, and write-back slice.
- **RECOMMENDATION:** Next phase should focus on explicit drift-reconciliation workflows, operational diagnostics around write-back failures, and clearing the pre-existing test-project validation blockers so the newly added lower-level tests can run.

## Final section

### IMPLEMENTED

- Durable internal planning-intent persistence.
- Product-scoped canonical sprint-calendar resolution.
- Forward mapping from durable intent to TFS planning dates.
- Reverse recovery from TFS planning dates to durable sprint intent.
- Precedence: internal intent wins; missing intent recovers/falls back.
- Stale internal-row reconciliation for removed epics.
- Session layering on top of durable intent.
- Real/mock/gateway TFS planning-date read/write support.

### NOT IMPLEMENTED

- Separate explicit drift-reconciliation trigger for stale TFS projections without a mutation/recovery event.
- UI, engine, auth/session redesign, or `ReleasePlanningBoard` work.

### BLOCKERS

- Pre-existing `PoTool.Core.Domain.Tests` restore failure.
- Pre-existing `PoTool.Tests.Unit` compile failures unrelated to this slice.

### Evidence (files/tests)

- **Files:** all files listed in sections 3–11 above.
- **Successful validation:**
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core/PoTool.Core.csproj`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`

### GO/NO-GO for next phase

- **GO:** The persistence/write-back/recovery implementation is in place and validated through the executable API-side planning tests and successful core/API builds.
