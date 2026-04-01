# Multi-Product Planning View

## Layout description

The Multi-Product Planning view renders a vertical stack of product lanes, all aligned to a shared horizontal time axis. Each lane corresponds to one product owned by the active profile. Within each lane, individual epic bars are positioned using the same gradient-based visual semantics as the single-product roadmap view.

The layout structure is:

```
[Global axis header: earliest start ... latest end]
[Pressure zone indicator row]
[Product A header + delayed/missing badges]
  - Epic 1: ~~~~~~~~~~~~~~~████ | dd MMM yyyy
  - Epic 2:         ~~~~~~████  | dd MMM yyyy
  - Epic 3:               (no forecast)
[Product B header]
  - Epic 1: ~~~~~~~~~~████      | dd MMM yyyy
[Product C header]
  - Epic 1:   ~~~████           | dd MMM yyyy
```

Each bar uses Option A gradient rendering (transparent left → solid right), with a 4 px solid end marker at the right edge.

## How alignment works

1. **Individual projections loaded in parallel** — `GET /api/products/{productId}/planning-projections` is called for all products simultaneously. No serial blocking.

2. **Global axis computation** — `RoadmapTimelineLayout.Build(allEpicsAcrossProducts)` is called with all epic inputs flattened across products. This yields a single `AxisStart` (minimum derived start) and `AxisEnd` (maximum `EstimatedCompletionDate`).

3. **Per-product lane positioning** — `RoadmapTimelineLayout.BuildWithSharedAxis(productEpics, globalAxisStart, globalAxisEnd)` positions each product's rows against the global axis. This ensures that a bar at 70% left in Product A refers to the same calendar date as a bar at 70% left in Product B.

4. **Start date derivation** — unchanged from the single-product view: `endDate - (SprintsRemaining × 14 days)` if available, else `endDate - 28 days`. This is a UI-only estimate and is communicated as such in hover tooltips.

## Example with 3 products

Given:

| Product | Epic | EstimatedCompletion | SprintsRemaining |
|---------|------|---------------------|------------------|
| Platform | Auth refactor | 2026-06-01 | 4 |
| Platform | API gateway | 2026-09-15 | 11 |
| Mobile | Onboarding flow | 2026-05-01 | 2 |
| Mobile | Push notifications | 2026-07-15 | 6 |
| Analytics | Dashboard v2 | 2026-08-01 | 8 |
| Analytics | Export API | 2026-06-15 | 5 |

**Global axis:** 2026-04-05 → 2026-09-15

Rendered positions (approx. %):

- Platform / Auth refactor: left ≈ 13%, right ≈ 50%
- Platform / API gateway: left ≈ 47%, right ≈ 100%
- Mobile / Onboarding flow: left ≈ 0%, right ≈ 42%
- Mobile / Push notifications: left ≈ 36%, right ≈ 66%
- Analytics / Dashboard v2: left ≈ 40%, right ≈ 71%
- Analytics / Export API: left ≈ 24%, right ≈ 57%

Without the shared axis, each product's bars would fill 0–100% of its own lane width. The user would perceive Platform/Auth and Mobile/Onboarding as ending at the same time because both appear to end at approximately half their lanes. With the shared axis, they are positioned in real calendar time: Onboarding ends 31 days before Auth refactor.

## Conflicts that become visible

### 1. End-date clusters (pressure zones)

Multiple epics with `EstimatedCompletionDate` within a 14-day window produce an amber pressure zone band in the axis header row. In the example above, Auth refactor (Jun 1), Onboarding flow (May 1), and Export API (Jun 15) create a cluster between May and mid-June. This is not visible in the single-product views.

### 2. Parallel execution collisions

When Product A has a large epic that spans the same calendar window as Product B's most critical epic, both bars overlap in horizontal space. Without the cross-product view this goes unnoticed. With it, the team can see that both epics are expected to require full effort during the same period.

### 3. Trailing-load imbalance

When Product C has all its epics ending before Product A's start, the lanes show a clear sequential dependency that could reveal over-commitment: resources available after Product C completes are theoretically being double-counted in early forecasts for Product A.

### 4. Delayed epics per product

Each lane header shows a "N delayed" chip if any epics have overdue `EstimatedCompletionDate`. Without the multi-product view, delayed counts were only visible by navigating to each product individually.

## Constraints confirmed

- No backend changes
- No cross-product forecasting or aggregation
- No dependency modeling
- No recalculation — all data comes from existing persisted projections
- Pure visualization layer
