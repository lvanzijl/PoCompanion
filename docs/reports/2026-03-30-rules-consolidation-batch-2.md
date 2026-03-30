# Batch 2 Rules Consolidation Report

## Summary

- consolidated repository rule authority into `.github/copilot-instructions.md`
- moved rule and rule-like documents into `docs/rules/` with lowercase kebab-case names
- updated repository references to the new `docs/rules/` paths
- converted every file in `docs/rules/` into the required non-authoritative mirror template

## Files relocated into docs/rules

- `docs/ARCHITECTURE_RULES.md` → `docs/rules/architecture-rules.md`
- `docs/PROCESS_RULES.md` → `docs/rules/process-rules.md`
- `docs/UI_RULES.md` → `docs/rules/ui-rules.md`
- `docs/UI_LOADING_RULES.md` → `docs/rules/ui-loading-rules.md`
- `docs/TFS_INTEGRATION_RULES.md` → `docs/rules/tfs-integration-rules.md`
- `docs/EF_RULES.md` → `docs/rules/ef-rules.md`
- `docs/Fluent_UI_compat_rules.md` → `docs/rules/fluent-ui-compat-rules.md`
- `docs/COPILOT_ARCHITECTURE_CONTRACT.md` → `docs/rules/copilot-architecture-contract.md`
- `docs/domain/ui-semantic-rules.md` → `docs/rules/ui-semantic-rules.md`
- `docs/analysis/validation-rules.md` → `docs/rules/validation-rules.md`
- `docs/domain/rules/hierarchy-rules.md` → `docs/rules/hierarchy-rules.md`
- `docs/domain/rules/estimation-rules.md` → `docs/rules/estimation-rules.md`
- `docs/domain/rules/state-rules.md` → `docs/rules/state-rules.md`
- `docs/domain/rules/sprint-rules.md` → `docs/rules/sprint-rules.md`
- `docs/domain/rules/propagation-rules.md` → `docs/rules/propagation-rules.md`
- `docs/domain/rules/metrics-rules.md` → `docs/rules/metrics-rules.md`
- `docs/domain/rules/source-rules.md` → `docs/rules/source-rules.md`

## Authoritative consolidation

The authoritative rule set now lives in `.github/copilot-instructions.md` and includes:

- architecture and layering rules
- EF Core concurrency and SQLite timestamp rules
- TFS integration and verification rules
- UI, UX, density, and loading rules
- validation, semantics, and domain analytics rules
- process, review, release-note, testing, and repository hygiene rules

## Mirror conversion

Each file in `docs/rules/` was rewritten to the required mirror template exactly as instructed by the task clarification.

## Reference alignment

Repository references to the relocated rule documents were updated from legacy `docs/...` paths to their `docs/rules/...` equivalents so links remain structurally aligned with the new location.
