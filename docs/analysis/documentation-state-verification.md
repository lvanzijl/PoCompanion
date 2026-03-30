# Documentation State Verification

_Scan basis: filesystem inventory of all `*.md` files under `/home/runner/work/PoCompanion/PoCompanion`, excluding `.git/`, `bin/`, and `obj/`._

## 1. Summary
- Total markdown files: **294**
- Compliant files: **73**
- Non-compliant files: **221**
- Critical violations:
  - **26** markdown files still sit directly under `docs/` instead of canonical subfolders.
  - **93** files are in the wrong folder for their current purpose/lifecycle.
  - **157** files violate lowercase and/or kebab-case naming rules.
  - **7** noncanonical `docs/` subtrees remain: `docs/cleanup`, `docs/domain`, `docs/exploration`, `docs/filters`, `docs/implementation`, `docs/roadmaps`, `docs/screenshots`.
  - `.github/github-instructions` is absent, so `.github` alignment currently depends entirely on `.github/copilot-instructions.md`.
  - OData / validator residue is still active in current docs, solution/project references, tests, and the `PoTool.Tools.TfsRetrievalValidator` project.

## 2. Root violations
- None. `README.md` is the only markdown file at repository root.

## 3. Docs root violations
| Path | Expected folder | Reason |
| --- | --- | --- |
| `docs/ARCHITECTURE_RULES.md` | `docs/architecture` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/COPILOT_ARCHITECTURE_CONTRACT.md` | `docs/architecture` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/EF_RULES.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/Fluent_UI_compat_rules.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/LIVE_TFS_CALLS_ANALYSIS.md` | `docs/analysis` | Stored directly under `docs/` even though its purpose is `exploratory analysis or investigation`. |
| `docs/MULTI_SELECT_BEHAVIOR.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/NAVIGATION_MAP.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/PROCESS_RULES.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/REALTFSCLIENT_GETALL_ANALYSIS.md` | `docs/analysis` | Stored directly under `docs/` even though its purpose is `exploratory analysis or investigation`. |
| `docs/TFS_CACHE_IMPLEMENTATION_PLAN.md` | `docs/plans` | Stored directly under `docs/` even though its purpose is `plan or migration guidance`. |
| `docs/TFS_INTEGRATION_RULES.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/UI_LOADING_RULES.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/UI_MIGRATION_PLAN.md` | `docs/plans` | Stored directly under `docs/` even though its purpose is `plan or migration guidance`. |
| `docs/UI_RULES.md` | `docs/rules` | Stored directly under `docs/` even though its purpose is `normative rule or governance reference`. |
| `docs/bug_trend_followups.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/health_additional_signals.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/health_workspace_fix_plan.md` | `docs/plans` | Stored directly under `docs/` even though its purpose is `plan or migration guidance`. |
| `docs/iteration_path_sorting_audit.md` | `docs/audits` | Stored directly under `docs/` even though its purpose is `structured audit or compliance verification`. |
| `docs/navigation_decision_backlog.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/navigation_followup_actions.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/pr_template.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/sprint-scoping-limitations.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/sprintmetrics_iteration_migration_plan.md` | `docs/plans` | Stored directly under `docs/` even though its purpose is `plan or migration guidance`. |
| `docs/sqlite-datetime-fix.md` | `docs/other` | Stored directly under `docs/` even though its purpose is `general markdown documentation`. |
| `docs/sqlite-timestamp-fix-audit.md` | `docs/audits` | Stored directly under `docs/` even though its purpose is `structured audit or compliance verification`. |
| `docs/test-determinism-report.md` | `docs/reports` | Stored directly under `docs/` even though its purpose is `report or summarized findings`. |

