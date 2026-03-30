# Documentation Migration — Batch 4 (Structural Cleanup + Semantic Alignment)

## 1. Summary
- canonical docs structure enforced
- non-compliant markdown files moved
- active docs semantically aligned
- legacy revision-ingestion archive narrowed to related artifacts only

## 2. Structural cleanup
- moved root-level markdown files into canonical folders
- moved non-canonical folder contents into `docs/analysis/`, `docs/architecture/`, `docs/implementation/`, `docs/reports/`, and `docs/archive/`
- rewrote `docs/README.md` to document the enforced structure
- normalized report filenames in `docs/reports/` to dated `YYYY-MM-DD-*.md` names

## 3. Semantic alignment
- rewrote active governance guidance in `docs/architecture/documentation-governance.md` to match the Batch 4 canonical structure
- removed deprecated OData/validator historical noise from active architecture/implementation folders
- added historical-state notes to analysis documents that still discuss pre-Batch-3 residue

## 4. Archive cleanup
- kept only OData/validator legacy-ingestion artifacts in `docs/archive/revision-ingestion/`
- moved unrelated boundary/cache/sprint-trend documents out of that archive subtree into active canonical folders

## 5. Test and reference updates
- updated document-audit tests and path assertions to the new folder layout
- updated code/doc comments that referenced moved report or architecture paths

## 6. Residual references
- historical path references remain inside some analysis and dated report documents where they are part of the retained audit trail
- `former docs/archive/experiments placeholder/` and `docs/archive/code-quality/` remain as non-active historical archives outside the legacy revision-ingestion subtree

## 7. Risks / uncertainties
- the strict Batch 4 folder model compresses several former categories into broader buckets, so a few documents now live in the closest canonical folder rather than a purpose-built category
- some older analysis documents still discuss previous structure states intentionally; they were kept with historical notes instead of being rewritten into current-state guidance

## 8. Next steps
- validate any remaining document cross-links that rely on historical paths
- continue report naming cleanup if additional dated task outputs are added later
