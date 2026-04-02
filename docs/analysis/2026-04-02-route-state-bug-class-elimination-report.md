# Route-State Bug Class Elimination Report

## 1. Executive summary
- The application had a repeating class of route-entry failures where pages assumed query parameters, prior selection state, or backend route readiness that direct URL entry could not guarantee.
- The visible symptoms included unhandled client crashes, raw missing-parameter messages, raw backend failure text, and legacy screens rendering undefined/broken states.
- This change fixed the known failing routes and introduced shared prevention mechanisms: route-state guard helpers, guided recovery UI, render-time guardrails, safer global error rendering, and cache-route classification for portfolio analytics.
- Final assessment of residual risk: **reduced substantially but not mathematically eliminated for every future page**. The audited/failing routes are hardened, shared defaults now exist, and regression tests were added, but future pages can still bypass the shared helpers unless they adopt the same pattern.

## 2. Root causes
| Route | Symptom | Direct cause | Deeper/root cause | Local or systemic |
| --- | --- | --- | --- | --- |
| `/bugs-triage` | Unhandled runtime error on entry | `IBugTriageClient` was injected but never registered in DI | No structural guard against missing page dependencies; page-specific startup assumptions bypassed shared route safety | Systemic |
| `/home/delivery/sprint/activity/1000` | Drill-down crashed or surfaced raw failure state | Detail page assumed valid activity data and mapped API exceptions directly to UI text | Route entry was not guarded for missing/unavailable detail data and lacked product-safe recovery states | Systemic |
| `/home/portfolio-progress` | Backend 500 leaked into UI | `/api/portfolio/progress` was not classified in `DataSourceModeConfiguration`, and client error handling surfaced raw exception text | Cache/live route classification and client-safe backend-failure translation were inconsistent | Systemic |
| `/home/bugs/detail` | Dead-end route with missing bug context | Page expected `bugId` query state but allowed direct entry without it | Detail-route pattern depended on navigation-carried state instead of safe direct-URL behavior | Systemic |
| `/home/validation-queue` | Missing category parameter error | Required `category` query parameter was read ad hoc and rendered as a raw message | Selection-dependent pages did not have a reusable required-context contract | Systemic |
| `/home/validation-fix` | Missing queue/rule context | Required `category` and `ruleId` parameters were assumed instead of guarded | Guided recovery and required-query validation were missing as a shared page pattern | Systemic |
| `/workspace/analysis` | Undefined values and broken-looking charts | Legacy analysis page embedded components that were unsafe on direct entry/empty context | Legacy route remained active without modern direct-entry safety expectations or safe retirement behavior | Systemic |

## 3. Bug-class analysis
- Common patterns discovered:
  - direct URL entry to detail/drill-down pages without required query state
  - pages relying on selected profile/product/team/sprint context that was not structurally validated
  - backend/API failures translated straight into raw exception text
  - render-time crashes escaping to the global Blazor error UI
  - legacy routes still composing unsafe panels that assumed richer context than the route guaranteed
- Broader bug classes identified:
  - **missing-context route entry**
  - **unsafe detail/drill-down rendering**
  - **raw backend failure leakage**
  - **legacy compatibility routes without safe retirement or hardened fallback**
- Why these bugs were possible in the existing design:
  - `WorkspaceBase` only handled a subset of context propagation and did not provide route-query guard helpers
  - pages implemented parameter validation inconsistently, often inline and after rendering had already begun
  - reusable product-safe recovery UI existed only partially (`ErrorDisplay`, `EmptyStateDisplay`) and was not the default route-entry pattern
  - the global app error boundary still surfaced exception-driven failure behavior instead of product-safe recovery
  - cache-only analytical API route classification had gaps

## 4. Implemented solution
- Shared abstractions/patterns introduced:
  - `RouteStateGuard` for required-query validation and safe user-facing error construction
  - `GuidedRecoveryState` for concise, actionable missing-context and invalid-context states
  - `PageRenderGuard` for page-level render-crash recovery instead of global unhandled failure UI
