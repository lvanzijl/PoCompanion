# UI / Frontend Rules — Blazor WebAssembly

## 1. Platform
- MUST use **Blazor WebAssembly**.
- MUST remain deployable as static assets.
- MUST NOT depend on server-side rendering or live server connections for UI behavior.

## 2. UI component policy
- MUST use **freely available, open-source Blazor component libraries**.
- Components MUST be implemented in **Blazor (C#)**.
- MUST NOT build custom UI widgets from scratch if an equivalent exists in an approved library.
- MUST NOT implement UI widgets in JavaScript or TypeScript.
- MUST NOT wrap JS/TS widgets as "Blazor components".

### Approved component libraries (OSS)
- **MudBlazor** (MIT)
- **Radzen Blazor Components** (MIT)
- **Fluent UI Blazor** (MIT)

Mixing libraries is discouraged and requires explicit justification.

## 3. JavaScript / TypeScript
- MUST NOT use custom JavaScript or TypeScript for UI behavior.
- MAY use JS/TS only for unavoidable browser-level gaps, never for widgets.
- Any exception MUST:
  - be explicitly approved
  - be written in TypeScript
  - be centralized in a single interop layer
  - not expose UI logic

## 4. Styling
- MUST use **CSS Isolation** per component.
- MUST use a **dark theme only**.
- MUST NOT provide a light theme.
- MUST NOT provide a theme switch.
- MUST define colors, spacing, and typography via CSS variables/tokens.
- MUST NOT hardcode colors in component styles.

## 5. Bootstrap
- MAY use **Bootstrap CSS** (grid, spacing, typography).
- MUST NOT use **Bootstrap JavaScript components** (modal, dropdown, collapse, tooltip, etc.).

## 6. Forms & validation
- MUST use **FluentValidation**.
- MUST keep validation logic in dedicated validator classes.
- MUST NOT embed complex validation logic directly in UI components.

## 7. State & UI behavior
- MUST keep UI components as thin as possible.
- SHOULD separate state management from presentation.
- SHOULD avoid global mutable state unless explicitly required.

## 8. Accessibility & consistency
- SHOULD use components from the chosen library consistently.
- SHOULD rely on library defaults for accessibility rather than custom behavior.

## 9. API interaction (UI side)
- MUST use API clients generated via **OpenAPI / NSwag**.
- MUST NOT call HttpClient directly from UI components.
- MUST route all backend interaction via application services.

## 10. Error handling & resilience (UI-facing)
- MUST handle API errors in a consistent, centralized manner.
- MUST show deterministic UI error states (no silent failures).
- MUST support retries for eligible operations.
- MUST NOT retry non-idempotent calls unless explicitly designed as idempotent.
- MUST surface a clear error when retries are exhausted.

## 11. Observability
- MUST log UI-relevant failures with correlation identifiers.
- MUST NOT log sensitive or classified data.

## 12. Non-goals
- No custom JS widgets
- No UI logic in JS/TS
- No ad-hoc component creation
- No light mode

## 13. TFS Data Selection

When users need to input TFS data that references an existing TFS entity, MUST NOT require manual entry of raw identifiers.

### 13.1 Searchable selection
- Work items MUST be selectable via searchable autocomplete or dropdown
- Search MUST support:
  - Work item ID (exact match)
  - Work item title (partial match)
- MUST display both ID and title in search results
- MUST show clear visual feedback during search

### 13.2 Structured TFS data
For TFS structured data fields:
- **Area Path**: MUST use searchable dropdown or tree picker
- **Iteration Path**: MUST use searchable dropdown or tree picker  
- **Work Item State**: MUST use dropdown with valid states for the work item type
- **Work Item Type**: MUST use dropdown with available types
- **Assigned To**: MUST use searchable user picker

### 13.3 Other TFS data fields
For TFS data fields not explicitly defined above:
- MUST choose the most appropriate UI component based on the data type and user experience requirements
- MUST follow all existing UI rules (sections 1-12) when selecting components
- MUST prioritize searchability and discoverability for large datasets
- MUST use approved component library controls (section 2)
- SHOULD prefer autocomplete/searchable dropdowns for reference data
- SHOULD use simple dropdowns only for small, static lists
- SHOULD use text input only when free-form entry is genuinely required

### 13.4 Implementation
- MUST use approved component library controls (MudBlazor Autocomplete, Radzen AutoComplete, or Fluent UI ComboBox)
- MUST implement debounced search to reduce API calls
- MUST handle empty results gracefully with clear messaging
- MUST provide keyboard navigation support
- Search results SHOULD be limited to a reasonable page size (e.g., 50 items)

### 13.5 Rationale
- Reduces user errors from manual ID entry
- Improves discoverability of existing work items
- Enforces referential integrity at the UI level
- Provides better user experience aligned with modern TFS/Azure DevOps UI patterns
