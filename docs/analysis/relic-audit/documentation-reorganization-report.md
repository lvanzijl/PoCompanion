> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Documentation Reorganization Report

## 1. Summary
- **Files moved:** 38
- **Folders created:** 8
- **Links updated:** 42

## 2. Folder structure (final)
```text
docs
docs/rules/architecture-rules.md
docs/rules/copilot-architecture-contract.md
docs/rules/ef-rules.md
docs/rules/fluent-ui-compat-rules.md
docs/LIVE_TFS_CALLS_ANALYSIS.md
docs/MULTI_SELECT_BEHAVIOR.md
docs/NAVIGATION_MAP.md
docs/rules/process-rules.md
docs/README.md
docs/REALTFSCLIENT_GETALL_ANALYSIS.md
docs/TFS_CACHE_IMPLEMENTATION_PLAN.md
docs/rules/tfs-integration-rules.md
docs/rules/ui-loading-rules.md
docs/UI_MIGRATION_PLAN.md
docs/rules/ui-rules.md
docs/analysis
docs/analysis/2026-03-18-breadcrumbs-sprint-delivery.md
docs/analysis/2026-03-18-bug-trend-count-mismatch.md
docs/analysis/2026-03-18-incremental-sync-planner-live-validation.md
docs/analysis/2026-03-18-parent-move-causes-resolvedworkitem-without-workitem.md
docs/analysis/2026-03-18-pr-lifetime-scatter-ux-issues.md
docs/analysis/2026-03-18-progress-trend-effort-spike.md
docs/analysis/2026-03-18-sync-testability-architecture.md
docs/analysis/2026-03-20-profile-export-import-and-onboarding-completion.md
docs/analysis/api-readmodels-validation.md
docs/analysis/cdc-corrections.md
docs/analysis/cross-slice-quality-feature-analysis.md
docs/analysis/ef-sqlite-compatibility-audit.md
docs/analysis/epic-aggregation-null-semantics-fix.md
docs/analysis/epic-aggregation-validation.md
docs/analysis/feature-forecast-validation.md
docs/analysis/field-contract.md
docs/analysis/filtering.md
docs/analysis/final-cdc-integration.md
docs/analysis/hierarchy-aggregation.md
docs/analysis/insight-null-semantics-fix.md
docs/analysis/insight-validation.md
docs/analysis/planning-quality-validation.md
docs/analysis/planning-quality.md
docs/analysis/product-aggregation-validation.md
docs/analysis/progress-model.md
docs/analysis/relic-audit
docs/analysis/relic-audit/repository-relic-audit.md
docs/analysis/snapshot-comparison-validation.md
docs/analysis/snapshots.md
docs/analysis/state-classifications.md
docs/analysis/ui-integration-validation.md
docs/rules/validation-rules.md
docs/architecture
docs/architecture/build-quality-persistence-abstraction.md
docs/architecture/canonical-workitem-alignment.md
docs/architecture/canonical-workitem-test-fix.md
docs/architecture/cdc-decision-record.md
docs/architecture/cdc-domain-map-audit-fix.md
docs/architecture/cross-slice-validation.md
docs/architecture/failure-classification-normalization.md
docs/architecture/final-test-fixes.md
docs/architecture/incremental-sync-planner.md
docs/architecture/persistence-abstraction-design.md
docs/architecture/pipeline-identity-normalization.md
docs/architecture/pipeline-insights-persistence-abstraction.md
docs/architecture/pipeline-time-semantics-migration.md
docs/architecture/portfolio-backlog-regression-investigation.md
docs/architecture/portfolio_flow_data_signals.md
docs/architecture/post-fix-stability-verification.md
docs/architecture/product-scope-validation-alignment.md
docs/architecture/pull-request-analytical-read-consolidation.md
docs/architecture/pull-request-analytical-read-validation-final.md
docs/architecture/pull-request-persistence-abstraction-validation.md
docs/architecture/repository-identity-normalization.md
docs/architecture/repository-stability-audit.md
docs/architecture/restore-build-determinism-audit.md
docs/architecture/restore-build-determinism-fix.md
docs/architecture/scatter-ordering-regression.md
docs/architecture/test-failure-cluster-fix.md
docs/architecture/test-failure-isolation.md
docs/architecture/test-setup-corrections.md
docs/architecture/validation-system-report.md
docs/architecture/workitem-query-boundary-phase1.md
docs/architecture/workitem-query-boundary-phase2.md
docs/architecture/workitem-query-boundary-phase3-goal-hierarchy.md
docs/archive
docs/archive/code-quality
docs/archive/code-quality/work-completed-2026-01-30.md
docs/archive/experiments
docs/archive/experiments/.gitkeep
docs/archive/legacy-revision-ingestion
docs/analysis/cache-insights-and-validation-report.md
docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md
docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md
docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md
docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md
docs/analysis/sprint-trends-vs-revisions-report.md
docs/audits
docs/analysis/application_handler_cleanup.md
docs/analysis/application_semantic_audit.md
docs/analysis/application_simplification_audit.md
docs/analysis/backlog_health_simplification.md
docs/analysis/backlog_quality_cdc_summary.md
docs/analysis/backlog_quality_domain_exploration.md
docs/analysis/buildquality_application_page_integration_report.md
docs/analysis/buildquality_calculation_validation_report.md
docs/analysis/buildquality_cdc_contract_report.md
docs/analysis/buildquality_chart_state_cleanup_report.md
docs/analysis/buildquality_data_aggregation_contract_report.md
docs/analysis/buildquality_discovery_report.md
docs/analysis/buildquality_edge_consistency_report.md
docs/analysis/buildquality_implementation_contract_report.md
docs/analysis/buildquality_missing_ingestion_build_168570_code_analysis_report.md
docs/analysis/buildquality_retrieval_performance_report.md
docs/analysis/buildquality_seed_data_report.md
docs/analysis/buildquality_ui_compliance_audit_report.md
docs/analysis/buildquality_ui_final_integration_report.md
docs/analysis/cdc-full-quality-audit.md
docs/analysis/cdc_behavioral_stress_test_audit.md
docs/analysis/cdc_completion_summary.md
docs/analysis/cdc_coverage_audit.md
docs/analysis/cdc_extraction_summary.md
docs/analysis/cdc_freeze_audit.md
docs/analysis/cdc_invariant_tests.md
docs/analysis/cdc_replay_fixture_validation.md
docs/analysis/cdc_usage_coverage.md
docs/analysis/compatibility_cleanup_phase3.md
docs/analysis/delivery_trend_analytics_cdc_summary.md
docs/analysis/domain_library_readiness_audit.md
docs/analysis/domain_logic_outside_cdc_exploration.md
docs/analysis/dto_contract_cleanup.md
docs/analysis/effort_diagnostics_cdc_extraction_report.md
docs/analysis/effort_diagnostics_cleanup_report.md
docs/analysis/effort_diagnostics_domain_exploration.md
docs/analysis/effort_diagnostics_semantic_audit.md
docs/analysis/effort_planning_boundary_audit.md
docs/analysis/effort_planning_boundary_cleanup.md
docs/analysis/effort_planning_cdc_extraction.md
docs/analysis/estimation_audit.md
docs/analysis/final_pre_usage_validation.md
docs/analysis/forecasting_cdc_summary.md
docs/analysis/forecasting_domain_exploration.md
docs/analysis/forecasting_semantic_audit.md
docs/analysis/hexagon_boundary_enforcement.md
docs/analysis/hierarchy_propagation_audit.md
docs/analysis/metrics_audit.md
docs/analysis/mock_data_quality.md
docs/analysis/mock_pr_pipeline_seed_validation.md
docs/analysis/portfolio_flow_application_migration.md
docs/analysis/portfolio_flow_consumers_audit.md
docs/analysis/portfolio_flow_domain_exploration.md
docs/analysis/portfolio_flow_feasibility.md
docs/analysis/portfolio_flow_projection.md
docs/analysis/portfolio_flow_projection_validation.md
docs/analysis/portfolio_flow_semantic_audit.md
docs/analysis/portfolio_flow_signal_enablement.md
docs/analysis/portfolio_handler_simplification.md
docs/analysis/post_runtime_fix_validation.md
docs/analysis/pr_pipeline_linkage_analysis.md
docs/analysis/pre_cleanup_app_validation.md
docs/analysis/projection_determinism_audit.md
docs/analysis/projection_trend_pipeline_audit.md
docs/analysis/runtime_integrity_fix.md
docs/analysis/sprint_commitment_application_alignment.md
docs/analysis/sprint_commitment_cdc_extraction.md
docs/analysis/sprint_commitment_handler_simplification.md
docs/analysis/sprint_scope_audit.md
docs/analysis/sqlite_buildquality_database_discovery_report.md
docs/analysis/state_sprint_delivery_audit.md
docs/analysis/statistical_core_cleanup_report.md
docs/analysis/statistical_helper_audit.md
docs/analysis/test_cleanup_step1.md
docs/analysis/test_ownership_audit.md
docs/analysis/test_ownership_normalization.md
docs/analysis/tfs_api_version_configuration_inspection_report.md
docs/analysis/transport_naming_alignment.md
docs/analysis/trend_delivery_analytics_exploration.md
docs/analysis/ui_semantic_correction.md
docs/analysis/ui_storypoint_adoption.md
docs/analysis/unit_test_cleanup_report.md
docs/analysis/unit_test_inventory_audit.md
docs/analysis/unit_test_redundancy_audit.md
docs/analysis/unit_test_speed_audit.md
docs/analysis/unit_test_strategy.md
docs/analysis/workspace_hub_tile_analysis.md
docs/implementation/bug-trend-followups.md
docs/cleanup
docs/reports/2026-03-30-cleanup-obsolete-changes-log.md
docs/reports/2026-03-30-cleanup-phase1-client-reachability-report.md
docs/reports/2026-03-30-cleanup-phase2-endpoint-usage-report.md
docs/reports/2026-03-30-cleanup-phase3-handler-usage-report.md
docs/reports/2026-03-30-cleanup-phase4-full-layer-summary.md
docs/domain
docs/architecture/repository-domain-discovery.md
docs/architecture/backlog-quality-domain-model.md
docs/architecture/cdc-domain-map.md
docs/architecture/cdc-domain-map-generated.md
docs/architecture/cdc-reference.md
docs/architecture/domain-model.md
docs/architecture/effort-diagnostics-domain-model.md
docs/architecture/forecasting-domain-model.md
docs/architecture/portfolio-flow-model.md
docs/domain/rules
docs/rules/estimation-rules.md
docs/rules/hierarchy-rules.md
docs/rules/metrics-rules.md
docs/rules/propagation-rules.md
docs/rules/source-rules.md
docs/rules/sprint-rules.md
docs/rules/state-rules.md
docs/architecture/sprint-commitment-cdc-summary.md
docs/architecture/sprint-commitment-domain-model.md
docs/rules/ui-semantic-rules.md
docs/exploration
docs/analysis/sprint-commitment-domain-exploration.md
docs/filters
docs/analysis/cache-only-guardrail-analysis-pipeline-workitems.md
docs/analysis/canonical_filter_state_model.md
docs/analysis/datasource-enforcement.md
docs/analysis/filter-analysis-improved.md
docs/analysis/filter-analysis.md
docs/analysis/filter-canonical-model.md
docs/analysis/filter-cross-slice-migration.md
docs/analysis/filter-current-state-analysis.md
docs/analysis/filter-delivery-migration.md
docs/analysis/filter-final-cleanup-report.md
docs/analysis/filter-implementation-design.md
docs/analysis/filter-implementation-execution-plan.md
docs/analysis/filter-implementation-plan.md
docs/analysis/filter-performance-audit.md
docs/analysis/filter-performance-verification.md
docs/analysis/filter-phases-1-4-pr-breakdown.md
docs/analysis/filter-pipeline-migration.md
docs/analysis/filter-pipeline-truncation-fix.md
docs/analysis/filter-pr-migration.md
docs/analysis/filter-sprint-migration.md
docs/analysis/filter-ui-behavior.md
docs/analysis/filter-ui-metadata-fix.md
docs/analysis/filter-validation-report.md
docs/analysis/page-filter-contracts.md
docs/analysis/pipeline-guardrail-and-workitem-split.md
docs/analysis/pipeline-provider-cleanup.md
docs/analysis/pr-batching-verification.md
docs/analysis/pr-cache-only-guardrails.md
docs/analysis/pr-live-provider-usage-audit.md
docs/analysis/pr-provider-cleanup.md
docs/analysis/tfs-access-boundary-sealed.md
docs/analysis/tfs-access-boundary-verification.md
docs/analysis/workitem-route-classification-fix.md
docs/implementation/health-additional-signals.md
docs/health_workspace_fix_plan.md
docs/history
docs/history/code-quality
docs/archive/code-quality/code-audit-report-2026-01-30.md
docs/archive/code-quality/final-summary-2026-01-30.md
docs/archive/code-quality/fixes-applied-2026-01-30.md
docs/archive/code-quality/non-test-issues-analysis-2026-01-30.md
docs/history/validation
docs/archive/validation/validators-implementation-2026-01-30.md
docs/implementation
docs/implementation/battleship-cdc-extension-report.md
docs/implementation/cdc-critical-fixes.md
docs/implementation/cdc-fallback-timestamp-hardening.md
docs/implementation/cdc-fix-report-empty-snapshot-snapshotcount.md
docs/implementation/phase-a-corrections.md
docs/implementation/phase-a-foundation.md
docs/implementation/phase-b-corrections.md
docs/implementation/phase-b-feature-progress.md
docs/implementation/phase-c-epic-progress.md
docs/implementation/phase-e-corrections.md
docs/implementation/phase-e-snapshots.md
docs/implementation/phase-f-lifecycle.md
docs/implementation/phase-g-consumption.md
docs/implementation/phase-h-persistence.md
docs/implementation/phase-i-finalization.md
docs/investigations
docs/iteration_path_sorting_audit.md
docs/implementation/navigation-decision-backlog.md
docs/implementation/navigation-followup-actions.md
.github/pull_request_template.md
docs/release-notes.json
docs/reports
docs/reports/2026-03-30-ingestion-observability-hardening.md
docs/archive/legacy-revision-ingestion/odata-ingestion-fix-plan.md
docs/reports/2026-03-30-sprint-attribution-analysis.md
docs/reports/2026-03-30-sprint-trends-current-state-analysis.md
docs/reviews
docs/analysis/tfs-integration-review.md
docs/reports/2026-03-30-swepo-review-report.md
docs/roadmaps
docs/implementation/application-simplification-plan.md
docs/screenshots
docs/analysis/screenshot-index-exploratory-testing.md
docs/analysis/sprint-scoping-limitations.md
docs/sprintmetrics_iteration_migration_plan.md
docs/sqlite-datetime-fix.md
docs/sqlite-timestamp-fix-audit.md
docs/test-determinism-report.md
docs/user
docs/architecture/gebruikershandleiding.md
```

