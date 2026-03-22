# BuildQuality Real Data Validation Report

## 1. Scope

- Requested validation target: end-to-end BuildQuality behavior with real TFS/Azure DevOps data across ingestion, provider computation, API endpoints, and UI consumption.
- Observed repository/runtime scope in this sandbox:
  - `PoTool.Api/appsettings.json` sets `TfsIntegration.UseMockClient` to `true`.
  - `PoTool.Api/appsettings.Development.json` also sets `TfsIntegration.UseMockClient` to `true`.
  - `PoTool.Tools.TfsRetrievalValidator/appsettings.json` contains placeholder TFS values (`http://your-tfs-server:8080/...`) rather than a usable production connection.
  - No checked-in SQLite database or other cached production BuildQuality dataset was present in the repository root for direct inspection.
- Pipelines tested (ids/names): **none**. No non-mock TFS connection or cached production dataset was available in the sandbox, so the requirement to validate at least 3 real pipelines could not be satisfied from observed data.
- Data ranges: **not available** for real pipelines. No real build/test/coverage date range could be sampled locally.

## 2. Data integrity findings

### TestRuns

- The ingestion path is implemented to preserve multiple test runs per build and to require both `BuildId` linkage and a stable `ExternalId` before persisting:
  - `PoTool.Api/Services/Sync/PipelineSyncStage.cs`
  - `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`
- The persistence model supports idempotent upsert semantics by `BuildId + ExternalId` and deletes stale child rows during resync for the affected builds.
- **Observed production validation result:** not executed. No real TestRun rows were available locally, so duplicate detection, orphan detection, and multi-run preservation could not be verified against production data.

### Coverage

- The ingestion path is implemented to link coverage rows by `BuildId` and replace existing rows for the synced build set:
  - `PoTool.Api/Services/Sync/PipelineSyncStage.cs`
  - `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`
- Coverage mapping explicitly resolves line counts from label-based coverage stats (`Line` / `Lines`) before persisting `CoveredLines` and `TotalLines`.
- **Observed production validation result:** not executed. No real coverage rows were available locally, so replace semantics and label consistency could not be verified against production data.

### Linkage issues

- No real cached child facts were present for direct linkage inspection.
- The local mock path is not suitable for this audit because mock BuildQuality ingestion returns empty test-run and coverage collections:
  - `PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`
  - `PoTool.Api/Services/MockTfsClient.cs`
- Result: real linkage integrity remains **unverified** in this sandbox.

## 3. Provider validation

### Metrics correctness

- The canonical provider computes:
  - `SuccessRate`
  - `TestPassRate`
  - `Coverage`
  - `Confidence`
  - supporting evidence counts
- The formulas and Unknown handling are implemented centrally in `PoTool.Api/Services/BuildQuality/BuildQualityProvider.cs`.
- Existing focused BuildQuality tests passed locally after a Release build:
  - `dotnet build PoTool.sln --configuration Release`
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~BuildQuality" -v minimal`
- **Observed production validation result:** not executed with real pipeline facts. Metric bounds, impossible combinations, and division-by-zero behavior were only confirmed indirectly through controlled automated tests, not through real cached TFS/Azure DevOps data.

### Unknown handling

- `BuildQualityProvider` sets Unknown flags only from missing eligible builds, missing test runs, missing coverage, or `TotalLines == 0`.
- That behavior is explicit in code and covered by focused automated tests.
- **Observed production validation result:** not executed with real pipeline facts. Unknown flags and `UnknownReason` alignment with production data reality remain **unverified**.

## 4. API validation

- Endpoints in scope are present and unchanged:
  - `/api/buildquality/rolling`
  - `/api/buildquality/sprint`
  - `/api/buildquality/pipeline`
- API contracts are exposed through:
  - `PoTool.Api/Controllers/BuildQualityController.cs`
  - `PoTool.Client/swagger.json`
  - `PoTool.Client/ApiClient/ApiClient.g.cs`
- DTO structure appears internally consistent from controller, Swagger, and generated client inspection.
- **Observed production validation result:** not executed by calling the endpoints against real cached production data. Because the checked-in API configuration uses the mock TFS client and no production cache snapshot was available, endpoint idempotence and Unknown alignment were not validated against real data.

## 5. UI validation

- UI consumers are wired to the typed BuildQuality client service:
  - `PoTool.Client/Pages/Home/HealthWorkspace.razor`
  - `PoTool.Client/Pages/Home/SprintTrend.razor`
  - `PoTool.Client/Pages/Home/PipelineInsights.razor`
  - `PoTool.Client/Services/BuildQualityService.cs`
- The UI follows the expected read-only consumption path from the API surface.
- **Observed production validation result:** not executed with real data. No real cached scope was available to confirm rolling-window stability, sprint alignment, drawer parity with API responses, or visual consistency across reloads.

## 6. Edge cases

- The codebase and focused tests include handling for missing test runs, missing coverage, and zero total lines.
- Real-data edge cases specifically requested by the issue were **not** observed locally:
  - pipelines with zero tests
  - pipelines with zero coverage
  - partially missing data
  - 1–2 build pipelines
  - very large pipelines
- Because no real pipeline sample was available, crash-resistance and Unknown correctness for those production scenarios remain **unverified**.

## 7. Issues found

- **CRITICAL** — Real-data validation is blocked by default runtime configuration. `PoTool.Api/appsettings.json` and `PoTool.Api/appsettings.Development.json` both set `TfsIntegration.UseMockClient` to `true`, so the checked-in application path does not target real TFS/Azure DevOps data.
- **CRITICAL** — No usable production TFS connection details or cached production BuildQuality dataset were available in the sandbox. `PoTool.Tools.TfsRetrievalValidator/appsettings.json` contains placeholder values, and no checked-in SQLite cache snapshot was present for direct evidence review.
- **MAJOR** — The mock BuildQuality path returns empty test-run and coverage collections, so a manual validation session that relies on default configuration cannot satisfy the “production reality check” requirement without external real-data access.

## 8. Final verdict

**NOT READY**

The repository contains the BuildQuality ingestion, provider, API, and UI wiring needed for real-data validation, but this sandbox did not expose any real TFS/Azure DevOps connection or cached production dataset. As a result, the mandatory validation of at least 3 real pipelines across differing products, sizes, and data-quality conditions could not be completed from observed data.

## Reviewer notes

### What was validated

- repository wiring for real BuildQuality ingestion, provider computation, API exposure, and UI consumption
- default runtime configuration that determines whether validation uses mock or real data
- absence of a locally available cached production dataset
- existing focused automated BuildQuality validation in the current repository state

### What was intentionally not changed

- no architectural changes
- no provider changes
- no UI changes

### Known limitations

- limited to the sandboxed repository contents and checked-in configuration
- no real pipeline sample size was available
- conclusions are dependent on the absence of accessible real TFS/Azure DevOps data in this environment
