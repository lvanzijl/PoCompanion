# Refactor Plan

**Date:** 2026-02-08
**Based on:** [Code Quality Audit](code-quality-audit.md)
**Constraint:** 3–7 focused refactors, each small and reversible.

---

## Refactor 1: Extract Cross-Cutting Error Handling into a Mediator Pipeline Behavior

### Goal
Remove the duplicated `catch (Exception ex) { _logger.LogError(...); return <empty>; }` pattern from 8+ handlers by introducing a Mediator pipeline behavior.

### Scope
- **New file:** `PoTool.Api/Behaviors/ExceptionHandlingBehavior.cs`
- **Modified:** `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` (register behavior)
- **Modified (remove try/catch):**
  - `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs`
  - `PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - `PoTool.Api/Handlers/ReleasePlanning/RefreshValidationCacheCommandHandler.cs`
  - `PoTool.Api/Handlers/ReleasePlanning/SplitEpicCommandHandler.cs`
  - `PoTool.Api/Handlers/WorkItems/GetAreaPathsFromTfsQueryHandler.cs`
  - `PoTool.Api/Handlers/WorkItems/GetGoalsFromTfsQueryHandler.cs`
  - `PoTool.Api/Handlers/WorkItems/ValidateWorkItemQueryHandler.cs`

### Steps
1. Create `ExceptionHandlingBehavior<TRequest, TResponse>` implementing `IPipelineBehavior`.
2. Log the exception with the handler type name for context.
3. Return `default(TResponse)` or an appropriate empty/error response.
4. Register the behavior in DI.
5. Remove try/catch blocks from each handler one at a time, verifying tests pass after each removal.

### Risks & Mitigations
- **Risk:** Different handlers may need different error responses (empty list vs. null vs. error DTO).
  - **Mitigation:** Introduce a marker interface (e.g., `IFallbackToDefault`) on handlers that opt in. Only apply the behavior to those.
- **Risk:** Swallowing exceptions may hide bugs.
  - **Mitigation:** The behavior logs at Error level, same as current code. No behavior change.

### Expected Improvements
- **Duplication:** –8 duplicate try/catch blocks
- **LOC:** –40 to –80 lines across handlers
- **Maintainability:** Single place to change error-handling policy

---

## Refactor 2: Fix API → Client Layer Violation

### Goal
Remove the architectural violation where `PoTool.Api` references `PoTool.Client.Services`.

### Scope
- **Modified:** `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` (line 16)
- **Potentially modified:** The referenced client service type — may need its interface moved to `PoTool.Shared`.

### Steps
1. Identify which type from `PoTool.Client.Services` is used in the API layer.
2. If it's an interface, move the interface to `PoTool.Shared` (the shared layer both projects reference).
3. If it's a concrete class registration, restructure so the Client project registers its own services.
4. Remove the `using PoTool.Client.Services;` import.
5. Verify the solution builds.

### Risks & Mitigations
- **Risk:** Moving an interface may break other references.
  - **Mitigation:** Use IDE rename/move refactoring. The interface likely has few consumers.

### Expected Improvements
- **Boundary violations:** –1
- **Architecture compliance:** API no longer depends on Client

---

## Refactor 3: Remove EF Core Dependency from Integrations.Tfs

### Goal
Remove the unnecessary `Microsoft.EntityFrameworkCore` package reference from the TFS integration project.

### Scope
- **Modified:** `PoTool.Integrations.Tfs/PoTool.Integrations.Tfs.csproj` (line 18)
- **Potentially modified:** Any file in `PoTool.Integrations.Tfs/` that uses EF Core types.

### Steps
1. Search for `using Microsoft.EntityFrameworkCore` in all files under `PoTool.Integrations.Tfs/`.
2. If no usages found, simply remove the package reference.
3. If usages found, determine if the type can be replaced with a framework type or moved to `PoTool.Api`.
4. Remove the package reference from the `.csproj`.
5. Build to verify.

### Risks & Mitigations
- **Risk:** A transitive dependency may break at runtime.
  - **Mitigation:** Run integration tests after removal.

### Expected Improvements
- **Dependencies:** –1 unnecessary package reference
- **Build time:** Marginal improvement in Integrations.Tfs compilation

---

## Refactor 4: Consolidate Duplicate GetColor Logic

### Goal
Eliminate the duplicate `GetColor(string type)` implementation that exists in both Core and Client layers.

### Scope
- **Keep:** `PoTool.Core/WorkItems/WorkItemType.cs` (authoritative source)
- **Modify:** `PoTool.Client/Models/WorkItemTypeHelper.cs` — delegate to Core's implementation or remove and update call sites
- **If Core is not referenced by Client:** Move `GetColor` to `PoTool.Shared/WorkItems/` so both layers can reference it.

### Steps
1. Check if `PoTool.Client` references `PoTool.Core` (via `.csproj`).
2. If yes: delete `WorkItemTypeHelper.GetColor` and update all Client call sites to use `WorkItemType.GetColor`.
3. If no: move `GetColor` to a static class in `PoTool.Shared`, update both Core and Client to reference it.
4. Build and run Blazor component tests.

### Risks & Mitigations
- **Risk:** Client and Core implementations may have intentional differences.
  - **Mitigation:** Diff the two implementations first. If differences exist, document them before merging.

### Expected Improvements
- **Duplication:** –1 parallel implementation
- **Drift risk:** Eliminated for color mapping

---

## Refactor 5: Split ReleasePlanningController into Resource-Specific Controllers

### Goal
Reduce the 748-LOC / ~40-endpoint `ReleasePlanningController` into focused controllers by resource type.

### Scope
- **Modified:** `PoTool.Api/Controllers/ReleasePlanningController.cs` → split into:
  - `ReleasePlanningBoardController.cs` — board-level queries
  - `ReleasePlanningLaneController.cs` — lane CRUD
  - `ReleasePlanningCardController.cs` — card CRUD
  - `ReleasePlanningValidationController.cs` — validation endpoints
- **Modified:** NSwag/API client regeneration (if auto-generated)

### Steps
1. Identify endpoint groups by analyzing route prefixes and resource nouns.
2. Create new controller files, moving endpoints one group at a time.
3. Keep the same route structure (use `[Route("api/release-planning/...")]` on new controllers).
4. Regenerate API client if using NSwag.
5. Build and run existing tests.

### Risks & Mitigations
- **Risk:** Client code may reference controller-specific generated client classes.
  - **Mitigation:** If routes stay the same, the generated client methods remain stable. Verify with a build.
- **Risk:** Shared DI or controller state.
  - **Mitigation:** Controllers should be stateless (only IMediator injection). Splitting should be mechanical.

### Expected Improvements
- **LOC per controller:** ~748 → ~150–200 each
- **Readability:** Each controller has a single resource focus
- **Testability:** Easier to test controller-level concerns in isolation

---

## Refactor 6: Extract Metric Calculation from GetMultiIterationBacklogHealthQueryHandler

### Goal
Move the metric calculation logic out of the 505-LOC handler into a dedicated service in the API or Core layer.

### Scope
- **New file:** `PoTool.Api/Services/BacklogHealthCalculator.cs` (or `PoTool.Core/Metrics/BacklogHealthCalculator.cs` if infrastructure-free)
- **Modified:** `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs` — delegate calculation

### Steps
1. Identify the pure-calculation methods within the handler (no DB or external calls).
2. Extract them into `BacklogHealthCalculator` with clear input/output types.
3. Inject `BacklogHealthCalculator` into the handler.
4. Verify existing unit tests pass.
5. Add unit tests for `BacklogHealthCalculator` in isolation (optional but recommended).

### Risks & Mitigations
- **Risk:** Calculation methods may have implicit dependencies on handler state.
  - **Mitigation:** Make all dependencies explicit as method parameters.

### Expected Improvements
- **Handler LOC:** ~505 → ~150
- **Testability:** Pure calculation logic testable without mocking data access
- **Reusability:** Calculator usable by other handlers or services

---

## Refactor 7: Rename TfsConfigEntity to TfsConfigDto

### Goal
Fix the misleading "Entity" suffix on a class that is actually a DTO in the Shared layer.

### Scope
- **Renamed:** `PoTool.Shared/Settings/TfsConfigEntity.cs` → `TfsConfigDto.cs`
- **Modified:** All files referencing `TfsConfigEntity` (find-and-replace)

### Steps
1. Use IDE rename refactoring to rename `TfsConfigEntity` → `TfsConfigDto` across the solution.
2. Rename the file to match.
3. Build to verify.

### Risks & Mitigations
- **Risk:** Generated API client may reference the old name.
  - **Mitigation:** Regenerate client after rename.

### Expected Improvements
- **Naming clarity:** Eliminates confusion between DTOs and EF entities
- **Convention alignment:** All Shared types use `Dto` suffix consistently
