# Current Sprint Resolution Analysis

This document traces the actual code paths that define, infer, and consume “current sprint” across the repository. The goal is to determine whether the term has one reliable global meaning or whether it is structurally dependent on team, product, or other local context.

## 1. Sprint resolution mechanisms

### 1.1 Team-scoped authoritative sprint source

- Team sprint data is synced per team from the Azure DevOps/TFS team iterations endpoint (`.../_apis/work/teamsettings/iterations`) in `RealTfsClient.GetTeamIterationsAsync`, which maps `path`, `startDate`, `finishDate`, and `timeFrame` into `TeamIterationDto`. `TeamSprintSyncStage` runs that fetch for every linked team and persists the results through `ISprintRepository.UpsertSprintsForTeamAsync`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs:294-421`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/TeamSprintSyncStage.cs:43-163`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TeamIterationDto.cs:3-14`
- The only explicit backend “current sprint” API is team-scoped: `SprintRepository.GetCurrentSprintForTeamAsync(teamId)` prefers `TimeFrame == "current"` and falls back to `start <= now < end`. `SprintsController.GetCurrentSprintForTeam` exposes that as `GET /api/sprints/current?teamId=...`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:45-72`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SprintsController.cs:42-65`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Contracts/ISprintRepository.cs:21-29`

### 1.2 Client-side team-scoped default selection from full sprint lists

- `PipelineInsights`, `PrOverview`, and `PrDeliveryInsights` load all sprints for one selected team and compute “current” locally by date overlap only; if none exists they fall back to the most recent past sprint, then the first sprint in the list. These pages do **not** use `TimeFrame == "current"`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:677-724`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:971-998`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:653-680`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:790-836`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:769-815`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:894-925`
- `SprintExecution`, `PortfolioDelivery`, and `PortfolioProgressPage` also load one team’s sprint list, but their local default logic prefers `TimeFrame == "current"`, then falls back to date overlap, then most recent past sprint. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:508-518`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:547-560`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor:531-547`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor:575-591`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:645-665`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:679-713`

### 1.3 Multi-team merged “current sprint” heuristics

- `DeliveryTrends` loads sprints for **all teams**, optionally narrows that merged set by selected team, and then finds the “current” sprint by date overlap only. If none exists it falls back to the latest past sprint, then the last sprint in the merged list. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:376-392`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:405-431`
- `SprintTrend` also loads all teams’ sprints, merges them, and finds the “current” sprint by date overlap only; if none exists it falls back to the latest past sprint, then the last sprint in the merged list. `ApplySprintFilters` does not apply team or product scoping to `_allSprints`, so the current sprint is effectively portfolio-wide over the loaded team set. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1064-1095`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1183-1209`

### 1.4 Aggregated home/workspace signals

- `WorkspaceSignalService.GetDeliverySignalAsync` scopes products, derives their distinct team IDs, calls `GetCurrentSprintForTeamAsync` once per team, and then merges the resulting list of current sprints. This service therefore assumes multiple concurrent “current sprints” can exist across the selected scope. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:88-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:416-425`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor:522-556`
- `GetHomeProductBarMetricsQueryHandler` derives team IDs from the effective product scope, finds all sprints where `start <= now <= end`, and averages their elapsed-time percentages. It does not use `TimeFrame`, and it intentionally produces one combined “current sprint progress” number across multiple teams. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:105-175`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Metrics/HomeProductBarMetricsDto.cs:6-23`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomePage.razor:490-556`

### 1.5 Iteration-window selectors that treat “current” as a generic date anchor

- `SprintWindowSelector` is used by `GetMultiIterationBacklogHealthQueryHandler` to construct health windows from arbitrary sprint lists matched by iteration path. It defines current as `start <= today < end`; if none exists it promotes the earliest future sprint, and if that also does not exist it promotes the latest past sprint. This is not team-based and can operate over any mixed set of sprint records. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:21-105`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:130-233`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:61-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/BacklogHealth/BacklogHealthPanel.razor:42-125`

### 1.6 Current-sprint inference for cadence, not selection

