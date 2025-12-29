# UI & UX Rules — PO Companion (Blazor WebAssembly)

## 1. Platform & Hosting Model
- The frontend is a **Blazor WebAssembly application hosted by ASP.NET Core**.
- There is **no desktop shell, WebView host, or alternative runtime**.
- All UI rules assume browser-based execution.
- Hosting concerns (startup, DI composition, environment configuration) belong to the ASP.NET Core host, not the UI layer.

## 2. Architectural Position of the UI
- The UI layer is **purely presentational and orchestration-focused**.
- The UI MUST NOT:
  - Contain business logic
  - Contain domain rules
  - Perform direct data persistence
- The UI MAY:
  - Transform DTOs into view models
  - Manage UI state
  - Coordinate calls to application services

## 3. Communication & Data Access
- The UI MUST communicate with the backend **only via typed frontend service abstractions**.
- Direct use of `HttpClient` in pages or components is forbidden.
- All backend interaction flows through:
  - Explicit frontend service interfaces
  - Strongly typed request/response models
- No implicit coupling to API routes or controllers is allowed.

## 4. Component Model
- UI MUST be composed of **small, focused components**.
- Components SHOULD be:
  - Stateless where possible
  - Parameter-driven
  - Reusable across views
- Shared UI logic MUST be extracted into:
  - Components
  - UI-specific helper classes
  - UI services

## 5. UI Library
- **MudBlazor is the mandatory UI component library.**
- Custom components MAY be created **only if**:
  - No MudBlazor equivalent exists
  - Or MudBlazor cannot meet UX or technical constraints
- “Equivalent” means:
  - Functional parity
  - Accessibility support
  - Keyboard navigation
  - Virtualization where applicable
  - Consistent theming

## 6. Navigation
- The application uses a **sidebar-driven navigation model**.
- Primary navigation MUST be placed in the sidebar.
- Contextual or secondary actions MAY exist within views but MUST NOT replace sidebar navigation.
- Navigation must always be explicit and reversible.

## 7. UX Authority
- **UX principles override UI or technical convenience.**
- When UX principles conflict with implementation simplicity, UX wins.
- Deviations from UX principles require explicit justification and review.

## 8. UX Principles (Binding)
The following principles are **hard constraints**:

- **Clarity over density**  
  Prefer explicit UI states over compact but ambiguous layouts.

- **State is visible**  
  Loading, empty, error, filtered, and disabled states MUST be visually distinct.

- **Predictability**  
  Identical actions behave identically across the application.

- **Progressive disclosure**  
  Advanced functionality is hidden until relevant.

- **No surprise navigation**  
  User-triggered navigation must be intentional and reversible.

## 9. State Management
- UI state MUST be explicit and observable.
- Hidden state mutation is forbidden.
- State SHOULD be scoped as narrowly as possible:
  - Component-level by default
  - Shared services only when unavoidable

## 10. Form Validation
Validation strategy depends on data source:

- **Locally cached data**
  - Validation occurs **on change**
  - Feedback must be immediate and inline

- **Remote data (TFS server calls)**
  - Validation occurs **on submit**
  - UI must clearly indicate that a remote call will occur

- Mixed validation strategies within a single form are forbidden unless explicitly justified.

## 11. Error Handling & Feedback
- Errors MUST be:
  - User-readable
  - Contextual
  - Actionable where possible
- Silent failures are forbidden.
- Technical details MAY be logged but MUST NOT be shown to users.

## 12. Performance & Responsiveness
- Lists with potentially large datasets MUST use virtualization.
- Long-running operations MUST provide visible feedback.
- UI-blocking operations are forbidden.

## 13. Accessibility
- All interactive elements MUST:
  - Be keyboard accessible
  - Have visible focus states
  - Use semantic HTML where possible
- Accessibility is mandatory, not optional.

## 14. Theming
- The application uses a **single, fixed dark theme**.
- No light theme or user-selectable theming is supported.
- All custom UI elements MUST conform to the dark theme.

## 15. UI Testing Expectations
- UI logic with conditional behavior MUST be testable.
- Critical user flows SHOULD be covered by automated tests.
- Purely visual structure does not require tests; behavior does.
