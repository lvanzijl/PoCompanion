# Process Rules — PO Companion

These rules are binding for all development work, including AI agents.
The purpose is architectural integrity, low noise, and senior-level quality control.

---

## 1. Core principle

Every change is reviewed as if it affects the long-term maintainability of the system.
Speed is secondary to correctness, clarity, and consistency.

---

## 2. Mandatory work order

All work follows this sequence:

1. Understand the request
2. Confirm scope and boundaries
3. Check impact against:
   - UX principles
   - UI rules
   - Architecture conventions
   - Process rules
4. Propose changes
5. Implement
6. Review
7. Merge

Skipping steps is not allowed.

---

## 3. Review is mandatory (senior standard)

- **All code must be reviewed before merge.**
- Reviews are performed at **senior level**, regardless of author.
- A review is not a style check; it is a rule-compliance and design check.

Unreviewed code is considered invalid.

---

## 4. What every review MUST check (hard)

Reviewers MUST explicitly verify:

- UX principles compliance
- UI rules compliance
- Architecture conventions compliance
- Process rules compliance
- No architectural boundary violations
- No hidden decisions or assumptions

If any rule is unclear or violated, the PR MUST be blocked.

---

## 5. Duplication rules (strict)

### 5.1 UI duplication
- If the same UI structure, layout, or interaction appears **more than once**, it MUST be extracted into:
  - a reusable Blazor component, or
  - a shared layout/partial component.

Copy-paste reuse is forbidden.

### 5.2 Backend duplication
- Repeated logic across handlers, services, or controllers MUST be extracted into:
  - Core services
  - shared helpers in Core

Duplication is considered technical debt and blocks approval.

---

## 6. Review feedback limits (noise control)

To prevent review overload, reviewers MUST follow this structure:

- **BLOCKER** (unlimited, but only rule violations)
  - Architecture violations
  - Security issues
  - Incorrect behavior
- **MAJOR** (max 3 per PR)
  - Design flaws
  - Duplication
  - Maintainability risks
- **MINOR** (max 5 per PR)
  - Readability
  - Naming
  - Minor structure improvements

Anything beyond this MUST be:
- deferred
- documented as follow-up
- or explicitly marked out-of-scope

---

## 7. Scope control during review

Reviewers MUST block PRs that:
- introduce features not in scope
- add “convenient extras”
- change behavior outside the stated goal
- introduce cross-cutting changes without approval

One PR = one goal.

---

## 8. Refactoring during review

Refactoring is allowed ONLY if:
- it reduces duplication
- it improves rule compliance
- it improves testability

Refactoring MUST NOT:
- change observable behavior
- introduce new dependencies
- alter layer boundaries

Large refactors require explicit approval.

---

## 9. Agent-specific review rules

AI agents MUST:
- assume their output will be reviewed against all rules
- proactively reduce duplication
- prefer existing components and patterns
- explain why code is structured the way it is

AI agents MUST NOT:
- leave duplicated code “for later”
- introduce abstractions without clear need
- optimize prematurely

---

## 10. Mandatory review outcome

Every reviewed change MUST end with:

- Explicit confirmation of rule compliance
- Explicit notes on:
  - what was changed
  - what was intentionally not changed
  - known limitations

Silent approval is not allowed.

---

## 11. Definition of Done (process-level)

A change is done only when:
- it passed senior-level review
- duplication has been actively addressed
- all rules have been re-checked
- feedback noise has been minimized
- no architectural drift is introduced
