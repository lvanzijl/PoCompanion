# Code Cleanup Plan

**Date**: 2026-01-21  
**Status**: READY FOR EXECUTION  
**Related**: CODE_QUALITY_REVIEW_REPORT.md

---

## Quick Reference

### What to Clean Up

| Category | Count | Priority | Effort | Impact |
|----------|-------|----------|--------|--------|
| Backup files (.old, .backup) | 4 | 🔴 HIGH | 5 min | Visual clarity |
| Deprecated methods | 2 | 🔴 HIGH | 1 hour | Code quality |
| Temporary MD files | 24 | ⚠️ MEDIUM | 15 min | Project organization |
| RealTfsClient refactoring | 1 file | 🔴 HIGH | 2-3 weeks | Maintainability |
| BattleshipMockDataFacade | 1 file | ⚠️ MEDIUM | 1 week | Nice to have |

---

## Phase 1: Quick Cleanup (30 minutes) 🔴

**Goal**: Remove clutter with zero risk

### Step 1.1: Delete Backup Files (5 minutes)

```bash
# Navigate to repository root
cd /home/runner/work/PoCompanion/PoCompanion

# Delete old test files
rm PoTool.Tests.Unit/Services/BacklogHealthCalculationServiceTests.cs.old
rm PoTool.Tests.Unit/Services/WorkItemFilteringServiceTests.cs.old

# Delete old swagger files
rm PoTool.Client/swagger.json.old
rm PoTool.Client/swagger.json.backup

# Verify deletion
git status
```

**Expected Output**: 4 files deleted

### Step 1.2: Archive Temporary Documentation (15 minutes)

```bash
# Create archive directory
mkdir -p docs/archive/historical-summaries

# Move temporary status/summary files
mv CATEGORY_ICONS_FIX_SUMMARY.md docs/archive/historical-summaries/
mv CHANGES_SUMMARY.md docs/archive/historical-summaries/
mv COMPLIANCE_ISSUES_REPORT.md docs/archive/historical-summaries/
mv CONTEXT_PACK_DELIVERY_SUMMARY.md docs/archive/historical-summaries/
mv DEMO_SCRIPT.md docs/archive/historical-summaries/
mv EF_CONCURRENCY_FIX_COMPLETE.md docs/archive/historical-summaries/
mv EF_CONCURRENCY_FIX_IMPLEMENTATION_SUMMARY.md docs/archive/historical-summaries/
mv EF_CONCURRENCY_FIX_REVIEWER_NOTES.md docs/archive/historical-summaries/
mv EF_CONCURRENCY_FIX_REVIEWER_NOTES_FINAL.md docs/archive/historical-summaries/
mv EF_CONCURRENCY_SOLUTION_PLAN.md docs/archive/historical-summaries/
mv EXECUTIVE_SUMMARY.md docs/archive/historical-summaries/
mv FIX_SUMMARY.md docs/archive/historical-summaries/
mv IMPLEMENTATION_NOTES_ORPHAN_PRODUCTS.md docs/archive/historical-summaries/
mv IMPLEMENTATION_STATUS.md docs/archive/historical-summaries/
mv IMPLEMENTATION_SUMMARY.md docs/archive/historical-summaries/
mv NTLM_AUTHENTICATION_FINAL_FIX.md docs/archive/historical-summaries/
mv NTLM_AUTHENTICATION_FIX.md docs/archive/historical-summaries/
mv NTLM_FIX_FINAL.md docs/archive/historical-summaries/
mv NTLM_FIX_SUMMARY.md docs/archive/historical-summaries/
mv NTLM_WIQL_URL_FIX.md docs/archive/historical-summaries/
mv ONBOARDING_WIZARD_UPDATE_STATUS.md docs/archive/historical-summaries/
mv PRODUCT_OWNER_PERSPECTIVE.md docs/archive/historical-summaries/
mv RELEASE_PLANNING_FEATURE_STATUS.md docs/archive/historical-summaries/
mv REPOSITORY_REVIEW_REPORT.md docs/archive/historical-summaries/
mv RULE_CONTRADICTIONS.md docs/archive/historical-summaries/
mv SECURITY_AUDIT_REPORT.md docs/archive/historical-summaries/
mv SECURITY_EXECUTIVE_SUMMARY.md docs/archive/historical-summaries/
mv SECURITY_FIX_IMPLEMENTATION_PLAN.md docs/archive/historical-summaries/
mv TREEGRID_IMPLEMENTATION_SUMMARY.md docs/archive/historical-summaries/
mv VIEW_READINESS_REPORT.md docs/archive/historical-summaries/
mv WORK_ITEM_RETRIEVAL_FIX_NOTES.md docs/archive/historical-summaries/
mv EXPLORATORY_TESTING_AUTOMATION.md docs/archive/historical-summaries/
mv EXPLORATORY_TESTING_README.md docs/archive/historical-summaries/

# Verify
ls -la docs/archive/historical-summaries/ | wc -l
```

