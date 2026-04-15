# Trend UI state hardening

## Current vs new UI state handling

### Before

- Trend pages mixed page-local `MudAlert` blocks, direct `DataStatePanel` checks, and null-data fallbacks.
- Several pages only differentiated `NotReady` and `Failed`.
- Invalid or unresolved filter state often returned early without setting a canonical UI state.
- Some valid empty responses were shown through page-local text while other paths surfaced generic unavailable wording.
- Trends workspace tile signals could silently disappear or show generic failure text when the real problem was an invalid filter.

### After

- Trend pages now use one shared trend-state rendering pattern through `TrendDataStateView`.
- Shared state categories are rendered consistently:
  - Loading
  - Success
  - Empty
  - Invalid filter
  - Error
- Unresolved or invalid global filter state is converted immediately into an invalid UI state before any backend call.
- Backend validation metadata continues to flow through `CanonicalFilterMetadataNotice`.
- Workspace trend signals now show explicit invalid-filter badges instead of disappearing or collapsing into generic unavailable text.

## Mapping from backend responses to UI states

### Shared model

- `DataStateViewModel<T>` remains the shared state carrier.
- Added `DataStateViewModel<T>.Invalid(...)` for local invalid/unresolved filter cases that do not come from a backend envelope.
- `TrendUiStateFactory` now maps `PageFilterExecutionGate` blocking results into the shared invalid state.

### State mapping

| Source condition | UI state |
| --- | --- |
| request in flight | Loading |
| response available and payload has usable data | Success |
| response available but payload is structurally empty for the selected period | Empty |
| backend envelope contains `InvalidFields`, or client-side filter gate blocks execution, or range mapping fails | InvalidFilter |
| response failed / exception thrown | Error |
| response not ready | NotReady |

### Notes

- No API contract changes were required.
- Existing backend `RequestedFilter`, `EffectiveFilter`, `InvalidFields`, and `ValidationMessages` were reused.
- Client-side unresolved filter cases now use the same invalid-state model even before the request is sent.

## List of updated pages and components

### Shared components / helpers

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/TrendDataStateView.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStateView.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/TrendUiStateFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/DataStateViewModel.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TrendsWorkspaceTileSignalService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/Components/WorkspaceTileBadge.razor`

### Updated pages

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/DeliveryTrends.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioDelivery.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrOverview.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PrDeliveryInsights.razor`

## State descriptions

### Loading

- Shared loading indicator remains visible while the page request is in flight.

### Success

- Trend charts, summaries, or analytical tables render normally.
- If backend normalization occurred, `CanonicalFilterMetadataNotice` shows requested vs. applied filter differences.

### Empty

- Shared title/message now indicates that the selected period returned no data.
- Canonical text: **No data for selected period**

### InvalidFilter

- Shared title/message now indicates that the selection itself is invalid or unresolved.
- Canonical text: **Invalid filter selection**
- When available, canonical filter notices continue to list invalid fields and validation messages.
- Trends workspace tile signals now show an **Invalid filter** badge.

### Error

- Shared title/message now indicates the request failed to load.
- Canonical text: **Failed to load data**

## Screenshots or descriptions of each state

- Loading: existing progress indicator or loading component shown while request is running.
- Success: charts, summary cards, and tables render as before.
- Empty: shared empty-state panel replaces generic unavailable text when the selected period is valid but has no data.
- InvalidFilter: shared invalid-state panel explains that the current filter selection must be corrected; tile badges show `Invalid filter`.
- Error: shared failure panel replaces generic unavailable text for request or server failures.

## API or contract adjustments

- None.
- The change reuses existing canonical filter response metadata and client-side `DataStateViewModel`.

## Validation

### Commands run

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~TrendUiStateFactoryTests|FullyQualifiedName~DataStateViewModelTests|FullyQualifiedName~TrendsWorkspaceTileSignalServiceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~GeneratedClientStateServiceTests|FullyQualifiedName~GlobalFilter|FullyQualifiedName~MetricsController|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests" --logger "console;verbosity=minimal"`

### Added / updated automated coverage

- `TrendUiStateFactoryTests`
- `DataStateViewModelTests`
- `TrendsWorkspaceTileSignalServiceTests`

## Remaining risks or edge cases

- Some analytical pages still use contextual not-requested prompts before the first valid query; those prompts are intentionally preserved where they explain how to make the page queryable.
- Pipeline Insights still has product-level no-data messaging inside the successful page body for partially empty product sections; page-level empty handling now covers the fully empty result case.
- Manual browser verification is still recommended to confirm final phrasing and visual emphasis for each state in the rendered UI.
