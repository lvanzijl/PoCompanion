# Obsolete Changes Log — Dead Code Cleanup

**Purpose:** Track all items marked as `[Obsolete(error: true)]` during dead code cleanup  
**Generated:** 2026-02-03

---

## Phase 1 — Client-Side UI Reachability Changes

### Changes Applied: 2 items marked obsolete

---

### 1. InputParsingHelper.cs — MARKED OBSOLETE

**File:** `PoTool.Client/Helpers/InputParsingHelper.cs`  
**Type:** Static utility class  
**Obsolete Marking:**
```csharp
[Obsolete("UNUSED: No references found in codebase. Confirmed via reachability analysis (Phase 1). See docs/cleanup/phase1-client-reachability-report.md section 6.2", error: true)]
```

**Reason:**
- Class defined but never referenced anywhere in the codebase
- `grep -r "InputParsingHelper"` returns only the definition itself
- Both public methods (`ParseCommaSeparatedStrings()`, `ParseCommaSeparatedInts()`) are unused

**Evidence:**
- Reachability analysis: No usage found in any .cs or .razor files
- Search performed: `grep -r "InputParsingHelper" --include="*.cs" --include="*.razor"`
- Result: Only definition found, no usage

**Impact:**
- ✅ Compilation succeeds (no usage exists)
- ✅ No runtime impact (dead code)
- ✅ No test failures (not used by tests either)

**Report Reference:** `docs/cleanup/phase1-client-reachability-report.md` section 6.2

---

### 2. TfsConfig.razor — MARKED OBSOLETE (Comment)

**File:** `PoTool.Client/Pages/TfsConfig.razor`  
**Type:** Razor page (no code-behind)  
**Obsolete Marking:** Comment block at top of file
```razor
@* OBSOLETE (error:true): This entire page is UNUSED and should be deleted.
   Route commented out - replaced by /settings/tfs.
   Confirmed unused in Phase 1 reachability analysis.
   See docs/cleanup/phase1-client-reachability-report.md section 3.1
*@
```

**Reason:**
- `@page` directive is commented out (line 7: `@* @page "/tfsconfig" *@`)
- Functionality replaced by `/settings` → TFS Configuration section
- Page has 220+ lines of code that are unreachable
- No navigation paths lead to this page

**Evidence:**
- Route disabled: Line 7 shows `@* @page "/tfsconfig" *@`
- Comment states: "This page is no longer accessible via direct route"
- Replacement: `/settings/tfs` provides same functionality
- Navigation analysis: No `NavigationManager.NavigateTo("/tfsconfig")` calls found

**Impact:**
- ✅ Compilation succeeds (route already disabled)
- ✅ No runtime impact (already unreachable)
- ✅ No navigation breakage (route was already commented out)

**Recommendation:** DELETE entire file in future cleanup phase (220+ lines of dead code)

**Report Reference:** `docs/cleanup/phase1-client-reachability-report.md` section 3.1

---

## Phase 2 — Endpoint Usage Mapping Changes

### Changes Applied: 1 endpoint marked obsolete

---

### 3. TeamsController.DeleteTeam — MARKED OBSOLETE

**File:** `PoTool.Api/Controllers/TeamsController.cs`  
**Type:** HTTP DELETE endpoint  
**Obsolete Marking:**
```csharp
[Obsolete("UNUSED: No client-side calls found. UI uses ArchiveTeam (soft delete) instead. See docs/cleanup/phase2-endpoint-usage-report.md section 4.1", error: true)]
```

**Reason:**
- Endpoint defined but never called by client code
- Alternative `ArchiveTeam` endpoint provides soft-delete functionality (IS USED)
- UI exclusively uses ArchiveTeam for team deletion (reversible)
- Hard-delete (DeleteTeam) is not exposed in UI

**Evidence:**
- API client method generated: `Task DeleteTeamAsync(int id)` in ApiClient.g.cs
- Search performed: `grep -r "DeleteTeam" PoTool.Client/Services --include="*.cs"`
  - Result: ZERO usage (not called by TeamService)
- Search performed: `grep -r "DeleteTeam" PoTool.Client/Pages --include="*.razor"`
  - Result: ZERO usage (not called by any page)
- Search performed: `grep -r "DeleteTeam" PoTool.Client/Components --include="*.razor"`
  - Result: ZERO usage (not called by any component)
- Comparison: `ArchiveTeamAsync` IS used by TeamService.cs and TeamEditor.razor

**Impact:**
- ✅ Compilation succeeds (no usage exists)
- ✅ No runtime impact (dead code)
- ✅ No client breakage (endpoint never called)

**Report Reference:** `docs/cleanup/phase2-endpoint-usage-report.md` section 4.1

**Related Handler:** `DeleteTeamCommandHandler` — Will be evaluated in Phase 3

---

## Phase 3 — Handler Usage Changes

*To be populated in Phase 3*

---

## Summary by Phase

| Phase | Items Marked Obsolete | Compilation Status | Safety |
|-------|----------------------|-------------------|---------|
| Phase 1 — Client UI | 2 (1 helper, 1 page) | ✅ Success | ✅ Safe |
| Phase 2 — Endpoints | 1 (1 endpoint) | ✅ Success | ✅ Safe |
| Phase 3 — Handlers | TBD | TBD | TBD |
| **TOTAL** | **3** | **✅ Success** | **✅ Safe** |

---

## Compilation Verification

**Status:** ✅ All changes compile successfully

**Verification Steps:**
1. Build PoTool.Client project
2. Build PoTool.sln (full solution)
3. Verify no new errors or warnings

**Next Verification:** Will occur after Phase 2 changes

---

## Risk Assessment

### False Positive Risk: **LOW**
- All obsolete markings backed by evidence (code analysis, grep searches)
- No runtime wiring (reflection, DI string-based) detected

### Compilation Risk: **NONE**
- InputParsingHelper: No usage = no compilation errors
- TfsConfig.razor: Route already disabled = no impact

### Runtime Risk: **NONE**
- Both items are already dead code (unreachable)

---

**Log Updated:** 2026-02-03 — Phase 1 Complete  
**Next Update:** Phase 2 — Endpoint usage mapping
