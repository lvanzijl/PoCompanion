# CI Artifact Contract Fix

## 1. Root cause

The GitHub Actions failure was caused by an incomplete workflow/script artifact contract, not by gate test logic.

Exact mismatch found:

- The workflow always attempted to upload artifacts from:
  - `/home/runner/work/_temp/core-gate`
  - `/home/runner/work/_temp/api-contract-gate`
  - `/home/runner/work/_temp/governance-gate`
- The gate wrapper scripts still exposed hidden local fallback output directories under `/tmp/po-test-gates/...` when no argument was supplied.
- More importantly, the workflow uploaded artifacts unconditionally with `if: always()` even when the gate execution step was skipped because an earlier step in the job had already failed.
- In the failed GitHub Actions run (`Build and Test Gates`, run `23923660683`), `Check sync-over-async patterns` failed first, each gate step was skipped, and the upload step then tried to upload an output directory that had never been populated.

Affected gates:

- `Core Gate`
- `API Contract Gate`
- `Governance Gate`

## 2. Before/after contract

### Core Gate

- **Workflow output path before:** `${{ runner.temp }}/core-gate`
- **Script output path before:** argument when supplied, otherwise hidden default `/tmp/po-test-gates/core-gate`
- **Workflow output path after:** `${{ runner.temp }}/core-gate`
- **Script output path after:** required explicit argument only; workflow passes `${{ runner.temp }}/core-gate`

### API Contract Gate

- **Workflow output path before:** `${{ runner.temp }}/api-contract-gate`
- **Script output path before:** argument when supplied, otherwise hidden default `/tmp/po-test-gates/api-contract-gate`
- **Workflow output path after:** `${{ runner.temp }}/api-contract-gate`
- **Script output path after:** required explicit argument only; workflow passes `${{ runner.temp }}/api-contract-gate`

### Governance Gate

- **Workflow output path before:** `${{ runner.temp }}/governance-gate`
- **Script output path before:** argument when supplied, otherwise hidden default `/tmp/po-test-gates/governance-gate`
- **Workflow output path after:** `${{ runner.temp }}/governance-gate`
- **Script output path after:** required explicit argument only; workflow passes `${{ runner.temp }}/governance-gate`

Additional workflow behavior change:

- **Before:** upload steps always ran, even when the gate step was skipped and no artifacts could exist.
- **After:** upload steps still run on gate failure, but only when the corresponding gate step actually ran and therefore had a chance to produce artifacts.

## 3. Files changed

- Workflow file:
  - `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml`
- Shared scripts:
  - `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/run-test-gate.sh`
  - `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/summarize-trx.py`
- Wrapper scripts:
  - `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/run-core-gate.sh`
  - `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/run-api-contract-gate.sh`
  - `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/run-governance-gate.sh`
- Supporting documentation:
  - `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/README.md`

## 4. Hardening details

- **Argument validation**
  - Each wrapper script now requires exactly one non-empty output-directory argument.
  - Hidden default `/tmp/...` fallback behavior was removed.
- **Directory creation**
  - The workflow prepares a distinct gate output directory for each job.
  - The shared runner script also runs `mkdir -p` on the supplied output directory before writing anything.
- **Log capture**
  - Gate console output continues to be captured with `tee` so logs survive test failures.
- **TRX handling**
  - TRX files are still emitted into the supplied output directory via `--results-directory`.
  - The TRX prefix remains deterministic from gate slug plus run token.
- **Summarization behavior**
  - Summarization now fails with a precise gate-specific error when no TRX file is present.
  - The error message includes the gate name, expected TRX pattern, and output directory.
- **Artifact upload behavior**
  - Upload continues to use `if-no-files-found: error`.
  - Upload now uses the same explicit output directory passed to the gate runner.
  - Upload runs with `always()` only when the gate step was not skipped, so real gate failures still upload artifacts while skipped gates no longer produce a second misleading artifact error.

## 5. Verification

Commands run:

- `cd /home/runner/work/PoCompanion/PoCompanion && dotnet build PoTool.sln --configuration Release --nologo`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh /tmp/ci-artifact-contract/core-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-api-contract-gate.sh /tmp/ci-artifact-contract/api-contract-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-governance-gate.sh /tmp/ci-artifact-contract/governance-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh` (usage validation)
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-api-contract-gate.sh` (usage validation)
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-governance-gate.sh` (usage validation)

Results:

- Build: **pass**
- `Core Gate`: **pass**
- `API Contract Gate`: **pass**
- `Governance Gate`: **pass**
- Wrapper usage validation: all three wrappers exit with usage error `2` when no output directory is supplied

Evidence that each gate directory contains the expected files:

- `/tmp/ci-artifact-contract/core-gate`
  - TRX files present
  - raw console log present
  - markdown summary present
  - JSON summary present
- `/tmp/ci-artifact-contract/api-contract-gate`
  - TRX file present
  - raw console log present
  - markdown summary present
  - JSON summary present
- `/tmp/ci-artifact-contract/governance-gate`
  - TRX file present
  - raw console log present
  - markdown summary present
  - JSON summary present

Confirmation that upload paths now match generation paths:

- `Core Gate` workflow upload path: `${{ runner.temp }}/core-gate` → wrapper receives and writes to the same path
- `API Contract Gate` workflow upload path: `${{ runner.temp }}/api-contract-gate` → wrapper receives and writes to the same path
- `Governance Gate` workflow upload path: `${{ runner.temp }}/governance-gate` → wrapper receives and writes to the same path

## 6. Remaining limitations

- If a job fails before its gate step runs, gate artifacts cannot exist because the gate never executed; the workflow now avoids the misleading secondary artifact-upload failure in that case.
- Verification of the upload step itself still depends on a real GitHub Actions run, but the in-repo contract is now explicit and deterministic.
- Core Gate still writes multiple TRX files because it runs against `PoTool.sln`; this is intentional and still deterministic within the gate output directory.
