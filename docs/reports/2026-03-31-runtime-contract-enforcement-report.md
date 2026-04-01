# Runtime Contract Enforcement

## Current gap confirmation

The current gap is real.

Verification result:

- ASP.NET-style `ActionResult<T>` does **not** coerce the runtime payload to `T`
- if a controller returns `Ok(new { ... })` from an action declared as `ActionResult<CalculateHealthScoreResponse>`, the runtime payload remains the anonymous object
- extra fields are serialized silently from the runtime object shape

This was verified with a focused unit test that returns an anonymous payload from a probe action declared as `ActionResult<CalculateHealthScoreResponse>` and then serializes the actual `OkObjectResult.Value`.

Observed behavior from the verification test:

- runtime JSON contains the extra field
- deserializing that JSON into `CalculateHealthScoreResponse` succeeds for known properties
- reserializing the shared DTO drops the extra field
- therefore the declared action return type alone does **not** guarantee exact runtime payload shape

Practical conclusion:

- Scenario 3 was possible because the API could emit extra fields on the wire while still advertising the shared DTO contract

## Chosen enforcement strategy

Chosen option: **Option A — strong typing enforcement**, implemented as a runtime result filter on the action.

Why this approach was chosen:

- it is the simplest effective runtime safeguard for this endpoint
- it validates the **actual** payload object before serialization
- it blocks anonymous objects and any non-exact runtime type, even if the action signature still says `ActionResult<CalculateHealthScoreResponse>`
- it is more robust than relying on developers to remember to use a helper method consistently

Enforcement rule:

- for successful `ObjectResult` responses from `/api/HealthCalculation/calculate-score`
- the runtime payload type must be **exactly** `PoTool.Shared.Health.CalculateHealthScoreResponse`
- otherwise an `InvalidOperationException` is thrown before serialization

## Implementation details

Files changed:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/EnforceObjectResultTypeAttribute.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/HealthCalculationController.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Controllers/HealthCalculationControllerTests.cs`

Implementation summary:

1. Added `EnforceObjectResultTypeAttribute`
   - inspects `ObjectResult.Value` during result execution
   - only checks success responses (`2xx`)
   - requires exact runtime type equality with the configured shared DTO type
   - throws with a clear contract-violation message if the payload type does not match

2. Applied the attribute to:
   - `/api/HealthCalculation/calculate-score`
   - specifically `HealthCalculationController.CalculateHealthScore`

3. Kept the controller success payload explicitly typed as:
   - `CalculateHealthScoreResponse`

4. Added focused tests for:
   - current gap confirmation: declared `ActionResult<T>` still allows anonymous runtime payload serialization
   - attribute presence on `HealthCalculationController.CalculateHealthScore`
   - exact shared DTO payload is allowed
   - anonymous success payload is rejected
   - non-success responses are ignored by the enforcement filter

## Validation results

Baseline before changes:

- restore, build, and targeted tests passed in the health contract area

Validation after implementation:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~HealthCalculationControllerTests|FullyQualifiedName~BacklogHealthCalculationServiceClientTests|FullyQualifiedName~NswagGovernanceTests"`

Result:

- build passed
- targeted tests passed

Manual runtime verification:

- started `PoTool.Api`
- posted to `http://localhost:5291/api/HealthCalculation/calculate-score`
- response remained:
  - `{"healthScore":50,"totalIssues":5}`

Meaning:

- normal endpoint behavior still works
- the route keeps returning the shared DTO payload shape for the successful path

## Remaining risks

1. **This enforcement is currently applied only to `HealthCalculationController.CalculateHealthScore`.**
   Other endpoints could still have similar runtime drift if they are not covered by the same pattern or equivalent tests.

2. **The filter enforces exact runtime type, not schema equivalence.**
   That is correct for this shared DTO scenario, but broader API contract governance across all controllers would need wider adoption if desired.

3. **A future developer could remove the attribute.**
   The added reflection-based test reduces that risk by making removal visible during test runs.

4. **Non-success responses are intentionally excluded.**
   This avoids interfering with existing error handling, but it also means this enforcement specifically protects successful DTO responses rather than every possible response body.