- `SprintCadenceResolver` uses completed sprint history first; if none exists it falls back to a single valid current sprint (`TimeFrame == "current"` or date overlap), otherwise to a hardcoded 14-day default. `ProductRoadmaps` and `MultiProductPlanning` feed it the first team’s sprint list for each product, so its “current sprint” inference is product-first-team scoped rather than globally valid. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/SprintCadenceResolver.cs:18-67`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1539-1555`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:685-700`

## 2. Dependency map

| Surface | How “current sprint” is resolved | Team required | Works without team | Multi-team behavior |
|---|---|---:|---:|---|
| `HomePage` delivery signal | Per-team backend current sprint (`GetCurrentSprintForTeamAsync`), merged across scoped product teams | No direct team input | Yes, if products imply teams | Yes; one current sprint per team is loaded and evaluated together. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:88-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:416-425` |
| `HomePage` product bar | Server computes all active sprints for scoped product teams and averages progress | No direct team input | Yes, if products imply teams | Yes; collapses multiple active sprints into one averaged percentage. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:105-175` |
| `DeliveryTrends` | Merged sprint list, date-based current, else latest past, else last list item | No | Yes | Yes; if no team filter is set, the “current” sprint is whichever merged sprint first matches the date heuristic. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:376-392`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:405-431` |
| `SprintTrend` | Merged sprint list, date-based current, else latest past, else last list item | No | Yes | Yes; current sprint is selected from all loaded team sprints, with no team filter applied in `ApplySprintFilters`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1064-1095`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1183-1209` |
| `PipelineInsights` | Selected team’s sprint list, date-based current, else latest past, else first list item | Effectively yes for sprint default | Page loads without team, but no current sprint is chosen until a team exists | No merged behavior; one team only. Auto-selects team only when exactly one team exists. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:638-663`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:677-724` |
| `PrOverview` | Selected team’s sprint list, date-based current, else latest past, else first list item | Effectively yes for sprint default | Yes; page falls back to date-range insights when no team is selected | No merged behavior; one team only. Auto-selects team only when exactly one team exists. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:653-680`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:790-836` |
| `PrDeliveryInsights` | Selected team’s sprint list, date-based current, else latest past, else first list item | Effectively yes for sprint default | Yes; page can still use date-range insights without a team | No merged behavior; one team only. Auto-selects team only when exactly one team exists. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:769-815`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:894-925` |
| `SprintExecution` | Selected team’s sprint list, `TimeFrame == "current"` preferred, else date overlap, else latest past | Yes | No default data path without team | No merged behavior; one team only. Auto-selects the first team. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:503-518`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:547-560` |
| `PortfolioDelivery` | Selected team’s sprint list, `TimeFrame == "current"` preferred, else date overlap, else latest past; builds 5-sprint window | Yes | No default data path without team | No merged behavior; one team only. Auto-selects the first team. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor:523-547`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor:575-591` |
| `PortfolioProgressPage` | Selected team’s sprint list, `TimeFrame == "current"` preferred, else date overlap, else latest past; builds 5-sprint window | Yes | No default data path without team | No merged behavior; one team only. Auto-selects the first team. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:639-665`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:679-713` |
| `BacklogHealthPanel` / legacy Analysis workspace | Backend `SprintWindowSelector`: date overlap, else earliest future, else latest past | No | Yes | Yes; the sprint set comes from iteration paths across loaded work items and matched sprint metadata, not from a single team context. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs:61-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:21-105` |
| `ProductRoadmaps` / `MultiProductPlanning` | `SprintCadenceResolver` uses current sprint only as a fallback cadence source | No explicit team selection | Yes, but product must have at least one team for team-derived cadence | Product cadence uses `product.TeamIds.First()`, so only the first team contributes. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/SprintCadenceResolver.cs:18-67`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1539-1555`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:685-700` |

## 3. Inconsistencies

1. **Backend authoritative current-sprint lookup is team-scoped and `TimeFrame`-aware, but many pages ignore `TimeFrame`.**  
   The repository endpoint prefers `TimeFrame == "current"` before dates, while `PipelineInsights`, `PrOverview`, `PrDeliveryInsights`, `DeliveryTrends`, and `SprintTrend` recompute current locally from dates only. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:45-72`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:694-710`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:805-821`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:904-920`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:418-431`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1193-1208`

2. **Team-selection policy differs by page.**  
   `SprintExecution`, `PortfolioDelivery`, and `PortfolioProgressPage` silently auto-select the first available team; `PipelineInsights`, `PrOverview`, and `PrDeliveryInsights` auto-select only when exactly one team exists; `DeliveryTrends` and `SprintTrend` work without a team by merging teams instead. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:508-517`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor:531-542`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:645-660`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:658-662`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:667-676`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor:782-790`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:77-118`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1064-1095`

3. **Fallback behavior is not consistent.**  
   The repository returns `null` if no current sprint exists; most team pages fall back to the most recent past sprint; `PipelineInsights`/PR pages then fall back again to the first item; `SprintWindowSelector` instead promotes the earliest future sprint before the latest past sprint. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:60-72`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:703-708`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:814-821`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:48-69`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:170-196`

