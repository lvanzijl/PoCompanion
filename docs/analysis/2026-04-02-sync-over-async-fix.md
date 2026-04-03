# Sync-over-async fix report

## 1. Root cause

Core Gate failed for sync-over-async because the client still contained real blocking async access in these places:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Flow/FlowPanel.razor`
  - `prTask.Result.Data.ToList()`
  - `pipelineTask.Result.Data.ToList()`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
  - `dependencyTask.Result`
  - `projectionTask.Result`
  - `sprintCadenceTask.Result`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor`
  - `StateClassificationService.GetInProgressStatesAsync(parentType).Result`

These usages violated the Blazor WebAssembly client async rules because they synchronously consumed asynchronous work instead of awaiting it.

The reported `r.Result` access in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/PipelineInsightsCalculator.cs` was not `Task.Result`; it was the `PipelineRunDto.Result` enum property. Core Gate also had a false-positive source scan that treated ordinary model properties named `Result` as blocking async access.

## 2. Refactor performed

### Before
- `FlowPanel` and `ProductRoadmaps` awaited `Task.WhenAll(...)` and then still read task results through `.Result`.
- `WorkItemDetailPanel` synchronously blocked on `GetInProgressStatesAsync(...)` inside a synchronous helper used by Razor markup.
- Core Gate used a raw grep for `.Result`, which misclassified DTO/view-model properties such as `PipelineRunDto.Result` and build-quality `Result` properties.

### After
- `FlowPanel` now awaits the completed task variables and reads their returned payloads without `.Result`.
- `ProductRoadmaps` now awaits the completed dependency, projection, and sprint cadence tasks directly.
- `WorkItemDetailPanel` now computes the suggestion through `OnParametersSetAsync()` and stores the awaited `MarkupString` for rendering.
- `.github/scripts/check-sync-over-async.sh` now performs a task-aware source scan so it flags task-like `.Result` usage while ignoring ordinary data properties named `Result`.

### Async propagation path
- `WorkItemDetailPanel.GetParentProgressSuggestion()`
  - replaced with `GetParentProgressSuggestionAsync()`
  - propagated into Blazor lifecycle via `OnParametersSetAsync()`
  - rendered through `_parentProgressSuggestion`
- `FlowPanel.LoadDataAsync()` remained async and now awaits the two metric tasks directly after `Task.WhenAll(...)`
- `ProductRoadmaps` remained async and now awaits each completed task directly after `Task.WhenAll(...)`

## 3. Files modified

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Flow/FlowPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/ProductRoadmaps.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/check-sync-over-async.sh`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-sync-over-async-fix.md`

## 4. Validation

- Local sync-over-async check: PASS (`/home/runner/work/PoCompanion/PoCompanion/.github/scripts/check-sync-over-async.sh`)
- Local build: PASS (`dotnet build PoTool.sln --configuration Release --nologo`)
- Local filtered tests: PASS (`dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`)
- Core Gate result: PASS on the latest post-fix branch run
- API Contract Gate result: unaffected
- Governance Gate result: unaffected

## 5. Residual risk

- The Core Gate `.Result` detection is now task-aware, but it is still a source scan rather than a full semantic analyzer.
- Future client code that blocks on tasks using unusually named variables could still require a follow-up tightening of the gate logic.
- No remaining real `.Result`, `.Wait(`, `GetAwaiter().GetResult()`, or `AsTask().Result` violations were found in `PoTool.Client` after the fix.
