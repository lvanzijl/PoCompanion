# Planning Quality & Signal Integration Analysis

## 1. Current severity model

The repository uses two parallel severity representations depending on the layer.

### 1.1 Domain layer — `RuleFindingClass`

`PoTool.Core.Domain/BacklogQuality/Rules/RuleFindingClass.cs` defines:

| Value | Meaning |
|---|---|
| `StructuralWarning` | Non-blocking structural issue; reported as a backlog health problem. |
| `RefinementBlocker` | Blocks refinement of the work item tree; suppresses implementation-readiness evaluation. |
| `ImplementationBlocker` | Blocks implementation readiness for a PBI. |

There is no explicit "informational" or "warning" class at this layer — the closest match is `StructuralWarning`, which is non-blocking by convention.

### 1.2 Domain layer — `RuleFamily`

`PoTool.Core.Domain/BacklogQuality/Rules/RuleFamily.cs` groups rules into:

- `StructuralIntegrity`
- `RefinementReadiness`
- `ImplementationReadiness`

There is no `PlanningQuality` family yet. All current families map to either a blocker or a structural-warning finding class.

### 1.3 Shared/UI layer — `ValidationCategory`

`PoTool.Shared/WorkItems/ValidationCategory.cs` exposes a flatter model:

| Category | UI key | MudBlazor severity |
|---|---|---|
| `StructuralIntegrity` | `SI` | `Error` |
| `RefinementReadiness` | `RR` | `Warning` |
| `RefinementCompleteness` | `RC` | `Warning` |
| `MissingEffort` | `EFF` | `Info` |

This mapping lives in `PoTool.Client/Models/ValidationCategoryMeta.cs` (the `GetAlertSeverity` helper).

### 1.4 Shared/UI layer — `ValidationConsequence`

`PoTool.Shared/WorkItems/ValidationConsequence.cs` defines three outcome values:

- `BacklogHealthProblem` — reported but non-suppressing
- `RefinementBlocker` — suppresses PBI-level rule evaluation
- `IncompleteRefinement` — blocks implementation readiness

These consequences map roughly to the three domain finding classes, but they are expressed as a separate enum without a direct typed bridge.

### 1.5 Summary: what is missing

There is no first-class `Warning` or `Info` severity concept in the domain. The current distinction is:

- **Error** = blocker (RefinementBlocker / ImplementationBlocker)
- **Warning** = structural non-blocker (StructuralWarning, surfaced as `SI` in UI)
- **Informational** = implied by the `EFF` / `Info` MudBlazor mapping, but not represented as a distinct domain class

Any new "Planning Quality" category would need an explicit informational or advisory severity class or rely on the existing `Info` MudBlazor mapping through a new `EFF`-style UI category.

---

## 2. Warning vs error distinction

### 2.1 How blockers are distinguished today

The domain uses suppression semantics as the primary discriminator:

1. `SI-*` findings are always reported regardless of other violations.
2. `RR-*` violations block `RC-*` evaluation for the affected scope.
3. `RC-*` violations are reported only when no RR blocker applies.

This suppression logic is implemented in `PoTool.Core.Domain/BacklogQuality/Services/BacklogValidationService.cs`.

### 2.2 How the UI renders severity

`ValidationCategoryMeta.GetAlertSeverity` maps category keys to MudBlazor `Severity`:

- `SI` → `Severity.Error` (red icon)
- `RR` → `Severity.Warning` (amber icon)
- `RC` → `Severity.Warning` (amber icon)
- `EFF` → `Severity.Info` (blue icon)

Any new Planning Quality category key could be added to this switch without changing downstream rendering code.

### 2.3 Informational signals

There is currently no dedicated informational signal tier in the domain. The `EFF` category (`RC-2`, `MissingEffort`) is the de facto informational tier because:

- It is always evaluated regardless of blocker state.
- It maps to `Severity.Info` in the UI.
- It does not suppress or block other validation.

A new advisory Planning Quality signal could follow this same pattern without requiring structural changes to the suppression model.

---

## 3. Signal aggregation

