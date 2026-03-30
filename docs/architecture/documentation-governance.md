# Documentation Governance

## Purpose

This document defines the enforced `docs/` structure for the repository after Batch 4 structural cleanup.
Documents must be placed by current intent, not by historical habit.

## Canonical structure

Only the following markdown folders are canonical:

- `docs/rules/` — mirrored repository rules and governance notes
- `docs/architecture/` — stable technical and product-structure reference
- `docs/implementation/` — active plans, migration guidance, and forward-looking execution notes
- `docs/analysis/` — audits, diagnostics, investigations, and exploratory write-ups
- `docs/reports/` — dated task outputs and completed work summaries
- `docs/archive/` — deprecated or historical material that must not be read as active guidance

`docs/README.md` is the only markdown file allowed at the `docs/` root.

## Placement rules

### `docs/architecture/`

Belongs here:
- enduring design references
- stable models and boundaries
- documentation that should stay valid across multiple changes

Does not belong here:
- exploratory diagnostics
- dated implementation outputs
- deprecated historical notes

### `docs/implementation/`

Belongs here:
- active plans
- approved migration guidance
- forward-looking execution documents

Does not belong here:
- completed task outputs
- historical records kept only for traceability
- stable source-of-truth architecture

### `docs/analysis/`

Belongs here:
- audits
- diagnostics
- explorations
- comparative investigations
- historical analysis that still has engineering traceability value

Historical references are allowed here only when the file is clearly labeled.

### `docs/reports/`

Belongs here:
- task outputs
- implementation summaries
- dated result documents

Every report filename must use the form:

- `YYYY-MM-DD-kebab-case-name.md`

### `docs/archive/`

Belongs here:
- deprecated material
- superseded historical documents
- experiment residue retained only for traceability

Archive subfolders must remain single-purpose, traceability-only domains and must not contain active guidance.

### `docs/rules/`

Belongs here:
- mirrored rule documents
- documentation standards
- stable governance checklists

## Naming rules

- use lowercase filenames
- use kebab-case
- avoid generic names
- keep dates only on reports and other intentionally time-bound records

## Review rule

If a document becomes misleading in its current folder, either:

1. move it to the correct canonical folder,
2. rewrite it to match the folder intent, or
3. archive it.
