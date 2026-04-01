# Contract Break Resilience

## Scenario results

### Scenario 1 — Client-side DTO duplication

Attempted mistake:

- created `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/CalculateHealthScoreResponse.cs`
- changed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BacklogHealthCalculationService.cs` to return the new client-owned DTO instead of the shared response type

Observed behavior:

- the solution failed to compile immediately
- the failure came from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Metrics/SubComponents/IterationHealthTable.razor`
- the duplicate type name created ambiguous resolution between:
  - `PoTool.Client.Services.CalculateHealthScoreResponse`
  - `PoTool.Shared.Health.CalculateHealthScoreResponse`

Representative compiler error:

- `CS0104: 'CalculateHealthScoreResponse' is an ambiguous reference between 'PoTool.Client.Services.CalculateHealthScoreResponse' and 'PoTool.Shared.Health.CalculateHealthScoreResponse'`

Result:

- this mistake is exposed at compile time before tests or runtime

### Scenario 2 — Bypass shared contract

Attempted mistake:

- changed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/HealthCalculationController.cs`
- changed the action signature from `ActionResult<CalculateHealthScoreResponse>` to `ActionResult<object>`
- returned an anonymous object instead of the shared DTO

Observed behavior:

- the full solution still compiled successfully
- the existing controller unit test failed immediately
- the generated Swagger contract changed the endpoint response to binary/octet-stream instead of the shared schema
- after refreshing the governed snapshot and running client generation, the client build failed because the generated API client no longer returned the shared DTO

Representative test failure:

- `Assert.IsNotNull failed. 'value' expression: 'response'.`
- from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/HealthCalculationControllerTests.cs`

Representative Swagger change:

- `/api/HealthCalculation/calculate-score` response became:
  - `application/octet-stream`
  - `type: string`
  - `format: binary`

Representative client-generation failure:

- `CS0029: Cannot implicitly convert type 'PoTool.Client.ApiClient.FileResponse' to 'PoTool.Shared.Health.CalculateHealthScoreResponse'`

Result:

- this mistake is not blocked by the compiler at API-change time
- it is exposed at test time
- it is also exposed by the governed Swagger/NSwag flow during client generation

### Scenario 3 — Silent drift attempt

Attempted mistake:

- kept the declared shared return type in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/HealthCalculationController.cs`
- changed the runtime payload to return an anonymous object with one extra API-only field:
  - `DriftSentinel = "api-only-field"`
- did **not** change `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Health/HealthCalculationDtos.cs`

Observed behavior:

- the full solution compiled successfully
- the existing controller unit test failed immediately because the in-memory `OkObjectResult.Value` was no longer a `CalculateHealthScoreResponse`
- Swagger still advertised the shared contract correctly:
  - `/api/HealthCalculation/calculate-score` still referenced `#/components/schemas/CalculateHealthScoreResponse`
- the live HTTP payload contained the undeclared extra field:
  - `{"healthScore":50,"totalIssues":5,"driftSentinel":"api-only-field"}`
- refreshing the governed snapshot and re-running client generation still succeeded because Swagger had not changed

Result:

- the main detection came from controller-level unit testing
- the governed Swagger/client-generation path did **not** detect the wire-level drift because the declared contract remained unchanged
- this is the closest scenario to a silent contract drift risk

## Detection timing

### Scenario 1

- **Detection moment:** compile time
- **First detector:** C# compiler / Razor compilation
- **Detection quality:** strong and immediate

### Scenario 2

- **Detection moment:** test time first, then client-generation/build time
- **First detector:** `HealthCalculationControllerTests`
- **Secondary detector:** Swagger/NSwag-generated client build
- **Detection quality:** good, but not compiler-immediate for the API change itself

### Scenario 3

- **Detection moment:** test time
- **First detector:** `HealthCalculationControllerTests`
- **Swagger detection:** none
- **Client-generation detection:** none
- **Runtime drift visibility:** the extra field is visible on the HTTP payload, but the declared contract and generated client remain unchanged
- **Detection quality:** mixed; strong if controller tests are present, weak if they are absent or not run

## Weak points

1. **API implementation can drift from the declared shared contract without compile-time failure.**
   Scenario 2 and Scenario 3 both compiled successfully after the controller stopped returning an actual shared DTO instance.

2. **The Swagger/NSwag flow protects declared-contract drift, not necessarily runtime-payload drift.**
   Scenario 2 changed the declared contract and was caught by the generated-client flow. Scenario 3 kept the declared contract unchanged, so the same flow stayed green.

3. **Controller-level tests are currently the critical early-warning system for runtime payload drift.**
   In both Scenario 2 and Scenario 3, the first concrete detection was the existing controller test asserting the returned object is actually `CalculateHealthScoreResponse`.

4. **Client-side duplication is caught here because of type-name collision side effects, not because there is an explicit governance rule against manual client-owned duplicates.**
   Scenario 1 was stopped by compile-time ambiguity in the UI layer. That is effective for this exact duplicate-name mistake, but it is not the same as a dedicated ownership audit over hand-written client DTOs.

## Can a developer break the system unnoticed?

**Not easily for the exact mistakes in Scenario 1 and Scenario 2.**

- Scenario 1 failed at compile time.
- Scenario 2 failed in existing tests and also broke the governed client-generation flow.

**Partially yes for Scenario 3, if the controller-level tests are missing or skipped.**

Why:

- the live HTTP payload can contain extra API-only fields not declared in `PoTool.Shared`
- Swagger can remain unchanged if the action signature still advertises the shared DTO
- NSwag/client generation can remain green because it only sees the unchanged declared schema

In this repository, the existing controller test prevented that drift from going unnoticed during the probe. But the experiment shows a real weak point:

- **wire-level payload drift can escape Swagger/NSwag detection when the declared signature stays shared-owned**
- the repository currently relies on test coverage, not generation governance alone, to catch that class of mistake
