# Compliance Issues Report — PO Companion
**Date**: 2026-01-01  
**Session**: Comprehensive codebase compliance check against copilot-instructions and rulesets

---

## Executive Summary

This document lists compliance violations discovered during a comprehensive audit of the codebase against:
- `.github/copilot-instructions.md` (authoritative entry point)
- `docs/ARCHITECTURE_RULES.md`
- `docs/PROCESS_RULES.md`
- `docs/UI_RULES.md`
- `docs/COPILOT_ARCHITECTURE_CONTRACT.md`
- `docs/PAT_STORAGE_BEST_PRACTICES.md`
- `docs/TFS_INTEGRATION_RULES.md`

**Current Status**: ✅ Build succeeds with zero warnings/errors after fixing small issues.

**Remaining Critical Issues**: 3 major violations that require separate, focused sessions to address.

---

## Critical Violations (Require Separate Sessions)

### ARCH-1: Client Layer Directly References Core Layer

**Severity**: 🔴 CRITICAL  
**Rule Violated**: ARCHITECTURE_RULES.md Section 2.3, COPILOT_ARCHITECTURE_CONTRACT.md  
**Session Estimate**: Multiple sessions (large refactoring)

#### Problem Description

The `PoTool.Client` project has a direct project reference to `PoTool.Core`:

```xml
<!-- PoTool.Client/PoTool.Client.csproj -->
<ItemGroup>
  <ProjectReference Include="..\PoTool.Core\PoTool.Core.csproj" />
</ItemGroup>
```

The Client layer is using the following Core components directly:

1. **Business Logic Classes**:
   - `PoTool.Core.WorkItems.Filtering.WorkItemFilterer` (used in `WorkItemFilteringService.cs`)
   - `PoTool.Core.Health.BacklogHealthCalculator` (used in `BacklogHealthCalculationService.cs`)

2. **Interfaces**:
   - `PoTool.Core.Contracts.IClipboardService` (used in `ClipboardService.cs`)
   - `PoTool.Core.Contracts.ITfsClient` (referenced but should not be)

3. **Exceptions**:
   - `PoTool.Core.Exceptions.*` (used throughout Client for error handling)

#### Architecture Rule Statement

From ARCHITECTURE_RULES.md Section 2.3:

> Frontend:
> - MUST be Blazor WebAssembly (Razor class library)
> - MUST communicate exclusively via:
>   - HTTP Web API
>   - SignalR
> - MUST NOT access TFS directly
> - MUST NOT contain business logic
>
> Frontend MUST NOT:
> - Call backend services directly (even in-process)
> - Store sensitive data locally
> - Depend on backend runtime hosting model

#### Impact

This violation means:
- Business logic is duplicated between Client and API layers
- Client and API are tightly coupled
- Cannot deploy API separately from Client
- Violates the "backend can be deployed standalone" architectural invariant (ARCHITECTURE_RULES.md Section 15, item 6)
- Makes testing more difficult (Client tests need Core logic)

#### Affected Files

**Client services using Core directly**:
- `PoTool.Client/Services/WorkItemFilteringService.cs` → Uses `WorkItemFilterer` from Core
- `PoTool.Client/Services/BacklogHealthCalculationService.cs` → Uses `BacklogHealthCalculator` from Core
- `PoTool.Client/Services/ClipboardService.cs` → Implements Core's `IClipboardService`
- `PoTool.Client/Services/ErrorMessageService.cs` → Uses Core exceptions
- `PoTool.Client/Program.cs` → Registers Core services in Client DI

**Components using Core**:
- `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor` → Uses Core exceptions
- `PoTool.Client/Components/WorkItems/SubComponents/WorkItemToolbar.razor` → Uses Core contracts
- `PoTool.Client/Components/WorkItems/SubComponents/WorkItemDetailPanel.razor` → Uses Core contracts
- `PoTool.Client/Pages/TfsConfig.razor` → Uses Core exceptions

#### Recommended Fix

Three architectural options:

**Option A: Move business logic to API, expose via endpoints**
- Move `WorkItemFilterer` and `BacklogHealthCalculator` to API layer
- Create API endpoints for filtering and health calculation
- Client calls these endpoints via generated API client
- **Pros**: Clean separation, true frontend/backend split
- **Cons**: More API calls, potential performance impact

