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
- Everything compiles
- All unit tests succeed

## 12. Pull Request discipline (mandatory)

- Every change MUST be submitted via a Pull Request.
- Every Pull Request MUST follow the repository PR template.
- A Pull Request that does not fully follow the template MUST be blocked.
- Reviewers MUST verify that all checklist items are explicitly addressed.
- Missing or unchecked items are considered review blockers.

---

## 13. Client-side async guardrails (mandatory)

### 13.1 CI enforcement: no sync-over-async in PoTool.Client

The repository MUST include a CI guardrail that prevents reintroduction of blocking-wait patterns in the Blazor WebAssembly client.

- CI MUST scan **only** the `PoTool.Client` directory.
- CI MUST fail the build if any forbidden sync-over-async patterns are detected.

#### Forbidden patterns (string-based detection is sufficient)
- `.Result`
- `.Wait(`
- `GetAwaiter().GetResult`
- `AsTask().Result`
- `AsTask().Wait`

False positives outside `PoTool.Client` are not acceptable.

### 13.2 Pull Request requirements (client-side changes)

For any Pull Request that modifies code under `PoTool.Client`:

- Newly introduced or modified client APIs MUST be async-first.
- Old synchronous APIs MUST be marked obsolete and MUST NOT be used.
- At least one real client component MUST demonstrate:
  - async lifecycle usage
  - async event handling
  - end-to-end awaiting of async services

Failure to meet these requirements is a **BLOCKER**.

### 13.3 Review responsibility

Reviewers MUST treat any sync-over-async usage in `PoTool.Client` as:
- an architectural violation
- a mandatory fix before approval

---

## 14. Release Notes Discipline (mandatory)

### 14.1 Canonical source

All user-visible release notes MUST be stored in:

```
docs/release-notes.json
```

This is the **single canonical source** consumed by the Home "What's New" dialog.  
No other file may duplicate or replace it.

Schema — array of objects, newest first (prepend new entries at position 0):

```json
[
  {
    "date": "YYYY-MM-DD",
    "type": "New | Changed | Fixed",
    "title": "<string, max 60 chars>",
    "impact": "<one sentence; user-facing; no marketing>",
    "area": "<Home | Backlog | Health | Trends | Planning | Bugs | Settings>",
    "link": "<optional route, e.g. /home/pipelines>"
  }
]
```

Field rules:
- `date`: implementation date in `YYYY-MM-DD` format (calendar date; no time or timezone).
- `type`: MUST be exactly one of `New`, `Changed`, or `Fixed`.
- `area`: MUST be exactly one of `Home`, `Backlog`, `Health`, `Trends`, `Planning`, `Bugs`, or `Settings`.
- `link`: optional; omit the field entirely if not applicable.
- New entries MUST be prepended (inserted at index 0) to preserve newest-first order.

Comments are not permitted inside the JSON file.

### 14.2 When a release note is REQUIRED

A release note entry is REQUIRED when a PR includes any of:

- A new page/route added, removed, or renamed (Razor `@page` changes, route registrations, nav/menu changes).
- Any workflow/UI behavior change (new buttons, changed primary actions, dialogs, changed defaults).
- Any change to metrics or trend definitions (calculation, grouping, filtering, "what the number means").
- Any change to scoping/filter behavior (product/team defaults, "all products/teams" logic, drill-down behavior).
- Any cache/sync UX change (sync gate behavior, error/loading behavior affecting users).
- Any validation rule change (RR/RC/SI/EFF logic or messages that users see).
- Any change that could alter a PO decision made based on the UI.

### 14.3 Paths that imply user-visible change

If a PR touches the following paths, assume release notes are required unless the bypass below is used:

- `PoTool.Client/**`
- `PoTool.Api/**` — only if it affects DTOs consumed by the UI or trend/metric computations
- `docs/GEBRUIKERSHANDLEIDING*.md` — only if it reflects shipped behavior

### 14.4 Allowed bypass (narrow)

Release notes may be skipped **only** if the change has no user impact (pure refactor, internal cleanup, comments, formatting) **and** the PR description contains **exactly** this line:

```
ReleaseNotes: N/A (no user impact)
```

If Copilot decides to skip release notes, it MUST add that line to the PR description text it outputs (or to the PR template section if the repo uses one).

### 14.5 End-of-work confirmation (mandatory)

At the end of any implementation, Copilot MUST explicitly output one of:

- `Release notes: updated` — and point to the new entry in `docs/release-notes.json`
- `Release notes: N/A (no user impact)`

