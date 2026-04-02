# Persistence contract

This document mirrors the authoritative persistence contract in `.github/copilot-instructions.md`.

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
