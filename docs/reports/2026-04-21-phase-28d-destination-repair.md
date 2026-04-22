# Phase 28d destination repair

## Scope

- DEBUG / REPAIR phase only
- No execution hint logic changes
- No CDC slice changes
- No interpretation changes
- No routing definition changes
- No UX structure changes
- Focused only on Sprint Execution destination behavior

## 1. Root cause

### ROOT CAUSE

1. The failing page request was **not** caused by the Sprint Execution API handler.
2. `/api/Metrics/sprint-execution?productOwnerId=1&sprintId=9&productId=1` returned `200 OK` with valid execution data after sync completed.
3. The actual failure happened in the **generated client deserialization path** before the page could consume the response:
   - exception: `System.Text.Json.JsonException`
   - failing path: `$.data.teamLabels`
   - expected client type: `ICollection<KeyValuePair<int, string>>`
   - actual JSON shape: object/dictionary (`{ "4": "Crew Safety" }`)
4. Because the generated Sprint Execution client envelope could not deserialize the canonical label dictionaries, `MetricsStateService.GetSprintExecutionStateAsync(...)` threw, and `SprintExecution.razor` fell into its generic failed-state handler:
   - snackbar error
   - `Failed to load data`

### ROOT CAUSE evidence

- Temporary client probe before the fix:
  - `ApiException: Could not deserialize the response body stream as DataStateResponseDtoOfSprintQueryResponseDtoOfSprintExecutionDto`
  - inner error:
    - `The JSON value could not be converted to System.Collections.Generic.ICollection<KeyValuePair<int,string>>`
    - `Path: $.data.teamLabels`

## 2. Fix implemented

### FIXED

1. Updated the Sprint Execution generated client label extension contract so the canonical label fields deserialize as dictionaries instead of `ICollection<KeyValuePair<int, string>>`.
2. Kept the existing shared mapping flow intact because `Dictionary<int, string>` still satisfies the downstream mapping expectations.
3. Added a regression test that exercises `MetricsStateService.GetSprintExecutionStateAsync(...)` with canonical label dictionaries in the response payload.

## 3. Code references

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/CanonicalFilterLabelExtensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/GeneratedClientStateServiceTests.cs`
- Validation path reference:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintExecution.razor`

## 4. Validation results

### VERIFIED

- Build:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
  - Result: passed

- Focused tests:
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --filter "GeneratedClientStateServiceTests|GetSprintExecutionQueryHandlerTests|MetricsControllerSprintCanonicalFilterTests|GlobalFilterDefaultsServiceTests|ProductPlanningExecutionHintNavigationTests"`
  - Result: passed

- Regression proof:
  - `GeneratedClientStateServiceTests.MetricsStateService_GetSprintExecutionStateAsync_DeserializesCanonicalLabelDictionaries`
  - Result: passed

- Direct client probe after the fix:
  - `MetricsStateService.GetSprintExecutionStateAsync(1, 9, 1)`
  - Result:
    - `State=Available`
    - shared `SprintQueryResponseDto<SprintExecutionDto>` returned successfully

- Live browser validation after sync success:
  - direct route:
    - `/home/delivery/execution?productId=1&teamId=4&sprintId=9&timeMode=Sprint`
  - result:
    - page loaded successfully
    - no failed-state panel
    - no snackbar error
    - rendered `Sprint Execution Summary`
    - rendered `Unfinished PBIs (208)`
    - rendered execution metrics showing:
      - `Committed Story Points = 669`
      - `Delivered Story Points = 0`

- End-to-end click-path validation:
  - source:
    - `/planning/plan-board?productId=1&timeMode=Snapshot`
  - clicked hint:
    - `Execution signal: committed delivery below typical range`
  - destination:
    - `/home/delivery/execution?productId=1&teamId=4&sprintId=9&timeMode=Sprint`
  - result:
    - destination loaded successfully
    - visible context:
      - `Team: Crew Safety`
      - `Time: Sprint 11`
    - visible explanation signal:
      - `Applied filter scope differs from the request`
      - `Applied: \Battleship Systems\Sprint 11`
    - meaningful execution content visible immediately below that message

## Final section

### ROOT CAUSE

- ROOT CAUSE: Sprint Execution failed because the generated client expected canonical label collections as `ICollection<KeyValuePair<int, string>>`, while the API returned dictionary-shaped JSON objects for `teamLabels` and `sprintLabels`.
- ROOT CAUSE: the page failure was therefore a client-side deserialization fault, not a backend data-load fault.

### FIXED

- FIXED: Sprint Execution canonical label fields now deserialize as dictionaries in the generated client extension contract.
- FIXED: the destination page now receives the envelope successfully and renders the anomaly-relevant execution data.
- FIXED: a regression test now covers the canonical-label dictionary response shape.

### VERIFIED

- VERIFIED: solution build passes.
- VERIFIED: focused unit tests pass.
- VERIFIED: temporary client probe now succeeds instead of throwing.
- VERIFIED: direct Sprint Execution navigation now loads.
- VERIFIED: hint click from Plan Board now lands on a working Sprint Execution page with visible anomaly-relevant diagnostics.
- VERIFIED: requested route sprint id remains aligned to the rendered sprint name (`sprintId=9` → `Sprint 11`).

### RISK

- RISK: the destination still shows an `Applied filter scope differs from the request` banner because the canonical effective iteration scope is more specific than the requested route context; this is informative rather than blocking, but it may still need wording review in a later UX-only phase.
- RISK: the canonical label deserialization issue may also affect other generated client envelopes that still use collection-typed label properties if they receive non-empty dictionary-shaped label payloads.

## GO / NO-GO for re-running Phase 28

- **GO — Phase 28 can be re-run because the Sprint Execution destination now loads successfully from the execution hint and shows meaningful anomaly-relevant execution data.**
