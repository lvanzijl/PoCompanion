# Canonical Filter Model

## Goal

This document defines the canonical filter model for PoTool.

The model must:

- work across all pages
- support global and page-specific filters without ambiguity
- be predictable, extensible, and strongly typed
- clearly separate selection, validation, applicability, and execution
- be implementable in .NET and Blazor without page-specific special cases in the core system

This document defines the target design.
It does not describe the current implementation.

---

## 1. Filter categories

### 1.1 Global Filters

**Purpose**

Global Filters define the business scope of the user’s navigation context.
They answer: **“What part of the organization or portfolio am I looking at?”**

**Lifecycle**

- selected once and reused across page navigation
- changed relatively infrequently
- expected to remain stable while the user moves between related pages

**Scope**

- available application-wide
- visible in the global filter summary
- considered part of the canonical navigation context

**Examples**

- Product
- Project

### 1.2 Conditional Global Filters

**Purpose**

Conditional Global Filters are remembered globally, but are only effective on pages that explicitly support them.
They answer: **“What shared contextual lens should apply when the destination page can use it?”**

**Lifecycle**

- selected once and remembered across navigation
- may become temporarily ignored on pages where they do not apply
- must not be lost simply because the current page cannot use them

**Scope**

- owned by the global filter system
- only effective when the destination page declares them applicable
- still visible to the user as remembered context when relevant

**Examples**

- Time

### 1.3 Page Filters

**Purpose**

Page Filters refine a specific page or workflow.
They answer: **“How should this page be narrowed further?”**

**Lifecycle**

- created and owned by the active page
- reset when navigating to a different page unless that page explicitly defines compatible persistence
- may be deep-linkable for that page only

**Scope**

- local to one page or one closely-related page flow
- not part of the global cross-page contract
- not shown in the global filter summary unless the page opts into a page-level summary section

**Examples**

- Team
- Repository
- ValidationType
- Pipeline
- PR Status

---

## 2. Core filter types

## 2.1 Global filters

### Product

- **Category:** Global Filter
- **Type:** Single-select
- **Value structure:**
  - `ProductRef`
    - `ProductId: int`
    - `DisplayName: string`
- **Required or optional:** Optional
- **Meaning of no value:** All products in the user’s authorized scope
- **Dependencies:** None
- **Notes:** Product is the primary global analytics boundary.

### Project

- **Category:** Global Filter
- **Type:** Single-select
- **Value structure:**
  - `ProjectRef`
    - `ProjectKey: string`
    - `DisplayName: string`
- **Required or optional:** Optional
- **Meaning of no value:** All projects in the current product scope or authorized scope
- **Dependencies:** None at selection time; validated against Product when both are selected
- **Notes:** Project is global because it changes the fundamental business slice, not just one page view.

## 2.2 Conditional global filters

### Time

- **Category:** Conditional Global Filter
- **Type:** Union filter with mutually exclusive modes
- **Value structure:**
  - `TimeFilterValue`
    - `Mode: TimeFilterMode`
    - `SingleSprint: SprintRef?`
    - `SprintRange: SprintRangeValue?`
    - `DateRange: DateRangeValue?`
- **Required or optional:** Optional
- **Meaning of no value:** No explicit shared time constraint
- **Dependencies:** Applicability depends on page contract; some pages may support only specific modes

#### Time sub-types

**Single Sprint**
- `SprintRef`
  - `SprintId: int`
  - `SprintName: string`
  - `TeamId: int?`

**Sprint Range**
- `SprintRangeValue`
  - `FromSprintId: int`
  - `ToSprintId: int`

**Date Range**
- `DateRangeValue`
  - `FromUtc: DateTimeOffset`
  - `ToUtc: DateTimeOffset`

**Mode rule**

Exactly one time mode may be active at a time.
If `Mode = SingleSprint`, only `SingleSprint` may contain a value.
If `Mode = SprintRange`, only `SprintRange` may contain a value.
If `Mode = DateRange`, only `DateRange` may contain a value.

## 2.3 Page-level canonical examples

### Team

- **Category:** Page Filter
- **Type:** Single-select
- **Value structure:**
  - `TeamRef`
    - `TeamId: int`
    - `DisplayName: string`
    - `ProductId: int?`
- **Required or optional:** Optional
- **Dependencies:** Depends on Product when Product is selected
- **Notes:** Team is page-level because it refines page scope rather than defining the primary global business scope.

### Repository

- **Category:** Page Filter
- **Type:** Multi-select
- **Value structure:**
  - `RepositoryFilterValue`
    - `RepositoryIds: IReadOnlyList<int>`
- **Required or optional:** Optional
- **Dependencies:** Depends on Product
- **Notes:** Multi-select is canonical even if a page initially exposes only single-select UI.

