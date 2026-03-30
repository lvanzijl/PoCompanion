> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# Backlog Quality Domain Exploration

## Summary
- **Rule families found:** four clear families exist today: structural integrity, refinement readiness, ready-to-implement / implementation readiness, and backlog health scoring. The first three already have named executable rules in `PoTool.Core`; the fourth is split between one simple calculator and several handler heuristics.
- **Stability assessment:** the validation engine and backlog-readiness scoring are stable enough to centralize. They are specification-backed (`/features/20260119_workitem_validation.md`, `/features/02032026_backlog_health.md`), isolated from persistence, and covered by dedicated tests in `/PoTool.Tests.Unit/HierarchicalWorkItemValidatorTests.cs` and `/PoTool.Tests.Unit/Services/BacklogStateComputationServiceTests.cs`.
- **Duplication assessment:** executable rule duplication is low in the core engine, but rule metadata and categorization are duplicated outside it. The biggest hotspot is `RC-2`: the rule itself lives in `/PoTool.Core/WorkItems/Validators/Rules/PbiEffortEmptyRule.cs`, while queue/triage/filter/UI code repeatedly special-case that rule as the separate `EFF` category.
- **Domain vs UI:** the actual backlog-quality logic lives in Core and some API handlers. The Blazor pages (`/PoTool.Client/Pages/Home/BacklogOverviewPage.razor`, `/PoTool.Client/Pages/Home/ValidationTriagePage.razor`, `/PoTool.Client/Pages/Home/ValidationQueuePage.razor`) are presentational and do not evaluate rules themselves.
- **Recommendation:** yes, backlog quality should be the next domain slice after sprint analytics CDC, but the slice should focus on **validation + backlog readiness** first. Queue/triage pages, handler orchestration, and heuristic health dashboards should remain adapters around that slice.

## Integrity Rules
- **Locations**
  - Executable rules: `/PoTool.Core/WorkItems/Validators/Rules/DoneParentWithUnfinishedDescendantsRule.cs`, `/RemovedParentWithUnfinishedDescendantsRule.cs`, `/NewParentWithInProgressDescendantsRule.cs`
  - Orchestration and suppression order: `/PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs`
  - Shared consequence/responsibility contracts: `/PoTool.Shared/WorkItems/ValidationCategory.cs`, `/ValidationConsequence.cs`, `/ResponsibleParty.cs`, `/HierarchicalValidationResult.cs`
  - UI/reporting consumers: `/PoTool.Api/Handlers/WorkItems/GetValidationTriageSummaryQueryHandler.cs`, `/GetValidationQueueQueryHandler.cs`, `/PoTool.Client/Components/Validation/TriageCategoryCard.razor`
  - Spec/docs: `/features/20260119_workitem_validation.md`, `/docs/architecture/validation-system-report.md`
- **What exists today**
  - `SI-1`: done parent with unfinished descendants
  - `SI-2`: removed parent with non-removed descendants
  - `SI-3`: new parent with in-progress or done descendants
  - All three are recursive tree rules and produce `BacklogHealthProblem`.
- **Duplication**
  - Rule execution is centralized in Core.
  - Display titles are duplicated in `/PoTool.Shared/WorkItems/ValidationRuleDescriptions.cs`.
  - Category inference is duplicated outside the validator in `/PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs` and `/PoTool.Client/Services/TreeBuilderService.cs`.
  - `GetValidationImpactAnalysisQueryHandler` reinterprets integrity findings as generic `"ParentProgress"` violations instead of consuming richer domain output, so impact semantics are duplicated in a weaker form.
- **Extraction readiness**
  - **High** for the rules and validator. They are deterministic, named, tested, and already independent of UI and persistence.
  - **Lower** for impact-analysis logic because it still works through legacy `ValidationIssue` output and local hierarchy heuristics.
- **Recommended ownership**
  - Core structural integrity rules belong in a future backlog-quality domain package.
  - Triage, queue, and impact-analysis handlers should remain API/application adapters.

## Refinement Readiness Rules
- **Locations**
  - Executable rules: `/PoTool.Core/WorkItems/Validators/Rules/EpicDescriptionEmptyRule.cs`, `/FeatureDescriptionEmptyRule.cs`, `/EpicWithoutFeaturesRule.cs`
  - Shared threshold: `/PoTool.Core/WorkItems/Validators/Rules/ValidationRuleConstants.cs`
  - Suppression behavior: `/PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs`
  - Readiness scoring spec and consumers: `/PoTool.Core/Health/BacklogStateComputationService.cs`, `/PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs`, `/PoTool.Api/Handlers/WorkItems/GetHealthWorkspaceProductSummaryQueryHandler.cs`
  - UI display: `/PoTool.Client/Pages/Home/BacklogOverviewPage.razor`
