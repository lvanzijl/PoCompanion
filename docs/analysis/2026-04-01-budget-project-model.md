# Budget and Project Model Analysis

Date: 2026-04-01

## Scope

This report describes the current implemented model for project-related and budget-related data in the repository. It is limited to structures, fields, storage locations, API/DTO exposure, and relationships that are explicitly present in code or migrations.

## 1. Summary

The current runtime model contains:

- a persisted **Product** entity (`ProductEntity`)
- epic-level **project/funding fields** on work items (`ProjectNumber`, `ProjectElement`)
- product-scoped **portfolio snapshot** tables and DTOs that group data by `ProjectNumber` and optional `WorkPackage`
- standard work-item estimation fields (`Effort`, `StoryPoints`)

The current runtime model does **not** contain:

- a standalone `ProjectEntity`
- a standalone `BudgetEntity`
- a `DbSet` for budgets
- shared DTOs or API contracts named `Budget*`

The term **Budget** appears in repository documentation, especially `docs/architecture/cdc-decision-record.md`, but no matching runtime entity/table/DTO/API contract was found in `PoTool.Api`, `PoTool.Shared`, or `PoTool.Core.Domain`.

## 2. Entities and fields

### 2.1 Product entity

`ProductEntity` is the only standalone persisted entity in the inspected model that represents an ownership/business scope.

File:

- `PoTool.Api/Persistence/Entities/ProductEntity.cs:9-96`

Fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `int` | Primary key |
| `ProductOwnerId` | `int?` | FK-like owner reference to profile; nullable |
| `Name` | `string` | Required, max 200 |
| `Order` | `int` | Explicit ordering |
| `PictureType` | `int` | Product picture mode |
| `DefaultPictureId` | `int` | Default picture index |
| `CustomPicturePath` | `string?` | Optional custom picture path |
| `CreatedAt` | `DateTimeOffset` | Required |
| `LastModified` | `DateTimeOffset` | Required |
| `LastSyncedAt` | `DateTimeOffset?` | Optional last sync time |
| `EstimationMode` | `int` | Product estimation mode |

Navigation properties:

- `ProductOwner`
- `ProductTeamLinks`
- `Repositories`
- `BacklogRoots`

### 2.2 Work-item project and effort/story-point fields

There is no standalone runtime `ProjectEntity`. Instead, project-related data is stored as fields on `WorkItemEntity`.

File:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-100`

Relevant fields:

| Field | Type | Notes |
| --- | --- | --- |
| `Effort` | `int?` | Commented as effort estimate in hours |
| `StoryPoints` | `int?` | TFS story-point estimate |
| `BusinessValue` | `int?` | Business value |
| `TimeCriticality` | `double?` | Manual feature-progress override field |
| `ProjectNumber` | `string?` | Funding project number, max 200 |
| `ProjectElement` | `string?` | Funding project element, max 200 |

The same fields are exposed on work-item DTOs:

- `PoTool.Shared/WorkItems/WorkItemDto.cs:7-37`
- `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs:6-35`

### 2.3 Portfolio snapshot entities

Portfolio snapshot persistence stores project-grouped rows, but it still does not define an explicit budget amount.

Files:

- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs:8-55`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs:9-58`

### PortfolioSnapshotEntity

| Field | Type | Notes |
| --- | --- | --- |
| `SnapshotId` | `long` | Primary key |
| `TimestampUtc` | `DateTime` | Queryable UTC timestamp |
| `ProductId` | `int` | Product scope of the snapshot header |
| `Source` | `string` | Required source label |
| `CreatedBy` | `string?` | Optional creator identity |
| `IsArchived` | `bool` | Archived flag |

Navigation:

- `Product`
- `Items`

### PortfolioSnapshotItemEntity

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `long` | Primary key |
| `SnapshotId` | `long` | FK to snapshot header |
| `ProjectNumber` | `string` | Required project-number key |
| `WorkPackage` | `string?` | Optional work-package key |
| `Progress` | `double` | Canonical unit-interval progress |
| `TotalWeight` | `double` | Canonical total weight |
| `LifecycleState` | `WorkPackageLifecycleState` | Snapshot lifecycle state |

No `Budget`, `BudgetAmount`, `BudgetRemaining`, or cost field exists on either snapshot entity.

### 2.4 Resolved hierarchy entity used for product linkage

The current epic/product relationship is supported by resolved hierarchy data rather than a direct epic-to-product FK on the work item table.

File:

- `PoTool.Api/Persistence/Entities/ResolvedWorkItemEntity.cs:9-71`

Relevant fields:

| Field | Type | Notes |
| --- | --- | --- |
| `WorkItemId` | `int` | TFS work item ID |
| `WorkItemType` | `string` | Work item type |
| `ResolvedProductId` | `int?` | Resolved internal product ID |
| `ResolvedEpicId` | `int?` | Resolved epic TFS ID |
| `ResolvedFeatureId` | `int?` | Resolved feature TFS ID |
| `ResolvedSprintId` | `int?` | Resolved sprint ID |

### 2.5 Budget entities

No runtime entity or record named any of the following was found:

- `BudgetEntity`
- `BudgetSnapshotEntity`
- `BudgetDto`
- `BudgetSnapshotDto`

No `DbSet<...Budget...>` was found in `PoToolDbContext`.

Code evidence:

- `PoTool.Api/Persistence/PoToolDbContext.cs:17-233`
- repository-wide search across `PoTool.Api`, `PoTool.Shared`, and `PoTool.Core.Domain`

## 3. Relationship analysis

### 3.1 Projects ↔ Epics

Project data is modeled as epic-level semantics, not as a separate table.

Code:

- `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs:8-35`
- `PoTool.Tests.Unit/DomainWorkItemFieldSemanticsTests.cs:10-90`

Explicit rules:

- `ProjectNumber` is relevant only for epics
- `ProjectElement` is relevant only for epics
- features normalize those fields to `null`

This means the implemented project-to-epic relationship is:

- epic carries `ProjectNumber`
- epic may carry `ProjectElement`
- non-epic canonical/domain work-item models do not preserve those fields

### 3.2 Projects ↔ Products

No direct `Project` table links to `Products`.

The implemented relationship is indirect and appears in product-scoped portfolio snapshot capture:

1. epic progress is computed with a `ProductId`
2. matching epic work items are loaded from `WorkItems`
3. `ProjectNumber` and `ProjectElement` are read from those epics
4. product-scoped snapshot inputs are created with both `ProductId` and `ProjectNumber`

File:

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:102-162`

