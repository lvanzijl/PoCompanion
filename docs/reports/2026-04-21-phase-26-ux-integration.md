# Phase 26 UX integration

## 1. Summary

- VERIFIED: this phase remains UX-integration design only.
- VERIFIED: this report does not modify planning signals, the Phase 23c CDC slice, the Phase 24 interpretation layer, the Phase 25 routing contract, or runtime UI behavior.
- VERIFIED: no additional signals, anomaly types, or routing options are introduced.
- JUSTIFIED: the execution signal should appear as one board-level inline hint at the top edge of the sprint-heat area, not as a per-sprint element and not as a page-wide banner.
- JUSTIFIED: the execution hint must remain visually weaker than planning heat and stronger than neutral helper text so planning scan order stays dominant.
- OVERLOAD: repeating execution hints per sprint cluster would duplicate meaning across the heatmap and would make execution status compete with planning signals.
- OVERLOAD: a global banner would pull attention away from the board and merge execution diagnostics with planning/status alerts.
- RISK: a single visible hint preserves clarity, but concurrent lower-priority anomalies will be hidden on the planning surface until the user navigates to the routed diagnostic view.

## 2. Placement decision

### 2.1 Selected placement

- JUSTIFIED: use **top of board** placement, but only as a compact inline row anchored immediately above the `Sprint heat` grid and below the sprint-heat explanatory text.
- VERIFIED: this keeps the hint on the planning surface without injecting it into each sprint card.
- VERIFIED: this placement preserves the current scan rhythm:
  1. board header and board status
  2. board-level planning feedback
  3. sprint-heat introduction
  4. execution hint
  5. sprint heat grid
  6. planning tracks

### 2.2 Rejected placements

#### Per sprint cluster

- OVERLOAD: rejected.
- JUSTIFIED: the execution anomalies are interpreted from a multi-sprint execution history, not from one planning sprint cell.
- JUSTIFIED: showing the hint inside every sprint cluster would falsely imply per-sprint planning ownership and would directly compete with heat, sprint labels, and explanation chips.

#### Global banner

- OVERLOAD: rejected.
- JUSTIFIED: a page-wide banner would visually join execution diagnostics with board authority, sync, and planning alerts, which contaminates the planning layer.
- JUSTIFIED: a banner would interrupt top-of-page scanning before the user reaches the planning heat context that gives the hint meaning.

## 3. Visual hierarchy definition

### 3.1 Relative weight

- VERIFIED: the visual order must remain:
  1. planning heat backgrounds and any strongest planning-red states
  2. sprint labels and primary planning chips
  3. the single execution hint
  4. neutral explanatory text

- JUSTIFIED: this places the execution hint below the dominant planning signal and above passive helper copy.

### 3.2 Required visual treatment

- JUSTIFIED: render the hint as one compact, single-line inline strip rather than as a cluster of chips.
- JUSTIFIED: use a muted accent treatment:
  - thin accent edge or subtle icon
  - light surface tint
  - semibold body-sized text
  - no filled warning/error block treatment

- VERIFIED: the hint must not use the same filled red emphasis as strong planning heat.
- VERIFIED: the hint must not look like another sprint explanation chip.
- VERIFIED: the hint must remain clickable as a single target, not as multiple small actions.

### 3.3 Weight rules

- VERIFIED: lower weight than red heat.
- VERIFIED: lower weight than blocking/error planning alerts.
- VERIFIED: higher weight than neutral body text.
- JUSTIFIED: approximately equal to a secondary board-status element is acceptable; equal weight to the dominant planning chips is not.

## 4. Final signal form

### 4.1 Shape

- VERIFIED: the execution signal is exactly one single-line hint.
- VERIFIED: no numbers.
- VERIFIED: no charts.
- VERIFIED: no stacked pills.
- VERIFIED: no inline severity counters.

### 4.2 Phrasing rules

