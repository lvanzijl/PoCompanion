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
| Bug Trend | STATIC | Fixed title/subtitle only. No badge, count, or trend value on the tile. |
| PR Trend | DYNAMIC | Adds a `TrendSlopeBadge` driven by runtime PR trend data when enough sprint context exists. |
| Pipeline Insights | STATIC | Fixed title/subtitle plus a static `Read-only` chip. No live pipeline health indicator on the tile. |
| PR Delivery Insights | STATIC | Fixed title/subtitle plus a static `Read-only` chip. No live friction/count signal on the tile. |
| Portfolio Progress | STATIC | Fixed title/subtitle only. No runtime signal binding. |
| Delivery Trends | STATIC | Fixed title/subtitle only. No runtime signal binding. |

### Planning

| Tile | Classification | Basis |
|---|---|---|
| Product Roadmaps | STATIC | Fixed title/subtitle. No runtime signal binding. |
| Plan Board | STATIC | Fixed title/subtitle. No runtime signal binding. |

## 2. Dynamic tiles

Only one workspace-hub tile currently behaves as a dynamic, signal-driven tile inside the analyzed scope.

### Trends → PR Trend

- **Code location:** `PoTool.Client/Pages/Home/TrendsWorkspace.razor:161-175`, `:567-612`
- **Dynamic element:** `TrendSlopeBadge`
- **Data source:** `PullRequestsClient.GetSprintTrendsAsync(selectedSprintIds, productIds: null, teamId: _selectedTeamId, ...)`
- **Driving values:** `_prTtmSlopeStart` and `_prTtmSlopeEnd`, taken from the first and last available `MedianTimeToMergeHours` values in the selected sprint range
- **Trigger condition:**
  - a team must be selected
  - both `fromSprintId` and `toSprintId` must be selected
  - sprint trend data must load successfully
  - at least one usable median time-to-merge value must exist at the start/end of the selected range
- **Communicated signal:** whether median PR time-to-merge is **improving**, **stable**, or **worsening**
- **Signal meaning for navigation:** this is the only tile that tries to answer *why click now?* with an explicit flow-efficiency cue rather than only naming the destination page
- **Important rendering rule:** if the required sprint context or data is missing, the badge disappears entirely and the tile falls back to a generic static card

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
- **Trends:** positioned as a signal workspace, but only one tile actually behaves like a signal-driven tile.

## 4. Issues found

### Unclear or weak dynamic intent

- **PR Trend is the only truly dynamic tile, but its signal is conditional and often absent.**  
  When no full sprint range is selected, the tile loses its strongest “why click?” signal and becomes a generic destination card.

### Static tiles that may appear signal-driven without actually being so

- **Trends labels the section as “Trend Signals,” but 5 of the 6 tiles are static.**  
  This creates a mismatch between the page framing and the actual behavior of the cards.
- **Pipeline Insights** and **PR Delivery Insights** sound like insight/signal surfaces, but the tiles do not expose any live indicator, count, or anomaly.

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

- The strongest signal-led navigation in the analyzed scope is concentrated in one place: **Trends → PR Trend**.
- Elsewhere, navigation is primarily **taxonomy-driven** (“go to this page type”) rather than **signal-driven** (“something needs attention here”).

## 5. Risk assessment

### Must not change

- **Health hub should remain lightweight and non-loading on entry.**  
  `docs/NAVIGATION_MAP.md` explicitly says the Health workspace hub “loads no Health data on entry” and only routes to subpages.
- **PR Trend dynamic badge should remain dynamic.**  
  It is the only current example of a runtime signal affecting click intent on a hub tile.
- **Current tile titles align closely with docs and user guide terminology.**  
  Changing labels casually would risk breaking navigation comprehension and documentation consistency.

### Safe to improve later

- Clarifying **why to click** for static tiles without changing routing behavior
- Improving subtitle precision for the weaker Trends tiles
- Making the distinction between PR-focused and delivery-focused trend tiles more explicit
- Reconsidering whether the Trends section should be called “Trend Signals” if most cards remain static

### Needs redesign later

- **Trends workspace signal strategy**  
  The workspace is framed as a signal entry point, but most tiles behave like static navigation labels. A later redesign should choose one of two directions:
  - make more tiles genuinely signal-driven, or
  - rename/reframe the section so it no longer implies live signals on every card
- **Conditional visibility of PR Trend’s badge**  
  The signal is valuable, but it is gated behind team + sprint-range selection. That makes the tile’s intent fluctuate between strong and weak.

### Wording changes that could break UX intent

- **Health → Overview** should be changed carefully because “Overview” is now the documented canonical path for the moved Build Quality experience.
- **Delivery → Sprint Delivery / Sprint Execution** should keep their separation explicit; the guide repeatedly distinguishes stakeholder reporting from internal diagnostics.
- **Planning → Product Roadmaps / Plan Board** should preserve the current future-oriented mental model.

## 6. Summary

Overall hub quality is **structurally clear but weakly signal-driven**.

- **Health, Delivery, and Planning** behave as intentional static navigation hubs.
- **Trends** is the only workspace that meaningfully attempts signal-led navigation, but only **PR Trend** currently delivers that behavior.
- Most analyzed tiles tell the user **what page exists**, not **why this page deserves attention now**.
- The biggest semantic gap is in **Trends**, where the section framing promises signal cards but most tiles remain static.

In short:

- the hub architecture is understandable
- the routing intent is mostly clear
- genuine signal-driven navigation is rare inside the workspace hubs
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
