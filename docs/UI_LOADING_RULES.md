## UI Loading and Rendering Rules

### Principle

Pages must render immediately. Data loading must never block the first render.  
The user should always see the page structure before data is retrieved.

Core principle:

Render first. Load data progressively.

---

### 1. Never block first render

Pages must not delay rendering while waiting for data.

Avoid patterns where the page performs heavy data loading before rendering the UI.

Page lifecycle methods must not block rendering with long-running calls.

Rendering the page structure must always occur before data retrieval finishes.

---

### 2. Always render the page skeleton first

The page layout must appear immediately.

The following elements must render even if data is not yet available:

- page title
- navigation elements
- filter bars
- cards or panels
- placeholders or skeleton loaders
- loading indicators inside components

The user must always see the structure of the page.

---

### 3. Load data asynchronously after render

Data loading should start after the page is rendered.

Preferred pattern:

- first render displays layout and placeholders
- background tasks retrieve data
- components update when data arrives

Pages should trigger background loading instead of waiting synchronously.

---

### 4. Use progressive component loading

Large pages must load components independently.

Do not use a single method that loads all dashboard data.

Instead, load each widget or card separately so that sections of the page become usable as soon as their data arrives.

Example categories that should load independently:

- trend graphs
- validation summaries
- pull request statistics
- pipeline metrics
- work item counts

---

### 5. Use component-level loading states

Loading indicators must be scoped to components.

Each card or panel should manage its own loading state.

Examples:

- skeleton card
- loading spinner inside a chart
- placeholder text until data arrives

Do not hide the entire page behind a global loading indicator.

---

### 6. Avoid full-page loading gates

Patterns that hide the entire page until data arrives are forbidden.

Examples of forbidden behaviour:

- blocking page rendering with a global `isLoading` flag
- rendering nothing until all data is retrieved
- showing only a single spinner for the whole page

The page layout must always be visible.

---

### 7. Cache expensive queries

Expensive operations should use short-lived caching when appropriate.

Examples include:

- historical trend data
- backlog analytics
- pull request analysis
- pipeline statistics

Caching avoids repeated delays when navigating between pages.

---

### 8. Load only visible data

Pages must not load data that is not immediately visible.

Examples of data that should load only when required:

- drill-down views
- detailed breakdown panels
- secondary charts
- expanded lists

Lazy loading must be used for deeper views.

---

### 9. Keep pages lightweight

Pages are responsible only for:

- rendering UI structure
- triggering asynchronous loading
- reacting to state changes

Heavy logic and data transformation must exist in services.

Pages must not perform expensive calculations.

---

### 10. Cancel unnecessary background work

If a user navigates away while data is loading, pending operations should be cancelled where possible.

Background tasks should not continue unnecessarily after the page is no longer active.

This prevents wasted processing and improves responsiveness.

---

### Expected UX behavior

Correct behaviour:

1. User opens a page.
2. The page layout appears immediately.
3. Cards and widgets fill with data progressively.

Users must never experience a blank page while the application loads data.