The snapshot model also preserves this combined structure:

- snapshot header: `ProductId`
- snapshot row: `ProjectNumber`, optional `WorkPackage`

Files:

- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs:22-27`
- `PoTool.Api/Persistence/Entities/PortfolioSnapshotItemEntity.cs:23-34`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:53-97`

This is reinforced by the domain snapshot keys:

- `ProjectKey => (ProductId, ProjectNumber)`
- `BusinessKey => (ProductId, ProjectNumber, WorkPackage)`

File:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:94-96`, `183-196`

### 3.3 Budgets ↔ Projects

No runtime project-to-budget relationship is implemented as an entity/table/DTO/API contract.

What exists in runtime code:

- project-number fields on work items
- project/work-package keys on portfolio snapshot rows

What was not found in runtime code:

- a budget field on project rows
- a budget table linked to projects
- a budget DTO linked to projects
- a budget API response linked to projects

The only explicit budget description found is in documentation:

- `docs/architecture/cdc-decision-record.md:227-244`

That document describes budget at project level, but no corresponding runtime class/table/DTO was found.

### 3.4 Budgets ↔ Time

No runtime budget entity or field was found, so no implemented static-or-time-based budget storage model could be identified in application code.

What is time-based in the current runtime model:

- `PortfolioSnapshotEntity.TimestampUtc`
- historical snapshot selection and trend APIs built from persisted snapshot history

Files:

- `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs:16-27`
- `PoTool.Api/Services/PortfolioSnapshotQueryService.cs:21-67`
- `PoTool.Api/Services/PortfolioProgressQueryService.cs:21-70`
- `PoTool.Api/Services/PortfolioReadModelStateService.cs:66-156`

Those time-based pieces apply to **project/work-package snapshot rows**, not to a separate budget amount.

## 4. Effort and story-point storage

### 4.1 Effort

Effort is stored on work items and described as hours:

- `WorkItemEntity.Effort : int?` (`PoTool.Api/Persistence/Entities/WorkItemEntity.cs:68-72`)
- `WorkItemDto.Effort : int?` (`PoTool.Shared/WorkItems/WorkItemDto.cs:15-17`)
- `WorkItemWithValidationDto.Effort : int?` (`PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs:14-17`)

Effort also appears in delivery-domain models:

- `CanonicalWorkItem.Effort : double?`
- `DeliveryTrendWorkItem.Effort : int?`

Files:

- `PoTool.Core.Domain/Models/CanonicalWorkItem.cs:11-20`, `51`
- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/SprintDeliveryProjectionInputs.cs:12-25`, `56`

### 4.2 Story points

Story points are also stored on work items:

- `WorkItemEntity.StoryPoints : int?`
- `WorkItemDto.StoryPoints : int?`
- `WorkItemWithValidationDto.StoryPoints : int?`

Files:

- `PoTool.Api/Persistence/Entities/WorkItemEntity.cs:73-76`
- `PoTool.Shared/WorkItems/WorkItemDto.cs:25-30`
- `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs:23-28`

### 4.3 Whether budgets are cost-based, effort-based, or both

No explicit runtime budget field or budget entity was found, so the runtime model does not currently expose a direct cost-based budget structure or a direct effort-based budget structure under `Budget*` names.

What is explicitly present:

