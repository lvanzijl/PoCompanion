# Documentation Migration — Batch 1

## 1. Summary

- files moved: 12
- files normalized: 12
- violations resolved: docs root reduced by 12 clearly safe files; repository root already compliant except README.md allowance
- violations remaining: 14 docs-root markdown files still need later handling; 144 non-compliant markdown filenames still exist repository-wide

Batch 1 applied only the safe subset requested. Existing files were not date-prefixed; the `YYYY-MM-DD-` convention is enforced only for new task-generated reports in this batch, including this report.

## 2. Files moved

| From | To |
|---|---|
| `docs/LIVE_TFS_CALLS_ANALYSIS.md` | `docs/analysis/live-tfs-calls-analysis.md` |
| `docs/REALTFSCLIENT_GETALL_ANALYSIS.md` | `docs/analysis/realtfsclient-getall-analysis.md` |
| `docs/TFS_CACHE_IMPLEMENTATION_PLAN.md` | `docs/implementation/tfs-cache-implementation-plan.md` |
| `docs/UI_MIGRATION_PLAN.md` | `docs/implementation/ui-migration-plan.md` |
| `docs/MULTI_SELECT_BEHAVIOR.md` | `docs/architecture/multi-select-behavior.md` |
| `docs/NAVIGATION_MAP.md` | `docs/architecture/navigation-map.md` |
| `docs/health-workspace-fix-plan.md` | `docs/implementation/health-workspace-fix-plan.md` |
| `docs/iteration-path-sorting-audit.md` | `docs/analysis/iteration-path-sorting-audit.md` |
| `docs/sprintmetrics-iteration-migration-plan.md` | `docs/implementation/sprintmetrics-iteration-migration-plan.md` |
| `docs/sqlite-datetime-fix.md` | `docs/reports/2026-03-30-sqlite-datetime-fix.md` |
| `docs/sqlite-timestamp-fix-audit.md` | `docs/analysis/sqlite-timestamp-fix-audit.md` |
| `docs/test-determinism-report.md` | `docs/reports/2026-03-30-test-determinism-report.md` |

## 3. Naming normalization

The following moved files were normalized to lowercase kebab-case as part of the move:

- `docs/analysis/live-tfs-calls-analysis.md`
- `docs/analysis/realtfsclient-getall-analysis.md`
- `docs/implementation/tfs-cache-implementation-plan.md`
- `docs/implementation/ui-migration-plan.md`
- `docs/architecture/multi-select-behavior.md`
- `docs/architecture/navigation-map.md`
- `docs/implementation/health-workspace-fix-plan.md`
- `docs/analysis/iteration-path-sorting-audit.md`
- `docs/implementation/sprintmetrics-iteration-migration-plan.md`
- `docs/reports/2026-03-30-sqlite-datetime-fix.md`
- `docs/analysis/sqlite-timestamp-fix-audit.md`
- `docs/reports/2026-03-30-test-determinism-report.md`

## 4. Filename convention violations (proposals only)

Per the binding clarification, the date-prefixed `YYYY-MM-DD-...` rule applies only to new task-generated reports. The proposals below therefore normalize existing filenames to lowercase kebab-case only; no existing files were renamed in this section.

