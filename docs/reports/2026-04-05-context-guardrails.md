# Context guardrails — 2026-04-05

## Detected implicit behaviors

### Must be removed
- **Cross-product first-team fallback** in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs`
  - Before this change, an explicitly selected product with no usable sprint history could still fall through to another owned product's first team.
  - Risk: pages loaded valid-looking but incorrect team/sprint context.

### Must be made explicit
- **Sprint Execution without an explicit product** in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs`
  - The endpoint accepted owner scope + sprint scope without forcing a concrete product, which could hide context drift behind an empty result.
- **Sprint-like iteration paths that do not match seeded sprint definitions** in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs`
  - The resolver accepted unmatched iteration paths and let downstream handlers interpret them as a silent empty scope.

### Allowed (temporary, now documented)
- **Owner-scoped product derivation** in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs`
  - Keeping owner-derived product scope for requests that intentionally use all owned products is still allowed.
  - Guardrails were added so derived scope must now remain consistent with selected teams and resolved sprints.
- **Once-per-session default team/sprint presets** in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs`
  - Presets still exist, but they no longer cross an explicit product boundary.

## Guard conditions added

### Client-side defaulting guardrails
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs`
  - When a product is explicitly selected, default team resolution now only considers that product's linked teams.
  - If the selected product has no linked teams with sprint history, the service stops instead of silently switching to another owned product's team.

### Sprint filter consistency guardrails
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs`
  - Added `RequireExplicitProductScope` to boundary requests for queries that need a concrete product.
  - Added validation for:
    - selected team outside selected product scope
    - selected sprint scope outside selected product scope
    - selected sprint scope outside selected team scope
    - sprint-like iteration paths that do not match known sprint definitions

### Fail-fast API behavior
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs`
  - Sprint filter based endpoints now return `400 Bad Request` when filter resolution detects invalid or inconsistent scope instead of continuing into handler execution.
  - `Sprint Execution` now explicitly requires product scope.

## Data inconsistencies found

### Battleship sprint-definition drift is now explicitly validated
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipSprintSeedCatalog.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockTfsClient.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockDataValidator.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`

Guardrails added:
- canonical Battleship sprint definitions are now centralized in `BattleshipSprintSeedCatalog`
- mock validation now flags work items assigned to unknown sprint paths
- mock validation now flags work items assigned to unknown team names

This detects the exact class of mismatch that previously let legacy sprint paths drift away from the seeded Sprint 10..14 definitions.

## Remaining risks
- Other route/filter normalizations still rely on documented implicit behavior, especially rolling-window defaults and route-authoritative project alias resolution.
- `RequireExplicitProductScope` is currently applied where the risk was highest (`Sprint Execution`); other product-sensitive endpoints may need the same treatment if new regressions appear.
- Mock validation now catches sprint-path and team-name drift, but it still does not perform a full persisted product-team-sprint graph audit after sync.

## Files changed
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterDefaultsService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintFilterResolutionService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/MetricsController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipSprintSeedCatalog.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/MockDataValidator.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/GlobalFilterDefaultsServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/SprintFilterResolutionServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/MetricsControllerSprintCanonicalFilterTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/MockDataValidatorTests.cs`
