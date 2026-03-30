## Feature: Work Item Tree (Goal → Objective → Epic → Feature → PBI → Task)

### Goal

Provide a hierarchical view of all work items (Goal, Objective, Epic, Feature, PBI, Task) under a user-configured Area Path, with local caching, inline search filtering, highlight of matched text, and a globally accessible pull-and-cache command.
This feature must explicitly reference and comply with:

* `docs/ux-principles.md`
* `docs/rules/architecture-rules.md`

---

## Functional Description

### Configuration Dialog

The configuration dialog defines the scope and access parameters for work item retrieval.

It contains:

1. **Area Path Selector**
   Field for specifying the base Area Path to collect all work items in the hierarchy (Goal, Objective, Epic, Feature, PBI, Task).

2. **Personal Access Token (PAT)**
   Secure input field.
   PAT is stored encrypted and never shown again after entry.

3. **General Layout Compliance**
   All elements follow `docs/ux-principles.md`: minimal, clear, consistent, non-technical in appearance.

---

## Global Pull & Cache Command

### Access Location

A **top-level menu bar** is always visible across the entire tool.
Within this menu bar is a **directly accessible button** for retrieving and caching work items.

### Button Requirements

* Always visible, independent of which view is active.
* Uses a **clear, recognizable icon** (e.g., refresh/download/cloud-sync style).
* Hover reveals: “Pull & Cache Work Items”.
* Clicking triggers the full data retrieval and caching cycle.

### Behavior

* On click, the tool retrieves all work items under the configured Area Path following the 6-level hierarchy (Goal → Objective → Epic → Feature → PBI → Task).
* Data is stored locally in SQLite, replacing previous cache atomically.
* Any view relying on cached data re-renders after completion.
* All communication paths, caching strategies, and component boundaries must follow `docs/rules/architecture-rules.md`.

---

## Main View: Hierarchical Work Item Tree

### Structure

Six-level hierarchy:

* **Goal**
  * **Objective**
    * **Epic**
      * **Feature**
        * **PBI**
          * **Task**

### Search Filter

A simple text filter above the tree with the following rules:

* Searches only within work item titles.
* The tree collapses to show only the branches where the search text occurs at any level.
* Parents stay visible when descendants match.
* All other branches are hidden.

### Highlighting

Inside any displayed title, the substring matching the filter is **visually highlighted**.
Highlighting must remain subtle and clean, according to the style rules in `docs/ux-principles.md`.

### Interaction

* Selecting a work item opens its detail panel on the right.
* No full page transitions; filtering state and expanded nodes persist.
* Uses the unified overview→detail pattern.

---

## Local Caching

### Retrieval

The pull process:

1. Retrieve all relevant work items via Azure DevOps REST API.
2. Persist in local SQLite according to the caching rules in `docs/rules/architecture-rules.md`.
3. Only necessary data for rendering and filtering is stored.
4. Cache is replaced atomically.

### Use

Tree loads exclusively from the local cache for speed and offline exploration.

---

## Mandatory References

This feature must always be implemented in compliance with:

* **`docs/ux-principles.md`** — defines layout, clarity, consistency, interaction patterns, and filtering behavior.
* **`docs/rules/architecture-rules.md`** — defines caching, API boundaries, component separation, and data integrity requirements.

All further features related to work item visualization or configuration must reference and align with these same documents.
