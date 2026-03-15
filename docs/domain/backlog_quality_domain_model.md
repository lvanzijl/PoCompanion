# Backlog Quality Domain Model

Status: Draft  
Purpose: Define the canonical current-state domain semantics for backlog validation and backlog readiness before any CDC v2 extraction or implementation refactor.

This document is the authoritative domain definition for the backlog quality slice.

If current implementation differs from this document, the implementation is a deviation until explicitly reviewed.

---

## Domain Scope

The backlog quality slice answers one question:

**What is the current quality and planning maturity of the active product backlog?**

It covers:

- structural integrity of the current backlog tree
- refinement readiness of backlog containers
- implementation readiness of implementable backlog work
- maturity scoring for Epics, Features, and PBIs
- canonical rule metadata, rule families, and rule ownership

It operates on **current snapshot data**, not historical update streams.

Canonical inputs:

- an already-loaded work item graph
- current parent-child relations
- current work item type
- current description
- current effort
- current canonical state classification

Canonical scope units:

- Epic
- Feature
- PBI

Additional hierarchy levels remain relevant only where needed for structural consistency:

- Bugs and Tasks may exist in the tree
- Bugs and Tasks do **not** participate in backlog readiness scoring
- Bugs and Tasks do **not** define implementation readiness in this slice unless a future rule explicitly adds them

State semantics follow the canonical state rules:

- snapshot state is authoritative for current-state questions
- removed items are outside active backlog scope
- done items are no longer candidates for planning, but done descendants may still contribute completed maturity to active parent scope

---

## Rule Families

The canonical backlog quality slice has four families.

1. Structural Integrity
2. Refinement Readiness
3. Implementation Readiness
4. Backlog Readiness Scoring

### Structural Integrity

Purpose:

- detect logically inconsistent parent-child state combinations
- preserve tree correctness
- surface maintenance issues without blocking maturity scoring

Canonical semantics:

- always evaluated
- never suppressed
- never changes readiness scores directly
- may coexist with any other family

Structural integrity is a **quality warning**, not a backlog maturity formula.

### Refinement Readiness

Purpose:

- determine whether higher-level backlog intent exists
- define whether scope is ready to be refined further

Canonical semantics:

- applies to Epics and Features
- evaluated before implementation-readiness findings are reported
- blocks lower-level implementation-readiness findings for the same blocked subtree
- participates directly in readiness scoring for the item being scored

Refinement readiness is about **intent and decomposition context**, not implementation detail.

### Implementation Readiness

Purpose:

- determine whether scoped work is ready to enter implementation planning
- identify missing implementable detail after refinement intent exists

Canonical semantics:

- applies to Features and PBIs
- reported only for scope whose ancestor refinement-readiness gates pass
- contributes directly to PBI, Feature, and Epic readiness scoring through descendant maturity

Implementation readiness is about **whether work can actually be planned and started**, not whether the tree is structurally valid.

### Backlog Readiness Scoring

Purpose:

- quantify backlog maturity without replacing rule findings
- show partial progress toward ready scope
- support ownership and prioritization views

Canonical semantics:

- scoring is a parallel domain projection, not a post-processing step over validation output
- scoring uses the same canonical rule semantics as validation
- structural integrity findings never change a score
- validation suppression never removes lower-level maturity scores

This means:

- an item may have warnings and still have a meaningful score
- a parent may score `0` while its children still show non-zero maturity

Example:

- Epic description missing
- Features exist and have mature PBIs

Canonical result:

- validation finding: `RR-1`
- Epic score: `0`
- child Feature and PBI scores: still computed from their own semantics

The Epic is not refinement-ready, but the decomposition work is still visible.

---

## Canonical Rule Definitions

### Structural Integrity

