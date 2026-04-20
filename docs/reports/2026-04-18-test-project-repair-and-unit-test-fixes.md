# Test project repair and unit test fixes

## 1. Summary

- **IMPLEMENTED:** Repaired the two broken test projects that were failing for restore/build/compile drift: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`.
- **VERIFIED:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj` was already healthy and remained healthy.
- **VERIFIED:** Solution restore now succeeds, `PoTool.Core.Domain.Tests` now builds and passes, `PoTool.Api.Tests` now builds and passes, and `PoTool.Tests.Unit` now builds and runs with a single residual governance/documentation blocker.
- **BLOCKER:** One unit test still fails because the repository still contains active markdown under the non-canonical `docs/implementation/**` folder, which conflicts with the enforced documentation-governance rule.

## 2. Broken test project inventory

### `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`
- **VERIFIED:** Broken at restore time.
- **Evidence:** `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
- **Representative error:** `NU1101 Unable to find package Microsoft.Extensions.DependencyInjection.Abstractions`
- **Representative file:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/packages.lock.json`

### `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`
- **VERIFIED:** Broken at build time due compile drift and inaccessible internal APIs.
- **Evidence:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`
- **Representative errors:**
  - `MockTfsClient is inaccessible due to its protection level`
  - `ToCanonicalDomainStateClassifications` extension method not found
  - `ProductPlanningRecoveryStatus` not found
  - `ResolveAncestry` / `ComputeProductSprintProjection` / mapper drift

### `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj`
- **VERIFIED:** Not broken during this phase.
- **Evidence:** `dotnet build` and `dotnet test` both passed before and after repairs.

## 3. Root-cause categories

- **VERIFIED — stale restore metadata / lock drift**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/packages.lock.json`

- **VERIFIED — internal API visibility drift**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/MockTfsClient.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Adapters/*.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/SprintTrendProjectionService.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Services/WorkItemResolutionService.cs`

- **VERIFIED — shared-type namespace / contract drift**
  - `ProductPlanningRecoveryStatus` moved to `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Planning/ProductPlanningBoardDtos.cs`

- **VERIFIED — test helper overload drift**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`

- **VERIFIED — test fixture drift after persistence contract hardening**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/ProductPlanningIntentStoreTests.cs`

- **VERIFIED — audit/governance expectation drift**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CacheBackedGeneratedClientMigrationAuditTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationVerificationBatch6Tests.cs`

## 4. Repair plan

- **IMPLEMENTED:** Regenerate the broken core-domain test lock metadata instead of changing production dependencies.
- **IMPLEMENTED:** Restore internal test visibility with minimal production compatibility only in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/AssemblyInfo.cs`.
- **IMPLEMENTED:** Fix test imports, fixtures, helper overloads, and stale expectations rather than redesigning production behavior.
- **IMPLEMENTED:** Update audit expectations only where the old rule assumptions were obsolete or had drifted from current intended behavior.
- **NOT IMPLEMENTED:** Broad repository-wide documentation taxonomy cleanup beyond the single residual blocker.

## 5. Files changed

### Production/minimal compatibility
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/AssemblyInfo.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`

### Core-domain test repairs
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardServiceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/ProductPlanningBoardTestFactory.cs`

### Unit-test repairs
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/ProductPlanningIntentStoreTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Persistence/PersistenceRelationshipContractTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RealTfsClientVerificationTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/CacheBackedGeneratedClientMigrationAuditTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationVerificationBatch6Tests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/GlobalFilterArchitectureAuditTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/OnboardingWorkspaceReadOnlyAuditTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/PageContextContractEnforcementAuditTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/UiSemanticLabelsTests.cs`

### Documentation adjustments made while repairing governance tests
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/gebruikershandleiding.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/documentation-state-verification.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/relic-audit/documentation-reorganization-report.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-cdc-fallback-timestamp-hardening.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/implementation/cdc-fallback-timestamp-hardening.md` (removed)

## 6. Tests/projects fixed

### Core domain tests
- **IMPLEMENTED:** Repaired restore/build drift and current test expectations.
- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-build --no-restore`
- **Result:** `Passed: 33, Failed: 0`

### API tests
- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build --no-restore`
- **Result:** `Passed: 30, Failed: 0`

### Unit tests
- **IMPLEMENTED:** Repaired compile drift from inaccessible internals, moved shared enums, fixture drift, TFS verification fixture drift, FK coverage manifest drift, and several stale audit expectations.
- **VERIFIED:** `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`
- **VERIFIED:** `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore`
- **Result:** `Passed: 2086, Failed: 1`
- **Remaining failing test:** `DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames`

## 7. Remaining failures or blockers

- **BLOCKER:** `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
  - **Exact failing test:** `DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames`
  - **Current evidence:** `docs/implementation/navigation-decision-backlog.md`
  - **Cause:** the repository still contains active markdown under `docs/implementation/**`, which conflicts with the enforced canonical-docs taxonomy.
  - **Why not fully fixed here:** fully resolving this remaining failure cleanly requires broader documentation curation and relocation of the existing `docs/implementation/**` set, plus coordinated link/reference cleanup across repo documents. That goes beyond minimal test-project repair.

## 8. Exact restore/build/test commands run

- **Phase 1 inventory**
  - `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-build --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore`

- **Phase 3/4 verification**
  - `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --no-build --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Api.Tests/PoTool.Api.Tests.csproj --no-build --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore`

## 9. Recommendation for next validation phase

- **RECOMMENDATION:** Run the normal lower-level validation gate again after the remaining `docs/implementation/**` documentation-governance cleanup is explicitly scoped and completed.
- **RECOMMENDATION:** Treat the residual blocker as documentation-governance debt, not as a production-code or planning-system defect.

## Final section

### IMPLEMENTED
- Restored solution restore health
- Restored `PoTool.Core.Domain.Tests` restore/build/test health
- Restored `PoTool.Tests.Unit` build health and reduced failures from broad compile/runtime drift to a single governance blocker
- Preserved `PoTool.Api.Tests` health
- Added minimal `PoTool.Api` internal visibility for legitimate lower-level test access
- Updated stale NSwag and unit-test contract expectations

### NOT IMPLEMENTED
- Full relocation/curation of the remaining legacy markdown set under `docs/implementation/**`

### BLOCKERS
- `DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames`
- Root cause: remaining non-canonical active docs under `docs/implementation/**`

### Evidence (files/tests/commands)
- Files: all concrete paths listed in sections 5–7
- Tests: `PoTool.Core.Domain.Tests` 33/33 passed, `PoTool.Api.Tests` 30/30 passed, `PoTool.Tests.Unit` 2086/2087 passed
- Commands: all exact commands listed in section 8

### GO/NO-GO for returning to normal feature development
- **NO-GO** until the remaining documentation-governance blocker is resolved or explicitly waived.
