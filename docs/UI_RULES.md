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
- The application uses an **intent-driven navigation model**.
- Navigation is organized around **user intent**, not pages or features.
- After profile selection, users MUST land on a **Landing page** with a limited set of **explicit intent entry points**.
- Selecting an intent establishes **navigation context** for all subsequent views.
- There is **no permanent feature-based navigation menu** (e.g. sidebar) in the final architecture.
- Screens function as **context-aware workspaces**.
- A workspace MAY be entered from multiple paths and MUST adapt its defaults based on context.
- Navigation beyond the Landing page MUST be **contextual and progressive**, exposed through explicit next-step actions within the current view.
- Global navigation is limited to **meta-level actions only** (e.g. Settings, Profile management, Return to Landing).
- The top workspace navigation uses `WorkspaceNavigationBar` and `WorkspaceNavItem` and must not be implemented using buttons.
- Navigation MUST remain explicit, reversible, and allow users to exit a flow without side effects.

### 6.1 Workspace entry types

Workspace entry pages are divided into two explicit categories:

- **Navigation workspaces** — Health, Delivery, and Planning
- **Signal workspace** — Trends

The category determines what the entry page is allowed to do.

#### Navigation workspaces

Navigation workspaces exist to route the user to the next page with minimal friction.

Rules:

- the entry page is a **navigation hub**, not a dashboard
- the hub contains **no heavy data loading** on entry
- the hub renders immediately and remains lightweight
- hub tiles are **STATIC**
- tiles represent navigation intent, not runtime status
- tiles stay visible regardless of data availability
- signal badges, counts, warnings, or runtime summaries do **not** belong on the hub tiles

Current examples:

- `PoTool.Client/Pages/Home/HealthWorkspace.razor`
- `PoTool.Client/Pages/Home/DeliveryWorkspace.razor`
- `PoTool.Client/Pages/Home/PlanningWorkspace.razor`

#### Signal workspace

The Trends entry page is the only workspace entry page that may expose runtime signal badges on its tiles.

Rules:

- the workspace still renders its tile shell immediately
- dynamic tile signals must answer **"why click now?"**
- signal badges are allowed only when they communicate a real, time-sensitive condition
- when no real signal exists, the tile falls back to normal navigation copy without blocking the page

Example:

- valid: a Trends tile showing a worsening PR time-to-merge badge or an unstable pipeline badge
- invalid: a navigation hub tile showing a default warning badge just to make the card look active

Current example:

- `PoTool.Client/Pages/Home/TrendsWorkspace.razor`

### 6.2 Tile behavior rules

#### STATIC tiles

STATIC tiles:

- describe the destination purpose or user goal
- have no runtime dependency
- are always visible
- remain understandable without live data

STATIC tiles must not:

- wait for queries before rendering
- imply urgency through fake defaults
- show placeholder badges that look like real signals

#### DYNAMIC tiles

DYNAMIC tiles are allowed **only** in signal workspaces.

DYNAMIC tiles must:

- represent a time-sensitive signal
- justify navigation with a clear reason to investigate now
- degrade gracefully when the signal is missing, insufficient, or unavailable
- load progressively without blocking the tile shell or surrounding navigation
- keep data retrieval lightweight enough for an entry-page signal

DYNAMIC tiles must not:

- require heavy hub-entry queries just to make navigation usable
- hide the tile when the signal cannot be calculated
- show misleading defaults that imply health, urgency, or stability without evidence

### 6.3 Hub constraints

Navigation hubs must:

- avoid heavy data loading on entry
- act as a navigation layer first
- keep cross-workspace movement explicit
- avoid duplicate navigation structures inside child pages

Navigation hubs must not:

- become summary dashboards
- duplicate subpage navigation with tabs inside the destination pages
- add secondary menu systems that compete with workspace navigation

Current implementation reference:

- Health, Delivery, and Planning are navigation hubs with STATIC tiles.
- Trends is the signal workspace and may mix STATIC tiles with DYNAMIC tiles when a real signal exists.

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

## 9. Button Hierarchy & Emphasis

Buttons are supporting UI elements and must never dominate the interface.

### Visual hierarchy rule

The UI must always follow this visual importance order:

1. Navigation tiles
2. Cards and dashboards
3. Buttons

Buttons must never visually compete with navigation tiles. If a button draws more attention than a tile, the button style is incorrect.

### Button roles

Buttons are divided into three semantic roles. Each role maps to a specific visual emphasis level.

Implementation note:
- `CompactButton` is the standard implementation mechanism for normal UI actions so semantic roles stay consistent across the application.

#### Utility buttons (lowest emphasis)

Purpose:
Small operational tools used in headers, toolbars, and page utilities.

Examples:
Refresh, Sync, Home, Reporting, Snapshots, Scope.

Visual rules:
- Minimal visual weight
- No strong borders
- Icon allowed but muted
- Must blend into the surrounding UI

MudBlazor style guideline:
Variant Text  
Color Default

These buttons must never dominate the page.

#### Action buttons (medium emphasis)

