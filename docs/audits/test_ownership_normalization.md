# Test Ownership Normalization

_Generated: 2026-03-17_

Reference documents:

- `docs/audits/test_ownership_audit.md`
- `docs/audits/test_cleanup_step1.md`
- `docs/domain/cdc_reference.md`

## Ownership Rules Applied

- CDC/domain tests own formulas, invariants, and edge-case semantics.
- Handler tests own orchestration, filtering, request scoping, and DTO mapping.
- Projection tests own persistence and deterministic replay outputs.
- Adapter tests own formatting and compatibility mapping when such tests exist in the active repository.

## Handler Tests Retained

The remaining handler coverage keeps ownership at the application boundary and avoids re-proving CDC formulas.

- `PoTool.Tests.Unit/Handlers/GetSprintMetricsQueryHandlerTests.cs`
  - retains missing-sprint handling, CDC service invocation, and zero-history DTO shape checks
- `PoTool.Tests.Unit/Handlers/GetSprintExecutionQueryHandlerTests.cs`
  - retains execution reconstruction orchestration through injected CDC services
- `PoTool.Tests.Unit/Handlers/GetCapacityCalibrationQueryHandlerTests.cs`
  - retains owner/product filtering, sample selection, empty handling, and DTO mapping from `IVelocityCalibrationService`
- `PoTool.Tests.Unit/Handlers/GetEpicCompletionForecastQueryHandlerTests.cs`
  - retains missing-epic handling, product-root loading, and DTO mapping from `IHierarchyRollupService` plus `ICompletionForecastService`
- `PoTool.Tests.Unit/Handlers/GetEffortDistributionTrendQueryHandlerTests.cs`
  - retains area-path filtering, product-root loading, and DTO mapping from `IEffortTrendForecastService`
- `PoTool.Tests.Unit/Handlers/GetEffortImbalanceQueryHandlerTests.cs`
  - retains empty handling, area filtering, recommendation plumbing, capacity-description formatting, and analyzer-to-DTO mapping through orchestration-focused test names

## CDC Tests Owning Semantics

These suites remain the canonical semantic owners after normalization.

- `PoTool.Tests.Unit/Services/SprintCommitmentCdcServicesTests.cs`
  - sprint commitment formulas, first-Done invariants, churn, spillover, and boundary timing
- `PoTool.Tests.Unit/Services/ForecastingDomainServicesTests.cs`
  - velocity calibration formulas, forecast projections, trend direction, forecast bands, and determinism of forecasting services
- `PoTool.Tests.Unit/Services/EffortDiagnosticsDomainModelsTests.cs`
  - imbalance risk bands, weighted scores, concentration thresholds, and canonical bucket validation
- `PoTool.Tests.Unit/Services/BacklogReadinessServiceTests.cs`
  - backlog quality semantic rules and readiness scoring ownership
- `PoTool.Tests.Unit/Services/PortfolioFlowProjectionServiceTests.cs`
  - portfolio stock, inflow, throughput, and projection semantics

## Projection / Adapter Tests Retained

- `PoTool.Tests.Unit/Services/SprintTrendProjectionServiceSqliteTests.cs`
  - retains persistence and deterministic projection rebuild ownership
- `PoTool.Tests.Unit/Services/DeliveryTrendDomainModelsTests.cs`
  - retains transport/projection model ownership rather than CDC semantic ownership
- Adapter-formatting ownership has no dedicated test file in the current repository snapshot; compatibility mapping remains covered within the relevant higher-layer suites without introducing duplicate formulas

## Final Boundary Status

- Handler test names now describe orchestration, filtering, persistence, formatting, or DTO mapping responsibilities rather than semantic formula ownership.
- The normalized handler suites no longer duplicate sprint formulas, forecast formulas, effort-planning formulas, portfolio flow formulas, or backlog quality semantic rules with independent literal expectations.
- CDC/domain suites remain the single semantic owners for formulas, invariants, and edge-case behavior.
- Projection coverage remains in projection-oriented suites, and no production code changes were required for this normalization step.
