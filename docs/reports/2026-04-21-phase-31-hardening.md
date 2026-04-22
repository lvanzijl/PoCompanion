# Phase 31 hardening

## Scope

- HARDENING phase only
- UX + minor consistency only
- No CDC slice changes
- No interpretation logic changes
- No routing-definition changes
- Hint placement preserved
- Single-hint constraint preserved
- No new signals
- No new user-facing UI structures
- Preserved:
  - Phase 27 behavior
  - Phase 29 amplification improvements

---

## 1. Wording refinements

### HARDENED

- Standardized all hint messages to the same structure:
  - `Execution signal:` + what happened + time anchor
- Replaced weaker anchors and softer wording with more explicit completed-sprint phrasing:
  - completion below typical → `Execution signal: committed work was not fully delivered in the last completed sprint`
  - completion variability → `Execution signal: delivery was less steady across recent completed sprints`
  - spillover increase → `Execution signal: committed work carried forward in the last completed sprint`

### IMPROVED

- Reduced ambiguity from:
  - `as expected`
  - `recent sprint`
  - `recent sprints`
- Kept the copy neutral, single-line, and non-metric.
- Updated hover copy to match the same completed-sprint framing:
  - `The last completed sprint delivered less than committed. Open Sprint Execution to see unfinished work.`
  - `Recent completed sprints delivered unevenly. Open Delivery Trends to see when this changed.`
  - `The last completed sprint carried committed work into the next sprint. Open Sprint Execution to see unfinished work.`

### UNCHANGED

- The `Execution signal:` prefix remains intact.
- Anomaly-to-destination routing remains unchanged.

### RISK

- `carried forward` is intentionally compact for the visible hint and still depends on hover/destination for the fullest explanation.

---

## 2. Context consistency decisions

### HARDENED

- Singular anomaly hints now consistently reference one completed sprint.
- Trend anomaly hints now consistently reference multiple completed sprints.
- Shared helper construction now enforces the message/explanation pattern instead of repeating ad hoc strings.

### IMPROVED

- The hint family now reads as one intentional system instead of three independently tuned phrases.

### UNCHANGED

- The single-hint arbitration model is unchanged.
- Anomaly priority and tie-break behavior are unchanged.

### RISK

- The visible hint remains intentionally compact; exact sprint/team identity is still delegated to the destination state.

---

## 3. Stability improvements

### HARDENED

- Reduced hint show debounce from `75ms` to `25ms` to tighten the preview-to-loaded handoff.
- Extended the early-preview minimum hold from `250ms` to `350ms` so the hint remains visible longer before the full board takes over.
- Strengthened the hint context key to include the anomaly key as well as product/team/sprint context, reducing stale-hint reuse when the surfaced anomaly changes in-place.

### IMPROVED

- Repeated anomaly navigation felt steadier.
- Returning from non-anomaly to anomaly context surfaced the correct hint again without duplicate rendering.
- The hint felt more deliberate during the loading-to-loaded transition.

### UNCHANGED

- Early preview still renders before the slow board grid resolves.
- Loaded-state hint placement remains directly above sprint heat.

### RISK

- Preview and loaded states are still separate render phases, so very slow response conditions can still expose timing sensitivity even though the transition is steadier.

---

## 4. Destination wording refinement

### HARDENED

- Replaced the symbolic material-difference chip wording:
  - from `Requested ≠ applied`
  - to:
    - `Closest matching sprint context shown` when the time dimension is involved
    - `Closest matching context shown` otherwise

### IMPROVED

- The destination notice now reads more like an intentional fallback explanation and less like a diagnostic symbol.
- Manual validation in Sprint Execution showed:
  - heading still present: `Applied filter scope differs from the request`
  - explanatory chip now present: `Closest matching context shown`

### UNCHANGED

- The notice remains visible.
- Requested/applied detail rows remain visible.
- The page structure is unchanged.

### RISK

