#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output_dir="${1:-/tmp/po-test-gates/api-contract-gate}"
cd "$repo_root"
./.github/scripts/run-test-gate.sh \
  "API Contract Gate" \
  "PoTool.Tests.Unit/PoTool.Tests.Unit.csproj" \
  'TestCategory=ApiContract' \
  "$output_dir"