**Option B: Move business logic to shared DTO library**
- Create new `PoTool.Shared` project with DTOs and pure domain models only
- Move calculation logic to API
- Client only uses shared DTOs for data transfer
- **Pros**: Clean separation, explicit shared types
- **Cons**: More projects to manage

**Option C: Accept exceptions as shared**
- Keep exceptions in Core, allow both API and Client to reference them
- Move business logic (filtering, calculation) to API only
- Client uses API endpoints for all business operations
- **Pros**: Pragmatic, allows shared error types
- **Cons**: Still violates strict layering

**Recommended**: **Option A** - This best aligns with the architectural rules and maintains the "backend can be deployed standalone" requirement.

#### Files to Create/Modify

1. **New API Endpoints** (if choosing Option A):
   - `PoTool.Api/Controllers/FilteringController.cs` - Expose filtering logic
   - `PoTool.Api/Controllers/HealthCalculationController.cs` - Expose health calculations

2. **Client Changes**:
   - Remove Core project reference from `PoTool.Client.csproj`
   - Modify `WorkItemFilteringService.cs` to call API instead of Core
   - Modify `BacklogHealthCalculationService.cs` to call API instead of Core
   - Update component imports to remove Core namespaces

3. **Verification**:
   - Ensure Client project builds without Core reference
   - All existing integration tests still pass
   - UI functionality remains unchanged

#### Acceptance Criteria

- [ ] `PoTool.Client.csproj` does NOT reference `PoTool.Core.csproj`
- [ ] Client code does NOT use `using PoTool.Core` (except for shared DTOs if Option B)
- [ ] All business logic execution happens via API calls
- [ ] All existing features work unchanged
- [ ] All tests pass
- [ ] Build succeeds with no warnings

---

### PROC-1: Missing CI Guardrail for Sync-Over-Async Patterns

**Severity**: 🔴 CRITICAL  
**Rule Violated**: PROCESS_RULES.md Section 13.1  
**Session Estimate**: 1-2 sessions

#### Problem Description

PROCESS_RULES.md Section 13.1 mandates:

> The repository MUST include a CI guardrail that prevents reintroduction of blocking-wait patterns in the Blazor WebAssembly client.
>
> - CI MUST scan **only** the `PoTool.Client` directory.
> - CI MUST fail the build if any forbidden sync-over-async patterns are detected.
>
> #### Forbidden patterns (string-based detection is sufficient)
> - `.Result`
> - `.Wait(`
> - `GetAwaiter().GetResult`
> - `AsTask().Result`
> - `AsTask().Wait`

Currently, **no such guardrail exists**.

#### Current State

✅ **Good News**: No sync-over-async violations currently exist in `PoTool.Client`
- Grep search for `.Result`, `.Wait(`, `GetAwaiter().GetResult` found no matches
- The one `.Result` found was just a property name (`Results`), not a blocking call

However, without a CI check, these violations could be reintroduced accidentally.

#### Impact

Without this guardrail:
- Developers could accidentally introduce blocking code in Blazor WebAssembly
- Blocking in single-threaded WASM runtime causes UI freezes and deadlocks
- Process rules are not enforced automatically
- Manual code review is the only defense

#### Recommended Implementation

Create a PowerShell or Bash script that:
1. Scans only `PoTool.Client/**/*.cs` and `PoTool.Client/**/*.razor`
2. Uses `grep` or equivalent to search for forbidden patterns
3. Excludes comments and string literals (to avoid false positives)
4. Exits with error code 1 if any patterns are found
5. Is integrated into CI workflow

**Script location**: `.github/scripts/check-sync-over-async.sh`

**Example implementation**:

```bash
#!/bin/bash
set -e

echo "Checking for sync-over-async patterns in PoTool.Client..."

PATTERNS=(
    '\.Result[^a-zA-Z]'  # .Result (not followed by letter)
    '\.Wait\('
    'GetAwaiter\(\)\.GetResult'
    'AsTask\(\)\.Result'
    'AsTask\(\)\.Wait'
)

FOUND_VIOLATIONS=0

for pattern in "${PATTERNS[@]}"; do
    echo "Searching for pattern: $pattern"
    if grep -rn --include="*.cs" --include="*.razor" -E "$pattern" PoTool.Client/; then
        echo "❌ Found forbidden pattern: $pattern"
        FOUND_VIOLATIONS=1
    fi
done

if [ $FOUND_VIOLATIONS -eq 1 ]; then
    echo ""
    echo "❌ FAILED: Sync-over-async patterns detected in PoTool.Client"
    echo "See ARCHITECTURE_RULES.md Section 8 and PROCESS_RULES.md Section 13"
    exit 1
else
    echo "✅ PASSED: No sync-over-async patterns found"
    exit 0
fi
```

#### Files to Create/Modify

1. **New Script**:
   - `.github/scripts/check-sync-over-async.sh` - Detection script
   - `.github/scripts/check-sync-over-async.ps1` - Windows version (optional)

2. **CI Workflow Modification**:
   - Modify `build.yml` (once re-enabled) to call the script
   - Add step before or after build step

3. **Documentation**:
   - Add script usage to `.github/workflows/README.md`
   - Document how to run locally in dev workflow

#### Acceptance Criteria

- [ ] Script exists and is executable
- [ ] Script correctly detects all forbidden patterns
- [ ] Script only scans `PoTool.Client` directory
- [ ] Script has no false positives on current codebase
- [ ] CI workflow calls script and fails build on violations
- [ ] Documentation updated

---

### PROC-2: All CI Workflows Are Disabled

**Severity**: 🔴 CRITICAL  
**Rule Violated**: PROCESS_RULES.md (general enforcement)  
**Session Estimate**: 1 session (investigation + re-enable)

#### Problem Description

All GitHub Actions workflow files have `.disabled` extension:
- `.github/workflows/build.yml.disabled`
- `.github/workflows/codeql.yml.disabled`
- `.github/workflows/exploratory-tests.yml.disabled`
- `.github/workflows/release.yml.disabled`

This means:
- No automated builds on PRs or main
- No automated tests
- No automated security scanning (CodeQL)
- No automated releases
- Process rules cannot be enforced automatically

#### Investigation Needed

Before re-enabling, need to determine:
1. **Why were workflows disabled?**
   - Check git history for commits that disabled them
   - Check for issues or discussions explaining the decision
   - Were there failing workflows?

2. **What needs to be fixed before re-enabling?**
   - Do workflows still work with current .NET 10?
   - Do all tests pass?
   - Are there infrastructure dependencies missing?

3. **Which workflows should be re-enabled first?**
   - Priority: `build.yml` (CI/CD)
   - Then: `codeql.yml` (security)
   - Then: `exploratory-tests.yml` (E2E tests)
   - Finally: `release.yml` (deployment)

#### Current Workflow Contents

**build.yml** (disabled):
- Runs on: push to main, PRs to main
- Steps: checkout, setup .NET 10, restore, build, publish
- **Issue**: References `PoTool.App` project which doesn't exist (should be `PoTool.Api`)

**codeql.yml** (disabled):
- CodeQL security analysis
- Critical for PROCESS_RULES.md enforcement

**exploratory-tests.yml** (disabled):
- Playwright automated exploratory tests
- Needs application running first

**release.yml** (disabled):
- Automated release on version tags
- Creates GitHub releases with artifacts

#### Files to Investigate/Modify

1. **Investigate**:
   - Git history: `git log --all --oneline -- .github/workflows/*.disabled`
   - Check for related issues/discussions

2. **Fix `build.yml`**:
   - Change `PoTool.App/PoTool.App.csproj` → `PoTool.Api/PoTool.Api.csproj`
   - Verify .NET 10 setup works
   - Test locally before enabling

3. **Re-enable workflows**:
   - Rename `.disabled` files to remove extension
   - Start with `build.yml` only
   - Monitor first run, fix issues
   - Enable others incrementally

#### Acceptance Criteria

- [ ] Root cause of disabling understood and documented
- [ ] `build.yml` fixed and working
- [ ] At least `build.yml` re-enabled and passing
- [ ] `codeql.yml` re-enabled (for security compliance)
- [ ] All enabled workflows pass on main branch
- [ ] README updated with CI status

---