### 3.1 Aggregation points

Findings are aggregated at several layers:

| Layer | File | What it aggregates |
|---|---|---|
| Domain | `BacklogQualityAnalyzer.cs` | Combines validation + readiness scores into `BacklogQualityAnalysisResult`. |
| API | `BacklogHealthDtoFactory.cs` | Groups domain findings into summary labels (Structural Integrity, Refinement Blocker, Refinement Needed). |
| API | `ValidationComputeStage.cs` | Computes and caches validation indicators during sync. |
| Shared | `ValidationRuleCatalog.cs` | Provides the metadata bridge between rule IDs and UI categories. |
| Client | `WorkItemFilterer.cs` | Resolves categories for queue/triage filtering. |

### 3.2 Aggregation gaps relevant to Planning Quality

`BacklogHealthCalculator.CalculateHealthScore` aggregates four counters into a single numeric score:

- `workItemsWithoutEffort`
- `workItemsInProgressWithoutEffort`
- `parentProgressIssues`
- `blockedItems`

This calculator does not distinguish between signal types; it treats all counts equally in a linear formula. As a result, planning-quality signals (such as missing Epic effort or an SP-unused sprint) would depress the health score identically to structural problems, which is misleading.

Any Planning Quality integration should either:

1. Keep these signals out of `BacklogHealthCalculator` and surface them as a separate quality score, or
2. Add a weighted formula so Planning Quality issues have lower impact on the health score.

---

## 4. Integration options for Planning Quality

Three integration patterns are available given the current architecture.

### Option A — New `RuleFamily.PlanningQuality` in the domain

Add `PlanningQuality = 3` to `RuleFamily` and register new canonical rules (`PQ-*`) in `CanonicalBacklogQualityRules.cs`. Each rule would carry `RuleFindingClass.PlanningAdvisory` (new enum value) and `RuleResponsibleParty.ProductOwner`.

- **Pros:** fully canonical, catalogued, testable via the existing rule pipeline.
- **Cons:** requires a new `RuleFindingClass` value and a new suppression/evaluation contract.

### Option B — Extend `ValidationCategory.MissingEffort` with additional advisory checks

Treat planning signals as additional members of the existing `EFF` / `MissingEffort` advisory tier. New rule IDs (`PQ-1`, `PQ-2`, …) are registered in `ValidationRuleCatalog.KnownRules` with `UiCategoryKey = "PQ"` and a new `KnownCategoryLabels` entry.

- **Pros:** minimal change to the domain model; immediately renders with `Severity.Info` if `ValidationCategoryMeta` is extended.
- **Cons:** planning quality becomes a sibling of `EFF` rather than a first-class family; harder to evolve independently.

### Option C — Dedicated `PlanningQualityService` outside the validation pipeline

Implement planning quality as a separate service with its own result model and projection, analogous to `BacklogHealthCalculator`. Findings are surfaced via a separate query handler and a dedicated UI section.

- **Pros:** no coupling to existing validation suppression rules; can carry richer metadata (budget references, sprint identifiers).
- **Cons:** creates a second parallel quality model; increases risk of duplication with existing signals.

**Recommended option:** Option A for canonical signals that belong in the backlog-quality domain (epic without effort, SP not used), combined with a thin projection layer (similar to `BacklogHealthDtoFactory`) that rolls up PQ findings for dashboards. Option C is appropriate only for signals that require data outside the backlog-quality domain (e.g., budget comparison requiring financial data).

---

## 5. Gaps

### 5.1 Epic without effort — gap analysis

| Check | Current state |
|---|---|
| Canonical rule | Not defined. `RC-2` applies to PBIs only. |
| Legacy compatibility | `HierarchicalWorkItemValidator` re-adds Epic/Feature RC-2 findings for backward compatibility, but this is not a canonical rule. |
| UI representation | No dedicated rule ID or category key exists for Epic-level missing effort. |

**Gap:** A canonical `PQ-1` (or extended `RC-2`) rule scoped to `Epic` and `Feature` types is missing.

### 5.2 SP not used — gap analysis