**Expected Output**: 33 files moved to archive

### Step 1.3: Update .gitignore (5 minutes)

```bash
# Add explicit ignore rules for swagger backups
cat >> PoTool.Client/.gitignore << 'EOF'

# Swagger backup files (auto-generated)
swagger.json.backup
swagger.json.old
EOF

# Verify
cat PoTool.Client/.gitignore
```

### Step 1.4: Commit Phase 1 (5 minutes)

```bash
git add -A
git status
git commit -m "chore: clean up orphaned files and temporary documentation

- Delete 4 backup/old files (.old, .backup)
- Archive 33 temporary status/summary MD files to docs/archive/
- Add .gitignore rules for swagger backup files
- Improves repository organization and first impressions"

git push
```

**Phase 1 Complete**: ✅ Repository is now cleaner and more professional

---

## Phase 2: Remove Deprecated Code (2 hours) 🔴

**Goal**: Remove unused deprecated methods with verification

### Step 2.1: Remove ConfigureAuthenticationAsync (30 minutes)

**Location**: `PoTool.Api/Services/RealTfsClient.cs`, lines 1684-1700

```bash
# Verify no usages exist
cd /home/runner/work/PoCompanion/PoCompanion
grep -r "ConfigureAuthenticationAsync" --include="*.cs" .
# Expected: Only definition in RealTfsClient.cs
```

**Manual Edit**:
1. Open `PoTool.Api/Services/RealTfsClient.cs`
2. Delete lines 1684-1700 (entire method + deprecation comment)
3. Save file

**Verification**:
```bash
# Build API project
dotnet build PoTool.Api/PoTool.Api.csproj

# Run relevant tests
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --filter "TfsClient"

# Expected: All tests pass
```

### Step 2.2: Remove PullRequestService.SyncAsync (30 minutes)

**Location**: `PoTool.Client/Services/PullRequestService.cs`, lines 65-76

```bash
# Verify no usages exist
grep -r "\.SyncAsync" --include="*.cs" --include="*.razor" PoTool.Client/
# Expected: Only definition in PullRequestService.cs
```

**Manual Edit**:
1. Open `PoTool.Client/Services/PullRequestService.cs`
2. Delete lines 65-76 (entire method + obsolete attribute + comment)
3. Save file

**Verification**:
```bash
# Build Client project
dotnet build PoTool.Client/PoTool.Client.csproj

# Run relevant tests
dotnet test PoTool.Tests.Blazor/PoTool.Tests.Blazor.csproj

# Expected: All tests pass
```

### Step 2.3: Run Full Test Suite (30 minutes)

```bash
# Run all tests to ensure nothing broke
dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj
dotnet test PoTool.Tests.Integration/PoTool.Tests.Integration.csproj
dotnet test PoTool.Tests.Blazor/PoTool.Tests.Blazor.csproj

# Expected: All tests pass
```

### Step 2.4: Commit Phase 2 (30 minutes)

```bash
git add -A
git status
git diff --cached  # Review changes

git commit -m "refactor: remove deprecated methods

- Remove ConfigureAuthenticationAsync from RealTfsClient (replaced by GetAuthenticatedHttpClient)
- Remove PullRequestService.SyncAsync (API endpoint no longer exists)
- Both methods were marked [Obsolete] and no longer in use
- All tests pass"

git push
```

**Phase 2 Complete**: ✅ Deprecated code removed, codebase cleaner

---

## Phase 3: RealTfsClient Refactoring (2-3 weeks) 🔴

**Goal**: Break down 4,624-line god class into maintainable services

⚠️ **WARNING**: This is a major refactoring. Do NOT start until:
- Feature work is stable
- Team is aligned on the plan
- Sufficient testing resources available
- No other large refactorings in progress

### Preparation (Week 0)

1. **Create Feature Branch**
   ```bash
   git checkout -b refactor/split-real-tfs-client
   ```

2. **Spike: Analyze Dependencies**
   - List all methods in RealTfsClient
   - Map method dependencies
   - Identify shared state
   - Design service boundaries

3. **Write RFC/Design Doc**
   - Propose service split
   - Define interfaces
   - Plan DI changes
   - Get team approval

### Execution (Week 1-2)

