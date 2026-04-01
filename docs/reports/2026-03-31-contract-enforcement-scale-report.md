# Contract Enforcement Scaling

## Coverage before

A reflection audit over `PoTool.Api` found **148** controller actions with declared return types of `ActionResult<T>` or `Task<ActionResult<T>>` whose response contract includes a `PoTool.Shared` DTO directly or through collection/query-response wrappers.

Coverage before this change:

- **Protected:** 1 endpoint
- **Unprotected:** 147 endpoints

The only explicitly protected endpoint before scaling was:

- `HealthCalculationController.CalculateHealthScore`

Controllers covered by the audit:

- `BugTriageController` (3)
- `BuildQualityController` (3)
- `CacheSyncController` (5)
- `FilteringController` (5)
- `HealthCalculationController` (1)
- `MetricsController` (23)
- `PipelinesController` (6)
- `PortfolioSnapshotsController` (1)
- `ProductsController` (10)
- `ProfilesController` (6)
- `PullRequestsController` (12)
- `ReleasePlanningController` (20)
- `RoadmapSnapshotsController` (3)
- `SettingsController` (8)
- `SprintsController` (2)
- `StartupController` (4)
- `TeamsController` (5)
- `TriageTagsController` (6)
- `WorkItemsController` (25)

The audited contracts include:

- direct shared DTOs such as `TeamDto`, `ProductDto`, `PipelineBuildQualityDto`
- collection contracts such as `IEnumerable<TeamDto>`, `List<TriageTagDto>`, `IReadOnlyList<EffortEstimationSuggestionDto>`
- shared wrapper contracts such as `SprintQueryResponseDto<T>`, `DeliveryQueryResponseDto<T>`, `PipelineQueryResponseDto<T>`, and `PullRequestQueryResponseDto<T>`

## Endpoints updated

Instead of adding repetitive per-action attributes across 147 endpoints, the API now applies equivalent runtime contract enforcement globally for shared DTO action results.

Implementation changes:

- added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/EnforceSharedDtoActionResultContractFilter.cs`
- added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/ObjectResultTypeContractValidator.cs`
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs` to register the global filter for MVC controllers
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Filters/EnforceObjectResultTypeAttribute.cs` to reuse the shared validator logic

Effective behavior after the change:

- for every successful `ObjectResult` emitted by an action whose declared `ActionResult<T>` contract includes `PoTool.Shared` DTOs
- the runtime payload must match the declared contract type
- concrete contract types are enforced as exact types
- interface/abstract contracts such as `IEnumerable<T>` are enforced as assignable runtime types
- mismatches throw before serialization

This keeps the existing explicit `HealthCalculationController.CalculateHealthScore` attribute protection in place while expanding equivalent protection across the rest of the API surface automatically.

## Safety test added

Added:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/SharedDtoRuntimeContractEnforcementTests.cs`

The new test:

- reflects over all `PoTool.Api` controllers
- finds every HTTP action whose declared `ActionResult<T>` contract includes shared DTOs
- verifies that runtime enforcement exists for that surface
- verifies the global `EnforceSharedDtoActionResultContractFilter` is registered in MVC options
- fails if future shared-DTO endpoints fall outside the enforcement path

This makes the scaled protection durable rather than relying on manual code review alone.

## Final coverage status

Final status after this change:

- **Protected:** 148 / 148 audited shared-contract endpoints
- **Unprotected:** 0

Validation completed:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~SharedDtoRuntimeContractEnforcementTests|FullyQualifiedName~HealthCalculationControllerTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~NswagGovernanceTests"`

Manual sanity verification:

- `POST http://localhost:5291/api/HealthCalculation/calculate-score`
- response remained `{"healthScore":50,"totalIssues":5}`

Remaining limitation:

- this enforcement intentionally targets endpoints whose declared response contract is `ActionResult<T>` / `Task<ActionResult<T>>` with shared DTO contracts
- endpoints returning untyped `IActionResult` are outside this specific audit rule unless they are later given typed shared contracts
