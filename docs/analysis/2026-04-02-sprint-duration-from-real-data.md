# Sprint Duration from Real Sprint Data

## How sprint data is retrieved

The UI now derives sprint cadence from the same product-team sprint source already used by the planning board:

- `ProductDto.TeamIds`
- `SprintService.GetSprintsForTeamAsync(product.TeamIds.First())`

No backend endpoints were added or changed. The roadmap pages reuse the existing sprint metadata already available in the client layer through `SprintDto.StartUtc` and `SprintDto.EndUtc`.

Affected UI surfaces:

- `/home/planning` → Product Roadmaps
- `/planning/multi-product`

## Calculation method for average duration

For each product lane, the UI resolves cadence once and reuses it for all epic bars in that lane.

Selection logic:

1. Load all sprints for the product's first team
2. Filter to completed sprints where:
   - `StartUtc` exists
   - `EndUtc` exists
   - `EndUtc > StartUtc`
   - `EndUtc < now`
3. Order by most recent completion
4. Take up to the last **5** completed sprints
5. Compute:

`AverageSprintDurationDays = mean((EndUtc - StartUtc).TotalDays)`

That duration is then used for start-date derivation:

`StartDate = EstimatedCompletionDate - (SprintsRemaining * AverageSprintDurationDays)`

If `SprintsRemaining` is missing, the UI keeps the existing fallback window shape but now bases it on the resolved cadence instead of a fixed 14-day assumption.

## Fallback behavior

If no valid completed sprints are available:

1. If a valid current sprint exists, the UI uses the current sprint duration
2. Otherwise it falls back to **14 days**

Fallback source is tracked explicitly:

- `CompletedSprintAverage`
- `CurrentSprintFallback`
- `DefaultFallback`

UI behavior:

- any fallback makes the bar render with the low-confidence visual treatment
- default fallback also shows a subtle warning indicator:
  - `Using default sprint duration`

Tooltips now explain the cadence source:

- `Duration based on avg of last N sprints`
- `Duration based on current sprint`
- `Duration based on default sprint duration`

## UI before/after

### Before

- every product lane assumed `14 days` per sprint
- start positions were derived from a fixed constant
- tooltip only stated that the start was estimated
- no distinction between historical cadence and fallback cadence

### After

- each product lane uses real sprint history from its product team
- start positions reflect that product's recent cadence
- cadence is computed once per lane and reused for all epics
- fallback cadence is visually downgraded as low-confidence
- default fallback is explicitly surfaced in the UI
- gradient bars still keep the end date visually dominant

## Validation that the 14-day constant is removed from timeline logic

The fixed `14-day per sprint` assumption has been removed from `RoadmapTimelineLayout`.

What changed:

- `RoadmapTimelineLayout` no longer owns a hardcoded sprint-duration constant
- the layout now receives `RoadmapTimelineBuildOptions.SprintDurationDays`
- each page resolves cadence from real sprint data before building timeline rows

What remains intentionally:

- `14 days` still exists as the **explicit last-resort fallback** in `SprintCadenceResolver`
- the multi-product pressure-zone window remains `14 days` because it is a fixed clustering threshold for "many end dates close together", not a sprint-duration calculation

So the hardcoded sprint-duration assumption is fully removed from visualization layout logic and replaced by real sprint cadence, while preserving the required default fallback behavior.
