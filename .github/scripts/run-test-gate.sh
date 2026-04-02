#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 4 || $# -gt 5 ]]; then
  echo "Usage: run-test-gate.sh <gate-name> <test-target> <test-filter> <output-dir> [baseline-json]" >&2
  exit 2
fi

gate_name="$1"
test_target="$2"
test_filter="$3"
output_dir="$4"
baseline_json="${5:-}"

if [[ -z "$output_dir" ]]; then
  echo "run-test-gate.sh requires a non-empty output directory for gate '$gate_name'." >&2
  exit 2
fi

run_token="${GATE_RUN_ID:-local-$(date -u +%Y%m%dT%H%M%SZ)}"
slug="$(echo "$gate_name" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/-/g; s/-\\+/-/g; s/^-//; s/-$//')"

mkdir -p "$output_dir"

trx_prefix="${slug}-${run_token}"
log_path="$output_dir/${slug}-${run_token}.log"
summary_md_path="$output_dir/${slug}-${run_token}-failing-summary.md"
summary_json_path="$output_dir/${slug}-${run_token}-failing-summary.json"

set +e
dotnet test "$test_target" \
  --configuration Release \
  --no-build \
  --nologo \
  --logger "trx;LogFilePrefix=$trx_prefix" \
  --results-directory "$output_dir" \
  --filter "$test_filter" \
  2>&1 | tee "$log_path"
test_exit=${PIPESTATUS[0]}
set -e

if ! python3 ./.github/scripts/summarize-trx.py "$output_dir" "$trx_prefix" "$gate_name" "$summary_md_path" "$summary_json_path"; then
  echo "Gate '$gate_name' could not summarize TRX output. Expected files matching '$output_dir/${trx_prefix}*.trx' in '$output_dir'." >&2
  exit 1
fi

if [[ -n "$baseline_json" ]]; then
  set +e
  python3 ./.github/scripts/check-governance-baseline.py "$baseline_json" "$summary_json_path"
  baseline_exit=$?
  set -e

  if [[ $baseline_exit -ne 0 ]]; then
    exit $baseline_exit
  fi

  exit 0
fi

exit $test_exit
