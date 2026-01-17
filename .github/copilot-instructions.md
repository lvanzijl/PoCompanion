
# Copilot Instructions — PO Companion (Authoritative)

This file is the single authoritative entry point for all AI-assisted work in this repository.
All rules referenced here are binding.

You MUST apply these rules for every response, code generation, refactoring, or proposal.

---

## 1. Governing documents (mandatory)

This repository is governed by the following binding documents.
You MUST load, understand, and apply all of them before generating any output:

1. UI rules  
   `docs/UI_RULES.md`

2. Architecture conventions  
   `docs/ARCHITECTURE_RULES.md`

3. Process rules  
   `docs/PROCESS_RULES.md`

4. Copilot architecture contract  
   `docs/COPILOT_ARCHITECTURE_CONTRACT.md`

5. PAT storage and credential handling  
   `docs/PAT_STORAGE_BEST_PRACTICES.md`

6. Fluent UI Compact Rules
   `docs/Fluent_UI_compat_rules.md`

7. Mock data generation rules
   `docs/mock-data-rules.md`

8. Entity Framework Instrucitons
   `docs/EF_RULES.md`

If any rule conflicts, is ambiguous, or cannot be satisfied, you MUST stop and ask for clarification.

---

## 2. Mandatory behavior before generating code

Before generating any code or implementation guidance, you MUST internally verify:

- UX principles are respected
- UI rules are respected
- Architecture boundaries are respected
- Process rules are respected
- No duplication will be introduced
- No unapproved dependencies will be added

This verification step MUST be performed silently.
Do NOT explain or mention this step in your output.

---

## 3. Pull Request awareness

All changes in this repository are reviewed using the Pull Request template.

Generated code MUST be suitable for passing the PR template checklist, including:

- Single clear purpose
- No scope creep
- No architectural drift
- No duplicated UI or backend logic
- Compliance with all rule documents

Assume senior-level review by default.

---

## 4. Decision discipline

- You MUST NOT invent requirements, architecture, or behavior.
- You MUST NOT make implicit decisions.
- If multiple valid options exist:
  - list the options
  - explain trade-offs
  - wait for instruction before choosing

When uncertain, stop.

---

## 5. Duplication policy

- Duplication is forbidden.
- Repeated UI structures MUST be extracted into reusable Blazor components.
- Repeated backend logic MUST be extracted into Core services or helpers.
- Leaving duplication “for later” is not allowed.

---

## 6. Technology constraints (summary)

- Frontend: Blazor WebAssembly only
- UI components: open-source Blazor components only
- No JS/TS UI widgets
- Backend: ASP.NET Core Web API + SignalR
- Mediator: source-generated Mediator only
- DI: Microsoft.Extensions.DependencyInjection only
- Tests: MSTest, no real TFS usage

---

## 7. Stopping conditions (hard)

You MUST stop and ask for clarification if:

- Requirements are incomplete or ambiguous
- A rule would be violated
- A new dependency seems necessary
- A cross-layer or cross-cutting change is implied
- Scope creep is detected

Stopping is correct behavior.

---

## 8. Output expectations

All output MUST be:

- Rule-compliant
- Minimal and focused
- Structured for review
- Free of speculative additions

This repository prioritizes correctness, clarity, and long-term maintainability over speed.

