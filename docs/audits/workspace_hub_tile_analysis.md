# Workspace Hub Tile Analysis

## 1. Tile classification

Scope analyzed:

- `PoTool.Client/Pages/Home/HealthWorkspace.razor`
- `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`
- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `PoTool.Client/Pages/Home/PlanningWorkspace.razor`
- `docs/NAVIGATION_MAP.md`
- `docs/GEBRUIKERSHANDLEIDING.md`

Notes:

- This audit covers the tile cards inside the Health, Delivery, Trends, and Planning workspace entry pages.
- Cross-workspace buttons were inspected for surrounding intent but are not classified as tiles.
- Home `/home` workspace tiles are not part of this issue scope.

### Health

| Tile | Classification | Basis |
|---|---|---|
| Overview | STATIC | Fixed title/subtitle. No runtime badge, count, warning, or conditional rendering on the tile. |
| Validation Triage | STATIC | Fixed title/subtitle. Tile itself carries no live issue count or severity marker. |
| Backlog Health | STATIC | Fixed title/subtitle. No runtime signal on the tile. |

### Delivery

| Tile | Classification | Basis |
|---|---|---|
| Sprint Delivery | STATIC | Fixed title/subtitle. No runtime signal binding. |
| Portfolio Delivery | STATIC | Fixed title/subtitle. No runtime signal binding. |
| Sprint Execution | STATIC | Fixed title/subtitle. No runtime signal binding. |

### Trends

| Tile | Classification | Basis |
|---|---|---|
| Bug Trend | DYNAMIC | Adds a `TrendSlopeBadge` when recent bug-creation periods are available, otherwise falls back to a runtime status badge. |
| PR Trend | DYNAMIC | Adds a `TrendSlopeBadge` driven by runtime PR trend data when enough sprint context exists. |
| Pipeline Insights | DYNAMIC | Adds a runtime `WorkspaceTileBadge` based on Build Quality evidence when a stable/unstable/no-data signal is available. |
| PR Delivery Insights | STATIC | Fixed title/subtitle plus a static `Read-only` chip. No live friction/count signal on the tile. |
| Portfolio Progress | STATIC | Fixed title/subtitle only. No runtime signal binding. |
| Delivery Trends | STATIC | Fixed title/subtitle only. No runtime signal binding. |

### Planning

| Tile | Classification | Basis |
|---|---|---|
| Product Roadmaps | STATIC | Fixed title/subtitle. No runtime signal binding. |
| Plan Board | STATIC | Fixed title/subtitle. No runtime signal binding. |

## 2. Dynamic tiles

Three workspace-entry tiles currently behave as dynamic, signal-driven tiles inside the analyzed scope.

### Trends → Bug Trend

- **Code location:** `PoTool.Client/Pages/Home/TrendsWorkspace.razor:151-175,435-519`
- **Dynamic elements:** `TrendSlopeBadge` or `WorkspaceTileBadge`
- **Data source:** bug-creation series derived from `WorkItemService.GetAllWithValidationAsync()`
- **Driving values:** the two most recent monthly bug-created counts
- **Trigger condition:**
  - bug data must load successfully for the selected date range
  - at least two usable periods produce a slope badge
  - one usable period produces an `Insufficient data` fallback badge
  - no usable periods produce a `No data` fallback badge
- **Communicated signal:** whether recent bug creation is **decreasing**, **stable**, or **increasing**
- **Rendering rule:** the tile remains visible even if the signal cannot be calculated

### Trends → PR Trend

- **Code location:** `PoTool.Client/Pages/Home/TrendsWorkspace.razor:178-203,646-699`
- **Dynamic elements:** `TrendSlopeBadge` or `WorkspaceTileBadge`
- **Data source:** `PullRequestsClient.GetSprintTrendsAsync(selectedSprintIds, productIds: null, teamId: _selectedTeamId, ...)`
- **Driving values:** first and last usable `MedianTimeToMergeHours` values in the selected sprint range
- **Trigger condition:**
  - a team must be selected
  - both `fromSprintId` and `toSprintId` must be selected
  - sprint trend data must load successfully
  - at least two usable medians produce a slope badge
  - one usable median produces an `Insufficient data` fallback badge
  - load failures produce a `No data` fallback badge