- VERIFIED: every hint starts with the prefix `Execution signal:`.
- VERIFIED: the remainder is a short anomaly phrase in sentence case.
- VERIFIED: phrasing must stay diagnostic and must not reuse planning language such as plan stability, board strain, confidence, or risk.
- VERIFIED: phrasing must not expose internal severity terms such as `Weak` or `Strong`.
- VERIFIED: phrasing must not expose `Watch` or `Investigate` as visible text in the hint body.

### 4.3 Allowed hint texts

- `Execution signal: committed delivery below typical range`
- `Execution signal: delivery consistency outside typical range`
- `Execution signal: direct spillover increasing`

### 4.4 Hover explanation rule

- JUSTIFIED: optional hover text may add one short explanatory sentence only.
- VERIFIED: hover text may explain what looks abnormal and that selection opens the linked diagnostic surface.
- VERIFIED: hover text must not expand into numbers, extra controls, or secondary routing choices.

## 5. Trigger conditions

### 5.1 Appearance rules

- VERIFIED: show the hint only when the overall execution state is `Watch` or `Investigate`.
- VERIFIED: never show the hint for `Stable`.
- VERIFIED: never show the hint for `InsufficientEvidence`.

### 5.2 Maximum simultaneous hints

- JUSTIFIED: maximum simultaneous hints = **1**.
- JUSTIFIED: a single visible hint prevents execution diagnostics from becoming a second chip system above the heatmap.

### 5.3 Selection rule when anomalies are active

- VERIFIED: if one anomaly is active, show that anomaly’s hint.
- VERIFIED: if multiple anomalies are active, show only one hint using deterministic arbitration.

- JUSTIFIED: arbitration order:
  1. any `Strong` anomaly outranks any `Weak` anomaly
  2. if severities tie, prefer:
     - `direct next-sprint spillover rate increasing`
     - `commitment completion below typical range`
     - `commitment completion variability high`

- JUSTIFIED: this order favors the most immediately planning-relevant execution symptom first:
  - spillover maps most directly to unfinished scope carrying into the next sprint
  - below-typical committed delivery is the next most actionable execution signal
  - variability is the most historical and least local to immediate plan shaping

## 6. Interaction model

### 6.1 Click behavior

- VERIFIED: clicking the hint performs direct navigation using the Phase 25 routing contract.
- VERIFIED: no intermediate chooser, drawer, menu, or expansion panel is allowed.

### 6.2 Route mapping

- `Execution signal: committed delivery below typical range` → `/home/delivery/execution`
- `Execution signal: delivery consistency outside typical range` → `/home/trends/delivery`
- `Execution signal: direct spillover increasing` → `/home/delivery/execution`

### 6.3 Hover behavior

- JUSTIFIED: hover is optional and secondary.
- VERIFIED: hover text must stay short.
- VERIFIED: hover exists to reduce ambiguity, not to become a second diagnostic surface.

## 7. Failure handling

### 7.1 Insufficient evidence

- VERIFIED: show no execution hint.
- VERIFIED: do not show warning-style, caution-style, or degraded execution placeholder UI.
- JUSTIFIED: insufficient evidence is a coverage limitation, not an execution warning, so silence preserves planning clarity better than a weak warning element.

### 7.2 Conflicting anomalies / multiple active signals

- VERIFIED: do not stack multiple hints.
- VERIFIED: do not show a `+1 more` or similar counter.
- JUSTIFIED: use the single-hint arbitration rule in Section 5.3.
- JUSTIFIED: if hover text is present, it may say that other execution signals may also be active, but it must not enumerate them inline.

### 7.3 Rapid appearance / disappearance

- VERIFIED: the displayed hint must use stable display rules so routine refreshes do not create visible flicker.
- JUSTIFIED: use both:
  - a short appearance debounce after data settles for the current board scope
  - a minimum visible duration once a hint is shown for that scope

- JUSTIFIED: removal should occur only after a later settled refresh for the same scope still indicates no visible hint.
- VERIFIED: changing product/team scope may reset the stabilization window because the surface context changed intentionally.

