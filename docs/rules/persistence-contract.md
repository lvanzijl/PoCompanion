# Persistence contract

This file mirrors the authoritative repository rules in [`../../.github/copilot-instructions.md`](../../.github/copilot-instructions.md).

## Absolute enforcement
- No semantic interpretation is allowed.
- If a rule requires zero occurrences, zero is absolute.
- If a violation is found, it must be fixed, not justified.

## Definitions
- Historical leakage: active documentation that preserves superseded semantics, removed terminology, or archive-only narratives as if they still guide current work.
- Active documentation: any markdown outside `docs/archive/**` that describes current architecture, implementation, process, user guidance, or working agreements.
- Archive-only content: superseded material retained only for traceability under `docs/archive/**`; it must never be treated as current guidance.

## Authority
- `.github/copilot-instructions.md` is the only authoritative rule source.
- `docs/rules/` exists as a human-readable mirror.
- When mirrored text differs from the authoritative file, the authoritative file wins.

## Rules

- No entity with required foreign keys may be persisted unless every required relationship is resolved before persistence begins.
- Required foreign keys must be assigned explicitly, or the corresponding parent navigation must be assigned and tracked in the same persistence boundary.
- Parent entities for required relationships must already exist as tracked or persisted principals before child persistence.
- Do not rely on default values, empty strings, post-save mutation, or database exceptions to repair required relationships.
- Validation before persistence is mandatory and must run from the central persistence boundary so callers cannot bypass it accidentally.
- Seeding and bootstrap flows must follow plan → validate → execute and must not partially persist a graph before required parents exist.

## Contributor guidance

- EF Core rejects broken relationships too late for clear diagnostics; validate before database commit.
- Use coherent persistence boundaries so one logical graph is validated and saved together.
- SQLite-backed tests are required when proving relational persistence behavior.
