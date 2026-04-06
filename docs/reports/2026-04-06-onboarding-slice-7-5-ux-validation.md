# Slice 7.5 — UX Validation & Signal Clarity

## 1. UX Goal Validation

Slice 7.5 stayed within the read-only onboarding scope and built on top of Slice 7 rather than replacing it.

Validated UX goal:

- a user can now identify within 30 seconds:
  - top blocking issues
  - affected scope
  - reason
  - what should be fixed first

Measurable indicators added to support that:

- a top-blockers panel above the main workspace
- a dedicated problem-first view
- blocker/warning counts in the global summary
- scope-grouped issue lists
- explicit `Fix first` markers on the highest-priority blockers
- one-click navigation from a problem item back to the exact graph entity

Read-only constraints preserved:

- no create/update/delete/import actions
- no wizard/session flows
- no direct TFS calls
- no backend redesign

## 2. Problem-first View

Added new read-only UX elements:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingProblemCard.razor`
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`

Behavior:

- default mode remains the structured graph view
- new top-level mode: problem-first view
- problem-first view renders:
  - Top blockers
  - Other blockers
  - Warnings
- issues are also grouped by impact scope:
  - Global / connection-level
  - Project-level
  - Root-level
  - Binding-level

Each problem item now shows:

- human-readable title
- affected entity
- location
- reason
- severity
- optional impacted-child count
- navigation action back to graph view

## 3. Prioritization Logic

Prioritization remained a safe aggregation of existing signals only.

Derived from existing data only:

- status engine blocking reasons
- status engine warnings
- entity validation state
- entity relationships already returned by Slice 6 / 6.5 reads

No new backend logic was introduced.

Ordering logic:

- severity first: blocking before warning
- then scope: global before project before root before binding
- then impacted visible child count
- then stable label ordering

Resulting UX buckets:

- Top blockers (max 5, marked `Fix first`)
- Other blockers
- Warnings

Implementation files:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/OnboardingWorkspaceModels.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWorkspaceViewModelFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingSignalPresentation.cs`

## 4. Graph View Enhancements

The existing Slice 7 graph view was enhanced, not replaced.

Enhancements:

- blocker and warning entities now receive stronger visual emphasis
- inline summaries now state:
  - `Blocked because ...`
  - `Warning because ...`
- graph sections with visible problems open by default
- healthy sections remain visible but collapse by default to reduce noise
- section headers now include blocker/warning counts when present

Affected graph files:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingEntityCard.razor`

## 5. “Why” Clarity

Human-readable reason mapping now comes from:

- existing backend issue messages where already readable
- UI-only presentation mapping for validation states when raw status alone is insufficient

Readable validation mapping was added in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingSignalPresentation.cs`

Examples of clarity improvements:

- validation failures now render as readable explanations instead of raw enum values
- problem cards always show a `Why:` line
- graph cards show concise inline `Blocked because ...` or `Warning because ...` summaries

No backend semantics were changed.

## 6. Navigation Improvements

Problem-first items now navigate users back into graph view.

Implemented behavior:

- selecting `Show in graph` switches to graph view
- the relevant graph section expands
- the page scrolls to the exact entity anchor

Navigation targets are stable element ids such as:

- `connection-{id}`
- `project-{id}`
- `team-{id}`
- `pipeline-{id}`
- `root-{id}`
- `binding-{id}`

This uses existing client-side navigation/scroll capabilities only.

## 7. Filtering Impact

Verified behavior:

- problem-first view respects the same existing backend-driven filters as Slice 7
- filtered results do not pretend the workspace is healthy when filtered blockers remain
- filtered partial graphs stay explicit and graph-consistent
- empty states remain explicit

Validation evidence:

- filtered blocker and partial-graph cases were added to `OnboardingWorkspaceViewModelFactoryTests`
- no client-side graph repair or hidden recomputation was introduced

## 8. Test Results

Added/updated test files:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/OnboardingWorkspaceViewModelFactoryTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/OnboardingWorkspaceReadOnlyAuditTests.cs`

Covered scenarios:

- problem ordering
- scope and reason visibility
- graph navigation targets
- filtered blockers
- filtered partial graphs
- read-only audit coverage for the new problem card
- feature-flag and route behavior through existing Slice 7 tests

Validation commands:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~OnboardingWorkspaceServiceTests|FullyQualifiedName~OnboardingWorkspaceViewModelFactoryTests|FullyQualifiedName~OnboardingWorkspaceReadOnlyAuditTests|FullyQualifiedName~WorkspaceNavigationCatalogTests|FullyQualifiedName~WorkspaceRoutesTests" --logger "console;verbosity=minimal"`

Targeted result:

- Passed: 40
- Failed: 0
- Skipped: 0

Rendered-state evidence:

- no screenshot artifact was captured in-repo
- rendered UX evidence is covered by the targeted tests and the new problem-first projections

## 9. Backend Impact (if any)

Backend impact: none.

Confirmed:

- no backend API changes
- no backend logic changes
- no new read/write endpoints
- no direct TFS calls from the UI

All Slice 7.5 behavior is projected from existing Slice 3, Slice 6, and Slice 6.5 contracts.

## 10. Governance Compliance

Compliance summary:

- read-only only
- no mutation paths introduced
- feature flag remains `FeatureFlags:OnboardingWorkspace`
- no onboarding wizard/session concepts reintroduced
- no backend redesign
- no import UI
- no hidden write path

Before vs after UX summary:

- before:
  - users could inspect the graph, but problem urgency and exact next focus were slower to detect
- after:
  - users can see top blockers first
  - each problem shows where and why
  - the graph highlights the same issue in context
  - the recommended first fixes are explicitly surfaced without adding any mutation behavior
