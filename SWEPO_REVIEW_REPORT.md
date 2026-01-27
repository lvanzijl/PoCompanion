# Senior SWE/PO Review Report – main branch

## 1. Blocking Issues
- **Sync-over-async violations in PoTool.Client**  
  - **Why it is a problem:** PROCESS_RULES.md §13.1 forbids sync-over-async in the client to avoid deadlocks in Blazor WebAssembly. There are active violations (`.Result`) in client components.  
  - **What could break:** UI can deadlock under load or when a task faults, and any CI guardrail will fail once enabled.  
  - **Fix (conceptual):** Make affected flows fully async (e.g., await state classification/pipeline metrics), push derived data into async lifecycle methods, and remove `.Result`/blocking calls entirely.
- **Direct HttpClient usage inside UI components**  
  - **Why it is a problem:** UI_RULES.md §3 forbids direct HttpClient usage in pages/components; backend access must go through typed frontend services.  
  - **What could break:** UI state and error handling become inconsistent, and API contract changes silently break components.  
  - **Fix (conceptual):** Introduce/extend typed client services for onboarding/startup/team selection flows and route all component calls through those services.
- **CI guardrail for sync-over-async is disabled**  
  - **Why it is a problem:** PROCESS_RULES.md §13.1 mandates a CI scan; the workflow is disabled, so the requirement is not enforced.  
  - **What could break:** Regressions will reintroduce deadlocks in the client, and compliance will fail review.  
  - **Fix (conceptual):** Re-enable the build workflow (or equivalent) and ensure the script runs on PoTool.Client only.
- **Unit tests contain NotImplementedException**  
  - **Why it is a problem:** Tests fail by definition and violate the Definition of Done.  
  - **What could break:** CI will be red once tests are enabled, and critical validation logic remains unverified.  
  - **Fix (conceptual):** Replace NotImplementedException placeholders with real assertions or remove tests until the behavior is defined.
- **Compact UI wrapper policy is violated in Settings team picker**  
  - **Why it is a problem:** Fluent_UI_compat_rules.md §Wrapper Component Policy requires compact wrappers; raw MudSelect/MudCheckBox usage introduces density drift.  
  - **What could break:** Inconsistent UX density and future theme divergence.  
  - **Fix (conceptual):** Replace raw Mud components with Compact* wrappers or document explicit justification.

## 2. Architectural Concerns
- **Startup configuration mixes infrastructure, persistence, and API endpoints in a single extension**  
  - Large `ConfigurePoToolApi` combines migration logic, diagnostics, endpoints, and middleware. This is hard to reason about and will become brittle as new endpoints are added.
- **Monolithic client components own orchestration and network details**  
  - Onboarding and dialog components directly orchestrate persistence and network calls. This violates the “UI as orchestration only” rule and makes state/behavior less testable.
- **Work item sync pipeline does DB mutation inline**  
  - Sync stage uses DbContext directly for heavy upsert loops. This inhibits reuse, makes concurrency rules harder to enforce, and prevents isolated unit tests.

## 3. State & Data Integrity
- **Watermark uses RetrievedAt instead of TFS ChangedDate**  
  - Sync logic uses local retrieval timestamps for incremental sync, risking missed updates if retrieval order diverges from server change order. A watermark is the last successful change timestamp used to drive incremental sync.  
  - A TODO hints at this but no invariant enforces correctness.
- **Potential duplicate upserts in sync stage**  
  - Work items are processed per DTO without de-duplication; duplicate IDs in the batch can cause multiple updates and nondeterministic last-write wins.
- **Repeated data loads in FlowPanel**  
  - `OnInitializedAsync` and `OnParametersSetAsync` both trigger loads without cancellation or version checks, creating race conditions and stale UI data.

## 4. UX & Interaction Correctness
- **Onboarding wizard mixes setup states without explicit cancellation/rollback**  
  - Partial progress states persist even if the user navigates away or closes the dialog; state visibility is unclear and risks inconsistent setup.
- **No explicit loading affordance during repeated FlowPanel refresh**  
  - `_isLoading` hides the entire view and re-renders; the user sees flicker instead of a contextual in-place load state.
- **Density drift in Settings dialogs**  
  - Raw MudSelect/MudCheckBox create non-compact height/spacing in otherwise compact UI, undermining predictable rhythm.

## 5. Maintainability & Evolution
- **RealTfsClient is a monolith**  
  - One class owns URL building, throttling, multiple resource types, batching, and retries. This violates single-responsibility and will be difficult to evolve safely; it should be split into resource-focused clients.
- **Sync stage contains ad-hoc mapping and persistence logic**  
  - Mapping and DB updates live directly in the stage, preventing reuse and isolatable tests.
- **UI components include networking details**  
  - When APIs evolve, every component becomes a refactor hot spot, raising risk of regression.

## 6. Testing Gaps
- **Unit test placeholders throw NotImplementedException**  
  - Several unit test files are stubs, so foundational validation logic is unverified.
- **No bUnit coverage for onboarding flows**  
  - Complex multi-step UI behavior is untested and likely to regress.
- **No explicit tests for sync watermark correctness**  
  - Incremental sync correctness is high-risk and should be validated with deterministic fixtures.

## 7. Design Improvements (Non-Blocking, ordered)
1. **Refactor WorkItem sync to pre-load existing entities and apply updates in-memory**  
  - Reduces N+1 queries and simplifies batch handling.
2. **Extract onboarding orchestration into a client service**  
  - Keeps UI pure, reduces duplicated error handling, and aligns with UI_RULES.md.
3. **Split RealTfsClient by resource boundary**  
  - Introduce focused collaborators (WorkItemsClient, PullRequestsClient, PipelinesClient) to reduce cognitive load.

## Feature Wishlist (PO, ordered; all items not in TFS)
1. **Cross-team dependency risk heatmap**  
   - Visualize and score dependency chains across teams, highlighting high-risk bottlenecks.
2. **What-if capacity simulator**  
   - Model effort changes, team availability, and scope shifts with immediate forecast impact.
3. **Quality gate trend dashboard**  
   - Track validation rule violations over time and correlate with delivery outcomes.
4. **Goal-to-initiative traceability graph**  
   - Visualize strategic impact and detect orphaned work items.
5. **PR review cycle friction analysis**  
   - Identify long-running review steps and suggest policy changes.

## First-Action Plan (ordered)
1. **Remove sync-over-async usage in PoTool.Client and re-enable the CI guardrail** to enforce PROCESS_RULES §13.1.  
2. **Replace direct HttpClient usage in UI components with typed client services** to align with UI_RULES §3.  
3. **Implement or delete NotImplementedException test stubs** and get a green unit test baseline.  
4. **Define and enforce a proper sync watermark using TFS ChangedDate** to prevent data loss.  
5. **Modularize RealTfsClient and WorkItemSyncStage** for scalable evolution.
