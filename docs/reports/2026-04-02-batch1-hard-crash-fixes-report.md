# Batch 1 — Hard Crash Fixes Report

## 1. Summary
- Routes fixed:
  - `/bugs-triage`
  - `/home/delivery/sprint/activity/1000`
  - `/home/portfolio-progress`
- Were all crashes resolved?
  - `/bugs-triage`: yes
  - `/home/delivery/sprint/activity/1000`: yes
  - `/home/portfolio-progress`: yes
- Remaining issues:
  - The targeted routes no longer crash, but the current mock environment still returns cache-not-ready responses for some analytical reads, so two routes now show product-level recovery states instead of data.

---

## 2. Route analysis and fixes

### `/bugs-triage`

**Original behavior**
- Direct navigation crashed the page before it could render a product-level state.
- The UI showed the global Blazor unhandled-error banner.
- Browser console showed:
  - `Cannot provide a value for property 'BugTriageClient' on type 'PoTool.Client.Pages.BugsTriage'. There is no registered service of type 'PoTool.Client.ApiClient.IBugTriageClient'.`

**Root cause**
- Exact technical cause: missing client DI registration for `IBugTriageClient`.
- Where it occurred: component activation in `BugsTriage.razor` before `OnInitializedAsync` could run.
- Why it was allowed to fail: `PoTool.Client/Program.cs` registered the concrete `BugTriageClient` JSON settings partial, but never registered the generated `IBugTriageClient` service used by the page.

**Fix applied**
- Added `IBugTriageClient` registration in `PoTool.Client/Program.cs`.
- Added a focused runtime DI regression test in `PoTool.Tests.Unit/Configuration/ClientProgramRegistrationTests.cs`.
- Replaced raw API exception text in `PoTool.Client/Pages/BugsTriage.razor` with a safe recovery message via `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs`.

**Result**
- New behavior: direct navigation renders the page shell and shows a clear recovery message when cache-backed work-item data is unavailable.
- Verified scenarios:
  - direct URL: page loads without crash
  - reload: page loads without crash
  - hub navigation: Home quick action opens the route without crash

### `/home/delivery/sprint/activity/1000`

**Original behavior**
- Direct navigation did not always hard-crash, but it exposed raw backend error text in the page alert.
- The UI showed the full generated API exception text including HTTP status and raw response body:
  - `Failed to load activity details: The HTTP status code of the response was not expected (409)...`

**Root cause**
- Exact technical cause: `ApiException` from the activity details request was caught only by a broad `catch (Exception)` block and surfaced with `ex.Message`.
- Where it occurred: `OnInitializedAsync` in `SprintTrendActivity.razor`.
- Why it was allowed to fail: the page assumed backend failures could be shown directly to the user instead of translating known HTTP failures into product-level states.

**Fix applied**
- Added a local route-parameter guard for invalid `WorkItemId` values in `PoTool.Client/Pages/Home/SprintTrendActivity.razor`.
- Added targeted `ApiException` handling there and mapped cache-not-ready / not-found failures to safe messages through `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs`.

**Result**
- New behavior: the route opens directly and shows a clear recovery state instead of raw exception text.
- Verified scenarios:
  - direct URL `/home/delivery/sprint/activity/1000`: safe cache-not-ready recovery message
  - reload: same safe recovery message, no crash
  - invalid input `/home/delivery/sprint/activity/0`: clear invalid-work-item message
  - route back-navigation: "Back to Sprint Delivery" still works

### `/home/portfolio-progress`

**Original behavior**
- Direct navigation showed raw server error text in the embedded CDC history panel.
- The underlying API returned a 500 with `RouteNotClassifiedException` text for `/api/portfolio/progress`, and that raw message was rendered in the UI.

**Root cause**
- Exact technical cause: `/api/portfolio/*` read endpoints used by the page were not fully classified in `DataSourceModeConfiguration`; `/api/portfolio/progress` fell through to `RouteNotClassifiedException`.
- Where it occurred: backend route classification in `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`, then surfaced by `PortfolioCdcReadOnlyPanel.razor`.
- Why it was allowed to fail: only `/api/portfolio/snapshots` had been explicitly classified, while sibling portfolio read endpoints used by the same route were left unclassified.

**Fix applied**
- Broadened live-allowed route classification from `/api/portfolio/snapshots` to `/api/portfolio` in `PoTool.Api/Configuration/DataSourceModeConfiguration.cs`.
- Added a regression test in `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs`.
- Replaced raw API exception text with safe messages in:
  - `PoTool.Client/Pages/Home/PortfolioProgressPage.razor`
  - `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor`
- Reused `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs` for localized error translation.

**Result**
- New behavior: the page opens directly, no longer throws route-classification server errors, and now renders a valid no-data/history-empty state.
- Verified scenarios:
  - direct URL: page loads without crash
  - reload: page loads without crash
  - hub navigation: Trends workspace tile opens the route without crash

---

## 3. Shared findings
- Two routes were failing because generated `ApiException` messages were rendered directly into page alerts. A small shared formatter was enough to translate known API failures into safe, product-level recovery text.
- One route failure was backend route-classification drift: sibling `/api/portfolio/*` read endpoints were not classified consistently even though the same page depended on them together.

---

## 4. Files changed
- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` — classified `/api/portfolio/*` routes consistently
- `PoTool.Client/ApiClient/BugTriageClientServiceCollectionExtensions.cs` — centralized bug-triage client registration for runtime DI coverage
- `PoTool.Client/Helpers/ApiErrorMessageFormatter.cs` — added shared local error-message translation for targeted routes
- `PoTool.Client/Pages/BugsTriage.razor` — replaced raw API error text with safe bugs-triage recovery state
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor` — replaced raw CDC history exception text
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` — replaced raw portfolio-flow exception text
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor` — added invalid-ID guard and safe activity error handling
- `PoTool.Client/Program.cs` — used shared bug-triage client registration
- `PoTool.Tests.Unit/Configuration/ClientProgramRegistrationTests.cs` — added bug-triage runtime DI regression coverage
- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs` — added portfolio route classification regression coverage
- `docs/reports/2026-04-02-batch1-hard-crash-fixes-report.md` — task report

---

## 5. Confidence assessment
- Confidence: high that the three targeted routes are now stable under direct URL activation.
- Why:
  - the `/bugs-triage` hard crash was eliminated at the DI root cause
  - the sprint activity route now handles both invalid IDs and cache-not-ready API responses safely
  - the portfolio route no longer hits the unclassified `/api/portfolio/progress` backend failure and now renders a valid empty/history state
- What could still go wrong:
  - other non-targeted routes may still surface raw API exceptions
  - if backend APIs return new error shapes beyond the handled cases, the pages will fall back to generic product-level messages rather than route-specific guidance