| Rule ID | Rule | Inputs | Output | Responsible party | Canonical owner |
| --- | --- | --- | --- | --- | --- |
| `SI-1` | Parent in `Done` with any descendant not in `Done` or `Removed` is invalid. | Current hierarchy, canonical state classification | `BacklogIntegrityFinding` | Product Owner / Process | BacklogValidationService |
| `SI-2` | Parent in `Removed` with any descendant not in `Done` or `Removed` is invalid. | Current hierarchy, canonical state classification | `BacklogIntegrityFinding` | Product Owner / Process | BacklogValidationService |
| `SI-3` | Parent in `New` with any descendant in progress or done is invalid. | Current hierarchy, canonical state classification | `BacklogIntegrityFinding` | Product Owner / Process | BacklogValidationService |

Structural rules are recursive tree rules.

They produce findings only.

They do **not**:

- block refinement scoring
- block implementation scoring
- change ownership state

### Refinement Readiness

| Rule ID | Rule | Inputs | Output | Responsible party | Canonical owner |
| --- | --- | --- | --- | --- | --- |
| `RR-1` | Epic description must be present and at least 10 characters. | Epic snapshot | `ValidationRuleResult` + Epic readiness state | Product Owner | BacklogValidationService |
| `RR-2` | Feature description must be present and at least 10 characters. | Feature snapshot | `ValidationRuleResult` + Feature readiness state | Product Owner | BacklogValidationService |
| `RR-3` | Epic must have at least one active Feature child. | Epic snapshot + child relations | `ValidationRuleResult` + Epic readiness state | Product Owner | BacklogValidationService |

Refinement-readiness thresholds are domain semantics:

- minimum description length = `10`
- empty and too-short descriptions are the same canonical failure

If a subtree has refinement-readiness violations:

- the subtree is not ready for refinement
- implementation-readiness findings under that blocked scope are suppressed for reporting
- backlog readiness scores for descendants may still be computed

### Implementation Readiness

The canonical family name is **Implementation Readiness**.

For compatibility, the existing rule IDs remain `RC-*`.

| Rule ID | Canonical semantic tag | Rule | Inputs | Output | Responsible party | Canonical owner |
| --- | --- | --- | --- | --- | --- | --- |
| `RC-1` | `MissingPbiDescription` | PBI description must be present. | PBI snapshot | `ValidationRuleResult` + PBI readiness state | Development Team | BacklogValidationService |
| `RC-2` | `MissingEffort` | PBI effort must be present and greater than zero. | PBI snapshot | `ValidationRuleResult` + PBI readiness state | Development Team | BacklogValidationService |
| `RC-3` | `MissingPbiChildren` | Feature must have at least one active PBI child. | Feature snapshot + child relations | `ValidationRuleResult` + Feature readiness state | Development Team | BacklogValidationService |

Canonical decisions:

- `RC-2` remains the canonical rule ID
- `MissingEffort` is the canonical semantic tag for that rule
- `EFF` is **not** a canonical domain category; it is an adapter/UI grouping alias only
- implementation-readiness findings are suppressed beneath refinement-readiness blockers

Parent effort semantics:

- Epic effort is not part of refinement readiness
- Feature effort is not part of implementation readiness
- missing Epic or Feature effort may remain a separate diagnostic concern outside this slice, but it is not a canonical backlog-readiness rule

This is intentional.

Backlog quality in this slice is about:

- whether scope is refined enough
- whether PBIs are implementable

It is not about enforcing effort completeness on every hierarchy level.

### Backlog Readiness Scoring

Scoring formulas are canonical domain rules, but they are **not** validation-rule IDs.

They produce `BacklogReadinessScore` outputs rather than validation findings.

#### PBI scoring

Inputs:

- current PBI snapshot
- current description
- current effort

Rules:

1. Missing description → score `0`
2. Description present, effort missing or `<= 0` → score `75`
3. Description present and effort `> 0` → score `100`

Interpretation:

- `0` = not implementable because intent is missing
- `75` = intent exists but estimate is still needed
- `100` = ready for implementation

#### Feature scoring

Inputs:

- current Feature snapshot
- active direct PBI children
- done direct PBI children

Rules:

