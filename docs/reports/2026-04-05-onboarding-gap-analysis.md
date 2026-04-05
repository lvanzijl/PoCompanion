# Onboarding Gap Analysis

Source reports used as the only factual basis:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`

## 1. Current vs Desired Overview

| Requirement | Current behavior | Desired behavior |
|---|---|---|
| 1. Separate TFS configuration from import/export | Step 1 combines TFS URL/project setup, live verification, progress reporting, and configuration import in one screen. | TFS configuration and configuration import/export are independent flows; completing one does not implicitly execute or conclude the other. |
| 2. No explicit save per step | Step 1 requires explicit save before `Next`; steps 2-5 require explicit save for persistence but not for progression. | Step progression either persists required state automatically or is itself the atomic persistence action; no step requires a separate save button to become valid for progression. |
| 3. Navigation must not drop input | Steps 2-5 store unsaved input only in component state and can close or complete without persisting it. | No navigation action may discard user-entered data without either persisting it or explicitly blocking/interrupting navigation. |
| 4. Single vs multi-item behavior explicit | Repository selection is visibly multi-select; product backlog root input is singular while the underlying model is list-based; in-memory vs persisted state is not explicit. | Every selectable field declares whether it accepts one item or multiple items, and the same rule applies consistently through selection, confirmation, and persistence. |
| 5. TFS selections must show confirmation details | Project, team, and repository choices have weak or incomplete confirmation after selection. | After every TFS-backed selection, the system shows enough retrieved details to verify the selected entity before completion or persistence. |
| 6. Work item selection must support lookup/search/filtering | Product creation uses a raw numeric work item ID; no lookup, no search, no type filtering, no title/type confirmation. | Work items are selected through a retrieval flow that supports lookup, search, type filtering, and post-selection confirmation details. |
| 7. Low cognitive load / single responsibility per step | Step 1 combines multiple concerns; step 4 combines creation and linking; step 5 combines live discovery and persistence. | Each onboarding step handles one user goal and does not combine unrelated setup, import, linking, or persistence responsibilities. |
| 8. Clear defaults and progressive disclosure | Step 4 has one useful default (derived area path), but step 1 exposes full complexity at once and uses a static summary. | Defaults are applied where possible, and non-essential detail appears only after prerequisite choices are made or when required to continue. |
| 9. Reflect existing system state | The wizard preloads TFS config only; reruns do not surface existing profiles, products, teams, or repositories; some steps depend only on current-session IDs. | Onboarding uses persisted system state as the authoritative baseline and distinguishes existing state from newly entered or unsaved state. |
| 10. Completion must reflect actual persisted state | Team link failure and partial repository failures can still display success; host routing ignores dialog outcome; completion can occur with unsaved data. | Success, completion, and post-onboarding routing reflect the actual persisted outcome of the flow and never report success for partial or failed required actions. |

## 2. Detailed Gap Analysis

### Requirement 1 — TFS configuration and import/export must be separate flows
- **Current**
  - Step 1 mixes saved-config preload, live project discovery, save/test/verify orchestration, and configuration import.
  - Successful import can complete onboarding immediately.
- **Desired**
  - The TFS configuration flow and the import/export flow are independent entry points.
  - Finishing import does not implicitly finish the configuration flow unless imported state is separately confirmed as complete.
- **Gap**
  - The current first step treats import as an alternate path inside connection setup instead of a separate flow.
  - Import outcome is allowed to terminate onboarding without a distinct completion decision for the onboarding flow.
- **Evidence**
  - Audit: step 1 purpose/actions/persistence and mixed responsibilities. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:67-88,268-295,339-341`
  - UX rating: expectation marked fully violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:94-98`

### Requirement 2 — No explicit "Save" requirement per step
- **Current**
  - Step 1 blocks `Next` until explicit save/verify succeeds.
  - Steps 2-5 allow `Next`/`Get Started` without save, but entered data remains unpersisted unless `Save*` is clicked.
- **Desired**
  - Advancing from a step must either persist the step’s required state automatically or constitute the only persistence trigger for that step.
  - The user must not have to interpret separate “save” and “advance” contracts within the same onboarding flow.
- **Gap**
  - The flow currently uses two incompatible progression models: save-gated progression on step 1 and non-persisting progression on steps 2-5.
  - The system cannot be tested as having a single progression contract because step semantics differ by step.
- **Evidence**
  - Audit: save-vs-next analysis. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-188`
  - UX rating: requirement marked fully violated and root cause identified. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:100-104,148-154`

### Requirement 3 — Navigation must never silently drop user input
- **Current**
  - Unsaved inputs on steps 2-5 exist only in component state.
  - `Skip Wizard` and `Get Started` close immediately without unsaved-change protection.
  - A reproducible failure exists where onboarding completes although entered step data was never persisted.
- **Desired**
  - Every navigation action must end in exactly one of two states: entered data is persisted, or navigation is interrupted before data is lost.
  - Completion and skip cannot discard user-entered unsaved data without an explicit state transition that preserves or rejects it.
- **Gap**
  - The current flow allows both intra-flow progression and flow completion while leaving user-entered state transient and disposable.
  - There is no binary protection rule preventing close/finish with unsaved data.
- **Evidence**
  - Audit: risk of data loss and failure scenario 1. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:190-194,311-313`
  - UX rating: requirement marked fully violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:106-111`

### Requirement 4 — Multi-item vs single-item behavior must be explicit and consistent
- **Current**
  - Repository selection is multi-select and visually expressed with chips and count.
  - Product setup accepts one backlog root in the UI while the underlying product model supports a list.
  - Repository selections are not explicitly described as in-memory until save.
- **Desired**
  - Every onboarding entity selection declares cardinality before input begins.
  - If a domain model supports multiple items, the onboarding contract either exposes multiple items or explicitly constrains the flow to one and states that constraint.
  - Selection cardinality and persistence state must be described consistently.
- **Gap**
  - Product root selection has a hidden cardinality mismatch between screen contract and model contract.
  - Repository selection exposes plural selection but does not expose its persistence boundary with the same clarity.
- **Evidence**
  - Audit: multi-item handling clarity. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:196-204`
  - UX rating: requirement marked partially violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:113-118`

### Requirement 5 — TFS selections must show confirmation details after selection
- **Current**
  - Project selection has no post-selection confirmation panel.
  - Team selection shows derived area path only, not stronger team/project confirmation.
  - Repository selection shows names only, not IDs or persisted-state confirmation.
- **Desired**
  - After a TFS-backed item is selected, the system must fetch and display confirmation fields sufficient to verify that the intended item was selected.
  - Confirmation must exist before the user completes the associated step or onboarding.
- **Gap**
  - Current confirmation is either absent or too narrow to verify selected entities reliably.
  - The system cannot be tested for post-selection verification because most TFS selections stop at raw selected labels.
- **Evidence**
  - Audit: where user cannot verify selections. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:232-242`
  - UX rating: requirement marked partially violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:120-125`

### Requirement 6 — Work item selection must support lookup, search, and type filtering
- **Current**
  - Product creation uses a numeric ID field.
  - The wizard does not call any work-item lookup/validation endpoint and shows no title, type, or existence confirmation.
- **Desired**
  - Work item selection must allow either direct lookup or search.
  - Search results must support filtering by work-item type.
  - After selection, the system must display at least fetched identity details for the selected item before persistence or step completion.
- **Gap**
  - The current product step has no selection model at all beyond numeric entry.
  - Lookup, search, filtering, and confirmation are absent, so the requirement is not partially missing; it is missing end-to-end.
- **Evidence**
  - Audit: missing validation and verification for work items. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-227,235-236,347-349`
  - UX rating: requirement marked not implemented. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:127-131`

### Requirement 7 — Screens must have low cognitive load (single responsibility per step)
- **Current**
  - Step 1 is the densest screen and mixes multiple concerns.
  - Step 4 combines team creation and product linking.
  - Step 5 combines live repository discovery and local repository persistence.
- **Desired**
  - Each step must map to one user task and one completion meaning.
  - A step must not combine unrelated transport, import, verification, linking, and persistence concerns.
- **Gap**
  - Current step boundaries are defined by implementation grouping rather than by a single user responsibility.
  - The current flow cannot satisfy a binary “one step = one responsibility” rule.
- **Evidence**
  - Audit: mixed responsibilities and cognitive overload points. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-295`
  - UX rating: requirement marked fully violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:133-137`

### Requirement 8 — Clear defaults and progressive disclosure
- **Current**
  - Step 4 derives area path from the selected team.
  - Step 1 exposes configuration, verification, progress, and import together from the start.
  - Step 1 also keeps a static summary that does not reflect current entered values.
- **Desired**
  - Defaults must populate fields only when they reduce input and remain visible as defaults.
  - Additional detail must appear only after prerequisite information exists or when required for the next decision.
  - Summaries must reflect actual current state rather than static placeholders.
- **Gap**
  - One local default exists, but the flow does not enforce progressive disclosure.
  - Step 1 front-loads complexity and fails to summarize the current working state.
- **Evidence**
  - Audit: step 4 default, step 1 overload, step 1 static summary. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:128-149,284-301`
  - UX rating: requirement marked partially violated. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:139-144`

### Requirement 9 — Onboarding must reflect existing system state
- **Current**
  - Only TFS config is preloaded.
  - Existing profiles, products, teams, and repositories are not loaded for review or reuse.
  - Step 5 and part of step 4 depend on IDs created in the current session rather than existing persisted entities.
  - Rerunning onboarding can create duplicates.
- **Desired**
  - Onboarding must read persisted configuration state for all onboarding entities that affect later steps.
  - Each step must distinguish existing persisted state, newly persisted state, and unsaved in-session state.
  - Later steps must be able to operate on already-persisted entities, not only session-created IDs.
- **Gap**
  - The current flow uses session-local state as the controlling context for team linking and repository persistence.
  - Rerun scenarios cannot be interpreted reliably because the system does not expose what already exists.
- **Evidence**
  - Audit: not validated / not loaded, confusing reruns, failure scenarios 3-5. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-230,297-301,319-329`
  - UX rating: root cause on transient session artifacts. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:171-177`

### Requirement 10 — Completion must reflect actual persisted state
- **Current**
  - Team-link failure can still render inline success.
  - Partial repository failure can still render inline success.
  - `Get Started` can complete onboarding with unsaved data.
  - The host page ignores whether the dialog ended by skip or completion.
- **Desired**
  - A success state may be shown only when the required persisted outcome for that action has completed successfully.
  - Completion may occur only when the flow state being declared complete matches persisted onboarding state.
  - Post-completion routing must depend on actual flow outcome.
- **Gap**
  - Current success signals describe attempted action flow, not verified persisted outcome.
  - Current completion status and routing are disconnected from persisted results and dialog outcome.
- **Evidence**
  - Audit: silent failures/unclear states, completion routing, failure scenarios 1, 6, 7, 9. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:173-176,245-248,311-345`
  - UX rating: dominant failure pattern and issue classification. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md:69-90`

## 3. Interaction Rules

### Navigation rules
1. A step transition is valid only if all required state for the current step has either been persisted or has been explicitly marked unnecessary for this onboarding run.
2. `Next`, `Previous`, `Skip`, and final completion actions must not discard unsaved user-entered state silently.
3. If unsaved state exists, completion and skip are invalid until the state is either persisted or explicitly resolved.
4. Post-dialog navigation must differ between completed and skipped outcomes.

### Persistence rules
1. No onboarding step may require a separate explicit save action in addition to the progression action.
2. A step is considered complete only when its required persisted state exists.
3. A persistence operation must not report success if any required sub-operation for that step failed.
4. Persisted onboarding state must be authoritative over transient in-session state when determining downstream step eligibility.

### Selection rules
1. Every selection field must declare whether it accepts exactly one item or multiple items.
2. For TFS-backed selections, the system must present retrieved confirmation details after selection and before step completion.
3. Work-item selection must support direct lookup or search, and search results must support filtering by type.
4. The same selection contract must apply on first-run and rerun flows.

### Validation rules
1. Required identifiers for TFS-backed entities must be validated against retrievable TFS data before the associated step is considered complete.
2. Validation must produce an explicit non-success state when lookup, load, or linkage fails.
3. Selection-based steps must not rely on raw user-entered identifiers alone when the requirement calls for lookupable TFS entities.
4. Validation status shown to the user must describe resulting state, not only attempted action execution.

### State rules
1. Onboarding must distinguish persisted existing state, newly persisted state, and unsaved in-session state for every onboarding entity.
2. Rerunning onboarding must surface existing persisted entities that affect the current step.
3. Later steps must be able to bind to existing persisted entities, not only entities created during the current wizard session.
4. A step may not allow actions that are guaranteed to fail solely because required context exists only in transient session state.

### Completion rules
1. Onboarding completion is valid only when the state being declared complete matches persisted onboarding state.
2. Import completion is not equivalent to onboarding completion unless the imported persisted state satisfies the onboarding completion contract.
3. Completion status must not be shown when required actions partially succeeded or failed.
4. Final routing must be driven by actual onboarding outcome, not merely by dialog closure.

## 4. Structural Blockers

1. **Step responsibility coupling**
   - The reports show that step 1 combines configuration, live discovery, verification, and import; step 4 combines team creation and product linking; step 5 combines discovery and persistence.
   - This coupling prevents isolated local correction of progression and completion rules because one step currently represents multiple state transitions.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-280,284-295`

