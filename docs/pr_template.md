```md
# Pull Request Template — PO Companion

## 1. Purpose
Describe the single, clear goal of this PR.
(No multiple goals, no scope creep.)

---

## 2. Scope confirmation
- [ ] This PR addresses only the stated purpose
- [ ] No unrelated changes are included
- [ ] No new navigation items or architectural patterns introduced

---

## 3. Rule compliance (mandatory)

### UX
- [ ] UX principles reviewed and followed
- [ ] Layout, interaction, and navigation patterns unchanged or extended correctly

### UI
- [ ] Uses approved open-source Blazor components
- [ ] No custom JS/TS UI widgets added
- [ ] CSS isolation respected
- [ ] Dark-only theme preserved

### Architecture
- [ ] Layer boundaries respected (Core / Api / Frontend / Shell)
- [ ] No direct TFS access outside backend
- [ ] Core remains infrastructure-free
- [ ] Backend remains runnable in- and out-of-process

### Process
- [ ] No implicit decisions or assumptions introduced
- [ ] No unapproved dependencies added
- [ ] One feature, one goal

---

## 4. Duplication check (hard rule)
- [ ] No obvious code duplication introduced
- [ ] Repeated UI structures extracted into Blazor components
- [ ] Repeated backend logic extracted into Core services/helpers

---

## 5. Testing
- [ ] Business logic covered by unit tests (MSTest)
- [ ] No real TFS calls used in tests
- [ ] Existing tests updated where behavior changed

---

## 6. Review expectations
Reviewer focus:
- Correctness
- Rule compliance
- Duplication
- Maintainability

Style and formatting are secondary.

---

## 7. Notes for reviewers
Explain:
- What was intentionally not changed
- Known limitations or follow-up items

---

## 8. Final checklist
- [ ] All rules re-checked
- [ ] Ready for senior-level review
```
