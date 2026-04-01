# Multi-Product Planning View

## Layout structure

The view uses one shared horizontal time axis and a vertical stack of product lanes:

1. shared axis header with earliest derived start and latest projected end
2. optional pressure-zone overlay row
3. one lane per selected product
4. epic rows inside each lane in roadmap order

Each product lane shows:

- product name
- delayed / missing-forecast summary chips
- timeline rows rendered with the existing roadmap bar styling

The bar rendering reuses the existing independent-epic positioning logic. No chaining is introduced.

## How data is combined client-side

The client reuses the existing per-product endpoint only:

- `GET /api/products/{productId}/planning-projections`

The page flow is:

1. load products for the active profile
2. request planning projections for each product in parallel
3. keep the results in memory per product
4. resolve each lane timeline from those existing projections
5. compute a single global axis from the selected products:
   - `minStart = earliest derived start`
   - `maxEnd = latest EstimatedCompletionDate`
6. rebuild visible lanes against that shared axis

No backend aggregation, DTO change, or forecast recalculation is added.

## Product filter and cluster toggle

The page now includes:

- a multi-select product filter
- a `Show clusters` toggle

Filtering is client-side and rebuilds only the shared-axis composition from already loaded projection data.

The cluster toggle controls whether pressure-zone highlight bands are shown. It does not change forecast data.

## Example with 3 products

Example selected products:

- Platform
- Mobile
- Analytics

Example lane output:

```text
Global axis: Apr 2026 -------------------------------- Sep 2026
Pressure zone:                [ 5 epics ending ]

Platform
  #1 Auth refactor         ~~~~~████
  #2 API gateway                    ~~~~~~~~~████

Mobile
  #1 Onboarding flow   ~~~████
  #2 Push notifications           ~~~~~~████

Analytics
  #1 Dashboard v2                 ~~~~~~~~████
  #2 Export API             ~~~~~████
```

Because all lanes share the same axis:

- overlapping delivery windows across products become visible
- empty time gaps become visible
- groups of nearby end dates become visible

## Pressure-zone visualization

Pressure zones are a UI-only grouping aid.

Rules:

- fixed visual threshold: `14 days`
- based on epic end dates across all selected products
- if multiple epic end dates fall within that window, render an amber vertical band
- tooltip shows the number of epics in the cluster

This is not forecasting logic. It is only a visual grouping layer for management pressure awareness.

## Limitations

- no capacity awareness
- no team-conflict detection
- no dependency graph
- no sequencing or scheduling logic
- no roadmap reordering
- only existing persisted planning projections are shown

## Implementation notes

The current implementation uses the existing `RoadmapTimelineLayout` and shared-axis positioning:

- per-product timelines are first resolved independently
- the earliest start and latest end across selected products define the global axis
- each lane is then rebuilt against that shared axis

This preserves the existing single-product timeline behavior while making cross-product overlap visible.
