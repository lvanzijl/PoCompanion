# Contract ownership recovery report

Timestamp: 2026-03-31T19:57:19Z

## Detected prior progress

The interrupted run had already completed the following foundation work before this recovery pass:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json` had been expanded so shared-owned public contract names were excluded from NSwag generation.
- governed NSwag layout was already in place:
  - OpenAPI snapshot: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
  - generated output: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- the earlier report `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-03-31-nswag-governance-report.md` had already documented the intended generated/manual folder split and the initial ownership direction.
- the repository baseline was clean and the pre-recovery checkpoint commit `750e8d23` had not left uncommitted code behind.

What had **not** been finished yet:

- the generated client had **not** been regenerated after the broadened exclusions were added.
- `PoTool.Client/ApiClient/ApiClient.LegacyCompatibility.cs` was still present.
- `PoTool.Api/PoTool.Api.csproj` still referenced `PoTool.Client`.
- test guardrails still reflected the old manual-file list and did not prove zero shared/generated overlap.

## Reconstructed intended ownership model

The recovered ownership model implemented in this run is:

- **Shared canonical owner**: API-facing DTOs, enums, envelopes, and filter/request models defined under `PoTool.Shared/**`
- **Generated client-only owner**: NSwag request/response wrappers, endpoint clients, exceptions, and client-specific transport shapes that do not exist in `PoTool.Shared`
- **Handwritten client-only owner**: the small set of manual envelope helpers and JSON settings adapters under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/*.cs`
- **Manual service owner by domain**:
  - BuildQuality stays intentionally manual through `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`
  - release-planning board access remains manual through `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ReleasePlanningService.cs`
  - generated clients remain the primary transport for the other controller domains, with handwritten partials limited to governed envelope helpers

## Current state classification at recovery start

### DONE before recovery

- governed NSwag layout and explicit generation trigger
- broad shared-type exclusion list in `nswag.json`
- BuildQuality manual-service decision already present

### PARTIAL before recovery

- shared/generated ownership: config was updated, but generated code still recreated shared-owned types because regeneration had not been rerun
- compatibility shim reduction: shim file still existed and still bridged removed generated shapes
- API/client dependency cleanup: API still referenced Client directly
- guardrails: existing tests checked governance basics, but not zero overlap or API-to-client dependency removal

### NOT STARTED before recovery

- final regeneration and compile stabilization after the broadened exclusions
- final recovery report

## Work completed in this recovery run

### 1. Regenerated the client against the broadened shared exclusions

- reran governed generation through `dotnet build /p:GenerateApiClient=true`
- updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- verified the regenerated client now has **zero** public-type overlap with `PoTool.Shared`

### 2. Completed the unfinished shared-contract migration

To make the regenerated client compile after shared-owned DTOs disappeared from `ApiClient.g.cs`, the recovery pass moved the remaining client and test code to canonical shared ownership:

- added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/GlobalUsings.Contracts.cs`
- updated affected client pages/services to use shared-owned DTOs/enums directly
- added compatibility constructors to shared positional records used by existing object-initializer call sites:
  - `WorkItemDto`
  - `WorkItemWithValidationDto`
  - `ProductDto`
  - `ProfileDto`
  - `SettingsDto`
  - `PipelineDto`
  - `PipelineRunDto`
  - `RepositoryDto`
  - `SprintDto`
  - `BacklogHealthDto`
  - `EpicCompletionForecastDto`
  - `DependencyGraphDto`
  - `FixValidationViolationDto`
  - `PullRequestMetricsDto`

These constructors were added to preserve existing caller ergonomics while keeping contract ownership canonical in `PoTool.Shared`.

### 3. Removed the legacy compatibility shim

Deleted:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.LegacyCompatibility.cs`

Recovered call sites were migrated as follows:

- `DeliveryTrends.razor` now uses the governed sprint-trend envelope path directly
- `BacklogHealthPanel.razor` now calls the generated method shape directly with cancellation token
- `BugTriageDetailsPanel.razor`, `TreeBuilderService.cs`, and `WorkItemFilteringService.cs` no longer depend on shimmed `JsonPayload` members on shared-owned work-item DTOs

### 4. Removed the API-to-client project dependency

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/PoTool.Api.csproj`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

Result:

- `PoTool.Api` no longer references `PoTool.Client`
- the unused `using PoTool.Client.Services;` import was removed
- API contract stability no longer depends on the client project reference edge

### 5. Strengthened guardrails

Updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs` so it now enforces:

- all public types declared in `PoTool.Shared` must be present in NSwag exclusions
- generated client output must have **zero** public-type overlap with shared public types
- manual `ApiClient/*.cs` companions are limited to the governed envelope/json helper set
- `PoTool.Api` must not reference `PoTool.Client`

### 6. Stabilized affected tests after ownership recovery

Updated only the tests touched by the regenerated/shared ownership shift so the repository remains green without reintroducing duplicate client-owned DTOs.

## Final ownership boundary

### Shared canonical contracts

Owned by `PoTool.Shared` and no longer regenerated by NSwag.

Representative domains:

- settings/profile/product/team/sprint/repository contracts
- work-item and validation contracts
- metrics/portfolio/health contracts
- pipelines and pull-request canonical DTOs
- BuildQuality and release-planning canonical DTOs
- TFS verification contracts

### Generated client-only contracts

Owned by `ApiClient.g.cs`.

Representative categories:

- `*Client` and `I*Client` transport types
- request objects such as `CreateProductRequest`, `UpdateProfileRequest`, `UpdateTeamRequest`
- generated exception/response wrappers such as `ApiException`, `SwaggerResponse`, and controller-specific wrapper types

### Handwritten client-only contracts

Owned by the six governed manual files under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient`:

- `ApiClient.DeliveryFilters.cs`
- `ApiClient.Extensions.cs`
- `ApiClient.PipelineFilters.cs`
- `ApiClient.PortfolioConsumption.cs`
- `ApiClient.PullRequestFilters.cs`
- `ApiClient.SprintFilters.cs`

## Remaining technical debt

No contract-ownership blocker remains from the interrupted task.

Two intentional design choices remain, but they are now explicit rather than ambiguous:

- BuildQuality remains manual-service driven on the client
- release-planning board access remains manual-service driven on the client

These are now bounded decisions, not ownership drift.

## Validation results

### Ownership validation

Measured after regeneration:

- shared public types: 325
- generated public types: 104
- shared/generated overlap: **0**

### Build validation

Succeeded:

- `dotnet build PoTool.Client/PoTool.Client.csproj --configuration Release --nologo /p:GenerateApiClient=true`
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`

### Targeted recovery tests

Succeeded:

- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~WorkItemFilteringServiceClientTests|FullyQualifiedName~TreeBuilderServiceTests|FullyQualifiedName~BacklogHealthCalculationServiceClientTests|FullyQualifiedName~WorkItemSelectionServiceTests"`

## Final confirmation

The interrupted contract-ownership-boundary task is now completed without redoing already-correct work:

- prior partial work was recovered instead of restarted
- shared-owned contracts are no longer regenerated
- generated/manual client usage is explicit by domain
- the legacy compatibility shim has been removed
- the API-to-client dependency edge has been removed
- guardrails now enforce the recovered ownership boundary