| Check | Current state |
|---|---|
| SP detection | No canonical rule checks whether Story Points are used consistently across PBIs in an iteration. |
| Health score | `BacklogHealthCalculator` counts items without effort, but does not distinguish "effort field not used at all" from "some items missing effort". |
| UI representation | No `PQ-*` or `EFF-*` subcategory exists. |

**Gap:** A sprint- or iteration-scoped advisory rule that flags when no PBIs carry Story Points is entirely absent.

### 5.3 Effort vs budget mismatch — gap analysis

| Check | Current state |
|---|---|
| Budget data | `BudgetSnapshotEntity` exists in the persistence model. |
| Effort data | `WorkItemEntity.Effort` is available. |
| Comparison logic | No service correlates sprint/epic total effort against a budget ceiling. |
| UI representation | No signal, badge, or advisory exists. |

**Gap:** The data is available but no aggregation, rule, or projection layer bridges effort totals to budget thresholds.

### 5.4 No first-class `Warning` severity class in the domain

The domain `RuleFindingClass` has no explicit advisory/informational class. New planning-quality signals would either need to reuse `StructuralWarning` (semantically wrong) or require a new enum value.

### 5.5 `BacklogHealthCalculator` is opaque to signal type

The health score formula does not carry signal provenance. Once a count enters the formula, its origin is lost. This prevents the UI from showing per-category health contributions and prevents filtering bad health scores by signal type.

---

## 6. Recommendations

### 6.1 Add `PlanningAdvisory` to `RuleFindingClass`

```csharp
public enum RuleFindingClass
{
    StructuralWarning      = 0,
    RefinementBlocker      = 1,
    ImplementationBlocker  = 2,
    PlanningAdvisory       = 3   // non-blocking advisory; does not suppress other rules
}
```

This creates a clean domain-level advisory class without breaking the existing suppression model.

### 6.2 Add `PlanningQuality` to `RuleFamily`

```csharp
public enum RuleFamily
{
    StructuralIntegrity    = 0,
    RefinementReadiness    = 1,
    ImplementationReadiness = 2,
    PlanningQuality        = 3
}
```

### 6.3 Register canonical `PQ-*` rules

Register at minimum:

| Rule ID | Title | Scope | Finding class |
|---|---|---|---|
| `PQ-1` | Epic has no effort estimate | Epic | PlanningAdvisory |
| `PQ-2` | Feature has no effort estimate | Feature | PlanningAdvisory |
| `PQ-3` | No PBIs in sprint carry Story Points | Iteration | PlanningAdvisory |
| `PQ-4` | Total PBI effort exceeds feature budget | Feature | PlanningAdvisory |

### 6.4 Add `PQ` to `ValidationRuleCatalog` and `ValidationCategoryMeta`

Extend `ValidationRuleCatalog.KnownCategoryLabels`:

```csharp
["PQ"] = "Planning Quality",
```

Extend `ValidationCategoryMeta.GetAlertSeverity`:

```csharp
"PQ" => Severity.Info,
```

This ensures planning quality signals render with the blue informational badge, consistent with the current `EFF` experience.

### 6.5 Keep `BacklogHealthCalculator` separate

Do not feed `PQ-*` findings into `BacklogHealthCalculator`. Instead, expose a separate `PlanningQualityScore` projection alongside the health score. This preserves the semantic distinction between structural/refinement health and planning quality advisories.

### 6.6 Bridge budget comparison via a dedicated projection service

For effort vs budget mismatch, introduce a `PlanningBudgetProjectionService` that reads `BudgetSnapshotEntity` and compares it against aggregated effort from the backlog. Keep this service in `PoTool.Api/Services` and expose results via a dedicated query handler, not via the existing `BacklogHealthDtoFactory`.

### 6.7 Retire legacy Epic/Feature RC-2 compatibility in `HierarchicalWorkItemValidator`

Once `PQ-1` and `PQ-2` are canonical rules, the legacy re-injection of Epic/Feature RC-2 findings in `HierarchicalWorkItemValidator` should be removed to prevent duplication.
