# Timeline Visual Semantics — End-Date Authority, Start-Date Uncertainty

## Chosen strategy: Option A (gradient)

Each bar fades from transparent at the left (derived start) to solid at the right (authoritative end date).
A narrow solid end marker at the right edge reinforces end-date dominance.

## Before

Each bar was a uniform solid-colour rectangle from start → end, giving equal visual weight to both edges.

- users could interpret the start edge as a precise anchor
- duration appeared as reliable as completion
- delayed bars were entirely red, highlighting the wrong signal

## After

The bar now communicates data certainty through a right-to-left gradient:

- **right edge (end date)** — solid colour, reinforced by a 4 px solid end marker
- **bar body** — gradient from solid at right → transparent at left
- **left edge (start date)** — no visible anchor; the fade signals estimated origin

### Delayed epics

Only the end marker is rendered in error colour (`--mud-palette-error`).
The gradient body also switches to the error gradient, but the bar body remains visually secondary.
This keeps the focus on the end date being overdue rather than the whole duration.

### Low-confidence epics

The bar opacity is reduced to 0.45.
The gradient shape is preserved so the right anchor remains identifiable, just dimmed.

### Missing forecast

No bar is rendered. A warning badge with "Missing forecast" is shown in its place.

## Hover tooltip

Each bar shows a tooltip on hover that contains:

- `EstimatedCompletionDate` as the primary bolded line
- label: "Projected completion (authoritative)"
- conditional: "Low confidence forecast" (warning colour)
- conditional: "Overdue — completion not yet recorded" (error colour)
- italic note: "Start is estimated from forecast, not actual data."

This makes the data provenance explicit on demand without cluttering the default view.

## Uncertainty communication

| Signal | What it means |
|--------|---------------|
| Solid right edge | End date is authoritative (from persisted forecast) |
| Gradient body | Duration is approximate; start is derived |
| Faded/transparent left edge | Start has no authoritative backing |
| Tooltip note | Explicit provenance statement for the start estimate |
| Low opacity | Low-confidence forecast overall |
| Error end marker | Overdue — end date has passed without completion |

## Files changed

- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`

## Constraints confirmed

- No backend changes
- No new fields
- No forecast recalculation
- No dependency logic
