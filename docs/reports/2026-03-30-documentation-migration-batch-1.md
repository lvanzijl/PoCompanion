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
| `docs/TFS_CACHE_IMPLEMENTATION_PLAN.md` | `docs/plans/tfs-cache-implementation-plan.md` |
| `docs/UI_MIGRATION_PLAN.md` | `docs/plans/ui-migration-plan.md` |
| `docs/MULTI_SELECT_BEHAVIOR.md` | `docs/user/multi-select-behavior.md` |
| `docs/NAVIGATION_MAP.md` | `docs/user/navigation-map.md` |
| `docs/health_workspace_fix_plan.md` | `docs/plans/health-workspace-fix-plan.md` |
| `docs/iteration_path_sorting_audit.md` | `docs/audits/iteration-path-sorting-audit.md` |
| `docs/sprintmetrics_iteration_migration_plan.md` | `docs/plans/sprintmetrics-iteration-migration-plan.md` |
| `docs/sqlite-datetime-fix.md` | `docs/reports/sqlite-datetime-fix.md` |
| `docs/sqlite-timestamp-fix-audit.md` | `docs/audits/sqlite-timestamp-fix-audit.md` |
| `docs/test-determinism-report.md` | `docs/reports/test-determinism-report.md` |

## 3. Naming normalization

The following moved files were normalized to lowercase kebab-case as part of the move:

- `docs/analysis/live-tfs-calls-analysis.md`
- `docs/analysis/realtfsclient-getall-analysis.md`
- `docs/plans/tfs-cache-implementation-plan.md`
- `docs/plans/ui-migration-plan.md`
- `docs/user/multi-select-behavior.md`
- `docs/user/navigation-map.md`
- `docs/plans/health-workspace-fix-plan.md`
- `docs/audits/iteration-path-sorting-audit.md`
- `docs/plans/sprintmetrics-iteration-migration-plan.md`
- `docs/reports/sqlite-datetime-fix.md`
- `docs/audits/sqlite-timestamp-fix-audit.md`
- `docs/reports/test-determinism-report.md`

## 4. Filename convention violations (proposals only)

Per the binding clarification, the date-prefixed `YYYY-MM-DD-...` rule applies only to new task-generated reports. The proposals below therefore normalize existing filenames to lowercase kebab-case only; no existing files were renamed in this section.