#### Week 1: Create Services
- **Day 1-2**: TfsAuthenticationService
  - Interface: `ITfsAuthenticationService`
  - Methods: `GetAuthenticatedHttpClient()`, authentication logic
  - Tests: 20+ unit tests for PAT/NTLM scenarios

- **Day 3-4**: TfsWorkItemService  
  - Interface: `ITfsWorkItemService`
  - Methods: Work item CRUD, hierarchy, revisions
  - Tests: 30+ unit tests for work item operations

- **Day 5**: TfsPullRequestService
  - Interface: `ITfsPullRequestService`
  - Methods: PR retrieval, iterations, comments
  - Tests: 15+ unit tests for PR operations

#### Week 2: Complete Services & Integrate
- **Day 6**: TfsPipelineService
  - Interface: `ITfsPipelineService`
  - Methods: Pipeline/run retrieval
  - Tests: 10+ unit tests

- **Day 7**: TfsVerificationService
  - Interface: `ITfsVerificationService`
  - Methods: Capability verification
  - Tests: 20+ unit tests

- **Day 8-9**: Integration
  - Update DI registrations in Program.cs
  - Update handler dependencies
  - Update MockTfsClient to delegate to new services
  - Run full test suite

- **Day 10**: Testing & Fixes
  - Fix failing tests
  - Manual testing
  - Performance benchmarks

### Verification (Week 3)

- **Day 11-12**: Code Review
  - PR with detailed description
  - Architecture review
  - Code quality review

- **Day 13-14**: Final Testing
  - Regression testing
  - Integration testing
  - Manual smoke testing

- **Day 15**: Merge & Deploy
  - Merge to main
  - Deploy to staging
  - Monitor for issues

### Success Metrics

| Metric | Before | Target | Measured |
|--------|--------|--------|----------|
| RealTfsClient LOC | 4,624 | 0 (deleted) | TBD |
| Largest Service LOC | N/A | <800 | TBD |
| Test Coverage | ~70% | >80% | TBD |
| Build Time | Baseline | No regression | TBD |

---

## Phase 4: Optional Improvements (Future)

### 4.1: Refactor BattleshipMockDataFacade (1 week)

**Current**: 1,351 lines in single file  
**Target**: 3-4 domain facades (300-400 lines each)

**Plan**:
```
BattleshipMockDataFacade
  ↓ Split into:
- BattleshipWorkItemFacade
- BattleshipPullRequestFacade
- BattleshipPipelineFacade
- BattleshipMockDataOrchestrator (thin coordinator)
```

**Priority**: MEDIUM (dev/test code, less critical)

### 4.2: Extract MockTfsClient Base (1 week)

**Current**: Duplication between API and Integration test versions  
**Target**: Shared base class with common logic

**Plan**:
```csharp
// New: PoTool.Core/Testing/MockTfsClientBase
public abstract class MockTfsClientBase : ITfsClient
{
    // Common transformation logic
    // Common error handling
    // Common retry logic
}

// Updated: PoTool.Api/Services/MockTfsClient
public class MockTfsClient : MockTfsClientBase
{
    // Uses BattleshipMockDataFacade
}

// Updated: PoTool.Tests.Integration/Support/MockTfsClient
public class MockTfsClient : MockTfsClientBase
{
    // Uses file-based test data
}
```

**Priority**: LOW (both work fine, infrequent changes)

---

## Rollback Plans

### Phase 1-2 Rollback
```bash
# If issues found after Phase 1-2
git revert <commit-hash>
git push
```

### Phase 3 Rollback
```bash
# If major issues found during Phase 3
git checkout main
git branch -D refactor/split-real-tfs-client

# Or if already merged:
git revert <merge-commit-hash>
git push
```

---

## Communication Plan

### Before Starting
- [ ] Review this plan with team
- [ ] Get approval for Phase 1-2 (low risk)
- [ ] Schedule Phase 3 (high impact)
- [ ] Assign ownership

### During Execution
- [ ] Daily updates on progress
- [ ] Flag blockers immediately
- [ ] Share test results
- [ ] Coordinate with other developers

### After Completion
- [ ] Demo improvements
- [ ] Update documentation
- [ ] Share lessons learned
- [ ] Close tracking issue

---

## Next Steps

1. ✅ **Immediate**: Execute Phase 1 (30 minutes)
2. ✅ **This Week**: Execute Phase 2 (2 hours)
3. ⏸️ **Plan**: Schedule Phase 3 for next sprint
4. ⏸️ **Future**: Consider Phase 4 based on team capacity

**Start Date**: 2026-01-21  
**Owner**: TBD  
**Tracking**: Create GitHub issue to track progress

---

**End of Cleanup Plan**
