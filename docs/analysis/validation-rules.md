# Validation, Integrity, and Health Rules Analysis

## 1. Current health providers and entry points

The repository currently surfaces validation, integrity, and health signals through several distinct layers instead of one single model.

### Core and domain services

- `PoTool.Core.Domain/BacklogQuality/Services/RuleCatalog.cs`  
  Canonical rule registry for `SI-*`, `RR-*`, and `RC-*`.
- `PoTool.Core.Domain/BacklogQuality/Services/BacklogValidationService.cs`  
  Executes canonical rules in fixed order and applies refinement-driven suppression.
- `PoTool.Core.Domain/BacklogQuality/Services/ImplementationReadinessService.cs`  
  Derives binary readiness states and rebuilds blocking findings from readiness semantics.
- `PoTool.Core.Domain/BacklogQuality/Services/BacklogQualityAnalyzer.cs`  
  Facade that combines validation, readiness scoring, and readiness-state output.
- `PoTool.Core/Health/BacklogStateComputationService.cs`  
  Projects analyzer output into the existing backlog state / readiness score model used by Health and Backlog Overview.
- `PoTool.Core/Health/BacklogHealthCalculator.cs`  
  Separate numeric health-score formula based on issue counts, blocked items, and effort gaps.

### API handlers and factories

- `PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`  
  Produces per-iteration backlog health data.
- `PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs`  
  Produces multi-iteration backlog health data.
- `PoTool.Api/Handlers/Metrics/BacklogHealthDtoFactory.cs`  
  Converts backlog-quality findings into Health-facing DTO summaries such as Structural Integrity, Refinement Blocker, and Refinement Needed counts.
- `PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`  
  Builds the Health workspace product summary from readiness scores and ready-feature counts.
- `PoTool.Api/Services/Sync/ValidationComputeStage.cs`  
  Computes validation results during sync for cached indicators.
- `PoTool.Api/Handlers/WorkItems/GetValidationTriageSummaryQueryHandler.cs`  
  Groups issues by category/rule for Validation Triage.
- `PoTool.Api/Handlers/WorkItems/GetValidationQueueQueryHandler.cs`  
  Builds the Validation Queue from rule/category filters.
- `PoTool.Api/Handlers/WorkItems/GetValidationFixSessionQueryHandler.cs`  
  Builds category/rule-specific fix-session data.

### Shared/UI categorization seams

- `PoTool.Shared/WorkItems/ValidationRuleCatalog.cs`  
  Shared metadata bridge used by queue/triage/filter consumers. This is also where `RC-2` is aliased into the UI category `EFF`.
- `PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs`  
  Resolves validation categories for filtering and still carries local filter/category logic.
- `docs/user/navigation-map.md` and `docs/user/gebruikershandleiding.md`  
  Document current user-visible grouping as `SI`, `RR`, `RC`, and `EFF`.

## 2. Full inventory of current rules

The codebase contains three kinds of “rules” today:

1. **Executable validation rules** with stable IDs
2. **Readiness / ownership scoring rules** without validation IDs
3. **Health heuristics** used for dashboards and summaries

### 2.1 Structural rules

These are the clearest match for a future Integrity model.

| Current rule | Current implementation | What it does | Notes |
| --- | --- | --- | --- |
| `SI-1` | `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs` and legacy `PoTool.Core/WorkItems/Validators/Rules/DoneParentWithUnfinishedDescendantsRule.cs` | Parent in `Done` with any descendant not in `Done` or `Removed` is invalid. | Canonical metadata classifies it as `StructuralIntegrity`. |
| `SI-2` | `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs` and legacy `PoTool.Core/WorkItems/Validators/Rules/RemovedParentWithUnfinishedDescendantsRule.cs` | Parent in `Removed` with any descendant not in `Done` or `Removed` is invalid. | Uses the same recursive descendant traversal pattern as `SI-1`. |
| `SI-3` | `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs` and legacy `PoTool.Core/WorkItems/Validators/Rules/NewParentWithInProgressDescendantsRule.cs` | Parent in `New` with started descendants is invalid. | Canonical rule fires on `InProgress` **or** `Done` descendants; older feature/spec text still describes only in-progress descendants. |

### 2.2 Classification rules

Using the requested categorization, the current “Classification” bucket is really a mix of refinement gating, implementation-readiness gating, and score/owner classification.

#### Executable validation rules

| Current rule | Family in canonical model | What it does | Current ownership |
| --- | --- | --- | --- |
| `RR-1` | `RefinementReadiness` | Epic description must be present and at least 10 characters. | Product Owner |
| `RR-2` | `RefinementReadiness` | Feature description must be present and at least 10 characters. | Product Owner |
| `RR-3` | `RefinementReadiness` | Epic must have at least one active Feature child. | Product Owner |
| `RC-1` | `ImplementationReadiness` | PBI description must be present. | Team |
| `RC-2` | `ImplementationReadiness` | PBI effort must be present and greater than zero. | Team |
| `RC-3` | `ImplementationReadiness` | Feature must have at least one active PBI child. | Team |

#### Readiness / ownership scoring rules

