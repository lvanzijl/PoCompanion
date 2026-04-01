# Planning Timeline Visualization Fix

## Before vs after

### Before

The planning timeline in `ProductRoadmaps.razor` chained roadmap rows together:

- Epic B visually started at Epic A's projected end
- later rows inherited timing from earlier rows
- overlap was hidden
- roadmap order was incorrectly treated like scheduling order

This distorted persisted forecast data because the visualization introduced artificial serialization.

### After

The timeline now uses:

- a shared horizontal time axis per product lane
- roadmap order only as the vertical row order
- independent start/end positions for each epic

Each epic is placed from its own derived start date to its own persisted projected end date, with no reference to previous epics.

## Rendering strategy

Per product lane:

- the lane builds one shared axis from the earliest visible bar start to the latest projected end
- each roadmap epic gets its own row
- rows stay in roadmap order
- bars are positioned by absolute percentage offsets on the shared axis

This allows:

- overlap
- gaps
- earlier-ending lower roadmap items
- non-sequential visual distributions

## How start dates are derived

The UI derives start dates without backend changes:

1. **Preferred available UI signal**
   - there is no epic start-date field currently available to this page, so the visualization does not use option A

2. **Forecast-based derivation**
   - if `SprintsRemaining` exists, start = `EstimatedCompletionDate - (SprintsRemaining * 14 days)`

3. **Fallback window**
   - if `SprintsRemaining` is missing, start = `EstimatedCompletionDate - 28 days`

4. **Safety rule**
   - if a derived range would invert, start is clamped so `start <= end`

No chaining, dependency logic, or forecast recomputation is added.

## Edge cases

### Overlap

Multiple epics can overlap naturally because all rows use the same axis and independent positions.

### Missing data

If forecast data is missing:

- no bar is rendered
- the row shows a missing-forecast placeholder
- the epic card still shows the forecast-unavailable badge

### Inverted or zero-width ranges

If derived start equals or exceeds end:

- the range is clamped to a non-inverted interval
- the rendered bar still gets a minimum visible width

### Low confidence

Low-confidence forecast rows use a dimmed, dashed bar style.

### Delayed items

If the epic is not done and today is past the projected end:

- the epic card remains highlighted as delayed
- the timeline bar is also rendered in delayed styling

## Visual description of corrected layout

For one product lane:

- the top of the timeline shows the left and right axis dates
- each roadmap epic appears on its own row
- `#1 Epic A` may start in March and end mid-April
- `#2 Epic B` may start earlier than Epic A and still end sooner
- `#3 Epic C` may appear later on the roadmap but overlap both

This corrected layout shows roadmap order vertically and forecast timing horizontally, without implying dependencies.

## Files changed for this fix

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/RoadmapTimelineLayout.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RoadmapTimelineLayoutTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## Validation summary

- timeline chaining logic removed
- no backend changes made
- no forecast recalculation added
- shared-axis positioning covered by focused unit tests