- The fallback wording is clearer, but still generic; it explains the behavior better without redesigning the notice.

---

## 5. Edge-case validation

### HARDENED

- Re-checked the following edge flows manually:
  - anomaly context load
  - click through to Sprint Execution
  - switch to non-anomaly context
  - switch back to anomaly context

### IMPROVED

- Anomaly context:
  - hint still appears early during the first loading window
  - wording is more immediately understandable
- Destination:
  - still loads useful explanation data
  - now shows clearer fallback microcopy
- Non-anomaly context:
  - still renders no execution hint
- Return to anomaly context:
  - correct anomaly hint reappears
  - no stale non-anomaly silence persisted
  - no duplicate hint was observed

### UNCHANGED

- Silence behavior for non-anomaly product context remains correct.
- Destination usefulness remains strong.

### RISK

- Manual validation covered the repeated-switch flows requested, but not an automated browser stress loop.

---

## 6. Regression coverage

### HARDENED

- Updated service tests for the refined hint and hover strings.
- Updated navigation/model tests for the anchored hint strings.
- Updated Battleship seeded-anomaly coverage to assert completed-sprint anchors.
- Added a governance audit for `CanonicalFilterMetadataNotice` microcopy.

### IMPROVED

- Regression checks now explicitly guard:
  - completed-sprint wording
  - single-line anchored hint structure
  - preserved hint placement
  - clearer destination fallback wording

### UNCHANGED

- Existing markup audits for:
  - one reusable hint component
  - early preview before board state
  - loaded hint section above sprint heat
  - no movement into track markup
  remain intact and passing.

### RISK

- Markup audits remain structure-focused rather than browser-visual diff coverage.

---

## Validation

### Automated

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore`
- `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/check-sync-over-async.sh`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --no-build --filter "ProductPlanningBoardExecutionHintServiceTests|ProductPlanningBoardClientUiTests"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~PoTool.Tests.Unit.Audits.PlanBoardExecutionHintMarkupAuditTests|FullyQualifiedName~PoTool.Tests.Unit.Audits.CanonicalFilterMetadataNoticeAuditTests|FullyQualifiedName~PoTool.Tests.Unit.Models.ProductPlanningExecutionHintNavigationTests|Name~BattleshipMockScenario_ProducesExecutionAnomaliesAndSinglePlanningBoardHint"`

### Manual

- HARDENED: verified anomaly context still shows the hint before the full board grid resolves.
- IMPROVED: verified visible hint wording now reads as completed-sprint scoped without hover.
- IMPROVED: verified Sprint Execution still explains the anomaly and now shows clearer fallback microcopy.
- UNCHANGED: verified non-anomaly context remains silent.
- HARDENED: verified repeated navigation anomaly → non-anomaly → anomaly did not leave stale or duplicate hint state.

---

## Final section

### HARDENED

- HARDENED: completed-sprint wording across all anomaly types
- HARDENED: hover wording consistency
- HARDENED: timing/stability settings for hint handoff
- HARDENED: destination microcopy for fallback context
- HARDENED: regression coverage for hint and filter-notice copy

### IMPROVED

- IMPROVED: immediate understandability
- IMPROVED: consistency of scope framing
- IMPROVED: perceived stability during repeated use
- IMPROVED: destination trust through clearer fallback explanation

### UNCHANGED

- UNCHANGED: CDC interpretation behavior
- UNCHANGED: routing
- UNCHANGED: hint placement
- UNCHANGED: single-hint constraint
- UNCHANGED: anomaly silence behavior in non-anomaly contexts
- UNCHANGED: downstream destination usefulness

### RISK

- RISK: timing is steadier, but the architecture still uses separate preview and loaded render phases
- RISK: destination fallback wording is clearer but still intentionally brief
- RISK: manual repeated-switch validation was successful, but no automated browser stress harness was added

---

## Next step

Transition to UX overhaul track (separate initiative)