| Current path | Proposed filename |
|---|---|
| `.github/pull_request_template.md` | `pull-request-template.md` |
| `.github/workflows/README.md` | `readme.md` |
| `README.md` | `readme.md` |
| `docs/rules/architecture-rules.md` | `architecture-rules.md` |
| `docs/rules/copilot-architecture-contract.md` | `copilot-architecture-contract.md` |
| `docs/rules/ef-rules.md` | `ef-rules.md` |
| `docs/rules/fluent-ui-compat-rules.md` | `fluent-ui-compat-rules.md` |
| `docs/rules/process-rules.md` | `process-rules.md` |
| `docs/README.md` | `readme.md` |
| `docs/rules/tfs-integration-rules.md` | `tfs-integration-rules.md` |
| `docs/rules/ui-loading-rules.md` | `ui-loading-rules.md` |
| `docs/rules/ui-rules.md` | `ui-rules.md` |
| `docs/architecture/portfolio-flow-data-signals.md` | `portfolio-flow-data-signals.md` |
| `docs/analysis/application-handler-cleanup.md` | `application-handler-cleanup.md` |
| `docs/analysis/application-semantic-audit.md` | `application-semantic-audit.md` |
| `docs/analysis/application-simplification-audit.md` | `application-simplification-audit.md` |
| `docs/analysis/backlog-health-simplification.md` | `backlog-health-simplification.md` |
| `docs/analysis/backlog-quality-cdc-summary.md` | `backlog-quality-cdc-summary.md` |
| `docs/analysis/backlog-quality-domain-exploration.md` | `backlog-quality-domain-exploration.md` |
| `docs/analysis/buildquality-application-page-integration-report.md` | `buildquality-application-page-integration-report.md` |
| `docs/analysis/buildquality-calculation-validation-report.md` | `buildquality-calculation-validation-report.md` |
| `docs/analysis/buildquality-cdc-contract-report.md` | `buildquality-cdc-contract-report.md` |
| `docs/analysis/buildquality-chart-state-cleanup-report.md` | `buildquality-chart-state-cleanup-report.md` |
| `docs/analysis/buildquality-data-aggregation-contract-report.md` | `buildquality-data-aggregation-contract-report.md` |
| `docs/analysis/buildquality-discovery-report.md` | `buildquality-discovery-report.md` |
| `docs/analysis/buildquality-edge-consistency-report.md` | `buildquality-edge-consistency-report.md` |
| `docs/analysis/buildquality-implementation-contract-report.md` | `buildquality-implementation-contract-report.md` |
| `docs/analysis/buildquality-missing-ingestion-build-168570-code-analysis-report.md` | `buildquality-missing-ingestion-build-168570-code-analysis-report.md` |
| `docs/analysis/buildquality-retrieval-performance-report.md` | `buildquality-retrieval-performance-report.md` |
| `docs/analysis/buildquality-seed-data-report.md` | `buildquality-seed-data-report.md` |
| `docs/analysis/buildquality-ui-compliance-audit-report.md` | `buildquality-ui-compliance-audit-report.md` |
| `docs/analysis/buildquality-ui-final-integration-report.md` | `buildquality-ui-final-integration-report.md` |
| `docs/analysis/cdc-behavioral-stress-test-audit.md` | `cdc-behavioral-stress-test-audit.md` |
| `docs/analysis/cdc-completion-summary.md` | `cdc-completion-summary.md` |
| `docs/analysis/cdc-coverage-audit.md` | `cdc-coverage-audit.md` |
| `docs/analysis/cdc-extraction-summary.md` | `cdc-extraction-summary.md` |
| `docs/analysis/cdc-freeze-audit.md` | `cdc-freeze-audit.md` |
| `docs/analysis/cdc-invariant-tests.md` | `cdc-invariant-tests.md` |
| `docs/analysis/cdc-replay-fixture-validation.md` | `cdc-replay-fixture-validation.md` |
| `docs/analysis/cdc-usage-coverage.md` | `cdc-usage-coverage.md` |
| `docs/analysis/compatibility-cleanup-phase3.md` | `compatibility-cleanup-phase3.md` |
| `docs/analysis/delivery-trend-analytics-cdc-summary.md` | `delivery-trend-analytics-cdc-summary.md` |
| `docs/analysis/domain-library-readiness-audit.md` | `domain-library-readiness-audit.md` |
| `docs/analysis/domain-logic-outside-cdc-exploration.md` | `domain-logic-outside-cdc-exploration.md` |
| `docs/analysis/dto-contract-cleanup.md` | `dto-contract-cleanup.md` |
| `docs/analysis/effort-diagnostics-cdc-extraction-report.md` | `effort-diagnostics-cdc-extraction-report.md` |
| `docs/analysis/effort-diagnostics-cleanup-report.md` | `effort-diagnostics-cleanup-report.md` |
| `docs/analysis/effort-diagnostics-domain-exploration.md` | `effort-diagnostics-domain-exploration.md` |
| `docs/analysis/effort-diagnostics-semantic-audit.md` | `effort-diagnostics-semantic-audit.md` |
| `docs/analysis/effort-planning-boundary-audit.md` | `effort-planning-boundary-audit.md` |
| `docs/analysis/effort-planning-boundary-cleanup.md` | `effort-planning-boundary-cleanup.md` |
| `docs/analysis/effort-planning-cdc-extraction.md` | `effort-planning-cdc-extraction.md` |
| `docs/analysis/estimation-audit.md` | `estimation-audit.md` |
| `docs/analysis/final-pre-usage-validation.md` | `final-pre-usage-validation.md` |
| `docs/analysis/forecasting-cdc-summary.md` | `forecasting-cdc-summary.md` |
| `docs/analysis/forecasting-domain-exploration.md` | `forecasting-domain-exploration.md` |
| `docs/analysis/forecasting-semantic-audit.md` | `forecasting-semantic-audit.md` |
| `docs/analysis/hexagon-boundary-enforcement.md` | `hexagon-boundary-enforcement.md` |
| `docs/analysis/hierarchy-propagation-audit.md` | `hierarchy-propagation-audit.md` |
| `docs/analysis/metrics-audit.md` | `metrics-audit.md` |
| `docs/analysis/mock-data-quality.md` | `mock-data-quality.md` |
| `docs/analysis/mock-pr-pipeline-seed-validation.md` | `mock-pr-pipeline-seed-validation.md` |
| `docs/analysis/portfolio-flow-application-migration.md` | `portfolio-flow-application-migration.md` |
| `docs/analysis/portfolio-flow-consumers-audit.md` | `portfolio-flow-consumers-audit.md` |
| `docs/analysis/portfolio-flow-domain-exploration.md` | `portfolio-flow-domain-exploration.md` |
| `docs/analysis/portfolio-flow-feasibility.md` | `portfolio-flow-feasibility.md` |
| `docs/analysis/portfolio-flow-projection.md` | `portfolio-flow-projection.md` |
| `docs/analysis/portfolio-flow-projection-validation.md` | `portfolio-flow-projection-validation.md` |
| `docs/analysis/portfolio-flow-semantic-audit.md` | `portfolio-flow-semantic-audit.md` |
| `docs/analysis/portfolio-flow-signal-enablement.md` | `portfolio-flow-signal-enablement.md` |
| `docs/analysis/portfolio-handler-simplification.md` | `portfolio-handler-simplification.md` |
| `docs/analysis/post-runtime-fix-validation.md` | `post-runtime-fix-validation.md` |
| `docs/analysis/pr-pipeline-linkage-analysis.md` | `pr-pipeline-linkage-analysis.md` |
| `docs/analysis/pre-cleanup-app-validation.md` | `pre-cleanup-app-validation.md` |
| `docs/analysis/projection-determinism-audit.md` | `projection-determinism-audit.md` |
| `docs/analysis/projection-trend-pipeline-audit.md` | `projection-trend-pipeline-audit.md` |
| `docs/analysis/runtime-integrity-fix.md` | `runtime-integrity-fix.md` |
| `docs/analysis/sprint-commitment-application-alignment.md` | `sprint-commitment-application-alignment.md` |
| `docs/analysis/sprint-commitment-cdc-extraction.md` | `sprint-commitment-cdc-extraction.md` |
| `docs/analysis/sprint-commitment-handler-simplification.md` | `sprint-commitment-handler-simplification.md` |
| `docs/analysis/sprint-scope-audit.md` | `sprint-scope-audit.md` |
| `docs/analysis/sqlite-buildquality-database-discovery-report.md` | `sqlite-buildquality-database-discovery-report.md` |
| `docs/analysis/state-sprint-delivery-audit.md` | `state-sprint-delivery-audit.md` |
| `docs/analysis/statistical-core-cleanup-report.md` | `statistical-core-cleanup-report.md` |
| `docs/analysis/statistical-helper-audit.md` | `statistical-helper-audit.md` |
| `docs/analysis/test-cleanup-step1.md` | `test-cleanup-step1.md` |
| `docs/analysis/test-ownership-audit.md` | `test-ownership-audit.md` |
| `docs/analysis/test-ownership-normalization.md` | `test-ownership-normalization.md` |
| `docs/analysis/tfs-api-version-configuration-inspection-report.md` | `tfs-api-version-configuration-inspection-report.md` |
| `docs/analysis/transport-naming-alignment.md` | `transport-naming-alignment.md` |
| `docs/analysis/trend-delivery-analytics-exploration.md` | `trend-delivery-analytics-exploration.md` |
| `docs/analysis/ui-semantic-correction.md` | `ui-semantic-correction.md` |
| `docs/analysis/ui-storypoint-adoption.md` | `ui-storypoint-adoption.md` |
| `docs/analysis/unit-test-cleanup-report.md` | `unit-test-cleanup-report.md` |
| `docs/analysis/unit-test-inventory-audit.md` | `unit-test-inventory-audit.md` |
| `docs/analysis/unit-test-redundancy-audit.md` | `unit-test-redundancy-audit.md` |
| `docs/analysis/unit-test-speed-audit.md` | `unit-test-speed-audit.md` |
| `docs/analysis/unit-test-strategy.md` | `unit-test-strategy.md` |
| `docs/analysis/workspace-hub-tile-analysis.md` | `workspace-hub-tile-analysis.md` |
| `docs/implementation/bug-trend-followups.md` | `bug-trend-followups.md` |
| `docs/architecture/repository-domain-discovery.md` | `repository-domain-discovery.md` |
| `docs/architecture/backlog-quality-domain-model.md` | `backlog-quality-domain-model.md` |
| `docs/architecture/cdc-domain-map.md` | `cdc-domain-map.md` |
| `docs/architecture/cdc-domain-map-generated.md` | `cdc-domain-map-generated.md` |
| `docs/architecture/cdc-reference.md` | `cdc-reference.md` |
| `docs/architecture/domain-model.md` | `domain-model.md` |
| `docs/architecture/effort-diagnostics-domain-model.md` | `effort-diagnostics-domain-model.md` |
| `docs/architecture/forecasting-domain-model.md` | `forecasting-domain-model.md` |
| `docs/architecture/portfolio-flow-model.md` | `portfolio-flow-model.md` |
| `docs/rules/estimation-rules.md` | `estimation-rules.md` |
| `docs/rules/hierarchy-rules.md` | `hierarchy-rules.md` |
| `docs/rules/metrics-rules.md` | `metrics-rules.md` |
| `docs/rules/propagation-rules.md` | `propagation-rules.md` |
| `docs/rules/source-rules.md` | `source-rules.md` |
| `docs/rules/sprint-rules.md` | `sprint-rules.md` |
| `docs/rules/state-rules.md` | `state-rules.md` |
| `docs/architecture/sprint-commitment-cdc-summary.md` | `sprint-commitment-cdc-summary.md` |
| `docs/architecture/sprint-commitment-domain-model.md` | `sprint-commitment-domain-model.md` |
| `docs/rules/ui-semantic-rules.md` | `ui-semantic-rules.md` |
| `docs/analysis/sprint-commitment-domain-exploration.md` | `sprint-commitment-domain-exploration.md` |
| `docs/analysis/canonical-filter-state-model.md` | `canonical-filter-state-model.md` |
| `docs/implementation/health-additional-signals.md` | `health-additional-signals.md` |
| `docs/implementation/navigation-decision-backlog.md` | `navigation-decision-backlog.md` |
| `docs/implementation/navigation-followup-actions.md` | `navigation-followup-actions.md` |
| `.github/pull_request_template.md` | `pr-template.md` |
| `docs/analysis/tfs-integration-review.md` | `tfsintegrationreview.md` |
| `docs/implementation/application-simplification-plan.md` | `application-simplification-plan.md` |
| `docs/analysis/screenshot-index-exploratory-testing.md` | `readme.md` |
| `features/02032026_backlog_health.md` | `02032026-backlog-health.md` |
| `features/20260110_User_profile_creation.md` | `20260110-user-profile-creation.md` |
| `features/20260119_workitem_validation.md` | `20260119-workitem-validation.md` |
| `features/20260126_epic_planning_v2.md` | `20260126-epic-planning-v2.md` |
| `features/Dependency_graph.md` | `dependency-graph.md` |
| `features/Pipeline_insights.md` | `pipeline-insights.md` |
| `features/Simple_workitem_explorer.md` | `simple-workitem-explorer.md` |
| `features/User_landing_v2.md` | `user-landing-v2.md` |
| `features/VERIFY_TFS_API_INTEGRATION.md` | `verify-tfs-api-integration.md` |
| `features/Verify_TFS_API.md` | `verify-tfs-api.md` |
| `features/effort_distribution_analytics.md` | `effort-distribution-analytics.md` |
| `features/epic_planning.md` | `epic-planning.md` |
| `features/planning_board_decommission.md` | `planning-board-decommission.md` |
| `features/plans/20260110_User_profile_creation_plan.md` | `20260110-user-profile-creation-plan.md` |
| `features/pr_insight.md` | `pr-insight.md` |
| `features/state_timeline.md` | `state-timeline.md` |

