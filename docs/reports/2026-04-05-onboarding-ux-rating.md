# Onboarding UX Rating

Source of truth for this assessment: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`.

## 1. Step-by-Step UX Scores

### Step 1 — Configure Azure DevOps Connection
- **Score: 2/10**
- **Why**
  - This step carries the highest cognitive load in the flow because it combines saved-config preload, live TFS project discovery, save/test/verify orchestration, streaming progress, error handling, and configuration import in one screen. The audit explicitly calls it the densest screen and says it mixes four concerns in one place. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:67-88,268-280,284-295`.
  - Action logic is inconsistent even inside the step: `Next` is blocked until an explicit `Save` succeeds, which establishes a different navigation model from all later steps. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-188,304-306`.
  - Feedback is not strong enough to support the amount of responsibility on the screen. If project loading fails, the UI falls back to manual entry without showing the failure reason, and post-selection confirmation for the chosen project is missing. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:232-248`.
  - Data integrity is also weak for a first-run gate: configuration is persisted before connection/API verification finishes, so a failed verification still leaves partial saved state. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:80-83,315-317`.

### Step 2 — Create Product Owner Profile
- **Score: 4/10**
- **Why**
  - The step is narrower than step 1, but it inherits the flow’s central inconsistency: the user may type data, press `Next`, and leave the step without persisting anything because `Next` is never gated and there is no auto-save. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:90-106,180-194,311-313`.
  - The action model is unclear because `Save Profile` is immediate, but `Next` does not depend on save. That creates two competing interpretations of progress: “advance means continue” versus “advance means finished.” Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:102-106,180-188`.
  - Validation is present, but feedback remains shallow: the audit describes success/error messaging, yet also shows no persistent intermediate state model and no unsaved-change warning on exit. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:191-194,299-306`.

### Step 3 — Create Product
- **Score: 2/10**
- **Why**
  - This is the weakest single-task step because the core input is a backlog root work item, but the audit says the user only gets a numeric ID field with no TFS lookup, no search, no type filtering, and no title/type confirmation after entry. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:108-126,225-242,347-349`.
  - The single-vs-multiple-item model is unclear. The UI accepts one root ID while the underlying product model supports multiple root IDs, and the audit states that this is not explained in the step. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:121-124,196-204`.
  - Like step 2, it allows silent abandonment of typed data because `Next` is always available and no persistence happens until explicit save. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:121-126,190-194,311-313`.
  - The step also contributes directly to later flow breakage because step 4 and step 5 depend on a product created in the current wizard session. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:323-329`.

### Step 4 — Select Your Team
- **Score: 4/10**
- **Why**
  - The step has one useful default: it can derive the area path from the selected team, which lowers effort relative to manual entry. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:128-149`.
  - That gain is offset by a weak confirmation model. After choosing a TFS team, the user sees the derived area path, but not a stronger confirmation surface for the selected team/project identity. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:232-240`.
  - The step preserves the save-vs-next split: `Save Team` is immediate, but `Next` does not require it. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:142-149,180-188`.
  - It also contains a backend-state trap visible in the UX: a team can be created without linking it to any product, and a failed link can still be presented as inline success. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:327-333`.

### Step 5 — Configure Repositories
- **Score: 3/10**
- **Why**
  - The step at least makes multi-select visible through chips and a count, so the user can see that repository selection is plural. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:151-171,196-204`.
  - That clarity stops at the interaction boundary. The audit states that the UI does not clearly tell the user selections are only in memory until `Save Repositories`. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:196-204`.
  - The step is structurally unstable because saving can fail even when the system already has a product; it depends specifically on a product created in the current wizard session. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:323-325`.
  - Feedback is misleading under failure: partial repository saves can still render as success inline. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:245-248,335-337`.
  - The step also shares the same silent-drop problem as steps 2-4 because `Get Started` does not require repository save. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:164-168,190-194`.

## 2. Overall UX Score

- **Overall score: 3/10**

The flow is usable only if the user correctly infers hidden rules:
- step 1 must be saved before progress is possible,
- steps 2-5 do not need to be saved to progress,
- but unsaved data can be lost,
- and several later steps depend on objects created in the same transient session rather than on already-configured state.

Those are not isolated defects; they are the dominant interaction model described in the audit. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:173-188,190-204,268-306,310-349`.

## 3. Dominant UX Failure Patterns