### ValidationType

- **Category:** Page Filter
- **Type:** Multi-select
- **Value structure:**
  - `ValidationTypeFilterValue`
    - `ValidationTypes: IReadOnlyList<ValidationType>`
- **Required or optional:** Optional
- **Dependencies:** None
- **Notes:** `ValidationType` should be a closed enum, not a free-form string.

### Pipeline

- **Category:** Page Filter
- **Type:** Multi-select
- **Value structure:**
  - `PipelineFilterValue`
    - `PipelineIds: IReadOnlyList<int>`
- **Required or optional:** Optional
- **Dependencies:** Depends on Product
- **Notes:** Supports future pipeline drill-down without changing the core model.

### PR Status

- **Category:** Page Filter
- **Type:** Multi-select
- **Value structure:**
  - `PrStatusFilterValue`
    - `Statuses: IReadOnlyList<PrStatus>`
- **Required or optional:** Optional
- **Dependencies:** None
- **Notes:** Must use a canonical enum, not page-specific string labels.

---

## 3. Filter state model

The filter system owns one central state object.

```text
FilterState
├── SelectedFilters
├── EffectiveFilters
├── ApplicableFilters
└── InvalidFilters
```

### 3.1 SelectedFilters

**Definition**

SelectedFilters are the exact values chosen by the user.
They represent user intent.

**Properties**

- may contain values that are invalid for the current combination
- may contain values that are not applicable on the current page
- must not be silently rewritten by validation

**Purpose**

SelectedFilters answer: **“What did the user ask for?”**

### 3.2 EffectiveFilters

**Definition**

EffectiveFilters are the validated, applicable filters actually used by the current page and backend request.

**Properties**

- contains only filters that are both applicable and valid
- excludes invalid filters
- excludes not-applicable filters
- may contain page defaults where required by page contract

**Purpose**

EffectiveFilters answer: **“What is actually being used?”**

### 3.3 ApplicableFilters

**Definition**

ApplicableFilters describe which canonical filters the current page supports.

**Properties**

- determined by the page contract, not by user selection
- includes filter-level applicability and, where needed, mode-level applicability
- may state that a filter is supported but only in limited modes

**Purpose**

ApplicableFilters answer: **“What can this page meaningfully use?”**

### 3.4 InvalidFilters

**Definition**

InvalidFilters are selected filters that fail validation relative to other filters or domain membership rules.

**Properties**

- retain the user’s selected value
- include a machine-readable reason code
- include a user-facing explanation
- are never included in EffectiveFilters

**Purpose**

InvalidFilters answer: **“What did the user select that cannot currently be applied?”**

---

## 4. Validation rules

## 4.1 Validation responsibilities

Validation happens in two places:

### Client-side validation

The frontend filter engine validates:

- cross-filter consistency
- applicability for the active page
- domain membership when lookup data is available client-side

Purpose:

- immediate UX feedback
- prevent invalid filters from being sent as effective query inputs

### Backend validation

The backend validates:

- all incoming effective filters
- authorization and domain membership
- stale or tampered URL/query values

Purpose:

- enforce correctness and security
- ensure clients cannot bypass filter rules

## 4.2 Product ↔ Project relationship

### Rule

If both Product and Project are selected, the selected Project must belong to the selected Product.

### Validation outcome

If the relationship is valid:
- both remain effective

If the relationship is invalid:
- both selections remain in SelectedFilters
- the invalid axis is added to InvalidFilters
- the invalid axis is excluded from EffectiveFilters
- the page uses only the remaining valid effective filters

### Canonical invalid reason code

- `ProjectNotInProduct`

## 4.3 Team must belong to Product

### Rule

If Product is selected and Team is selected, the Team must belong to the selected Product.

### Validation outcome

If invalid:
- Team remains in SelectedFilters
- Team is added to InvalidFilters
- Team is excluded from EffectiveFilters
- Product remains effective if valid

### Canonical invalid reason code

- `TeamNotInProduct`

## 4.4 Repository must belong to Product

### Rule

If Product is selected and Repository is selected, every selected Repository must belong to the selected Product.

### Validation outcome

If one or more repositories are invalid:
- all selected repositories remain in SelectedFilters
- invalid repositories are listed in InvalidFilters
- only valid repositories remain in EffectiveFilters
- if no repositories remain valid, Repository is absent from EffectiveFilters

### Canonical invalid reason code

- `RepositoryNotInProduct`

## 4.5 Time applicability rules

### Rule

Time is a Conditional Global Filter.
It is only effective when:

- the current page supports Time, and
- the current page supports the selected time mode

### Validation outcome

