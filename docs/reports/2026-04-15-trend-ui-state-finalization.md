# Trend UI state finalization

## Decision on "NotReady" and rationale

### Decision

- `NotReady` was **not kept as a distinct rendered UI state** for trend pages.
- Shared UI rendering now normalizes `NotReady` to **Loading**.
- Invalid or blocked filter state is rendered as **Invalid filter selection** instead.

### Rationale

- `NotReady` created ambiguity because users saw a third non-success state that often meant either waiting on cache materialization or a blocked request path.
- For trend-facing UI, the meaningful user-visible states are now:
  - Loading
  - Success
  - Empty
  - InvalidFilter
  - Error
- This keeps the rendered state model aligned with the hardened trend-state contract while preserving lower-level `DataStateDto.NotReady` internally where services still need it.

### Implementation

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/CacheStatePresentation.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStateView.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/DataStatePanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TrendsWorkspaceTileSignalService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/TrendsWorkspace.razor`

## Page vs section state rules and implementation

### Rule

- **Page-level state is authoritative**
- **Section-level empty states are localized**
- A page stays **Success** when at least one meaningful section has data
- A page becomes **Empty** only when no meaningful page content exists for the valid filter scope

### Implementation

- Pipeline Insights now evaluates page-level renderability separately from per-product empty sections.
- Product sections with no runs keep their local empty message.
- The overall page only falls back to page-level empty when there is no usable global or per-product data.

### Updated composite-page logic

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/PipelineInsights.razor`

### Result

- Partial data no longer downgrades the full page to empty.
- Empty product sections no longer contradict page-level success.

## Invalid filter UX improvements

### Shared additions

- Trend invalid states now carry normalized invalid-field metadata and validation messages.
- Trend pages push that metadata into shared global filter UI state.
- Global filter controls now show:
  - highlighted invalid fields
  - inline error text
  - a shared warning summary above the controls

### Shared implementation

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/TrendUiStateFactory.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GlobalFilterValidationMapper.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/GlobalFilterValidationFeedback.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/GlobalFilterUiState.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/TrendDataStateView.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/GlobalFilterControls.razor`

### Examples

- Missing or unresolved sprint range highlights the shared time controls
- Invalid team/product scope highlights the corresponding team or product control
- Backend invalid fields such as `productIds`, `teamIds`, or time-related fields are normalized to the matching shared filter control
- Shared invalid message text still surfaces guidance such as:
  - “Selected sprint range cannot be resolved”
  - “Select a valid team and sprint range to load …”

## Validation alignment strategy (frontend vs backend)

### Alignment approach

- Backend validation remains authoritative.
- Frontend invalidation is now a **strict subset**:
  - page gate blocking from `FilterStateResolution`
  - unresolved trend range mapping from `GlobalFilterTrendRequestMapper`
  - backend `InvalidFields` and `ValidationMessages`

### Changes made

- Removed the reflection-based client heuristic that guessed invalid scope from any `InvalidFields` property on arbitrary payloads.
- `DataStateViewModel<T>` now extracts canonical filter metadata through known shared response envelopes and uses backend-provided invalid fields/messages directly.
- Shared client invalid-filter state now carries:
  - `InvalidFields`
  - `ValidationMessages`
  - canonical filter metadata when available

### Relevant files

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/DataStateViewModel.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PageFilterExecutionGate.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GlobalFilterTrendRequestMapper.cs`

## Diagnostics added

### Added

- Dev-only trend filter diagnostics now log:
  - frontend-requested filter
  - backend effective filter
  - invalid fields
  - validation messages
  - reason text

### Trigger

- Logging occurs only in development and only when:
  - requested filter differs from effective filter, or
  - invalid fields/messages are present

### Implementation

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TrendFilterDiagnosticsService.cs`
- registered in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Program.cs`
- invoked from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/TrendDataStateView.razor`

## Validation results per page

### Delivery Trends

- Valid range with data → success rendering retained
- Valid range without data → page-level empty
- Invalid range → invalid filter state with time-control highlighting
- Backend failure → error

### Portfolio Delivery

- Valid range with data → success
- Valid range without data → page-level empty
- Invalid range → invalid filter with shared control highlighting
- Backend failure → error

### Portfolio Flow Trend

- Valid range with data → success
- Valid range without data → page-level empty
- Invalid range → invalid filter with shared control highlighting
- Backend failure → error

### Pull Request Insights

- Valid range with data → success
- Valid range without data → empty
- Invalid filter → invalid filter state with shared control highlighting
- Backend failure → error

### PR Delivery Insights

- Valid range with data → success
- Valid range without data → empty
- Invalid filter → invalid filter state with shared control highlighting
- Backend failure → error

### Pipeline Insights

- Valid range with full or partial data → success
- Valid range without any renderable data → page-level empty
- Product with no runs while page has other data → localized section empty
- Invalid filter → invalid filter state with shared control highlighting
- Backend failure → error

### Trends Workspace signals

- Invalid filter → explicit invalid-filter tile status
- Cache not ready → loading tile status
- Failure → failed-load tile status

## Automated validation

### Commands run

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~TrendUiStateFactoryTests|FullyQualifiedName~DataStateViewModelTests|FullyQualifiedName~TrendsWorkspaceTileSignalServiceTests|FullyQualifiedName~PageFilterExecutionGateTests|FullyQualifiedName~CacheStatePresentationTests|FullyQualifiedName~GlobalFilterValidationMapperTests|FullyQualifiedName~TrendFilterDiagnosticsServiceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~GeneratedClientStateServiceTests|FullyQualifiedName~GlobalFilter|FullyQualifiedName~MetricsController|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~ColdCacheNormalizationTests" --logger "console;verbosity=minimal"`

### Added or updated tests

- `PageFilterExecutionGateTests`
- `DataStateViewModelTests`
- `TrendUiStateFactoryTests`
- `TrendsWorkspaceTileSignalServiceTests`
- `ColdCacheNormalizationTests`
- `CacheStatePresentationTests`
- `GlobalFilterValidationMapperTests`
- `TrendFilterDiagnosticsServiceTests`

## Remaining risks or technical debt

- `NotReady` still exists in lower-level data contracts and shared services; it is now normalized in the rendered UI but not yet removed from the broader client/service model.
- Direct `DataStatePanel` callers outside trend finalization still depend on cache-state semantics and were not fully refactored in this task.
- Development diagnostics rely on the response metadata available after successful backend responses; purely client-blocked invalid states do not produce requested/effective backend comparisons because no request is sent.
- Manual browser verification is still recommended to confirm final filter highlighting and inline error emphasis across all pages.
