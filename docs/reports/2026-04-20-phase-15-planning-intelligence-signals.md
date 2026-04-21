# Summary

- IMPLEMENTED: per-sprint planning intelligence signals on the Plan Board.
- IMPLEMENTED: risk is shown as sprint heat color and confidence is shown as signal intensity.
- IMPLEMENTED: latest planning-impact summaries now call out sprint-level risk/confidence shifts after planning actions.

# Risk signal model

- Risk is interpreted per sprint from four client-side heuristics:
  - active Epic load in that sprint
  - amount of parallel work active in that sprint
  - recent forward pull-in caused by the latest reshaping
  - overlap pressure inside the sprint window
- Output levels:
  - Low = green
  - Medium = yellow
  - High = red
- VERIFIED: this stays client-side and does not change planning engine logic, persistence, recovery, TFS integration, or planning semantics.

# Confidence signal model

- Confidence is interpreted per sprint from:
  - distance from the current planning horizon
  - recent changed/affected Epics still touching that sprint
  - structural instability from parallel-work or overlap changes
- Output levels:
  - High = stronger color
  - Medium = moderately faded
  - Low = visibly faded
- VERIFIED: no single global confidence score was introduced; confidence remains spatial and per sprint only.

# Visualization approach

- IMPLEMENTED: sprint heat cards above the board now use:
  - background color for risk
  - opacity/intensity for confidence
- IMPLEMENTED: readable labels remain on top of muted heat backgrounds so content stays legible in the dark theme.
- VERIFIED: raw counts are not the primary signal; interpreted labels lead the UI.

# Explanation layer

- IMPLEMENTED: inline sprint chips such as:
  - High load
  - Parallel work high
  - Plan frequently changed
  - Low confidence (far future)
- IMPLEMENTED: sprint tooltips explain why a sprint looks strained or uncertain in planning language.
- VERIFIED: explanation text avoids backend/system terminology as the primary wording.

# Integration with existing UI

- IMPLEMENTED: Phase 14 latest-impact feedback now includes sprint-intelligence deltas such as:
  - Sprint N now above normal load
  - Confidence decreased for Sprint N after recent changes
- IMPLEMENTED: reporting-maintenance actions remain separated from planning-signal messaging.
- VERIFIED: TFS reporting refresh does not drive risk/confidence changes.

# Tests added/updated

- IMPLEMENTED: `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - risk classification per sprint
  - confidence classification per sprint
  - combined color/intensity heat output
  - explanation labels and tooltip language
- IMPLEMENTED: updated `PoTool.Tests.Unit/Services/PlanningBoardImpactSummaryBuilderTests.cs`
  - sprint risk/confidence delta messaging
  - maintenance separation regression
- IMPLEMENTED: updated `PoTool.Api.Tests/ProductPlanningBoardClientUiTests.cs`
  - render-model sprint signal coverage

# Verification of unchanged backend behavior

- VERIFIED: no backend project files or planning-engine contracts were changed.
- VERIFIED: `dotnet build PoTool.sln --configuration Release --no-restore` passed.
- VERIFIED: `dotnet test PoTool.sln --configuration Release` passed.
- VERIFIED: targeted regression runs passed:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~ProductPlanningSprintSignalFactoryTests|FullyQualifiedName~PlanningBoardImpactSummaryBuilderTests"`
  - `dotnet test PoTool.Api.Tests/PoTool.Api.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~ProductPlanningBoardClientUiTests`

# Known limitations

- NOT IMPLEMENTED: dependency-driven uncertainty; no cross-team dependency model was added.
- NOT IMPLEMENTED: external data sources; signals use only existing board data and recent board diffs.
- NOT IMPLEMENTED: a whole-board/global confidence score; intentionally excluded.

# Final section

- IMPLEMENTED
  - Per-sprint risk signal
  - Per-sprint confidence signal
  - Heat visualization
  - Explanation chips/tooltips
  - Latest-impact signal deltas
  - Release notes update

- NOT IMPLEMENTED
  - Dependency modeling
  - External uncertainty inputs
  - Any backend planning/TFS semantic change

- BLOCKER
  - None

- GO/NO-GO
  - GO
