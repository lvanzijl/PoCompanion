# Feature: Product Owner, Products, Teams, and Product Backlogs

---

## 1. Purpose

This feature defines the **structural foundation** of the application.  
It introduces how **Product Owners**, **Products**, and **Teams** are represented, how **product backlogs** are derived and classified, and how Product Owners manage these elements.

This feature is intentionally **structural and functional**.  
It does **not** define:
- which views exist,
- when they are shown,
- or how users navigate between them.

The goal is to enable current and future Product Owner workflows without constraining them prematurely.

---

## 2. Core domain concepts (functional)

### 2.1 Product Owner

A **Product Owner** represents a single user of the application.

Functional rules:
- The Product Owner *is* the user.
- There is no distinction between “user” and “profile”.
- Multiple Product Owners exist so they can view each other’s Products and Teams.
- The Product Owner exists primarily to group and order Products.

Products are the primary focus of the domain, not the Product Owner entity itself.

---

### 2.2 Product

A **Product** represents a single Scrum product and is the primary concept in the system.

Functional rules:
- A Product has **exactly one Product Backlog**.
- A Product is defined by:
  - a name
  - a **Backlog Root Work Item** (required - defines the product backlog)
  - an explicit **order** relative to other Products of the same Product Owner
  - an optional picture

Backlog rules:
- The Product Backlog is defined by the **Backlog Root Work Item**.
- The Backlog Root Work Item:
  - serves as the root entry point of the backlog
  - can be any work item type (Epic, Feature, etc.)
  - is **required** for a product to be valid
  - defines the scope of the product backlog hierarchically

Lifecycle rules:
- Products require a backlog root work item to be created.
- Products may temporarily exist without linked teams.
- This supports incomplete or evolving organizational setups.

Ordering:
- Products are explicitly ordered by the Product Owner.
- Ordering expresses importance or activity, not workflow state.

Ownership:
- Each Product has exactly **one primary Product Owner** (“cradle to grave”).
- Ownership reflects responsibility, not authorization.
- The design must allow future extension to shared or delegated ownership without rewriting core logic.

---

### 2.3 Team

A **Team** represents a delivery team and is a secondary concept.

Functional rules:
- A Team is defined by:
  - a name
  - a **Team Area Path**
  - an optional picture
- Teams exist to explain how work within a Product Backlog is divided.
- A Team can:
  - work on multiple Products
  - exist temporarily without being linked to any Product (during setup or organizational uncertainty)

Lifecycle:
- Teams can be **archived**.
- Archived Teams:
  - remain available for historical classification
  - are hidden from selection controls by default
  - do not break existing Products or backlogs

---

### 2.4 Product–Team relationship

Products and Teams are linked to express delivery responsibility.

Functional rules:
- A Product can have zero or more Teams.
- A Team can be linked to zero or more Products.
- Linking a Team to a Product:
  - does not move work
  - does not change data
  - only affects classification and visualization

---

### 2.5 Assignment and classification semantics

Assignment is **derived**, never stored.

Rules:
- A work item is considered **assigned to a Team** if its Area Path falls under that Team’s Area Path.
- If multiple Teams match, the most specific Area Path wins.
- If no Team matches, the work item is considered **Unassigned**.

Unassigned work:
- Remains fully visible.
- Is treated as **explicit backlog debt**.
- Must be clearly distinguishable from assigned work.

The system is **read-only** with respect to assignment for now, but must not prevent future write actions.

---

### 2.6 Area Path flexibility

There is no enforced hierarchy requirement between:
- Team Area Paths
- Backlog Root Work Item location

Rules:
- These elements may be structurally unrelated.
- Mismatches result in **warnings**, never errors.
- The system must remain usable even in imperfect or inconsistent TFS setups.
- Mismatches result in **warnings**, never errors.
- The system must remain usable even in imperfect or inconsistent TFS setups.

---

### 2.7 Pictures (personalization)

Pictures are purely presentational.

Functional rules:
- Product Owners, Products, and Teams may each have a square picture.
- Pictures are optional.
- The application provides built-in picture sets per entity type.
- The number of built-in pictures is **not fixed** and may change over time.
- Built-in picture sets may be replaced or extended in future versions.
- Users may upload a custom picture instead.

Pictures must never affect logic, filtering, or behavior.

---

## 3. Product backlog behavior (functional)

For a selected Product, the Product Backlog shows:
- All work items hierarchically under the Backlog Root Work Item.
- Work items that match a linked Team Area Path are shown as **assigned to that Team**.
- Work items that do not match any linked Team Area Path are shown as **Unassigned backlog debt**.

Classification is informational only.

---

## 4. Configuration and management behavior

### 4.1 Creating a Product Owner

When creating a Product Owner, the user must be able to:
- Create Products
- Create Teams
- Assign Teams to Products
- Create Teams inline while creating or editing a Product

### 4.2 Editing an existing Product Owner

The user must be able to:
- Add, edit, reorder, and archive Products
- Add, edit, and archive Teams
- Link and unlink Teams to Products
- Create Teams inline while editing a Product
- Configure Product Backlog Roots (required work item IDs)
- See warnings for inconsistent configurations without being blocked

---

## 5. Selection and interaction rules (UI-agnostic)

Whenever the user must select **one or more items**:
- A single, consistent **combobox-style control** is used.
- Each item has a checkbox.
- The text field is filterable.
- The same control supports both single- and multi-select where applicable.

---

## 6. Copilot implementation instructions

### 6.1 Conceptual-to-technical mapping
- Treat this document as the **source of truth**.
- Copilot may choose any suitable persistence model (relational, document, hybrid).
- Do not infer domain rules from storage structure.

### 6.2 Ownership and future-proofing
- Products have exactly one primary Product Owner today.
- Do not hard-code assumptions that ownership can never change.
- Do not conflate ownership with authorization or visibility.
- Principal Product Owners (future):
  - never own Products directly
  - only aggregate Products owned by other Product Owners

### 6.3 Product ordering
- Product order is explicit and user-defined.
- Ordering must persist.
- Do not derive ordering from name or creation date.

### 6.4 Products and Backlog Roots
- All products require a backlog root work item.
- Views must validate that the backlog root exists and is accessible.
- Missing or inaccessible backlog roots should be reported as configuration errors.

### 6.5 Cache invalidation
- Backlog data may be cached for performance.
- Changes to work item hierarchies require cache invalidation and reload.
- Do not assume work item structures are immutable.

### 6.6 Read-only today, writable tomorrow
- Assignment and classification are read-only for now.
- The design must allow future write actions, such as:
  - proposing reassignment
  - initiating controlled changes

### 6.7 Views intentionally undefined
- This feature does not define views or navigation.
- Domain logic must remain view-agnostic.

---

## 7. Explicit non-goals (for this feature)

- Defining concrete screens or navigation
- Authorization and permissions
- Workflow states
- Suppressing or configuring warnings
- Product merge/split behavior (future consideration)

---

## 8. Acceptance criteria (structural)

- Products are the primary organizing concept.
- Each Product has exactly one Product Backlog.
- Teams subdivide work but do not define visibility.
- Unassigned work is always visible and explicit.
- Products and Teams can be configured incrementally.
- The model supports future expansion without structural rewrites.
