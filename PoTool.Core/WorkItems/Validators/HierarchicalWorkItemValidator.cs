using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Core.Domain.BacklogQuality.Services;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems.Validators;

/// <summary>
/// Hierarchical work item validator that enforces evaluation order and suppression rules.
/// 
/// Evaluation order:
/// 1. Structural Integrity (always evaluated, never suppressed)
/// 2. Refinement Readiness (evaluated after Structural Integrity)
/// 3. Refinement Completeness (suppressed if any Refinement Readiness violations exist)
/// </summary>
public sealed class HierarchicalWorkItemValidator : IHierarchicalWorkItemValidator
{
    private readonly IReadOnlyList<IHierarchicalValidationRule> _rules;
    private readonly IWorkItemStateClassificationService? _stateClassificationService;
    private readonly BacklogQualityAnalyzer? _backlogQualityAnalyzer;
    private readonly RuleCatalog _ruleCatalog = new();

    /// <summary>
    /// Creates a new hierarchical validator with the specified rules.
    /// </summary>
    /// <param name="rules">All validation rules to apply.</param>
    public HierarchicalWorkItemValidator(
        IEnumerable<IHierarchicalValidationRule> rules,
        IWorkItemStateClassificationService? stateClassificationService = null,
        BacklogQualityAnalyzer? backlogQualityAnalyzer = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
        _stateClassificationService = stateClassificationService;
        _backlogQualityAnalyzer = backlogQualityAnalyzer ?? (stateClassificationService is null ? null : new BacklogQualityAnalyzer());
    }

    /// <inheritdoc />
    public IReadOnlyList<HierarchicalValidationResult> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();
        if (_stateClassificationService is not null && _backlogQualityAnalyzer is not null)
        {
            return ValidateWithAnalyzer(itemsList);
        }

        var results = new List<HierarchicalValidationResult>();

        // Find all root items (items with no parent or parent not in the dataset)
        var allTfsIds = new HashSet<int>(itemsList.Select(w => w.TfsId));
        var rootItems = itemsList.Where(w => !w.ParentTfsId.HasValue || !allTfsIds.Contains(w.ParentTfsId.Value));

        foreach (var root in rootItems)
        {
            var treeResult = ValidateTree(root.TfsId, itemsList);
            results.Add(treeResult);
        }

