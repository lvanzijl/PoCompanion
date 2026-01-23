# UI Migration Plan — PO Companion

**Version:** 1.0  
**Status:** ACTIVE  
**Last Updated:** 2026-01-23  
**Document Type:** Living Artifact (Mandatory Continuous Maintenance)

---

## Document Purpose

This document is the **single source of truth** for transforming the PO Companion UI from a **page-based toolbox with sidebar navigation** into an **intent-driven, context-shaped workspace UX**.

This plan is:
- Explicitly phased
- Designed to avoid big-bang changes
- Structured to keep the application usable between phases
- Committed to removing obsolete functionality once replacements are complete

**Authority:** All requirements in this document are binding. Any deviation requires explicit justification and amendment to this plan.

---

## Table of Contents

1. [Core Principles](#1-core-principles)
2. [Target Navigation Model](#2-target-navigation-model)
3. [Context Contract](#3-context-contract)
4. [Workspace Specifications](#4-workspace-specifications)
5. [Current-to-Target Mapping](#5-current-to-target-mapping)
6. [Required User Flows](#6-required-user-flows)
7. [Migration Phases](#7-migration-phases)
8. [Completion Guards](#8-completion-guards)
9. [Continuous Maintenance Log](#9-continuous-maintenance-log)

---

## 1. Core Principles

These principles are **non-negotiable** and apply throughout the entire migration.

### 1.1 Profile Selection is Mandatory

- A user MUST select a profile before accessing the application
- Without a selected profile, no other screens are accessible
- Profile gating is already implemented at `/profiles` (ProfilesHome.razor)
- **Current Status:** ✅ Implemented

### 1.2 Onboarding is Fullscreen and Blocking

- Onboarding is shown ONLY when essential settings are missing
- Onboarding completion ALWAYS returns the user to profile selection
- Onboarding is NOT a session start
- **Current Status:** ✅ Implemented (`/onboarding` → Onboarding.razor)

### 1.3 Meta Navigation Only in Header

The header MAY contain:
- Settings (currently: SettingsModal)
- Profile management (currently: ProfileSelector)
- Return to Landing (to be added)
- Keyboard shortcuts (currently: KeyboardShortcutsDialog)
- Help/Getting Started (currently: OnboardingWizard)

These are **meta actions only** — not primary navigation.

### 1.4 Sidebar Navigation is Removed

- The final architecture MUST NOT contain a sidebar or feature-based navigation menu
- During migration, the sidebar (NavMenu.razor) MAY exist temporarily
- The sidebar MUST be explicitly phased out
- **Current Status:** 🔴 Active sidebar with 14 navigation items

### 1.5 Anti-Regress Rule — No New Pages

During migration:
- **No new standalone pages MAY be introduced**
- All new functionality MUST be implemented as:
  - Workspace extensions
  - New workspace modes or states
  - Context-driven variants of existing workspaces
- Temporary or transitional pages are NOT allowed

---

## 2. Target Navigation Model

### 2.1 Intent-Driven Entry

After profile selection, users MUST land on a **Landing page** that exposes a limited set of **intent-based start points**.

The four intents are:

| Intent | Dutch Name | Purpose | Entry Question |
|--------|-----------|---------|----------------|
| **Overzien** | Overzien | Build context and choose scope | "What am I looking at?" |
| **Begrijpen** | Begrijpen | Analyze, diagnose, reflect | "Why is this happening?" |
| **Plannen** | Plannen | Structure and look ahead | "What should happen next?" |
| **Delen** | Delen | Communicate and export | "Who needs to know?" |

Selecting an intent establishes **navigation context** for all subsequent views.

### 2.2 Workspaces (Not Pages)

Screens in the application are **workspaces**, not fixed pages.

- A workspace MAY be entered from multiple paths
- A workspace MUST adapt its defaults and emphasis based on context
- There MUST NOT be duplicate "simple" or "advanced" variants of the same workspace

### 2.3 Primary Workspaces

| Workspace | Purpose | Primary Entry Intents |
|-----------|---------|----------------------|
| **Product Workspace** | Summarize product state, drill-down entry points | Overzien |
| **Team Workspace** | Team-scope summary, sprint/time navigation | Overzien, Begrijpen |
| **Analysis Workspace** | Diagnostics, evidence views, comparisons | Begrijpen |
| **Planning Workspace** | Structuring, validation, sequencing | Plannen |
| **Communication Workspace** | Templates, snapshots, exports | Delen |

---

## 3. Context Contract

### 3.1 Context Structure

The Context Contract is a **first-class artifact** that shapes all workspace behavior.

```csharp
public record NavigationContext
{
    // REQUIRED: Intent that triggered this navigation
    public required Intent Intent { get; init; }
    
    // REQUIRED: Current scope of work
    public required Scope Scope { get; init; }
    
    // OPTIONAL: What triggered entry into current workspace
    public Trigger? Trigger { get; init; }
    
    // OPTIONAL: Time perspective for data
    public TimeHorizon TimeHorizon { get; init; } = TimeHorizon.Current;
    
    // OPTIONAL: Source context for back navigation
    public NavigationContext? Parent { get; init; }
}

public enum Intent
{
    Overzien,   // Build context and choose scope
    Begrijpen,  // Analyze, diagnose, reflect
    Plannen,    // Structure and look ahead
    Delen       // Communicate and export
}

public record Scope
{
    public ScopeLevel Level { get; init; }
    public int? ProfileId { get; init; }    // Always required after profile selection
    public int? ProductId { get; init; }    // Required for Product/Team scope
    public int? TeamId { get; init; }       // Required for Team scope
}

public enum ScopeLevel
{
    Portfolio,  // All products under profile
    Product,    // Single product
    Team        // Single team
}

public record Trigger
{
    public TriggerType Type { get; init; }
    public string? SourceId { get; init; }     // Identifier of triggering element
    public string? SourceLabel { get; init; }  // Human-readable source description
}

public enum TriggerType
{
    Choice,     // User explicitly selected this path
    Deviation,  // System detected an issue requiring attention
    Request     // External request (e.g., from communication)
}

public enum TimeHorizon
{
    Current,    // Now, active sprint, current state
    Historical, // Past sprints, trends, history
    Future      // Forecasts, plans, projections
}
```

### 3.2 Context Rules

1. **Context is Preserved Across Navigation**
   - Navigating within a workspace preserves context
   - Navigating to a different workspace may transform context
   - Context is never silently discarded

2. **Context Shapes Defaults and Highlights**
   - Entry with `Intent.Begrijpen` + `Trigger.Deviation` highlights diagnostic views
   - Entry with `Intent.Plannen` + `TimeHorizon.Future` defaults to forecast mode
   - Entry with `Intent.Delen` pre-selects sharing templates

3. **Context is Reversible**
   - Back/forward navigation MUST behave logically
   - Parent context enables proper back navigation
   - Context stack supports multi-step undo

4. **Context Stability**
   - The Context Contract MUST be stable throughout the migration
   - All workspaces MUST reference this single contract
   - Per-workspace reinterpretation of context is FORBIDDEN

### 3.3 Context Service Interface

```csharp
public interface INavigationContextService
{
    // Get current active context
    NavigationContext Current { get; }
    
    // Navigate with new context
    Task NavigateWithContextAsync(string route, NavigationContext context);
    
    // Navigate back to parent context
    Task NavigateBackAsync();
    
    // Update current context without navigation
    void UpdateContext(Action<NavigationContext> update);
    
    // Check if context allows specific action
    bool CanPerform(string action);
}
```

---

## 4. Workspace Specifications

### 4.1 Product Workspace

**Purpose:** Summarize product state with drill-down entry points into Analysis/Planning/Sharing.

**Replaces Current Pages:**
- ProductHome.razor (`/`, `/product-home`)

**Capabilities:**
- Product overview with health indicators
- Work item hierarchy summary
- Entry points to detailed workspaces
- Quick status at a glance
- Drill-down to specific areas

**Entry Modes:**

| Entry Mode | From Intent | Default View | Highlighted Actions |
|------------|-------------|--------------|---------------------|
| Overview | Overzien + Choice | Dashboard cards | Explore details |
| Status Check | Overzien + Deviation | Issue summary | Investigate issues |
| Quick Share | Delen + Choice | Summary view | Export/Share options |

**Context Contract Values (Default):**
- `Intent`: Overzien
- `Scope.Level`: Product
- `Trigger.Type`: Choice
- `TimeHorizon`: Current

### 4.2 Team Workspace

**Purpose:** Team-scope summary with sprint/time navigation and drill-down into Analysis/Sharing.

**Replaces Current Pages:**
- VelocityDashboard.razor (`/velocity`) - team velocity view
- StateTimeline.razor (`/state-timeline`) - team timeline view

**Capabilities:**
- Team velocity trends
- Sprint navigation
- Work distribution visualization
- Team-specific health indicators
- State transitions over time

**Entry Modes:**

| Entry Mode | From Intent | Default View | Highlighted Actions |
|------------|-------------|--------------|---------------------|
| Team Overview | Overzien + Choice | Current sprint summary | Browse sprints |
| Performance Analysis | Begrijpen + Choice | Velocity trends | Investigate deviations |
| Historical Review | Begrijpen + Historical | Timeline view | Compare periods |

**Context Contract Values (Default):**
- `Intent`: Overzien
- `Scope.Level`: Team
- `Trigger.Type`: Choice
- `TimeHorizon`: Current

### 4.3 Analysis Workspace

**Purpose:** Diagnostics, evidence views, comparisons, and "impact to planning" routing.

**Replaces Current Pages:**
- BacklogHealth.razor (`/backlog-health`)
- EffortDistribution.razor (`/effort-distribution`)
- PRInsight.razor (`/pr-insights`)
- PipelineInsights.razor (`/pipeline-insights`)
- DependencyGraph.razor (`/dependency-graph`)
- EpicForecast.razor (`/epic-forecast`)
- StateTimeline.razor (`/state-timeline`) - analysis mode

**Capabilities:**
- Backlog health diagnostics
- Effort distribution analysis
- Pull request flow analysis
- Pipeline performance analysis
- Dependency visualization
- Epic forecasting
- State transition analysis
- Cross-scope comparisons
- Trend identification
- Root cause investigation

**Entry Modes:**

| Entry Mode | From Intent | Default View | Highlighted Actions |
|------------|-------------|--------------|---------------------|
| Health Diagnosis | Begrijpen + Deviation | Health issues list | Fix recommendations |
| Effort Analysis | Begrijpen + Choice | Distribution charts | Balance adjustments |
| Flow Analysis | Begrijpen + Choice | PR/Pipeline metrics | Bottleneck identification |
| Forecast Review | Begrijpen + Future | Epic projections | Plan adjustments |
| Dependency Check | Begrijpen + Choice | Dependency graph | Risk identification |

**Context Contract Values (Default):**
- `Intent`: Begrijpen
- `Scope.Level`: Product (or Team)
- `Trigger.Type`: Choice
- `TimeHorizon`: Current

**Analysis Modes:**
The workspace supports multiple analysis modes, switchable via tabs or context:

| Mode | Focus | Source Pages |
|------|-------|--------------|
| Health | Backlog validation issues | BacklogHealth.razor |
| Effort | Work distribution | EffortDistribution.razor |
| Flow | PR and pipeline metrics | PRInsight.razor, PipelineInsights.razor |
| Dependencies | Work item relationships | DependencyGraph.razor |
| Forecast | Projections and estimates | EpicForecast.razor |
| Timeline | State changes over time | StateTimeline.razor |

### 4.4 Planning Workspace

**Purpose:** Structuring, validation signals, and "explain conflicts" routing to Analysis.

**Replaces Current Pages:**
- ReleasePlanning.razor (`/release-planning`)

**Capabilities:**
- Release planning board
- Epic sequencing and ordering
- Milestone management
- Iteration planning
- Validation and conflict detection
- Capacity planning integration
- Export to stakeholders

**Entry Modes:**

| Entry Mode | From Intent | Default View | Highlighted Actions |
|------------|-------------|--------------|---------------------|
| Plan Construction | Plannen + Choice | Planning board | Add/order items |
| Plan Validation | Plannen + Deviation | Validation issues | Resolve conflicts |
| Plan Review | Overzien + Future | Overview mode | Assess feasibility |

**Context Contract Values (Default):**
- `Intent`: Plannen
- `Scope.Level`: Product
- `Trigger.Type`: Choice
- `TimeHorizon`: Future

### 4.5 Communication Workspace

**Purpose:** Templates, snapshot generation, exports from current context.

**Replaces Current Pages:**
- Export functionality from various pages
- Report generation (currently distributed)

**Capabilities:**
- Status update generation
- Stakeholder reports
- Snapshot exports (PDF, PowerPoint)
- Share current analysis context
- Template-based communication
- Historical report access

**Entry Modes:**

| Entry Mode | From Intent | Default View | Highlighted Actions |
|------------|-------------|--------------|---------------------|
| Status Update | Delen + Choice | Template selection | Generate update |
| Share Analysis | Delen + Request | Current context export | Share snapshot |
| Report Generation | Delen + Historical | Report templates | Build report |

**Context Contract Values (Default):**
- `Intent`: Delen
- `Scope.Level`: Product
- `Trigger.Type`: Choice
- `TimeHorizon`: Current

---

## 5. Current-to-Target Mapping

### 5.1 Navigation/Entry Mapping

For each current sidebar entry point, this table specifies the target mapping.

| Current Sidebar Item | Current Route | Target Intent | Target Workspace | Default Context |
|---------------------|---------------|---------------|------------------|-----------------|
| Dashboard | `/` | Overzien | Product Workspace | Scope: Product, Time: Current |
| Profiles | `/profiles` | N/A (meta) | Profile Selection (header) | N/A |
| Work Items | `/workitems` | Overzien | Product Workspace | Scope: Product, Time: Current |
| Backlog Health | `/backlog-health` | Begrijpen | Analysis Workspace (Health mode) | Scope: Product, Time: Current |
| Effort Distribution | `/effort-distribution` | Begrijpen | Analysis Workspace (Effort mode) | Scope: Product, Time: Current |
| PR Insights | `/pr-insights` | Begrijpen | Analysis Workspace (Flow mode) | Scope: Product, Time: Current |
| Pipeline Insights | `/pipeline-insights` | Begrijpen | Analysis Workspace (Flow mode) | Scope: Product, Time: Current |
| State Timeline | `/state-timeline` | Begrijpen | Analysis Workspace (Timeline mode) | Scope: varies, Time: Historical |
| Epic Forecast | `/epic-forecast` | Begrijpen | Analysis Workspace (Forecast mode) | Scope: Product, Time: Future |
| Release Planning | `/release-planning` | Plannen | Planning Workspace | Scope: Product, Time: Future |
| Velocity | `/velocity` | Begrijpen | Team Workspace | Scope: Team, Time: Historical |
| Dependencies | `/dependency-graph` | Begrijpen | Analysis Workspace (Dependencies mode) | Scope: Product, Time: Current |
| TFS Config | `/tfsconfig` | N/A (meta) | Settings (header) | N/A |
| Work Item States | `/settings/workitem-states` | N/A (meta) | Settings (header) | N/A |

### 5.2 Page-to-Workspace Mapping Table

| Current Page / Route | Primary Purpose | Target Intent | Target Workspace | Entry Mode | Default Scope | Default Time | Replaced-by Phase | Decommission Phase |
|---------------------|-----------------|---------------|------------------|------------|---------------|--------------|-------------------|-------------------|
| ProductHome.razor (`/`, `/product-home`) | observe | Overzien | Product | Choice | Product | Current | Phase 4 | Phase 7 |
| ProfilesHome.razor (`/profiles`) | configure | N/A | Profile Selection | N/A | N/A | N/A | Phase 2 | Phase 7 |
| Onboarding.razor (`/onboarding`) | configure | N/A | Onboarding (blocking) | N/A | N/A | N/A | Phase 2 | N/A (keep) |
| BacklogHealth.razor (`/backlog-health`) | analyze | Begrijpen | Analysis (Health) | Choice | Product | Current | Phase 5 | Phase 7 |
| EffortDistribution.razor (`/effort-distribution`) | analyze | Begrijpen | Analysis (Effort) | Choice | Product | Current | Phase 5 | Phase 7 |
| VelocityDashboard.razor (`/velocity`) | analyze | Begrijpen | Team | Choice | Team | Historical | Phase 5 | Phase 7 |
| StateTimeline.razor (`/state-timeline`) | analyze | Begrijpen | Analysis (Timeline) | Choice | varies | Historical | Phase 5 | Phase 7 |
| EpicForecast.razor (`/epic-forecast`) | analyze | Begrijpen | Analysis (Forecast) | Choice | Product | Future | Phase 5 | Phase 7 |
| DependencyGraph.razor (`/dependency-graph`) | analyze | Begrijpen | Analysis (Dependencies) | Choice | Product | Current | Phase 5 | Phase 7 |
| PRInsight.razor (`/pr-insights`) | analyze | Begrijpen | Analysis (Flow) | Choice | Product | Current | Phase 5 | Phase 7 |
| PipelineInsights.razor (`/pipeline-insights`) | analyze | Begrijpen | Analysis (Flow) | Choice | Product | Current | Phase 5 | Phase 7 |
| ReleasePlanning.razor (`/release-planning`) | plan | Plannen | Planning | Choice | Product | Future | Phase 6 | Phase 7 |
| TfsConfig.razor (`/tfsconfig`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| WorkItemStates.razor (`/settings/workitem-states`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| ManageProducts.razor (`/settings/products`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| ManageTeams.razor (`/settings/teams`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| ManageProductOwner.razor (`/settings/productowner/{id}`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| EditProductOwner.razor (`/settings/productowner/edit`) | configure | N/A | Settings | N/A | N/A | N/A | Phase 2 | Phase 7 |
| Help.razor (`/help`) | support | N/A | Help (header action) | N/A | N/A | N/A | Phase 2 | Phase 7 |
| NotFound.razor (`/not-found`) | error | N/A | Error handling | N/A | N/A | N/A | Phase 7 | N/A (keep) |

### 5.3 Backend Endpoint/Handler Mapping

| Controller | Primary Endpoints | Target Workspace(s) | Status | Legacy Phase | Delete Phase |
|------------|------------------|---------------------|--------|--------------|--------------|
| **ProfilesController** | GET/POST/PUT/DELETE profiles, POST active | Profile Selection, Settings | Remains | N/A | N/A |
| **SettingsController** | GET/PUT settings, state-classifications | Settings | Remains | N/A | N/A |
| **StartupController** | GET readiness, tfs-teams | Onboarding, Startup | Remains | N/A | N/A |
| **ProductsController** | CRUD products, team assignments, repositories | Product Workspace, Settings | Remains | N/A | N/A |
| **TeamsController** | CRUD teams | Team Workspace, Settings | Remains | N/A | N/A |
| **WorkItemsController** | GET workitems, validated, goals, area-paths, revisions, dependency-graph, state-timeline | Product, Team, Analysis | Remains | N/A | N/A |
| **FilteringController** | POST validation filters, goal filters | Analysis | Refactor | Phase 5 | Phase 8 |
| **MetricsController** | GET sprint, velocity, backlog-health, effort-distribution, capacity, epic-forecast | Team, Analysis | Refactor | Phase 5 | Phase 8 |
| **HealthCalculationController** | POST calculate-score | Analysis (Health mode) | Merge into MetricsController | Phase 5 | Phase 6 |
| **PipelinesController** | GET pipelines, runs, metrics, definitions | Analysis (Flow mode) | Remains | N/A | N/A |
| **PullRequestsController** | GET PRs, metrics, filter, iterations, comments, review-bottleneck | Analysis (Flow mode) | Remains | N/A | N/A |
| **ReleasePlanningController** | Board, lanes, placements, milestones, iterations, validation, export | Planning | Remains | N/A | N/A |

### 5.4 Data/Computation Ownership

#### Health Score Computation

**Current State:** Duplicated between:
- `BacklogHealthCalculationService.cs` (client)
- `HealthCalculationController.cs` (API)
- `MetricsController.cs` → backlog-health endpoint

**Target State:**
- Single source: `MetricsController.cs` → unified health endpoint
- Client-side service becomes thin wrapper
- Remove `HealthCalculationController.cs` in Phase 6

**Compatibility Strategy:**
- Phase 5: Add unified endpoint alongside existing
- Phase 6: Migrate all clients to unified endpoint
- Phase 7: Remove legacy endpoints

#### Velocity/Sprint Metrics

**Current State:** Computed in:
- `MetricsController.cs` → sprint, velocity endpoints

**Target State:**
- No change to backend
- Team Workspace consumes existing endpoints
- Add context-aware filtering parameters

#### Effort Distribution

**Current State:** Computed in:
- `MetricsController.cs` → effort-distribution, effort-imbalance, effort-concentration-risk

**Target State:**
- No change to backend
- Analysis Workspace (Effort mode) consumes existing endpoints
- Consider consolidation into single endpoint with mode parameter (Phase 6)

---

## 6. Required User Flows

### 6.1 Overzien Flows

#### Flow Template: Scope Selection → Workspace → Deepen → Plan/Share

```
[Landing (Overzien)]
    ↓ select product
[Product Workspace]
    ↓ drill into health issue
[Analysis Workspace (Health mode)]
    ↓ (optional) create plan to address
[Planning Workspace]
    ↓ (optional) share plan
[Communication Workspace]
```

**Context Transformation:**
1. Landing: `Intent=Overzien, Scope=Portfolio`
2. Product Workspace: `Intent=Overzien, Scope=Product, ProductId=X`
3. Analysis: `Intent=Begrijpen, Scope=Product, ProductId=X, Trigger=Deviation`
4. Planning: `Intent=Plannen, Scope=Product, ProductId=X, Time=Future`
5. Communication: `Intent=Delen, Scope=Product, ProductId=X`

#### Flow Template: Comparison Within Scope

```
[Landing (Overzien)]
    ↓ select team
[Team Workspace]
    ↓ compare to previous sprint
[Analysis Workspace (Comparison mode)]
    ↓ explain difference
[Analysis Workspace (Diagnosis)]
    ↓ (optional) plan or share
[Planning/Communication Workspace]
```

### 6.2 Begrijpen Flows

#### Flow Template: Deviation-Driven Diagnosis

```
[Landing (Begrijpen)]
    ↓ select deviation type (quality / throughput / predictability / flow)
[Analysis Workspace]
    → diagnose issue
    → gather evidence
    → (optional) route to Planning to address
    → (optional) route to Sharing to communicate
```

**Deviation Types (Analysis Modes):**
- Quality: Health mode (validation issues)
- Throughput: Flow mode (PR/Pipeline bottlenecks)
- Predictability: Forecast mode (estimate accuracy)
- Engineering Flow: Timeline mode (state transitions)

#### Flow Template: Question-Driven Investigation

```
[Landing (Begrijpen)]
    ↓ search/select question
[Analysis Workspace]
    → build context
    → gather evidence
    → may end without action
```

**Note:** Flows MAY end without action. Not all investigation leads to planning or sharing.

### 6.3 Plannen Flows

#### Flow Template: Plan Construction

```
[Landing (Plannen)]
    ↓ select scope
[Planning Workspace]
    → structure work
    → sequence items
    → set milestones
    → validate plan
    ↓ (optional) share plan
[Communication Workspace]
```

**Planning is Deliberate:** Planning is NOT an automatic outcome of analysis. Users explicitly enter planning mode.

#### Flow Template: Plan Validation

```
[Planning Workspace]
    ↓ validation issues detected
[Analysis Workspace (triggered by Deviation)]
    → investigate conflict
    → return with resolution
[Planning Workspace]
    → apply resolution
```

### 6.4 Delen Flows

#### Flow Template: Status Update

```
[Landing (Delen)]
    ↓ select scope and template
[Communication Workspace]
    → generate update from current state
    → customize content
    → export/share
```

#### Flow Template: Share Analysis Context

```
[Analysis Workspace]
    ↓ "Share this" action
[Communication Workspace]
    → snapshot current analysis
    → add narrative
    → export/share
```

---

## 7. Migration Phases

### Phase 1: Foundation — Context Model and Infrastructure

**Goals:**
- Establish context contract infrastructure
- Implement navigation context service
- Prepare for intent-driven navigation

**UI Changes:**
- Add `INavigationContextService` implementation
- Create `NavigationContext` and related types
- No visible changes to users

**Backend Changes:**
- None

**Context Impacts:**
- Context service available but not yet used by workspaces

**Compatibility Strategy:**
- Existing navigation continues to work
- Context service runs in parallel

**Risks:**
- Context contract design may need adjustment based on workspace implementation

**Exit Criteria:**
- [ ] `NavigationContext` record defined in `PoTool.Client/Models`
- [ ] `INavigationContextService` interface defined
- [ ] `NavigationContextService` implementation complete
- [ ] Unit tests for context service pass
- [ ] Context can be created, stored, and retrieved

**What Became Deletable:**
- Nothing (additive phase)

---

### Phase 2: Entry Points — Profile Gating and Landing

**Goals:**
- Ensure profile selection is enforced
- Create Landing page with intent-based entry
- Move meta navigation to header

**UI Changes:**
- Create `Landing.razor` page with four intent cards
- Update MainLayout to include "Return to Landing" in header
- Verify profile gating enforcement (already exists)
- Settings/Profile actions remain in header

**Backend Changes:**
- None

**Context Impacts:**
- Landing page creates initial context based on selected intent
- Profile selection sets `Scope.ProfileId`

**Compatibility Strategy:**
- Sidebar navigation remains active
- Users can use either Landing or sidebar
- Existing routes continue to work

**Risks:**
- User confusion with dual navigation (mitigated: temporary)

**Exit Criteria:**
- [ ] Landing.razor created at `/landing`
- [ ] Four intent cards (Overzien, Begrijpen, Plannen, Delen) implemented
- [ ] Intent selection creates appropriate initial context
- [ ] "Return to Landing" action in header works
- [ ] Profile gating verified (user cannot bypass profile selection)
- [ ] Onboarding → profile selection flow verified

**What Became Deletable:**
- Nothing (additive phase, sidebar still active)

---

### Phase 3: First Workspace — Product Workspace

**Goals:**
- Create Product Workspace as first context-aware workspace
- Demonstrate context-driven behavior
- Replace ProductHome with workspace-based approach

**UI Changes:**
- Create `ProductWorkspace.razor` component/page
- Implement context-aware header and navigation
- Add drill-down actions to Analysis/Planning/Communication
- Product Workspace accessible from Landing (Overzien intent)

**Backend Changes:**
- None

**Context Impacts:**
- Product Workspace reads and respects context
- Drill-down actions create child contexts with proper `Trigger` and `Intent`

**Compatibility Strategy:**
- ProductHome.razor remains at `/` and `/product-home`
- Product Workspace available at `/workspace/product`
- Both routes work during transition

**Risks:**
- Workspace may not yet cover all ProductHome features

**Exit Criteria:**
- [ ] ProductWorkspace.razor created
- [ ] Context-aware header displays current scope
- [ ] Drill-down actions create proper child contexts
- [ ] Landing → Product Workspace flow works
- [ ] Context back-navigation works

**What Became Deletable:**
- Nothing (ProductHome still primary)

---

### Phase 4: Complete Overzien Flow — Team Workspace

**Goals:**
- Create Team Workspace
- Complete one end-to-end Overzien flow
- Validate context flow across workspaces

**UI Changes:**
- Create `TeamWorkspace.razor` component/page
- Implement sprint/time navigation
- Add team-specific views (velocity, timeline)
- Team Workspace accessible from Product Workspace or Landing

**Backend Changes:**
- None

**Context Impacts:**
- Team selection narrows scope from Product to Team
- Sprint selection affects `TimeHorizon`

**Compatibility Strategy:**
- VelocityDashboard.razor remains active
- Team Workspace available at `/workspace/team`

**Risks:**
- Velocity page has significant functionality that must be preserved

**Exit Criteria:**
- [ ] TeamWorkspace.razor created
- [ ] Sprint navigation implemented
- [ ] Velocity trend display works
- [ ] Context: Product → Team scope transition works
- [ ] Complete Overzien flow: Landing → Product → Team → Analysis works

**What Became Deletable:**
- Nothing (legacy pages still primary)

---

### Phase 5: Analysis Workspace — Begrijpen Intent

**Goals:**
- Create unified Analysis Workspace with multiple modes
- Consolidate health, effort, flow, forecast, dependencies, timeline views
- Complete Begrijpen flows

**UI Changes:**
- Create `AnalysisWorkspace.razor` component/page
- Implement mode tabs/switcher (Health, Effort, Flow, Forecast, Dependencies, Timeline)
- Migrate content from existing analysis pages
- Analysis Workspace accessible from Landing (Begrijpen) and other workspaces

**Backend Changes:**
- Add unified `/api/metrics/health-summary` endpoint (consolidation)
- Deprecate `/api/healthcalculation` endpoints

**Context Impacts:**
- Mode selection updates context
- Entry from deviation creates appropriate default mode
- Cross-workspace routing (to Planning, Communication) works

**Compatibility Strategy:**
- All legacy analysis pages remain active
- Analysis Workspace available at `/workspace/analysis`
- Parallel run: both access same backend endpoints

**Risks:**
- Large scope: many pages to consolidate
- Mode switching UX complexity

**Exit Criteria:**
- [ ] AnalysisWorkspace.razor created with mode switcher
- [ ] Health mode implements BacklogHealth functionality
- [ ] Effort mode implements EffortDistribution functionality
- [ ] Flow mode implements PR/Pipeline insights
- [ ] Forecast mode implements EpicForecast functionality
- [ ] Dependencies mode implements DependencyGraph functionality
- [ ] Timeline mode implements StateTimeline functionality
- [ ] Context-driven mode selection works (entry via Begrijpen selects appropriate mode)
- [ ] Route to Planning Workspace works
- [ ] Route to Communication Workspace works

**What Became Deletable (legacy status, not yet deleted):**
- BacklogHealth.razor (legacy)
- EffortDistribution.razor (legacy)
- PRInsight.razor (legacy)
- PipelineInsights.razor (legacy)
- EpicForecast.razor (legacy)
- DependencyGraph.razor (legacy)
- StateTimeline.razor (legacy)
- VelocityDashboard.razor (legacy - Team Workspace)
- HealthCalculationController.cs (deprecated)

---

### Phase 6: Planning Workspace — Plannen Intent

**Goals:**
- Create Planning Workspace
- Migrate release planning functionality
- Complete Plannen flows

**UI Changes:**
- Create `PlanningWorkspace.razor` component/page
- Migrate release planning board from ReleasePlanning.razor
- Add validation signals and conflict routing
- Planning Workspace accessible from Landing (Plannen) and other workspaces

**Backend Changes:**
- Delete `HealthCalculationController.cs` (deprecated in Phase 5)
- No changes to ReleasePlanningController

**Context Impacts:**
- Planning mode enforces Future time horizon
- Conflict detection routes to Analysis with Deviation trigger

**Compatibility Strategy:**
- ReleasePlanning.razor remains active at `/release-planning`
- Planning Workspace available at `/workspace/planning`

**Risks:**
- Release planning has complex drag-and-drop interactions

**Exit Criteria:**
- [ ] PlanningWorkspace.razor created
- [ ] Release planning board migrated
- [ ] Validation/conflict detection works
- [ ] Route to Analysis for conflict explanation works
- [ ] Route to Communication for sharing works
- [ ] HealthCalculationController.cs deleted

**What Became Deletable (legacy status):**
- ReleasePlanning.razor (legacy)

---

### Phase 7: Communication Workspace and Sidebar Removal

**Goals:**
- Create Communication Workspace
- Complete Delen flows
- Remove sidebar navigation
- Route all traffic through Landing + Workspaces

**UI Changes:**
- Create `CommunicationWorkspace.razor` component/page
- Implement template selection, snapshot generation, export
- **Remove NavMenu.razor from MainLayout**
- Update all workspace routes to be primary
- Remove legacy page routes or redirect to workspaces

**Backend Changes:**
- None

**Context Impacts:**
- Delen intent fully supported
- All navigation flows through context-aware system

**Compatibility Strategy:**
- Legacy routes redirect to workspace equivalents
- No parallel run: workspaces are primary

**Risks:**
- User disruption (mitigated: workspaces have been available for testing)
- Missing edge case functionality

**Exit Criteria:**
- [ ] CommunicationWorkspace.razor created
- [ ] Template selection implemented
- [ ] Snapshot/export functionality works
- [ ] **NavMenu.razor removed from MainLayout**
- [ ] **Sidebar CSS/styling removed**
- [ ] All legacy routes redirect to workspace equivalents
- [ ] No sidebar or feature-based navigation exists

**What Became Deletable:**
- NavMenu.razor (delete)
- NavMenu.razor.css (delete)
- ProductHome.razor (redirect to Product Workspace, then delete)
- VelocityDashboard.razor (delete)
- BacklogHealth.razor (delete)
- EffortDistribution.razor (delete)
- PRInsight.razor (delete)
- PipelineInsights.razor (delete)
- EpicForecast.razor (delete)
- DependencyGraph.razor (delete)
- StateTimeline.razor (delete)
- ReleasePlanning.razor (delete)
- Help.razor (integrate into header action, delete page)

---

### Phase 8: Cleanup and Verification

**Goals:**
- Delete all legacy code
- Verify end state
- Document completion

**UI Changes:**
- Delete all legacy page files marked for deletion in Phase 7
- Delete unused components
- Remove unused services
- Update documentation

**Backend Changes:**
- Delete deprecated endpoints:
  - `/api/healthcalculation/*`
  - Any unused filtering endpoints
- Remove unused handlers/services
- Clean up unused DTOs

**Context Impacts:**
- None (cleanup only)

**Compatibility Strategy:**
- N/A (no legacy code remains)

**Risks:**
- Accidental deletion of needed code (mitigated: comprehensive testing before deletion)

**Exit Criteria:**
- [ ] All legacy pages deleted
- [ ] All deprecated backend endpoints deleted
- [ ] All unused client services deleted
- [ ] Application fully functional with workspace-only navigation
- [ ] All tests pass
- [ ] Documentation updated
- [ ] **Migration complete per Section 8 verification**

**What Became Deletable:**
- Deprecated backend handlers
- Unused DTOs
- Unused client services

---

## 8. Completion Guards

### 8.1 Migration End State (System-Level DoD)

The migration is complete ONLY when:

- [ ] No sidebar or feature-based navigation exists
- [ ] All navigation flows through:
  - [ ] Profile selection (mandatory gating)
  - [ ] Landing (intent entry)
  - [ ] Contextual progression in workspaces
- [ ] No legacy page-based routes are in use
- [ ] No duplicate legacy/new functionality exists
- [ ] All replaced frontend artifacts are deleted:
  - [ ] NavMenu.razor
  - [ ] ProductHome.razor (or reduced to redirect)
  - [ ] All legacy Metrics pages
  - [ ] ReleasePlanning.razor
  - [ ] Help.razor
- [ ] All replaced backend artifacts are deleted:
  - [ ] HealthCalculationController.cs
  - [ ] Deprecated filtering endpoints (if any)

### 8.2 End State Verification Procedure

1. **Navigation Audit**
   - Start from fresh browser session
   - Verify: Onboarding appears if needed → Profile selection required → Landing page shown
   - Verify: No sidebar visible at any point
   - Verify: All navigation is contextual (through workspace actions)

2. **Route Audit**
   - Attempt to access legacy routes directly
   - Verify: All redirect to workspace equivalents
   - Verify: No 404 errors for previously valid routes

3. **Functionality Audit**
   - For each original page, verify all features exist in target workspace
   - Use Page-to-Workspace Mapping Table as checklist

4. **Code Audit**
   - Verify no files in Pages/Metrics folder (deleted)
   - Verify NavMenu.razor does not exist
   - Verify HealthCalculationController.cs does not exist

5. **Context Audit**
   - Navigate through complete flows for each intent
   - Verify back navigation works correctly
   - Verify context is preserved across workspace transitions

### 8.3 Sections Demonstrating Completion

| Requirement | Demonstrated By |
|-------------|-----------------|
| No sidebar navigation | Phase 7 exit criteria + NavMenu.razor deleted |
| Profile gating enforced | Phase 2 exit criteria + existing implementation |
| Landing with intents | Phase 2 exit criteria |
| Contextual workspaces | Phases 3-6 exit criteria |
| All legacy deleted | Phase 8 exit criteria |

### 8.4 Context Contract Stability Verification

Before any phase completion, verify:
- [ ] `NavigationContext` structure unchanged from Phase 1 definition
- [ ] All workspaces use the same context service
- [ ] No workspace-specific context reinterpretation
- [ ] Context flows correctly between workspaces

---

## 9. Continuous Maintenance Log

This section tracks all updates to the migration plan.

### Log Format

```
### [Date] - [Change Type] - [Author]

**Changed:** Description of what changed
**Reason:** Why the change was made
**Impact:** What phases or sections are affected
```

### Entries

---

### 2026-01-23 - Initial Creation - Migration Plan

**Changed:** Initial plan document created
**Reason:** Establish single source of truth for UI migration
**Impact:** All phases defined, execution can begin

---

<!-- Future entries will be added here as the migration progresses -->

---

## Appendix A: Current Application Structure Reference

### A.1 Current Page Routes

| Route | Page File | Purpose |
|-------|-----------|---------|
| `/` | ProductHome.razor | Main dashboard |
| `/product-home` | ProductHome.razor | Alias |
| `/profiles` | ProfilesHome.razor | Profile selection |
| `/onboarding` | Onboarding.razor | First-time setup |
| `/backlog-health` | BacklogHealth.razor | Health analysis |
| `/effort-distribution` | EffortDistribution.razor | Effort analysis |
| `/velocity` | VelocityDashboard.razor | Velocity trends |
| `/state-timeline` | StateTimeline.razor | State history |
| `/state-timeline/{workItemId}` | StateTimeline.razor | Item state history |
| `/epic-forecast` | EpicForecast.razor | Forecasting |
| `/epic-forecast/{epicId}` | EpicForecast.razor | Epic forecast |
| `/dependency-graph` | DependencyGraph.razor | Dependencies |
| `/pr-insights` | PRInsight.razor | PR metrics |
| `/pipeline-insights` | PipelineInsights.razor | Pipeline metrics |
| `/release-planning` | ReleasePlanning.razor | Planning board |
| `/tfsconfig` | TfsConfig.razor | TFS configuration |
| `/settings/workitem-states` | WorkItemStates.razor | State config |
| `/settings/products` | ManageProducts.razor | Product config |
| `/settings/teams` | ManageTeams.razor | Team config |
| `/settings/productowner/{id}` | ManageProductOwner.razor | Profile view |
| `/settings/productowner/edit` | EditProductOwner.razor | Profile edit |
| `/help` | Help.razor | Help page |
| `/not-found` | NotFound.razor | 404 page |

### A.2 Current Backend Controllers

| Controller | Endpoint Base | Purpose |
|------------|---------------|---------|
| ProfilesController | `/api/profiles` | Profile CRUD |
| SettingsController | `/api/settings` | App settings |
| StartupController | `/api/startup` | Startup checks |
| ProductsController | `/api/products` | Product CRUD |
| TeamsController | `/api/teams` | Team CRUD |
| WorkItemsController | `/api/workitems` | Work item data |
| FilteringController | `/api/filtering` | Validation filters |
| MetricsController | `/api/metrics` | All metrics |
| HealthCalculationController | `/api/healthcalculation` | Health scores |
| PipelinesController | `/api/pipelines` | Pipeline data |
| PullRequestsController | `/api/pullrequests` | PR data |
| ReleasePlanningController | `/api/releaseplanning` | Planning board |

### A.3 Current Client Services

| Service | Purpose |
|---------|---------|
| ProfileService | Profile management |
| ProductService | Product operations |
| TeamService | Team operations |
| SettingsService | Settings access |
| WorkItemService | Work item data |
| PipelineService | Pipeline data |
| PullRequestService | PR data |
| ReleasePlanningService | Planning operations |
| BacklogHealthCalculationService | Health calculations |
| StateClassificationService | State mapping |
| ExportService | Report generation |
| ErrorMessageService | Error handling |
| OnboardingService | Onboarding state |
| StartupOrchestratorService | Startup routing |

---

## Appendix B: Workspace Route Structure (Target)

### B.1 Target Routes

| Route | Workspace | Notes |
|-------|-----------|-------|
| `/` | Profile Selection | Redirect if no profile |
| `/landing` | Landing | Intent selection |
| `/workspace/product` | Product Workspace | |
| `/workspace/product/{productId}` | Product Workspace | Direct access |
| `/workspace/team` | Team Workspace | |
| `/workspace/team/{teamId}` | Team Workspace | Direct access |
| `/workspace/analysis` | Analysis Workspace | Mode via context |
| `/workspace/analysis/{mode}` | Analysis Workspace | Direct mode access |
| `/workspace/planning` | Planning Workspace | |
| `/workspace/communication` | Communication Workspace | |
| `/onboarding` | Onboarding | Keep as blocking page |
| `/settings/*` | Settings (header modal) | Keep as modal/drawer |

### B.2 Legacy Route Redirects

| Legacy Route | Redirect To |
|--------------|-------------|
| `/product-home` | `/workspace/product` |
| `/backlog-health` | `/workspace/analysis/health` |
| `/effort-distribution` | `/workspace/analysis/effort` |
| `/velocity` | `/workspace/team` |
| `/state-timeline` | `/workspace/analysis/timeline` |
| `/epic-forecast` | `/workspace/analysis/forecast` |
| `/dependency-graph` | `/workspace/analysis/dependencies` |
| `/pr-insights` | `/workspace/analysis/flow` |
| `/pipeline-insights` | `/workspace/analysis/flow` |
| `/release-planning` | `/workspace/planning` |
| `/tfsconfig` | `/settings` (modal) |
| `/help` | (removed, use header action) |

---

*End of Migration Plan Document*
