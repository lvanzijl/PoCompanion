# Capacity Collision Hints

## Detection algorithm

The capacity collision hint is a client-side heuristic layered on top of the existing multi-product planning view.

Step-by-step:

1. keep using the already loaded multi-product planning inputs:
   - selected products
   - each product's `TeamIds`
   - each epic's rendered start and end window on the shared axis
2. build overlap segments from the visible epic bars on the shared axis
3. for each overlap segment, collect:
   - overlapping epic ids
   - product names
   - team ids from the participating products
4. count which teams appear in at least two overlapping epics
5. if a segment has:
   - at least two overlapping epics
   - and at least one shared team
   then mark that segment as a capacity collision hint
6. merge directly adjacent segments when the same overlapping epics and shared teams remain active

This stays heuristic-only. It does not calculate load, effort, velocity, or availability.

## Example scenario

Example:

- Product A uses Team 12
- Product B uses Teams 12 and 18
- Epic A runs from 1 Apr to 10 Apr
- Epic B runs from 5 Apr to 12 Apr

The overlapping window is 5 Apr to 10 Apr.

Because both visible epics overlap in time and both involve Team 12, the UI shows a capacity collision hint for that shared window.

## Visual representation

The view adds a second optional shared-axis overlay:

- pressure zones stay amber
- capacity collisions use a blue/purple neutral band
- each collision band includes a small team/group icon

Tooltip content explains:

- number of overlapping epics
- number of shared teams
- products involved
- teams involved

Each epic that participates in a collision window also gets a subtle row-level hint icon with a tooltip that says the item may compete with other work on the same team(s).

## Difference vs pressure zones

The two signals are independent:

- pressure zone = many epic end dates land near each other
- capacity collision = overlapping epic windows share one or more teams

A window may have one signal, both signals, or neither.

## Limitations

- no capacity truth
- no effort comparison
- no velocity usage
- no staffing or availability modeling
- no dependency or sequencing logic
- products without team mappings are ignored for this hint
- team display currently reflects the mapped team identifiers available in the loaded product data