Purpose:
Start a workflow or navigation step that is meaningful but not dangerous.

Examples:
Edit Roadmap, Back to Delivery, Bug Triage, Validation Triage.

Visual rules:
- Slightly stronger than utility buttons
- Border allowed but visually muted
- Must remain secondary to cards and navigation tiles

MudBlazor style guideline:
Variant Outlined  
Color Default

The border must not be bright or visually heavy.

#### Critical buttons (high emphasis)

Purpose:
Actions with destructive or irreversible consequences.

Examples:
Reset All.

Visual rules:
- Strong visual emphasis
- Clearly separated from other controls
- Used rarely

MudBlazor style guideline:
Variant Filled  
Color Error

### Icon usage

Icons are allowed in buttons because the application relies on domain metaphors.

Rules:
- Maximum one icon per button
- Icon must be placed left of the label
- Icon must not dominate the text
- Icon color should follow button color rules

Icons must support recognition, not create visual noise.

### Design safety rule

If a button visually competes with:
- navigation tiles
- dashboard cards
- major visualizations

then the button styling is incorrect and must be reduced in emphasis.

## 10. State Management
- UI state MUST be explicit and observable.
- Hidden state mutation is forbidden.
- State SHOULD be scoped as narrowly as possible:
  - Component-level by default
  - Shared services only when unavoidable

## 11. Form Validation
Validation strategy depends on data source:

- **Locally cached data**
  - Validation occurs **on change**
  - Feedback must be immediate and inline

- **Remote data (TFS server calls)**
  - Validation occurs **on submit**
  - UI must clearly indicate that a remote call will occur

- Mixed validation strategies within a single form are forbidden unless explicitly justified.

## 12. Error Handling & Feedback
- Errors MUST be:
  - User-readable
  - Contextual
  - Actionable where possible
- Silent failures are forbidden.
- Technical details MAY be logged but MUST NOT be shown to users.

## 13. Performance & Responsiveness
- Lists with potentially large datasets MUST use virtualization.
- Long-running operations MUST provide visible feedback.
- UI-blocking operations are forbidden.

## 14. Accessibility
- All interactive elements MUST:
  - Be keyboard accessible
  - Have visible focus states
  - Use semantic HTML where possible
- Accessibility is mandatory, not optional.

## 15. Theming
- The application uses a **single, fixed dark theme**.
- No light theme or user-selectable theming is supported.
- All custom UI elements MUST conform to the dark theme.

## 16. UI Testing Expectations
- UI logic with conditional behavior MUST be testable.
- Critical user flows SHOULD be covered by automated tests.
- Purely visual structure does not require tests; behavior does.

## 17. Primary Visualization Pattern (Analytical Pages)

An **analytical page** is any page whose primary purpose is trend analysis, behavioral modeling, structural system insight, or multi-chart comparison.

### Definition
If a page is analytical:

1. It MUST designate exactly one **Primary Visualization**.
2. The Primary Visualization MUST:
   - Span full width (12 columns / full container width)
   - Use `PrimaryVisualizationSection` component (enforces `--analytical-primary-height: 400px`)
   - Visually dominate summary tiles and filters
3. Secondary visualizations MUST:
   - Appear below the primary visualization
   - Use `SecondaryVisualizationGrid` component with `Columns="2"` or `Columns="3"`
   - Enforce equal height via `analytical-secondary-cell` class (`--analytical-secondary-height: 320px`)
4. Configuration UI (filters, selectors):
   - MUST NOT visually dominate charts
   - MUST be collapsible or inline-editable
   - MUST default to collapsed state if meaningful defaults exist
5. Summary tiles:
   - MUST use compact padding
   - MUST NOT exceed visual weight of the primary visualization

### Implementation

Use the semantic layout components from `PoTool.Client/Components/Analytical/`:

```razor
<AnalyticalPageLayout>
    <PrimaryVisualizationSection>
        <!-- one full-width primary chart, Height="340px" -->
    </PrimaryVisualizationSection>

    <SecondaryVisualizationGrid Columns="3">
        <MudPaper Class="pa-3 analytical-secondary-cell"><!-- chart, Height="260px" --></MudPaper>
        <MudPaper Class="pa-3 analytical-secondary-cell"><!-- chart, Height="260px" --></MudPaper>
        <MudPaper Class="pa-3 analytical-secondary-cell"><!-- chart, Height="260px" --></MudPaper>
    </SecondaryVisualizationGrid>
</AnalyticalPageLayout>
```

### CSS Tokens (in `app.css`)

| Token | Value | Usage |
|---|---|---|
| `--analytical-primary-height` | 400px | `min-height` of primary visualization container |
| `--analytical-secondary-height` | 320px | `min-height` of secondary visualization cells |
| `--analytical-primary-chart-height` | 340px | SVG chart height inside primary container |
| `--analytical-secondary-chart-height` | 260px | SVG chart height inside secondary containers |

### Applies To
- Portfolio Progress Trend (`/home/portfolio-progress`)
- Sprint Trend
- Any future "Trends (Past)" pages