- **What exists today**
  - `RR-1`: Epic description must be present and at least 10 characters
  - `RR-2`: Feature description must be present and at least 10 characters
  - `RR-3`: Epic must have at least one Feature child
  - Any RR violation suppresses refinement-completeness evaluation for the tree.
  - The backlog-overview scoring model mirrors this family: Epic score `0` for missing description, `30` for no Features; Feature score `0` for missing description, `25` for no PBIs.
- **Duplication**
  - Core execution is centralized, but thresholds and meanings are repeated in XML comments in `/PoTool.Shared/Health/BacklogStateDtos.cs` and again in `/features/02032026_backlog_health.md`.
  - Queue/triage code re-categorizes these rules by `RR-` prefix instead of relying on a single domain classification source.
- **Extraction readiness**
  - **High.** This is already close to domain-package shape: stable thresholds, explicit rules, no infrastructure coupling, clear tests, and consistent terminology across code and specs.
- **Recommended ownership**
  - The RR rules and their suppression semantics should move into the future backlog-quality domain slice.
  - DTO comments, queue grouping, and page-level wording should remain Shared/UI concerns.

## Ready-to-Implement Rules
- **Locations**
  - Executable rules: `/PoTool.Core/WorkItems/Validators/Rules/PbiDescriptionEmptyRule.cs`, `/FeatureWithoutChildrenRule.cs`, `/PbiEffortEmptyRule.cs`
  - Readiness result contract: `/PoTool.Shared/WorkItems/HierarchicalValidationResult.cs`
  - Backlog-readiness scoring: `/PoTool.Core/Health/BacklogStateComputationService.cs`
  - Product backlog and health summary handlers: `/PoTool.Api/Handlers/WorkItems/GetProductBacklogStateQueryHandler.cs`, `/GetHealthWorkspaceProductSummaryQueryHandler.cs`
  - Legacy overlap: `/PoTool.Core/WorkItems/Validators/WorkItemInProgressWithoutEffortValidator.cs`
  - Queue/triage/filter categorization: `/PoTool.Api/Handlers/WorkItems/GetValidationTriageSummaryQueryHandler.cs`, `/GetValidationQueueQueryHandler.cs`, `/PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs`, `/PoTool.Client/Services/TreeBuilderService.cs`
- **What exists today**
  - `RC-1`: PBI description is empty
  - `RC-3`: Feature has no PBI children
  - `RC-2`: missing effort, but implemented as category `MissingEffort` and evaluated independently of RR suppression
  - `HierarchicalValidationResult.IsReadyForImplementation` defines implementation readiness as: ready for refinement, no incomplete refinement issues, and no missing-effort issues.
  - `BacklogStateComputationService` adds a score-oriented view of the same problem: PBI `0/75/100`, Feature owner `PO/Team/Ready`, Feature average from PBIs.
- **Duplication**
  - This is the most duplicated family.
  - `RC-2` is the clearest example:
    - rule ID remains `RC-2`
    - executable category is `MissingEffort`
    - triage splits it into `EFF`
    - queue/filter/UI code special-case it repeatedly
    - deprecated legacy validator still exists with the same rule ID
  - There is also conceptual duplication between binary validation (`IsReadyForImplementation`) and percentage scoring (`0/75/100`, `0/25/avg`), although they complement each other rather than conflict.
- **Extraction readiness**
  - **Moderate to high** for the underlying rules.
  - **Moderate** for the broader slice because naming and categorization still drift at the edges, especially around missing effort.
  - This family is centralizable, but the extraction should normalize `RC-2` / `MissingEffort` ownership instead of copying today's split.
- **Recommended ownership**
  - Rule execution, readiness state, and scoring logic belong in the future domain slice.
  - Queue grouping, fix-session flows, and page-specific prioritization should stay outside as application/UI adapters.

## Health Scoring Rules
- **Locations**
  - Simple calculator: `/PoTool.Core/Health/BacklogHealthCalculator.cs`
  - Iteration health handlers: `/PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs`, `/GetMultiIterationBacklogHealthQueryHandler.cs`
  - Health controller: `/PoTool.Api/Controllers/HealthCalculationController.cs`
  - Discovery doc: `/docs/architecture/repository-domain-discovery.md`
- **What exists today**
  - `BacklogHealthCalculator` computes a numeric score from total items, missing effort, in-progress-without-effort, parent progress issues, and blocked items.
  - The main health handlers do not use that calculator. They separately derive:
    - structural integrity counts from hierarchical validation
    - refinement-blocker and refinement-needed counts
    - blocked-item counts via state-name string matching
    - in-progress-at-end counts via state-name string matching
  - This produces a health dashboard slice, but not one single canonical health formula.
