# Domain Rules — Estimation

These rules define how PoTool interprets Story Points and Effort.

## Story Points

Story Points represent relative complexity of deliverable work.

Story Points are used for:

- sprint planning
- velocity
- delivery analytics
- forecasting

Story Points are team-relative.

## Story Point origin

Authoritative Story Points exist only on PBIs.

Story points on the following must be ignored:

- Bugs
- Tasks
- other work item types

## Story Point field resolution

PoTool resolves story points using the following order:

1. StoryPoints
2. BusinessValue
3. Missing estimate

Special rule:

If Story Points = 0 and item is NOT Done → treat as missing estimate  
If Story Points = 0 and item is Done → valid zero-point completion

## Story Point rollup

Rules:

1. If PBIs contain Story Points → sum PBIs
2. If PBIs contain no Story Points → parent estimate may be used as fallback
3. Once any PBI contains Story Points → parent estimate is ignored

Removed PBIs must not contribute to active scope rollups.

## Missing Story Points

Missing estimates must never silently become zero.

PoTool must surface missing estimates in diagnostics or completeness indicators.

## Derived estimates

If some PBIs within a Feature have Story Points and others do not:

DerivedSP = average of sibling PBIs with valid estimates

Derived estimates:

- remain fractional
- are marked as derived
- used only for aggregation or forecasting
- never used for velocity or sprint commitment

## Effort

Effort represents estimated implementation hours.

Effort may exist on:

- Epic
- Feature
- PBI

Effort is primarily used for reporting and diagnostics.

## Effort rollup

Effort uses child precedence.

Rules:

1. If children contain Effort → sum children
2. If no children contain Effort → use parent Effort
3. Once children contain Effort → parent Effort ignored

Rollup stops at the first level where child Effort exists.

## Hours per Story Point

PoTool may calculate:

HoursPerSP = DeliveredEffort / DeliveredStoryPoints

Purpose:

- validate sprint planning load
- calibrate forecasting

This metric is diagnostic only.
