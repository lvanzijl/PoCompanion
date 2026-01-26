# Planning Board Decommission Checklist

This document classifies all existing planning-related code as part of the migration to the new table-based Planning Board with Products as Columns.

## Classification Legend
- **REMOVE** - Obsolete code that does not fit the new specification
- **REUSE** - Generic utilities that remain useful
- **REPLACE** - Conceptually similar but will be reimplemented

---

## 1. UI Components (PoTool.Client/Components/ReleasePlanning/)

| File | Status | Reason |
|------|--------|--------|
| ReleasePlanningBoard.razor | **REPLACE** | New table-based board with product columns instead of objective lanes |
| ReleasePlanningBoard.razor.css | **REPLACE** | Styling needs to match new table structure |
| LaneRow.razor | **REMOVE** | Lanes are obsolete; replaced by ProductColumns |
| LaneRow.razor.css | **REMOVE** | Styling for obsolete lanes |
| EpicCard.razor | **REPLACE** | Similar concept but adapted for new cell layout |
| ConnectorRenderer.razor | **REMOVE** | Connectors between epics are explicitly out of scope |
| UnplannedEpicsList.razor | **REPLACE** | Needs product grouping for all-products scope |
| UnplannedEpicsDialog.razor | **REMOVE** | Replaced by integrated Unplanned Epics Panel |
| ObjectiveEpicsDialog.razor | **REMOVE** | Objective concept removed; products are the grouping |
| AddLaneDialog.razor | **REMOVE** | Lanes are obsolete |
| AddMilestoneLineDialog.razor | **REMOVE** | Replaced by simple MarkerRow creation |
| AddIterationLineDialog.razor | **REMOVE** | Replaced by simple MarkerRow creation |
| SplitEpicDialog.razor | **REMOVE** | Epic split is not in scope |
| ExportDialog.razor | **REMOVE** | Export is explicitly out of scope |
| BoardRenderModel.cs | **REPLACE** | New render model for table layout |
| EventArgs.cs | **REPLACE** | Simplified event args for new behavior |

---

## 2. Pages

| File | Status | Reason |
|------|--------|--------|
| PlanningWorkspace.razor | **REPLACE** | Needs major update: remove Related Dashboards, Validation, Actions sections; add Scope Selector and new board |

---

## 3. Services

| File | Status | Reason |
|------|--------|--------|
| ReleasePlanningService.cs | **REPLACE** | New API endpoints for product-column-based board |

---

## 4. DTOs (PoTool.Shared/ReleasePlanning/)

| File | Status | Reason |
|------|--------|--------|
| ReleasePlanningDtos.cs | **REPLACE** | New DTOs: ProductColumnDto, BoardRowDto, MarkerRowDto, EpicPlacementDto (new structure) |
| ExportDtos.cs | **REMOVE** | Export is out of scope |

### Specific DTOs to Remove:
- `LaneDto` - Lanes replaced by ProductColumns
- `ConnectorDto` - Connectors are out of scope
- `ConnectorType` enum - Out of scope
- `ObjectiveEpicDto` - Objectives replaced by Products
- `EpicSplitResultDto` - Split is out of scope
- `EpicFeatureDto` - Split is out of scope
- Export-related DTOs

### Specific DTOs to Replace:
- `EpicPlacementDto` - Now uses ProductId instead of LaneId
- `MilestoneLineDto` → `MarkerRowDto` (Release Line)
- `IterationLineDto` → `MarkerRowDto` (Iteration Line)
- `ReleasePlanningBoardDto` → `PlanningBoardDto` (new structure)
- `ValidationIndicator` - Keep as is

---

## 5. Entities (PoTool.Api/Persistence/Entities/)

| File | Status | Reason |
|------|--------|--------|
| LaneEntity.cs | **REMOVE** | Lanes replaced by ProductColumns |
| EpicPlacementEntity.cs | **REPLACE** | Now references ProductId instead of LaneId |
| MilestoneLineEntity.cs | **REPLACE** | Becomes MarkerRowEntity (type: Release) |
| IterationLineEntity.cs | **REPLACE** | Becomes MarkerRowEntity (type: Iteration) |

### New Entities to Create:
- `BoardRowEntity` - Ordered rows (including marker rows)
- `PlanningBoardSettingsEntity` - Scope, product visibility, persisted state

---

## 6. Commands (PoTool.Core/ReleasePlanning/Commands/)