If Time is not applicable on the current page:
- Time remains in SelectedFilters
- Time is not added to InvalidFilters
- Time is excluded from EffectiveFilters
- Time is marked not applicable via ApplicableFilters

### Validation outcome for invalid time mode

If the page supports Time but not the selected mode:
- Time remains in SelectedFilters
- Time is added to InvalidFilters with mode-specific reason
- Time is excluded from EffectiveFilters

### Canonical invalid reason codes

- `TimeNotSupportedByPage`
- `TimeModeNotSupportedByPage`
- `InvalidTimeRange`

---

## 5. Filter application model

## 5.1 Selection flow

### Step 1: UI writes SelectedFilters

When the user changes any filter:

- the UI updates SelectedFilters immediately
- no page-specific query logic runs at this stage
- no backend DTO is created at this stage

### Step 2: Page declares ApplicableFilters

The page contract determines:

- which canonical filters are supported
- which time modes are supported
- which filters are required for execution

### Step 3: Validation produces InvalidFilters

The filter engine evaluates:

- cross-filter dependencies
- membership rules
- page applicability
- time-mode compatibility

### Step 4: EffectiveFilters are computed

EffectiveFilters are created from SelectedFilters by applying these rules:

1. filter must be applicable
2. filter must be valid
3. filter must be complete if the filter type requires completeness
4. page-required defaults may be injected only here

## 5.2 Canonical separation of concerns

### What the user selected
- `SelectedFilters`

### What is actually used
- `EffectiveFilters`

### What the page can use
- `ApplicableFilters`

### What is ignored or rejected
- `InvalidFilters` or non-applicable filters

## 5.3 Ignored vs invalid

These must be distinct.

### Ignored
A filter is ignored when:
- it is selected
- it is valid in general
- but it is not applicable on the current page

Ignored filters are:
- not in EffectiveFilters
- not errors
- still remembered

### Invalid
A filter is invalid when:
- it conflicts with another selected filter, or
- it violates domain membership, or
- it is structurally invalid

Invalid filters are:
- not in EffectiveFilters
- explicitly reported in InvalidFilters
- shown to the user with reason

---

## 6. Filter scoping

## 6.1 Global across navigation

The following filters are global across navigation:

- Product
- Project

These must:
- persist when moving between pages
- be represented in shared navigation state
- be eligible for URL persistence

## 6.2 Conditionally global across navigation

The following filters are globally remembered but conditionally applied:

- Time

Time must:
- persist across navigation
- remain selected even when a page ignores it
- become effective again when navigating to a compatible page

## 6.3 Reset per page by default

The following filters reset on page change unless explicitly declared as shareable within a page family:

- Team
- Repository
- ValidationType
- Pipeline
- PR Status

## 6.4 Persistence rules

### URL persistence

Use URL persistence for:

- Product
- Project
- Time
- page filters only when the page explicitly supports deep linking

Rule:
- URL is the canonical shareable state format

### Storage persistence

Use browser storage only for:

- restoring the last user state when no URL state is provided
- non-shareable convenience restoration

Rule:
- storage must never override explicit URL state

### In-memory only

Use in-memory state for:

- transient invalid state before URL update
- temporary UI editing state
- non-shareable page refinements when deep linking is intentionally disabled

---

## 7. Extensibility

New filters must be addable without changing the core engine logic.

## 7.1 Extension model

Each filter is defined by a filter definition object.

A filter definition must provide:

- filter key
- category
- typed value contract
- dependencies
- validation rules
- persistence policy
- backend serialization rules
- page applicability metadata

## 7.2 Core engine rule

The core engine must operate on filter definitions, not page-specific `if` statements.

This means:

- no hardcoded filter handling inside pages
- no duplicated validation logic per page
- no duplicated query mapping logic per page

## 7.3 Adding a new filter

To add a new filter:

1. define a new strongly typed value object
2. register a new filter definition
3. declare page applicability where needed
4. add backend mapping for that filter if a page uses it

No other part of the core system should require structural change.

---

## 8. Data contract

## 8.1 Canonical backend DTO

Filters must be passed to the backend as a single structured DTO.

### C# shape

```csharp
public sealed record FilterRequestDto(
    GlobalFilterDto Global,
    ConditionalGlobalFilterDto ConditionalGlobal,
    PageFilterDto Page);

public sealed record GlobalFilterDto(
    int? ProductId,
    string? ProjectKey);

public sealed record ConditionalGlobalFilterDto(
    TimeFilterDto? Time);

public sealed record PageFilterDto(
    int? TeamId,
    IReadOnlyList<int>? RepositoryIds,
    IReadOnlyList<ValidationType>? ValidationTypes,
    IReadOnlyList<int>? PipelineIds,
    IReadOnlyList<PrStatus>? PrStatuses);

public sealed record TimeFilterDto(
    TimeFilterMode Mode,
    int? SprintId,
    int? FromSprintId,
    int? ToSprintId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);
```

