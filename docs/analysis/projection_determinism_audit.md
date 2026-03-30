# Projection Determinism Audit

_Generated: 2026-03-16_

Reference documents:

- `docs/architecture/domain-model.md`
- `docs/rules/estimation-rules.md`
- `docs/rules/metrics-rules.md`
- `docs/rules/source-rules.md`
- `docs/rules/sprint-rules.md`
- `docs/analysis/portfolio_flow_projection_validation.md`
- `docs/analysis/forecasting_cdc_summary.md`

## Projection inventory

The CDC migration left the following projection-producing services in scope for this audit:

1. `PoTool.Core.Domain/Domain/DeliveryTrends/Services/SprintDeliveryProjectionService.cs`
   - computes canonical historical sprint delivery projections from prepared sprint facts
2. `PoTool.Api/Services/PortfolioFlowProjectionService.cs`
   - rebuilds persisted `PortfolioFlowProjectionEntity` rows for each `(SprintId, ProductId)` key
3. Forecasting projection services
   - `PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs`
   - `PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs`
   - `PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs`

The delivery services rebuild historical facts. The forecasting services produce future-looking projections from already prepared historical inputs. All of them must remain repeatable after the CDC migration.

## Determinism verification

### SprintDeliveryProjectionService

Audit finding:

- the service is a pure CDC calculation over a `SprintDeliveryProjectionRequest`
- no persisted mutable state is read or updated inside the projection formula
- no `DateTime.UtcNow`, randomization, or append-style accumulation occurs during `Compute`

Determinism evidence:

- `PoTool.Tests.Unit/Services/SprintDeliveryProjectionServiceTests.cs`
  - `Compute_RepeatedWithSameInputs_ProducesIdenticalProjection`

Verified behavior:

- rebuild deterministically
- produce identical results across rebuilds
- use first-done and spillover inputs without re-counting delivery on repeated execution

### PortfolioFlowProjectionService

Audit finding:

- rebuild persistence is keyed by `(SprintId, ProductId)`
- existing projection rows are updated in place through `ApplyProjection`
- repeated rebuilds do not append duplicate rows for the same key
- inflow is based on canonical portfolio-entry events and throughput is based on first-done reconstruction

Determinism evidence:

- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
  - `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates`
- `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs`
  - first-done, reopen, and membership-transition edge cases remain covered

Verified behavior:

- rebuild deterministically
- do not duplicate inflow
- do not duplicate throughput
- produce identical results across rebuilds
- keep exactly one persisted row for the `(SprintId, ProductId)` key after repeated recomputation

### Forecasting projection services

Audit finding:

- `CompletionForecastService`, `VelocityCalibrationService`, and `EffortTrendForecastService` all execute as pure in-memory calculations over explicit inputs
- none of the audited forecasting services depend on ambient time, mutable caches, or persistence side effects
- the output shape is determined only by the provided historical samples and work-item distributions

Determinism evidence:

- `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs`
  - `ForecastingServices_RepeatedWithSameInputs_ProduceIdenticalOutputs`

Verified behavior:

- repeated completion forecasts return the same estimated velocity, confidence, completion date, and sprint projections
- repeated velocity calibration returns the same percentile bands, predictability, entries, and outlier classification
- repeated effort-trend analysis returns the same trend direction, slope, per-sprint trends, area-path trends, and forecast bands

## Coverage updates

Coverage already present before this audit:

- `ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates`
  already proved repeated rebuild stability for persisted portfolio-flow rows

Coverage added in this audit:

1. `Compute_RepeatedWithSameInputs_ProducesIdenticalProjection`
   - closes the direct determinism gap for `SprintDeliveryProjectionService`
2. `ForecastingServices_RepeatedWithSameInputs_ProduceIdenticalOutputs`
   - closes the explicit repeatability gap for the forecasting projection services

No production-code change was required because the audited services were already deterministic; the missing work was explicit regression coverage and a consolidated report.

## Audit conclusion

Result: **pass**

The audited projection services remain deterministic after CDC migration.

- `SprintDeliveryProjectionService` rebuilds deterministically from canonical request data
- `PortfolioFlowProjectionService` does not duplicate inflow, does not duplicate throughput, and preserves a single persisted row per projection key across rebuilds
- forecasting projection services produce identical results across repeated runs with the same inputs

Local validation used for this audit:

- `dotnet build PoTool.sln`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~SprintDeliveryProjectionServiceTests|FullyQualifiedName~PortfolioFlowProjectionServiceTests|FullyQualifiedName~SprintTrendProjectionServiceSqliteTests|FullyQualifiedName~ForecastingDomainServicesTests|FullyQualifiedName~ProjectionDeterminismAuditDocumentTests" -v minimal`
