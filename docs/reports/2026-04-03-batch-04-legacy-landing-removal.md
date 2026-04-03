# Batch 4 — legacy landing removal

## 1. Scope removed

- Removed the standalone legacy landing route `/legacy`.
- Deleted the legacy landing page component `Landing.razor`.
- Removed route constants and helper fallbacks that still targeted the deleted `/legacy` entry point.
- Removed visible legacy-workspace navigation that sent users back into the deleted landing entry; those exits now return to `/home`.

## 2. Files deleted

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Landing.razor`

## 3. Files modified

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Models/WorkspaceRoutes.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/ProductWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/AnalysisWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/CommunicationWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/LegacyWorkspaces/TeamWorkspace.razor`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/INavigationContextService.cs`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/CacheStatusSection.razor`
- `/home/runner/work/PoCompanion/PoCompanion/docs/architecture/navigation-map.md`
- `/home/runner/work/PoCompanion/PoCompanion/docs/release-notes.json`

## 4. Residual legacy routes status

The underlying legacy workspace routes were intentionally kept:

- `/workspace/product`
- `/workspace/product/{ProductId:int}`
- `/workspace/team`
- `/workspace/team/{TeamId:int}`
- `/workspace/analysis`
- `/workspace/analysis/{Mode}`
- `/workspace/communication`

These routes still compile and remain direct-entry only in this batch. They are no longer reachable through the removed `/legacy` landing page, and their visible “Landing” return actions now route to `/home`.

## 5. Residual reference check

### Removed from live client code

- `/legacy`
- `WorkspaceRoutes.Legacy`
- `WorkspaceRoutes.Landing`
- `Landing.razor`
- visible legacy-workspace “Landing” buttons and breadcrumbs targeting `/legacy`
- helper fallback to `/legacy` from `WorkspaceRoutes.GetRouteForIntent`
- communication deep-link fallback to `/legacy`

### Retained intentionally

- `/workspace/*` route declarations and legacy workspace implementations
- legacy workspace cross-navigation between residual `/workspace/*` pages

### Historical references left in place

- prior dated analysis/report artifacts under `docs/analysis/**` and older `docs/reports/**`
- implementation/history-style documents under `docs/implementation/**`
- feature notes outside canonical active docs, such as `/home/runner/work/PoCompanion/PoCompanion/features/planning_board_decommission.md`

These references were left for traceability and are not live navigation code.

## 6. Risks / follow-ups

- Direct bookmarks to `/workspace/*` still expose the residual legacy workspace cluster until later staged removal.
- Legacy workspace pages still link to other residual legacy/workspace-era routes; this batch only removed the `/legacy` entry point and navigation into it.
- Historical and implementation documents still describe the deleted landing page and should be cleaned up later only if those documents are brought back under active governance.

## 7. Validation results

- Verified `/legacy` no longer exists in live client code.
- Verified no UI navigation or helper fallback still targets `/legacy`.
- Verified no redirect/default route still points to `/legacy`; the intent fallback now returns `/home`.
- Verified `/workspace/*` routes still exist and compile.
- Verified build and filtered tests pass.

Validation commands:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo`
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --nologo --filter "TestCategory!=Governance&TestCategory!=ApiContract"`

Result:

- Build passed.
- Filtered tests passed.

Release notes: updated
