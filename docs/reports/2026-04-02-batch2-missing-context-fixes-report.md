# Batch 2 — Missing Context Fixes Report

## 1. Summary
- Routes fixed:
  - `/home/bugs/detail`
  - `/home/validation-queue`
  - `/home/validation-fix`
- Are all missing-context issues resolved?
  - `/home/bugs/detail`: yes
  - `/home/validation-queue`: yes
  - `/home/validation-fix`: yes
- Remaining edge cases:
  - The validation routes still depend on cache-backed data for valid-context loading. In the current mock environment that means valid URLs can still land in a cache-not-ready recovery state, but they no longer show raw transport errors or misleading screens.
  - `Bug Detail` still has no dedicated upstream in-app navigation entry in the current UI; it now fails safely and points users back to `Bug Insights`.

---

## 2. Route analysis and fixes

### `/home/bugs/detail`

**Required context**
- Required parameters:
  - `bugId`
- Minimum valid input:
  - a positive integer bug ID
- Invalid input:
  - missing `bugId`
  - non-numeric `bugId`
  - zero/negative `bugId`
  - numeric ID that does not resolve to a bug work item
- Safe default:
  - no; the route now guides the user back to `Bug Insights`

**Original behavior**
- Missing `bugId` showed a warning, but invalid `bugId=abc` was treated exactly the same as missing context.
- Any positive numeric `bugId` rendered hardcoded sample data instead of real bug data, which was misleading under direct URL access.
- The route relied on local sample data rather than verifying that the requested bug actually existed.

**Root cause**
- Exact cause:
  - query parsing collapsed invalid bug IDs to `null`
  - the page never fetched real data from the backend
  - any numeric ID was accepted and mapped to sample placeholders
- Where in code:
  - `PoTool.Client/Pages/Home/BugDetail.razor`
  - `ParseQueryParameters()` only parsed the raw query string
  - `LoadBugData()` filled static sample values without validating the bug ID

**Fix applied**
- Replaced sample-data loading with a real backend lookup through `WorkItemService.GetByTfsIdAsync(...)`.
- Added explicit missing-context and invalid-context checks for `bugId`.
- Added safe recovery handling for not-found and cache-not-ready API responses.
- Replaced the misleading editable section with a clear read-only action panel pointing users to `Bugs Triage`.
- Reused the shared guided recovery UI in `PoTool.Client/Components/Common/GuidedRecoveryPanel.razor`.

**New behavior**
- Missing context:
  - shows “Bug context missing” with a clear action to open `Bug Insights`
- Invalid context:
  - shows “Invalid bug ID” or “Bug not found” with guided recovery
- Valid context:
  - attempts to load the real bug
  - if cached data is not ready, shows a product-level cache-not-ready recovery message instead of raw API text

**Validation**
- Direct URL (no params):
  - verified; guided recovery state shown
- Invalid params:
  - verified with `bugId=abc`; guided invalid-state message shown
- Valid params:
  - verified with `bugId=123`; cache-not-ready recovery state shown instead of placeholder data or raw exception text
- Navigation flow:
  - route still provides back/home actions
  - no explicit in-app upstream entry to this page currently exists; recovery now points to `Bug Insights`

### `/home/validation-queue`

**Required context**
- Required parameters:
  - `category`
- Minimum valid input:
  - one canonical UI category key: `SI`, `RR`, `RC`, or `EFF`
- Invalid input:
  - missing `category`
  - unknown category values such as `bad`
- Safe default:
  - no; the route now guides the user back to `Validation Triage`

**Original behavior**
- Missing `category` showed a raw implementation message: `Missing required query parameter: category`.
- Invalid `category` values were sent through to the backend and then surfaced as raw transport text such as:
  - `Failed to load validation queue: net_http_message_not_success_statuscode_reason, 409, Conflict`
- The page did not tell the user what a valid category was or where to recover.

**Root cause**
- Exact cause:
  - the page only checked for presence of `category`, not whether it was a valid canonical category
  - backend/transport exceptions were rendered via `ex.Message`
- Where in code:
  - `PoTool.Client/Pages/Home/ValidationQueuePage.razor`
  - query parameter parsing in `OnInitializedAsync()`
  - broad exception handling converted HTTP failures into raw text

**Fix applied**
- Added canonical category validation using `PoTool.Client/Helpers/ValidationRouteContextHelper.cs`.
- Added guided recovery states for missing and invalid categories.
- Added safe mapping for cache-backed HTTP failures through `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs`.
- Reused the shared `GuidedRecoveryPanel` for user-safe route recovery UI.

**New behavior**
- Missing context:
  - shows “Validation category missing” with a button back to `Validation Triage`
- Invalid context:
  - shows “Unknown validation category” with guided recovery
- Valid context:
  - loads the real queue when available
  - if cache data is not ready, shows a clear recovery state instead of raw HTTP text