## 4. Misplaced documents
| Path | Current folder | Expected folder | Reason |
| --- | --- | --- | --- |
| `docs/ARCHITECTURE_RULES.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/architecture. |
| `docs/COPILOT_ARCHITECTURE_CONTRACT.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/architecture. |
| `docs/EF_RULES.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/Fluent_UI_compat_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/LIVE_TFS_CALLS_ANALYSIS.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/PROCESS_RULES.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/REALTFSCLIENT_GETALL_ANALYSIS.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/TFS_CACHE_IMPLEMENTATION_PLAN.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/TFS_INTEGRATION_RULES.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/UI_LOADING_RULES.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/UI_MIGRATION_PLAN.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/UI_RULES.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/analysis/cdc-corrections.md` | `analysis` | `audits` | Document reads as a completed verification artifact rather than active exploratory analysis. |
| `docs/analysis/ef-sqlite-compatibility-audit.md` | `analysis` | `audits` | Document reads as a completed verification artifact rather than active exploratory analysis. |
| `docs/analysis/final-cdc-integration.md` | `analysis` | `audits` | Document reads as a completed verification artifact rather than active exploratory analysis. |
| `docs/analysis/relic-audit/documentation-reorganization-report.md` | `analysis` | `audits` | Document reads as a completed verification artifact rather than active exploratory analysis. |
| `docs/analysis/relic-audit/repository-relic-audit.md` | `analysis` | `audits` | Document reads as a completed verification artifact rather than active exploratory analysis. |
| `docs/analysis/validation-rules.md` | `analysis` | `rules` | Inferred purpose is normative rule or governance reference; governance expects docs/rules. |
| `docs/architecture/canonical-workitem-test-fix.md` | `architecture` | `reports` | Document title/content are report-style current-state findings, not durable architecture reference. |
| `docs/architecture/cdc-decision-record.md` | `architecture` | `plans` | Document is forward-looking planning/migration material rather than stable architecture guidance. |
| `docs/architecture/cdc-domain-map-audit-fix.md` | `architecture` | `audits` | Inferred purpose is structured audit or compliance verification; governance expects docs/audits. |
| `docs/architecture/final-test-fixes.md` | `architecture` | `reports` | Document title/content are report-style current-state findings, not durable architecture reference. |
| `docs/architecture/incremental-sync-planner.md` | `architecture` | `plans` | Document is forward-looking planning/migration material rather than stable architecture guidance. |
| `docs/architecture/pipeline-time-semantics-migration.md` | `architecture` | `plans` | Document is forward-looking planning/migration material rather than stable architecture guidance. |
| `docs/architecture/portfolio-backlog-regression-investigation.md` | `architecture` | `analysis` | Document is investigative analysis rather than evergreen architecture source-of-truth. |
| `docs/architecture/repository-stability-audit.md` | `architecture` | `audits` | Inferred purpose is structured audit or compliance verification; governance expects docs/audits. |
| `docs/architecture/restore-build-determinism-audit.md` | `architecture` | `audits` | Inferred purpose is structured audit or compliance verification; governance expects docs/audits. |
| `docs/architecture/scatter-ordering-regression.md` | `architecture` | `analysis` | Document is investigative analysis rather than evergreen architecture source-of-truth. |
| `docs/architecture/test-failure-cluster-fix.md` | `architecture` | `reports` | Document title/content are report-style current-state findings, not durable architecture reference. |
| `docs/architecture/validation-system-report.md` | `architecture` | `reports` | Document title/content are report-style current-state findings, not durable architecture reference. |
| `docs/archive/code-quality/work-completed-2026-01-30.md` | `archive` | `audits` | Archived location does not match the document title/content; either the file needs a dated archive name or its content still reads as active material. |
| `docs/archive/legacy-revision-ingestion/cache-insights-and-validation-report.md` | `archive` | `reports` | Archived location does not match the document title/content; either the file needs a dated archive name or its content still reads as active material. |
| `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md` | `archive` | `reports` | Archived location does not match the document title/content; either the file needs a dated archive name or its content still reads as active material. |
| `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md` | `archive` | `analysis` | Inferred purpose is exploratory analysis or investigation; governance expects docs/analysis. |
| `docs/archive/legacy-revision-ingestion/sprint-trends-vs-revisions-report.md` | `archive` | `reports` | Archived location does not match the document title/content; either the file needs a dated archive name or its content still reads as active material. |
| `docs/cleanup/phase1-client-reachability-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/cleanup/phase2-endpoint-usage-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/cleanup/phase3-handler-usage-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/cleanup/phase4-full-layer-summary.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/domain/REPOSITORY_DOMAIN_DISCOVERY.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/architecture. |
| `docs/domain/backlog_quality_domain_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/cdc_domain_map.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/cdc_domain_map_generated.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/cdc_reference.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/domain_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/effort_diagnostics_domain_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/forecasting_domain_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/portfolio_flow_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/rules/estimation_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/hierarchy_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/metrics_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/propagation_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/source_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/sprint_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/rules/state_rules.md` | `other` | `rules` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/rules. |
| `docs/domain/sprint_commitment_cdc_summary.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/architecture. |
| `docs/domain/sprint_commitment_domain_model.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is architecture or reference documentation, so it belongs under docs/architecture. |
| `docs/domain/ui_semantic_rules.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/architecture. |
| `docs/exploration/sprint_commitment_domain_exploration.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/filters/cache-only-guardrail-analysis-pipeline-workitems.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/filters/filter-analysis-improved.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/filters/filter-analysis.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/filters/filter-cross-slice-migration.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-current-state-analysis.md` | `other` | `analysis` | Stored outside the canonical docs taxonomy; inferred purpose is exploratory analysis or investigation, so it belongs under docs/analysis. |
| `docs/filters/filter-delivery-migration.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-final-cleanup-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/filters/filter-implementation-execution-plan.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-implementation-plan.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-performance-audit.md` | `other` | `audits` | Stored outside the canonical docs taxonomy; inferred purpose is structured audit or compliance verification, so it belongs under docs/audits. |
| `docs/filters/filter-pipeline-migration.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-pr-migration.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-sprint-migration.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/filter-validation-report.md` | `other` | `audits` | Stored outside the canonical docs taxonomy; inferred purpose is structured audit or compliance verification, so it belongs under docs/audits. |
| `docs/filters/page-filter-contracts.md` | `other` | `architecture` | Stored outside the canonical docs taxonomy; inferred purpose is normative rule or governance reference, so it belongs under docs/architecture. |
| `docs/filters/pipeline-guardrail-and-workitem-split.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/filters/pr-live-provider-usage-audit.md` | `other` | `audits` | Stored outside the canonical docs taxonomy; inferred purpose is structured audit or compliance verification, so it belongs under docs/audits. |
| `docs/health_workspace_fix_plan.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/history/code-quality/code-audit-report-2026-01-30.md` | `history` | `audits` | Historical folder conflicts with present-tense audit content that still reads as a current check. |
| `docs/history/code-quality/final-summary-2026-01-30.md` | `history` | `audits` | Historical folder conflicts with present-tense audit content that still reads as a current check. |
| `docs/history/code-quality/fixes-applied-2026-01-30.md` | `history` | `audits` | Historical folder conflicts with present-tense audit content that still reads as a current check. |
| `docs/history/code-quality/non-test-issues-analysis-2026-01-30.md` | `history` | `audits` | Historical folder conflicts with present-tense audit content that still reads as a current check. |
| `docs/history/validation/validators-implementation-2026-01-30.md` | `history` | `reports` | Inferred purpose is report or summarized findings; governance expects docs/reports. |
| `docs/implementation/battleship-cdc-extension-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/implementation/cdc-fallback-timestamp-hardening.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/implementation/cdc-fix-report-empty-snapshot-snapshotcount.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |
| `docs/iteration_path_sorting_audit.md` | `other` | `audits` | Stored outside the canonical docs taxonomy; inferred purpose is structured audit or compliance verification, so it belongs under docs/audits. |
| `docs/reports/sprint-attribution-analysis.md` | `reports` | `analysis` | Report folder overstates stability; the file still reads as exploratory/problem-analysis material. |
| `docs/reports/sprint-trends-current-state-analysis.md` | `reports` | `analysis` | Report folder overstates stability; the file still reads as exploratory/problem-analysis material. |
| `docs/reviews/swepo-review-report.md` | `reviews` | `reports` | Review folder does not match the file’s present-tense report content. |
| `docs/roadmaps/application_simplification_plan.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/sprintmetrics_iteration_migration_plan.md` | `other` | `plans` | Stored outside the canonical docs taxonomy; inferred purpose is plan or migration guidance, so it belongs under docs/plans. |
| `docs/sqlite-timestamp-fix-audit.md` | `other` | `audits` | Stored outside the canonical docs taxonomy; inferred purpose is structured audit or compliance verification, so it belongs under docs/audits. |
| `docs/test-determinism-report.md` | `other` | `reports` | Stored outside the canonical docs taxonomy; inferred purpose is report or summarized findings, so it belongs under docs/reports. |

## 5. Naming violations
- Total naming violations: **157**
  - missing required date: **6**
  - not kebab-case: **150**
  - not lowercase: **26**
- Highest-density folders:
  - `docs/audits`: **86** naming violations
  - `docs`: **22** naming violations
  - `features`: **15** naming violations
  - `docs/domain`: **12** naming violations
  - `docs/domain/rules`: **7** naming violations
  - `docs/archive/legacy-revision-ingestion`: **6** naming violations
  - `.github`: **1** naming violations
  - `docs/architecture`: **1** naming violations
  - `docs/exploration`: **1** naming violations
  - `docs/filters`: **1** naming violations
- Required-date violations (historical/archive files missing dates):
  - `docs/archive/legacy-revision-ingestion/cache-insights-and-validation-report.md`
  - `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md`
  - `docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md`
  - `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md`
  - `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md`
  - `docs/archive/legacy-revision-ingestion/sprint-trends-vs-revisions-report.md`
- Notable non-`docs/` naming violations:
  - `.github/pull_request_template.md` — not kebab-case
  - `features/02032026_backlog_health.md` — not kebab-case
  - `features/20260110_User_profile_creation.md` — not lowercase, not kebab-case
  - `features/20260119_workitem_validation.md` — not kebab-case
  - `features/20260126_epic_planning_v2.md` — not kebab-case
  - `features/Dependency_graph.md` — not lowercase, not kebab-case
  - `features/Pipeline_insights.md` — not lowercase, not kebab-case
  - `features/Simple_workitem_explorer.md` — not lowercase, not kebab-case
  - `features/User_landing_v2.md` — not lowercase, not kebab-case
  - `features/VERIFY_TFS_API_INTEGRATION.md` — not lowercase, not kebab-case
  - `features/Verify_TFS_API.md` — not lowercase, not kebab-case
  - `features/effort_distribution_analytics.md` — not kebab-case
  - `features/epic_planning.md` — not kebab-case
  - `features/planning_board_decommission.md` — not kebab-case
  - `features/plans/20260110_User_profile_creation_plan.md` — not lowercase, not kebab-case
  - `features/pr_insight.md` — not kebab-case
  - `features/state_timeline.md` — not kebab-case
  - `prompts/CONTEXT_PACK.MD` — not lowercase, not kebab-case

## 6. Structural issues
- Noncanonical `docs/` directories still present:
  - `docs/cleanup` (**5** markdown files)
  - `docs/domain` (**19** markdown files)
  - `docs/exploration` (**1** markdown files)
  - `docs/filters` (**33** markdown files)
  - `docs/implementation` (**15** markdown files)
  - `docs/roadmaps` (**1** markdown files)
  - `docs/screenshots` (**1** markdown files)
- Mixed-purpose folders with obvious taxonomy drift:
  - `docs/architecture` mixes enduring architecture references with reports, audits, implementation fixes, and planning documents.
  - `docs/audits` mixes audits with exploration, domain discovery, strategy, and implementation summary documents.
  - `docs/filters` mixes analysis, plans, audits, reports, architecture contracts, and tactical fix notes in one topic folder.
  - `docs/implementation` mixes implementation narratives, reports, and unfinished phase notes instead of separating active plans from historical output.
  - `features/` mixes dated historical feature notes, mixed-case filenames, and a nested `features/plans/` subtree outside canonical documentation governance.
- Deprecated-folder residue status:
  - No live `docs/analyze`, `docs/Reports`, or `docs/audit` directories were found.
  - Historical references to those old paths still appear in `docs/analysis/relic-audit/documentation-reorganization-report.md` and `docs/analysis/relic-audit/repository-relic-audit.md`, which is acceptable as migration history but confirms prior structure drift.
- Lifecycle mismatches:
  - `docs/analysis/2026-03-18-breadcrumbs-sprint-delivery.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-bug-trend-count-mismatch.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-incremental-sync-planner-live-validation.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-parent-move-causes-resolvedworkitem-without-workitem.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-pr-lifetime-scatter-ux-issues.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-progress-trend-effort-spike.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-18-sync-testability-architecture.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/analysis/2026-03-20-profile-export-import-and-onboarding-completion.md` — inferred lifecycle `historical` but current folder is `analysis`
  - `docs/cleanup/obsolete-changes-log.md` — inferred lifecycle `obsolete` but current folder is `other`

## 7. .github alignment issues
- `.github/github-instructions` is missing; there is no file to validate for referenced markdown inputs.
- `.github/copilot-instructions.md` references the following markdown files:
| Referenced file | Exists | Current folder | Expected folder | Should move under `.github` later? |
| --- | --- | --- | --- | --- |
| `docs/ARCHITECTURE_RULES.md` | Yes | `other` | `architecture` | Yes |
| `docs/COPILOT_ARCHITECTURE_CONTRACT.md` | Yes | `other` | `architecture` | Yes |
| `docs/EF_RULES.md` | Yes | `other` | `rules` | Yes |
| `docs/Fluent_UI_compat_rules.md` | Yes | `other` | `rules` | Yes |
| `docs/PROCESS_RULES.md` | Yes | `other` | `rules` | Yes |
| `docs/README.md` | Yes | `other` | `other` | No |
| `docs/UI_LOADING_RULES.md` | Yes | `other` | `rules` | Yes |
| `docs/UI_RULES.md` | Yes | `other` | `rules` | Yes |
| `docs/domain/domain_model.md` | Yes | `other` | `architecture` | No |
| `docs/domain/rules/estimation_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/hierarchy_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/metrics_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/propagation_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/source_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/sprint_rules.md` | Yes | `other` | `rules` | No |
| `docs/domain/rules/state_rules.md` | Yes | `other` | `rules` | No |
- Later `.github` consolidation candidates:
  - `docs/ARCHITECTURE_RULES.md`
  - `docs/COPILOT_ARCHITECTURE_CONTRACT.md`
  - `docs/EF_RULES.md`
  - `docs/Fluent_UI_compat_rules.md`
  - `docs/PROCESS_RULES.md`
  - `docs/UI_LOADING_RULES.md`
  - `docs/UI_RULES.md`

## 8. OData / validator residue
- Active references that still keep deprecated architecture/tooling alive:
| Path | Classification | Severity | Why it matters |
| --- | --- | --- | --- |
| `docs/reports/odata-ingestion-fix-plan.md` | documentation | active reference (incorrect) | Present-tense report still references deleted revision-ingestion types such as RevisionIngestionService and RealODataRevisionTfsClient. |
| `PoTool.Tools.TfsRetrievalValidator/appsettings.json` | other | active reference (incorrect) | Contains stale AnalyticsOData* keys and RevisionIngestionPagination settings that the relic audit already marked as misleading residue. |
| `PoTool.Tools.TfsRetrievalValidator/Program.cs` | code references | active reference (incorrect) | Validator still binds RevisionIngestionPaginationOptions and keeps the retrieval-validator tool active in the current solution surface. |
| `PoTool.Core/Configuration/RevisionIngestionV2Options.cs` | code references | active reference (incorrect) | Unconsumed V2 revision-ingestion options keep a deprecated design direction alive in code. |
| `PoTool.Integrations.Tfs/Diagnostics/RevisionIngestionDiagnostics.cs` | code references | active reference (incorrect) | Disconnected diagnostics helper retains revision-ingestion concepts without an active runtime registration path. |
| `PoTool.sln` | solution/project references | active reference (incorrect) | The validator project still ships in the solution, which keeps validator residue visible in normal build/test workflows. |
| `PoTool.Tests.Unit/PoTool.Tests.Unit.csproj` | solution/project references | active reference (incorrect) | Unit tests still project-reference the validator tool directly. |
| `PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs` | tests | active reference (incorrect) | Architecture tests still treat the validator as a first-class boundary participant. |
- Historical references that are acceptable to keep:
| Path | Classification | Severity | Why it is acceptable |
| --- | --- | --- | --- |
| `docs/archive/legacy-revision-ingestion/*` | documentation | historical reference (acceptable) | Archive subtree correctly preserves superseded OData/revision-ingestion material for traceability. |
| `PoTool.Api/Migrations/*DropLegacyODataColumns*.cs` | code references | historical reference (acceptable) | Migration history shows the old explicit OData configuration was intentionally removed. |
| `docs/analysis/relic-audit/repository-relic-audit.md` | documentation | historical reference (acceptable) | Current relic audit documents residue precisely; it should not be treated as evidence that OData is active architecture. |
- Unclear references that need targeted follow-up rather than immediate removal:
| Path | Classification | Severity | Follow-up need |
| --- | --- | --- | --- |
| `PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` | code references | unclear | OData remains in a narrow metadata validation path; this is active code, but it does not imply OData is the primary ingestion architecture. |
| `PoTool.Core/RevisionFieldWhitelist.cs` | code references | unclear | OData selection/parsing types remain present; more implementation-time review is needed before treating them as removable residue. |

## 9. Recommended next migration batches
- **Batch 1: safe moves**
  - Move the 26 `docs/`-root violations into canonical subfolders.
  - Move `docs/domain/*` material into `docs/architecture` / `docs/rules` and retire the noncanonical `docs/domain` subtree.
  - Move `docs/roadmaps/application_simplification_plan.md` into `docs/plans` and reclassify `docs/cleanup/*` and `docs/implementation/*` by current lifecycle.
- **Batch 2: rule consolidation**
  - Rename uppercase / underscore-heavy rule docs to lowercase kebab-case.
  - Decide whether agent-facing rule files referenced by `.github/copilot-instructions.md` should live under `.github/` in a later repository-instructions migration.
- **Batch 3: validator removal**
  - Decide whether `PoTool.Tools.TfsRetrievalValidator` remains part of the supported toolchain.
  - If not, remove the project from `PoTool.sln`, `PoTool.Tests.Unit`, and architecture-test assumptions, then archive validator-only docs.
- **Batch 4: OData cleanup**
  - Archive or rewrite `docs/reports/odata-ingestion-fix-plan.md` so it no longer presents deleted revision-ingestion components as current.
  - Remove stale validator `AnalyticsOData*` / `RevisionIngestionPagination` configuration and any disconnected revision-ingestion option/diagnostic classes that are confirmed unused.

## Appendix A. Complete markdown inventory

### analysis
- `docs/analysis/2026-03-18-breadcrumbs-sprint-delivery.md` — filename `2026-03-18-breadcrumbs-sprint-delivery.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-bug-trend-count-mismatch.md` — filename `2026-03-18-bug-trend-count-mismatch.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-incremental-sync-planner-live-validation.md` — filename `2026-03-18-incremental-sync-planner-live-validation.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-parent-move-causes-resolvedworkitem-without-workitem.md` — filename `2026-03-18-parent-move-causes-resolvedworkitem-without-workitem.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-pr-lifetime-scatter-ux-issues.md` — filename `2026-03-18-pr-lifetime-scatter-ux-issues.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-progress-trend-effort-spike.md` — filename `2026-03-18-progress-trend-effort-spike.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-18-sync-testability-architecture.md` — filename `2026-03-18-sync-testability-architecture.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/2026-03-20-profile-export-import-and-onboarding-completion.md` — filename `2026-03-20-profile-export-import-and-onboarding-completion.md` — purpose: exploratory analysis or investigation — title: Bug Analysis Report
- `docs/analysis/api-readmodels-validation.md` — filename `api-readmodels-validation.md` — purpose: exploratory analysis or investigation — title: API Read Models Validation
- `docs/analysis/cdc-corrections.md` — filename `cdc-corrections.md` — purpose: structured audit or compliance verification — title: Corrective CDC Integration Audit
- `docs/analysis/cross-slice-quality-feature-analysis.md` — filename `cross-slice-quality-feature-analysis.md` — purpose: exploratory analysis or investigation — title: Cross-Slice Quality Feature Analysis
- `docs/analysis/ef-sqlite-compatibility-audit.md` — filename `ef-sqlite-compatibility-audit.md` — purpose: structured audit or compliance verification — title: EF Core SQLite Compatibility Report
- `docs/analysis/epic-aggregation-null-semantics-fix.md` — filename `epic-aggregation-null-semantics-fix.md` — purpose: exploratory analysis or investigation — title: Epic Aggregation Null Semantics Fix
- `docs/analysis/epic-aggregation-validation.md` — filename `epic-aggregation-validation.md` — purpose: exploratory analysis or investigation — title: Epic Aggregation Validation
- `docs/analysis/feature-forecast-validation.md` — filename `feature-forecast-validation.md` — purpose: exploratory analysis or investigation — title: Feature Forecast Validation
- `docs/analysis/field-contract.md` — filename `field-contract.md` — purpose: normative rule or governance reference — title: Field Contract & Usage Analysis
- `docs/analysis/filtering.md` — filename `filtering.md` — purpose: exploratory analysis or investigation — title: Global Filters & Context Model Analysis
- `docs/analysis/final-cdc-integration.md` — filename `final-cdc-integration.md` — purpose: structured audit or compliance verification — title: Final CDC Integration Audit & Implementation Plan
- `docs/analysis/hierarchy-aggregation.md` — filename `hierarchy-aggregation.md` — purpose: exploratory analysis or investigation — title: Hierarchy & Aggregation Analysis
- `docs/analysis/insight-null-semantics-fix.md` — filename `insight-null-semantics-fix.md` — purpose: exploratory analysis or investigation — title: Insight Null Semantics Fix
- `docs/analysis/insight-validation.md` — filename `insight-validation.md` — purpose: exploratory analysis or investigation — title: Insight Validation
- `docs/analysis/planning-quality-validation.md` — filename `planning-quality-validation.md` — purpose: exploratory analysis or investigation — title: Planning Quality Validation
- `docs/analysis/planning-quality.md` — filename `planning-quality.md` — purpose: exploratory analysis or investigation — title: Planning Quality & Signal Integration Analysis
- `docs/analysis/product-aggregation-validation.md` — filename `product-aggregation-validation.md` — purpose: exploratory analysis or investigation — title: Product Aggregation Validation
- `docs/analysis/progress-model.md` — filename `progress-model.md` — purpose: exploratory analysis or investigation — title: Progress Model & Override Integration Analysis
- `docs/analysis/relic-audit/documentation-reorganization-report.md` — filename `documentation-reorganization-report.md` — purpose: structured audit or compliance verification — title: Documentation Reorganization Report
- `docs/analysis/relic-audit/repository-relic-audit.md` — filename `repository-relic-audit.md` — purpose: structured audit or compliance verification — title: Repository Relic Audit
- `docs/analysis/snapshot-comparison-validation.md` — filename `snapshot-comparison-validation.md` — purpose: exploratory analysis or investigation — title: Snapshot Comparison Validation
- `docs/analysis/snapshots.md` — filename `snapshots.md` — purpose: exploratory analysis or investigation — title: Snapshot & Budget Model Analysis
- `docs/analysis/state-classifications.md` — filename `state-classifications.md` — purpose: exploratory analysis or investigation — title: State Classifications & Refinement Gating Analysis
- `docs/analysis/ui-integration-validation.md` — filename `ui-integration-validation.md` — purpose: exploratory analysis or investigation — title: UI Integration Validation
- `docs/analysis/validation-rules.md` — filename `validation-rules.md` — purpose: normative rule or governance reference — title: Validation, Integrity, and Health Rules Analysis

### architecture
- `docs/architecture/build-quality-persistence-abstraction.md` — filename `build-quality-persistence-abstraction.md` — purpose: architecture or reference documentation — title: Build Quality Persistence Abstraction
- `docs/architecture/canonical-workitem-alignment.md` — filename `canonical-workitem-alignment.md` — purpose: architecture or reference documentation — title: Canonical Work Item Type Alignment
- `docs/architecture/canonical-workitem-test-fix.md` — filename `canonical-workitem-test-fix.md` — purpose: report or summarized findings — title: Canonical Work Item Test and Fixture Fix Report
- `docs/architecture/cdc-decision-record.md` — filename `cdc-decision-record.md` — purpose: plan or migration guidance — title: CDC Decision Record — Delivery Analytics & Planning Model
- `docs/architecture/cdc-domain-map-audit-fix.md` — filename `cdc-domain-map-audit-fix.md` — purpose: structured audit or compliance verification — title: CDC Domain Map Audit Fix Report
- `docs/architecture/cross-slice-validation.md` — filename `cross-slice-validation.md` — purpose: architecture or reference documentation — title: Cross-Slice Validation & Alignment — Build Quality vs Pipeline Insights
- `docs/architecture/documentation-governance.md` — filename `documentation-governance.md` — purpose: normative rule or governance reference — title: Documentation Governance
- `docs/architecture/failure-classification-normalization.md` — filename `failure-classification-normalization.md` — purpose: architecture or reference documentation — title: Failure Classification Normalization — Build Quality vs Pipeline Insights
- `docs/architecture/final-test-fixes.md` — filename `final-test-fixes.md` — purpose: report or summarized findings — title: Final Test Fixes Report
- `docs/architecture/incremental-sync-planner.md` — filename `incremental-sync-planner.md` — purpose: plan or migration guidance — title: Incremental Sync Planner Design
- `docs/architecture/persistence-abstraction-design.md` — filename `persistence-abstraction-design.md` — purpose: architecture or reference documentation — title: Persistence Abstraction Design for Analytical Query Side
- `docs/architecture/pipeline-identity-normalization.md` — filename `pipeline-identity-normalization.md` — purpose: architecture or reference documentation — title: Pipeline Identity Normalization — Build Quality vs Pipeline Insights
- `docs/architecture/pipeline-insights-persistence-abstraction.md` — filename `pipeline-insights-persistence-abstraction.md` — purpose: architecture or reference documentation — title: Pipeline Insights Persistence Abstraction Slice
- `docs/architecture/pipeline-time-semantics-migration.md` — filename `pipeline-time-semantics-migration.md` — purpose: plan or migration guidance — title: Pipeline Time Semantics Migration — Finish-Time Canonical Anchor
- `docs/architecture/portfolio-backlog-regression-investigation.md` — filename `portfolio-backlog-regression-investigation.md` — purpose: exploratory analysis or investigation — title: Portfolio & Backlog Regression Investigation
- `docs/architecture/portfolio_flow_data_signals.md` — filename `portfolio_flow_data_signals.md` — purpose: architecture or reference documentation — title: PortfolioFlow Data Signals
- `docs/architecture/post-fix-stability-verification.md` — filename `post-fix-stability-verification.md` — purpose: architecture or reference documentation — title: Post-Fix Stability Verification
- `docs/architecture/product-scope-validation-alignment.md` — filename `product-scope-validation-alignment.md` — purpose: architecture or reference documentation — title: Product Scope Validation Alignment — Build Quality vs Pipeline Insights
- `docs/architecture/pull-request-analytical-read-consolidation.md` — filename `pull-request-analytical-read-consolidation.md` — purpose: architecture or reference documentation — title: Pull Request Analytical Read Consolidation
- `docs/architecture/pull-request-analytical-read-validation-final.md` — filename `pull-request-analytical-read-validation-final.md` — purpose: architecture or reference documentation — title: Pull Request Analytical Read Validation — Final
- `docs/architecture/pull-request-persistence-abstraction-validation.md` — filename `pull-request-persistence-abstraction-validation.md` — purpose: architecture or reference documentation — title: Pull Request Persistence Abstraction Validation
- `docs/architecture/repository-identity-normalization.md` — filename `repository-identity-normalization.md` — purpose: architecture or reference documentation — title: Repository Identity Normalization — Build Quality vs Pipeline Insights
- `docs/architecture/repository-stability-audit.md` — filename `repository-stability-audit.md` — purpose: structured audit or compliance verification — title: Repository Stability Audit
- `docs/architecture/restore-build-determinism-audit.md` — filename `restore-build-determinism-audit.md` — purpose: structured audit or compliance verification — title: Restore & Build Determinism Audit
- `docs/architecture/restore-build-determinism-fix.md` — filename `restore-build-determinism-fix.md` — purpose: architecture or reference documentation — title: Restore & Build Determinism Fix
- `docs/architecture/scatter-ordering-regression.md` — filename `scatter-ordering-regression.md` — purpose: exploratory analysis or investigation — title: Scatter Ordering Regression Analysis
- `docs/architecture/test-failure-cluster-fix.md` — filename `test-failure-cluster-fix.md` — purpose: report or summarized findings — title: Remaining Test Failure Cluster Fix Report
- `docs/architecture/test-failure-isolation.md` — filename `test-failure-isolation.md` — purpose: architecture or reference documentation — title: Remaining Test Failure Isolation
- `docs/architecture/test-setup-corrections.md` — filename `test-setup-corrections.md` — purpose: architecture or reference documentation — title: Test Setup Corrections
- `docs/architecture/validation-system-report.md` — filename `validation-system-report.md` — purpose: report or summarized findings — title: Validation System Comprehensive Report
- `docs/architecture/workitem-query-boundary-phase1.md` — filename `workitem-query-boundary-phase1.md` — purpose: architecture or reference documentation — title: Work Item Query Boundary Phase 1
- `docs/architecture/workitem-query-boundary-phase2.md` — filename `workitem-query-boundary-phase2.md` — purpose: architecture or reference documentation — title: Work Item Query Boundary Phase 2
- `docs/architecture/workitem-query-boundary-phase3-goal-hierarchy.md` — filename `workitem-query-boundary-phase3-goal-hierarchy.md` — purpose: architecture or reference documentation — title: Work Item Query Boundary Phase 3 — Goal Hierarchy

### archive
- `docs/archive/code-quality/work-completed-2026-01-30.md` — filename `work-completed-2026-01-30.md` — purpose: structured audit or compliance verification — title: Work Completed - CODE_AUDIT_REPORT.md
- `docs/archive/legacy-revision-ingestion/cache-insights-and-validation-report.md` — filename `cache-insights-and-validation-report.md` — purpose: report or summarized findings — title: Cache Insights & Validation — Design Report
- `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md` — filename `odata-validator-vs-ingestion-report.md` — purpose: report or summarized findings — title: OData Validator vs Ingestion Report
- `docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md` — filename `real-revision-tfsclient-pagination-review.md` — purpose: general markdown documentation — title: RealRevisionTfsClient Pagination Review
- `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md` — filename `revision-ingestion-api-vs-validator-odata-divergence.md` — purpose: exploratory analysis or investigation — title: Revision ingestion divergence investigation: PoTool.Api vs TfsRetrievalValidator
- `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md` — filename `revision-ingestor-v2.md` — purpose: general markdown documentation — title: Revision Ingestor V2
- `docs/archive/legacy-revision-ingestion/sprint-trends-vs-revisions-report.md` — filename `sprint-trends-vs-revisions-report.md` — purpose: report or summarized findings — title: Sprint Trends vs Revision Database — Engineering Report

### audits
- `docs/audits/application_handler_cleanup.md` — filename `application_handler_cleanup.md` — purpose: structured audit or compliance verification — title: Application Handler Cleanup
- `docs/audits/application_semantic_audit.md` — filename `application_semantic_audit.md` — purpose: structured audit or compliance verification — title: Application Semantic Audit — Effort vs StoryPoints Usage
- `docs/audits/application_simplification_audit.md` — filename `application_simplification_audit.md` — purpose: structured audit or compliance verification — title: Application Simplification Audit
- `docs/audits/backlog_health_simplification.md` — filename `backlog_health_simplification.md` — purpose: structured audit or compliance verification — title: Backlog Health Simplification
- `docs/audits/backlog_quality_cdc_summary.md` — filename `backlog_quality_cdc_summary.md` — purpose: structured audit or compliance verification — title: Backlog Quality CDC Summary
- `docs/audits/backlog_quality_domain_exploration.md` — filename `backlog_quality_domain_exploration.md` — purpose: structured audit or compliance verification — title: Backlog Quality Domain Exploration
- `docs/audits/buildquality_application_page_integration_report.md` — filename `buildquality_application_page_integration_report.md` — purpose: structured audit or compliance verification — title: Prompt 3 — BuildQuality Application & Page Integration Report
- `docs/audits/buildquality_calculation_validation_report.md` — filename `buildquality_calculation_validation_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Calculation Validation Report
- `docs/audits/buildquality_cdc_contract_report.md` — filename `buildquality_cdc_contract_report.md` — purpose: normative rule or governance reference — title: Prompt 1 — BuildQuality CDC Contract Report
- `docs/audits/buildquality_chart_state_cleanup_report.md` — filename `buildquality_chart_state_cleanup_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Chart State Cleanup Report
- `docs/audits/buildquality_data_aggregation_contract_report.md` — filename `buildquality_data_aggregation_contract_report.md` — purpose: normative rule or governance reference — title: Prompt 2 — BuildQuality Data & Aggregation Contract Report
- `docs/audits/buildquality_discovery_report.md` — filename `buildquality_discovery_report.md` — purpose: structured audit or compliance verification — title: Prompt 0 — BuildQuality Discovery Report
- `docs/audits/buildquality_edge_consistency_report.md` — filename `buildquality_edge_consistency_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Edge Case & Cross-Workspace Consistency Report
- `docs/audits/buildquality_implementation_contract_report.md` — filename `buildquality_implementation_contract_report.md` — purpose: normative rule or governance reference — title: Prompt 4 — BuildQuality Data Foundation (Ingestion & Persistence Contract Report)
- `docs/audits/buildquality_missing_ingestion_build_168570_code_analysis_report.md` — filename `buildquality_missing_ingestion_build_168570_code_analysis_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Missing Ingestion For Build 168570 — Code Analysis Report
- `docs/audits/buildquality_retrieval_performance_report.md` — filename `buildquality_retrieval_performance_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Retrieval Performance Report
- `docs/audits/buildquality_seed_data_report.md` — filename `buildquality_seed_data_report.md` — purpose: structured audit or compliance verification — title: BuildQuality Seed Data Report
- `docs/audits/buildquality_ui_compliance_audit_report.md` — filename `buildquality_ui_compliance_audit_report.md` — purpose: structured audit or compliance verification — title: BuildQuality UI Compliance Audit Report
- `docs/audits/buildquality_ui_final_integration_report.md` — filename `buildquality_ui_final_integration_report.md` — purpose: structured audit or compliance verification — title: BuildQuality UI Final Integration Report
- `docs/audits/cdc-full-quality-audit.md` — filename `cdc-full-quality-audit.md` — purpose: structured audit or compliance verification — title: CDC Full Quality Audit
- `docs/audits/cdc_behavioral_stress_test_audit.md` — filename `cdc_behavioral_stress_test_audit.md` — purpose: structured audit or compliance verification — title: CDC Behavioral Stress-Test Audit
- `docs/audits/cdc_completion_summary.md` — filename `cdc_completion_summary.md` — purpose: structured audit or compliance verification — title: CDC Completion Summary
- `docs/audits/cdc_coverage_audit.md` — filename `cdc_coverage_audit.md` — purpose: structured audit or compliance verification — title: CDC Coverage Audit
- `docs/audits/cdc_extraction_summary.md` — filename `cdc_extraction_summary.md` — purpose: structured audit or compliance verification — title: CDC Extraction Summary
- `docs/audits/cdc_freeze_audit.md` — filename `cdc_freeze_audit.md` — purpose: structured audit or compliance verification — title: CDC Freeze Audit
- `docs/audits/cdc_invariant_tests.md` — filename `cdc_invariant_tests.md` — purpose: structured audit or compliance verification — title: CDC Invariant Tests
- `docs/audits/cdc_replay_fixture_validation.md` — filename `cdc_replay_fixture_validation.md` — purpose: structured audit or compliance verification — title: CDC Replay Fixture Validation
- `docs/audits/cdc_usage_coverage.md` — filename `cdc_usage_coverage.md` — purpose: structured audit or compliance verification — title: CDC Usage Coverage Audit
- `docs/audits/compatibility_cleanup_phase3.md` — filename `compatibility_cleanup_phase3.md` — purpose: structured audit or compliance verification — title: Compatibility Cleanup Phase 3
- `docs/audits/delivery_trend_analytics_cdc_summary.md` — filename `delivery_trend_analytics_cdc_summary.md` — purpose: structured audit or compliance verification — title: Delivery Trend Analytics CDC Summary
- `docs/audits/domain_library_readiness_audit.md` — filename `domain_library_readiness_audit.md` — purpose: structured audit or compliance verification — title: PoTool Domain Library Extraction Readiness Audit
- `docs/audits/domain_logic_outside_cdc_exploration.md` — filename `domain_logic_outside_cdc_exploration.md` — purpose: structured audit or compliance verification — title: Domain Logic Outside CDC Exploration
- `docs/audits/dto_contract_cleanup.md` — filename `dto_contract_cleanup.md` — purpose: normative rule or governance reference — title: DTO Contract Cleanup — Canonical Naming
- `docs/audits/effort_diagnostics_cdc_extraction_report.md` — filename `effort_diagnostics_cdc_extraction_report.md` — purpose: structured audit or compliance verification — title: Effort Diagnostics CDC Extraction Report
- `docs/audits/effort_diagnostics_cleanup_report.md` — filename `effort_diagnostics_cleanup_report.md` — purpose: structured audit or compliance verification — title: Effort Diagnostics Cleanup Report
- `docs/audits/effort_diagnostics_domain_exploration.md` — filename `effort_diagnostics_domain_exploration.md` — purpose: structured audit or compliance verification — title: Effort Diagnostics Domain Exploration
- `docs/audits/effort_diagnostics_semantic_audit.md` — filename `effort_diagnostics_semantic_audit.md` — purpose: structured audit or compliance verification — title: Effort Diagnostics Semantic Audit
- `docs/audits/effort_planning_boundary_audit.md` — filename `effort_planning_boundary_audit.md` — purpose: structured audit or compliance verification — title: Effort Planning Boundary Audit
- `docs/audits/effort_planning_boundary_cleanup.md` — filename `effort_planning_boundary_cleanup.md` — purpose: structured audit or compliance verification — title: EffortPlanning Boundary Cleanup
- `docs/audits/effort_planning_cdc_extraction.md` — filename `effort_planning_cdc_extraction.md` — purpose: structured audit or compliance verification — title: EffortPlanning CDC Extraction
- `docs/audits/estimation_audit.md` — filename `estimation_audit.md` — purpose: structured audit or compliance verification — title: PoTool Estimation Audit
- `docs/audits/final_pre_usage_validation.md` — filename `final_pre_usage_validation.md` — purpose: structured audit or compliance verification — title: Final Pre-Usage Validation
- `docs/audits/forecasting_cdc_summary.md` — filename `forecasting_cdc_summary.md` — purpose: structured audit or compliance verification — title: Forecasting CDC Summary
- `docs/audits/forecasting_domain_exploration.md` — filename `forecasting_domain_exploration.md` — purpose: structured audit or compliance verification — title: Forecasting Domain Exploration
- `docs/audits/forecasting_semantic_audit.md` — filename `forecasting_semantic_audit.md` — purpose: structured audit or compliance verification — title: Forecasting Semantic Audit
- `docs/audits/hexagon_boundary_enforcement.md` — filename `hexagon_boundary_enforcement.md` — purpose: structured audit or compliance verification — title: Hexagon Boundary Enforcement
- `docs/audits/hierarchy_propagation_audit.md` — filename `hierarchy_propagation_audit.md` — purpose: structured audit or compliance verification — title: PoTool Hierarchy and Propagation Domain Audit
- `docs/audits/metrics_audit.md` — filename `metrics_audit.md` — purpose: structured audit or compliance verification — title: PoTool Metrics Domain Audit
- `docs/audits/mock_data_quality.md` — filename `mock_data_quality.md` — purpose: structured audit or compliance verification — title: Mock Data Quality Audit
- `docs/audits/mock_pr_pipeline_seed_validation.md` — filename `mock_pr_pipeline_seed_validation.md` — purpose: structured audit or compliance verification — title: Mock PR/Pipeline Seed Validation
- `docs/audits/portfolio_flow_application_migration.md` — filename `portfolio_flow_application_migration.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Application Migration
- `docs/audits/portfolio_flow_consumers_audit.md` — filename `portfolio_flow_consumers_audit.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Projection Consumer Audit
- `docs/audits/portfolio_flow_domain_exploration.md` — filename `portfolio_flow_domain_exploration.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Domain Exploration
- `docs/audits/portfolio_flow_feasibility.md` — filename `portfolio_flow_feasibility.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Feasibility Audit
- `docs/audits/portfolio_flow_projection.md` — filename `portfolio_flow_projection.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Projection
- `docs/audits/portfolio_flow_projection_validation.md` — filename `portfolio_flow_projection_validation.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Projection Validation
- `docs/audits/portfolio_flow_semantic_audit.md` — filename `portfolio_flow_semantic_audit.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Semantic Audit
- `docs/audits/portfolio_flow_signal_enablement.md` — filename `portfolio_flow_signal_enablement.md` — purpose: structured audit or compliance verification — title: PortfolioFlow Signal Enablement
- `docs/audits/portfolio_handler_simplification.md` — filename `portfolio_handler_simplification.md` — purpose: structured audit or compliance verification — title: Portfolio Handler Simplification
- `docs/audits/post_runtime_fix_validation.md` — filename `post_runtime_fix_validation.md` — purpose: structured audit or compliance verification — title: Post Runtime Fix Validation
- `docs/audits/pr_pipeline_linkage_analysis.md` — filename `pr_pipeline_linkage_analysis.md` — purpose: structured audit or compliance verification — title: PR Pipeline Linkage Analysis
- `docs/audits/pre_cleanup_app_validation.md` — filename `pre_cleanup_app_validation.md` — purpose: structured audit or compliance verification — title: Pre-Cleanup App Validation
- `docs/audits/projection_determinism_audit.md` — filename `projection_determinism_audit.md` — purpose: structured audit or compliance verification — title: Projection Determinism Audit
- `docs/audits/projection_trend_pipeline_audit.md` — filename `projection_trend_pipeline_audit.md` — purpose: structured audit or compliance verification — title: PoTool Projection and Trend Pipeline Domain Audit
- `docs/audits/runtime_integrity_fix.md` — filename `runtime_integrity_fix.md` — purpose: structured audit or compliance verification — title: Runtime Integrity Fix (Dev Startup + TFS Access Boundary)
- `docs/audits/sprint_commitment_application_alignment.md` — filename `sprint_commitment_application_alignment.md` — purpose: structured audit or compliance verification — title: Sprint Commitment Application Alignment
- `docs/audits/sprint_commitment_cdc_extraction.md` — filename `sprint_commitment_cdc_extraction.md` — purpose: structured audit or compliance verification — title: Sprint Commitment CDC Extraction
- `docs/audits/sprint_commitment_handler_simplification.md` — filename `sprint_commitment_handler_simplification.md` — purpose: structured audit or compliance verification — title: Sprint Commitment Handler Simplification
- `docs/audits/sprint_scope_audit.md` — filename `sprint_scope_audit.md` — purpose: structured audit or compliance verification — title: PoTool Sprint Scope Domain Audit
- `docs/audits/sqlite_buildquality_database_discovery_report.md` — filename `sqlite_buildquality_database_discovery_report.md` — purpose: structured audit or compliance verification — title: SQLite BuildQuality Database Discovery Report
- `docs/audits/state_sprint_delivery_audit.md` — filename `state_sprint_delivery_audit.md` — purpose: structured audit or compliance verification — title: PoTool State + Sprint + Delivery Audit
- `docs/audits/statistical_core_cleanup_report.md` — filename `statistical_core_cleanup_report.md` — purpose: structured audit or compliance verification — title: Statistical Core Cleanup Report
- `docs/audits/statistical_helper_audit.md` — filename `statistical_helper_audit.md` — purpose: structured audit or compliance verification — title: Statistical Helper Audit
- `docs/audits/test_cleanup_step1.md` — filename `test_cleanup_step1.md` — purpose: structured audit or compliance verification — title: Test Cleanup Step 1
- `docs/audits/test_ownership_audit.md` — filename `test_ownership_audit.md` — purpose: structured audit or compliance verification — title: Test Ownership Audit
- `docs/audits/test_ownership_normalization.md` — filename `test_ownership_normalization.md` — purpose: structured audit or compliance verification — title: Test Ownership Normalization
- `docs/audits/tfs_api_version_configuration_inspection_report.md` — filename `tfs_api_version_configuration_inspection_report.md` — purpose: structured audit or compliance verification — title: TFS API Version Configuration Inspection Report
- `docs/audits/transport_naming_alignment.md` — filename `transport_naming_alignment.md` — purpose: structured audit or compliance verification — title: Transport Naming Alignment Audit
- `docs/audits/trend_delivery_analytics_exploration.md` — filename `trend_delivery_analytics_exploration.md` — purpose: structured audit or compliance verification — title: Trend / Delivery Analytics Exploration
- `docs/audits/ui_semantic_correction.md` — filename `ui_semantic_correction.md` — purpose: structured audit or compliance verification — title: UI Semantic Correction Audit
- `docs/audits/ui_storypoint_adoption.md` — filename `ui_storypoint_adoption.md` — purpose: structured audit or compliance verification — title: UI Story Point Adoption Audit
- `docs/audits/unit_test_cleanup_report.md` — filename `unit_test_cleanup_report.md` — purpose: structured audit or compliance verification — title: PoTool Unit Test Cleanup Report
- `docs/audits/unit_test_inventory_audit.md` — filename `unit_test_inventory_audit.md` — purpose: structured audit or compliance verification — title: PoTool Unit Test Inventory Audit
- `docs/audits/unit_test_redundancy_audit.md` — filename `unit_test_redundancy_audit.md` — purpose: structured audit or compliance verification — title: PoTool Unit Test Redundancy Audit
- `docs/audits/unit_test_speed_audit.md` — filename `unit_test_speed_audit.md` — purpose: structured audit or compliance verification — title: PoTool Unit Test Speed Audit
- `docs/audits/unit_test_strategy.md` — filename `unit_test_strategy.md` — purpose: structured audit or compliance verification — title: PoTool Unit Test Strategy
- `docs/audits/workspace_hub_tile_analysis.md` — filename `workspace_hub_tile_analysis.md` — purpose: structured audit or compliance verification — title: Workspace Hub Tile Analysis

### history
- `docs/history/code-quality/code-audit-report-2026-01-30.md` — filename `code-audit-report-2026-01-30.md` — purpose: structured audit or compliance verification — title: Code Quality & Architecture Audit Report
- `docs/history/code-quality/final-summary-2026-01-30.md` — filename `final-summary-2026-01-30.md` — purpose: structured audit or compliance verification — title: Final Summary - CODE_AUDIT_REPORT.md Complete Analysis
- `docs/history/code-quality/fixes-applied-2026-01-30.md` — filename `fixes-applied-2026-01-30.md` — purpose: structured audit or compliance verification — title: Fixes Applied - CODE_AUDIT_REPORT.md
- `docs/history/code-quality/non-test-issues-analysis-2026-01-30.md` — filename `non-test-issues-analysis-2026-01-30.md` — purpose: structured audit or compliance verification — title: Non-Test Issues Analysis - CODE_AUDIT_REPORT.md
- `docs/history/validation/validators-implementation-2026-01-30.md` — filename `validators-implementation-2026-01-30.md` — purpose: report or summarized findings — title: Validators Implementation Summary

### other
- `.github/copilot-instructions.md` — filename `copilot-instructions.md` — purpose: normative rule or governance reference — title: Copilot Instructions — PO Companion (Authoritative)
- `.github/pull_request_template.md` — filename `pull_request_template.md` — purpose: general markdown documentation — title: Pull Request Template — PO Companion
- `.github/workflows/README.md` — filename `README.md` — purpose: folder index or entry-point documentation — title: GitHub Actions Workflows
- `docs/ARCHITECTURE_RULES.md` — filename `ARCHITECTURE_RULES.md` — purpose: normative rule or governance reference — title: No H1 title
- `docs/COPILOT_ARCHITECTURE_CONTRACT.md` — filename `COPILOT_ARCHITECTURE_CONTRACT.md` — purpose: normative rule or governance reference — title: Copilot Architecture Contract — PO Companion
- `docs/EF_RULES.md` — filename `EF_RULES.md` — purpose: normative rule or governance reference — title: EF Core Concurrency Rules (Non-Negotiable)
- `docs/Fluent_UI_compat_rules.md` — filename `Fluent_UI_compat_rules.md` — purpose: normative rule or governance reference — title: UI Density Rules — Fluent UI Compact Aligned
- `docs/LIVE_TFS_CALLS_ANALYSIS.md` — filename `LIVE_TFS_CALLS_ANALYSIS.md` — purpose: exploratory analysis or investigation — title: Investigation Report: Unexpected Live TFS Calls in Workspace Navigation
- `docs/MULTI_SELECT_BEHAVIOR.md` — filename `MULTI_SELECT_BEHAVIOR.md` — purpose: general markdown documentation — title: Multi-Select Dropdown Behavior
- `docs/NAVIGATION_MAP.md` — filename `NAVIGATION_MAP.md` — purpose: general markdown documentation — title: PO Companion — Navigation Map
- `docs/PROCESS_RULES.md` — filename `PROCESS_RULES.md` — purpose: normative rule or governance reference — title: Process Rules — PO Companion
- `docs/README.md` — filename `README.md` — purpose: folder index or entry-point documentation — title: No H1 title
- `docs/REALTFSCLIENT_GETALL_ANALYSIS.md` — filename `REALTFSCLIENT_GETALL_ANALYSIS.md` — purpose: exploratory analysis or investigation — title: RealTfsClient 'GetAll' Methods Analysis Report
- `docs/TFS_CACHE_IMPLEMENTATION_PLAN.md` — filename `TFS_CACHE_IMPLEMENTATION_PLAN.md` — purpose: plan or migration guidance — title: ProductOwner-Scoped Incremental TFS Cache — Implementation Plan
- `docs/TFS_INTEGRATION_RULES.md` — filename `TFS_INTEGRATION_RULES.md` — purpose: normative rule or governance reference — title: TFS Integration Rules
- `docs/UI_LOADING_RULES.md` — filename `UI_LOADING_RULES.md` — purpose: normative rule or governance reference — title: No H1 title
- `docs/UI_MIGRATION_PLAN.md` — filename `UI_MIGRATION_PLAN.md` — purpose: plan or migration guidance — title: UI Migration Plan — PO Companion
- `docs/UI_RULES.md` — filename `UI_RULES.md` — purpose: normative rule or governance reference — title: UI & UX Rules — PO Companion (Blazor WebAssembly)
- `docs/bug_trend_followups.md` — filename `bug_trend_followups.md` — purpose: general markdown documentation — title: Bug Trend Follow-up Actions
- `docs/cleanup/obsolete-changes-log.md` — filename `obsolete-changes-log.md` — purpose: general markdown documentation — title: Obsolete Changes Log — Dead Code Cleanup
- `docs/cleanup/phase1-client-reachability-report.md` — filename `phase1-client-reachability-report.md` — purpose: report or summarized findings — title: Phase 1 — Client-Side UI Reachability Report
- `docs/cleanup/phase2-endpoint-usage-report.md` — filename `phase2-endpoint-usage-report.md` — purpose: report or summarized findings — title: Phase 2 — Endpoint Usage Mapping Report
- `docs/cleanup/phase3-handler-usage-report.md` — filename `phase3-handler-usage-report.md` — purpose: report or summarized findings — title: Phase 3 — Handler Usage Report
- `docs/cleanup/phase4-full-layer-summary.md` — filename `phase4-full-layer-summary.md` — purpose: report or summarized findings — title: PoCompanion Solution Architecture Map — Phase 4 Summary
- `docs/domain/REPOSITORY_DOMAIN_DISCOVERY.md` — filename `REPOSITORY_DOMAIN_DISCOVERY.md` — purpose: exploratory analysis or investigation — title: Repository Domain Discovery
- `docs/domain/backlog_quality_domain_model.md` — filename `backlog_quality_domain_model.md` — purpose: architecture or reference documentation — title: Backlog Quality Domain Model
- `docs/domain/cdc_domain_map.md` — filename `cdc_domain_map.md` — purpose: architecture or reference documentation — title: CDC Domain Map
- `docs/domain/cdc_domain_map_generated.md` — filename `cdc_domain_map_generated.md` — purpose: architecture or reference documentation — title: CDC Domain Map — Generated
- `docs/domain/cdc_reference.md` — filename `cdc_reference.md` — purpose: architecture or reference documentation — title: Canonical Domain Core Reference
- `docs/domain/domain_model.md` — filename `domain_model.md` — purpose: architecture or reference documentation — title: PoTool Domain Model
- `docs/domain/effort_diagnostics_domain_model.md` — filename `effort_diagnostics_domain_model.md` — purpose: architecture or reference documentation — title: EffortDiagnostics Domain Model
- `docs/domain/forecasting_domain_model.md` — filename `forecasting_domain_model.md` — purpose: architecture or reference documentation — title: Forecasting Domain Model
- `docs/domain/portfolio_flow_model.md` — filename `portfolio_flow_model.md` — purpose: architecture or reference documentation — title: PortfolioFlow Canonical Model
- `docs/domain/rules/estimation_rules.md` — filename `estimation_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Estimation
- `docs/domain/rules/hierarchy_rules.md` — filename `hierarchy_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Hierarchy
- `docs/domain/rules/metrics_rules.md` — filename `metrics_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Metrics
- `docs/domain/rules/propagation_rules.md` — filename `propagation_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Propagation
- `docs/domain/rules/source_rules.md` — filename `source_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Data Sources
- `docs/domain/rules/sprint_rules.md` — filename `sprint_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — Sprint Semantics
- `docs/domain/rules/state_rules.md` — filename `state_rules.md` — purpose: normative rule or governance reference — title: Domain Rules — State Classification
- `docs/domain/sprint_commitment_cdc_summary.md` — filename `sprint_commitment_cdc_summary.md` — purpose: report or summarized findings — title: Sprint Commitment CDC Summary
- `docs/domain/sprint_commitment_domain_model.md` — filename `sprint_commitment_domain_model.md` — purpose: architecture or reference documentation — title: Sprint Commitment Domain Model
- `docs/domain/ui_semantic_rules.md` — filename `ui_semantic_rules.md` — purpose: normative rule or governance reference — title: UI Semantic Rules
- `docs/exploration/sprint_commitment_domain_exploration.md` — filename `sprint_commitment_domain_exploration.md` — purpose: exploratory analysis or investigation — title: Sprint Commitment Domain Exploration
- `docs/filters/cache-only-guardrail-analysis-pipeline-workitems.md` — filename `cache-only-guardrail-analysis-pipeline-workitems.md` — purpose: exploratory analysis or investigation — title: Cache-Only Guardrail Analysis — Pipeline and Work Item Read Paths
- `docs/filters/canonical_filter_state_model.md` — filename `canonical_filter_state_model.md` — purpose: general markdown documentation — title: Canonical Filter State Model
- `docs/filters/datasource-enforcement.md` — filename `datasource-enforcement.md` — purpose: general markdown documentation — title: DataSourceMode Enforcement
- `docs/filters/filter-analysis-improved.md` — filename `filter-analysis-improved.md` — purpose: exploratory analysis or investigation — title: PoTool Global Filtering — Decision-Grade Analysis
- `docs/filters/filter-analysis.md` — filename `filter-analysis.md` — purpose: exploratory analysis or investigation — title: Filter analysis
- `docs/filters/filter-canonical-model.md` — filename `filter-canonical-model.md` — purpose: general markdown documentation — title: Canonical Filter Model
- `docs/filters/filter-cross-slice-migration.md` — filename `filter-cross-slice-migration.md` — purpose: plan or migration guidance — title: Cross-Slice Canonical Filter Migration
- `docs/filters/filter-current-state-analysis.md` — filename `filter-current-state-analysis.md` — purpose: exploratory analysis or investigation — title: Current Filter State Analysis
- `docs/filters/filter-delivery-migration.md` — filename `filter-delivery-migration.md` — purpose: plan or migration guidance — title: Delivery Slice Canonical Filter Migration
- `docs/filters/filter-final-cleanup-report.md` — filename `filter-final-cleanup-report.md` — purpose: report or summarized findings — title: Filter Final Cleanup Report
- `docs/filters/filter-implementation-design.md` — filename `filter-implementation-design.md` — purpose: general markdown documentation — title: Canonical Filter Implementation Design
- `docs/filters/filter-implementation-execution-plan.md` — filename `filter-implementation-execution-plan.md` — purpose: plan or migration guidance — title: Canonical Filter Implementation Execution Plan
- `docs/filters/filter-implementation-plan.md` — filename `filter-implementation-plan.md` — purpose: plan or migration guidance — title: Filter Implementation Plan
- `docs/filters/filter-performance-audit.md` — filename `filter-performance-audit.md` — purpose: structured audit or compliance verification — title: Filter Performance Audit and Optimization Plan
- `docs/filters/filter-performance-verification.md` — filename `filter-performance-verification.md` — purpose: general markdown documentation — title: Filter Performance Verification
- `docs/filters/filter-phases-1-4-pr-breakdown.md` — filename `filter-phases-1-4-pr-breakdown.md` — purpose: general markdown documentation — title: Canonical Filter Phases 1–4 PR Breakdown
- `docs/filters/filter-pipeline-migration.md` — filename `filter-pipeline-migration.md` — purpose: plan or migration guidance — title: Pipeline Slice Canonical Filter Migration
- `docs/filters/filter-pipeline-truncation-fix.md` — filename `filter-pipeline-truncation-fix.md` — purpose: general markdown documentation — title: Pipeline Run Truncation Corrective Fix
- `docs/filters/filter-pr-migration.md` — filename `filter-pr-migration.md` — purpose: plan or migration guidance — title: PR Slice Canonical Filter Migration
- `docs/filters/filter-sprint-migration.md` — filename `filter-sprint-migration.md` — purpose: plan or migration guidance — title: Sprint Slice Canonical Filter Migration
- `docs/filters/filter-ui-behavior.md` — filename `filter-ui-behavior.md` — purpose: general markdown documentation — title: Filter UI Behavior
- `docs/filters/filter-ui-metadata-fix.md` — filename `filter-ui-metadata-fix.md` — purpose: general markdown documentation — title: UI/Client Canonical Filter Metadata Fix
- `docs/filters/filter-validation-report.md` — filename `filter-validation-report.md` — purpose: structured audit or compliance verification — title: Filter Integration Validation & Regression Audit
- `docs/filters/page-filter-contracts.md` — filename `page-filter-contracts.md` — purpose: normative rule or governance reference — title: Page Filter Contracts
- `docs/filters/pipeline-guardrail-and-workitem-split.md` — filename `pipeline-guardrail-and-workitem-split.md` — purpose: plan or migration guidance — title: Pipeline Guardrail Implementation Plan and Work Item API Split Design
- `docs/filters/pipeline-provider-cleanup.md` — filename `pipeline-provider-cleanup.md` — purpose: general markdown documentation — title: Pipeline Provider Cleanup
- `docs/filters/pr-batching-verification.md` — filename `pr-batching-verification.md` — purpose: general markdown documentation — title: PR Batching Verification
- `docs/filters/pr-cache-only-guardrails.md` — filename `pr-cache-only-guardrails.md` — purpose: general markdown documentation — title: PR Cache-Only Guardrails
- `docs/filters/pr-live-provider-usage-audit.md` — filename `pr-live-provider-usage-audit.md` — purpose: structured audit or compliance verification — title: PR Live Provider Usage Audit
- `docs/filters/pr-provider-cleanup.md` — filename `pr-provider-cleanup.md` — purpose: general markdown documentation — title: PR Provider Cleanup
- `docs/filters/tfs-access-boundary-sealed.md` — filename `tfs-access-boundary-sealed.md` — purpose: general markdown documentation — title: TFS Access Boundary Sealed
- `docs/filters/tfs-access-boundary-verification.md` — filename `tfs-access-boundary-verification.md` — purpose: general markdown documentation — title: TFS Access Boundary Verification
- `docs/filters/workitem-route-classification-fix.md` — filename `workitem-route-classification-fix.md` — purpose: general markdown documentation — title: Work Item Route Classification Fix
- `docs/health_additional_signals.md` — filename `health_additional_signals.md` — purpose: general markdown documentation — title: Additional Health Signals Proposal
- `docs/health_workspace_fix_plan.md` — filename `health_workspace_fix_plan.md` — purpose: plan or migration guidance — title: Health Workspace Fix Plan
- `docs/implementation/battleship-cdc-extension-report.md` — filename `battleship-cdc-extension-report.md` — purpose: report or summarized findings — title: Battleship CDC Extension Report
- `docs/implementation/cdc-critical-fixes.md` — filename `cdc-critical-fixes.md` — purpose: general markdown documentation — title: CDC Critical Fixes
- `docs/implementation/cdc-fallback-timestamp-hardening.md` — filename `cdc-fallback-timestamp-hardening.md` — purpose: report or summarized findings — title: CDC Fallback Timestamp Hardening Report
- `docs/implementation/cdc-fix-report-empty-snapshot-snapshotcount.md` — filename `cdc-fix-report-empty-snapshot-snapshotcount.md` — purpose: report or summarized findings — title: CDC Fix Report — Empty Snapshot & SnapshotCount
- `docs/implementation/phase-a-corrections.md` — filename `phase-a-corrections.md` — purpose: general markdown documentation — title: Phase A Corrections
- `docs/implementation/phase-a-foundation.md` — filename `phase-a-foundation.md` — purpose: general markdown documentation — title: Phase A Foundation Implementation
- `docs/implementation/phase-b-corrections.md` — filename `phase-b-corrections.md` — purpose: general markdown documentation — title: Phase B Corrections
- `docs/implementation/phase-b-feature-progress.md` — filename `phase-b-feature-progress.md` — purpose: general markdown documentation — title: Phase B Feature Progress & Override Aggregation
- `docs/implementation/phase-c-epic-progress.md` — filename `phase-c-epic-progress.md` — purpose: general markdown documentation — title: Phase C Epic Progress Aggregation
- `docs/implementation/phase-e-corrections.md` — filename `phase-e-corrections.md` — purpose: general markdown documentation — title: Phase E Corrections
- `docs/implementation/phase-e-snapshots.md` — filename `phase-e-snapshots.md` — purpose: general markdown documentation — title: Phase E Snapshot Model & Comparison Engine
- `docs/implementation/phase-f-lifecycle.md` — filename `phase-f-lifecycle.md` — purpose: general markdown documentation — title: Phase F Snapshot Lifecycle & Capture Strategy
- `docs/implementation/phase-g-consumption.md` — filename `phase-g-consumption.md` — purpose: general markdown documentation — title: Phase G Consumption Validation
- `docs/implementation/phase-h-persistence.md` — filename `phase-h-persistence.md` — purpose: general markdown documentation — title: Phase H Snapshot Persistence & Selection Policy
- `docs/implementation/phase-i-finalization.md` — filename `phase-i-finalization.md` — purpose: general markdown documentation — title: Phase I Finalization Validation
- `docs/iteration_path_sorting_audit.md` — filename `iteration_path_sorting_audit.md` — purpose: structured audit or compliance verification — title: Iteration Path Sorting Audit
- `docs/navigation_decision_backlog.md` — filename `navigation_decision_backlog.md` — purpose: general markdown documentation — title: Navigation Decision Backlog
- `docs/navigation_followup_actions.md` — filename `navigation_followup_actions.md` — purpose: general markdown documentation — title: Navigation Follow-up Actions
- `docs/pr_template.md` — filename `pr_template.md` — purpose: general markdown documentation — title: Pull Request Template — PO Companion
- `docs/roadmaps/application_simplification_plan.md` — filename `application_simplification_plan.md` — purpose: plan or migration guidance — title: Application Simplification Plan
- `docs/screenshots/README.md` — filename `README.md` — purpose: folder index or entry-point documentation — title: Screenshot Index — PoCompanion Exploratory Testing
- `docs/sprint-scoping-limitations.md` — filename `sprint-scoping-limitations.md` — purpose: general markdown documentation — title: Sprint Scoping — Limitations and Current Implementation
- `docs/sprintmetrics_iteration_migration_plan.md` — filename `sprintmetrics_iteration_migration_plan.md` — purpose: plan or migration guidance — title: Sprint Metrics Iteration Migration Plan
- `docs/sqlite-datetime-fix.md` — filename `sqlite-datetime-fix.md` — purpose: general markdown documentation — title: SQLite DateTime translation fix
- `docs/sqlite-timestamp-fix-audit.md` — filename `sqlite-timestamp-fix-audit.md` — purpose: structured audit or compliance verification — title: SQLite timestamp translation fix audit
- `docs/test-determinism-report.md` — filename `test-determinism-report.md` — purpose: report or summarized findings — title: Deterministic Test Suite Report
- `features/02032026_backlog_health.md` — filename `02032026_backlog_health.md` — purpose: user-facing or feature-planning documentation — title: Backlog State Model Specification
- `features/20260110_User_profile_creation.md` — filename `20260110_User_profile_creation.md` — purpose: user-facing or feature-planning documentation — title: Feature: Product Owner, Products, Teams, and Product Backlogs
- `features/20260119_workitem_validation.md` — filename `20260119_workitem_validation.md` — purpose: user-facing or feature-planning documentation — title: Feature: Hierarchical Work Item Validation with Explicit Consequences
- `features/20260126_epic_planning_v2.md` — filename `20260126_epic_planning_v2.md` — purpose: plan or migration guidance — title: GitHub Copilot Prompt — Implement New Planning Board (Table-Based, Products as Columns)
- `features/Dependency_graph.md` — filename `Dependency_graph.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `features/Pipeline_insights.md` — filename `Pipeline_insights.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `features/Simple_workitem_explorer.md` — filename `Simple_workitem_explorer.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `features/User_landing_v2.md` — filename `User_landing_v2.md` — purpose: user-facing or feature-planning documentation — title: Feature: Startup UX Refactor (Phase 1–3) — Hard gating, Profiles Home, Profile Pictures, Guided Profile Creation, Transparent Sync Progress
- `features/VERIFY_TFS_API_INTEGRATION.md` — filename `VERIFY_TFS_API_INTEGRATION.md` — purpose: user-facing or feature-planning documentation — title: GitHub Copilot Feature Prompt
- `features/Verify_TFS_API.md` — filename `Verify_TFS_API.md` — purpose: user-facing or feature-planning documentation — title: Feature: Verify TFS API (Functional Compatibility & Safety Check)
- `features/effort_distribution_analytics.md` — filename `effort_distribution_analytics.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `features/epic_planning.md` — filename `epic_planning.md` — purpose: plan or migration guidance — title: Feature: Release Planning Board (Agent-Ready Specification)
- `features/planning_board_decommission.md` — filename `planning_board_decommission.md` — purpose: plan or migration guidance — title: Planning Board Decommission Checklist
- `features/plans/20260110_User_profile_creation_plan.md` — filename `20260110_User_profile_creation_plan.md` — purpose: plan or migration guidance — title: Implementation Plan (Copilot-executable)
- `features/pr_insight.md` — filename `pr_insight.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `features/state_timeline.md` — filename `state_timeline.md` — purpose: user-facing or feature-planning documentation — title: No H1 title
- `prompts/CONTEXT_PACK.MD` — filename `CONTEXT_PACK.MD` — purpose: general markdown documentation — title: 0) Repo Snapshot

### reports
- `docs/reports/ingestion-observability-hardening.md` — filename `ingestion-observability-hardening.md` — purpose: report or summarized findings — title: Ingestion observability hardening
- `docs/reports/odata-ingestion-fix-plan.md` — filename `odata-ingestion-fix-plan.md` — purpose: report or summarized findings — title: OData Ingestion Fix Plan
- `docs/reports/sprint-attribution-analysis.md` — filename `sprint-attribution-analysis.md` — purpose: exploratory analysis or investigation — title: Sprint Attribution Strategy Analysis
- `docs/reports/sprint-trends-current-state-analysis.md` — filename `sprint-trends-current-state-analysis.md` — purpose: exploratory analysis or investigation — title: Sprint Trends — Current State Analysis

### reviews
- `docs/reviews/TfsIntegrationReview.md` — filename `TfsIntegrationReview.md` — purpose: general markdown documentation — title: TFS Integration Review — Fix-Ready, Risk-Ranked Findings
- `docs/reviews/swepo-review-report.md` — filename `swepo-review-report.md` — purpose: report or summarized findings — title: Senior SWE/PO Review Report – main branch

### root
- `README.md` — filename `README.md` — purpose: repository entry point — title: potool

### user
- `docs/user/gebruikershandleiding.md` — filename `gebruikershandleiding.md` — purpose: user-facing or feature-planning documentation — title: PO Companion — Gebruikershandleiding
