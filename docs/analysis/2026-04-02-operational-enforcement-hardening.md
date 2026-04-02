# Operational Enforcement Hardening

## 1. What was hardened

- **Workflow naming**
  - Confirmed the workflow name is the stable operational name `Build and Test Gates`.
  - Confirmed the job names are the stable branch-protection contract names:
    - `Core Gate`
    - `API Contract Gate`
    - `Governance Gate`
- **Trigger coverage**
  - Confirmed the workflow runs on:
    - `pull_request`
    - `push` to `main`
    - `push` to `release/**`
- **Gate semantics**
  - Kept the three-gate model intact.
  - Kept Governance and API contract tests out of the Core Gate filter.
  - Kept governance baseline logic isolated to the Governance Gate only.
- **Category enforcement**
  - Extended `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/TestCategoryEnforcementTests.cs` to enforce:
    - Governance category on governed folders
    - Governance category on governance-style class names
    - ApiContract category on API contract-style class names
- **Artifact behavior**
  - Hardened artifact upload steps with `if-no-files-found: error` so missing artifact outputs fail the job.
  - Kept per-gate artifact directories and filenames isolated by gate slug and run token.
  - Kept failing-test summaries in both markdown and JSON.
- **Baseline behavior**
  - Hardened TRX summarization so a missing TRX file now fails the job instead of allowing an implicit success path.
  - Confirmed baseline comparison still applies only to Governance Gate.

## 2. Exact operational checks

- **Workflow name:** `Build and Test Gates`
- **Job names:**
  - `Core Gate`
  - `API Contract Gate`
  - `Governance Gate`

Exact gate commands and filters:

- **Core Gate**
  - `dotnet test PoTool.sln --configuration Release --no-build --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory"`
- **API Contract Gate**
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=ApiContract"`
- **Governance Gate**
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "TestCategory=Governance"`

Operational semantics:

- **Core Gate** is the authoritative merge gate for runtime correctness and excludes Governance, ApiContract, and AutomatedExploratory tests.
- **API Contract Gate** is operationally ready to become required and isolates governed contract drift.
- **Governance Gate** remains independently visible, uses the baseline, and does not re-enter runtime trust.

## 3. Branch protection handoff

A repository admin must configure branch protection manually in GitHub.

Manual handoff document:

- `/home/runner/work/PoCompanion/PoCompanion/docs/analysis/2026-04-02-branch-protection-handoff.md`

Configure now:

- Required check: `Core Gate`

Phase in after short stabilization:

- Recommended required check: `API Contract Gate`

Keep visible but optional initially:

- `Governance Gate`

Critical instruction:

- Use the exact **job names** above as required checks.
- Do **not** use only the workflow name `Build and Test Gates`.

## 4. Verification evidence

Commands run locally:

- `cd /home/runner/work/PoCompanion/PoCompanion && dotnet build PoTool.sln --configuration Release --nologo`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-core-gate.sh /tmp/po-hardening-final/core`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-api-contract-gate.sh /tmp/po-hardening-final/api`
- `cd /home/runner/work/PoCompanion/PoCompanion && ./.github/scripts/run-governance-gate.sh /tmp/po-hardening-final/gov`
- `cd /home/runner/work/PoCompanion/PoCompanion && dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "FullyQualifiedName~NswagGovernanceTests&TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory"`
- `cd /home/runner/work/PoCompanion/PoCompanion && dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo --filter "FullyQualifiedName~TestCategoryEnforcementTests&TestCategory!=Governance&TestCategory!=ApiContract&TestCategory!=AutomatedExploratory"`

Results:

- Build: **pass**
- Core Gate: **pass**
- API Contract Gate: **pass**
- Governance Gate: **pass**
- Category enforcement test: **pass** (included in Governance Gate)
- Governance baseline check: **pass**
- Governance/API contract leak checks against the Core Gate filter: **pass** (`No test matches...`)

Artifact output locations produced locally:

- `/tmp/po-hardening-final/core`
- `/tmp/po-hardening-final/api`
- `/tmp/po-hardening-final/gov`

Artifact contents per gate:

- TRX result file(s)
- raw console log
- failing summary markdown
- failing summary JSON

## 5. Remaining limitations

- GitHub branch protection is still a manual admin action; it cannot be completed from repository code.
- Pattern-based categorization enforcement is intentionally heuristic for governance-style classes outside governed folders:
  - `*AuditTests`
  - `*GovernanceTests`
  - `*ArchitectureGuardTests`
- A future governance-style test with a completely unrelated class name outside `Audits/**` and `Architecture/**` would still rely on explicit category discipline rather than naming-pattern detection.
- Governance Gate remains intentionally non-required until a repository admin chooses to require it in GitHub.

## 6. Conclusion

Operational enforcement is now **structurally complete in-repo**:

- workflow and job names are stable
- gate semantics are explicit
- artifact failure handling is hardened
- governance baseline behavior is isolated and explicit
- categorization enforcement is stricter and less dependent on folder convention alone
- branch-protection instructions are written down in-repo

The exact remaining manual GitHub action is:

- a repository admin must configure branch protection for `main` (and `release/**` if applicable) using `Core Gate` as a required check, with `API Contract Gate` ready for phase-in and `Governance Gate` left visible but optional initially.
