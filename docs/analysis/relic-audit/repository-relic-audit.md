> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Repository Relic Audit

## 1. Executive summary

- **Total relic candidates reviewed:** 24
- **High-confidence removal candidates:** 4
- **High-confidence archive/move candidates:** 13
- **Areas with the most obsolete residue:**
  - OData/revision-ingestion experiment residue in reports, investigations, and validator-only configuration
  - Root-level markdown reports from one-off audits and implementation summaries
  - Inconsistent documentation folder naming conventions (`docs/Reports` vs `docs/reports`, `docs/audit` vs `docs/audits`, `docs/analyze` vs `docs/analysis`)

Baseline verification before this audit:

- `dotnet restore PoTool.sln --nologo`
- `dotnet build PoTool.sln --configuration Release --no-restore --nologo`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal`
- `dotnet test PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build --nologo -v minimal`

All of the above passed in the current checkout, so this report treats candidates as architectural/documentation residue rather than currently broken code.

## 2. Candidate inventory table

| ID | Type | Path | Name | Suspected original purpose | Current status | Evidence of non-use or obsolescence | Confidence | Recommended action |
|---|---|---|---|---|---|---|---|---|
| C01 | Code | `PoTool.Core/Configuration/RevisionIngestionV2Options.cs` | `RevisionIngestionV2Options` | Planned V2 streaming revision ingestor toggle/options | Not referenced from runtime, validator, tests, DI, or appsettings | Repo search finds only the type declaration; no registrations or consumers | High | Remove |
| C02 | Code | `PoTool.Integrations.Tfs/Diagnostics/RevisionIngestionDiagnostics.cs` | `RevisionIngestionDiagnostics` | Diagnostics helper for removed revision-ingestion pipeline | Effectively disconnected | Only referenced by optional ctor parameter in `TfsRequestThrottler`; both API and validator register `AddSingleton<TfsRequestThrottler>()` without registering `RevisionIngestionDiagnostics`, so DI uses the default `null` value; no callers invoke `StartRun`, `BeginPageScope`, or page logging methods | High | Remove |
| C03 | Config | `PoTool.Core/Configuration/RevisionIngestionDiagnosticsOptions.cs` | `RevisionIngestionDiagnosticsOptions` | Options backing `RevisionIngestionDiagnostics` | Disconnected with C02 | Only referenced by `RevisionIngestionDiagnostics`; no `Configure<RevisionIngestionDiagnosticsOptions>` registration found | High | Remove |
| C04 | Config | `PoTool.Tools.TfsRetrievalValidator/appsettings.json` | `AnalyticsODataBaseUrl`, `AnalyticsODataEntitySetPath` keys | Old explicit OData endpoint configuration for validator/revision ingestor | Misleading leftover config | `TfsConfigEntity` no longer defines these properties; repo search shows these keys are only in this file and old migrations; `RealTfsClient` derives OData metadata URL from `Url` + `Project` instead | High | Remove |
| C05 | Code | `PoTool.Shared/Settings/AnalyticsODataDefaults.cs` | `AnalyticsODataDefaults` | Shared OData path builder | Still active but legacy-looking | Used by `RealTfsClient.WorkItems.cs` during `ValidateConnectionAsync`; verified by `RealTfsClientVerificationTests` OData metadata failure test | High | Remove in Batch 3 |
| C06 | Doc | `docs/reports/revision-ingestor-v2.md` | Revision Ingestor V2 | Design note for planned V2 OData ingestor | Stale design for unimplemented direction | Recommends `RevisionIngestionV2` appsettings and DI dispatcher that do not exist in current code; only code match is unused `RevisionIngestionV2Options` | High | Archive under `docs/archive` |
| C07 | Report | `docs/reports/odata-validator-vs-ingestion-report.md` | OData Validator vs Ingestion Report | Compare validator OData path vs production ingestion path | Misleading historical report | Describes `RevisionIngestionService`, `IWorkItemRevisionSource`, and `RealODataRevisionTfsClient`, none of which exist in current tree | High | Move under `docs/history` or `docs/archive` |
| C08 | Report | `docs/investigations/revision-ingestion-api-vs-validator-odata-divergence.md` | Revision ingestion divergence investigation | Investigate old OData pagination mismatch | Misleading historical report | Same deleted types as C07 presented as current runtime flow | High | Move under `docs/history` or `docs/archive` |
| C09 | Report | `docs/reviews/RealRevisionTfsClient_Pagination_Review.md` | RealRevisionTfsClient Pagination Review | Review a now-removed revision client | Misleading historical report | Entire report centers on `RealRevisionTfsClient.cs`, `IRevisionTfsClient`, `RevisionSyncStage`, `RelationRevisionHydrator`, which are not in current code | High | Archive under `docs/archive` |
| C10 | Report | `docs/reports/sprint-trends-vs-revisions-report.md` | Sprint Trends vs Revision Database | Analyze sprint trends on old revision DB architecture | Obsolete architecture report | Describes `RevisionHeaders`, `RevisionFieldDeltas`, `RevisionRelationDeltas`, `RevisionIngestionWatermarks` as active dependencies, but those were dropped by `20260223221758_RemoveRevisionPersistenceSchema.cs` | High | Archive under `docs/archive` |
| C11 | Report | `REPORT.md` | Cache Insights & Validation — Design Report | Design for cache insights and revision-cache validation | Stale and root-misplaced | Lists removed tables (`RevisionHeaders`, `RevisionFieldDeltas`, `RevisionRelationDeltas`, `RevisionIngestionWatermarks`) as current cache items; generic filename hides content | High | Archive under `docs/archive` |
| C12 | Doc | `CODE_AUDIT_REPORT.md` | Code Quality & Architecture Audit Report | Point-in-time audit deliverable | Historical, root-misplaced | Dated `2026-01-30`; reports 674 tests / 625 passing, while current baseline is 1711 + 1 tests all passing | High | Move under `docs/history` |
| C13 | Doc | `FINAL_SUMMARY.md` | Final Summary | Summary of C12 follow-up work | Historical, root-misplaced | Explicitly tied to `CODE_AUDIT_REPORT.md`; dated `2026-01-30`; current repo state has moved on | High | Move under `docs/history` |
| C14 | Doc | `FIXES_APPLIED.md` | Fixes Applied | Work log for C12 findings | Historical, root-misplaced | Explicitly “Based on: CODE_AUDIT_REPORT.md”; dated; tied to a completed branch effort | High | Move under `docs/history` |
| C15 | Doc | `NON_TEST_ISSUES_ANALYSIS.md` | Non-Test Issues Analysis | Follow-up analysis for C12 findings | Historical, root-misplaced | Explicitly about C12; dated; not canonical architecture or user documentation | High | Move under `docs/history` |
| C16 | Doc | `WORK_COMPLETED.md` | Work Completed | Completion summary for C12 work | Historical duplicate summary | Overlaps `FINAL_SUMMARY.md`; references the same report set as deliverables | High | Archive under `docs/archive` |
| C17 | Doc | `VALIDATORS_IMPLEMENTATION.md` | Validators Implementation Summary | Implementation summary of validator additions | Historical implementation report in root | Dated `2026-01-30`; coverage numbers are point-in-time and not canonical process/architecture docs | Medium | Move under `docs/history` |
| C18 | Doc | `VALIDATION_SYSTEM_REPORT.md` | Validation System Comprehensive Report | Enduring validation architecture description | Likely still useful but misplaced | `docs/analysis/backlog_quality_domain_exploration.md` references `/VALIDATION_SYSTEM_REPORT.md`; content describes active handlers and validators, not obviously stale | Medium | Move under `docs/architecture` |
| C19 | Doc | `SWEPO_REVIEW_REPORT.md` | Senior SWE/PO Review Report | Review artifact generated by prompt workflow | Suspicious but still workflow-connected | `prompts/seniorswe_and_seniorpo_review` explicitly instructs writing to `SWEPO_REVIEW_REPORT.md` in repo root | Medium | Keep because still active, or move only together with prompt update |
| C20 | Other | `PoTool.Tools.TfsRetrievalValidator/` | TFS Retrieval Validator tool | Manual diagnostic validator for TFS/revision retrieval | Legacy-feeling but still active in build/test workflow | Included in `PoTool.sln`; referenced by architecture tests and multiple docs; builds in baseline | Medium | Remove in Batch 3 |
| C21 | Code | `PoTool.Client/Pages/LegacyWorkspaces/*`, `PoTool.Client/Models/WorkspaceRoutes.cs` | Legacy workspace pages/routes | Earlier workspace navigation model | Still active, not dead | Routes still exist, `NavigationContextService` still maps intents through `GetRouteForIntent`, tests and docs reference them | High | Keep because still active |
| C22 | Doc | `docs/Reports/SprintAttributionAnalysis.md` | Sprint Attribution Strategy Analysis | Active analysis report | Misplaced by folder taxonomy | Appears current, but sits alone under capitalized `docs/Reports` while related files live under `docs/reports` | High | Move under `docs/reports` |
| C23 | Doc | `docs/audit/cdc-full-quality-audit.md` | CDC Full Quality Audit | Active audit document | Misplaced by folder taxonomy | Appears current, but sits alone under singular `docs/audit` while similar documents live under `docs/audits` | High | Move under `docs/audits` |
| C24 | Doc | `docs/README.md` | Documentation Index | Docs landing page | Active but partially misleading | References missing `mock-data-rules.md`; current docs taxonomy no longer matches a single clean structure | Medium | Remove in Batch 3 |

## 3. OData and failed experiment review

### Artifacts found

| Artifact | Location | Current connection | Assessment | Recommendation |
|---|---|---|---|---|
| `AnalyticsODataDefaults` | `PoTool.Shared/Settings/AnalyticsODataDefaults.cs` | **Connected** to `RealTfsClient.ValidateConnectionAsync` | Not dead code; it is the remaining OData touchpoint used to validate the analytics metadata endpoint | Keep, but document that this is only for connection verification |
| OData metadata validation step | `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs:76-96` | **Connected** to runtime verification and config import flows via `ValidateConnectionAsync` | Active behavior, even though bulk OData ingestion was removed | Keep |
| OData verification test | `PoTool.Tests.Unit/Services/RealTfsClientVerificationTests.cs:307-320` | **Connected** to tests | Confirms the validation path still requires OData metadata success | Keep |
| Validator OData config keys | `PoTool.Tools.TfsRetrievalValidator/appsettings.json:9-10` | **Not connected** | Keys are ignored by current code and no longer exist on `TfsConfigEntity` | Remove |
| `AddRevisionSourceConfiguration` migration | `PoTool.Api/Migrations/20260218162240_AddRevisionSourceConfiguration.cs` | Historical only | Shows OData/revision-source experiment was once first-class | Keep because migration history |
| `AddProfileRevisionSourceOverride` migration | `PoTool.Api/Migrations/20260218175925_AddProfileRevisionSourceOverride.cs` | Historical only | Same as above | Keep because migration history |
| `RemoveRevisionPersistenceSchema` migration | `PoTool.Api/Migrations/20260223221758_RemoveRevisionPersistenceSchema.cs` | Historical only | Confirms revision DB and source-switch experiment were removed | Keep because migration history |
| `DropLegacyODataColumns` migration | `PoTool.Api/Migrations/20260225173702_DropLegacyODataColumns.cs` | Historical only | Confirms old explicit OData config columns were intentionally removed from persisted config | Keep because migration history |
| `docs/reports/odata-validator-vs-ingestion-report.md` | Report | **Not connected** | Describes deleted classes as active | Archive/history |
| `docs/investigations/revision-ingestion-api-vs-validator-odata-divergence.md` | Investigation | **Not connected** | Same stale OData runtime narrative | Archive/history |
| `docs/archive/legacy-revision-ingestion/odata-ingestion-fix-plan.md` | Report/plan | **Not connected** | Follow-up plan for deleted ingestion components | Archive/history |
| `docs/reports/revision-ingestor-v2.md` | Design note | **Not connected** | Documents a V2 ingestor that was never completed and is no longer wired | Archive |
| `docs/reviews/RealRevisionTfsClient_Pagination_Review.md` | Review report | **Not connected** | Reviews removed classes and a removed pipeline stage | Archive |
| `docs/reports/sprint-trends-vs-revisions-report.md` | Engineering report | **Not connected** | Still treats revision DB tables as active dependencies | Archive |

### What the current code says

- **Current historical work-item change ingestion is activity-event based**, not OData revision-ingestion based:
  - `PoTool.Api/Services/ActivityEventIngestionService.cs`
  - `PoTool.Api/Services/Sync/ActivityIngestionSyncStage.cs`
  - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs:179-194`
- **Current per-item revision history uses REST revisions endpoints**, not OData:
  - `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemRevisions.cs`
- **OData still survives in one narrow place**: connection verification.

### Exact recommendation per OData-related area

1. **Keep** `AnalyticsODataDefaults` and the validation step, but add clarifying documentation later so developers do not infer that OData is still the main ingestion architecture.
2. **Remove** unused validator config keys `AnalyticsODataBaseUrl` and `AnalyticsODataEntitySetPath`.
3. **Remove** `RevisionIngestionV2Options` if no hidden dependency is discovered during implementation-time verification.
4. **Archive** OData/revision-ingestion reports that describe deleted classes in present tense.
5. **Retain** migrations as historical schema record.

## 4. Root-level documentation review

| File | Acceptable in repo root? | Why / why not | Proposed target location |
|---|---|---|---|
| `README.md` | Yes | Standard repository entry point | Keep in root |
| `CODE_AUDIT_REPORT.md` | No | Time-bound audit artifact, not canonical root documentation | `docs/history/code-quality/` |
| `FINAL_SUMMARY.md` | No | Time-bound summary of the same audit effort | `docs/history/code-quality/` |
| `FIXES_APPLIED.md` | No | Work log for a completed audit cycle | `docs/history/code-quality/` |
| `NON_TEST_ISSUES_ANALYSIS.md` | No | Time-bound follow-up analysis | `docs/history/code-quality/` |
| `WORK_COMPLETED.md` | No | Duplicate completion summary for the same audit cycle | `docs/archive/code-quality/` |
| `REPORT.md` | No | Generic name; stale revision-cache design; strongly misleading in root | `docs/archive/legacy-revision-ingestion/` |
| `VALIDATION_SYSTEM_REPORT.md` | Probably not | Content is still useful, but root placement makes it look like a transient report instead of architecture documentation | `docs/architecture/` |
| `VALIDATORS_IMPLEMENTATION.md` | No | Time-bound implementation summary, not canonical architecture | `docs/history/validation/` |
| `SWEPO_REVIEW_REPORT.md` | **Conditional** | Suspicious by default, but there is a strong current reason: `prompts/seniorswe_and_seniorpo_review` hardcodes this root filename | Keep for now, or move to `docs/reviews/` only together with prompt update |

## 5. Documentation structure problems

### Current documentation placement issues

1. **Parallel folder names for the same concept**
   - `docs/Reports` vs `docs/reports`
   - `docs/audit` vs `docs/audits`
   - `docs/analyze` vs `docs/analysis`
2. **Root of repository holds many one-off reports**
   - Most root markdown files are not entry-point docs
   - They are historical audits, implementation summaries, or review artifacts
3. **Docs root mixes durable guidance with transient analysis**
   - Durable rules files live beside planning and analysis documents such as `LIVE_TFS_CALLS_ANALYSIS.md`, `REALTFSCLIENT_GETALL_ANALYSIS.md`, `TFS_CACHE_IMPLEMENTATION_PLAN.md`, `UI_MIGRATION_PLAN.md`
4. **Broken or outdated indexing**
   - `docs/README.md` links to `mock-data-rules.md`, which is not present

### Duplicate or conflicting docs

- `WORK_COMPLETED.md` materially overlaps `FINAL_SUMMARY.md`
- Multiple OData/revision-ingestion reports describe now-deleted classes as if still current
- `docs/reports/2026-03-30-sprint-trends-current-state-analysis.md` correctly documents the post-removal state, but older reports nearby describe the pre-removal architecture without historical framing

### Reports mixed with enduring documentation

- `VALIDATION_SYSTEM_REPORT.md` is architecture/reference material but lives at repo root
- `docs/Reports/SprintAttributionAnalysis.md` appears current but is stranded in a capitalized singleton folder
- `docs/audit/cdc-full-quality-audit.md` appears current but is stranded in a singular singleton folder

### Temporary analysis mixed with user-facing docs

- `docs/README.md` is a user/developer entry point, but surrounding docs root files include deep internal analyses and plans
- `docs/GEBRUIKERSHANDLEIDING.md` (Dutch user manual) shares the same top-level namespace as engineering investigations rather than a `docs/user` area

## 6. Proposed cleanup batches

### Batch A: safe removals

- `PoTool.Core/Configuration/RevisionIngestionV2Options.cs`
- `PoTool.Integrations.Tfs/Diagnostics/RevisionIngestionDiagnostics.cs`
- `PoTool.Core/Configuration/RevisionIngestionDiagnosticsOptions.cs`
- `PoTool.Tools.TfsRetrievalValidator/appsettings.json` keys:
  - `Tfs.AnalyticsODataBaseUrl`
  - `Tfs.AnalyticsODataEntitySetPath`

### Batch B: safe moves/archives

- Move root historical audit files (`CODE_AUDIT_REPORT.md`, `FINAL_SUMMARY.md`, `FIXES_APPLIED.md`, `NON_TEST_ISSUES_ANALYSIS.md`, `VALIDATORS_IMPLEMENTATION.md`) under `docs/history/code-quality/` or `docs/history/validation/` as listed in Section 7
- Archive `WORK_COMPLETED.md` under `docs/archive/code-quality/`
- Move `VALIDATION_SYSTEM_REPORT.md` to `docs/architecture`
- Move `docs/Reports/SprintAttributionAnalysis.md` to `docs/reports`
- Move `docs/audit/cdc-full-quality-audit.md` to `docs/audits`
- Archive stale OData/revision-ingestion reports under `docs/archive/legacy-revision-ingestion/`:
  - `docs/reports/revision-ingestor-v2.md`
  - `docs/reports/odata-validator-vs-ingestion-report.md`
  - `docs/investigations/revision-ingestion-api-vs-validator-odata-divergence.md`
  - `docs/reviews/RealRevisionTfsClient_Pagination_Review.md`
  - `docs/reports/sprint-trends-vs-revisions-report.md`
  - `REPORT.md`

### Batch C: needs manual review

- `SWEPO_REVIEW_REPORT.md` because prompt automation currently targets the repo root
- `PoTool.Tools.TfsRetrievalValidator/` because it still participates in build/tests/docs, even if it feels legacy
- `PoTool.Core/Configuration/RevisionIngestionPaginationOptions.cs` because it is still validator-connected, not dead
- The OData metadata validation logic in `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` (`ValidateConnectionAsync`)

### Batch D: keep but clarify

- `PoTool.Shared/Settings/AnalyticsODataDefaults.cs`
- Legacy workspace pages/routes under `PoTool.Client/Pages/LegacyWorkspaces` and `PoTool.Client/Models/WorkspaceRoutes.cs`
- `docs/README.md` (keep, but repair missing-link/reference issues during reorganization)

## 7. Proposed target documentation structure

Recommended target structure:

- `docs/architecture` — durable architecture, invariants, design references
- `docs/analysis` — dated investigations and short-lived analyses
- `docs/reports` — current still-relevant engineering/audit reports
- `docs/history` — completed implementation narratives and historical context still worth keeping
- `docs/archive` — obsolete, superseded, or misleading documents preserved only for record
- `docs/user` — end-user manuals and guides
- `docs/reviews` — review artifacts that are still operationally useful
- `docs/audits` — structured audit documents that remain current

### Suggested moves

| Current file | Proposed destination |
|---|---|
| `VALIDATION_SYSTEM_REPORT.md` | `docs/architecture/validation-system-report.md` |
| `CODE_AUDIT_REPORT.md` | `docs/archive/code-quality/code-audit-report-2026-01-30.md` |
| `FINAL_SUMMARY.md` | `docs/archive/code-quality/final-summary-2026-01-30.md` |
| `FIXES_APPLIED.md` | `docs/archive/code-quality/fixes-applied-2026-01-30.md` |
| `NON_TEST_ISSUES_ANALYSIS.md` | `docs/archive/code-quality/non-test-issues-analysis-2026-01-30.md` |
| `VALIDATORS_IMPLEMENTATION.md` | `docs/archive/validation/validators-implementation-2026-01-30.md` |
| `WORK_COMPLETED.md` | `docs/archive/code-quality/work-completed-2026-01-30.md` |
| `REPORT.md` | `docs/analysis/cache-insights-and-validation-report.md` |
| `docs/Reports/SprintAttributionAnalysis.md` | `docs/reports/2026-03-30-sprint-attribution-analysis.md` |
| `docs/audit/cdc-full-quality-audit.md` | `docs/analysis/cdc-full-quality-audit.md` |
| `docs/reports/revision-ingestor-v2.md` | `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md` |
| `docs/reports/odata-validator-vs-ingestion-report.md` | `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md` |
| `docs/investigations/revision-ingestion-api-vs-validator-odata-divergence.md` | `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md` |
| `docs/reviews/RealRevisionTfsClient_Pagination_Review.md` | `docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md` |
| `docs/reports/sprint-trends-vs-revisions-report.md` | `docs/analysis/sprint-trends-vs-revisions-report.md` |
| `docs/GEBRUIKERSHANDLEIDING.md` | `docs/architecture/gebruikershandleiding.md` |
| `SWEPO_REVIEW_REPORT.md` | `docs/reports/2026-03-30-swepo-review-report.md` **only if** `prompts/seniorswe_and_seniorpo_review` is updated in the same change |

## 8. Risks of cleanup

- **Prompt/workflow breakage:** moving `SWEPO_REVIEW_REPORT.md` without updating `prompts/seniorswe_and_seniorpo_review` will break that workflow.
- **Historical schema context loss:** deleting migrations or their designer files would damage database history. They should be kept even when they document removed features.
- **False “dead code” removal:** `AnalyticsODataDefaults` looks legacy, but removing it would break current TFS connection validation and related tests.
- **Validator-tool coupling:** `PoTool.Tools.TfsRetrievalValidator` is not part of normal runtime, but it is part of the solution, is referenced by tests, and is still part of team workflow documentation.
- **Link rot:** many documents reference one another by current relative path or filename. Moves should update links and any document tests that assert exact paths/content.
- **Documentation ambiguity during transition:** some stale reports are useful as historical context. Archiving is safer than deleting when the document is referenced as prior design rationale.

### Candidates requiring runtime or workflow verification before deletion

- `AnalyticsODataDefaults.cs`
- `PoTool.Tools.TfsRetrievalValidator/`
- `RevisionIngestionPaginationOptions.cs`
- `SWEPO_REVIEW_REPORT.md` relocation

## 9. Recommended next prompt

Use this exact next implementation prompt:

> Reorganize repository documentation based on the current repository relic audit report at `docs/analysis/relic-audit/repository-relic-audit.md` (or its moved equivalent if this path changes during the reorganization).  
>  
> Scope:
> - move root-level documentation out of the repository root except `README.md`
> - normalize documentation folders so `docs/Reports` → `docs/reports`, `docs/audit` → `docs/audits`, and merge `docs/analyze` into `docs/analysis` as the canonical target while preserving and reviewing existing content from both folders
> - move enduring architecture docs under `docs/architecture`
> - move user-facing docs under `docs/user`
> - move historical but still useful docs under `docs/history`
> - move obsolete/superseded OData/revision-ingestion reports under `docs/archive`
> - preserve or update `SWEPO_REVIEW_REPORT.md` handling safely; if you move it, update `prompts/seniorswe_and_seniorpo_review` in the same change
> - update links, references, indexes, and any document tests so nothing breaks
> - do not delete EF migration history or other schema-history artifacts
>  
> Constraints:
> - make no behavioral code changes
> - be conservative with deletions; prefer archive over delete when historical context may matter
> - verify all moved documents still have valid inbound/outbound references
> - run existing build/tests that cover document validity after the reorganization