## 5. Remaining structural issues

### 5.1 Remaining docs root violations
- `docs/rules/architecture-rules.md`
- `docs/rules/copilot-architecture-contract.md`
- `docs/rules/ef-rules.md`
- `docs/rules/fluent-ui-compat-rules.md`
- `docs/rules/process-rules.md`
- `docs/rules/tfs-integration-rules.md`
- `docs/rules/ui-loading-rules.md`
- `docs/rules/ui-rules.md`
- `docs/implementation/bug-trend-followups.md`
- `docs/implementation/health-additional-signals.md`
- `docs/implementation/navigation-decision-backlog.md`
- `docs/implementation/navigation-followup-actions.md`
- `.github/pull_request_template.md`
- `docs/analysis/sprint-scoping-limitations.md`

### 5.2 Non-canonical docs folders still containing markdown
- `docs/cleanup`
- `docs/exploration`
- `docs/filters`
- `docs/implementation`
- `docs/roadmaps`
- `docs/screenshots`

### 5.3 Additional notes
- Repository root markdown is already compliant for Batch 1: only `README.md` remains at the root.
- Rules documents (`ARCHITECTURE_RULES.md`, `PROCESS_RULES.md`, `UI_RULES.md`, etc.) were left in place because moving them would require coordinated updates to `.github/copilot-instructions.md`, feature docs, and at least one hardcoded test path.
- General docs with unclear canonical placement (`bug-trend-followups.md`, `health-additional-signals.md`, `navigation-decision-backlog.md`, `navigation-followup-actions.md`, `pr_template.md`, `sprint-scoping-limitations.md`) were intentionally left for a later batch.

## 6. OData / validator residue

| Path | Classification | Notes |
|---|---|---|
| `docs/archive/legacy-revision-ingestion/odata-ingestion-fix-plan.md` | `archive` | Archived in Batch 3 because the OData ingestion experiment was removed. |
| `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md` | `archive` | Already archived legacy ingestion analysis; leave as archived residue. |
| `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md` | `archive` | Already archived divergence analysis; keep archived. |
| `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md` | `archive` | Legacy revision-ingestion design artifact; keep archived. |
| `docs/archive/validation/validators-implementation-2026-01-30.md` | `unclear` | Historical validator implementation summary; keep as history until a dedicated validator cleanup batch decides whether it still adds value. |

## 7. .github candidates

- `.github/workflows/README.md` — candidate for later docs/history or docs/analysis relocation; not moved because workflow-local placement may still be intentional
- `.github/pull_request_template.md` — keep in .github for functionality, but include in later consolidation review because duplicate template content also exists at .github/pull_request_template.md

## 8. Next batch plan

- batch 2: .github consolidation
- batch 3: validator + OData removal
- batch 4: filename enforcement
