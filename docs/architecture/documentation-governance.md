# Documentation Governance

## 1. Purpose

This document defines the canonical placement, naming, lifecycle, and review rules for repository documentation.
Its purpose is to prevent documentation structure drift after the documentation reorganization and to make document intent obvious from location and filename alone.

These rules apply to all new markdown documents and to any moved or renamed existing markdown documents.

---

## 2. Canonical folder purposes

The folders below are the canonical documentation taxonomy.
A document must be placed according to its primary purpose, not according to convenience, author preference, or where similar files were historically left behind.

> `docs/rules` and `docs/plans` are canonical target folders even if they are not yet populated in the current tree.

### 2.1 `docs/architecture`

**Belongs here**
- Durable architecture references
- Stable design decisions and invariants
- Cross-layer boundaries and contracts
- Canonical technical reference documents that should remain valid across multiple PRs
- Architecture-level decision records and enduring system behavior explanations

**Does NOT belong here**
- One-off investigations
- Time-boxed debugging notes
- Temporary implementation plans
- Historical summaries that are only retained for record
- User-facing documentation
- Obsolete or misleading material preserved only for traceability

### 2.2 `docs/analysis`

**Belongs here**
- Exploratory investigations
- Comparative analyses
- Diagnostic write-ups used to understand a problem before or during implementation
- Working analysis documents that may later become reports or be retired
- Time-bound engineering investigations whose primary value is reasoning, not governance

**Does NOT belong here**
- Final canonical architecture references
- User guides
- Closed implementation summaries whose work is complete and no longer active analysis
- Obsolete documents kept only for record
- Formal policy/rule documents

### 2.3 `docs/reports`

**Belongs here**
- Current, still-relevant engineering reports
- Verified summary documents that describe current repository behavior or current findings
- Reports that are no longer exploratory and are intended to be read as an actionable current-state summary

**Does NOT belong here**
- Raw exploratory notes
- User manuals
- Long-term architecture source-of-truth documents
- Superseded or misleading reports
- Historical artifacts whose value is mainly archival

### 2.4 `docs/audits`

**Belongs here**
- Structured audits against explicit criteria
- Repository inspections intended to verify compliance, correctness, or migration status
- Current audit documents that should still be read as valid checks against the present repository

**Does NOT belong here**
- Speculative analysis
- User documentation
- Forward-looking plans
- Historical implementation summaries
- Stale audits that no longer reflect the current repository state

### 2.5 `docs/history`

**Belongs here**
- Historical implementation narratives worth retaining
- Completed work summaries that still provide useful context
- Dated follow-up records tied to a past change, milestone, or review cycle
- Historical material that is no longer canonical but is still useful for understanding how the repository evolved

**Does NOT belong here**
- Active source-of-truth architecture or rules
- Current user documentation
- Misleading obsolete documents that should only be preserved for record
- Working analysis documents still under active use

### 2.6 `docs/archive`

**Belongs here**
- Obsolete documents
- Superseded documents
- Misleading historical material that must be preserved but must not be read as current guidance
- Experiment residue and decommissioned design directions retained only for traceability

**Does NOT belong here**
- Active guidance
- Canonical architecture or rules
- Current reports or audits
- User documentation
- Anything that is still linked as the primary source of truth

### 2.7 `docs/user`

**Belongs here**
- User manuals
- User-facing operating instructions
- Onboarding/help content intended for product users or administrators
- End-user workflow guidance

**Does NOT belong here**
- Internal engineering analysis
- Architecture references
- Implementation plans
- Audits or code-review artifacts
- Historical engineering summaries

### 2.8 `docs/rules` (canonical target folder)

**Belongs here**
- Normative repository rules
- Governance documents
- Stable policies and checklists that define how future work must be done
- Cross-cutting documentation standards

**Does NOT belong here**
- Dated reports
- Temporary decisions
- User guides
- Historical artifacts
- Working analysis documents

### 2.9 `docs/plans` (canonical target folder)

**Belongs here**
- Approved forward-looking plans
- Migration plans
- Roadmaps
- Execution plans for work not yet complete
- Structured planning artifacts that describe intended future work