## Medium Priority Issues (Future Enhancement)

### PAT Storage Compliance Review

**Status**: ⚠️ UNKNOWN - Needs manual code review  
**Affected**: `docs/PAT_STORAGE_BEST_PRACTICES.md` compliance

The PAT storage best practices document defines specific requirements for credential handling:
- PAT MUST be client-side only (never server-side)
- Server MUST NOT persist PAT to database
- Browser storage MUST use encryption or session-only storage
- Content Security Policy (CSP) required for XSS protection

**Needs Investigation**:
1. How is PAT currently stored in Client?
2. Does API persist PAT anywhere? (should be NO)
3. Is browser storage encrypted?
4. Is CSP implemented?
5. Is there session timeout for PAT?

**Files to Review**:
- `PoTool.Client/Services/*Storage*.cs` (if exists)
- `PoTool.Api/Services/TfsConfigurationService.cs`
- Database migrations for PAT fields
- Startup configuration for CSP

---

## Issues Fixed in Current Session ✅

### ARCH-2: Missing TreatWarningsAsErrors (FIXED)

**Files Modified**:
- `PoTool.Tests.Blazor/PoTool.Tests.Blazor.csproj` - Added `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `PoTool.Tests.AutomatedExploratory/PoTool.Tests.AutomatedExploratory.csproj` - Added `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`

**Verification**: ✅ Build now treats all warnings as errors in all projects

### CODE-1: Test Assertion Warnings (FIXED)

**Files Modified** (13 test files):
- Replaced `Assert.IsFalse(x.Contains(y))` → `Assert.DoesNotContain(y, x)`
- Replaced `Assert.AreEqual(count, collection.Count)` → `Assert.HasCount(count, collection)`
- Replaced `Assert.IsTrue(collection.Count > 0)` → `Assert.IsNotEmpty(collection)`
- Replaced `Assert.IsTrue(x >= y)` → `Assert.IsGreaterThanOrEqualTo(x, y)`
- Fixed nullability warnings in mock setups
- Added test parallelization configuration

**Verification**: ✅ Build now succeeds with **0 warnings, 0 errors**

---

## Verification Checklist

- [x] Core layer has no infrastructure dependencies
- [x] No MediatR usage (using approved Mediator library)
- [x] Integration tests use Reqnroll + MSTest
- [x] All projects have `TreatWarningsAsErrors=true`
- [x] Build succeeds with 0 warnings, 0 errors
- [x] No sync-over-async patterns currently in Client
- [ ] **BLOCKER**: Client does not reference Core (ARCH-1)
- [ ] **BLOCKER**: CI guardrails exist (PROC-1)
- [ ] **BLOCKER**: CI workflows enabled (PROC-2)
- [ ] PAT storage follows best practices
- [ ] TFS integration follows integration rules

---

## Recommendations

### Immediate Next Steps (Priority Order)

1. **Session 1**: Fix PROC-2 (Re-enable CI workflows)
   - Investigate why disabled
   - Fix `build.yml` project reference
   - Re-enable and verify

2. **Session 2**: Implement PROC-1 (Sync-over-async guardrail)
   - Create detection script
   - Integrate into CI
   - Test and verify

3. **Session 3+**: Address ARCH-1 (Client→Core refactoring)
   - Decide on architectural approach (Options A/B/C)
   - Plan incremental refactoring
   - Execute refactoring in small, reviewable chunks
   - This will require multiple sessions

### Long-term Maintenance

- **Weekly**: Run compliance check script (once created)
- **Per PR**: Code review against architectural rules
- **Per Sprint**: Review architectural invariants (ARCHITECTURE_RULES.md Section 15)
- **Quarterly**: Full compliance audit like this one

---

## Conclusion

The codebase is **generally well-structured** with good separation of concerns in most areas. The major violations are:

1. **Architectural**: Client→Core coupling (requires refactoring)
2. **Process**: Missing CI enforcement (requires tooling)

Both are fixable but require dedicated effort. The fixes in this session ensure that the **code quality foundations** are solid (warnings-as-errors, modern test assertions), which will make the larger refactorings safer.

All future work should enforce these rules through:
- Automated CI checks
- Code review checklists
- Documentation references
- Developer education

---

**Report End**
