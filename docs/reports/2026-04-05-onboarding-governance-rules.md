# Onboarding Governance Rules

Authoritative inputs:
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-audit.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-ux-rating.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-gap-analysis.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-redesign.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-domain-api-alignment.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-migration-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-implementation-slices.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-validation-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-reconciliation-strategy.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/reports/2026-04-05-onboarding-execution-plan.md`

This document defines the automated governance contract for onboarding implementation slices. It does not change any redesign, migration, validation, reconciliation, execution, or rollout decision.

## 1. Governance Model

### 1.1 Enforcement locations

Governance must live in four places:

1. **GitHub repository rulesets**
   - require pull requests
   - require required status checks
   - block direct pushes to protected branches
   - require reviewer approval from CODEOWNERS
2. **CI workflow**
   - one required workflow dedicated to onboarding governance
   - runs on every pull request touching onboarding-governed paths
3. **PR template**
   - mandatory structured metadata for slice, scope, exclusions, validation, and flags
4. **CODEOWNERS / branch protections**
   - enforce mandatory reviewer groups for backend, API, UI, and migration paths

### 1.2 Automatic vs manual enforcement

**Automatic and merge-blocking**
- slice declaration presence
- slice metadata format
- file-scope validation against allowed paths
- forbidden path detection
- forbidden symbol and pattern detection
- dual-write detection
- legacy wizard usage detection in new slices
- feature-flag guard validation
- migration-safety guard validation
- observability marker validation
- validation-evidence marker validation
- required test-command marker validation
- required workflow success

**Manual but still required**
- reviewer judgment on correctness of attached evidence
- reviewer confirmation that acceptance criteria are actually satisfied
- approver confirmation that slice completion unlocks the next slice

Manual review never replaces automatic enforcement. If CI cannot prove a rule, the rule is incomplete and must be converted into a machine-checkable requirement.

### 1.3 Merge blockers

A merge is blocked when any of the following is true:

- required onboarding governance workflow is not green
- PR body does not match the mandatory onboarding PR template contract
- changed files exceed the declared slice scope
- forbidden patterns are detected
- validation evidence markers are missing
- required reviewers have not approved
- repository ruleset detects missing branch protection requirements

## 2. Slice Boundary Enforcement

### 2.1 Required slice declaration

Every onboarding PR must declare its slice in three places:

1. PR title prefix: `[Onboarding Slice X]`
2. PR body field: `Slice: X`
3. PR label: `onboarding-slice-x`

CI fails if any one of the three is missing or inconsistent.

### 2.2 Slice manifest

CI must validate file scope from a machine-readable slice manifest stored in the repository. The manifest is the authority for:

- allowed path globs
- forbidden path globs
- allowed legacy symbol exceptions
- required validation markers
- required reviewer groups
- required feature flags
- required observability markers

No onboarding PR may bypass the manifest.

### 2.3 Allowed and forbidden scope by slice

The following path rules are mandatory. `PoTool.Tests.Unit/**` entries are allowed only for tests that verify the same slice.

| Slice | Allowed paths | Forbidden paths |
|---|---|---|
| 1 | `PoTool.Api/Persistence/**`, `PoTool.Api/Migrations/**`, `PoTool.Api/Configuration/**`, slice-1 tests | `PoTool.Client/**`, `PoTool.Api/Controllers/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, migration-runner code outside schema registration |
| 2 | `PoTool.Api/Controllers/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, `PoTool.Shared/**`, slice-2 tests | `PoTool.Client/**`, `PoTool.Api/Migrations/**`, migration execution code, cutover/routing code |
| 3 | `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, `PoTool.Shared/**`, slice-3 tests | `PoTool.Client/**`, `PoTool.Api/Migrations/**`, onboarding write handlers, migration execution code |
| 4 | `PoTool.Api/Persistence/**`, `PoTool.Api/Migrations/**`, `PoTool.Api/Services/**`, `PoTool.Api/Handlers/**`, `PoTool.Shared/**`, slice-4 tests | `PoTool.Client/**`, legacy data movement logic, UI routing changes, production trigger code |
| 5 | `PoTool.Api/Services/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Persistence/**`, `PoTool.Shared/**`, slice-5 tests | `PoTool.Client/**`, UI files, startup routing changes, compatibility dual-write code |
| 6 | `PoTool.Api/Controllers/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, `PoTool.Shared/**`, slice-6 tests | `PoTool.Client/**`, legacy wizard files, migration trigger code, cutover routing files |
| 7 | `PoTool.Client/Pages/**`, `PoTool.Client/Components/**`, `PoTool.Client/Services/**`, `PoTool.Client/Models/**`, `PoTool.Client/Validators/**`, slice-7 tests | `PoTool.Api/Persistence/**`, migration code, domain-configuration UI files, legacy wizard write services without redirect/removal intent |
| 8 | `PoTool.Client/Pages/**`, `PoTool.Client/Components/**`, `PoTool.Client/Services/**`, `PoTool.Client/Models/**`, `PoTool.Client/Validators/**`, slice-8 tests | `PoTool.Api/Persistence/**`, migration code, domain-configuration UI files unless required display-only reuse, legacy wizard write services |
| 9 | `PoTool.Client/Pages/**`, `PoTool.Client/Components/**`, `PoTool.Client/Services/**`, `PoTool.Client/Models/**`, `PoTool.Client/Validators/**`, slice-9 tests | `PoTool.Api/Persistence/**`, migration code, cutover routing files, legacy wizard write services |
| 10 | `PoTool.Api/Controllers/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, `PoTool.Shared/**`, slice-10 tests | `PoTool.Client/**`, migration infrastructure files beyond import-path dependencies, old wizard preference logic |
| 11 | `PoTool.Api/Controllers/**`, `PoTool.Api/Handlers/**`, `PoTool.Api/Services/**`, `PoTool.Client/Pages/**`, `PoTool.Client/Services/**`, `PoTool.Client/Components/**`, `PoTool.Shared/**`, slice-11 tests | schema redesign beyond required cutover configuration, new migration execution logic, cleanup deletions reserved for slice 12 |
| 12 | legacy onboarding files targeted for removal, cutover cleanup files, slice-12 tests, CODEOWNERS/rules cleanup files | new onboarding feature work, new migration code, new feature flags |

### 2.4 Universal path restrictions

The following are always forbidden unless the manifest explicitly lists a slice-specific exception:

- changes touching both `PoTool.Client/**` and `PoTool.Api/Persistence/**` in the same onboarding PR before Slice 11
- changes touching both migration execution files and UI files in the same PR
- any PR changing more than one slice label or slice number
- any PR modifying both legacy wizard write files and new onboarding write files after Slice 6 starts

### 2.5 Scope failure behavior

CI must produce one violation per offending file with:

- slice number
- offending file path
- matched forbidden rule
- closest allowed path set for that slice

The workflow fails immediately after reporting all violations.

## 3. PR Template

### 3.1 Mandatory onboarding PR template block

The existing `/home/runner/work/PoCompanion/PoCompanion/.github/pull_request_template.md` must be extended with a mandatory onboarding block containing these exact fields:

- `Slice:`
- `Scope:`
- `Not Changed:`
- `Validation Evidence:`
- `Test Commands:`
- `Manual Verification:`
- `Observability Evidence:`
- `Feature Flags:`
- `Legacy Write Impact:`

### 3.2 Required content rules

CI must fail if:

- `Slice:` is missing or not `1` through `12`
- `Scope:` is empty
- `Not Changed:` is empty
- `Validation Evidence:` is empty
- `Test Commands:` is empty for slices that change code
- `Manual Verification:` is empty
- `Observability Evidence:` is empty for slices 2–11
- `Feature Flags:` is empty for slices 7–11
- `Legacy Write Impact:` is empty for slices 5–12

### 3.3 Required validation checklist items

The PR template must contain checkboxes for:

- automated tests executed
- manual verification executed
- observability validated
- no unrelated changes
- no dual-write introduced
- feature-flag behavior validated where applicable

CI parses these checkbox markers and fails if required items are absent or unchecked.

## 4. Automated Validation Checks

The onboarding governance workflow must contain these required jobs:

1. **`onboarding-pr-contract`**
   - validates title prefix
   - validates body fields
   - validates slice label
2. **`onboarding-slice-scope`**
   - compares changed files with slice manifest
   - blocks out-of-scope files
3. **`onboarding-dual-write-guard`**
   - scans for legacy + new write combinations
   - scans for legacy wizard service usage in new slices
4. **`onboarding-feature-flag-guard`**
   - ensures new UI flows remain behind flags
   - ensures flags are not default-enabled prematurely
5. **`onboarding-migration-guard`**
   - blocks migration execution logic outside slices 4–5
   - blocks migration triggers in UI/API entry points
6. **`onboarding-observability-guard`**
   - checks required log/metric markers
7. **`onboarding-validation-evidence`**
   - confirms validation sections and test markers exist in PR body
8. **existing build/test jobs**
   - remain required and continue blocking merge

### 4.1 CI failures that must exist

CI must fail for:

- missing slice declaration
- mismatched title/body/label slice identifiers
- files outside allowed scope
- missing validation evidence markers
- missing test command markers
- unchecked required PR checklist items
- forbidden legacy symbol usage
- direct TFS usage from UI
- validation-bypass patterns
- missing required flag references
- missing required observability markers

### 4.2 Test execution evidence

CI must verify both:

- the PR body names the required commands
- the workflow actually ran those commands in the same commit

If a PR claims tests ran but the workflow matrix does not show those commands, the PR fails.

## 5. Dual-Write Prevention

### 5.1 Merge-blocking dual-write rules

CI must block any diff that causes one code path to write to both:

- legacy onboarding entities and new onboarding entities
- legacy wizard preference state and persisted onboarding status
- legacy wizard services and new onboarding write APIs

### 5.2 Required automated detections

The dual-write guard must scan diffs and changed files for:

- references to both legacy entity writers and new onboarding writers in the same handler/service
- references to both legacy route completion logic and new `OnboardingStatus` write/read authority in the same client flow
- any use of `IOnboardingService`, `OnboardingService`, `IOnboardingWizardState`, `OnboardingWizardState`, or `OnboardingWizard.razor` from slices 7–11 except slice-11 redirect-only removal logic and slice-12 cleanup
- any write-path use of `OnboardingCompleted` or `OnboardingSkipped` after Slice 11

### 5.3 Legacy symbol blocklist

The default blocklist for new slices must include:

- `IOnboardingService`
- `OnboardingService`
- `IOnboardingWizardState`
- `OnboardingWizardState`
- `OnboardingWizard.razor`
- `OnboardingCompleted`
- `OnboardingSkipped`
- `StartupReadinessDto` as onboarding route authority

The only allowed exceptions are:

- Slice 11 redirect/removal logic
- Slice 12 deletion and compatibility cleanup

## 6. Feature Flag Enforcement

### 6.1 Mandatory flag rules

CI must enforce:

- Slice 7 code references `Onboarding.NewConnectionFlowEnabled`
- Slice 8 code references `Onboarding.NewDataSourcesFlowEnabled`
- Slice 9 code references `Onboarding.NewDomainConfigurationFlowEnabled`
- Slice 10 code references `Onboarding.NewImportPathEnabled`
- Slice 11 code references `Onboarding.NewExperienceEnabled` and `Onboarding.LegacyWizardWriteEnabled`

### 6.2 Default-state enforcement

Before Slice 11 cutover, CI must fail if:

- any new onboarding flag is default-enabled in production configuration
- a new onboarding page/route renders without checking its required flag
- `Onboarding.NewExperienceEnabled` is set true while `Onboarding.LegacyWizardWriteEnabled` is also true in the same environment configuration

### 6.3 Flag bypass detection

CI must block:

- unconditional navigation to new onboarding pages
- unconditional invocation of new onboarding write services
- fallback logic that silently executes the new write path when the flag is false

## 7. Migration Safety Enforcement

### 7.1 Allowed migration code locations

Migration execution code is allowed only in Slice 4 or Slice 5 and only in backend infrastructure locations listed in the slice manifest.

### 7.2 Merge-blocking migration rules

CI must fail if:

- migration execution code appears outside slices 4–5
- UI files reference migration jobs, migration runners, or migration triggers
- controllers expose user-triggered migration execution endpoints
- API startup code auto-runs migration execution
- slice 11 or 12 attempts to add new migration logic instead of using the already approved cutover path

### 7.3 Idempotency enforcement

Migration code must declare machine-checkable idempotency markers. CI must require:

- explicit idempotent upsert implementation markers on migration handlers/services
- explicit unit-order declaration for connection → project → team → pipeline → root → binding
- no insert-only logic for external-ID-backed onboarding entities inside migration code

If the required markers are absent, migration code fails CI even if tests pass.

## 8. Observability Enforcement

### 8.1 Required observability by slice

Observability is mandatory for slices 2–11.

- Slices 2, 6, 10: write/read validation logs and failure categorization metrics
- Slice 3: status computation outcome metrics and blocker/warning logs
- Slices 4–5: migration run logs, unit outcome logs, failure metrics, idempotency rerun metrics
- Slices 7–9: feature-flag path selection logs and client-visible failure telemetry hooks
- Slice 11: cutover status logs, legacy-write-disabled metrics, old-write-detected alert hooks

### 8.2 CI enforcement

CI must fail when a slice changes governed code but does not add or update:

- required log event definitions
- required metric instrument references
- required alert or query markers listed in the slice manifest

### 8.3 Allowed observability pattern

The manifest must map each slice to required identifiers. CI checks for those exact identifiers in the diff and in the resulting codebase. “We will add logs later” is never allowed.

## 9. Forbidden Patterns

The following are always merge-blocking:

1. **Cross-slice imports/usages**
   - code in one slice directly depends on code declared for a later slice
2. **Direct DB access outside persistence rules**
   - UI or non-approved layers accessing `DbContext` directly
   - persistence logic introduced in client code
3. **Validation bypass**
   - direct write handlers that skip validator or validation service invocation
   - optimistic success returned before persistence
4. **UI calling TFS directly**
   - `HttpClient` or TFS SDK usage in onboarding UI for TFS authority
5. **Legacy wizard authority after cutover slices**
   - client preference gating used as onboarding authority
   - wizard-session state used as completion authority
6. **Mixed legacy/new write logic**
   - any bridge that updates both old and new onboarding stores
7. **Flag bypass**
   - new onboarding routes or writes executing without required flag checks
8. **Migration trigger leakage**
   - migration execution exposed through normal user flows

## 10. Enforcement Output

### 10.1 Required CI message format

Every governance failure must report:

- rule ID
- slice number
- file path
- line number when available
- why the rule failed
- exact remediation step

### 10.2 Required message style

Messages must be:

- specific
- non-ambiguous
- one violation per message
- fix-oriented

### 10.3 Example output structure

The workflow must emit failures in this structure:

- `RULE: ONB-SCOPE-003`
- `SLICE: 7`
- `FILE: PoTool.Api/Persistence/...`
- `WHY: Slice 7 permits UI-only paths; persistence changes belong to Slice 1, 4, 5, or 6.`
- `FIX: Remove the persistence change from this PR or move it into the correct slice PR.`

Generic messages such as “scope violation” or “governance failed” are not allowed.

## 11. Governance Lifecycle

### 11.1 Introduction point

Governance rules are introduced before Slice 1 starts:

- PR contract enforcement
- slice manifest enforcement
- reviewer enforcement
- forbidden-pattern baseline scans

No onboarding implementation PR may merge before these rules are active.

### 11.2 Tightening points

- **Before Slice 4:** enable migration guard and observability guard
- **Before Slice 6:** enable dual-write guard for new write APIs
- **Before Slice 7:** enable feature-flag guard for all new UI flows
- **Before Slice 11:** tighten legacy blocklists so legacy onboarding authority is treated as merge-blocking outside cutover/removal code
- **Before Slice 12:** require explicit flag-removal proof and zero remaining legacy-write references

### 11.3 Post-cleanup simplification

After Slice 12 is complete:

- remove slice-specific onboarding exceptions for legacy wizard symbols
- keep permanent architecture guards:
  - no UI-to-TFS direct access
  - no validation bypass
  - no mixed write systems
  - no client-side onboarding authority
- archive slice manifest entries for traceability, but keep the permanent forbidden-pattern rules active

No governance rule may be removed before cleanup if that removal would allow a blocked onboarding risk to re-enter the repository.
