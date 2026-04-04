# DateTimeOffset ORDER BY fix

## 1. Summary

- Occurrences found: 24 ordering clauses using `DateTimeOffset` values in `PoTool.Api`
- Occurrences fixed: 24
- Confirmation: the audited `PoTool.Api` ordering bug class is eliminated by converting `DateTimeOffset` sorting to UTC `DateTime` sorting, and the SQLite configuration export failure is resolved

## 2. Files changed

Production code:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Configuration/ExportConfigurationService.cs`
  - `ExportAsync`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/Configuration/ImportConfigurationService.cs`
  - `ApplyEffortSettingsAsync`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/Entities/EffortEstimationSettingsEntity.cs`
  - `LastModified`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Persistence/PoToolDbContext.cs`
  - `OnModelCreating` (`EffortEstimationSettingsEntity` index)
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/PipelineRepository.cs`
  - `GetRunsAsync`
  - `GetRunsForPipelinesAsync`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CachedPipelineReadProvider.cs`
  - `GetRunsForPipelinesAsync`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineInsightsQueryHandler.cs`
  - pipeline scatter ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Pipelines/GetPipelineMetricsQueryHandler.cs`
  - run ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioTrendAnalysisService.cs`
  - snapshot ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/PortfolioReadModelFiltering.cs`
  - filtered item ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SyncChangesSummaryService.cs`
  - latest state-change selection
  - sprint summary ordering
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/CacheManagementService.cs`
  - sprint grouping ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/ActivityEventIngestionService.cs`
  - update ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Settings/SprintQueryHandlers.cs`
  - `GetSprintsForTeamQueryHandler.Handle`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs`
  - sprint metrics ordering block
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs`
  - completed work-item ordering
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipMockDataFacade.cs`
  - cached pipeline run ordering
  - grouped pipeline run ordering
  - test-run ordering
  - coverage ordering
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockData/BattleshipPipelineGenerator.cs`
  - generated run ordering

Persistence:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260404085552_AddEffortSettingsLastModifiedUtcForSqliteOrdering.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/20260404085552_AddEffortSettingsLastModifiedUtcForSqliteOrdering.Designer.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Migrations/PoToolDbContextModelSnapshot.cs`

Guardrail:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DateTimeOffsetOrderingAuditTests.cs`

## 3. Before vs after examples

Representative SQL-translated fix:

- Before: `OrderByDescending(settingsEntity => settingsEntity.LastModified)`
- After: `OrderByDescending(settingsEntity => settingsEntity.LastModifiedUtc)`

Representative in-memory fix:

- Before: `OrderByDescending(r => r.FinishTime).ThenByDescending(r => r.StartTime)`
- After: `OrderByDescending(r => r.FinishTime.HasValue ? r.FinishTime.Value.UtcDateTime : DateTime.MinValue).ThenByDescending(r => r.StartTime.HasValue ? r.StartTime.Value.UtcDateTime : DateTime.MinValue)`

Representative nullable ascending fix:

- Before: `OrderBy(m => m.StartUtc ?? DateTimeOffset.MaxValue)`
- After: `OrderBy(m => m.StartUtc.HasValue ? m.StartUtc.Value.UtcDateTime : DateTime.MaxValue)`

## 4. Validation results

Build and tests:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release` ✅
- targeted audit and regression tests passed:
  - `DateTimeOffsetOrderingAuditTests`
  - `ExportConfigurationServiceTests`
  - `ImportConfigurationServiceTests`
  - `CacheManagementServiceTests`
  - `PortfolioQueryServicesTests` ✅

Runtime validation:

- `GET http://localhost:5291/api/settings/configuration-export` returned `200 OK` ✅
- response parsed successfully
- observed payload summary:
  - `version = 1.0`
  - `profiles = 3`
  - `teams = 8`
  - `products = 6`

Other affected surfaces:

- additional affected ordering paths were validated through targeted unit tests and the new API ordering audit ✅

## 5. Guardrails added

- Added `DateTimeOffsetOrderingAuditTests`
- The audit scans `PoTool.Api/**/*.cs` for ordering clauses that use known `DateTimeOffset` members without converting to `UtcDateTime`
- The guardrail covers `OrderBy`, `OrderByDescending`, `ThenBy`, and `ThenByDescending`

## 6. Remaining risks

- No client-side fallback was introduced for SQL translation
- The only schema addition was `EffortEstimationSettings.LastModifiedUtc`, added specifically to keep SQLite ordering server-side