2. **Transient session state as control state**
   - The flow uses `_createdProductId`, `_createdTeamId`, and `_configCompleted` from the current wizard session to decide whether downstream actions are allowed or possible.
   - This prevents simple correction of rerun behavior because current persisted state is not the operative state model.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:143-146,165-167,323-329`

3. **Missing work-item capability in the onboarding contract**
   - The reports show no lookup/search/filter/confirmation path for work-item selection.
   - This blocks compliance with the work-item requirement because the current product step has only a raw numeric input contract.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-227,235-236,347-349`

4. **Outcome reporting is disconnected from persisted result state**
   - Success rendering can remain positive despite failed team linking or partial repository persistence, and final host navigation ignores dialog outcome.
   - This blocks reliable completion semantics because the current flow does not use persisted result truth as the only success source.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:173-176,245-248,331-345`

5. **Implementation responsibility leakage into the wizard flow**
   - The audit states that the wizard itself mixes UI state, raw HTTP orchestration, SignalR wiring, and per-step persistence behavior.
   - This is a blocker because interaction rules are currently entangled with transport-level behavior inside one component boundary.
   - Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:274-276,295`

## 5. Risk Assessment

| Gap | Risk if not fixed | Why |
|---|---|---|
| TFS configuration and import/export remain combined | Usability, Maintainability | One step continues to carry multiple goals and completion meanings, preserving overload and ambiguous flow ownership. |
| Save-vs-progress remains inconsistent | User trust, Usability | Users must infer different progression contracts per step, making mistakes likely and the flow non-predictable. |
| Navigation can still drop unsaved input | Data integrity, User trust | Entered onboarding data can still disappear while the system claims onboarding is complete or skipped. |
| Selection cardinality stays implicit or mismatched | Usability, Maintainability | The product step remains inconsistent with the underlying model, and selection semantics remain hard to reason about. |
| TFS selections remain weakly confirmed | User trust, Usability | Users cannot reliably verify that the selected project/team/repository is the intended one before proceeding. |
| Work-item lookup/search/filtering remains absent | Data integrity, Usability | Invalid or wrong work items can still be accepted as plausible inputs because the flow lacks retrievable confirmation. |
| Multi-responsibility steps remain intact | Usability, Maintainability | High cognitive load and mixed state transitions remain structural properties of the flow. |
| Progressive disclosure remains absent | Usability | The most complex step continues to expose full complexity immediately, increasing onboarding friction. |
| Existing persisted state remains hidden | Data integrity, User trust, Usability | Rerun flows can still duplicate entities or block valid actions because current state is invisible and session state dominates. |
| Completion can still diverge from actual persisted state | Data integrity, User trust | False success and outcome-blind routing continue to misstate what actually completed. |