        return results;
    }

    /// <inheritdoc />
    public HierarchicalValidationResult ValidateTree(int rootWorkItemId, IEnumerable<WorkItemDto> workItems)
    {
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();
        if (_stateClassificationService is not null && _backlogQualityAnalyzer is not null)
        {
            var graph = BacklogQualityDomainAdapter.CreateGraph(
                itemsList,
                item => BacklogQualityDomainAdapter.Classify(_stateClassificationService, item));
            var analysis = _backlogQualityAnalyzer.Analyze(graph);
            return CreateAnalyzerResult(rootWorkItemId, itemsList, graph, analysis);
        }

        // Get all items in this tree
        var treeItems = GetTreeItems(rootWorkItemId, itemsList);

        // Phase 1: Evaluate Structural Integrity rules (always)
        var structuralIntegrityRules = _rules
            .Where(r => r.Category == ValidationCategory.StructuralIntegrity)
            .ToList();
        var backlogHealthProblems = EvaluateRules(structuralIntegrityRules, treeItems);

        // Phase 2: Evaluate Refinement Readiness rules
        var refinementReadinessRules = _rules
            .Where(r => r.Category == ValidationCategory.RefinementReadiness)
            .ToList();
        var refinementBlockers = EvaluateRules(refinementReadinessRules, treeItems);

        // Phase 3: Evaluate Refinement Completeness rules (suppressed if refinement blockers exist)
        var refinementCompletenessRules = _rules
            .Where(r => r.Category == ValidationCategory.RefinementCompleteness)
            .ToList();

        List<ValidationRuleResult> incompleteRefinementIssues;
        bool wasSuppressed;

        if (refinementBlockers.Count > 0)
        {
            // Suppression: Do not evaluate PBI-level validation if refinement blockers exist
            incompleteRefinementIssues = new List<ValidationRuleResult>();
            wasSuppressed = true;
        }
        else
        {
            incompleteRefinementIssues = EvaluateRules(refinementCompletenessRules, treeItems);
            wasSuppressed = false;
        }

        // Phase 4: Evaluate MissingEffort rules (always evaluated, never suppressed)
        var missingEffortRules = _rules
            .Where(r => r.Category == ValidationCategory.MissingEffort)
            .ToList();
        var missingEffortIssues = EvaluateRules(missingEffortRules, treeItems);

        return new HierarchicalValidationResult(
            rootWorkItemId,
            backlogHealthProblems,
            refinementBlockers,
            incompleteRefinementIssues,
            wasSuppressed,
            missingEffortIssues
        );
    }

    private IReadOnlyList<HierarchicalValidationResult> ValidateWithAnalyzer(List<WorkItemDto> itemsList)
    {
        var graph = BacklogQualityDomainAdapter.CreateGraph(
            itemsList,
            item => BacklogQualityDomainAdapter.Classify(_stateClassificationService!, item));
        var analysis = _backlogQualityAnalyzer!.Analyze(graph);

        return graph.RootItems
            .Select(root => CreateAnalyzerResult(root.WorkItemId, itemsList, graph, analysis))
            .ToArray();
    }

    private HierarchicalValidationResult CreateAnalyzerResult(
        int rootWorkItemId,
        IReadOnlyList<WorkItemDto> itemsList,
        PoTool.Core.Domain.BacklogQuality.Models.BacklogGraph graph,
        PoTool.Core.Domain.BacklogQuality.Models.BacklogQualityAnalysisResult analysis)
    {
        var treeIds = CollectTreeIds(graph, rootWorkItemId);
        var backlogHealthProblems = analysis.IntegrityFindings
            .Where(finding => treeIds.Contains(finding.WorkItemId))
            .Select(finding => BacklogQualityDomainAdapter.ToLegacyValidationResult(
                finding,
                finding.ConflictingDescendantIds.Count == 0
                    ? null
                    : $"Conflicting descendants: {string.Join(", ", finding.ConflictingDescendantIds.Select(id => $"#{id}"))}"))
            .ToArray();
        var refinementBlockers = analysis.Findings
            .Where(finding => finding.Rule.Family == RuleFamily.RefinementReadiness)
            .Where(finding => treeIds.Contains(finding.WorkItemId))
            .Select(finding => BacklogQualityDomainAdapter.ToLegacyValidationResult(finding))
            .ToArray();

        var wasSuppressed = refinementBlockers.Length > 0;
        var incompleteRefinementIssues = wasSuppressed
            ? Array.Empty<ValidationRuleResult>()
            : analysis.Findings
                .Where(finding => finding.Rule.Family == RuleFamily.ImplementationReadiness)
                .Where(finding => !string.Equals(finding.Rule.SemanticTag, "MissingEffort", StringComparison.Ordinal))
                .Where(finding => treeIds.Contains(finding.WorkItemId))
                .Select(finding => BacklogQualityDomainAdapter.ToLegacyValidationResult(finding))
                .ToArray();

        var missingEffortIssues = BuildLegacyMissingEffortIssues(itemsList, treeIds, analysis);

        return new HierarchicalValidationResult(
            rootWorkItemId,
            backlogHealthProblems,
            refinementBlockers,
            incompleteRefinementIssues,
            wasSuppressed,
            missingEffortIssues);
    }

    private IReadOnlyList<ValidationRuleResult> BuildLegacyMissingEffortIssues(
        IReadOnlyList<WorkItemDto> itemsList,
        IReadOnlySet<int> treeIds,
        PoTool.Core.Domain.BacklogQuality.Models.BacklogQualityAnalysisResult analysis)
    {
        var results = new List<ValidationRuleResult>();
        var seenWorkItemIds = new HashSet<int>();

        foreach (var finding in analysis.ImplementationStates
                     .Where(state => treeIds.Contains(state.WorkItemId) && state.HasMissingEffort)
                     .SelectMany(state => state.BlockingFindings)
                     .Where(finding => string.Equals(finding.Rule.SemanticTag, "MissingEffort", StringComparison.Ordinal))
                     .OrderBy(finding => finding.WorkItemId))
        {
            if (seenWorkItemIds.Add(finding.WorkItemId))
            {
                results.Add(BacklogQualityDomainAdapter.ToLegacyValidationResult(finding));
            }
        }

        if (!_ruleCatalog.TryGetRule("RC-2", out var missingEffortRule) || missingEffortRule is null)
        {
            throw new InvalidOperationException("Canonical backlog-quality rule 'RC-2' is not registered.");
        }

        foreach (var item in itemsList
                     .Where(item => treeIds.Contains(item.TfsId))
                     .Where(item => string.Equals(item.Type, WorkItemType.Epic, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(item.Type, WorkItemType.Feature, StringComparison.OrdinalIgnoreCase))
                     .Where(item => !IsFinished(item))
                     .Where(item => item.Effort is not > 0)
                     .OrderBy(item => item.TfsId))
        {
            if (seenWorkItemIds.Add(item.TfsId))
            {
                results.Add(new ValidationRuleResult(
                    BacklogQualityDomainAdapter.CreateLegacyRule(missingEffortRule.Metadata, missingEffortRule.Metadata.Description),
                    item.TfsId,
                    IsViolated: true));
            }
        }

        return results;
    }

    private static IReadOnlySet<int> CollectTreeIds(
        PoTool.Core.Domain.BacklogQuality.Models.BacklogGraph graph,
        int rootWorkItemId)
    {
        var treeIds = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootWorkItemId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!treeIds.Add(currentId))
            {
                continue;
            }

            foreach (var child in graph.GetChildren(currentId))
            {
                queue.Enqueue(child.WorkItemId);
            }
        }

        return treeIds;
    }

    private bool IsFinished(WorkItemDto item)
    {
        return _stateClassificationService is not null &&
               BacklogQualityDomainAdapter.Classify(_stateClassificationService, item) is
                   PoTool.Core.Domain.Models.StateClassification.Done or PoTool.Core.Domain.Models.StateClassification.Removed;
    }

    /// <summary>
    /// Gets all work items in a tree rooted at the specified item.
    /// </summary>
    private static List<WorkItemDto> GetTreeItems(int rootId, List<WorkItemDto> allItems)
    {
        var result = new List<WorkItemDto>();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            if (!visited.Add(currentId))
            {
                continue;
            }

            var item = allItems.FirstOrDefault(w => w.TfsId == currentId);
            if (item != null)
            {
                result.Add(item);

                // Add children to queue
                var children = allItems.Where(w => w.ParentTfsId == currentId);
                foreach (var child in children)
                {
                    queue.Enqueue(child.TfsId);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Evaluates a set of rules against work items and aggregates results.
    /// </summary>
    private static List<ValidationRuleResult> EvaluateRules(
        IEnumerable<IHierarchicalValidationRule> rules,
        List<WorkItemDto> workItems)
    {
        var results = new List<ValidationRuleResult>();

        foreach (var rule in rules)
        {
            var ruleResults = rule.Evaluate(workItems);
            results.AddRange(ruleResults);
        }

        return results;
    }
}