**Validation**
- Direct URL (no params):
  - verified; guided recovery state shown
- Invalid params:
  - verified with `category=bad`; guided invalid-state message shown
- Valid params:
  - verified with `category=SI`; cache-not-ready recovery state shown instead of raw transport error
- Navigation flow:
  - existing “Validation Triage” navigation remains intact
  - queue-to-fix navigation logic was left unchanged

### `/home/validation-fix`

**Required context**
- Required parameters:
  - `category`
  - `ruleId`
- Minimum valid input:
  - a canonical category key (`SI`, `RR`, `RC`, `EFF`)
  - a known rule ID that belongs to that category
- Invalid input:
  - missing `category`
  - missing `ruleId`
  - unknown category
  - unknown rule ID
  - known rule ID paired with the wrong category
- Safe default:
  - no; recovery points users back to either `Validation Triage` or the matching queue

**Original behavior**
- Missing `category` or `ruleId` showed raw implementation text such as:
  - `Missing required query parameter: category`
  - `Missing required query parameter: ruleId`
- Invalid combinations such as `category=bad&ruleId=bad` fell through to the backend and surfaced raw HTTP transport text.
- The page relied on incoming navigation state being correct instead of validating that the rule matched the selected category.

**Root cause**
- Exact cause:
  - the page only checked whether parameters existed
  - it never validated category keys or rule/category pairing before loading
  - transport failures were shown via `ex.Message`
- Where in code:
  - `PoTool.Client/Pages/Home/ValidationFixPage.razor`
  - query parsing in `OnInitializedAsync()`
  - raw error construction in the exception handler

**Fix applied**
- Added canonical category and rule validation using `ValidationRouteContextHelper`.
- Enforced that `ruleId` must be a known rule for the selected UI category.
- Added different guided recovery states for:
  - missing/invalid category
  - missing/invalid rule ID
- Added safe cache-not-ready messaging through `ApiErrorMessageFormatter`.
- Updated breadcrumbs so missing-category recovery no longer links to an invalid empty-category queue.

**New behavior**
- Missing context:
  - missing `category` sends the user back toward `Validation Triage`
  - missing `ruleId` sends the user back toward the selected queue
- Invalid context:
  - invalid category or invalid rule/category combination shows a clear not-valid state with recovery navigation
- Valid context:
  - loads the real fix session when available
  - if cache data is not ready, shows a clear recovery state instead of raw HTTP text

**Validation**
- Direct URL (no params):
  - verified; guided recovery state shown
- Invalid params:
  - verified with `category=bad&ruleId=bad`; invalid-state recovery now comes from local validation logic
  - verified with `category=SI` and no `ruleId`; guided missing-rule recovery shown
- Valid params:
  - validated by code path using canonical category/rule matching and safe HTTP failure handling; in the current environment valid sessions remain cache-gated and now resolve to recovery text instead of raw errors
- Navigation flow:
  - existing “Back to Queue” behavior remains for valid queue context
  - recovery now routes users to the nearest valid entry point when context is incomplete

---

## 3. Shared findings
- Two validation routes had the same underlying issue: required query parameters were treated as raw strings and only checked for presence, not canonical validity.
- All three routes benefited from the same small recovery pattern: identify missing/invalid context before doing work, then show a guided recovery state with a clear next step instead of surfacing raw exception text.

---

## 4. Files changed
- `PoTool.Client/Components/Common/GuidedRecoveryPanel.razor` — shared minimal guided recovery UI used by the targeted routes
- `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs` — added safe route-level messages for bug detail and validation route failures
- `PoTool.Client/Helpers/ValidationRouteContextHelper.cs` — added canonical validation for validation route categories and rule/category pairing
- `PoTool.Client/Pages/Home/BugDetail.razor` — replaced sample-data assumptions with real bug lookup and guided missing/invalid context handling
- `PoTool.Client/Pages/Home/ValidationQueuePage.razor` — added category validation and guided recovery states
- `PoTool.Client/Pages/Home/ValidationFixPage.razor` — added category/rule validation, safer breadcrumbs, and guided recovery states
- `PoTool.Tests.Unit/Helpers/ValidationRouteContextHelperTests.cs` — regression coverage for category and rule/category validation
- `docs/reports/2026-04-02-batch2-missing-context-fixes-report.md` — task report

---

## 5. Confidence assessment
- These routes are now robust under direct URL usage for missing and invalid context.
- Confidence is high because:
  - each route now validates its required context locally before doing work
  - raw transport errors are no longer shown for the targeted routes
  - recovery states now point users back to valid entry points
- What could still go wrong:
  - valid-context flows still depend on cache-backed data availability for the validation routes and some bug detail loads
  - `Bug Detail` still lacks a dedicated upstream in-app selection flow, so recovery depends on redirecting users back to `Bug Insights`