- effort in hours on work items
- story points on work items
- `TotalWeight` on portfolio snapshot rows

The portfolio snapshot write path maps `TotalWeight` from domain snapshot items into persistence:

- `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs:51-68`

The capture path builds snapshot items from epic progress:

- `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:143-161`

The domain snapshot model names that value `Weight` / `TotalWeight`, not `Budget`:

- `PoTool.Core.Domain/Domain/DeliveryTrends/Models/PortfolioSnapshotModels.cs:55-61`, `118-151`

## 5. Storage locations

### 5.1 Tables / EF Core persistence

The current relevant persistence locations are:

| Table / entity | Purpose |
| --- | --- |
| `Products` / `ProductEntity` | Product ownership and configuration |
| `WorkItems` / `WorkItemEntity` | Epic project fields plus effort/story points |
| `ResolvedWorkItems` / `ResolvedWorkItemEntity` | Resolved product/epic/feature/sprint linkage |
| `PortfolioSnapshots` / `PortfolioSnapshotEntity` | Product-scoped snapshot headers |
| `PortfolioSnapshotItems` / `PortfolioSnapshotItemEntity` | Project/work-package snapshot rows |

Migration evidence:

- `PoTool.Api/Migrations/20260326141717_PhaseAFoundationContracts.cs:11-33`
- `PoTool.Api/Migrations/20260326221853_AddPortfolioSnapshots.cs:12-75`

### 5.2 Shared DTOs

Shared DTOs carrying relevant data:

- `PoTool.Shared/Settings/ProductDto.cs:24-45`
- `PoTool.Shared/WorkItems/WorkItemDto.cs:7-37`
- `PoTool.Shared/WorkItems/WorkItemWithValidationDto.cs:6-35`
- `PoTool.Shared/Metrics/PortfolioConsumptionDtos.cs:206-218`, `301-323`, `370-407`, `472-497`, `547-584`

The portfolio DTOs expose:

- `ProjectNumber`
- `WorkPackage`
- `Progress`
- `Weight`

No shared DTO containing an explicit budget amount was found.

### 5.3 API surface

Project/work-package data is exposed through portfolio read APIs and snapshot capture:

- `POST /api/portfolio/snapshots/capture`
  - `PoTool.Api/Controllers/PortfolioSnapshotsController.cs:23-38`

- `GET /api/portfolio/progress`
- `GET /api/portfolio/snapshots`
- `GET /api/portfolio/comparison`
- `GET /api/portfolio/trends`
- `GET /api/portfolio/signals`
  - `PoTool.Api/Controllers/MetricsController.cs:304-482`

The portfolio filter model also accepts project/work-package filters:

- `PortfolioReadQueryOptions.ProjectNumber`
- `PortfolioReadQueryOptions.WorkPackage`
- project/work-package validation against persisted snapshot rows

File:

- `PoTool.Api/Services/PortfolioFilterResolutionService.cs:101-118`, `186-285`

## 6. Assumptions and constraints visible in code

The following constraints are explicit in the current implementation:

1. **Project fields are epic-only in canonical/domain semantics**
   - `WorkItemFieldSemantics` normalizes them away for non-epics
   - `PoTool.Core.Domain/Models/WorkItemFieldSemantics.cs:8-35`

2. **Portfolio snapshot capture requires `ProjectNumber`**
   - capture throws if included epics are missing `ProjectNumber`
   - `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:128-141`

3. **`ProjectElement` is mapped into snapshot `WorkPackage`**
   - `PoTool.Api/Services/PortfolioSnapshotCaptureDataService.cs:147-152`

4. **Snapshot headers are product-scoped**
   - `PortfolioSnapshotEntity.ProductId`
   - `PoTool.Api/Persistence/Entities/PortfolioSnapshotEntity.cs:22-27`

5. **Snapshot rows must match the snapshot header product**
   - persistence mapper validates that all items match the header `ProductId`
   - `PoTool.Api/Services/PortfolioSnapshotPersistenceService.cs:46-49`

6. **Project filtering is performed against persisted snapshot rows**
   - valid projects are resolved from `PortfolioSnapshotItems`
   - `PoTool.Api/Services/PortfolioFilterResolutionService.cs:215-229`

7. **No runtime budget model is present**
   - no `Budget*` entity, `DbSet`, shared DTO, or API handler/controller contract was found in the inspected runtime code

## 7. Conclusion

The implemented runtime model currently consists of:

- a standalone **Product** entity
- epic-level project/funding fields on work items
- resolved hierarchy data linking work items to products
- product-scoped portfolio snapshots whose rows are keyed by `ProjectNumber` and optional `WorkPackage`
- work-item effort and story-point fields

The implemented runtime model does **not** currently expose a standalone **Project** entity or a standalone **Budget** entity. The project concept is represented by epic fields and snapshot keys, while the budget concept appears in documentation but was not found as an implemented runtime persistence/API/DTO model.
