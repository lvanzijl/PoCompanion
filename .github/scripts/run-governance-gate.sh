#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output_dir="${1:-/tmp/po-test-gates/governance-gate}"
cd "$repo_root"
./.github/scripts/run-test-gate.sh \
  "Governance Gate" \
  "PoTool.Tests.Unit/PoTool.Tests.Unit.csproj" \
  'TestCategory=Governance' \
  "$output_dir" \
  "$repo_root/docs/governance/test-failure-baseline.json"
