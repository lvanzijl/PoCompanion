#!/usr/bin/env python3
import json
import sys
from pathlib import Path


def load_names(path: Path, key: str) -> list[str]:
    data = json.loads(path.read_text())
    values = data.get(key, [])
    if not isinstance(values, list) or any(not isinstance(item, str) for item in values):
        raise ValueError(f"{path} must contain a '{key}' array of strings.")
    return sorted(set(values))


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: check-governance-baseline.py <baseline-json> <current-summary-json>", file=sys.stderr)
        return 2

    baseline_path = Path(sys.argv[1])
    current_summary_path = Path(sys.argv[2])

    baseline = load_names(baseline_path, "knownFailingTests")
    current_data = json.loads(current_summary_path.read_text())
    current = sorted({item["name"] for item in current_data.get("failedTests", [])})

    new_failures = [item for item in current if item not in baseline]
    resolved_failures = [item for item in baseline if item not in current]

    if new_failures:
        print("New governance failures detected:")
        for item in new_failures:
            print(f"- {item}")

    if resolved_failures:
        print("Governance baseline shrank; update docs/governance/test-failure-baseline.json manually:")
        for item in resolved_failures:
            print(f"- {item}")

    if new_failures or resolved_failures:
        return 1

    if current:
        print("Governance failures match the tracked baseline.")
    else:
        print("Governance gate is clean and baseline remains empty.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
