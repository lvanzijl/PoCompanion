#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output_dir="${1:-/tmp/po-test-gates/core-gate}"
cd "$repo_root"
./.github/scripts/run-test-gate.sh \
  "Core Gate" \
  "PoTool.sln" \
  'TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory' \
  "$output_dir"