- **Duplication**
  - `GetBacklogHealthQueryHandler` and `GetMultiIterationBacklogHealthQueryHandler` duplicate most of the per-iteration aggregation logic.
  - The simple calculator exists beside, not inside, those handlers, so health scoring is split across two models.
  - Blocked/in-progress heuristics are handler-owned and not reused by the calculator.
- **Extraction readiness**
  - **Low to moderate** as a standalone health-scoring package.
  - The idea is domain-like, but the current implementation mixes canonical validation counts with ad hoc dashboard heuristics and legacy compatibility fields.
  - This should follow the validation/readiness extraction, not lead it.
- **Recommended ownership**
  - Structural validation counts should be owned by the backlog-quality domain slice.
  - Iteration health dashboards and trend windows should remain API/application concerns until one canonical health-scoring contract is agreed.

## Conclusion
Backlog quality **should** become the next domain slice after the current CDC, but the slice should be defined narrowly as **backlog validation + backlog readiness**, not as every current health/triage screen.

Why this should be next:
- the core rule engine already exists in `/PoTool.Core/WorkItems/Validators`
- the readiness scoring model already exists in `/PoTool.Core/Health/BacklogStateComputationService.cs`
- both areas are specification-backed, deterministic, and tested
- they are reused across backlog overview, validation queue/triage, and sync-time computation

Why the extraction should stay focused:
- rule categorization is still duplicated outside the engine
- `RC-2` / `MissingEffort` naming is inconsistent at the boundaries
- health scoring is not yet one canonical model
- impact analysis and queue/fix-session orchestration are still application-layer concerns

**Recommendation:** make backlog quality the next domain package / CDC v2 candidate, with initial ownership over:
- structural integrity rules
- refinement readiness rules
- implementation-readiness rules
- backlog readiness scoring and ownership states

Keep outside the first extraction:
- UI pages and components
- queue/triage/fix-session orchestration
- multi-iteration health dashboards until their heuristics are canonicalized

## Backlog Quality CDC Progress — Domain Models and Rule Catalog

- **Models added in `PoTool.Core.Domain/BacklogQuality`:**
  - `Models/BacklogGraph.cs`
  - `Models/WorkItemSnapshot.cs`
  - `Models/ReadinessScore.cs`
  - `Models/ValidationOutputs.cs` (`ValidationRuleResult`, `BacklogIntegrityFinding`, `RefinementReadinessState`, `ImplementationReadinessState`, `BacklogValidationResult`)
  - `Models/ReadinessOwnerState.cs`
- **Rule catalog created:**
  - `Rules/RuleFamily.cs`
  - `Rules/RuleMetadata.cs`
  - `Rules/IBacklogQualityRule.cs`
  - `Rules/PlaceholderBacklogQualityRule.cs`
  - `Services/RuleCatalog.cs`
- **Tests added:**
  - `PoTool.Tests.Unit/Services/BacklogQualityDomainModelsTests.cs`
  - coverage includes backlog-graph construction/invariants, readiness-score semantics, rule metadata shape, and stable manual rule registration order

## Backlog Quality CDC Progress — Canonical Rules Extracted

- **Rules extracted into executable canonical rule objects in `PoTool.Core.Domain/BacklogQuality/Rules`:**
  - Structural Integrity: `SI-1`, `SI-2`, `SI-3`
  - Refinement Readiness: `RR-1`, `RR-2`, `RR-3`
  - Implementation Readiness: `RC-1`, `RC-2`, `RC-3`
- **RC-2 ownership normalized:**
  - canonical rule ID remains `RC-2`
  - canonical semantic tag remains `MissingEffort`
  - canonical applicability is limited to PBI work item types; adapter/UI aliases such as `EFF` remain outside the CDC domain rule definition
- **Tests added/updated:**
  - `PoTool.Tests.Unit/Services/BacklogQualityCanonicalRulesTests.cs`
  - coverage includes per-rule firing behavior, canonical metadata/family assertions, deterministic family ordering, and explicit `RC-2` identity preservation

## Backlog Quality CDC Progress — BacklogValidationService Implemented

- **Validation service added in `PoTool.Core.Domain/BacklogQuality/Services/BacklogValidationService.cs`:**
  - executes Structural Integrity, Refinement Readiness, and Implementation Readiness in fixed canonical order
  - aggregates reported findings, specialized structural findings, refinement states, and implementation states into `BacklogValidationResult`
  - preserves canonical rule metadata by consuming rule outputs from the domain `RuleCatalog`
