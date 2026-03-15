using PoTool.Core.Domain.BacklogQuality.Models;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Core.Domain.BacklogQuality.Rules;

internal static class BacklogQualityRuleThresholds
{
    public const int MinimumDescriptionLength = 10;
}

/// <summary>
/// Shared executable behavior for canonical backlog-quality rules.
/// </summary>
public abstract class BacklogQualityRuleBase : IBacklogQualityRule
{
    private readonly HashSet<string> _applicableWorkItemTypes;

    protected BacklogQualityRuleBase(RuleMetadata metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _applicableWorkItemTypes = new HashSet<string>(metadata.ApplicableWorkItemTypes, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public RuleMetadata Metadata { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph);

    protected IEnumerable<WorkItemSnapshot> GetApplicableItems(BacklogGraph backlogGraph)
    {
        ArgumentNullException.ThrowIfNull(backlogGraph);

        return backlogGraph.Items
            .Where(item => _applicableWorkItemTypes.Contains(item.WorkItemType))
            .OrderBy(item => item.WorkItemId);
    }

    protected static bool IsActive(WorkItemSnapshot workItem)
    {
        return workItem.StateClassification is not StateClassification.Done and not StateClassification.Removed;
    }

    protected static bool HasDescription(WorkItemSnapshot workItem)
    {
        return !string.IsNullOrWhiteSpace(workItem.Description);
    }

    protected static bool HasMinimumDescription(WorkItemSnapshot workItem)
    {
        return !string.IsNullOrWhiteSpace(workItem.Description) &&
               workItem.Description.Trim().Length >= BacklogQualityRuleThresholds.MinimumDescriptionLength;
    }

    protected static bool HasPositiveEffort(WorkItemSnapshot workItem)
    {
        return workItem.Effort is > 0;
    }

    protected static IReadOnlyList<WorkItemSnapshot> GetDescendants(BacklogGraph backlogGraph, int workItemId)
    {
        var descendants = new List<WorkItemSnapshot>();
        var queue = new Queue<WorkItemSnapshot>(backlogGraph.GetChildren(workItemId));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            descendants.Add(current);

            foreach (var child in backlogGraph.GetChildren(current.WorkItemId))
            {
                queue.Enqueue(child);
            }
        }

        return descendants;
    }

    protected static IReadOnlyList<WorkItemSnapshot> GetChildrenOfTypes(
        BacklogGraph backlogGraph,
        int workItemId,
        IReadOnlyList<string> workItemTypes)
    {
        var allowedTypes = new HashSet<string>(workItemTypes, StringComparer.OrdinalIgnoreCase);

        return backlogGraph.GetChildren(workItemId)
            .Where(child => allowedTypes.Contains(child.WorkItemType))
            .OrderBy(child => child.WorkItemId)
            .ToArray();
    }

    protected ValidationRuleResult CreateFinding(WorkItemSnapshot workItem, string? message = null)
    {
        return new ValidationRuleResult(
            Metadata,
            workItem.WorkItemId,
            workItem.WorkItemType,
            message ?? Metadata.Description);
    }

    protected BacklogIntegrityFinding CreateIntegrityFinding(
        WorkItemSnapshot workItem,
        IReadOnlyList<int> conflictingDescendantIds,
        string scopeDescription)
    {
        var orderedIds = conflictingDescendantIds.OrderBy(id => id).ToArray();
        var suffix = orderedIds.Length == 0
            ? string.Empty
            : $" Conflicting descendants ({scopeDescription}): {string.Join(", ", orderedIds.Select(id => $"#{id}"))}.";

        return new BacklogIntegrityFinding(
            Metadata,
            workItem.WorkItemId,
            workItem.WorkItemType,
            Metadata.Description + suffix,
            orderedIds);
    }
}

public sealed class DoneParentWithUnfinishedDescendantsRule : BacklogQualityRuleBase
{
    public DoneParentWithUnfinishedDescendantsRule()
        : base(new RuleMetadata(
            "SI-1",
            RuleFamily.StructuralIntegrity,
            "DoneParentWithUnfinishedDescendants",
            "Parent in Done with any descendant not in Done or Removed is invalid.",
            RuleResponsibleParty.Process,
            RuleFindingClass.StructuralWarning,
            BacklogWorkItemTypes.StructuralIntegrityTypes))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(item => item.StateClassification == StateClassification.Done)
            .Select(item =>
            {
                var conflictingIds = GetDescendants(backlogGraph, item.WorkItemId)
                    .Where(descendant => descendant.StateClassification is not StateClassification.Done and not StateClassification.Removed)
                    .Select(descendant => descendant.WorkItemId)
                    .ToArray();

                return conflictingIds.Length == 0
                    ? null
                    : CreateIntegrityFinding(item, conflictingIds, "not in Done or Removed");
            })
            .Where(finding => finding is not null)
            .Cast<ValidationRuleResult>()
            .ToArray();
    }
}

