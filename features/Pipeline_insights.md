You are implementing a new read-only view called **Pipeline Insights**.

Goal:
Provide exploratory insights into build/release pipelines to help identify instability, bottlenecks, and patterns. This is a V1 view: descriptive, not prescriptive. No scoring, no recommendations, no automation.

Scope constraints:
- Do NOT restructure existing UI or navigation.
- Add this as a standalone view, consistent with existing views.
- Follow all existing GitHub instructions, architecture rules, UX rules, caching rules, and TFS integration rules.
- Reuse existing TFS client abstractions and caching strategies.
- Do NOT invent new backend architecture patterns.
- Do NOT add write operations to TFS.

---

## 1. Data source

Use TFS/Azure DevOps Server pipeline APIs (build and/or release pipelines depending on availability in the current TFS version).

Retrieve, at minimum:
- Pipeline definition id + name
- Pipeline type (build / release, if distinguishable)
- Pipeline runs:
  - run id
  - start time
  - finish time
  - duration
  - result (succeeded, failed, partiallySucceeded, canceled)
  - triggered by (manual / CI / scheduled / user, if available)
- Optional if easily available:
  - branch
  - reason
  - requestedFor / queuedBy

Do NOT assume all fields exist; handle partial data gracefully.

---

## 2. Caching rules

- Use the same caching model as the Work Item Explorer and PR Insights views.
- Pipeline data must be explicitly pulled via a "Pull & Cache Pipelines" action.
- Cached data is read-only and timestamped.
- The view must clearly show:
  - last refresh time
  - whether data is cached or live
- No automatic background refresh.

---

## 3. View layout (high level)

The Pipeline Insights view consists of three vertical sections:

### 3.1 Pipeline Overview (top)
- Table or list of pipelines with:
  - pipeline name
  - total runs (cached scope)
  - failure rate (%)
  - average duration
  - last run result
- Sorting enabled on all numeric columns.
- No filtering beyond basic text search on pipeline name.

### 3.2 Pipeline Run History (middle)
- When a pipeline is selected:
  - show a paged list of recent runs
  - include:
    - result
    - duration
    - start time
    - trigger
- Clicking a run opens the pipeline run in TFS web UI (external link).

### 3.3 Exploratory Signals (bottom)
Lightweight, descriptive signals only, for example:
- Pipelines with the highest failure rate
- Pipelines with the longest average duration
- Pipelines with high variance in duration
- Pipelines with frequent consecutive failures

These are **lists**, not alerts.
No thresholds are enforced; numbers are shown, interpretation is left to the user.

---

## 4. UX rules

- This view is **exploratory**, not operational.
- No red/green health scores.
- No “recommendations”.
- No blocking warnings.
- Visual emphasis must be subtle and consistent with other insight views.
- Empty states must explain what data is missing and how to pull it.

---

## 5. Error handling

- Partial API failures must not break the view.
- Show missing data explicitly (e.g. “not available”).
- If pipeline APIs are unavailable for this TFS version:
  - show a clear message in the view
  - include this in the TFS Integration Verify report.

---

## 6. Integration with existing tooling

- Add pipeline-related checks to the existing TFS Integration Verify feature:
  - list which pipeline endpoints are accessible
  - list which data points cannot be retrieved
- Do NOT create a new verification mechanism.

---

## 7. Non-goals (explicitly out of scope)

- No prediction
- No pipeline optimization advice
- No SLA or DORA metrics
- No cross-linking to work items
- No configuration per pipeline
- No export functionality (for now)

---

## 8. Deliverables

- New view implementation
- Reused caching and config patterns
- Clear empty and error states
- Updated TFS Integration Verify reporting for pipelines
- No breaking changes to existing views

Start by identifying the existing view pattern most similar to this (e.g. PR Insights), reuse its structure, and adapt it for pipeline data.