1. **Progression does not mean persistence**
   - Step 1 requires save-before-next, but steps 2-5 allow forward movement without saving; this breaks the user’s mental model of what “Next” means. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-188,304-306`.
2. **Navigation can silently discard work**
   - Unsaved input on steps 2-5 lives only in component state, and `Skip Wizard` / `Get Started` close without unsaved-change protection. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:190-194,311-313`.
3. **TFS-backed selections lack strong confirmation**
   - Project, team, repository, and especially work item choices do not expose enough detail after selection to verify correctness. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-242,347-349`.
4. **Step boundaries do not match responsibility boundaries**
   - Step 1 combines TFS config and import; step 4 combines team creation and linking; step 5 combines live discovery and local persistence. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-280,284-295`.
5. **Visible success can diverge from real outcome**
   - Team-link failure and partial repository failure can still present as inline success, which damages trust in the flow. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:245-248,331-337`.

## 4. Issue Classification

| Title | Description | Step(s) | Severity | Type |
|---|---|---:|---|---|
| Overloaded configuration entry step | The first screen combines TFS configuration, live project discovery, verification progress, error handling, and configuration import in one place. | 1 | High | Structural UX flaw |
| Save-vs-Next inconsistency | Step 1 requires explicit save before progression, while steps 2-5 permit progression without save. | 1-5 | High | Interaction flaw |
| Silent loss of unsaved input | Steps 2-5 keep data only in component state; `Next`, `Get Started`, and `Skip Wizard` can end the flow without persisting typed input. | 2-5 | Critical | Data integrity risk |
| Completion detached from actual state | The host page always routes to `/sync-gate` after dialog close, regardless of whether the dialog ended via skip or completion. | 1-5 | High | Interaction flaw |
| Work item entry has no lookup model | Product creation relies on a raw numeric work item ID with no lookup, search, type filter, or title confirmation. | 3 | High | Validation/feedback flaw |
| Single-vs-multiple item model is unclear | Product creation supports only one visible root item even though the underlying model supports multiple; repository multi-select is visible but its save boundary is not explicit. | 3,5 | Medium | Structural UX flaw |
| Missing post-selection confirmation | After TFS selection, the wizard does not provide a strong confirmation surface for project, team, or repository details. | 1,4,5 | High | Validation/feedback flaw |
| Existing setup is hidden on rerun | Rerunning the wizard does not surface existing profiles, products, teams, or repositories, so the user cannot tell whether they are adding, replacing, or duplicating. | 2-5 | High | Structural UX flaw |
| Repository step depends on current-session product creation | Step 5 can fail even when the system already contains a product because it only recognizes `_createdProductId` from the current wizard run. | 5 | Critical | Data integrity risk |
| Team creation can leave configuration incomplete | Step 4 can create a team without linking it to any product when the current session lacks a product. | 4 | High | Data integrity risk |
| Misleading success state for team linking | A failed product-team link can still render as inline success. | 4 | Critical | Validation/feedback flaw |
| Misleading success state for partial repository save | Partial repository failure can still render as inline success when at least one repository succeeded. | 5 | Critical | Validation/feedback flaw |
| Import flow is embedded and terminates review | Import lives inside the TFS configuration step, and a successful import completes onboarding immediately instead of preserving a review point. | 1 | High | Structural UX flaw |
| Load failures degrade into vague UI states | Failed project/team/repository loading becomes manual fallback or generic “not found” messaging without on-screen diagnostic clarity. | 1,4,5 | Medium | Validation/feedback flaw |
| Component-level transport leakage shapes the UX | The audit shows the wizard itself owns raw HTTP orchestration, SignalR wiring, and persistence sequencing, which leaks implementation concerns into screen behavior. | 1-5 | Medium | Architectural leakage into UX |

## 5. Mapping to Desired Behavior

### Expectation: TFS configuration and import/export must be separate flows
- **Status: Fully violated**
- **Evidence**
  - The audit states that step 1 mixes four concerns, including TFS configuration and configuration import, on the same screen. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-273`
  - The audit also calls step 1 the densest screen because it includes the full import flow alongside configuration and verification. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:284-295`

### Expectation: No explicit "Save" requirement per step (auto-save or atomic progression)
- **Status: Fully violated**
- **Evidence**
  - Step 1 requires explicit save before `Next` is enabled. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-188`
  - Steps 2-5 do not auto-save and allow forward progression without persistence. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:102-106,121-126,142-149,164-168,190-194`

### Expectation: Navigation should never silently drop user input
- **Status: Fully violated**
- **Evidence**
  - Unsaved input on steps 2-5 exists only in component state until explicit save. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:190-194`
  - `Skip Wizard` and `Get Started` close immediately without unsaved-change protection. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:173-176,190-194`
  - The audit documents a reproducible scenario where onboarding completes while entered data was never persisted. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:311-313`

