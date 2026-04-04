# Generated-client ergonomics refinement

## 1. Summary

- Reduced mapping friction by adding typed envelope extension methods that replace repeated inline mapper lambdas.
- Improved naming/readability by moving most generated-wrapper ugliness behind readable fluent calls such as `response.ToCacheBackedResult()`, `response.ToDataStateResponse()`, `response.GetDataOrDefault()`, `response.GetReadOnlyListOrDefault()`, and `.RequireData(...)`.
- Runtime behavior did not change: state handling, DTO semantics, service contracts, and page behavior remain unchanged.

## 2. Mapping friction improvements

Reduced patterns:

- `GeneratedCacheEnvelopeHelper.ToCacheBackedResult(response, static data => data.ToShared())`
- `GeneratedCacheEnvelopeHelper.ToDataStateResponse(response, static data => data.ToShared())`
- `GeneratedCacheEnvelopeHelper.GetDataOrDefault(response, static data => data.ToShared())`
- `GeneratedCacheEnvelopeHelper.GetDataOrDefault(response, static data => data.ToReadOnlyList(), ...)`
- `GeneratedCacheEnvelopeHelper.ToDataStateResponse(response, static data => data.ToReadOnlyList())`

New ergonomics live in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedClientEnvelopeExtensions.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/ApiClient.Extensions.cs`

Why this is better:

- mapping remains explicit and compile-time safe
- the mapping seam is centralized once per generated wrapper family
- call sites are shorter and easier to review
- no JSON, reflection, dynamic typing, or object conversion pipelines were reintroduced

## 3. Naming/readability improvements

Representative before vs after:

- Before:
  - `CacheBackedGeneratedClientHelper.RequireData(GeneratedCacheEnvelopeHelper.ToCacheBackedResult(response, static data => data.ToShared()), nameof(GetMetricsEnvelopeAsync))`
- After:
  - `response.ToCacheBackedResult().RequireData(nameof(GetMetricsEnvelopeAsync))`

- Before:
  - `GeneratedCacheEnvelopeHelper.ToDataStateResponse(await _metricsClient.GetSprintExecutionAsync(...), static data => data.ToShared())`
- After:
  - `(await _metricsClient.GetSprintExecutionAsync(...)).ToDataStateResponse()`

- Before:
  - `GeneratedCacheEnvelopeHelper.GetDataOrDefault(response, static data => data.ToReadOnlyList(), Array.Empty<WorkItemDto>())`
- After:
  - `response.GetReadOnlyListOrDefault(Array.Empty<WorkItemDto>())`

How generated-type ugliness is now contained:

- generated wrapper type names stay inside the dedicated envelope extension layer
- ApiClient wrapper files and generated-client-facing services now read in terms of cache/result intent rather than conversion mechanics

## 4. Validation

Tests run:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --no-build --filter "FullyQualifiedName~GeneratedCacheEnvelopeHelperTests|FullyQualifiedName~GeneratedClientDtoMappingsTests|FullyQualifiedName~GeneratedClientEnvelopeExtensionsTests|FullyQualifiedName~GeneratedClientStateServiceTests|FullyQualifiedName~GeneratedDtoMappingHardeningAuditTests|FullyQualifiedName~CacheBackedGeneratedClientMigrationAuditTests|FullyQualifiedName~BuildQualityServiceTests|FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~GlobalFilterRouteServiceTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~NswagGovernanceTests"`

Preserved behavior:

- `READY` still maps to shared DTO data correctly
- `NOT_READY` still propagates unchanged
- `FAILED` still propagates unchanged
- `EMPTY` still propagates unchanged

## 5. Guardrails

Added/extended:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Helpers/GeneratedClientEnvelopeExtensionsTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/GeneratedDtoMappingHardeningAuditTests.cs`

Guardrails now verify:

- no JSON/reflection mapping in the hardened helper and client-integration path
- generated-client call sites use the new envelope ergonomics instead of repeating inline `ToShared()` / `ToReadOnlyList()` mapper lambdas
- the new extension layer preserves mapped result and state semantics

## 6. Remaining limitations

Real remaining ergonomic limitations:

- `GeneratedClientEnvelopeContracts.cs` still contains long generated NSwag type names because the transport shapes are unchanged by design
- adding brand-new generated wrapper families still requires one explicit extension entry in the dedicated envelope ergonomics layer

These limitations are intentional and keep the system explicit, local, and compile-time safe.