## 3. Files moved
- `docs/analyze/api-readmodels-validation.md` → `docs/analysis/api-readmodels-validation.md`
- `docs/analyze/cdc-corrections.md` → `docs/analysis/cdc-corrections.md`
- `docs/analyze/ef-sqlite-compatibility-audit.md` → `docs/analysis/ef-sqlite-compatibility-audit.md`
- `docs/analyze/epic-aggregation-null-semantics-fix.md` → `docs/analysis/epic-aggregation-null-semantics-fix.md`
- `docs/analyze/epic-aggregation-validation.md` → `docs/analysis/epic-aggregation-validation.md`
- `docs/analyze/feature-forecast-validation.md` → `docs/analysis/feature-forecast-validation.md`
- `docs/analyze/field-contract.md` → `docs/analysis/field-contract.md`
- `docs/analyze/filtering.md` → `docs/analysis/filtering.md`
- `docs/analyze/final-cdc-integration.md` → `docs/analysis/final-cdc-integration.md`
- `docs/analyze/hierarchy-aggregation.md` → `docs/analysis/hierarchy-aggregation.md`
- `docs/analyze/insight-null-semantics-fix.md` → `docs/analysis/insight-null-semantics-fix.md`
- `docs/analyze/insight-validation.md` → `docs/analysis/insight-validation.md`
- `docs/analyze/planning-quality-validation.md` → `docs/analysis/planning-quality-validation.md`
- `docs/analyze/planning-quality.md` → `docs/analysis/planning-quality.md`
- `docs/analyze/product-aggregation-validation.md` → `docs/analysis/product-aggregation-validation.md`
- `docs/analyze/progress-model.md` → `docs/analysis/progress-model.md`
- `docs/analyze/snapshot-comparison-validation.md` → `docs/analysis/snapshot-comparison-validation.md`
- `docs/analyze/snapshots.md` → `docs/analysis/snapshots.md`
- `docs/analyze/state-classifications.md` → `docs/analysis/state-classifications.md`
- `docs/analyze/ui-integration-validation.md` → `docs/analysis/ui-integration-validation.md`
- `docs/analyze/validation-rules.md` → `docs/rules/validation-rules.md`
- `VALIDATION_SYSTEM_REPORT.md` → `docs/architecture/validation-system-report.md`
- `WORK_COMPLETED.md` → `docs/archive/code-quality/work-completed-2026-01-30.md`
- `REPORT.md` → `docs/analysis/cache-insights-and-validation-report.md`
- `docs/reports/odata-validator-vs-ingestion-report.md` → `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md`
- `docs/reviews/RealRevisionTfsClient_Pagination_Review.md` → `docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md`
- `docs/investigations/revision-ingestion-api-vs-validator-odata-divergence.md` → `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md`
- `docs/reports/revision-ingestor-v2.md` → `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md`
- `docs/reports/sprint-trends-vs-revisions-report.md` → `docs/analysis/sprint-trends-vs-revisions-report.md`
- `docs/audit/cdc-full-quality-audit.md` → `docs/analysis/cdc-full-quality-audit.md`
- `CODE_AUDIT_REPORT.md` → `docs/archive/code-quality/code-audit-report-2026-01-30.md`
- `FINAL_SUMMARY.md` → `docs/archive/code-quality/final-summary-2026-01-30.md`
- `FIXES_APPLIED.md` → `docs/archive/code-quality/fixes-applied-2026-01-30.md`
- `NON_TEST_ISSUES_ANALYSIS.md` → `docs/archive/code-quality/non-test-issues-analysis-2026-01-30.md`
- `VALIDATORS_IMPLEMENTATION.md` → `docs/archive/validation/validators-implementation-2026-01-30.md`
- `docs/Reports/SprintAttributionAnalysis.md` → `docs/reports/2026-03-30-sprint-attribution-analysis.md`
- `SWEPO_REVIEW_REPORT.md` → `docs/reports/2026-03-30-swepo-review-report.md`
- `docs/GEBRUIKERSHANDLEIDING.md` → `docs/architecture/gebruikershandleiding.md`

