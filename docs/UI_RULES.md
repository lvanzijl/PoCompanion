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