These are active rules in behavior, but not represented as first-class validation-rule definitions:

| Current rule-like behavior | Current implementation | Effect |
| --- | --- | --- |
| PBI readiness `0 / 75 / 100` | `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs`, surfaced via `PoTool.Core/Health/BacklogStateComputationService.cs` | Missing description = `0`; description present but effort missing = `75`; description + effort = `100`. |
| Feature readiness `0 / 25 / average(PBI)` | Same services as above | Missing description = `0`; no active PBI children = `25`; otherwise derived from PBI scores. |
| Epic readiness `0 / 30 / average(Feature)` | Same services as above | Missing description = `0`; no active Feature children = `30`; otherwise derived from Feature scores. |
| Feature owner classification (`PO`, `Team`, `Ready`) | `PoTool.Core.Domain/BacklogQuality/Services/BacklogReadinessService.cs` and `PoTool.Core/Health/BacklogStateComputationService.cs` | Converts score/gating state into an owner/action signal. |

### 2.3 Other rules and health heuristics

These behaviors affect Health output but do not currently live in the canonical validation-rule catalog.

| Current heuristic / rule-like behavior | Current implementation | Effect |
| --- | --- | --- |
| Health score = `100 - issuePercentage * 100` | `PoTool.Core/Health/BacklogHealthCalculator.cs` | Uses `workItemsWithoutEffort + workItemsInProgressWithoutEffort + parentProgressIssues + blockedItems`. |
| Blocked item detection | `PoTool.Api/Handlers/Metrics/BacklogHealthDtoFactory.cs` | Treats state text containing `Blocked` or `On Hold` as blocked. |
| In-progress-at-end detection | `PoTool.Api/Handlers/Metrics/BacklogHealthDtoFactory.cs` | Uses raw state-name checks for `In Progress` or `Active`. |
| Validation summary grouping | `PoTool.Api/Handlers/Metrics/BacklogHealthDtoFactory.cs` | Maps findings into summary labels `Structural Integrity`, `Refinement Blocker`, and `Refinement Needed`. |
| UI category alias `EFF` | `PoTool.Shared/WorkItems/ValidationRuleCatalog.cs` | Exposes `RC-2` as a separate triage/filter category even though canonical rule identity remains `RC-2`. |
| Legacy epic/feature missing-effort compatibility | `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs` | Re-adds Epic/Feature `RC-2` issues outside the canonical rule, even though canonical `RC-2` applies only to PBI types. |

## 3. Current categorization and execution model

### 3.1 Canonical domain categorization

The canonical backlog-quality slice already defines:

- `StructuralIntegrity`
- `RefinementReadiness`
- `ImplementationReadiness`
- readiness scoring as a parallel projection

Primary files:

