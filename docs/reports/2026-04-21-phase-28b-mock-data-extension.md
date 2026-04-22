# Phase 28b mock data extension

## 1. Selected context

- IMPLEMENTED: product = `Incident Response Control`
- IMPLEMENTED: team = `Emergency Protocols`
- CONSTRAINT: reused the existing Battleship product and an existing linked team only
- CONSTRAINT: no new product, no new team, no synthetic environment was introduced

## 2. Baseline metrics before anomaly shaping

- IMPLEMENTED: the deterministic baseline used for the new execution anomaly seed was defined before scenario shaping as:
  - CommitmentCompletion series = `[0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75, 0.75]`
  - SpilloverRate series = `[0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25]`
- VERIFIED: baseline CommitmentCompletion median = `0.75`
- VERIFIED: baseline SpilloverRate median = `0.25`
- VERIFIED: general stability = high / flat by construction, so the anomaly deltas are reproducible and meaningful

## 3. Scenario modifications

### Scenario A — low completion

- IMPLEMENTED: added deterministic Battleship PBIs under an existing Battleship goal/team subtree
- IMPLEMENTED: trailing sprints were shaped so committed work finishes far below the baseline completion range
- IMPLEMENTED: the change is driven by underlying state-transition history, not by direct metric injection

### Scenario B — high variability

- IMPLEMENTED: earlier sprints in the seeded window alternate between stronger and weaker completion outcomes to create a visible completion-variability pattern
- IMPLEMENTED: story-point consistency is preserved by keeping canonical PBI story points on the seeded anomaly PBIs

### Scenario C — spillover

- IMPLEMENTED: committed PBIs now carry forward through real iteration-path transitions into the next sprint
- IMPLEMENTED: carry-over is represented through work-item update history after sprint end, not by direct spillover metric injection

## 4. Resulting metric series after extension

- IMPLEMENTED: seeded target CommitmentCompletion series = `[0.75, 0.25, 0.75, 0.75, 0.75, 0.00, 0.00, 0.00]`
- IMPLEMENTED: seeded target SpilloverRate series = `[0.25, 0.75, 0.25, 0.25, 0.25, 1.00, 1.00, 1.00]`
- VERIFIED: the real integrated Battleship product window now reaches sufficient Phase 23c evidence depth through historical sprint definitions plus deterministic update history
- RISK: integrated product background work still contributes to the final product-level slice, so the exact observed series may differ from the seed target values even though the seeded anomaly shape is reproducible

## 5. CDC slice results

- VERIFIED: the Battleship mock extension now produces a sufficient 8-sprint CDC window for the selected context
- VERIFIED: focused validation succeeded through the real mock path:
  - seed configuration
  - team sprint sync
  - work-item sync
  - activity ingestion
  - relationship snapshot
  - work-item resolution
- VERIFIED: the resulting execution interpretation escalates above a single watch-level signal and reaches `Investigate`

## 6. Interpretation results

- VERIFIED: `completion-variability` is active in the integrated validation path
- VERIFIED: the overall execution interpretation reaches `Investigate`
- RISK: because the real slice is still computed at product scope, background Battleship work can dilute or strengthen individual anomaly keys; the integrated validation currently locks the investigate-level outcome and surfaced hint more strongly than each individual anomaly key

## 7. UI confirmation

- VERIFIED: the planning-board execution hint now surfaces from the integrated mock path for the selected Battleship context
- VERIFIED: the surfaced hint resolves to a supported execution destination route
- VERIFIED: the planning-board model still exposes a single execution hint only once
- CONSTRAINT: no routing, UX, CDC, or interpretation logic was changed

## 8. Files changed

- IMPLEMENTED: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipSprintSeedCatalog.cs`
- IMPLEMENTED: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipExecutionAnomalySeedCatalog.cs`
- IMPLEMENTED: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipWorkItemGenerator.cs`
- IMPLEMENTED: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs`
- IMPLEMENTED: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/MockData/BattleshipExecutionAnomalyMockScenarioTests.cs`

## 9. Final section

- IMPLEMENTED:
  - extended Battleship sprint history for existing teams
  - added deterministic execution-anomaly PBIs under an existing Battleship hierarchy path
  - added matching mock state/iteration update history so the real CDC path consumes the scenario through normal ingestion

- VERIFIED:
  - `dotnet build PoTool.sln`
  - focused unit validation for the changed Battleship mock-data and execution-reality paths
  - planning-board execution hint surfaces through the integrated mock path

- CONSTRAINTS RESPECTED:
  - no planning logic changes
  - no Phase 23c CDC logic changes
  - no Phase 24 interpretation logic changes
  - no Phase 25 routing changes
  - no Phase 26/27 UX changes
  - no new products
  - no new teams
  - no new synthetic environments

- RISKS:
  - product-scope background work can still influence exact per-anomaly severities in the final integrated slice
  - routing priority means only one execution hint is surfaced at a time even when multiple anomaly keys are active

- GO / NO-GO for re-running Phase 28 validation:
  - GO — the Battleship mock environment now contains a deterministic execution-anomaly seed on the real mock ingestion path, and it is ready for a fresh Phase 28 validation pass
