
# Feature: Startup UX Refactor (Phase 1–3) — Hard gating, Profiles Home, Profile Pictures, Guided Profile Creation, Transparent Sync Progress

## Goal
Refactor startup so the app is NOT usable without a valid TFS connection, except when Mock Data mode is enabled via `appconfig.json`. Introduce a Startup Orchestrator that routes the user to Configuration / Profile Creation / Profiles Home depending on readiness. Add Netflix-style Profiles Home with square profile pictures. Profile creation enforces Area Path and Goal selection using filterable multi-select comboboxes. Improve Full Sync UX by showing real progress instead of giving the impression it is instantly done. Keep the prompt functional; avoid low-level implementation details unless required by tests.

---

## Definitions (existing behavior)
- There is a TFS Configuration screen with:
  - Save configuration
  - Test Connection button
  - Verify TFS API button (next to Test Connection) that runs multiple checks; ALL must pass to be considered verified.
- Mock data is enabled/disabled in `appconfig.json`.
- “Goal” is a Work Item type.
- Existing profile management exists; do not break it.
- Full Sync currently triggers a large backend operation (400+ batches of work items) and then persists all data to the local database.

---

## Non-negotiable Rules

### 1) Mock mode
- If mock mode is enabled in `appconfig.json`, the app remains usable without TFS.
- In mock mode, profile creation is allowed without TFS.

### 2) Real TFS mode (mock disabled)
- The app must be unusable until:
  - TFS configuration is saved
  - Test Connection succeeded at least once
  - Verify TFS API passed (all checks)
  - At least one profile exists
  - An active profile is selected
- If the user never tested connection and never verified API, the user cannot create a profile.
- If the user can connect AND verify but has no profile, the user must be guided to create one.

### 3) Landing behavior
- When verified + profiles exist: show Profiles Home (Netflix-style) at startup.
- When no configuration / not tested / not verified: show Configuration with a clear banner explaining what’s missing.
- When verified but no profiles: route to “Create first profile” experience.

---

## Phase 1 — Startup gating + Profiles Home + Profile Pictures

### A) Persist readiness flags (durable)
Track:
- IsMockDataEnabled (from `appconfig.json`)
- HasSavedTfsConfig
- HasTestedConnectionSuccessfully (ONLY from successful Test Connection)
- HasVerifiedTfsApiSuccessfully (ONLY when ALL verify checks pass)
- HasAnyProfile
- ActiveProfileId
- HasCompletedInitialSyncForProfile(profileId) (model only; enforced in Phase 3)

### B) Startup Orchestrator (single decision tree)
Route on startup:

IF IsMockDataEnabled:
- Route to Profiles Home or existing home; do not block navigation.

ELSE:
- if !HasSavedTfsConfig → Configuration (“Configuration required”)
- else if !HasTestedConnectionSuccessfully → Configuration (“Test Connection required”)
- else if !HasVerifiedTfsApiSuccessfully → Configuration (“Verify TFS API required”)
- else if !HasAnyProfile → Create first profile
- else if ActiveProfileId is null → Profiles Home (force selection)
- else → Profiles Home

Decision logic must live in one place and be unit-tested.

### C) Navigation guards
In real mode, block ALL feature pages unless:
- HasVerifiedTfsApiSuccessfully
- HasAnyProfile
- ActiveProfileId != null

On block: redirect to Startup Orchestrator and show missing requirement.

### D) Profiles Home (Netflix-style)
- Profile tiles/cards with:
  - Square profile picture
  - Profile name
- Selecting a tile sets ActiveProfileId and continues.
- “Add profile” action reuses existing creation flow.
- Empty state when no profiles.

### E) Profile pictures
- Include **64 offline maritime-domain square images** as defaults (generic motifs).
- Profile defaults to one of these images.
- Allow user to set a **custom local image**.
- Allow reset to default.
- Profile model:
  - PictureType: Default | Custom
  - DefaultPictureId (0..63)
  - CustomPicturePath/BlobRef

### F) Configuration integration
- Save config → HasSavedTfsConfig = true
- Test Connection success → HasTestedConnectionSuccessfully = true
- Verify button:
  - Execute existing verification checks.
  - Set HasVerifiedTfsApiSuccessfully only when ALL pass.
  - Show which checks failed on error.

### G) Tests (minimum)
- Unit tests for all startup routing cases.
- One guard test ensuring feature navigation is blocked when not verified (real mode).

---

## Phase 2 — Create-first-profile wizard (mandatory Area Paths & Goals)

### Profile creation rules
- User MUST select:
  - ≥1 Area Path
  - ≥1 Goal (Work Item)
- Profile cannot be saved until both are satisfied.

### Area Path & Goal selection UI (mandatory)
- Use a **combobox-style control** for both Area Paths and Goals.
- Dropdown shows **all items** retrieved from TFS.
- Each item has a **checkbox** for **multi-selection**.
- User can type in the combobox input.
- Typing applies a **live text filter** (contains match).
- Checked items remain checked even when filtered out.
- UI must indicate:
  - Selected item count
  - Selected items (chips/tokens or summarized text; visual choice is flexible).

### Validation
- Area Paths must not overlap (no ancestor/descendant relationship).
- On overlap:
  - Show blocking validation error.
  - User must reselect.

---

## Phase 3 — Initial sync orchestration + transparent progress reporting

### Initial Full Sync behavior
- After profile creation, automatically run initial Full Sync for:
  - Work Items
  - Pull Requests
  - Pipelines

### Progress reporting requirements
- Full Sync must NOT appear “instant” if significant work is happening.
- When the system can determine the amount of work (e.g. number of batches, items, or steps):
  - Show a **modal dialog** during sync.
  - Display **one or more progress bars** reflecting real progress.
- Progress indicators should:
  - Represent meaningful backend progress (e.g. batches processed vs total, phases completed).
  - Update incrementally as batches are processed.
  - Clearly distinguish phases if applicable (e.g. fetching → processing → persisting).
- If total work is not fully known at start:
  - Show an indeterminate state initially.
  - Switch to determinate progress once totals are known.
- Sync completion:
  - Mark HasCompletedInitialSyncForProfile(profileId) = true
  - Close dialog and unlock feature views.

### Error handling
- If sync fails:
  - Keep the dialog open with clear error state.
  - Allow retry.
  - Do not unlock feature views.

---

## Out of scope for Phase 1
- Area path overlap validation
- Mandatory selection enforcement
- Initial sync execution and progress UI

---

## Acceptance Criteria
- Real mode:
  - App unusable until verified + profile exists + active profile selected.
  - Profile creation blocked until Test Connection + Verify passed.
  - Correct startup routing with clear messages.
- Mock mode:
  - App usable without TFS.
- Profiles Home:
  - Visible after verification when profiles exist.
- Profile pictures:
  - 64 defaults available.
  - Custom image selectable and persisted.
- Full Sync UX:
  - User sees clear, honest progress during large sync operations.
  - No misleading “instant completion” perception.

---

## Output
- Implement phases incrementally, starting with Phase 1.
- After each phase, provide a short markdown summary:
  - What changed
  - How to manually test