**Does NOT belong here**
- Final reports of completed work
- Root-cause analyses
- Architecture source-of-truth references
- User manuals
- Obsolete canceled plans that are retained only for record

---

## 3. Placement decision rule

A document must be placed by its **current primary purpose**, not by:
- where a related document already happens to exist
- where it was first created during implementation
- who wrote it
- whether the author considers it “temporary”

If a document appears to fit more than one folder, use this precedence:

1. **Rules** beat everything else when the document is normative.
2. **User** beats engineering folders when the primary audience is product users.
3. **Architecture** beats reports and analysis when the document is intended to remain canonical over time.
4. **Audits** beat reports when the document is a structured compliance/inspection artifact.
5. **Reports** beat analysis when findings are verified and intended as a current-state summary.
6. **History** beats archive when the document still provides useful historical context.
7. **Archive** is the sink for obsolete, superseded, or misleading material.

---

## 4. Naming conventions

### 4.1 File naming

All markdown filenames MUST use:
- lowercase
- kebab-case
- descriptive names

Examples:
- `documentation-governance.md`
- `validation-system-report.md`
- `sprint-attribution-analysis.md`
- `code-audit-report-2026-01-30.md`

The following are forbidden:
- uppercase or mixed-case filenames
- spaces in filenames
- generic filenames like `REPORT.md`, `SUMMARY.md`, or `NOTES.md`
- ambiguous filenames that do not reveal subject matter

### 4.2 When to include dates

Include a date in the filename when the document is intentionally time-bound, such as:
- historical implementation summaries
- point-in-time audit outcomes that should not masquerade as evergreen guidance
- retained completed-work artifacts in `docs/history`
- archived records where the date helps distinguish versions or phases

Use the format:
- `YYYY-MM-DD`

Examples:
- `final-summary-2026-01-30.md`
- `fixes-applied-2026-01-30.md`

### 4.3 When to avoid dates

Do **not** include dates in filenames when the document is intended to be the stable canonical reference, such as:
- architecture references
- governance/rules documents
- user manuals
- current reports that should be updated in place
- active plans that represent the current working plan rather than a historical snapshot

Examples:
- `documentation-governance.md`
- `validation-system-report.md`
- `gebruikershandleiding.md`

---

## 5. Lifecycle rules

Documents may change folders as their purpose changes.
Moving a document is required when its current folder no longer reflects what the document actually is.

Canonical lifecycle transitions:
- `analysis → reports`
- `reports → history`
- `history → archive`

### 5.1 `analysis` → `reports`

Move a document from `docs/analysis` to `docs/reports` when:
- the investigation is complete
- findings are verified
- the document is still intended to be read as current
- the document has become a present-tense summary rather than an exploratory working note

Do **not** move if the document is still exploratory or is mainly a reasoning scratchpad.

### 5.2 `reports` → `history`

Move a document from `docs/reports` to `docs/history` when:
- the report describes a completed change or past effort
- it is no longer the current source of truth
- it still has useful historical value
- reading it as current would be inaccurate, but its background remains useful

### 5.3 `history` → `archive`

Move a document from `docs/history` to `docs/archive` when:
- it is no longer useful as historical context for active work
- it would be misleading if read without strong historical framing
- it documents a removed, abandoned, or superseded approach
- the repository keeps it only for traceability or record retention

### 5.4 Direct moves to archive

A document may move directly to `docs/archive` from any folder when it is immediately recognized as:
- obsolete
- superseded
- misleading in current context
- residue from an abandoned experiment

---

## 6. Root rules

### 6.1 Repository root

Allowed in repository root:
- `README.md` only, as the repository entry point markdown file
- non-markdown project files that are part of normal repository structure

Forbidden in repository root:
- new markdown reports
- new markdown audits
- new markdown plans
- new markdown implementation summaries
- generic markdown artifacts produced by one-off work

**Hard rule:** no new markdown files may be added to repository root except a deliberate replacement or rename of the repository entry-point `README.md`.

### 6.2 `docs/` root

