#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [[ $# -ne 1 || -z "${1:-}" ]]; then
  echo "Usage: run-governance-gate.sh <output-dir>" >&2
  exit 2
fi

output_dir="$1"
cd "$repo_root"
./.github/scripts/run-test-gate.sh \
  "Governance Gate" \
  "PoTool.Tests.Unit/PoTool.Tests.Unit.csproj" \
  'TestCategory=Governance' \
  "$output_dir" \
  "$repo_root/docs/governance/test-failure-baseline.json"
