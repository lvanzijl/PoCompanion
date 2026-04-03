# Pre-Enforcement Cleanup

## Changes Applied

- **Legacy routes**
  - Disabled direct routing for `/legacy`
  - Disabled direct routing for `/workspace/product`
  - Disabled direct routing for `/workspace/team`
  - Disabled direct routing for `/workspace/analysis`
  - Disabled direct routing for `/workspace/communication`

- **Sprint Delivery**
  - Removed implicit multi-team sprint loading
  - Requires explicit `teamId` and `sprintId` from route context
  - Stops loading when the selected sprint cannot be resolved for the selected team

- **Sprint Execution**
  - Removed automatic first-team selection
  - Removed automatic sprint selection
  - Added explicit unresolved state for missing team or sprint
  - Preserves explicit team/sprint context in the URL

- **Pipeline Insights**
  - Removed single-team auto-selection
  - Removed automatic current/latest sprint selection
  - Requires explicit team and sprint before loading

- **Portfolio Delivery**
  - Removed automatic first-team selection
  - Removed automatic sprint-range defaulting
  - Requires explicit team plus explicit from/to sprint range

- **Delivery Trends**
  - Removed current/latest sprint anchoring
  - Uses a deterministic end-of-range selection based on the ordered sprint list

- **Portfolio Flow**
  - Removed first-team auto-selection
  - Replaced current-sprint anchoring with a deterministic last-5-sprints window for an explicitly selected team

- **PR Insights**
  - Removed automatic team selection
  - Removed automatic current/latest sprint selection
  - Keeps rolling default behavior unless a sprint is explicitly chosen

- **PR Delivery Insights**
  - Removed automatic team selection
  - Removed automatic current/latest sprint selection
  - Keeps rolling default behavior unless a sprint is explicitly chosen

- **Home Changes**
  - Explicitly marked as a non-filtered operational page
  - Left outside the global filter contract

- **Product Roadmaps**
  - Removed product-team sprint lookup based on `product.TeamIds.First()`

- **Multi-Product Planning**
  - Removed product-team sprint lookup based on `product.TeamIds.First()`

- **Plan Board**
  - Removed product-team sprint lookup based on `product.TeamIds.First()`
  - Added explicit unresolved message for unavailable sprint columns

## Fallbacks Removed

- first-team selection (`_teams[0]`)
- product-team resolution via `product.TeamIds.First()`
- automatic current sprint selection
- automatic latest / most recent sprint selection
- implicit multi-team sprint merging for Sprint Delivery
- automatic sprint-range defaulting for Portfolio Delivery based on current/latest sprint

## Remaining Risks

- `PoTool.Client/Services/SprintCadenceResolver.cs` still contains current/default fallback cadence logic; snapshot planning pages now avoid hidden team lookup, but the shared resolver still models fallback duration behavior.
- `PoTool.Api/Repositories/SprintRepository.cs` and current-sprint query paths still exist server-side; they are no longer required by the cleaned-up pages in this change set, but they remain in the codebase.
- `WorkspaceRoutes` and `NavigationContextService` still carry legacy route constants and intent-routing logic even though direct route access was disabled.

## Readiness Verdict

**NOT READY**

Reasons:

- shared sprint-cadence fallback logic still exists in the client service layer
- current-sprint backend resolution paths still exist
- legacy route constants and intent-routing helpers remain, even though direct access is disabled
