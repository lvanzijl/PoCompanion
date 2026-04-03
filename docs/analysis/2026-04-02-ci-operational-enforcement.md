# CI Operational Enforcement

## 1. CI Activation

- Restored the active workflow by renaming `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml.disabled` to `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/build.yml`.
- Configured workflow triggers for:
  - `pull_request` on all branches
  - `push` on `main`
  - `push` on `release/**`
- The workflow name is now `Build and Test Gates`.

## 2. Gate Definitions

Exact workflow job names:

1. `Core Gate`
2. `API Contract Gate`
3. `Governance Gate`

Exact gate commands enforced by the workflow and local wrappers:

- Core Gate
  - `dotnet test PoTool.sln --configuration Release --no-build --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory"`
- API Contract Gate
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=ApiContract"`
- Governance Gate
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=Governance"`

Filtering logic:

- Core Gate excludes non-runtime governance tests, API contract tests, and exploratory UI tests.
- API Contract Gate runs only the NSwag / contract-governance layer.
- Governance Gate runs audit, documentation, architecture, and API-contract governance tests as the visible non-runtime layer.

## 3. Artifact Strategy

Each workflow job now publishes an artifact bundle containing:

- TRX test result files
- full console log captured from the gate command
- failing test summary in both markdown and JSON

Implementation details:

- Shared runner script: `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/run-test-gate.sh`
- TRX summarizer: `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/summarize-trx.py`
- Artifacts are uploaded with job-specific names and `github.run_id` suffixes:
  - `core-gate-${run_id}`
  - `api-contract-gate-${run_id}`
  - `governance-gate-${run_id}`
- Per-file names also include the gate slug plus run id or local timestamp.

## 4. Categorization Enforcement

Implemented hard categorization enforcement in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/TestCategoryEnforcementTests.cs`

Rules enforced:

1. All test classes under:
   - `PoTool.Tests.Unit/Audits/**`
   - `PoTool.Tests.Unit/Architecture/**`
   must declare `[TestCategory("Governance")]`.
2. `NswagGovernanceTests` must declare `[TestCategory("ApiContract")]` in addition to Governance.
3. Governance-folder tests must not rely on folder placement alone; missing category attributes are treated as violations because they would leak back into the core gate.

Files directly affected:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/TestCategoryEnforcementTests.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs`

## 5. Baseline Strategy

Baseline file:

- `/home/runner/work/PoCompanion/PoCompanion/docs/governance/test-failure-baseline.json`

Comparison logic:

- Implemented in `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/check-governance-baseline.py`
- The Governance Gate compares the current failing-test set against the baseline.
- New failures fail the job.
- Resolved baseline entries also fail the job so the baseline is updated deliberately rather than drifting silently.
- Core Gate does not use baseline logic.

Current baseline state:

- empty (`knownFailingTests: []`)

## 6. Branch Protection Instructions

Exact check names to configure in branch protection:

- Required:
  - `Core Gate`
- Recommended required after stabilization:
  - `API Contract Gate`
- Visible but not initially required:
  - `Governance Gate`

Recommended branch-protection configuration:

1. Require status checks to pass before merging.
2. Select `Core Gate` as a required check immediately.
3. Add `API Contract Gate` as a required check once the team is comfortable making contract drift blocking.
4. Leave `Governance Gate` visible but optional during phase-in.
5. Do not use the aggregate workflow name as the protected check; use the exact job names above.

## 7. Developer Workflow

Local developer entry points were added under `/home/runner/work/PoCompanion/PoCompanion/.github/scripts/`:

- `./.github/scripts/run-core-gate.sh`
- `./.github/scripts/run-api-contract-gate.sh`
- `./.github/scripts/run-governance-gate.sh`

These wrappers run the same filters as CI and write TRX, console logs, and failing-test summaries under `/tmp/po-test-gates/...` by default.

Local documentation was added to:

- `/home/runner/work/PoCompanion/PoCompanion/.github/workflows/README.md`

## 8. Verification

Validation performed locally:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh /tmp/po-test-gates/core-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-api-contract-gate.sh /tmp/po-test-gates/api-contract-gate`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-governance-gate.sh /tmp/po-test-gates/governance-gate`

Results:

- Core Gate passed.
- API Contract Gate passed.
- Governance Gate passed.
- Governance baseline comparison passed and remained empty.
- Artifact directories were produced locally with TRX, logs, and summaries for each gate.

Operational status:

- CI workflow is active in the repository tree.
- Gate separation is hard-enforced by workflow jobs and by a governance categorization audit.
- Governance remains visible as a distinct gate and cannot silently drift back into the core merge gate.
