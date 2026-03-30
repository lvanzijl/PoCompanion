# Documentation Migration — Batch 3 (OData + Validator Removal)

## 1. Summary
- validator components removed
- OData components removed
- docs cleaned
- items archived

## 2. Validator removal
- Deleted the deprecated validator tool project directory:
  - `PoTool.Tools.TfsRetrievalValidator/PoTool.Tools.TfsRetrievalValidator.csproj`
  - `PoTool.Tools.TfsRetrievalValidator/Program.cs`
  - `PoTool.Tools.TfsRetrievalValidator/appsettings.json`
  - `PoTool.Tools.TfsRetrievalValidator/appsettings.Development.json`
  - `PoTool.Tools.TfsRetrievalValidator/packages.lock.json`
- Removed validator-project references from:
  - `PoTool.sln`
  - `PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`
  - `PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs`
- Cleaned validator-tool residue from tests and docs:
  - `PoTool.Tests.Unit/Audits/BuildQualityDiscoveryReportDocumentTests.cs`
  - `docs/analysis/buildquality-discovery-report.md`
  - `docs/architecture/repository-stability-audit.md`
  - `docs/architecture/restore-build-determinism-fix.md`
  - `docs/analysis/tfs-api-version-configuration-inspection-report.md`

## 3. OData removal
- Removed OData-specific code and settings classes:
  - `PoTool.Shared/Settings/AnalyticsODataDefaults.cs`
  - `PoTool.Core/Configuration/RevisionIngestionPaginationOptions.cs`
  - `PoTool.Core/Configuration/RevisionIngestionV2Options.cs`
- Removed the Analytics OData metadata validation path from:
  - `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs`
- Removed OData-specific revision-whitelist structures and helper API from:
  - `PoTool.Core/RevisionFieldWhitelist.cs`
- Updated targeted tests for the new non-OData behavior:
  - `PoTool.Tests.Unit/Services/RealTfsClientVerificationTests.cs`
  - `PoTool.Tests.Unit/RevisionFieldWhitelistTests.cs`
- Confirmed active revision retrieval remains REST-based through:
  - `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemRevisions.cs`

## 4. Documentation changes
- Files deleted:
  - validator tool source/config files under `PoTool.Tools.TfsRetrievalValidator/`
  - active copies of:
    - `docs/filters/tfs-access-boundary-verification.md`
    - `docs/filters/tfs-access-boundary-sealed.md`
    - `docs/reports/odata-ingestion-fix-plan.md`
- Files archived (with paths):
  - `docs/analysis/tfs-access-boundary-verification.md`
  - `docs/analysis/tfs-access-boundary-sealed.md`
  - `docs/archive/revision-ingestion/odata-ingestion-fix-plan.md`
- Updated path/reference cleanup in:
  - `docs/analysis/relic-audit/documentation-reorganization-report.md`
  - `docs/reports/2026-03-30-documentation-migration-batch-1.md`
  - `docs/analysis/documentation-state-verification.md`
  - `docs/analysis/relic-audit/repository-relic-audit.md`
  - `docs/implementation/phase-a-corrections.md`
  - `docs/implementation/phase-a-foundation.md`
- Removed literal `validator` references from `.github/copilot-instructions.md` by rewriting them to generic validation terminology so the rule source no longer mentions the deprecated validator tooling.

## 5. Residual references (if any)
- Historical OData references remain in EF Core migrations and migration designer files under `PoTool.Api/Migrations/`.
  - These were intentionally kept as migration history and not edited.
- Historical validator/OData discussion remains in some analysis and archive markdown files under `docs/analysis/**` and `docs/archive/revision-ingestion/**`.
  - These are retained as traceability/history, not active implementation guidance.

## 6. Risks / uncertainties
- Old migration files still contain historical `AnalyticsOData*` column names; removing those references would require editing migration history, which was intentionally avoided.
- Some repository analysis documents still describe pre-Batch-3 state as historical evidence. They do not affect build/runtime behavior, but they are listed above as residual historical references.

## 7. Next batch plan
- batch 4: structural cleanup (non-canonical folders)
- batch 5: filename enforcement (large-scale)