- **Communicated signal:** whether median PR time-to-merge is **improving**, **stable**, or **worsening**
- **Rendering rule:** the tile remains visible and degrades gracefully when sprint context or data is missing

### Trends → Pipeline Insights

- **Code location:** `PoTool.Client/Pages/Home/TrendsWorkspace.razor:206-221,701-726`
- **Dynamic element:** `WorkspaceTileBadge`
- **Data source:** `BuildQualityService.GetRollingWindowAsync(...)`
- **Driving values:** Build Quality success-rate evidence for the current date range and optional product scope
- **Trigger condition:**
  - an active profile must exist
  - Build Quality evidence must contain usable success-rate data
  - enough evidence returns `Stable` or `Unstable`
  - missing or unavailable evidence returns `No data`
- **Communicated signal:** whether recent pipeline behavior is **stable** or **unstable**
- **Rendering rule:** the tile remains navigable even when only a fallback badge is available

### Signal meaning for navigation

- **Bug Trend**, **PR Trend**, and **Pipeline Insights** each try to answer *why click now?* with an explicit runtime cue rather than only naming the destination page.
- The remaining Trends tiles stay intentionally static and serve as normal navigation cards inside the signal workspace.

### What is not dynamic, even if the page around it is dynamic

- Trends header filters and chips (`Team`, `From Sprint`, `To Sprint`, `Last 6 Months`, `Product Filter`) are dynamic page controls, but they are not tile signals.
- The three `Read-only` chips in Trends are static labels, not data-driven indicators.
- The bug chart below the Trends tiles is dynamic and navigable, but it is not a tile.

## 3. Intent evaluation

### Stronger tiles

These tiles communicate a concrete user reason to click, not just a destination:

- **Health → Validation Triage**  
  Strong because it implies follow-up work on validation issues.
- **Health → Backlog Health**  
  Strong because it points to readiness/refinement detail, which is a clear PO decision activity.
- **Delivery → Sprint Delivery**  
  Strong because it directly answers “what landed?”
- **Delivery → Sprint Execution**  
  Strong because it implies diagnosis of churn and execution behavior.
- **Planning → Plan Board**  
  Strong because “place upcoming work into target sprints” is action-oriented.
- **Trends → PR Trend**  
  Strong only when the slope badge is visible; then it communicates a reason to investigate.

### Weaker tiles

These tiles mostly communicate *what the page is* rather than *why now*:

- **Health → Overview**  
  “Review the current Build Quality summary” identifies content, but the hub tile does not expose whether Build Quality is healthy or degraded.
- **Delivery → Portfolio Delivery**  
  Explains scope, but not what condition should make the user prefer it now.
- **Trends → Bug Trend**  
  Generic; it names the topic without surfacing any anomaly.
- **Trends → Pipeline Insights**  
  The subtitle is domain-specific, but still abstract without a current stability cue.
- **Trends → PR Delivery Insights**  
  Explains classification, but not the problem it helps solve.
- **Trends → Portfolio Progress**  
  Strategic and broad; low urgency and weak click trigger.
- **Trends → Delivery Trends**  
  Describes a metric family, not a decision trigger.
- **Planning → Product Roadmaps**  
  Understandable, but mostly descriptive rather than urgency- or signal-driven.

### Intent pattern by workspace

- **Health, Delivery, Planning:** mostly deliberate static navigation hubs. They prioritize clear routing over live status.
- **Trends:** positioned as a signal workspace and currently uses dynamic tile signals only where a real runtime signal exists.

## 4. Issues found

### Unclear or weak dynamic intent

- **Dynamic intent is concentrated in only part of the Trends grid.**  
  Bug Trend, PR Trend, and Pipeline Insights now carry runtime signals, but the other three Trends tiles remain descriptive navigation cards.

### Static tiles that may appear signal-driven without actually being so

- **Trends labels the section as “Trend Signals,” but half of the grid remains static.**  
  This is acceptable under the current rules because the workspace allows dynamic tiles only where a real signal exists, but the wording still sets a high expectation for live indicators.
- **PR Delivery Insights** sounds like an insight surface, but the tile does not expose any live indicator, count, or anomaly.

### Static tiles that should become dynamic?

- **Health hub tiles should not automatically be treated as missing signals.**  
  The documented intent in `docs/NAVIGATION_MAP.md` is that `/home/health` stays lightweight and loads no Health data on entry. Making those tiles dynamic would cut against the current hub contract.