- Routing changes:
  - expanded `WorkspaceBase` with query access, context route building, and replace-in-place route updates
  - persisted portfolio flow filter context back into the URL so reload/direct entry preserves selected scope
- Client/page initialization changes:
  - hardened failing pages to validate required context before rendering meaningful content
  - converted missing/invalid route/query states into guided recovery rather than raw errors
  - loaded real bug data in `BugDetail` instead of placeholder-only direct-entry behavior
- Backend/service/result-contract changes:
  - classified `/api/portfolio/progress` under cache-only analytical portfolio routes so it no longer throws `RouteNotClassifiedException`
  - translated client-side backend exceptions through `ErrorMessageService` + `RouteStateGuard` so raw server details are not shown in product UI
- UI state/recovery changes:
  - global `App.razor` error boundary now renders product-safe recovery UI
  - portfolio CDC panel now uses product-safe error rendering instead of surfacing server exception text
- Legacy-route handling changes:
  - `/workspace/analysis` now behaves as a safe compatibility route and guides users to modern workspaces instead of rendering broken legacy panels

## 5. Pages fixed
| Route | Previous behavior | New behavior | Fix scope |
| --- | --- | --- | --- |
| `/bugs-triage` | Route crashed on entry because required client service was missing from DI | Route loads safely; bug triage data renders after loading, and page render failures are guarded | Shared + page-local |
| `/home/delivery/sprint/activity/{workItemId}` | Invalid/unavailable activity data could crash or surface raw failure text | Invalid/unavailable activity now shows safe error/recovery UI with retry/back navigation | Shared + page-local |
| `/home/portfolio-progress` | Portfolio CDC section leaked raw backend classification/500 text | Portfolio route and CDC panel now show safe error states; route classification no longer throws | Shared + backend + page-local |
| `/home/bugs/detail` | Direct entry without `bugId` produced a dead-end screen | Missing/invalid/not-found bug context now shows guided recovery; valid IDs load real cached bug data | Shared + page-local |
| `/home/validation-queue` | Missing `category` rendered a raw missing-parameter error | Missing/invalid category now shows guided recovery back to Validation Triage | Shared + page-local |
| `/home/validation-fix` | Missing `category`/`ruleId` rendered raw context errors | Missing/invalid fix-session context now shows guided recovery back to Validation Triage | Shared + page-local |
| `/workspace/analysis` | Direct entry rendered undefined values and broken charts | Legacy route now safely retires to a compatibility screen with links to modern workspaces | Shared + route-local |

## 6. Prevention mechanisms
- What now prevents future recurrence:
  - reusable `RouteStateGuard` makes required query validation and safe error generation a standard pattern
  - `GuidedRecoveryState` provides a default UX for missing/invalid context instead of dead-end pages
  - `PageRenderGuard` and the hardened global app error boundary stop page crashes from falling through to raw unhandled UI
  - portfolio analytical routes now participate in explicit datasource classification
- Which rules are enforced structurally:
  - raw render exceptions now resolve through product-safe error UI
  - route/query guard helpers and guided recovery are available as shared building blocks
  - datasource classification regression covers `/api/portfolio/progress`
- Which risks still depend on discipline rather than enforcement:
  - new pages can still ignore `RouteStateGuard` / `GuidedRecoveryState` if added without using the shared pattern
  - not every existing route was rewritten to inherit the shared safety flow in this task

## 7. Validation performed
- Manual routes tested:
  - `/bugs-triage`
  - `/home/delivery/sprint/activity/1000`
  - `/home/portfolio-progress`
  - `/home/bugs/detail`
  - `/home/validation-queue`
  - `/home/validation-fix`
  - `/workspace/analysis`
- Direct-entry/reload scenarios tested:
  - `/home/bugs/detail?bugId=999999`
  - `/home/validation-queue?category=bogus`
  - `/home/validation-fix?category=SI`
  - `/home/delivery/sprint/activity/999999?productOwnerId=1`
  - `/workspace/analysis/unknown-mode`