### Expectation: It must be clear when multiple items can be added vs single item
- **Status: Partially violated**
- **Evidence**
  - Repository multi-select is visible through chips and count. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:196-203`
  - Product creation exposes only one root ID even though the underlying model supports multiple, and the audit says this is not explained in the step. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:121-124,199-204`
  - The audit also says repository selections are not clearly described as in-memory until save. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:202-204`

### Expectation: After selecting something from TFS, details must be shown (confirmation)
- **Status: Partially violated**
- **Evidence**
  - Project dropdown items show name/description while searching, but there is no post-selection confirmation panel. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:233-234`
  - Team selection shows derived area path, but not a stronger confirmation surface for team/project details. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:237-238`
  - Repository selection shows names as chips, but not IDs or persisted-state confirmation. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:239-240`

### Expectation: Work item selection must support lookup, search, and type filtering
- **Status: Not implemented**
- **Evidence**
  - The audit states that the product step uses only a numeric ID field.
  - It explicitly states there is no work-item validation or lookup endpoint usage and no title/details display. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-227,235-236,347-349`

### Expectation: Screens should have low cognitive load (no overloaded screens)
- **Status: Fully violated**
- **Evidence**
  - The audit names step 1 as the densest screen and lists its combined responsibilities. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:284-295`
  - It separately classifies step 1 as mixing four concerns. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-273`

### Expectation: Clear defaults and progressive disclosure
- **Status: Partially violated**
- **Evidence**
  - There is one clear default in step 4: the area path can be derived from the selected team. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:128-149`
  - The broader flow does not follow progressive disclosure because step 1 combines configuration, verification, progress reporting, and import on one screen. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-273,284-295`
  - The audit also notes that step 1’s summary stays static (“Activity source: not configured”) instead of clarifying current entered values. `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:297-301`

## 6. Root Causes

### 1. Save/progress is not governed by a single interaction model
- This root cause explains:
  - save-before-next only on step 1,
  - non-blocking next on steps 2-5,
  - silent loss of unsaved data,
  - completion without persisted state.
- Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-194,311-313`.

### 2. Step boundaries do not align with user goals
- This root cause explains:
  - TFS config and import sharing step 1,
  - team creation and product linking sharing step 4,
  - live repository discovery and local repository persistence sharing step 5,
  - high cognitive load on the first screen.
- Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-295`.

### 3. The flow lacks a confirmation-oriented selection model
- This root cause explains:
  - weak project/team/repository confirmation,
  - numeric work-item entry with no lookup,
  - inability to verify selected TFS-backed entities before committing.
- Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-242,347-349`.

### 4. Wizard state is tied to transient session artifacts instead of current configuration reality
- This root cause explains:
  - step 5 failing without a product created in the same session,
  - step 4 creating a team without a product link,
  - rerun flows not surfacing existing configured entities,
  - duplicate creation risk.
- Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:230,300-301,319-329`.

### 5. Feedback reports action execution, not resulting state
- This root cause explains:
  - inline success despite failed team linking,
  - inline success despite partial repository failure,
  - vague load-failure presentation.
- Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:245-248,331-337`.

## 7. Structural vs Local Fixes

### Structural redesign required
- Separate TFS configuration from configuration import/export because the current first step combines multiple unrelated goals. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:268-295`
- Replace the mixed save/next progression model with a single progression model; the current flow cannot be made coherent through copy changes alone because persistence semantics differ by step. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:180-194,304-306`
- Redefine product setup around a real work-item selection flow rather than a raw numeric ID field. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:225-227,235-236,347-349`
- Make onboarding aware of already-configured system state instead of relying on IDs created during the current wizard session. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:230,300-301,319-329`
- Reintroduce a review-oriented completion model for import because immediate wizard completion prevents the imported state from being reviewed in-context. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:241-242,339-341`

### Local fixes possible
- Show stronger post-selection confirmation details for project, team, and repository selections. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:232-240`
- Make single-vs-multiple item handling explicit on product and repository steps. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:196-204`
- Surface on-screen reasons when project/team/repository loading fails instead of degrading into vague fallback states. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:245-246`
- Stop rendering inline success when team linking failed or repository saves only partially succeeded. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:247-248,331-337`
- Align close/finish behavior with dialog outcome instead of always taking the same post-close route. Evidence: `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md:343-345`
