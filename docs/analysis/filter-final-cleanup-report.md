# Filter Final Cleanup Report

## Summary

This closeout pass audited the canonical filtering workstream for dead legacy code, duplicate request-building, compatibility shims, and outdated documentation.

The audit found that the migrated filtering slices already run through the canonical API-side boundary resolution services and canonical response envelopes. No dead legacy filter parser, old query-string builder, or envelope-collapse branch was found inside those active migrated execution paths, so this pass focused on documentation alignment and explicit closeout reporting rather than risky code deletion.

## Removed Legacy Code

No code was removed in this pass.

The remaining candidate legacy-looking paths were audited and were found to be either:

- still active navigation-context helpers
- active endpoint-family transport adapters
- non-filter compatibility aliases outside the scope of canonical filter execution cleanup

## Remaining Compatibility Paths

| Path | Status | Reason |
|---|---|---|
| `PoTool.Client/Pages/Home/WorkspaceBase.cs` | Temporary and should remain with reason | Still actively propagates coarse `productId` / `teamId` home-workspace navigation context. It is not a dead compatibility path, and removing it now would break current workspace navigation because no shared `FilterUrlParser` / `FilterNavigationAdapter` replacement was shipped. |
| `PoTool.Client/Services/PipelineService.cs` | Required to keep | Still adapts client calls to the active pipeline API contract, which accepts family-specific transport parameters such as `productIds`, `fromDate`, and `toDate` before API-side canonical resolution. |
| `PoTool.Client/Services/PullRequestService.cs` | Required to keep | Still adapts client calls to the active pull-request API contract, which accepts family-specific transport parameters and returns the canonical envelope. |
| `PoTool.Client/Services/SprintDeliveryMetricsService.cs` | Required to keep | Still adapts sprint trend requests to the active API contract that accepts sprint collections and optional product scope before API-side canonical resolution. |
| `PoTool.Client/Services/BuildQualityService.cs` | Required to keep | Still adapts build-quality requests to the active delivery-family API contract. The canonical filtering metadata is preserved in the response envelope; the client adapter is not dead code. |
| Page-local date/sprint selection in migrated pages such as `PoTool.Client/Pages/Home/PrOverview.razor`, `PoTool.Client/Pages/Home/PrDeliveryInsights.razor`, `PoTool.Client/Pages/Home/DeliveryTrends.razor`, `PoTool.Client/Pages/Home/SprintTrend.razor`, and `PoTool.Client/Pages/Home/PortfolioProgressPage.razor` | Temporary and should remain with reason | These pages still own UI selection state and conversion to the active endpoint-family request shapes. That is remaining technical debt, but it is live behavior rather than dead compatibility code, so deleting it in a cleanup-only pass would amount to a redesign. |

## Verification

The following verification was run during this closeout pass:

- `dotnet build PoTool.sln --configuration Release`
- `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~PullRequestFilterResolutionServiceTests|FullyQualifiedName~PipelineFilterResolutionServiceTests|FullyQualifiedName~DeliveryFilterResolutionServiceTests|FullyQualifiedName~SprintFilterResolutionServiceTests|FullyQualifiedName~PortfolioFilterResolutionServiceTests|FullyQualifiedName~MetricsControllerSprintCanonicalFilterTests|FullyQualifiedName~MetricsControllerDeliveryCanonicalFilterTests|FullyQualifiedName~PipelinesControllerCanonicalFilterTests|FullyQualifiedName~PullRequestsControllerCanonicalFilterTests|FullyQualifiedName~BuildQualityControllerDeliveryCanonicalFilterTests|FullyQualifiedName~CanonicalClientResponseFactoryTests|FullyQualifiedName~PipelineServiceTests|FullyQualifiedName~WorkspaceSignalServiceTests|FullyQualifiedName~ReleaseNotesServiceTests" -v minimal`

Results:

- Release build succeeded
- 43 focused canonical-filter and metadata tests passed
- no deleted code was required because no dead filter execution path was identified

## Final Architecture Status

The canonical API-side filtering system is now the sole active execution system for the migrated sprint, delivery, pipeline, pull-request, and portfolio slices.

Client-side query-string helpers and endpoint-family request adapters still remain, but they no longer replace or bypass the canonical backend effective-filter resolution model in the migrated slices.

## Known Remaining Technical Debt

- A shared client-side canonical filter runtime (`FilterState`, shared URL parser/serializer, shared navigation adapter) was planned but was not shipped; page-local selection and transport shaping still exist in several migrated pages.
- `WorkspaceBase` still carries coarse workspace context through query parameters and remains separate from the canonical backend filtering system.
- Some DTO compatibility aliases outside the narrow filter-execution scope still remain for contract stability and can be revisited only in a dedicated contract-cleanup change.
