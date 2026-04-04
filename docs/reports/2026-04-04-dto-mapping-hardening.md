# DTO mapping hardening

## 1. Summary

Removed JSON-based DTO conversion from the client generated-client path.

Removed occurrences:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedCacheEnvelopeHelper.cs`

Explicitly mapped DTO pairs now include:

- generated delivery query wrappers → shared `DeliveryQueryResponseDto<T>`
- generated sprint query wrappers → shared `SprintQueryResponseDto<T>`
- generated pipeline query wrappers → shared `PipelineQueryResponseDto<T>`
- generated pull request query wrappers → shared `PullRequestQueryResponseDto<T>`
- generated collection payloads → explicit materialized read-only lists

## 2. Mapping strategy

Mappings live in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedClientDtoMappings.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/GeneratedClientEnvelopeContracts.cs`

Separation of concerns:

- `GeneratedCacheEnvelopeHelper` now handles only envelope/state behavior
- `GeneratedClientDtoMappings` owns explicit generated-to-shared DTO conversion
- generated envelope partial contracts expose typed access without reflection

Reuse enforcement:

- one mapping method per wrapper family / DTO pair
- services and manual generated-client wrapper files call the shared mapper methods
- no inline JSON conversion or per-service custom DTO reconstruction remains in the migrated path

## 3. Before vs after

Before:

- runtime serialize/deserialize conversion
- reflection-based property extraction
- object-based envelope handling

After:

- typed `IGeneratedDataStateEnvelope<T>` contracts
- explicit wrapper mapping methods such as:
  - `DeliveryQueryResponseDtoOfPortfolioDeliveryDto -> DeliveryQueryResponseDto<PortfolioDeliveryDto>`
  - `PullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto -> PullRequestQueryResponseDto<IReadOnlyList<PullRequestDto>>`
  - `PipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto -> PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>`

Representative change:

- cache-backed service/wrapper calls now pass explicit typed mappers like `data => data.ToShared()` or `data => data.ToReadOnlyList()`
- no DTO conversion goes through JSON text anymore

## 4. Validation

State correctness preserved:

- `READY` keeps mapped payload data
- `NOT_READY` remains `NotReady`
- `FAILED` remains `Failed`
- `EMPTY` remains `Empty`

Data correctness validated by:

- explicit mapper unit tests for delivery, pipeline, and pull request wrapper conversion
- helper tests for envelope-state handling
- targeted regression tests for the affected client services

Validation commands:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release`
- targeted `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~GeneratedCacheEnvelopeHelperTests|FullyQualifiedName~GeneratedClientDtoMappingsTests|FullyQualifiedName~GeneratedClientStateServiceTests|FullyQualifiedName~GeneratedDtoMappingHardeningAuditTests|FullyQualifiedName~CacheBackedGeneratedClientMigrationAuditTests|FullyQualifiedName~BuildQualityServiceTests|FullyQualifiedName~ProjectServiceTests|FullyQualifiedName~GlobalFilterRouteServiceTests|FullyQualifiedName~GlobalFilterStoreTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~NswagGovernanceTests"` 

## 5. Guardrails

Added:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/GeneratedDtoMappingHardeningAuditTests.cs`

This audit fails if the hardened DTO mapping path reintroduces:

- `JsonSerializer`
- reflection-based property access
- serialize/deserialize DTO-to-DTO conversion in the guarded helper/mapping files

## 6. Remaining risks

No known functional regressions were found.

Residual maintenance risk:

- new generated query wrapper types must receive explicit mappings before use in shared-model service paths

That risk is now compile-time visible instead of silently hidden behind runtime JSON conversion.