- **Delivery and Planning hubs are also intentionally lightweight navigation pages.**  
  Their current static behavior is consistent with the docs and does not look accidentally incomplete.
- **Trends is the only workspace where more dynamic tile behavior seems semantically aligned with the page intent.**

### Duplicated or overlapping purpose

- **PR Trend** and **PR Delivery Insights** are both PR-oriented entries inside the same grid, but the distinction is not obvious from the tile wording alone.
- **Portfolio Progress** and **Delivery Trends** both sound historical/analytical; the difference is real, but the tile text requires prior domain knowledge.

### Tiles with no clear purpose

- No tile is completely purposeless, but several Trends tiles are **under-explained**:
  - Pipeline Insights
  - PR Delivery Insights
  - Portfolio Progress

Their subtitles identify topic areas, but not the decision or problem that should pull the user in.

### Navigation-impact observation

- The strongest signal-led navigation in the analyzed scope is concentrated in the **Trends** workspace.
- Elsewhere, navigation is primarily **taxonomy-driven** (“go to this page type”) rather than **signal-driven** (“something needs attention here”).

## 5. Risk assessment

### Must not change

- **Health hub should remain lightweight and non-loading on entry.**  
  `docs/NAVIGATION_MAP.md` explicitly says the Health workspace hub “loads no Health data on entry” and only routes to subpages.
- **Health, Delivery, and Planning tiles should remain static.**  
  They are navigation workspaces, not signal dashboards.
- **Bug Trend, PR Trend, and Pipeline Insights should remain dynamic only when a real signal exists.**  
  Their current runtime badges are the implementation examples for signal-driven workspace tiles.
- **Current tile titles align closely with docs and user guide terminology.**  
  Changing labels casually would risk breaking navigation comprehension and documentation consistency.

### Safe to improve later

- Clarifying **why to click** for static tiles without changing routing behavior
- Improving subtitle precision for the weaker Trends tiles
- Making the distinction between PR-focused and delivery-focused trend tiles more explicit
- Reconsidering whether the Trends section should be called “Trend Signals” if the team wants a lower expectation for static cards in the same grid

### Needs redesign later

- **Trends workspace signal strategy**  
  The workspace is framed as a signal entry point, but only half of the tiles currently expose runtime signals. A later redesign should choose one of two directions:
  - make more tiles genuinely signal-driven, or
  - rename/reframe the section so it no longer implies live signals on every card
- **Conditional visibility of PR Trend’s slope badge**  
  The signal is valuable, but its strongest form is still gated behind team + sprint-range selection. The fallback badge helps, but the tile’s urgency still varies with context.

### Wording changes that could break UX intent

- **Health → Overview** should be changed carefully because “Overview” is now the documented canonical path for the moved Build Quality experience.
- **Delivery → Sprint Delivery / Sprint Execution** should keep their separation explicit; the guide repeatedly distinguishes stakeholder reporting from internal diagnostics.
- **Planning → Product Roadmaps / Plan Board** should preserve the current future-oriented mental model.

## 6. Summary

Overall hub quality is **structurally clear but weakly signal-driven**.

- **Health, Delivery, and Planning** behave as intentional static navigation hubs.
- **Trends** is the only workspace that meaningfully uses signal-led navigation, currently through **Bug Trend**, **PR Trend**, and **Pipeline Insights**.
- Most analyzed tiles tell the user **what page exists**, not **why this page deserves attention now**.
- The biggest semantic gap is in **Trends**, where the section framing still promises a stronger all-grid signal story than the current 3 dynamic / 3 static split.

In short:

- the hub architecture is understandable
- the routing intent is mostly clear
- genuine signal-driven navigation exists, but is intentionally limited to the signal workspace
- future improvement should focus first on the **Trends** tile set, not on Health/Delivery/Planning

## Reviewer notes

### What changed

- added this analysis-only audit document at `docs/audits/workspace_hub_tile_analysis.md`

### What was intentionally not changed

- all code
- all UI
- all tile wording
- all routing and signal behavior

### Known limitations / follow-up

- this audit is based on the current workspace entry pages and the two requested documentation sources
- it does not analyze the Home dashboard tiles, which are outside the stated scope
- it does not include a redesign proposal; it only identifies where a later improvement pass should focus
