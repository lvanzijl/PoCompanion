# Final cleanup sweep

## 1. Scope cleaned
- Normalized the remaining legacy route aliases for backlog health, sprint delivery, and sprint activity so each concept now has one canonical routed page and a redirect-only legacy entry.
- Removed visible dead `/help` navigation and the stale startup-guard exemption that referenced the non-routable target.
- Reduced `WorkItemExplorer` query-state support to the proven live surface and normalized an unsupported caller onto the retained canonical query.

## 2. Files deleted
- None.

## 3. Files modified
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/CanonicalRouteRedirect.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/StartupGuard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/SubComponents/ValidationSummaryPanel.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomeChanges.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrendActivity.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/BacklogOverviewLegacyRedirect.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/SprintTrendActivityLegacyRedirect.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/SprintTrendLegacyRedirect.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Components/Common/WorkspaceNavigationCatalogTests.cs`

## 4. Alias route decisions
- `/home/backlog-overview`
  - **Decision:** retain only as a redirect alias.
  - **Result:** removed the alias `@page` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BacklogOverviewPage.razor` and added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/BacklogOverviewLegacyRedirect.razor` to normalize to `/home/health/backlog-health` with `replace: true`.
- `/home/sprint-trend`
  - **Decision:** retain only as a redirect alias.
  - **Result:** removed the alias `@page` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrend.razor`, added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/SprintTrendLegacyRedirect.razor`, and normalized the remaining in-app navigation in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/HomeChanges.razor` to `/home/delivery/sprint`.
- `/home/sprint-trend/activity/{WorkItemId:int}`
  - **Decision:** retain only as a redirect alias.
  - **Result:** removed the alias `@page` from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/SprintTrendActivity.razor` and added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyRoutes/SprintTrendActivityLegacyRedirect.razor` to normalize to `/home/delivery/sprint/activity/{WorkItemId:int}` while preserving query string context.
- Supporting cleanup
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/WorkspaceNavigationCatalog.cs` no longer treats legacy aliases as active workspace prefixes.
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs` now documents the alias constants as redirect-only compatibility routes rather than equal-status routes.

## 5. Dead link cleanup results
- Removed the visible `/help` action from `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/SubComponents/ValidationSummaryPanel.razor` because no routable help page exists.
- Removed `/help` from the default exempt-route list in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/StartupGuard.razor` so startup guard state no longer preserves a dead target.
- Post-change client sweep found no remaining visible `/help` route references or other visible navigation to the cleaned alias routes outside the dedicated redirect pages.

## 6. Work Item Explorer query-state cleanup results
- **Removed query-state inputs**
  - `validationCategory`
  - `allProducts`
  - `allTeams`
- **Removed dormant manual query parsing**
  - `filter=issues`
  - `focusRoot`
- **Retained intentionally**
  - `rootWorkItemId` remains because it has proven live callers in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/BacklogOverviewPage.razor` and `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`.
- **Caller normalization**
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor` previously sent the unsupported `selected` query parameter.
  - That caller now uses `rootWorkItemId`, which is the retained supported scope query for the explorer.

## 7. Shared pieces intentionally retained
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/WorkItems/WorkItemExplorer.razor` itself was retained unchanged as a route surface; only unsupported query-state entry points were trimmed.
- `rootWorkItemId` support was retained because it is the current shared deep-link contract for backlog- and release-planning-driven explorer navigation.
- The fallback behavior in `WorkItemExplorer` that loads all products when no active profile exists was retained because it is runtime safety behavior, not dormant query-state surface.
- The legacy route constants in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs` were retained because the redirect pages still use those bookmark-compatibility paths.

## 8. Risks / follow-ups
- Any external bookmarks to the cleaned legacy aliases will now perform a client-side redirect instead of rendering the old page directly; this is expected, but browser-history behavior should be spot-checked manually if a UX review is needed.
- The release-planning “view details” action now scopes the explorer by `rootWorkItemId`; if that flow was implicitly relying on the old unsupported `selected` query for future work, that latent expectation is now removed.
- No broader navigation ownership or page redesign was done in this batch; deeper route taxonomy cleanup remains a separate concern.

## 9. Validation results
- Pre-change validation passed:
  - `dotnet build PoTool.sln --configuration Release --nologo`
  - `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`
- Post-change validation passed with the same commands:
  - `dotnet build PoTool.sln --configuration Release --nologo`
  - `dotnet test PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`
- Test adjustment:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Components/Common/WorkspaceNavigationCatalogTests.cs` was updated so legacy alias routes are no longer treated as active workspace routes.
