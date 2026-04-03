# Final Filter Contract Cleanup

## 1. SprintCadenceResolver outcome

- **Rewritten to strict behavior**
- `PoTool.Client/Services/SprintCadenceResolver.cs` no longer falls back to:
  - current sprint duration
  - default sprint duration
- It now resolves cadence **only** from completed sprint history.
- When no completed sprint history exists, it returns an unresolved cadence state (`DurationDays = null`) instead of manufacturing a duration.
- `ProductRoadmaps` and `MultiProductPlanning` were updated to treat unresolved cadence as an explicit unavailable state and hide timeline rendering instead of using synthetic duration assumptions.

## 2. Backend sprint resolution outcome

- **Removed**
  - unused `GetNextSprintForTeamAsync` contract and repository implementation
- **Kept**
  - `GetCurrentSprintForTeamAsync`
  - `GetCurrentSprintForTeamQuery`
  - `SprintsController` current-sprint endpoint
  - `SprintService.GetCurrentSprintForTeamAsync`
  - `WorkspaceSignalService` current-sprint lookup for home signals
- **Hardening applied**
  - repository and query comments now describe strict semantics only
  - strict behavior confirmed:
    - explicit team required
    - returns `null` when no team-scoped current/overlapping sprint exists
    - no fallback to latest past sprint
    - no fallback to earliest future sprint
    - no synthetic “current” sprint promotion
- **Fallback behavior remaining?**
  - **No shared backend fallback behavior remains** in the kept current-sprint lookup path.

## 3. Legacy navigation cleanup outcome

- **Removed artifacts**
  - `PoTool.Client/Services/NavigationContextService.cs`
  - `PoTool.Client/Services/INavigationContextService.cs`
  - `PoTool.Client/Models/NavigationContext.cs`
  - `PoTool.Client/Pages/Landing.razor`
  - `PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor`
  - `PoTool.Client/Pages/LegacyWorkspaces/TeamWorkspace.razor`
  - `PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor`
  - `PoTool.Client/Pages/LegacyWorkspaces/CommunicationWorkspace.razor`
  - `PoTool.Tests.Unit/Services/NavigationContextServiceTests.cs`
- **Reduced remaining pieces**
  - `WorkspaceRoutes` no longer contains `/legacy` or `/workspace/*` route constants
  - `WorkspaceRoutes.GetRouteForIntent(...)` was removed
  - DI registration for `INavigationContextService` was removed
  - stale injections were removed from `MainLayout` and `TrendsWorkspace`
- **Minimal pieces remaining**
  - none from the old intent/context navigation model remain in live client infrastructure

## 4. Files changed

- `PoTool.Api/Repositories/SprintRepository.cs`
- `PoTool.Client/Layout/MainLayout.razor`
- `PoTool.Client/Models/NavigationContext.cs` *(deleted)*
- `PoTool.Client/Models/WorkspaceRoutes.cs`
- `PoTool.Client/Pages/Home/MultiProductPlanning.razor`
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Pages/Landing.razor` *(deleted)*
- `PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor` *(deleted)*
- `PoTool.Client/Pages/LegacyWorkspaces/CommunicationWorkspace.razor` *(deleted)*
- `PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor` *(deleted)*
- `PoTool.Client/Pages/LegacyWorkspaces/TeamWorkspace.razor` *(deleted)*
- `PoTool.Client/Program.cs`
- `PoTool.Client/Services/INavigationContextService.cs` *(deleted)*
- `PoTool.Client/Services/NavigationContextService.cs` *(deleted)*
- `PoTool.Client/Services/SprintCadenceResolver.cs`
- `PoTool.Core/Contracts/ISprintRepository.cs`
- `PoTool.Core/Settings/Queries/SprintQueries.cs`
- `PoTool.Tests.Unit/Services/NavigationContextServiceTests.cs` *(deleted)*
- `PoTool.Tests.Unit/Services/SprintCadenceResolverTests.cs`

## 5. Verification

- **Build**
  - `dotnet build PoTool.sln --configuration Release --nologo`
  - **Result:** passed
- **Tests**
  - `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`
  - **Result:** passed
- **Fallback search summary**
  - removed code-level legacy navigation references to `NavigationContextService`, `INavigationContextService`, `NavigationContext`, `Intent`, `ScopeLevel`, `TimeHorizon`, `TriggerType`, and `WorkspaceRoutes.GetRouteForIntent(...)`
  - removed code-level cadence fallback markers such as `UsesFallback`, `UsesDefaultDuration`, `CurrentSprintFallback`, and `DefaultFallback`
  - `GetNextSprintForTeamAsync` no longer exists
  - remaining `"current sprint"` occurrences are in:
    - strict current-sprint APIs/comments
    - home-signal logic that intentionally consumes strict current-sprint lookup
    - historical/documentation text outside the enforcement path
  - remaining `First(` occurrences are general collection access or grouping logic, not first-team / first-sprint fallback resolution

## 6. Readiness verdict

- **READY for Phase 1**

No shared sprint-cadence fallback logic remains.
No legacy navigation/context infrastructure remains in the live client.
The remaining shared current-sprint lookup is strict, team-scoped, and unresolved when no current sprint exists.
