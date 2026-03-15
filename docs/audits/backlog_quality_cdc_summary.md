# Backlog Quality CDC Summary

- **CDC package/location:** `PoTool.Core.Domain/BacklogQuality`

- **Models added**
  - `Models/BacklogGraph.cs`
  - `Models/WorkItemSnapshot.cs`
  - `Models/ReadinessScore.cs`
  - `Models/BacklogReadinessScore.cs`
  - `Models/ReadinessOwnerState.cs`
  - `Models/ValidationOutputs.cs`
  - `Models/BacklogWorkItemTypes.cs`

- **Rules and services added**
  - rule contracts and metadata:
    - `Rules/IBacklogQualityRule.cs`
    - `Rules/RuleMetadata.cs`
    - `Rules/RuleFamily.cs`
    - `Rules/RuleResponsibleParty.cs`
    - `Rules/RuleFindingClass.cs`
    - `Services/RuleCatalog.cs`
  - canonical rules:
    - `Rules/CanonicalBacklogQualityRules.cs`
    - owns `SI-1`, `SI-2`, `SI-3`, `RR-1`, `RR-2`, `RR-3`, `RC-1`, `RC-2`, `RC-3`
  - domain services:
    - `Services/BacklogValidationService.cs`
    - `Services/BacklogReadinessService.cs`
    - `Services/ImplementationReadinessService.cs`
    - `Services/BacklogQualityAnalyzer.cs`

- **What remains outside the domain**
  - `PoTool.Core/BacklogQuality/BacklogQualityDomainAdapter.cs` remains the bridge from canonical domain outputs to legacy Shared contracts
  - `PoTool.Core/WorkItems/Validators/HierarchicalWorkItemValidator.cs` remains a legacy-facing wrapper over analyzer-backed results
  - `PoTool.Core/Health/BacklogStateComputationService.cs` remains a compatibility adapter that preserves older score return types
  - API handlers still own queue, triage, and impact-analysis orchestration:
    - `PoTool.Api/Handlers/WorkItems/GetValidationTriageSummaryQueryHandler.cs`
    - `PoTool.Api/Handlers/WorkItems/GetValidationQueueQueryHandler.cs`
    - `PoTool.Api/Handlers/WorkItems/GetValidationImpactAnalysisQueryHandler.cs`
  - UI aliases and display metadata remain outside the domain:
    - `EFF` aliasing for `RC-2`
    - `PoTool.Client/Models/ValidationCategoryMeta.cs`
    - `PoTool.Shared/WorkItems/ValidationRuleDescriptions.cs`
  - dashboard health heuristics remain outside the canonical backlog-quality slice:
    - `PoTool.Core/Health/BacklogHealthCalculator.cs`
    - handler-owned blocked/in-progress heuristics

- **Readiness status**
  - **Backlog Quality CDC ready after minor cleanup**
  - blocking issues: none
  - minor cleanup remaining:
    - centralize `RC-2` / `EFF` alias handling at one adapter seam
    - reduce remaining local rule/category inference in filter and queue adapters
    - preserve canonical rule identity more directly in impact-analysis adapters

- **Tests validated during re-audit**
  - `BacklogQualityCanonicalRulesTests`
  - `BacklogValidationServiceTests`
  - `BacklogReadinessServiceTests`
  - `ImplementationReadinessServiceTests`
  - `BacklogQualityAnalyzerTests`
  - `BacklogQualityDomainModelsTests`
  - `HierarchicalWorkItemValidatorTests`
  - `BacklogStateComputationServiceTests`

- **Recommended next step after backlog quality CDC**
  - extract/integrate the backlog-quality CDC as the canonical current-state backlog domain package, and fold the remaining adapter-side `RC-2` / `EFF` mapping cleanup into that integration work
  - after that, audit backlog health dashboards separately so their heuristic formulas can either be canonically adopted or explicitly left outside the CDC

## Final verdict

**Backlog Quality CDC ready after minor cleanup**