## 4. Files archived
- `docs/archive/code-quality/work-completed-2026-01-30.md`
- `docs/analysis/cache-insights-and-validation-report.md`
- `docs/archive/legacy-revision-ingestion/odata-validator-vs-ingestion-report.md`
- `docs/archive/legacy-revision-ingestion/real-revision-tfsclient-pagination-review.md`
- `docs/archive/legacy-revision-ingestion/revision-ingestion-api-vs-validator-odata-divergence.md`
- `docs/archive/legacy-revision-ingestion/revision-ingestor-v2.md`
- `docs/analysis/sprint-trends-vs-revisions-report.md`

## 5. Updated references
- Updated `prompts/seniorswe_and_seniorpo_review` to write to `docs/reports/2026-03-30-swepo-review-report.md`.
- Updated `docs/README.md` to remove the missing `mock-data-rules.md` link.
- Updated active docs that still referenced moved paths:
  - `docs/analysis/state_sprint_delivery_audit.md`
  - `docs/analysis/workspace_hub_tile_analysis.md`
  - `docs/analysis/backlog_quality_domain_exploration.md`
  - `docs/analysis/filter-analysis-improved.md`
  - `docs/analysis/cdc-corrections.md`
  - `docs/analysis/final-cdc-integration.md`
  - `docs/analysis/progress-model.md`
  - `docs/analysis/snapshots.md`
  - `docs/analysis/insight-null-semantics-fix.md`
  - `docs/rules/validation-rules.md`
  - `docs/analysis/cdc_replay_fixture_validation.md`
  - `docs/analysis/screenshot-index-exploratory-testing.md`
- Updated document tests in `PoTool.Tests.Unit/Audits/*` from `docs/analyze/...` to `docs/analysis/...` where required.
- Updated one historical planning checklist and one mock-data code comment to remove references to the missing mock-data rules filename.

## 6. Prompt updates
- Confirmed: `prompts/seniorswe_and_seniorpo_review` now writes to `docs/reports/2026-03-30-swepo-review-report.md`.

## 7. Validation results
- **Build:** `dotnet build PoTool.sln --configuration Release --no-restore --nologo` ✅ passed
- **Test:** `dotnet test --configuration Release --nologo -v minimal` ✅ passed
- **Broken local markdown link sweep:** ✅ passed
- **Old-path reference sweep outside relic-audit history docs:** ✅ passed

## 8. Remaining issues
- `docs/analysis/relic-audit/repository-relic-audit.md` intentionally still contains pre-reorganization paths as historical audit evidence and move mappings.
- `docs/archive/experiments/.gitkeep` was added only to preserve the required empty folder in git.
- No behavioral code changes were made; the only non-doc source edits were path/comment updates needed to keep prompts, tests, and repository references consistent with the new documentation structure.