## 8.2 Naming conventions

- single-select values use singular names
  - `ProductId`
  - `ProjectKey`
  - `TeamId`
- multi-select values use plural names
  - `RepositoryIds`
  - `PipelineIds`
  - `PrStatuses`
- range values use paired boundary names
  - `FromSprintId`, `ToSprintId`
  - `FromUtc`, `ToUtc`
- enum values must be closed and versioned in shared contracts
- free-form strings are allowed only where the business identifier is truly textual

## 8.3 Optional filter handling

- optional filters may be omitted or set to `null`
- omitted and `null` have the same semantic meaning: no effective constraint for that filter
- invalid filters must not be serialized into the backend request as effective inputs

## 8.4 Multi-select handling

- multi-select filters are always arrays/lists
- comma-separated strings are not canonical
- empty arrays are treated the same as `null`: no effective constraint
- order is ignored unless the filter contract explicitly defines ordered semantics

## 8.5 Request rule

The backend request must be created from `EffectiveFilters`, never from `SelectedFilters`.

---

## 9. Examples

## Example 1 — Product selected, Team selected, Time not applicable

**Scenario**
- Global Product is selected
- Page Team filter is selected
- a global Time selection exists from a previous page
- current page does not support Time

**SelectedFilters**

```yaml
SelectedFilters:
  Product:
    ProductId: 12
    DisplayName: "Payments"
  Project:
    null
  Time:
    Mode: SingleSprint
    SingleSprint:
      SprintId: 44
      SprintName: "Sprint 44"
      TeamId: 7
  Page:
    Team:
      TeamId: 7
      DisplayName: "Payments API"
      ProductId: 12
```

**ApplicableFilters**

```yaml
ApplicableFilters:
  Product: true
  Project: true
  Time: false
  Team: true
  Repository: false
  ValidationType: false
  Pipeline: false
  PrStatus: false
```

**InvalidFilters**

```yaml
InvalidFilters: []
```

**EffectiveFilters**

```yaml
EffectiveFilters:
  Product:
    ProductId: 12
  Page:
    Team:
      TeamId: 7
```

## Example 2 — Product and Time selected globally, page ignores Time

**Scenario**
- user selected Product globally
- user selected a Sprint Range globally
- destination page supports Product but not Time

**SelectedFilters**

```yaml
SelectedFilters:
  Product:
    ProductId: 3
    DisplayName: "Commerce"
  Project: null
  Time:
    Mode: SprintRange
    SprintRange:
      FromSprintId: 100
      ToSprintId: 104
  Page: {}
```

**ApplicableFilters**

```yaml
ApplicableFilters:
  Product: true
  Project: true
  Time: false
  Team: false
  Repository: false
  ValidationType: false
  Pipeline: false
  PrStatus: false
```

**InvalidFilters**

```yaml
InvalidFilters: []
```

**EffectiveFilters**

```yaml
EffectiveFilters:
  Product:
    ProductId: 3
```

## Example 3 — Invalid Team not in Product

**Scenario**
- Product is selected globally
- Team is selected on the page
- selected Team does not belong to selected Product

**SelectedFilters**

```yaml
SelectedFilters:
  Product:
    ProductId: 5
    DisplayName: "Platform"
  Project: null
  Time: null
  Page:
    Team:
      TeamId: 99
      DisplayName: "Retail Mobile"
      ProductId: 8
```

**ApplicableFilters**

```yaml
ApplicableFilters:
  Product: true
  Project: true
  Time: true
  Team: true
  Repository: false
  ValidationType: false
  Pipeline: false
  PrStatus: false
```

**InvalidFilters**

```yaml
InvalidFilters:
  - Filter: Team
    Code: TeamNotInProduct
    Message: "The selected team does not belong to the selected product."
```

**EffectiveFilters**

```yaml
EffectiveFilters:
  Product:
    ProductId: 5
```

---

## 10. Summary

This model is consistent because it gives every filter a single canonical place in the system:

- Global Filters define stable business scope
- Conditional Global Filters preserve shared context without forcing every page to use it
- Page Filters refine only the current page

It reduces duplication because:

- validation is centralized in one filter engine
- page applicability is declared through page contracts
- backend mapping is created from one effective DTO model
- new filters are added through definitions instead of scattered page logic

It enables future features because:

- new filters can be introduced without redesigning the state object
- pages can support filters incrementally through applicability contracts
- invalid and ignored filters are represented explicitly
- shared deep links become predictable and type-safe

The result is a single filter system that is implementable, extensible, and unambiguous for .NET and Blazor.
