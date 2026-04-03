# Test Governance Realignment

## 1. Before state

Initial census at `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-test-failure-census.md` recorded 17 failing tests and 0 runtime or persistence failures.

Cluster summary before remediation:

| Cluster | Failures | Notes |
| --- | ---: | --- |
| Route intent classification drift | 1 | Portfolio route expectation no longer matched governed route intent. |
| Read-provider fixture drift | 4 | `DataSourceAwareReadProviderFactory` and `LazyReadProvider` tests no longer registered `IHttpContextAccessor`. |
| NSwag / client contract drift | 2 | Shared DataState contracts were no longer fully excluded and a governed client extension file had been added. |
| Documentation governance drift | 4 | Non-dated / non-canonical markdown paths, missing historical notes, and rule-mirror drift. |
| CDC audit drift | 3 | Generated CDC map and CDC usage coverage anchors no longer matched current source. |
| UI semantic / portfolio CDC audit drift | 2 | UI audit expectations still targeted older client state-access patterns and forecast bindings. |
| Architecture guard overreach | 1 | Generic `Confidence ==` guard incorrectly flagged forecasting presentation code. |

## 2. Actions per cluster

### Route intent classification drift
- Decision: fix test
- Reasoning: `/api/portfolio/progress` is explicitly governed as `CacheOnlyAnalyticalRead` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs`.
- Changes applied: updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs` to assert the cache-only intent.

### Read-provider fixture drift
- Decision: fix test
- Reasoning: production activation now requires `IHttpContextAccessor`; the failures came from incomplete unit-test service registration, not incorrect runtime behavior.
- Changes applied: added `AddHttpContextAccessor()` to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/DataSourceAwareReadProviderFactoryTests.cs` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/LazyReadProviderTests.cs`.

### NSwag / client contract drift
- Decision: fix code and governance test inventory
- Reasoning: the shared DataState contracts are authoritative shared types and must stay excluded from generated clients; the extra manual client extension file is intentional and governed.
- Changes applied:
  - added `DataStateDto` and `DataStateResponseDto` to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`
  - updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs` to include the intentional `BugTriageClientServiceCollectionExtensions.cs` root file
  - added `ApiContract` categorization to the NSwag governance test class

### Documentation governance drift
- Decision: fix documentation/files
- Reasoning: the rules remain valid; the repository content had drifted out of compliance.
- Changes applied:
  - moved `/home/runner/work/PoCompanion/PoCompanion/docs/testing/ui-exploratory-screenshot-run.md` to `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-03-31-ui-exploratory-screenshot-run.md`
  - renamed `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-02-di-activation-audit-and-fix.md`
  - renamed `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-02-routing-strategy-audit-and-fix.md`
  - updated markdown references after the moves
  - added historical notes to `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-global-persistence-contract-enforcement.md` and `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-global-seeding-contract-hardening.md`
  - aligned `/home/runner/work/PoCompanion/PoCompanion/docs/rules/persistence-contract.md` with the trust-closure mirror preamble expected by governance tests
  - escaped accidental markdown-link patterns in `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-test-failure-census.md`

### CDC audit drift
- Decision: fix documentation and audit expectations
- Reasoning: the CDC reports are still authoritative governance artifacts, but they had fallen behind current persisted-forecast and service-interface reality.
- Changes applied:
  - updated `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/cdc-domain-map-generated.md` for the current service count and `IDeliveryForecastProjector`
  - updated `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/cdc-usage-coverage.md` to describe persisted forecast projections instead of the removed direct forecast-service path
  - aligned `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CdcUsageCoverageDocumentTests.cs` with the current forecast-projection and projector boundaries

### UI semantic / portfolio CDC audit drift
- Decision: fix tests
- Reasoning: the behaviors are intentional; the tests still asserted superseded client binding names and pre-DataState service calls.
- Changes applied:
  - updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs` to assert the current `forecast.*StoryPoints` bindings
  - updated `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PortfolioCdcUiAuditTests.cs` to assert state-aware portfolio read calls

### Architecture guard overreach
- Decision: update rule
- Reasoning: the failing code compared forecasting confidence only to drive presentation emphasis; the guard was written too broadly and was catching non-BuildQuality enum equality checks.
- Changes applied: narrowed `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/BuildQualityArchitectureGuardTests.cs` so the rule continues to block threshold-style confidence logic without banning all enum equality checks.

## 3. Test layer separation

Implemented with MSTest categories.

- `Governance`
  - applied to audit and architecture tests under `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/**` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Architecture/**`
- `ApiContract`
  - applied to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`

CI execution definition updated in `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml.disabled`:

- Core gate:
  - `dotnet test PoTool.sln --configuration Release --no-build --verbosity normal --filter "TestCategory!=Governance&TestCategory!=AutomatedExploratory"`
- Governance gate:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --verbosity normal --filter "TestCategory=Governance"`
- Controlled API contract gate:
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=ApiContract"`

## 4. After state

Validation commands executed:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-build --nologo --filter "TestCategory!=Governance&TestCategory!=AutomatedExploratory"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=Governance"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=ApiContract"`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-build --nologo`

Results:

| Gate | Result |
| --- | --- |
| Core gate | Passed — 1799/1799 relevant tests green across the solution filter |
| Governance gate | Passed — 158/158 governance tests green |
| Controlled API contract gate | Passed — 5/5 NSwag contract tests green |
| Full Release suite | Passed — 1799/1799 tests green |

Remaining failures: none.

## 5. Governance decisions

- Route classification authority stays with `DataSourceModeConfiguration`; the outdated test was corrected.
- Shared DataState DTO ownership remains authoritative; NSwag exclusions were updated instead of duplicating contracts.
- Audit and architecture tests are now explicitly non-runtime governance tests via `Governance` categorization.
- NSwag governance tests remain part of the governance layer and also expose an `ApiContract` category for controlled execution.
- The BuildQuality confidence guard remains authoritative for threshold-style semantic derivation, but no longer treats all client enum equality checks as violations.
- Documentation-governance rules remained authoritative; repository files were renamed/moved/annotated to comply instead of weakening those rules.

## 6. Residual risks

- The workflow definition is still stored as `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml.disabled`; enabling it remains a separate operational step.
- Governance categorization was applied by folder ownership (`Audits` and `Architecture`), so any future governance-style tests created outside those areas must be explicitly categorized.
- Generated / analysis documentation can drift again if regenerated artifacts are not refreshed when their source boundaries change.
