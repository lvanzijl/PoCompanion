# Docs implementation temporary governance exception

## 1. Summary

- **VERIFIED:** The failing documentation-governance rule was `DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`.
- **IMPLEMENTED:** Added the smallest explicit temporary exception so `docs/implementation/**` is treated as a legacy transitional folder in that one canonical-folder check only.
- **VERIFIED:** Other documentation governance tests still passed after the change, including:
  - `DocumentationVerification_AllMarkdownLinksAndAnchorsResolve`
  - `DocumentationCompliance_ActiveFoldersDoNotReferenceDeprecatedIngestionTerminology`
  - `DocumentationVerification_ActiveDocumentationContainsNoSemanticLeakage`
- **VERIFIED:** Full `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` test execution passed after the narrow exception.

## 2. Exact failing test(s) and rule location

- **VERIFIED — failing test**
  - `DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames`
  - file: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
  - failing assertion location: canonical-folder check at the `CanonicalFolders.Contains(segments[1], StringComparer.Ordinal)` assertion
  - representative failure:
    - `Non-canonical docs folder: docs/implementation/navigation-decision-backlog.md`

- **VERIFIED — related tests inspected**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationVerificationBatch6Tests.cs`
  - The temporary exception was **not** needed there because those tests enforce link validity and semantic rules, not canonical-folder membership.

- **VERIFIED — narrowest adjustment point**
  - One place only: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`

## 3. Files changed

- **IMPLEMENTED**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-docs-implementation-temporary-governance-exception.md`

## 4. Exception implemented

- **IMPLEMENTED:** Added a single-purpose constant:
  - `TemporaryLegacyImplementationFolder = "implementation"`

- **IMPLEMENTED:** Updated the canonical-folder assertion so it now allows:
  - any folder in the existing `CanonicalFolders` list
  - **or only** `docs/implementation/**` via the explicit temporary legacy constant

- **IMPLEMENTED:** Added inline explanatory comment in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs` stating:
  - the exception is temporary
  - the folder is legacy/transitional
  - canonical enforcement still applies elsewhere
  - the exception must be removed after migration

- **NOT IMPLEMENTED:** No broad folder wildcarding
- **NOT IMPLEMENTED:** No documentation content move/reorganization
- **NOT IMPLEMENTED:** No relaxation for any other non-canonical docs folder

## 5. Verification results

- **VERIFIED — targeted documentation governance run**
  - command:
    - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore --filter "DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames|DocumentationVerification_AllMarkdownLinksAndAnchorsResolve|DocumentationCompliance_ActiveFoldersDoNotReferenceDeprecatedIngestionTerminology|DocumentationVerification_ActiveDocumentationContainsNoSemanticLeakage"`
  - result:
    - `Passed: 4, Failed: 0`

- **VERIFIED — full unit test project**
  - command:
    - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore`
  - result:
    - `Passed: 2087, Failed: 0`

- **VERIFIED — narrow exception evidence**
  - Only `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs` changed for the rule.
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationVerificationBatch6Tests.cs` was inspected but not changed.

## 6. Residual risks

- **VERIFIED:** `docs/implementation/**` remains a non-canonical legacy folder in the repository.
- **BLOCKER:** None for this narrow repair phase.
- **Residual risk:** The temporary exception can become permanent drift if the legacy folder is never migrated.
- **Residual risk:** Future contributors may wrongly assume `docs/implementation/**` is now canonical unless the inline comment and report are read carefully.

## 7. Recommended future cleanup

- **IMPLEMENTED recommendation:** Keep the temporary exception only until the repository migrates or archives `docs/implementation/**`.
- **NOT IMPLEMENTED:** The actual migration/cleanup of `docs/implementation/**` was intentionally deferred because this phase forbids broad documentation reorganization.
- **Recommended removal trigger:** Remove the exception when the remaining files under `docs/implementation/**` have been moved into canonical locations such as `docs/reports`, `docs/history`, `docs/analysis`, or `docs/plans` as appropriate.

## Final section

### IMPLEMENTED
- Narrow temporary exception for `docs/implementation/**` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
- Inline explanatory comment documenting temporary transitional status
- Required markdown report at `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-docs-implementation-temporary-governance-exception.md`

### NOT IMPLEMENTED
- No broad documentation reorganization
- No weakening for any folder other than `docs/implementation/**`
- No production-code changes

### BLOCKERS
- None in this phase

### Evidence (files/tests/commands)
- changed file:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/DocumentationComplianceBatch5Tests.cs`
- report:
  - `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-18-docs-implementation-temporary-governance-exception.md`
- commands:
  - `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj`
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-restore`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore --filter "DocumentationCompliance_AllMarkdownFilesUseCanonicalFoldersAndNames|DocumentationVerification_AllMarkdownLinksAndAnchorsResolve|DocumentationCompliance_ActiveFoldersDoNotReferenceDeprecatedIngestionTerminology|DocumentationVerification_ActiveDocumentationContainsNoSemanticLeakage"`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --no-restore`

### GO/NO-GO for returning to normal development
- **GO** for normal development, with the explicit caveat that `docs/implementation/**` remains temporary legacy content and should still be cleaned up later.
