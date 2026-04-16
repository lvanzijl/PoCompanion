# Read-surface systemic enforcement

- Date: 2026-04-16
- Scope: option B only — data-query/read surfaces

## Pattern-based governance rules

- Governance no longer relies on a fixed read-surface allowlist.
- `PoTool.Tests.Unit/Audits/DataStateGovernanceAuditTests.cs` now discovers governed read surfaces by pattern:
  - `PoTool.Client/Pages/Home/**/*.razor` pages with `@page`, excluding page-internal `Components/` and `SubComponents/`, and matching read-surface names such as `overview`, `trend`, `insights`, `execution`, `roadmap`, `planning`, `delivery`, `queue`, `triage`, `health`, `bug`, `board`, and `fix`
  - `PoTool.Client/Components/**/*.razor` files whose names match `*Panel.razor`, `*Overview*`, `*Trend*`, or `*Insights*`, excluding subcomponent-only work-item internals
- Governed read surfaces now fail the audit if their Razor markup contains:
  - manual `_isLoading` render gates
  - direct `LoadingIndicator`
  - direct `ErrorDisplay`
  - inline fallback `MudAlert Severity="Severity.Error"` tied to `_errorMessage` or `_loadError`
  - null-driven rendering branches on canonical data fields

## Exclusion mechanism for action flows

- Explicit exclusions now use a Razor comment marker:
  - `@* DataStateGovernance:Exclude(ActionOnly) *@`
  - `@* DataStateGovernance:Exclude(MutationFlow) *@`
  - `@* DataStateGovernance:Exclude(DialogFlow) *@`
- The audit validates the marker and fails if it is used without mutation/action evidence.
- Applied exclusions:
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor` — action-only fix flow
  - `PoTool.Client/Pages/Home/ProductRoadmapEditor.razor` — roadmap mutation/editor flow

## Migrated pages and panels

### Pages

- `PoTool.Client/Pages/Home/PipelineInsights.razor`
- `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`
- `PoTool.Client/Pages/Home/PrOverview.razor`
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `PoTool.Client/Pages/Home/SprintExecution.razor`
- `PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `PoTool.Client/Pages/Home/BugOverview.razor`
- `PoTool.Client/Pages/Home/HealthOverviewPage.razor`
- `PoTool.Client/Pages/Home/PortfolioDelivery.razor`

### Shared panels

- `PoTool.Client/Components/EffortDistribution/EffortDistributionPanel.razor`
- `PoTool.Client/Components/Metrics/CapacityCalibrationPanel.razor`
- `PoTool.Client/Components/Flow/FlowPanel.razor`
- `PoTool.Client/Components/Forecast/ForecastPanel.razor`

## Removed legacy patterns

- Removed page-level loading/error fallback rendering from the migrated analytics pages.
- Replaced direct `LoadingIndicator`/`ErrorDisplay` usage on governed shared read panels.
- Replaced direct null-gated read rendering with canonical `CanonicalDataStateView` gates on the migrated surfaces.
- Added `CanonicalDataStateViewModelMapper` to unwrap canonical envelope responses into canonical payload states without page-specific fallback branches.
- Moved `PortfolioCdcReadOnlyPanel` out of the portfolio-flow page state wrapper so page-level and panel-level loading/error logic are no longer nested.

## Validation results

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo` ✅
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~DataStateGovernanceAuditTests" --logger "console;verbosity=minimal"` ✅

## Representative UI-behavior validation

- Pipeline Insights, PR Delivery Insights, Portfolio Flow Trend, and Sprint Execution now map query results into one canonical read-state path.
- The migrated pages now route loading, success, empty, invalid-filter, and failure behavior through canonical state instead of page-local `_isLoading`, `_errorMessage`, or raw null checks.
- Shared effort, capacity, flow, and forecast panels now use one canonical read-state wrapper and no longer render direct legacy loading or error surfaces.

## Enforcement status

- Enforcement is now systemic for the governed read-surface patterns above.
- New matching pages or panels inherit governance automatically and fail CI immediately if they reintroduce the blocked legacy fallback patterns.
- Governance is no longer opt-in by explicit file list.

## Remaining technical debt

- Minimal and intentional:
  - workspace entry shells and non-governed action/mutation flows remain outside this option B read-surface rule set by design
  - work-item subcomponents under `PoTool.Client/Components/WorkItems/SubComponents/**` are intentionally excluded because they are detail/action adjuncts rather than core analytics read surfaces