| File | Status | Reason |
|------|--------|--------|
| CreateLaneCommand.cs | **REMOVE** | Lanes obsolete |
| DeleteLaneCommand.cs | **REMOVE** | Lanes obsolete |
| CreateEpicPlacementCommand.cs | **REPLACE** | Uses ProductId and RowId |
| UpdateEpicPlacementCommand.cs | **REPLACE** | Updated structure |
| DeleteEpicPlacementCommand.cs | **REUSE** | Logic remains similar |
| MoveEpicCommand.cs | **REPLACE** | Now moves within product column only |
| ReorderEpicsInRowCommand.cs | **REPLACE** | Updated for new cell structure |
| SplitEpicCommand.cs | **REMOVE** | Split is out of scope |
| CreateMilestoneLineCommand.cs | **REPLACE** | Becomes CreateMarkerRowCommand |
| UpdateMilestoneLineCommand.cs | **REPLACE** | Becomes UpdateMarkerRowCommand |
| DeleteMilestoneLineCommand.cs | **REPLACE** | Becomes DeleteMarkerRowCommand |
| CreateIterationLineCommand.cs | **REMOVE** | Merged into MarkerRow |
| UpdateIterationLineCommand.cs | **REMOVE** | Merged into MarkerRow |
| DeleteIterationLineCommand.cs | **REMOVE** | Merged into MarkerRow |
| RefreshValidationCacheCommand.cs | **REMOVE** | Validation indicators out of scope |

### New Commands to Create:
- `CreateBoardRowCommand` - Insert row above/below
- `DeleteBoardRowCommand` - Remove empty row
- `MoveMarkerRowCommand` - Reorder marker rows
- `UpdateBoardScopeCommand` - Set all products / single product scope
- `UpdateProductVisibilityCommand` - Show/hide product columns

---

## 7. Queries (PoTool.Core/ReleasePlanning/Queries/)

| File | Status | Reason |
|------|--------|--------|
| GetReleasePlanningBoardQuery.cs | **REPLACE** | Returns new board structure |
| GetUnplannedEpicsQuery.cs | **REPLACE** | Groups by product |
| GetObjectiveEpicsQuery.cs | **REMOVE** | Objectives obsolete |
| GetEpicFeaturesQuery.cs | **REMOVE** | Split is out of scope |

### New Queries to Create:
- `GetBoardSettingsQuery` - Get scope and visibility settings
- `GetProductsForBoardQuery` - Get products for column picker

---

## 8. Handlers (PoTool.Api/Handlers/ReleasePlanning/)

All handlers corresponding to REMOVE/REPLACE commands/queries follow the same status.

---

## 9. Controller

| File | Status | Reason |
|------|--------|--------|
| ReleasePlanningController.cs | **REPLACE** | New endpoints for new structure; remove export, split, validation refresh |

### Endpoints to Remove:
- `POST /export` - Export out of scope
- `POST /epics/{id}/split` - Split out of scope
- `GET /epics/{id}/features` - Split out of scope
- `POST /validation/refresh` - Validation out of scope
- All lane endpoints
- Separate iteration/milestone line endpoints (merged into marker rows)

---

## 10. Persistence Impact

### Tables to Remove:
- `Lanes` table (if exists)

### Tables to Modify:
- `EpicPlacements` - Add ProductId column, remove LaneId
- Consider merging Milestone/Iteration lines into single `BoardRows` table

### New Tables:
- `BoardRows` - Ordered rows with optional MarkerType
- `PlanningBoardSettings` - Scope, product visibility

---

## 11. Workspace Navigation

| File | Status | Reason |
|------|--------|--------|
| PlanningWorkspace.razor | **REPLACE** | Remove: Related Dashboards, Validation & Conflicts, Actions sections |
| WorkspaceRoutes.cs | **REUSE** | Route path stays the same |
| Landing.razor | **REUSE** | Planning intent stays the same |

---

## Summary

| Category | REMOVE | REPLACE | REUSE |
|----------|--------|---------|-------|
| UI Components | 9 | 7 | 0 |
| DTOs | 6 | 5 | 1 |
| Entities | 1 | 3 | 0 |
| Commands | 8 | 6 | 1 |
| Queries | 2 | 2 | 0 |
| Navigation | 0 | 1 | 2 |

**Total files affected: ~58**

---

## Implementation Order

1. **Phase 1**: Remove obsolete UI sections from PlanningWorkspace (dashboards, validation, actions)
2. **Phase 2**: Create new DTOs and entities
3. **Phase 3**: Create/update commands and queries
4. **Phase 4**: Create new handlers
5. **Phase 5**: Update controller
6. **Phase 6**: Build new UI components
7. **Phase 7**: Remove obsolete files

This phased approach ensures the application remains buildable throughout the migration.
