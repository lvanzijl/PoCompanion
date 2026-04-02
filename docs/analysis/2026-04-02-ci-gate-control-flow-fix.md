# CI Gate Control-Flow Fix

## 1. Root cause

The blocking step was `Check sync-over-async patterns`, defined in `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml` before each gate runner step.

Problematic placement before this fix:

- `Core Gate`
  - restore
  - build
  - `Check sync-over-async patterns`
  - `Run Core Gate`
- `API Contract Gate`
  - restore
  - build
  - `Check sync-over-async patterns`
  - `Run API Contract Gate`
- `Governance Gate`
  - restore
  - build
  - `Check sync-over-async patterns`
  - `Run Governance Gate`

Because the sync-over-async check currently fails in this repository, each job failed before its own gate runner step executed. That caused:

- skipped gate runner steps
- missing gate artifacts
- secondary artifact noise instead of direct gate diagnostics

## 2. Changes made

- Removed `Check sync-over-async patterns` from:
  - `API Contract Gate`
  - `Governance Gate`
- Kept `Check sync-over-async patterns` only in:
  - `Core Gate`
- Left the three jobs independent with no `needs:` relationship between them
- Kept skipped-safe artifact upload conditions so uploads still run after a gate failure, but not when the gate step itself never ran

Modified job file:

- `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml`

Added report:

- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-ci-gate-control-flow-fix.md`

## 3. Final workflow structure

Final job list:

- `Core Gate`
- `API Contract Gate`
- `Governance Gate`

Execution structure:

- no global blocking validation step exists outside gate ownership
- no `needs:` links exist between the three gates
- each gate restores and builds independently
- `Core Gate` owns sync-over-async enforcement
- `API Contract Gate` and `Governance Gate` are no longer blocked by a Core-owned validation failure

Core Gate order is now:

1. restore
2. build
3. sync-over-async check
4. prepare artifact directory
5. run core tests
6. upload artifacts

## 4. Gate responsibility mapping

- **Core Gate**
  - sync-over-async enforcement
  - runtime correctness tests
  - reason: sync-over-async violations are a client/runtime correctness concern and are part of the runtime safety gate

- **API Contract Gate**
  - API contract tests only
  - reason: this gate should report contract drift, not runtime threading violations owned elsewhere

- **Governance Gate**
  - governance tests and baseline enforcement only
  - reason: this gate should report governance and audit failures, not runtime correctness checks owned by Core

## 5. Verification

Commands run locally:

- `cd /home/runner/work/PoCompanion/PoCompanion && dotnet build PoTool.sln --configuration Release --nologo`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/check-sync-over-async.sh`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-api-contract-gate.sh /tmp/controlflow-gates/api-contract-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-governance-gate.sh /tmp/controlflow-gates/governance-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh /tmp/controlflow-gates/core-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-test-gate.sh "API Contract Gate" "PoTool.Tests.Unit/DoesNotExist.csproj" 'TestCategory=ApiContract' /tmp/controlflow-scenarios/api-fail`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-test-gate.sh "Governance Gate" "PoTool.Tests.Unit/PoTool.Tests.Unit.csproj" 'TestCategory=Governance' /tmp/controlflow-scenarios/governance-fail /tmp/does-not-exist-baseline.json`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh /tmp/controlflow-scenarios/core-pass`

Results observed:

- `dotnet build`: **pass**
- sync-over-async check: **fail** with exit code `1`
- `API Contract Gate`: **pass** and produced TRX/log/markdown/JSON artifacts
- `Governance Gate`: **pass** and produced TRX/log/markdown/JSON artifacts
- `Core Gate` direct runner: **pass** and produced TRX/log/markdown/JSON artifacts
- forced API gate-local failure (invalid test target): **fail** with exit code `1`
- forced Governance gate-local failure (invalid baseline path): **fail** with exit code `1`
- Core Gate still passed when run separately after the forced API/Governance gate-local failures

Independence confirmed in repository validation:

1. **Core-owned pre-check failure scenario**
   - sync-over-async check fails
   - API Contract Gate still runs successfully
   - Governance Gate still runs successfully

2. **Governance-local failure scenario**
   - forced Governance gate-local failure returns non-zero
   - Core Gate still runs successfully afterward

3. **API Contract-local failure scenario**
   - forced API Contract gate-local failure returns non-zero
   - Core Gate still runs successfully afterward

Artifact evidence:

- `/tmp/controlflow-gates/api-contract-gate`
  - TRX present
  - console log present
  - markdown summary present
  - JSON summary present
- `/tmp/controlflow-gates/governance-gate`
  - TRX present
  - console log present
  - markdown summary present
  - JSON summary present
- `/tmp/controlflow-gates/core-gate`
  - TRX present
  - console log present
  - markdown summary present
  - JSON summary present

## 6. Remaining risks

- A future workflow edit could reintroduce hidden blocking behavior by adding a failing validation step before a gate runner step in multiple jobs.
- Any new repository-wide validation must be explicitly assigned to one gate, not duplicated across all gates as a pre-run blocker.
- Real GitHub Actions verification still depends on the next workflow run after this change, but the repository control flow is now explicit and gate-owned.
