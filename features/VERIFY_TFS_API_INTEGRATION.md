Below is a **single, extensive, copy-pasteable GitHub Copilot feature prompt**.
It is written to force **deterministic, senior-level behavior** and to correct the current “false-positive verification” problem without assumptions.

---

# GitHub Copilot Feature Prompt

## Feature: Comprehensive TFS Capability Verification (Real Data, Step-Based, Non-Blocking)

You are a senior .NET architect and Azure DevOps Server (TFS 2022 on-prem, API 7.0) expert.

You are implementing a **new verification feature** for an existing tool that integrates with **Azure DevOps Server 2022** at:

```
http://tfs.rhmarine.com/tfs/DefaultCollection
```

This feature **replaces shallow connectivity checks** with **real, production-grade data retrieval tests**.

---

## Context & Hard Constraints (do not violate)

* TFS is **on-prem Azure DevOps Server 2022**
* API version: **7.0**
* Single collection: **DefaultCollection** (already in base URL)
* Project name is a **string**, default `"Rhodium"` (may contain spaces)
* Authentication:

  * NTLM (CurrentCredentials)
  * PAT
* Work item hierarchy:

  ```
  Goal
    Objective
      Epic
        Feature
          PBI or Bug
            Task
  ```
* Work items live in **area paths deeper than the project root**
* Verification must:

  * Use the **same code paths** as real data retrieval
  * Never short-circuit on first failure
  * Produce a **full report of what works and what doesn’t**

---

## Core Design Goal

When the user clicks **“Verify TFS Integration”**:

1. A **modal dialog opens immediately**
2. Progress is visible:

   * Current step / total steps
   * Running / Success / Failed state per step
3. Verification continues **even if a step fails**
4. At completion:

   * A **collapsible report** is shown
   * Each step shows:

     * What was tested
     * What data was retrieved (summary)
     * Errors (if any)
     * Impacted functionality

This feature must **prove real functionality**, not just endpoint reachability.

---

## Verification Model (mandatory structure)

### A) Step-based execution

Implement verification as a **sequence of independent steps**:

```text
Step 1 of N – Server reachability
Step 2 of N – Project validation
Step 3 of N – Work item hierarchy retrieval
Step 4 of N – Pull requests
Step 5 of N – Pipelines
...
```

Each step:

* Has a unique `StepId`
* Has a human-readable `Title`
* Reports:

  * `Success`
  * `ObservedBehavior`
  * `Errors` (raw + sanitized)
  * `ImpactedFeatures`

Failure of a step **must not abort the sequence**.

---

### B) Required verification steps (minimum)

#### Step 1 – Server & authentication validation

* Real call:

  ```
  GET _apis/projects?api-version=7.0
  ```
* Confirms:

  * Server reachable
  * Authentication works (NTLM or PAT)
* No mocks, no shortcuts

---

#### Step 2 – Project validation

* Real call:

  ```
  GET _apis/projects/{Project}?api-version=7.0
  ```
* Validate:

  * Project exists
  * Project name is correct
* Store returned:

  * `project.name`
  * `project.id`

---

#### Step 3 – Work item query (hierarchy aware)

* WIQL using **UNDER**:

  ```sql
  SELECT [System.Id]
  FROM WorkItems
  WHERE [System.AreaPath] UNDER '{ConfiguredAreaPath}'
  ```
* Validate:

  * Query executes
  * At least 1 result OR a clear “no data” result
* Do not treat “no items” as a hard failure

---

#### Step 4 – Work item chain retrieval (Goal → Task)

* From WIQL results:

  * Pick **a small sample** (e.g. first 5–10 IDs)
* Fetch via:

  ```
  _apis/wit/workitems?ids=...&fields=...&$expand=relations
  ```
* Validate:

  * Fields exist
  * Parent relationships resolved via `relations`
  * At least one multi-level chain is reconstructed (best-effort)
* Report:

  * How many levels could be resolved
  * Where hierarchy breaks (if it does)

---

#### Step 5 – Pull requests

* Real calls:

  ```
  GET {project}/_apis/git/repositories
  GET {project}/_apis/git/repositories/{repo}/pullrequests
  ```
* Validate:

  * Repositories accessible
  * PRs retrievable
* If no PRs exist:

  * Mark step as **Success (No Data)**, not failure

---

#### Step 6 – Pipelines (build + release)

* Real calls:

  ```
  GET {project}/_apis/build/definitions
  GET {project}/_apis/build/builds
  GET {project}/_apis/release/definitions (if supported)
  ```
* Validate:

  * Pipelines exist
  * Runs retrievable
* Release API failures must be **soft failures** (on-prem variance)

---

#### Step 7 – Optional write checks (explicitly flagged)

Only if user enables write verification:

* Non-destructive PATCH test
* Use a user-supplied work item ID
* Never create or delete data implicitly

---

### C) UI behavior (mandatory)

* Button click opens **modal dialog**
* Modal shows:

  * Spinner
  * Current step name
  * Step counter (e.g. `4 / 9`)
* Each step updates UI state when completed
* On completion:

  * Modal switches to **report view**
  * Steps are **collapsible**
  * Errors are readable and copyable

---

### D) Reporting model

Final report must include:

* Overall success flag
* Timestamp
* Server URL
* Project name
* Per-step results:

  * Status
  * Observed behavior
  * Errors (if any)
  * Impacted functionality

This report must be:

* Serializable
* Storable
* Reusable for debugging and support

---

## Non-negotiable implementation rules

* Use **the same TFS client code paths** as production features
* Do not introduce mock logic
* Do not stop execution on failure
* Do not assume data exists
* Do not hide partial success
* Prefer explicit “No Data Found” over “Success” or “Failure”

---

## Goal of this feature

This verification must answer, conclusively:

> “Which parts of the TFS integration actually work in *this* environment, and which don’t — and why.”

Not:

* “Can I connect”
* “Does the API respond”

---

## Deliverables

* Backend verification pipeline (step-based)
* UI modal + progress reporting
* Final structured verification report
* Clean separation between:

  * Verification orchestration
  * Actual data retrieval logic

---

## UI Integration Requirement (replace existing button)

* In the existing **TFS Config view**, there is currently a button labeled **“Verify TFS API”**.
* Replace the behavior of this existing button with the new comprehensive step-based verification flow described in this feature.
* Do **not** add a second button.
* Keep the existing layout and styling conventions of the TFS Config view unless strictly required to support:

  * opening the modal
  * showing progress and step results
  * showing the final collapsible report

### Backwards compatibility

* Keep the existing public API endpoint / command wiring used by the current “Verify TFS API” button **if it exists**, but change the underlying implementation to run the new verification pipeline.
* If an endpoint currently returns a `TfsVerificationReport`, evolve it in a compatible way:

  * Prefer extending the report model (new properties) over breaking changes
  * If a breaking change is unavoidable, update all callers and the UI in the same PR.

### Button behavior

* On click:

  * Immediately set UI state to “busy”
  * Open the modal dialog immediately
  * Start the verification pipeline
* The button must be disabled while verification is running to prevent concurrent runs.

---

## Updated Deliverables (explicit)

* Replace the existing “Verify TFS API” button functionality in the TFS Config view with this new verification flow.
* Modal dialog integrated into the existing TFS Config view UX.
* The old shallow verification logic must be removed or no longer reachable from the UI.

---


Do not simplify.
Do not guess.
Do not hide failures.
Implement for **debuggability, determinism, and trust**.

---

If you want next:

* I can split this into **backend agent prompt + frontend agent prompt**
* Or produce a **step-by-step task breakdown** Copilot can execute sequentially
