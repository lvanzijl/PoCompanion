# Validation Rules Mirror

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