- **Suppression semantics implemented:**
  - refinement-readiness blockers suppress implementation-readiness findings for the blocked scope and descendants in the reported findings list
  - implementation-readiness states still retain their own blocking findings so readiness computation remains independent from reporting suppression
  - structural-integrity findings remain reported even when refinement blockers suppress lower implementation findings
- **Tests added:**
  - `PoTool.Tests.Unit/Services/BacklogValidationServiceTests.cs`
  - coverage includes canonical family execution order, suppression beneath refinement blockers, structural reporting, aggregate output shape, and deterministic mixed-tree behavior

## Backlog Quality CDC Progress — Readiness Services Implemented

- **Readiness scoring implemented in `PoTool.Core.Domain/BacklogQuality`:**
  - `Models/BacklogReadinessScore.cs`
  - `Services/BacklogReadinessService.cs`
  - scores are computed directly from canonical graph semantics for active Epics, Features, and PBIs
  - removed descendants are excluded, done descendants contribute `100`, and child averages use midpoint-to-even rounding
  - Feature owner-state semantics remain canonical: `PO`, `Team`, `Ready`
- **Binary readiness implemented in `PoTool.Core.Domain/BacklogQuality/Services/ImplementationReadinessService.cs`:**
  - derives `ImplementationReadinessState` from canonical readiness scoring and direct rule semantics
  - preserves binary ready/not-ready interpretation without introducing a competing rule system
  - `BacklogValidationService` now delegates readiness-state derivation to the canonical implementation-readiness service
- **Tests added/updated:**
  - `PoTool.Tests.Unit/Services/BacklogReadinessServiceTests.cs`
  - `PoTool.Tests.Unit/Services/ImplementationReadinessServiceTests.cs`
  - `PoTool.Tests.Unit/Services/BacklogValidationServiceTests.cs`
  - coverage includes PBI/Feature/Epic score rules, done descendant contribution, removed descendant exclusion, midpoint-to-even rounding, owner-state semantics, binary readiness derivation, and the contradiction case where an Epic score is blocked while mature descendants still retain their own scores

## Backlog Quality CDC Progress — Analyzer Introduced

- **Analyzer added in `PoTool.Core.Domain/BacklogQuality/Services/BacklogQualityAnalyzer.cs`:**
  - provides one facade entrypoint over `BacklogValidationService`, `BacklogReadinessService`, and `ImplementationReadinessService`
  - returns one domain analysis result that exposes validation findings, integrity findings, refinement states, implementation states, and readiness scores together
- **Existing Core consumers integrated or wrapped:**
  - `PoTool.Core/Health/BacklogStateComputationService.cs` now delegates score computation through the canonical backlog-quality analyzer while preserving its existing return types
  - `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs` now uses the analyzer-backed path when state classification is available, while keeping legacy wrapper behavior such as tree-level suppression and legacy epic/feature `RC-2` reporting intact
- **Tests added/updated:**
  - `PoTool.Tests.Unit/Services/BacklogQualityAnalyzerTests.cs`
  - `PoTool.Tests.Unit/HierarchicalWorkItemValidatorTests.cs`
  - coverage includes coherent combined analyzer output, readiness/validation alignment, and preservation of adapted Core validator behavior

## Re-Audit Results — Backlog Quality CDC v2

- **What is now in CDC v2**
  - `PoTool.Core.Domain/BacklogQuality` now owns the canonical backlog-quality slice:
    - domain models: `BacklogGraph`, `WorkItemSnapshot`, `ReadinessScore`, `BacklogReadinessScore`, `ReadinessOwnerState`, and validation output records
    - canonical rule metadata and families via `RuleMetadata`, `RuleFamily`, `RuleResponsibleParty`, `RuleFindingClass`, `IBacklogQualityRule`, and `RuleCatalog`
    - executable canonical rules for structural integrity (`SI-1..SI-3`), refinement readiness (`RR-1..RR-3`), and implementation readiness (`RC-1..RC-3`) in `Rules/CanonicalBacklogQualityRules.cs`
    - scoring logic in `Services/BacklogReadinessService.cs`
    - binary implementation-readiness derivation in `Services/ImplementationReadinessService.cs`
    - suppression-aware validation orchestration in `Services/BacklogValidationService.cs`
    - one analyzer facade in `Services/BacklogQualityAnalyzer.cs`
  - live implementation ownership now matches the intended CDC v2 scope:
    - rule logic lives in `PoTool.Core.Domain`
    - rule metadata is domain-owned
    - scoring logic is domain-owned
    - suppression semantics are service-owned inside the validation service