4. **Some surfaces merge multiple teams; some surfaces assume exactly one team.**  
   Home signals and product-bar metrics aggregate across multiple team calendars, while execution/delivery/progress pages require one team selection to avoid ambiguity. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:88-120`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:109-149`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:508-518`

5. **Some product-level logic quietly collapses product context to the first team.**  
   `ProductRoadmaps` and `MultiProductPlanning` take `product.TeamIds.First()` before resolving cadence, so product scope is not enough when a product has multiple teams with different sprint calendars. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1539-1555`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:685-700`

## 4. Edge-case behavior

### No active sprint exists

- Team endpoint: returns `null`/404. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:60-72`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SprintsController.cs:57-63`
- Team pages: usually fall back to the most recent completed sprint; some then fall back to the first loaded sprint. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:703-708`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:814-821`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:695-700`
- Home product bar: returns `null` sprint progress. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:133-147`
- Backlog health selector: promotes earliest future, else latest past, so it still manufactures a “current slot”. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:52-69`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:174-196`

### Multiple active sprints exist across teams

- This is explicitly supported by `WorkspaceSignalService`, which loads one current sprint per team and merges them. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:416-425`
- Home product-bar metrics also treats this as normal and averages progress percentages across all active team sprints. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:121-149`
- `DeliveryTrends` and `SprintTrend` use a merged sprint list, so whichever sprint happens to win the ordering/date heuristic becomes “the” current sprint for the page. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor:405-431`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor:1183-1209`

### Sprint calendars differ across teams or products

- Product and workspace-level aggregation can combine teams with different sprint windows. The code does not normalize them to a single calendar first. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:103-119`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:109-149`
- Product cadence pages sidestep the ambiguity by taking only the first team, which avoids merging but makes the result dependent on team ordering. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1541-1549`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/MultiProductPlanning.razor:687-695`

### Sprint data is missing or outdated

- Missing sprint metadata on team pages leads to empty sprint selectors or to fallback defaults based on whatever list remains. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:536-544`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor:829-833`
- `SprintWindowSelector` creates placeholder future slots when data is incomplete, so downstream UI still shows a window even without real sprint records. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:34-45`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:91-102`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:219-229`

## 5. Feasibility verdict

**Can “current sprint” be a reliable global default? No.**

Reason:

- The authoritative backend definition is **team-scoped**. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:45-72`
- Several important surfaces already behave as if multiple simultaneous current sprints are normal. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:103-119`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:121-149`
- The client contains multiple incompatible fallback rules, so a single global default would not match current behavior across pages. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor:694-710`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor:695-700`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Metrics/Services/SprintWindowSelector.cs:52-69`

**Can it be uniquely defined across the system? No.**

- It is unique only when the scope has exactly one team calendar in play.
- Once multiple teams are in scope, the system either merges multiple current sprints, averages them, or arbitrarily picks one from a merged list.

**Can it be used without team context? Only as an explicitly aggregated heuristic, not as a singular, reliable meaning.**

## 6. Required context

Minimum context for correct resolution:

1. **Team ID** for any singular “current sprint” lookup.  
   That is the only direct authoritative path in the system. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:45-72`
2. **Fresh team iteration data** (`path`, dates, optional `timeFrame`) synced for that team. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs:367-403`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Sync/TeamSprintSyncStage.cs:106-118`

When team is not provided:

- **Product alone is insufficient** if the product is linked to multiple teams, because product-level code either merges multiple teams or silently chooses `TeamIds.First()`. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:103-119`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1541-1549`
- **Project alone is insufficient** because sprint persistence and current-sprint lookup are not project-scoped; they are stored and queried per team. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/SprintEntity.cs:17-23`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/SprintRepository.cs:45-72`
- If team context is intentionally absent, the system needs an **explicit aggregation rule** (“all current sprints for scoped teams”, “average progress across active team sprints”, “pick first team”) rather than the term “current sprint” alone.

## 7. Risks if forced globally

1. **Hidden ambiguity**  
   Users would see “current sprint” while different pages mean different things: one team’s sprint, a merged multi-team sprint, the latest past sprint, or an averaged progress number.

2. **Silent wrong defaults**  
   First-team fallback (`TeamIds.First()` or first team auto-select) can make a product appear to have a single current sprint when it actually has several. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor:508-512`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor:1541-1549`

3. **Cross-page inconsistency**  
   The same user context can land on pages where “current sprint” means:
   - exact team current from `TimeFrame` or dates,
   - most recent past sprint,
   - earliest future sprint promoted to current,
   - a merged multi-team active sprint set.

4. **Misleading portfolio-level analytics**  
   Multi-team workspaces can compare or summarize different sprint calendars as if they were one sprint. `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/WorkspaceSignalService.cs:103-119`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetHomeProductBarMetricsQueryHandler.cs:121-149`

5. **Poor UX explainability**  
   On aggregated surfaces, users cannot infer whether “current sprint” refers to their team, the product’s first team, all scoped teams, or a fallback past/future sprint without reading the implementation.

## Bottom line

The codebase does **not** support a single globally reliable meaning of “current sprint”. The structurally reliable unit is **team-scoped current sprint**. Outside that, the system already relies on context-specific aggregation or fallback behavior, so a global default would be ambiguous and frequently misleading.