- `PoTool.Core.Domain/BacklogQuality/Rules/RuleMetadata.cs`
- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs`
- `PoTool.Core.Domain/BacklogQuality/Services/RuleCatalog.cs`
- `docs/domain/backlog_quality_domain_model.md`

### 3.2 Legacy / shared categorization

The older and UI-facing categorization is still different:

- `ValidationCategory.StructuralIntegrity`
- `ValidationCategory.RefinementReadiness`
- `ValidationCategory.RefinementCompleteness`
- `ValidationCategory.MissingEffort`

Primary files:

- `PoTool.Shared/WorkItems/ValidationCategory.cs`
- `PoTool.Shared/WorkItems/ValidationRuleCatalog.cs`
- `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs`

### 3.3 Execution order

Current execution is deterministic and already close to the desired Integrity / Planning Quality split:

1. Structural Integrity (`SI-*`)
2. Refinement Readiness (`RR-*`)
3. Implementation Readiness (`RC-*`)

Current suppression behavior:

- Structural findings are always reported.
- Refinement-readiness blockers suppress reported implementation-readiness findings beneath the blocked scope.
- Implementation-readiness states still retain their own blocking findings even when reporting is suppressed.
- Legacy wrapper behavior still re-injects Epic/Feature missing-effort findings for compatibility.

Primary files:

- `PoTool.Core.Domain/BacklogQuality/Services/BacklogValidationService.cs`
- `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs`
- `PoTool.Tests.Unit/Services/BacklogValidationServiceTests.cs`

## 4. Overlap and duplicate logic

### 4.1 Canonical rules vs legacy wrappers

There are two active interpretations of the same rule set:

- canonical rules in `PoTool.Core.Domain/BacklogQuality`
- legacy-compatible validator output in `PoTool.Core/WorkItems/Validators`

That is intentional for compatibility, but it means rule behavior is not owned in one place at the application boundary.

### 4.2 `RC-2` / `EFF` is the main duplication hotspot

`RC-2` is simultaneously represented as:

- canonical rule `RC-2`
- semantic tag `MissingEffort`
- shared category `ValidationCategory.MissingEffort`
- UI alias `EFF`
- legacy Epic/Feature compatibility issues synthesized in `HierarchicalWorkItemValidator`

This is the clearest overlap between the existing system and a future Planning Quality model. The same concept is being grouped, filtered, and displayed through several different identities.

### 4.3 Description-length threshold exists in more than one rule system

The minimum description length `10` exists in:

- `PoTool.Core.Domain/BacklogQuality/Rules/CanonicalBacklogQualityRules.cs`
- `PoTool.Core/WorkItems/Validators/Rules/ValidationRuleConstants.cs`

The actual threshold matches, but the duplication means future tuning would need coordinated changes.

### 4.4 Readiness classification is split from validation metadata

Score formulas and owner-state classification are implemented in the readiness services, but they are not represented in the same metadata structure as `SI-*`, `RR-*`, and `RC-*`.

Result:

- validation rules are cataloged
- planning/readiness scoring rules are executable
- but the overall “quality model” is still split across two representations

### 4.5 Health heuristics are separate from validation ownership

`BacklogHealthCalculator` and `BacklogHealthDtoFactory` still carry Health-specific logic that is not part of the canonical rule catalog:

- blocked-state string matching
- in-progress-at-end string matching
- issue-summary label grouping
- separate health-score arithmetic

This makes Health a consumer of validation output plus additional handler-owned heuristics, not a pure projection of one canonical model.

## 5. Gaps vs desired Integrity / Planning Quality model

The current implementation already covers much of the raw rule behavior, but it does not yet present one clean Integrity / Planning Quality model.

### 5.1 What maps cleanly already

- **Integrity**  
  `SI-1`, `SI-2`, and `SI-3` already form a coherent structural family and are domain-owned.
- **Planning Quality gating**  
  `RR-1..RR-3` and `RC-1..RC-3` already express the planning/refinement/implementation checks needed to decide whether work is ready.
- **Planning Quality scoring**  
  PBI/Feature/Epic readiness scoring and owner-state classification already exist and are used in Health and Backlog Overview.

### 5.2 What is still missing

1. **No single canonical “Planning Quality” surface**  
   Planning-quality behavior is split between rule families (`RR` / `RC`), readiness scores, owner-state classification, and UI aliases.

2. **No first-class metadata for scoring rules**  
   The score rules (`0/30`, `0/25`, `0/75/100`) are real behavior, but they are not represented as cataloged rule definitions the way `SI-*`, `RR-*`, and `RC-*` are.

3. **No single canonical Health rule set**  
   Health combines domain findings with dashboard heuristics such as blocked-state and in-progress-state string matching.

4. **Category naming still drifts at the edges**  
   Current terms include:
   - `ImplementationReadiness`
   - `RefinementCompleteness`
   - `MissingEffort`
   - `EFF`
   - `Refinement Needed`

   These are related, but not one unified model.

5. **Epic/Feature missing effort is unresolved conceptually**  
   `features/02032026_backlog_health.md` treats missing Epic/Feature effort as an Integrity signal, while the canonical backlog-quality model explicitly keeps parent effort outside the canonical readiness rules. Current runtime behavior still adds legacy Epic/Feature `RC-2` compatibility findings.

6. **One structural rule still has wording drift**  
   The canonical `SI-3` rule fires for descendants in progress **or done**, while older text still describes only in-progress descendants.

## 6. Refactoring recommendations

1. **Keep `PoTool.Core.Domain/BacklogQuality` as the single source of executable rule truth**  
   Continue moving interpretation out of legacy adapters instead of adding new rule logic in handlers or UI seams.

2. **Decide one canonical boundary for `RC-2` / `MissingEffort` / `EFF`**  
   Recommended direction based on current code: keep canonical identity as `RC-2` with semantic tag `MissingEffort`, and treat `EFF` as a pure UI alias only.

3. **Remove or explicitly quarantine legacy Epic/Feature `RC-2` compatibility behavior**  
   Today it is synthesized in `HierarchicalWorkItemValidator`, which means the runtime still mixes canonical and compatibility semantics.

4. **Unify category lookup through shared metadata**  
   Consumers such as `WorkItemFilterer`, triage handlers, queue handlers, and client metadata should resolve categories from one shared metadata source instead of carrying local mappings or prefix-based inference.

5. **Make the Planning Quality projection explicit**  
   If the target model is “Integrity + Planning Quality,” introduce one canonical projection that bundles:
   - refinement-readiness findings
   - implementation-readiness findings
   - readiness score
   - owner-state classification

   That would better match the current behavior than inventing an entirely new rule family.

6. **Separate canonical rule output from Health-specific heuristics**  
   Keep validation/readiness findings domain-owned, but move Health dashboard heuristics into an explicit projection service or contract so the health model is easier to analyze and evolve.

7. **Align old wording with current canonical behavior**  
   Update remaining specs/docs/comments so `SI-3` and parent-effort treatment match the behavior that is actually running.

## 7. Bottom line

Current implementation status:

- **Structural / Integrity rules:** already well-defined and domain-owned
- **Classification / Planning Quality rules:** present, but split across validation families, scoring services, owner-state logic, and UI aliases
- **Other / Health rules:** still partly handler/dashboard heuristics rather than one canonical rule model

The main refactoring need is not new rule invention; it is consolidation. The repository already has the rule content needed for an Integrity / Planning Quality model, but it still expresses that content through multiple parallel categorization and compatibility seams.