public sealed class RemovedParentWithUnfinishedDescendantsRule : BacklogQualityRuleBase
{
    public RemovedParentWithUnfinishedDescendantsRule()
        : base(new RuleMetadata(
            "SI-2",
            RuleFamily.StructuralIntegrity,
            "RemovedParentWithUnfinishedDescendants",
            "Parent in Removed with any descendant not in Done or Removed is invalid.",
            RuleResponsibleParty.Process,
            RuleFindingClass.StructuralWarning,
            BacklogWorkItemTypes.StructuralIntegrityTypes))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(item => item.StateClassification == StateClassification.Removed)
            .Select(item =>
            {
                var conflictingIds = GetDescendants(backlogGraph, item.WorkItemId)
                    .Where(descendant => descendant.StateClassification is not StateClassification.Done and not StateClassification.Removed)
                    .Select(descendant => descendant.WorkItemId)
                    .ToArray();

                return conflictingIds.Length == 0
                    ? null
                    : CreateIntegrityFinding(item, conflictingIds, "not in Done or Removed");
            })
            .Where(finding => finding is not null)
            .Cast<ValidationRuleResult>()
            .ToArray();
    }
}

public sealed class NewParentWithStartedDescendantsRule : BacklogQualityRuleBase
{
    public NewParentWithStartedDescendantsRule()
        : base(new RuleMetadata(
            "SI-3",
            RuleFamily.StructuralIntegrity,
            "NewParentWithStartedDescendants",
            "Parent in New with any descendant in progress or done is invalid.",
            RuleResponsibleParty.Process,
            RuleFindingClass.StructuralWarning,
            BacklogWorkItemTypes.StructuralIntegrityTypes))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(item => item.StateClassification == StateClassification.New)
            .Select(item =>
            {
                var conflictingIds = GetDescendants(backlogGraph, item.WorkItemId)
                    .Where(descendant => descendant.StateClassification is StateClassification.InProgress or StateClassification.Done)
                    .Select(descendant => descendant.WorkItemId)
                    .ToArray();

                return conflictingIds.Length == 0
                    ? null
                    : CreateIntegrityFinding(item, conflictingIds, "in progress or done");
            })
            .Where(finding => finding is not null)
            .Cast<ValidationRuleResult>()
            .ToArray();
    }
}

public sealed class EpicDescriptionRule : BacklogQualityRuleBase
{
    public EpicDescriptionRule()
        : base(new RuleMetadata(
            "RR-1",
            RuleFamily.RefinementReadiness,
            "MissingDescription",
            "Epic description must be present and at least 10 characters.",
            RuleResponsibleParty.ProductOwner,
            RuleFindingClass.RefinementBlocker,
            [BacklogWorkItemTypes.Epic]))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => !HasMinimumDescription(item))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}

public sealed class FeatureDescriptionRule : BacklogQualityRuleBase
{
    public FeatureDescriptionRule()
        : base(new RuleMetadata(
            "RR-2",
            RuleFamily.RefinementReadiness,
            "MissingDescription",
            "Feature description must be present and at least 10 characters.",
            RuleResponsibleParty.ProductOwner,
            RuleFindingClass.RefinementBlocker,
            [BacklogWorkItemTypes.Feature]))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => !HasMinimumDescription(item))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}

public sealed class EpicMissingChildrenRule : BacklogQualityRuleBase
{
    public EpicMissingChildrenRule()
        : base(new RuleMetadata(
            "RR-3",
            RuleFamily.RefinementReadiness,
            "MissingChildren",
            "Epic must have at least one active Feature child.",
            RuleResponsibleParty.ProductOwner,
            RuleFindingClass.RefinementBlocker,
            [BacklogWorkItemTypes.Epic]))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => GetChildrenOfTypes(backlogGraph, item.WorkItemId, [BacklogWorkItemTypes.Feature]).All(child => !IsActive(child)))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}

public sealed class PbiDescriptionRule : BacklogQualityRuleBase
{
    public PbiDescriptionRule()
        : base(new RuleMetadata(
            "RC-1",
            RuleFamily.ImplementationReadiness,
            "MissingDescription",
            "PBI description must be present.",
            RuleResponsibleParty.Team,
            RuleFindingClass.ImplementationBlocker,
            BacklogWorkItemTypes.PbiTypes))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => !HasDescription(item))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}

public sealed class PbiMissingEffortRule : BacklogQualityRuleBase
{
    public PbiMissingEffortRule()
        : base(new RuleMetadata(
            "RC-2",
            RuleFamily.ImplementationReadiness,
            "MissingEffort",
            "PBI effort must be present and greater than zero.",
            RuleResponsibleParty.Team,
            RuleFindingClass.ImplementationBlocker,
            BacklogWorkItemTypes.PbiTypes))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => !HasPositiveEffort(item))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}

public sealed class FeatureMissingChildrenRule : BacklogQualityRuleBase
{
    public FeatureMissingChildrenRule()
        : base(new RuleMetadata(
            "RC-3",
            RuleFamily.ImplementationReadiness,
            "MissingChildren",
            "Feature must have at least one active PBI child.",
            RuleResponsibleParty.Team,
            RuleFindingClass.ImplementationBlocker,
            [BacklogWorkItemTypes.Feature]))
    {
    }

    public override IReadOnlyList<ValidationRuleResult> Evaluate(BacklogGraph backlogGraph)
    {
        return GetApplicableItems(backlogGraph)
            .Where(IsActive)
            .Where(item => GetChildrenOfTypes(backlogGraph, item.WorkItemId, BacklogWorkItemTypes.PbiTypes).All(child => !IsActive(child)))
            .Select(item => CreateFinding(item))
            .ToArray();
    }
}