1. Missing Feature description → score `0`, owner state `PO`
2. Description present, no active or done PBI children → score `25`, owner state `Team`
3. Otherwise → score = rounded average of direct child PBI scores, with done PBIs contributing `100`

Owner-state semantics:

- `PO` only when the Feature itself fails refinement-readiness
- `Team` when the Feature passes refinement-readiness but is not fully implementable
- `Ready` only when score is `100`

#### Epic scoring

Inputs:

- current Epic snapshot
- active direct Feature children
- done direct Feature children

Rules:

1. Missing Epic description → score `0`
2. Description present, no active or done Feature children → score `30`
3. Otherwise → score = rounded average of direct child Feature scores, with done Features contributing `100`

Epic effort does not influence score.

Done and removed semantics:

- removed descendants are excluded from active backlog scoring
- done descendants contribute `100` to parent maturity
- done items may be hidden by adapters, but their maturity still counts

---

## Domain Outputs

The backlog quality domain must expose domain outputs that are independent of transport DTOs and UI models.

### ValidationRuleResult

Represents one rule finding.

Minimum canonical contents:

- rule metadata
- affected work item ID
- affected work item type
- human-readable message
- responsible party
- consequence/finding class

### BacklogIntegrityFinding

Specialized structural-integrity output.

Purpose:

- preserve explicit distinction between structural maintenance problems and readiness problems

Minimum canonical contents:

- rule ID
- affected item ID
- ancestor/descendant context sufficient to explain the conflict

### RefinementReadinessState

Represents the discrete refinement-readiness outcome for a scope node.

Minimum canonical contents:

- work item ID
- is ready / not ready
- blocking findings
- whether implementation-readiness reporting beneath the node is suppressed

### ImplementationReadinessState

Represents the discrete implementation-readiness outcome for implementable scope.

Minimum canonical contents:

- work item ID
- is ready / not ready
- blocking findings
- missing-effort status

Canonical derivation:

- a PBI is ready for implementation iff its readiness score is `100`
- a Feature is fully ready iff its score is `100`
- an Epic is fully ready iff its score is `100`

### BacklogReadinessScore

Represents maturity as a numeric projection.

Minimum canonical contents:

- work item ID
- work item type
- numeric score
- score reason
- owner state where applicable

The score is not a validation message.

It is a planning/maturity indicator.

### RuleMetadata

Rule metadata must be a canonical domain concept.

Minimum canonical contents:

- rule ID
- family
- semantic tag
- description
- responsible party
- consequence class
- applicable work item types

Adapters may format this metadata differently, but they must not redefine it.

---

## Domain Services

### BacklogValidationService

Owns:

- Structural Integrity rules
- Refinement Readiness rules
- Implementation Readiness rules
- reporting order and suppression behavior
- canonical rule metadata exposure

Inputs:

- already-loaded work item graph
- canonical state classification lookup

Outputs:

- validation findings
- refinement-readiness states
- implementation-readiness states

### BacklogReadinessService

Owns:

- PBI, Feature, and Epic readiness scoring
- Feature owner-state derivation
- treatment of done and removed descendants in scoring

Inputs:

- already-loaded work item graph
- canonical state classification lookup

Outputs:

- `BacklogReadinessScore` values for PBIs, Features, and Epics

This service does **not** consume UI DTOs and does **not** depend on API handlers.

### ImplementationReadinessService

Owns:

- thresholded ready/not-ready interpretations for implementable scope
- derivation of binary readiness from canonical scoring and rule semantics

Outputs:

- `ImplementationReadinessState`
- ready-scope summaries for downstream adapters

This service exists because binary “ready” and numeric “maturity” serve different consumers and should not be conflated in transport contracts.

### Rule Catalog / Metadata Provider

The domain slice must own one canonical rule metadata source.

It may be implemented as:

- rule objects exposing metadata directly, or
- a domain rule catalog

Canonical requirement:

- metadata originates in the domain slice once
- adapters consume it
- adapters do not infer family from string prefixes as a source of truth

