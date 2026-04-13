# WIQL Construction Hardening

## 1. Inventory of WIQL Usage

| File | Method | Purpose | Query shape | Risk |
| --- | --- | --- | --- | --- |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` | `GetWorkItemsByTypeAsync` | Fetch work items for a specific work item type inside an area path | Templated | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` | `GetWorkItemsAsync` | Fetch work items for an area path, with optional incremental changed-date filtering | Dynamically assembled with optional filter | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` | `GetWorkItemsByRootIdsAsync` | Traverse descendant hierarchy through recursive `WorkItemLinks` WIQL | Dynamically assembled from root ID list | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | `VerifyWorkItemQueryAsync` | Verify baseline WIQL execution capability | Static | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | `VerifyWorkItemHierarchyAsync` | Verify hierarchy discovery against configured area path | Templated | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` | `ValidateWorkItemFieldPayloadAsync` | Validate sample work item payloads for the `work-item-fields` verification path | Previously templated with `TOP`; now deterministic ordered query | Hardened and migrated |
| `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Wiql.cs` | `ExecuteWiqlQueryAsync` | Shared WIQL execution boundary for all TFS WIQL POSTs | Shared execution boundary | Hardened guard path |

All repository WIQL POSTs to `/_apis/wit/wiql` now route through the shared execution boundary above. No additional production WIQL execution paths remain outside this set.

## 2. Defect Patterns Found

- Direct string interpolation for `SELECT`, `FROM`, `WHERE`, and `ORDER BY` assembly across multiple call sites.
- Optional clause composition implemented as string concatenation, which could silently emit malformed `WHERE` shapes.
- No shared validation before POSTing WIQL to TFS.
- No centralized diagnostics for the exact WIQL emitted at the execution boundary.
- `ValidateWorkItemFieldPayloadAsync` used `SELECT TOP {n} ...`, which produced the reported `TF51006` malformed-query failure on the `work-item-fields` validation path.
- Callers could pass empty semantic inputs such as blank area paths or work item types without a fast local rejection.

## 3. Fix Strategy Applied

- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/WiqlQueryBuilder.cs` as the shared WIQL construction and validation helper.
- Added `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Wiql.cs` as the shared execution boundary.
- Migrated every production WIQL call site to builder-generated queries plus execution-boundary validation.
- Replaced the failing `SELECT TOP ...` verification query with a deterministic `SELECT [System.Id] FROM WorkItems ORDER BY [System.Id] DESC` query and retained client-side sampling via `Take(...)`.
- Added explicit semantic input guards for blank area paths and blank work item types before a query can be built.
- Kept existing query intent intact where it was already valid, including the recursive `WorkItemLinks` hierarchy query.

## 4. Files Changed

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/WiqlQueryBuilder.cs` — shared builder and syntax guard layer.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Wiql.cs` — shared execution boundary, semantic input validation, and diagnostic logging.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItems.cs` — migrated work item WIQL construction.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.WorkItemsHierarchy.cs` — migrated recursive hierarchy WIQL construction.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/Clients/RealTfsClient.Verification.cs` — migrated verification WIQL construction and removed `TOP` usage from the work-item-fields path.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WiqlQueryBuilderTests.cs` — added focused builder/guard regression tests.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/RealTfsClientRequestTests.cs` — added execution-boundary and `work-item-fields` regression coverage.

## 5. Validation Guards Added

- Reject null, empty, or whitespace WIQL before execution.
- Reject WIQL that does not begin with `SELECT` and include `FROM WorkItems` or `FROM WorkItemLinks`.
- Reject empty `SELECT` field lists.
- Reject incomplete `WHERE` composition.
- Reject malformed boolean composition such as dangling `AND`/`OR`.
- Reject empty `IN ()` filters.
- Reject `SELECT TOP ...` on the hardened path.
- Reject blank area paths and blank work item types before query construction.
- Log blocked malformed WIQL at the execution boundary with a normalized diagnostic query string.

## 6. Test Coverage

- `WiqlQueryBuilderTests.BuildWorkItemsQuery_MinimalQuery_IsValid`
- `WiqlQueryBuilderTests.BuildWorkItemsQuery_OptionalFilters_ComposesDeterministically`
- `WiqlQueryBuilderTests.BuildWorkItemsQuery_EmptySelectField_Throws`
- `WiqlQueryBuilderTests.BuildWorkItemsQuery_EmptyWhereClause_Throws`
- `WiqlQueryBuilderTests.Validate_UnsupportedTop_Throws`
- `WiqlQueryBuilderTests.Validate_EmptyInFilter_Throws`
- `RealTfsClientRequestTests.GetWorkItemsAsync_RejectsEmptyAreaPathBeforeSendingWiql`
- `RealTfsClientRequestTests.VerifyCapabilitiesAsync_WorkItemFieldValidationUsesHardenedWiqlWithoutTopClause`

These tests cover valid minimal generation, optional filters, empty field/filter rejection, malformed dynamic input rejection, the `work-item-fields` verification path, and the current `TF51006` regression scenario.

## 7. Remaining Exceptions

- No production WIQL call sites were left on ad hoc string-post execution.
- The recursive hierarchy query intentionally remains a `FROM WorkItemLinks` query because that is the valid WIQL source for link traversal; it was migrated to the same hardened builder and execution guard path instead of being rewritten.

## 8. Result

- The reported `work-item-fields` malformed WIQL failure is fixed by removing the unsupported `TOP` form from that verification path.
- Malformed WIQL is now blocked before TFS execution by shared validation at the execution boundary.
- Repository WIQL usage has been inventoried and all active production call sites are either migrated to the hardened path or explicitly justified.
- Targeted validation passed:
  - `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -c Release --nologo`
  - targeted WIQL-related unit tests passed
- Full unit-suite baseline remains at the same six unrelated pre-existing failures outside this change scope.
