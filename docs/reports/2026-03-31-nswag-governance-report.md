# NSwag governance report

Timestamp: 2026-03-31T18:58:00Z

## Current NSwag setup overview

- **Authoritative config:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
- **Governed OpenAPI snapshot:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
- **Generated client output:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- **Handwritten companion files:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/*.cs` except `Generated/ApiClient.g.cs`
- **Build trigger:** explicit only via `/p:GenerateApiClient=true` on `PoTool.Client.csproj`

The corrected flow is now:

1. refresh the governed OpenAPI snapshot from the running API into `ApiClient/OpenApi/swagger.json`
2. run `dotnet build /p:GenerateApiClient=true` for `PoTool.Client`
3. compile the solution with the regenerated client checked in under `ApiClient/Generated`

## Violations found

1. **NSwag was triggered automatically on Debug builds**
   - `PoTool.Client.csproj` previously ran NSwag before every Debug compile.
   - This violated controlled-generation governance because developers could regenerate client code without explicitly opting in.

2. **The generated client and swagger snapshot lived in mixed, root-adjacent locations**
   - Generated code was stored at `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.g.cs`
   - The swagger snapshot was stored at `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/swagger.json`
   - That layout did not clearly separate generated artifacts, handwritten extensions, and OpenAPI source material.

3. **The repository contained stale handwritten NSwag compensations**
   - Stale files existed for:
     - `ApiClient.PullRequestInsights.cs`
     - `ApiClient.PrDeliveryInsights.cs`
     - `ApiClient.BuildQualityDeliveryFilters.cs`
   - These files existed because the checked-in swagger snapshot and generated client had drifted away from the live API contract.

4. **NSwag contract ownership was unclear**
   - Shared DTOs and generated DTOs overlapped in multiple domains.
   - This produced ambiguous types and made regeneration unsafe.

5. **Traceability from config to output was incomplete**
   - The old flow did not enforce a single governed output path.
   - Generation was not guarded against invalid layout or missing source snapshot.

## Root causes of violations

1. **Stale swagger snapshot as generation source**
   - The checked-in client snapshot did not stay aligned with the active backend contract.
   - Handwritten partials were added over time to compensate for drift instead of fixing the generation boundary.

2. **Automatic generation in normal development builds**
   - Regeneration happened during ordinary Debug compilation instead of an explicit maintenance step.
   - That encouraged silent, local, partially-reviewed output churn.

3. **No enforced generated/manual folder split**
   - Generated and handwritten files shared the same top-level folder with no dedicated generated subdirectory.

4. **Shared-contract exclusions were incomplete**
   - Several DTOs and enums that canonically belong to `PoTool.Shared` were still being regenerated, creating ambiguous references.

## Contract mismatches discovered

During the governed regeneration pass, the refreshed snapshot exposed several classes of mismatch:

- **BuildQuality duplication** between generated DTOs and shared DTOs
- **Portfolio consumption DTO duplication** between generated DTOs and shared DTOs
- **Validation/triage DTO duplication** between generated DTOs and shared DTOs
- **Filter-context DTO duplication** between generated DTOs and shared DTOs
- **Legacy client-call assumptions** where existing pages/services relied on older generated method shapes or removed optional fields such as `JsonPayload`

These were corrected by:

- explicitly excluding shared-owned DTOs/enums from NSwag generation
- keeping client-owned DTOs generated
- adding one governed compatibility shim file for legacy method/property expectations that still need to compile against the refreshed contract

## Changes applied

### 1. Configuration

Updated:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/PoTool.Client.csproj`

Changes:

- generation remains controlled by one config file: `PoTool.Client/nswag.json`
- output moved to `ApiClient/Generated/ApiClient.g.cs`
- shared-contract exclusions were expanded so NSwag no longer regenerates DTOs/enums that belong in `PoTool.Shared`
- generation now fails if the canonical config or governed snapshot is missing
- automatic Debug-time generation was removed; generation is now explicit only

### 2. File locations

Removed:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/swagger.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.g.cs`

Added:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/OpenApi/swagger.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`

This establishes explicit separation between:

- **OpenAPI source** → `ApiClient/OpenApi`
- **generated code** → `ApiClient/Generated`
- **handwritten wrappers/shims** → `ApiClient/*.cs`

### 3. Generation flow

The client project now supports a governed explicit generation flow:

```bash
cd /home/runner/work/PoCompanion/PoCompanion
curl -fsSL http://localhost:5291/swagger/v1/swagger.json -o PoTool.Client/ApiClient/OpenApi/swagger.json
dotnet build PoTool.Client/PoTool.Client.csproj --configuration Release --nologo /p:GenerateApiClient=true
```

`GenerateApiClient=true` is now required. Normal Debug builds no longer regenerate the client implicitly.

### 4. Manual vs generated decisions

**Remain generated**

- the main NSwag client and client-owned DTOs under `ApiClient/Generated/ApiClient.g.cs`
- pipeline metrics/run DTOs
- work-item/product/team/profile/TFS verification DTOs still owned by generated client output

**Remain handwritten**

- envelope wrappers and JSON settings partials:
  - `ApiClient.DeliveryFilters.cs`
  - `ApiClient.PipelineFilters.cs`
  - `ApiClient.PortfolioConsumption.cs`
  - `ApiClient.PullRequestFilters.cs`
  - `ApiClient.SprintFilters.cs`
  - `ApiClient.Extensions.cs`
- compatibility shims:
  - `ApiClient.LegacyCompatibility.cs`
- BuildQuality frontend access remains manual through:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/BuildQualityService.cs`

**Removed as stale / conflicting**

- `ApiClient.PullRequestInsights.cs`
- `ApiClient.PrDeliveryInsights.cs`
- `ApiClient.BuildQualityDeliveryFilters.cs`

These files were not part of a governed long-term NSwag boundary anymore.

## Final structure of generated clients

```text
PoTool.Client/
  ApiClient/
    Generated/
      ApiClient.g.cs
    OpenApi/
      swagger.json
    ApiClient.DeliveryFilters.cs
    ApiClient.Extensions.cs
    ApiClient.LegacyCompatibility.cs
    ApiClient.PipelineFilters.cs
    ApiClient.PortfolioConsumption.cs
    ApiClient.PullRequestFilters.cs
    ApiClient.SprintFilters.cs
```

## Decision log

1. **Single authoritative NSwag config**
   - kept `PoTool.Client/nswag.json` as the only config file

2. **Governed snapshot retained**
   - the repository now uses a governed checked-in snapshot under `ApiClient/OpenApi`
   - this keeps generation deterministic and reviewable while still allowing explicit refresh from the live API

3. **BuildQuality stays manual**
   - BuildQuality had already required manual stabilization to avoid stale generated-contract behavior
   - the governance fix preserves that decision instead of forcing a risky reversion

4. **Compatibility shim kept small and explicit**
   - one handwritten partial file preserves legacy compile-time expectations where refreshed generated signatures or optional properties changed
   - this is traceable and limited rather than allowing silent ad hoc fixes across many files

## Validation results

### Local build/test

Succeeded:

- `dotnet build PoTool.Client/PoTool.Client.csproj --configuration Release --nologo /p:GenerateApiClient=true`
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests|FullyQualifiedName~WorkspaceSignalServiceTests"`

### Generation validation

Confirmed:

- the live API snapshot refreshed into `PoTool.Client/ApiClient/OpenApi/swagger.json`
- NSwag regenerated `PoTool.Client/ApiClient/Generated/ApiClient.g.cs`
- no NSwag outputs were written to repository root
- normal builds no longer regenerate the client implicitly

## Remaining risks

1. **Snapshot refresh is still an explicit maintenance action**
   - the repository now governs where the snapshot lives and where generated output goes, but refreshing the snapshot from the live API remains a deliberate developer step

2. **PoTool.Api currently references PoTool.Client**
   - this coupling makes client-generation hygiene especially important because broken client output can block API build/run workflows

3. **A small compatibility shim remains**
   - `ApiClient.LegacyCompatibility.cs` is intentional and traceable, but it indicates there are still some legacy call sites expecting older generated shapes

## Confirmation

NSwag now complies with repository governance expectations for this repository:

- no generated outputs are written to repository root
- one authoritative NSwag configuration is used
- generated code and handwritten code are clearly separated
- the refreshed client compiles from the governed snapshot
- stale handwritten NSwag compensations were removed
- contract ownership between generated types and shared types is explicitly enforced