- **What remains outside the CDC**
  - adapters and legacy wrappers remain outside the domain:
    - `PoTool.Core/BacklogQuality/BacklogQualityDomainAdapter.cs` maps domain outputs into legacy `PoTool.Shared` contracts
    - `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs` preserves legacy wrapper behavior when routing analyzer results back into old validation result shapes
    - `PoTool.Core/Health/BacklogStateComputationService.cs` preserves existing Core return types while delegating canonical scoring to the analyzer
  - API/application consumers remain outside the domain:
    - `PoTool.Api/Handlers/WorkItems/GetValidationTriageSummaryQueryHandler.cs`
    - `PoTool.Api/Handlers/WorkItems/GetValidationQueueQueryHandler.cs`
    - `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs`
  - UI aliases remain outside domain ownership:
    - `EFF` is still a queue/triage/UI category alias for `RC-2`
    - client display metadata remains in `PoTool.Client/Models/ValidationCategoryMeta.cs`
  - dashboard heuristics remain outside the CDC:
    - `PoTool.Core/Health/BacklogHealthCalculator.cs` and handler-owned health heuristics are still separate from the canonical backlog-quality slice

- **Remaining cleanup**
  - **Blocking:** none found in the current CDC v2 ownership scan
  - **Minor cleanup:**
    - `RC-2` / `EFF` aliasing is still hardcoded in queue and triage adapters rather than being centralized at one adapter seam
    - `PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs` still carries a local rule/category map plus message-based fallback inference
    - `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs` still reinterprets findings as generic `"ParentProgress"` impact records instead of preserving canonical rule identity
    - legacy validator rule classes remain present as fallback/compatibility infrastructure even though the canonical rules now live in `PoTool.Core.Domain`
  - **None:**
    - no duplicated canonical rule execution was found outside `PoTool.Core.Domain`
    - no duplicated scoring formulas were found outside the CDC
    - no adapter or handler was found re-owning canonical rule metadata as a source of truth

- **Test validation**
  - focused re-audit validation passed after restore/build:
    - `dotnet build PoTool.sln --no-restore`
    - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --no-build --filter 'FullyQualifiedName~BacklogQualityAnalyzerTests|FullyQualifiedName~BacklogValidationServiceTests|FullyQualifiedName~BacklogReadinessServiceTests|FullyQualifiedName~ImplementationReadinessServiceTests|FullyQualifiedName~BacklogQualityCanonicalRulesTests|FullyQualifiedName~BacklogQualityDomainModelsTests|FullyQualifiedName~HierarchicalWorkItemValidatorTests|FullyQualifiedName~BacklogStateComputationServiceTests' --verbosity minimal`
  - confirmed coverage includes:
    - rule execution
    - suppression behavior
    - scoring formulas
    - binary readiness derivation
    - analyzer facade behavior

- **Readiness verdict**
  - **Backlog Quality CDC ready after minor cleanup**
  - rationale:
    - the canonical domain package is already coherent and domain-owned
    - remaining issues are adapter/legacy cleanup items rather than CDC ownership blockers
    - extraction/integration can proceed, but the `RC-2` / `EFF` alias seam and remaining legacy adapter interpretations should be cleaned up as part of that extraction work

## Backlog Quality CDC Cleanup — Adapter Normalization

- `RC-2` / `EFF` aliasing is centralized in shared validation rule metadata via `PoTool.Shared/WorkItems/ValidationRuleCatalog.cs`.
  - adapters and UI consumers now resolve the UI category from one rule-metadata seam instead of duplicating `RC-2` checks
  - canonical rule identity remains `RC-2`; only the UI category alias becomes `EFF`
- local rule inference has been removed from adapter consumers:
  - `PoTool.Core/WorkItems/Filtering/WorkItemFilterer.cs` now resolves categories from canonical rule metadata instead of local maps or message parsing
  - `PoTool.Client/Services/TreeBuilderService.cs` now determines highest validation category from canonical rule metadata instead of rule ID prefix parsing
  - queue and triage handlers now group rules by canonical metadata instead of `SI-` / `RR-` / `RC-` string checks
- impact analysis preserves rule identity:
  - `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs` now keeps the canonical `RuleId` visible in `ViolationType` instead of rewriting findings as generic `ParentProgress`
- legacy validator duplication was reduced by downgrading the old `WorkItemInProgressWithoutEffortValidator` fallback to an explicitly hidden compatibility-only type while retaining the analyzer-backed execution path
- canonical execution path remains unchanged:
  - `BacklogQualityAnalyzer` stays the canonical evaluator
  - `BacklogValidationService` stays the canonical rule orchestrator