| Current path | Proposed filename |
|---|---|
| `.github/pull_request_template.md` | `pull-request-template.md` |
| `.github/workflows/README.md` | `readme.md` |
| `README.md` | `readme.md` |
| `docs/ARCHITECTURE_RULES.md` | `architecture-rules.md` |
| `docs/COPILOT_ARCHITECTURE_CONTRACT.md` | `copilot-architecture-contract.md` |
| `docs/EF_RULES.md` | `ef-rules.md` |
| `docs/Fluent_UI_compat_rules.md` | `fluent-ui-compat-rules.md` |
| `docs/PROCESS_RULES.md` | `process-rules.md` |
| `docs/README.md` | `readme.md` |
| `docs/TFS_INTEGRATION_RULES.md` | `tfs-integration-rules.md` |
| `docs/UI_LOADING_RULES.md` | `ui-loading-rules.md` |
| `docs/UI_RULES.md` | `ui-rules.md` |
| `docs/architecture/portfolio_flow_data_signals.md` | `portfolio-flow-data-signals.md` |
| `docs/audits/application_handler_cleanup.md` | `application-handler-cleanup.md` |
| `docs/audits/application_semantic_audit.md` | `application-semantic-audit.md` |
| `docs/audits/application_simplification_audit.md` | `application-simplification-audit.md` |
| `docs/audits/backlog_health_simplification.md` | `backlog-health-simplification.md` |
| `docs/audits/backlog_quality_cdc_summary.md` | `backlog-quality-cdc-summary.md` |
| `docs/audits/backlog_quality_domain_exploration.md` | `backlog-quality-domain-exploration.md` |
| `docs/audits/buildquality_application_page_integration_report.md` | `buildquality-application-page-integration-report.md` |
| `docs/audits/buildquality_calculation_validation_report.md` | `buildquality-calculation-validation-report.md` |
| `docs/audits/buildquality_cdc_contract_report.md` | `buildquality-cdc-contract-report.md` |
| `docs/audits/buildquality_chart_state_cleanup_report.md` | `buildquality-chart-state-cleanup-report.md` |
| `docs/audits/buildquality_data_aggregation_contract_report.md` | `buildquality-data-aggregation-contract-report.md` |
| `docs/audits/buildquality_discovery_report.md` | `buildquality-discovery-report.md` |
| `docs/audits/buildquality_edge_consistency_report.md` | `buildquality-edge-consistency-report.md` |
| `docs/audits/buildquality_implementation_contract_report.md` | `buildquality-implementation-contract-report.md` |
| `docs/audits/buildquality_missing_ingestion_build_168570_code_analysis_report.md` | `buildquality-missing-ingestion-build-168570-code-analysis-report.md` |
| `docs/audits/buildquality_retrieval_performance_report.md` | `buildquality-retrieval-performance-report.md` |
| `docs/audits/buildquality_seed_data_report.md` | `buildquality-seed-data-report.md` |
| `docs/audits/buildquality_ui_compliance_audit_report.md` | `buildquality-ui-compliance-audit-report.md` |
| `docs/audits/buildquality_ui_final_integration_report.md` | `buildquality-ui-final-integration-report.md` |
| `docs/audits/cdc_behavioral_stress_test_audit.md` | `cdc-behavioral-stress-test-audit.md` |
| `docs/audits/cdc_completion_summary.md` | `cdc-completion-summary.md` |
| `docs/audits/cdc_coverage_audit.md` | `cdc-coverage-audit.md` |
| `docs/audits/cdc_extraction_summary.md` | `cdc-extraction-summary.md` |
| `docs/audits/cdc_freeze_audit.md` | `cdc-freeze-audit.md` |
| `docs/audits/cdc_invariant_tests.md` | `cdc-invariant-tests.md` |
| `docs/audits/cdc_replay_fixture_validation.md` | `cdc-replay-fixture-validation.md` |
| `docs/audits/cdc_usage_coverage.md` | `cdc-usage-coverage.md` |
| `docs/audits/compatibility_cleanup_phase3.md` | `compatibility-cleanup-phase3.md` |
| `docs/audits/delivery_trend_analytics_cdc_summary.md` | `delivery-trend-analytics-cdc-summary.md` |
| `docs/audits/domain_library_readiness_audit.md` | `domain-library-readiness-audit.md` |
| `docs/audits/domain_logic_outside_cdc_exploration.md` | `domain-logic-outside-cdc-exploration.md` |
| `docs/audits/dto_contract_cleanup.md` | `dto-contract-cleanup.md` |
| `docs/audits/effort_diagnostics_cdc_extraction_report.md` | `effort-diagnostics-cdc-extraction-report.md` |
| `docs/audits/effort_diagnostics_cleanup_report.md` | `effort-diagnostics-cleanup-report.md` |
| `docs/audits/effort_diagnostics_domain_exploration.md` | `effort-diagnostics-domain-exploration.md` |
| `docs/audits/effort_diagnostics_semantic_audit.md` | `effort-diagnostics-semantic-audit.md` |
| `docs/audits/effort_planning_boundary_audit.md` | `effort-planning-boundary-audit.md` |
| `docs/audits/effort_planning_boundary_cleanup.md` | `effort-planning-boundary-cleanup.md` |
| `docs/audits/effort_planning_cdc_extraction.md` | `effort-planning-cdc-extraction.md` |
| `docs/audits/estimation_audit.md` | `estimation-audit.md` |
| `docs/audits/final_pre_usage_validation.md` | `final-pre-usage-validation.md` |
| `docs/audits/forecasting_cdc_summary.md` | `forecasting-cdc-summary.md` |
| `docs/audits/forecasting_domain_exploration.md` | `forecasting-domain-exploration.md` |
| `docs/audits/forecasting_semantic_audit.md` | `forecasting-semantic-audit.md` |
| `docs/audits/hexagon_boundary_enforcement.md` | `hexagon-boundary-enforcement.md` |
| `docs/audits/hierarchy_propagation_audit.md` | `hierarchy-propagation-audit.md` |
| `docs/audits/metrics_audit.md` | `metrics-audit.md` |
| `docs/audits/mock_data_quality.md` | `mock-data-quality.md` |
| `docs/audits/mock_pr_pipeline_seed_validation.md` | `mock-pr-pipeline-seed-validation.md` |
| `docs/audits/portfolio_flow_application_migration.md` | `portfolio-flow-application-migration.md` |
| `docs/audits/portfolio_flow_consumers_audit.md` | `portfolio-flow-consumers-audit.md` |
| `docs/audits/portfolio_flow_domain_exploration.md` | `portfolio-flow-domain-exploration.md` |
| `docs/audits/portfolio_flow_feasibility.md` | `portfolio-flow-feasibility.md` |
| `docs/audits/portfolio_flow_projection.md` | `portfolio-flow-projection.md` |
| `docs/audits/portfolio_flow_projection_validation.md` | `portfolio-flow-projection-validation.md` |
| `docs/audits/portfolio_flow_semantic_audit.md` | `portfolio-flow-semantic-audit.md` |
| `docs/audits/portfolio_flow_signal_enablement.md` | `portfolio-flow-signal-enablement.md` |
| `docs/audits/portfolio_handler_simplification.md` | `portfolio-handler-simplification.md` |
| `docs/audits/post_runtime_fix_validation.md` | `post-runtime-fix-validation.md` |
| `docs/audits/pr_pipeline_linkage_analysis.md` | `pr-pipeline-linkage-analysis.md` |
| `docs/audits/pre_cleanup_app_validation.md` | `pre-cleanup-app-validation.md` |
| `docs/audits/projection_determinism_audit.md` | `projection-determinism-audit.md` |
| `docs/audits/projection_trend_pipeline_audit.md` | `projection-trend-pipeline-audit.md` |
| `docs/audits/runtime_integrity_fix.md` | `runtime-integrity-fix.md` |
| `docs/audits/sprint_commitment_application_alignment.md` | `sprint-commitment-application-alignment.md` |
| `docs/audits/sprint_commitment_cdc_extraction.md` | `sprint-commitment-cdc-extraction.md` |
| `docs/audits/sprint_commitment_handler_simplification.md` | `sprint-commitment-handler-simplification.md` |
| `docs/audits/sprint_scope_audit.md` | `sprint-scope-audit.md` |
| `docs/audits/sqlite_buildquality_database_discovery_report.md` | `sqlite-buildquality-database-discovery-report.md` |
| `docs/audits/state_sprint_delivery_audit.md` | `state-sprint-delivery-audit.md` |
| `docs/audits/statistical_core_cleanup_report.md` | `statistical-core-cleanup-report.md` |
| `docs/audits/statistical_helper_audit.md` | `statistical-helper-audit.md` |
| `docs/audits/test_cleanup_step1.md` | `test-cleanup-step1.md` |
| `docs/audits/test_ownership_audit.md` | `test-ownership-audit.md` |
| `docs/audits/test_ownership_normalization.md` | `test-ownership-normalization.md` |
| `docs/audits/tfs_api_version_configuration_inspection_report.md` | `tfs-api-version-configuration-inspection-report.md` |
| `docs/audits/transport_naming_alignment.md` | `transport-naming-alignment.md` |
| `docs/audits/trend_delivery_analytics_exploration.md` | `trend-delivery-analytics-exploration.md` |
| `docs/audits/ui_semantic_correction.md` | `ui-semantic-correction.md` |
| `docs/audits/ui_storypoint_adoption.md` | `ui-storypoint-adoption.md` |
| `docs/audits/unit_test_cleanup_report.md` | `unit-test-cleanup-report.md` |
| `docs/audits/unit_test_inventory_audit.md` | `unit-test-inventory-audit.md` |
| `docs/audits/unit_test_redundancy_audit.md` | `unit-test-redundancy-audit.md` |
| `docs/audits/unit_test_speed_audit.md` | `unit-test-speed-audit.md` |
| `docs/audits/unit_test_strategy.md` | `unit-test-strategy.md` |
| `docs/audits/workspace_hub_tile_analysis.md` | `workspace-hub-tile-analysis.md` |
| `docs/bug_trend_followups.md` | `bug-trend-followups.md` |
| `docs/domain/REPOSITORY_DOMAIN_DISCOVERY.md` | `repository-domain-discovery.md` |
| `docs/domain/backlog_quality_domain_model.md` | `backlog-quality-domain-model.md` |
| `docs/domain/cdc_domain_map.md` | `cdc-domain-map.md` |
| `docs/domain/cdc_domain_map_generated.md` | `cdc-domain-map-generated.md` |
| `docs/domain/cdc_reference.md` | `cdc-reference.md` |
| `docs/domain/domain_model.md` | `domain-model.md` |
| `docs/domain/effort_diagnostics_domain_model.md` | `effort-diagnostics-domain-model.md` |
| `docs/domain/forecasting_domain_model.md` | `forecasting-domain-model.md` |
| `docs/domain/portfolio_flow_model.md` | `portfolio-flow-model.md` |
| `docs/domain/rules/estimation_rules.md` | `estimation-rules.md` |
| `docs/domain/rules/hierarchy_rules.md` | `hierarchy-rules.md` |
| `docs/domain/rules/metrics_rules.md` | `metrics-rules.md` |
| `docs/domain/rules/propagation_rules.md` | `propagation-rules.md` |
| `docs/domain/rules/source_rules.md` | `source-rules.md` |
| `docs/domain/rules/sprint_rules.md` | `sprint-rules.md` |
| `docs/domain/rules/state_rules.md` | `state-rules.md` |
| `docs/domain/sprint_commitment_cdc_summary.md` | `sprint-commitment-cdc-summary.md` |
| `docs/domain/sprint_commitment_domain_model.md` | `sprint-commitment-domain-model.md` |
| `docs/domain/ui_semantic_rules.md` | `ui-semantic-rules.md` |
| `docs/exploration/sprint_commitment_domain_exploration.md` | `sprint-commitment-domain-exploration.md` |
| `docs/filters/canonical_filter_state_model.md` | `canonical-filter-state-model.md` |
| `docs/health_additional_signals.md` | `health-additional-signals.md` |
| `docs/navigation_decision_backlog.md` | `navigation-decision-backlog.md` |
| `docs/navigation_followup_actions.md` | `navigation-followup-actions.md` |
| `docs/pr_template.md` | `pr-template.md` |
| `docs/reviews/TfsIntegrationReview.md` | `tfsintegrationreview.md` |
| `docs/roadmaps/application_simplification_plan.md` | `application-simplification-plan.md` |
| `docs/screenshots/README.md` | `readme.md` |
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
- `docs/ARCHITECTURE_RULES.md`
- `docs/COPILOT_ARCHITECTURE_CONTRACT.md`
- `docs/EF_RULES.md`
- `docs/Fluent_UI_compat_rules.md`
- `docs/PROCESS_RULES.md`
- `docs/TFS_INTEGRATION_RULES.md`
- `docs/UI_LOADING_RULES.md`
- `docs/UI_RULES.md`
- `docs/bug_trend_followups.md`
- `docs/health_additional_signals.md`
- `docs/navigation_decision_backlog.md`
- `docs/navigation_followup_actions.md`
- `docs/pr_template.md`
- `docs/sprint-scoping-limitations.md`

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
- General docs with unclear canonical placement (`bug_trend_followups.md`, `health_additional_signals.md`, `navigation_decision_backlog.md`, `navigation_followup_actions.md`, `pr_template.md`, `sprint-scoping-limitations.md`) were intentionally left for a later batch.

## 6. OData / validator residue

| Path | Classification | Notes |
|---|---|---|
| `docs/reports/odata-ingestion-fix-plan.md` | `unclear` | Active plan/report; keep in place until ingestion cleanup batch decides whether it remains active or moves to history. |
| `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md` | `archive` | Already archived legacy ingestion analysis; leave as archived residue. |
| `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md` | `archive` | Already archived divergence analysis; keep archived. |
| `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md` | `archive` | Legacy revision-ingestion design artifact; keep archived. |
| `docs/history/validation/validators-implementation-2026-01-30.md` | `unclear` | Historical validator implementation summary; keep as history until a dedicated validator cleanup batch decides whether it still adds value. |

## 7. .github candidates

- `.github/workflows/README.md` — candidate for later docs/history or docs/analysis relocation; not moved because workflow-local placement may still be intentional
- `.github/pull_request_template.md` — keep in .github for functionality, but include in later consolidation review because duplicate template content also exists at docs/pr_template.md

## 8. Next batch plan

- batch 2: .github consolidation
- batch 3: validator + OData removal
- batch 4: filename enforcement