## 8. Overload analysis

| Design choice | Result | Reason |
| --- | --- | --- |
| One inline board-level hint above the sprint heat grid | ACCEPTABLE | Keeps execution visible but singular; does not duplicate per sprint. |
| Per-sprint execution hint repetition | OVERLOAD | Competes directly with heat, labels, and explanation chips. |
| Global banner at page top | OVERLOAD | Hijacks early scan attention and merges execution with planning alerts. |
| Single-line phrasing with no numbers/charts | ACCEPTABLE | Preserves clarity and prevents diagnostic density on the planning surface. |
| One visible hint maximum | ACCEPTABLE | Avoids stacking and preserves a single escape path into diagnostics. |
| Showing `Stable` as a positive execution hint | OVERLOAD | Adds non-actionable noise to a planning-first surface. |
| Showing insufficient evidence as warning UI | OVERLOAD | Creates caution semantics without actionable evidence. |
| Short hover explanation only | ACCEPTABLE | Reduces ambiguity without turning hover into a second panel. |

### 8.1 Competition with planning heat

- VERIFIED: the selected design does not place execution text inside sprint heat cards.
- JUSTIFIED: this prevents the user from reading execution diagnostics as if they were another planning heat dimension.
- ACCEPTABLE: the selected placement preserves planning heat as the dominant visual explanation of sprint strain.

### 8.2 Added scanning burden

- JUSTIFIED: one board-level line adds limited scan cost because it is encountered once, in a stable place, before the heat grid.
- OVERLOAD: repeating the same signal across sprint clusters would materially increase scan burden without adding new meaning.

### 8.3 Ambiguity risk

- JUSTIFIED: the `Execution signal:` prefix creates a hard semantic boundary between planning and execution layers.
- JUSTIFIED: direct click-through reduces ambiguity about what the user should do next.
- RISK: users may still misread the hint as planning advice unless the visual treatment stays clearly lower than the planning heat system.

## 9. Final section

### VERIFIED

- VERIFIED: the design remains UX-only and does not change Phase 23c, Phase 24, or Phase 25 semantics.
- VERIFIED: the planning surface receives at most one execution hint.
- VERIFIED: the hint never appears for `Stable` or `InsufficientEvidence`.
- VERIFIED: the hint is single-line, chart-free, and number-free.
- VERIFIED: click behavior stays single-step and uses the existing Phase 25 route mapping only.

### JUSTIFIED

- JUSTIFIED: a single inline board-level hint is the best placement because it preserves scan order without contaminating sprint heat cards.
- JUSTIFIED: one visible hint is better than two because this surface is planning-first and must not become a second anomaly dashboard.
- JUSTIFIED: severity-first, then fixed-priority arbitration gives deterministic behavior without adding new anomaly semantics.
- JUSTIFIED: suppressed insufficient-evidence UI preserves clarity better than a non-actionable warning.

### OVERLOAD

- OVERLOAD: per-sprint execution hint repetition.
- OVERLOAD: global banner treatment.
- OVERLOAD: stacked anomaly hints or counters.
- OVERLOAD: warning-style insufficient-evidence UI.
- OVERLOAD: any phrasing that reuses planning heat vocabulary or exposes internal severity/state terms.

### RISKS

- RISK: one visible hint can hide concurrent lower-priority anomalies until the user enters the routed diagnostic view.
- RISK: if the hint styling drifts upward toward chip or alert weight, it will compete with planning heat and undo the separation this phase requires.
- RISK: if hover copy becomes verbose, it will recreate the complexity that this phase intentionally avoids.
- RISK: if future implementation places the hint inside sprint cards instead of above the grid, the design intent of this report will be violated.

### GO / NO-GO for Phase 27 (implementation of UX layer)

- GO: Phase 27 may proceed if implementation preserves the single inline board-level hint, one-hint maximum, Phase 25 direct routing, and the lower-than-heat visual hierarchy defined in this report.