---

## Domain Boundaries

### Inside the backlog quality domain slice

- validation rule definitions
- validation execution order
- suppression semantics
- readiness scoring formulas
- owner-state derivation
- binary readiness derivation
- canonical rule metadata
- canonical rule family definitions

### Outside the backlog quality domain slice

- API handlers
- controller orchestration
- queue and triage page composition
- fix-session workflows
- UI display labels, icons, colors, and tile emphasis
- transport DTO shape
- product-loading orchestration
- multi-iteration backlog health dashboards
- string-based blocked-item heuristics
- string-based in-progress heuristics
- any aggregate health score formula that mixes canonical findings with dashboard-only heuristics

Specific consequence:

- `BacklogHealthCalculator` and health dashboard heuristics remain outside the initial canonical backlog quality slice
- those consumers may use domain outputs, but they do not define the domain

---

## Resolved Contradictions

### 1. Validation vs readiness scoring

Resolved decision:

- readiness scoring is **not blocked** by structural-integrity findings
- readiness scoring is **not derived from validator suppression**
- readiness scoring **does share the same canonical semantics** as refinement and implementation readiness

Therefore:

- Structural Integrity produces warnings only
- Refinement Readiness gates the scored item itself
- descendant maturity may still be computed even when a parent fails refinement readiness

Canonical example:

- Epic missing description + mature Features
- output = `RR-1` finding + Epic score `0` + Feature scores still computed

### 2. Missing effort (`RC-2`)

Resolved decision:

- canonical rule ID = `RC-2`
- canonical family = `Implementation Readiness`
- canonical semantic tag = `MissingEffort`
- canonical scope = PBI only
- canonical responsible party = Development Team
- canonical adapter alias `EFF` is outside the domain

Effects:

- missing PBI effort lowers PBI score from `100` to `75`
- missing PBI effort blocks binary implementation readiness for that PBI
- Feature and Epic scores inherit the effect only through descendant averaging
- missing Epic or Feature effort is not a canonical readiness rule in this slice

### 3. Binary readiness vs maturity scoring

Resolved decision:

- both models remain
- numeric scoring is the maturity model
- binary readiness is the thresholded domain state

Canonical relationship:

- score explains *how far along* backlog maturity is
- binary readiness answers *is this scope ready now*
- binary readiness is derived from canonical scoring/rule semantics, not maintained as a competing rule system

### 4. Rule categorization ownership

Resolved decision:

- rule family and metadata are owned by the domain slice
- adapters may map canonical rules to UI groupings such as `SI`, `RR`, `RC`, or `EFF`
- those UI groupings are presentation concerns, not canonical semantics

Consequences:

- rule IDs and metadata must not be re-authored in Shared, API, and Client separately
- prefix-based inference may exist temporarily in adapters, but it is not the future source of truth

### 5. Current implementation deviations to be corrected later

Current implementation differs from the canonical model in several places:

- `RC-2` is currently treated as a separate always-evaluated `MissingEffort` category
- the executable `PbiEffortEmptyRule` currently evaluates Epic and Feature effort as well as PBI effort
- queue and triage handlers special-case `RC-2` into `EFF`
- rule descriptions and category labels are duplicated outside rule definitions
- some handlers combine canonical findings with string-based health heuristics

These are extraction follow-ups, not reasons to change the canonical domain definition.

---

## Open Questions

The following questions remain intentionally outside the canonical backlog quality slice for now:

1. Should the application continue to expose `EFF` as a separate triage tile once adapters consume canonical rule metadata directly?
2. Should missing Epic or Feature effort become a separate future diagnostic family outside readiness, or be removed entirely from backlog-health reporting?
3. Should the health workspace eventually define one canonical health score formula, or continue to treat health as an adapter-level dashboard composition over multiple signals?
4. What is the migration plan for replacing legacy `HierarchicalValidationResult` transport semantics with domain-first outputs without breaking existing pages?
