# Copilot Instructions — PO Companion (Authoritative)

This file is the **single authoritative entry point** for all AI-assisted work in this repository.  
All referenced documents are **binding**.

You MUST apply these rules for every response, code generation, refactor, or proposal.

---

## 1. Binding rule set (load first)

Before producing any output, you MUST load, understand, and apply:

1. UI & UX rules  
   `docs/UI_RULES.md`

2. Architecture rules  
   `docs/ARCHITECTURE_RULES.md`

3. Copilot architecture contract  
   `docs/COPILOT_ARCHITECTURE_CONTRACT.md`

4. Process rules  
   `docs/PROCESS_RULES.md`

5. Fluent UI compact density rules  
   `docs/Fluent_UI_compat_rules.md`

6. Entity Framework rules  
   `docs/EF_RULES.md`

7. Mock data rules  
   `docs/mock-data-rules.md`

If any rule conflicts, is ambiguous, or cannot be satisfied, you MUST stop and ask for clarification.

---

## 2. Mandatory pre-generation verification (silent)

Before generating any output, you MUST internally verify that:

- UX and UI rules are respected
- Architecture boundaries are respected
- Process rules are respected
- No duplication will be introduced
- No new or unapproved dependencies are required

This verification is mandatory and MUST NOT be mentioned in the output.

---

## 3. Decision discipline (hard)

- You MUST NOT invent requirements, behavior, or architecture.
- You MUST NOT make implicit decisions.
- If multiple valid options exist:
  - list the options
  - describe trade-offs
  - wait for instruction before choosing

When uncertain, stop.

---

## 4. Duplication policy (non-negotiable)

- Duplication is forbidden.
- Repeated UI structures MUST be extracted into reusable Blazor components.
- Repeated backend logic MUST be extracted into Core services or helpers.
- “Leave it for later” is not acceptable.

---

## 5. Pull request fitness

All output MUST be suitable for senior-level review and pass the PR checklist:

- Single, clear purpose
- No scope creep
- No architectural drift
- No duplicated logic
- Full compliance with all rule documents

Assume strict review by default.

---

## 6. Technology constraints (summary only)

- Frontend: Blazor WebAssembly
- UI: open-source Blazor components only
- No JS/TS UI widgets
- Backend: ASP.NET Core Web API + SignalR
- Mediator: source-generated Mediator only
- DI: Microsoft.Extensions.DependencyInjection only
- Tests: MSTest; no real TFS usage

---

## 7. Mandatory stop conditions

You MUST stop and ask for clarification if:

- Requirements are incomplete or ambiguous
- Any rule would be violated
- A new dependency appears necessary
- A cross-layer or cross-cutting change is implied
- Scope creep is detected

Stopping is correct behavior.

---

## 8. Output quality bar

All output MUST be:

- Rule-compliant
- Minimal and focused
- Structured for review
- Free of speculative additions

Correctness and long-term maintainability outweigh speed.