Allowed in `docs/` root:
- `docs/README.md`
- canonical top-level index/entry documents
- canonical cross-cutting rules and repository-wide reference documents that are intentionally stable at a top-level path
- explicit shared entry documents whose purpose spans multiple folders and is not analysis/report/history/user-specific

Forbidden in `docs/` root:
- one-off reports
- exploratory analysis
- historical summaries
- archived material
- user manuals
- ad hoc migration notes placed there for convenience

### 6.3 Everything else forbidden by default

If a new markdown document does not clearly qualify for repository root or `docs/` root under the rules above, it must be placed in a canonical subfolder.
Convenience placement is forbidden.

---

## 7. PR enforcement rules

Every PR that adds, moves, or renames markdown documents must verify the following.

### 7.1 Placement checklist

- Is the folder correct for the document’s primary purpose?
- Is the filename descriptive and kebab-case?
- Is the document time-bound? If yes, does it need a date in the filename?
- Is the document evergreen? If yes, is the filename undated?
- Is the document current, historical, or obsolete? Does the folder match that lifecycle state?
- Are any links or references affected by the move or creation?
- Does the PR accidentally create a mixed-purpose folder?

### 7.2 Hard PR rules

- No new markdown in repository root.
- No new markdown in root.
- No mixed-purpose folders.
- No generic markdown filenames.
- No creation of parallel folders for the same concept.
- No leaving a moved document referenced by its old path.

### 7.3 Mixed-purpose folder rule

A folder must not mix incompatible document purposes.
Examples of forbidden mixes:
- active reports together with obsolete archive-only documents
- user manuals mixed with internal engineering investigations
- evergreen rules mixed with dated one-off implementation summaries

---

## 8. Migration safety rules

When reorganizing documentation:

- Never delete historical documents if archive is the correct answer.
- Prefer move/rename over recreate so history is preserved.
- Always update links, prompt references, tests, and indexes affected by the move.
- Never leave a document in a more prominent location than its lifecycle state justifies.
- Never move a document into `archive` if active docs still depend on it as the canonical reference.
- If a document is misleading but must be retained, archive it rather than leaving it in an active folder.

---

## 9. Anti-patterns

### 9.1 Bad placement examples

**Bad:** a user manual in `docs/analysis`
- Why bad: audience and purpose are wrong; it hides user-facing content among engineering investigations.

**Bad:** a current audit in `docs/history`
- Why bad: it suggests the audit is only historical even though it is meant to describe present state.

**Bad:** a one-off debugging write-up in `docs/architecture`
- Why bad: it makes a temporary investigation look like a durable architectural invariant.

**Bad:** a completed implementation summary in repository root
- Why bad: root markdown should not be used as a dumping ground for one-off artifacts.

### 9.2 Misleading document examples

**Misleading:** generic filenames such as `REPORT.md`
- Why bad: the name gives no signal about content, age, or lifecycle state.

**Misleading:** obsolete experiment reports left in active `docs/reports`
- Why bad: they read as current even when they describe removed architecture.

**Misleading:** dated audit deliverables left beside evergreen rules without historical framing
- Why bad: readers cannot tell what is source-of-truth versus what is only historical evidence.

**Misleading:** duplicate folders such as `docs/Reports` and `docs/reports`
- Why bad: casing drift fragments discovery and creates uncertainty about the canonical home.

---

## 10. Folder hygiene rules

- One concept, one folder name.
- Folder names must be lowercase.
- New top-level documentation folders require explicit justification and should be rare.
- If a document family emerges repeatedly, create or use a canonical folder rather than leaving files in `docs/` root.
- If a folder becomes a grab-bag of unrelated documents, the folder structure must be corrected rather than tolerated.

---

## 11. Enforcement summary

The repository documentation model is:
- **rules** for governance
- **architecture** for durable technical truth
- **analysis** for investigation
- **reports** for current verified summaries
- **audits** for structured inspections
- **plans** for future work
- **history** for completed but still useful past context
- **archive** for obsolete retained record
- **user** for end-user guidance

Any new markdown document that does not clearly match one of those purposes is incorrectly scoped and must not be merged until its placement is made explicit.
