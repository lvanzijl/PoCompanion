# Feature development boundary validation report

Timestamp: 2026-03-31T21:40:02.504Z

## Feature chosen

A small but real backlog-health enhancement was implemented through the normal shared → API → client boundary:

- add `TotalIssues` to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Health/HealthCalculationDtos.cs`
- return that value from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/HealthCalculationController.cs`
- consume it in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BacklogHealthCalculationService.cs`
- surface it in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Metrics/SubComponents/IterationHealthTable.razor` as a new **Counted Issues** row on each iteration card

This was chosen because it is a realistic feature change with user-visible value, but it remains small enough to validate the governed contract workflow without unrelated architecture churn.

## Changes made

### Shared contract

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Health/HealthCalculationDtos.cs`

Change:

- `CalculateHealthScoreResponse` now includes required `TotalIssues`

Purpose:

- keep the health-score API contract authoritative in `PoTool.Shared`
- avoid introducing any client-owned duplicate response type

### API

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/HealthCalculationController.cs`

Change:

- the controller now computes `totalIssues` from the request inputs and returns it together with `HealthScore`

Purpose:

- implement the feature at the API boundary without changing infrastructure or adding adapters/shims

### Client

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BacklogHealthCalculationService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Metrics/SubComponents/IterationHealthTable.razor`

Changes:

- added `CalculateHealthScoreDetailsAsync(...)` so the client can consume the richer response directly
- kept `CalculateHealthScoreAsync(...)` by delegating to the richer API call instead of duplicating logic
- iteration cards now show **Counted Issues** next to the existing backlog-health metrics

Purpose:

- exercise real client usage of the shared-owned contract
- keep UI orchestration in the client and calculation ownership in the API/shared boundary

### Tests

Updated/added:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/BacklogHealthCalculationServiceClientTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/HealthCalculationControllerTests.cs`

Changes:

- existing client-service tests now populate `TotalIssues`
- new service test verifies the richer response is returned intact
- new controller test verifies API output includes both `HealthScore` and `TotalIssues`

### User-visible documentation

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

Change:

- added a release-note entry describing the new counted-issues indicator on backlog-health iteration cards

## Steps executed

1. **Established clean baseline**
   - `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --nologo`
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests"`

2. **Implemented the feature through the governed contract path**
   - shared DTO updated first
   - API response updated second
   - client service and UI usage updated third
   - tests updated last

3. **Ran the governed OpenAPI/client flow**
   - started the API locally on `http://localhost:5291`
   - refreshed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
   - regenerated/build-validated the client with:
     - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj --configuration Release --nologo /p:GenerateApiClient=true`

4. **Validated feature and boundary integrity**
   - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v normal --filter "FullyQualifiedName~BacklogHealthCalculationServiceClientTests|FullyQualifiedName~HealthCalculationControllerTests|FullyQualifiedName~NswagGovernanceTests"`
   - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

5. **Checked broader suite behavior**
   - a full `PoTool.Tests.Unit` run still reports 6 failures unrelated to this feature change
   - the failures were inspected and recorded rather than fixed because they are outside this task’s scope

## Issues encountered

### 1. API startup delay before snapshot refresh

The governed snapshot refresh could not run immediately after starting the API because `http://localhost:5291/swagger/v1/swagger.json` was not ready yet. A short wait loop was needed before the snapshot could be fetched successfully.

Impact:

- minor developer friction
- no ownership or architecture violation

### 2. Full unit suite contains unrelated failures

A full rerun of `PoTool.Tests.Unit` still failed with 6 tests unrelated to this feature:

- 4 failures in `DataSourceAwareReadProviderFactoryTests` / `LazyReadProviderTests` due missing `IHttpContextAccessor` test registration
- 2 documentation-governance failures for pre-existing non-compliant markdown files:
  - `2026-04-02-di-activation-audit-and-fix.md`
  - `docs/reports/2026-03-31-ui-exploratory-screenshot-run.md`

These were not introduced by this feature and were intentionally left unchanged.

### 3. Screenshot context was not feature-representative

The locally reachable Analysis Workspace rendered a valid page shell but showed **No Backlog Health Data Available**, so the supplied screenshot URL was not suitable as direct proof of the new counted-issues row.

## Guardrail behavior

### Shared ownership held

Observed behavior:

- `CalculateHealthScoreResponse` remained owned by `PoTool.Shared`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json` changed to include `totalIssues`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs` did **not** need a source diff for this feature

Interpretation:

- the client generation path respected the existing exclusion of shared-owned DTOs
- the feature did not reintroduce duplicate generated contracts

### NSwag governance remained valid

Confirmed by targeted governance tests:

- `CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource`
- `GeneratedClient_DoesNotRecreateSharedPublicTypes`
- `ClientProject_OnlyRunsNswagWhenExplicitlyRequested_AndKeepsGeneratedOutputSeparated`
- `ApiProject_DoesNotReferenceClientProject`

Result:

- all targeted governance tests passed after the feature change

### No duplication or workarounds introduced

Observed:

- no duplicate response DTO was created in client or API
- no handwritten contract shim was added to bypass the shared contract
- client behavior was extended by reusing the existing service boundary instead of introducing parallel code paths

## Developer friction points

1. **Server readiness is easy to underestimate.**
   The governed snapshot flow is clear, but it is easy to run the `curl` step before the local API is actually listening.

2. **The distinction between snapshot refresh and generated-client diffs is subtle.**
   In this feature, the snapshot changed while `ApiClient.g.cs` did not. That is correct, but it can feel unintuitive until the shared-type exclusion model is understood.

3. **UI verification depends on data availability.**
   The workspace route loaded correctly, but mock/sample data did not surface the changed row in the browser. The feature is still validated through build/tests and the shared/API/client chain, but interactive demonstration was weaker than the code path itself.

4. **Broader suite noise makes regression assessment slower.**
   The unrelated full-suite failures require manual triage before a developer can confidently treat the remaining targeted validations as the authoritative result.

## Assessment: is the system usable under real development?

Yes — with some friction, but without workarounds.

Why the system is usable:

- a real user-visible feature was implemented by changing the shared contract once and consuming it cleanly in API and client layers
- the governed OpenAPI snapshot and explicit NSwag generation flow worked as designed
- the shared DTO remained shared-owned; no duplicate generated contract was introduced
- boundary guardrail tests stayed green
- the client consumed the new field without bypassing the repository’s ownership rules

What makes it slightly awkward:

- developers must remember that API readiness gates snapshot refresh
- understanding why the snapshot changes but generated client code may not change requires familiarity with the exclusion-based governance model
- broader pre-existing test noise makes final confidence slower than ideal

Final assessment:

- **ownership boundary:** preserved
- **duplication:** not reintroduced
- **governance:** not bypassed
- **developer workflow:** viable for normal feature work
- **workarounds required:** none
