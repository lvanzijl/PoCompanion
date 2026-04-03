#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [[ $# -ne 1 || -z "${1:-}" ]]; then
  echo "Usage: run-core-gate.sh <output-dir>" >&2
  exit 2
fi

output_dir="$1"
cd "$repo_root"
./.github/scripts/run-test-gate.sh \
  "Core Gate" \
  "PoTool.sln" \
  'TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory' \
  "$output_dir"
