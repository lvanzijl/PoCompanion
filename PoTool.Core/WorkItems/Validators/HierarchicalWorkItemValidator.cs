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

    /// <summary>
    /// Creates a new hierarchical validator with the specified rules.
    /// </summary>
    /// <param name="rules">All validation rules to apply.</param>
    public HierarchicalWorkItemValidator(IEnumerable<IHierarchicalValidationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<HierarchicalValidationResult> ValidateWorkItems(IEnumerable<WorkItemDto> workItems)
    {
        var itemsList = workItems as List<WorkItemDto> ?? workItems.ToList();
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

        return new HierarchicalValidationResult(
            rootWorkItemId,
            backlogHealthProblems,
            refinementBlockers,
            incompleteRefinementIssues,
            wasSuppressed
        );
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