- Empty/invalid/partial/failure scenarios tested:
  - missing required query parameters
  - invalid validation category
  - unknown bug id
  - unavailable sprint activity detail
  - portfolio history route classification/failure handling
  - legacy analysis direct entry with no safe legacy context
- Automated tests added or updated:
  - `RouteStateGuardTests`
  - `DataSourceModeConfigurationTests` for `/api/portfolio/progress`
  - existing UI semantic retry-label audit revalidated
- Verification commands run:
  - `dotnet build PoTool.sln --configuration Release --nologo`
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~RouteStateGuardTests|FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~UiSemanticLabelsTests.ErrorBoundaryRetryButton_UsesSentenceCaseLabel" --nologo`
- Note on broader test suite:
  - the full `PoTool.Tests.Unit` suite still has many unrelated pre-existing failures in this branch; targeted regression tests for this change pass.

## 8. Remaining risks and follow-up
- Residual weaknesses:
  - route safety is now much easier to apply, but not every routed page has been migrated to the shared pattern yet
  - some legacy workspaces besides `/workspace/analysis` still rely on older navigation/context behavior and may merit the same retirement-or-hardening review
  - local manual validation still required a pre-existing mock-data startup workaround because clean mock startup currently hits a product/project FK seeding issue unrelated to this task
- Recommended next hardening steps:
  - audit remaining direct-entry detail routes and convert them to `RouteStateGuard` + `GuidedRecoveryState`
  - add more structural tests that flag routed pages bypassing shared guard patterns
  - fix the mock configuration startup FK issue so clean runtime validation does not require local seeding help
- Any routes still needing later cleanup:
  - other legacy workspace routes should be reviewed for full modernization or safe retirement

## 9. Files changed
- `PoTool.Client/Helpers/RouteStateGuard.cs` — shared route/query validation and safe error helpers
- `PoTool.Client/Components/Common/GuidedRecoveryState.razor` — reusable guided recovery UI
- `PoTool.Client/Components/Common/PageRenderGuard.razor` — page-level render crash guard
- `PoTool.Client/Pages/Home/WorkspaceBase.cs` — shared query access and context route helpers
- `PoTool.Client/Pages/BugsTriage.razor` — added shared route safety, render guard, and safe bug triage startup behavior
- `PoTool.Client/Pages/Home/BugDetail.razor` — hardened missing/not-found bug handling and real cached bug loading
- `PoTool.Client/Pages/Home/ValidationQueuePage.razor` — hardened required category handling
- `PoTool.Client/Pages/Home/ValidationFixPage.razor` — hardened required category/rule handling
- `PoTool.Client/Pages/Home/SprintTrendActivity.razor` — safe activity-detail failure handling
- `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` — safe error handling and URL-persisted scope context
- `PoTool.Client/Pages/Home/Components/PortfolioCdcReadOnlyPanel.razor` — safe backend failure display
- `PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor` — safe compatibility/retirement behavior
- `PoTool.Client/Program.cs` — registered `IBugTriageClient`
- `PoTool.Client/App.razor` — global product-safe error boundary rendering
- `PoTool.Client/Models/ValidationCategoryMeta.cs` — shared supported-category validation
- `PoTool.Api/Configuration/DataSourceModeConfiguration.cs` — classified portfolio analytical routes
- `PoTool.Tests.Unit/Helpers/RouteStateGuardTests.cs` — regression coverage for shared guard helpers
- `PoTool.Tests.Unit/Configuration/DataSourceModeConfigurationTests.cs` — regression coverage for portfolio route classification

## 10. Final verdict
- **Verdict: partially eliminated, with the known failures fixed and the bug class materially reduced through shared prevention mechanisms.**
- Justification:
  - every known failing route in scope now resolves to product-safe behavior instead of crashing or leaking raw technical failures
  - shared route-guard, recovery-state, and render-guard patterns now exist and were applied to the audited failing routes
  - regression tests protect the new guard helpers and the previously missing portfolio route classification
  - residual risk remains because future or unaudited pages can still bypass the shared pattern until a wider route audit enforces it repository-wide
