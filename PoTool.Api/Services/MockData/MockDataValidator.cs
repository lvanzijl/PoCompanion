using PoTool.Shared.WorkItems;
using PoTool.Shared.PullRequests;

using PoTool.Core.WorkItems;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Validates mock data against the expected operational realism constraints.
/// </summary>
public class MockDataValidator
{
    private static readonly string[] GoalStates = ["Proposed", "Active", "Completed", "Removed"];
    private static readonly string[] EpicStates = ["New", "Active", "Resolved", "Closed", "Removed"];
    private static readonly string[] BacklogStates = ["New", "Approved", "Committed", "Done", "Removed"];
    private static readonly string[] TaskStates = ["To Do", "In Progress", "Done", "Removed"];
    private static readonly HashSet<int> ValidStoryPoints = [1, 2, 3, 5, 8, 13, 21];

    /// <summary>
    /// Validates work item hierarchy and generates a validation report.
    /// </summary>
    public ValidationReport ValidateWorkItems(List<WorkItemDto> workItems)
    {
        var report = new ValidationReport
        {
            TotalWorkItems = workItems.Count,
            GoalCount = workItems.Count(w => w.Type == WorkItemType.Goal),
            ObjectiveCount = workItems.Count(w => w.Type == WorkItemType.Objective),
            EpicCount = workItems.Count(w => w.Type == WorkItemType.Epic),
            FeatureCount = workItems.Count(w => w.Type == WorkItemType.Feature),
            PbiCount = workItems.Count(w => w.Type == WorkItemType.Pbi),
            BugCount = workItems.Count(w => w.Type == WorkItemType.Bug),
            TaskCount = workItems.Count(w => w.Type == WorkItemType.Task)
        };

        ValidateQuantities(report);
        ValidateHierarchyIntegrity(workItems, report);
        ValidateAreaPathConsistency(workItems, report);
        ValidateIterationPaths(workItems, report);
        ValidateStates(workItems, report);
        ValidateEstimation(workItems, report);
        ValidateBacklogQualityDistribution(workItems, report);
        ValidateBattleshipTheme(workItems, report);

        return report;
    }

    /// <summary>
    /// Validates dependencies against rules.
    /// </summary>
    public void ValidateDependencies(List<DependencyLink> dependencies, List<WorkItemDto> workItems, ValidationReport report)
    {
        report.TotalDependencies = dependencies.Count;

        var crossTeamCount = dependencies.Count(d => d.IsCrossTeam);
        report.CrossTeamDependencyPercentage = report.TotalDependencies > 0
            ? (double)crossTeamCount / report.TotalDependencies * 100
            : 0;

        var selfDependencies = dependencies.Count(d => d.SourceWorkItemId == d.TargetWorkItemId);
        report.SelfDependencyCount = selfDependencies;

        var workItemIds = new HashSet<int>(workItems.Select(w => w.TfsId));
        var orphanedCount = dependencies.Count(d =>
            !workItemIds.Contains(d.SourceWorkItemId) || !workItemIds.Contains(d.TargetWorkItemId));
        report.OrphanedDependencyCount = orphanedCount;

        var invalidCount = selfDependencies + orphanedCount;
        report.InvalidDependencyPercentage = report.TotalDependencies > 0
            ? (double)invalidCount / report.TotalDependencies * 100
            : 0;
    }

    /// <summary>
    /// Validates pull requests against rules.
    /// </summary>
    public void ValidatePullRequests(
        List<PullRequestDto> pullRequests,
        List<PrWorkItemLink> prWorkItemLinks,
        ValidationReport report)
    {
        report.TotalPullRequests = pullRequests.Count;
        report.PullRequestVolumeValid = pullRequests.Count >= 100;

        var activeCount = pullRequests.Count(pr => pr.Status == "active");
        var completedCount = pullRequests.Count(pr => pr.Status == "completed");
        var abandonedCount = pullRequests.Count(pr => pr.Status == "abandoned");

        report.ActivePrPercentage = pullRequests.Count > 0 ? (double)activeCount / pullRequests.Count * 100 : 0;
        report.CompletedPrPercentage = pullRequests.Count > 0 ? (double)completedCount / pullRequests.Count * 100 : 0;
        report.AbandonedPrPercentage = pullRequests.Count > 0 ? (double)abandonedCount / pullRequests.Count * 100 : 0;

        var linkedPrCount = prWorkItemLinks.Select(l => l.PullRequestId).Distinct().Count();
        report.PrWithWorkItemLinksPercentage = pullRequests.Count > 0
            ? (double)linkedPrCount / pullRequests.Count * 100
            : 0;

        var prsWithoutTitle = pullRequests.Count(pr => string.IsNullOrEmpty(pr.Title));
        var prsWithoutCreator = pullRequests.Count(pr => string.IsNullOrEmpty(pr.CreatedBy));
        var prsWithoutRepository = pullRequests.Count(pr => string.IsNullOrEmpty(pr.RepositoryName));

        report.PrMetadataValid = prsWithoutTitle == 0 && prsWithoutCreator == 0 && prsWithoutRepository == 0;
    }

    private static void ValidateQuantities(ValidationReport report)
    {
        report.GoalQuantityValid = report.GoalCount == 10;
        report.ObjectiveQuantityValid = report.ObjectiveCount >= 25 && report.ObjectiveCount <= 35;
        report.EpicQuantityValid = report.EpicCount >= 80 && report.EpicCount <= 120;
        report.FeatureQuantityValid = report.FeatureCount >= 400 && report.FeatureCount <= 600;
        report.PbiQuantityValid = report.PbiCount >= 2500 && report.PbiCount <= 3500;
        report.BugQuantityValid = report.BugCount >= 800 && report.BugCount <= 1200;
        report.TaskQuantityValid = report.TaskCount >= 12000 && report.TaskCount <= 18000;
    }

    private static void ValidateHierarchyIntegrity(List<WorkItemDto> workItems, ValidationReport report)
    {
        var workItemIds = new HashSet<int>(workItems.Select(w => w.TfsId));
        var orphanedCount = 0;

        foreach (var item in workItems)
        {
            if (item.Type == WorkItemType.Goal)
            {
                if (item.ParentTfsId.HasValue)
                {
                    orphanedCount++;
                }
            }
            else if (!item.ParentTfsId.HasValue || !workItemIds.Contains(item.ParentTfsId.Value))
            {
                orphanedCount++;
            }
        }

        report.OrphanedWorkItemCount = orphanedCount;
        report.HierarchyIntegrityValid = orphanedCount == 0;
    }

    private static void ValidateAreaPathConsistency(List<WorkItemDto> workItems, ValidationReport report)
    {
        var violations = 0;
        var epics = workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        foreach (var epic in epics)
        {
            var descendants = GetDescendants(epic.TfsId, workItems);
            violations += descendants.Count(descendant => descendant.AreaPath != epic.AreaPath);
        }

        report.AreaPathViolationCount = violations;
        report.AreaPathConsistencyValid = violations == 0;
    }

    private static List<WorkItemDto> GetDescendants(int parentId, List<WorkItemDto> allItems)
    {
        var descendants = new List<WorkItemDto>();
        var directChildren = allItems.Where(w => w.ParentTfsId == parentId).ToList();

        foreach (var child in directChildren)
        {
            descendants.Add(child);
            descendants.AddRange(GetDescendants(child.TfsId, allItems));
        }

        return descendants;
    }

    private static void ValidateIterationPaths(List<WorkItemDto> workItems, ValidationReport report)
    {
        var invalidIterations = workItems.Count(w =>
            string.IsNullOrWhiteSpace(w.IterationPath) ||
            (!w.IterationPath.Contains("Backlog", StringComparison.OrdinalIgnoreCase) &&
             !w.IterationPath.Contains("Sprint", StringComparison.OrdinalIgnoreCase) &&
             !w.IterationPath.Contains("2025", StringComparison.OrdinalIgnoreCase)));

        report.InvalidIterationPathCount = invalidIterations;
        report.IterationPathValid = invalidIterations == 0;
    }

    private static void ValidateStates(List<WorkItemDto> workItems, ValidationReport report)
    {
        var invalidStates = 0;

        foreach (var item in workItems)
        {
            var validStates = item.Type switch
            {
                WorkItemType.Goal => GoalStates,
                WorkItemType.Objective => GoalStates,
                WorkItemType.Epic => EpicStates,
                WorkItemType.Feature => EpicStates,
                WorkItemType.Pbi => BacklogStates,
                WorkItemType.Bug => BacklogStates,
                WorkItemType.Task => TaskStates,
                _ => Array.Empty<string>()
            };

            if (!validStates.Contains(item.State))
            {
                invalidStates++;
            }
        }

        report.InvalidStateCount = invalidStates;
        report.StateValidityValid = invalidStates == 0;
    }

    private static void ValidateEstimation(List<WorkItemDto> workItems, ValidationReport report)
    {
        var targetItems = workItems.Where(w =>
            w.Type == WorkItemType.Epic ||
            w.Type == WorkItemType.Feature ||
            w.Type == WorkItemType.Pbi ||
            w.Type == WorkItemType.Bug).ToList();

        var pbis = workItems.Where(w => w.Type == WorkItemType.Pbi).ToList();
        var nonPbisWithStoryPoints = workItems.Count(w =>
            w.Type != WorkItemType.Pbi &&
            (w.StoryPoints.HasValue || w.BusinessValue.HasValue));

        report.EstimatedItemCount = targetItems.Count(w => w.Effort.HasValue && w.Effort.Value > 0);
        report.UnestimatedItemCount = targetItems.Count - report.EstimatedItemCount;
        report.UnestimatedPercentage = targetItems.Count > 0
            ? (double)report.UnestimatedItemCount / targetItems.Count * 100
            : 0;

        report.NonStandardStoryPointCount = pbis.Count(w =>
            w.StoryPoints.HasValue &&
            !ValidStoryPoints.Contains(w.StoryPoints.Value) &&
            !(w.StoryPoints.Value == 0 && IsClosedState(w.State)));
        report.NonPbiStoryPointCount = nonPbisWithStoryPoints;
        report.StoryPointCoveragePercentage = pbis.Count > 0
            ? (double)pbis.Count(w => w.StoryPoints.HasValue || w.BusinessValue.HasValue) / pbis.Count * 100
            : 0;

        report.StoryPointEstimationValid = report.NonStandardStoryPointCount == 0;
        report.EffortStoryPointSeparationValid = report.NonPbiStoryPointCount == 0;
    }

    private static void ValidateBacklogQualityDistribution(List<WorkItemDto> workItems, ValidationReport report)
    {
        var activeBacklogItems = workItems.Where(w =>
                (w.Type == WorkItemType.Epic || w.Type == WorkItemType.Feature || w.Type == WorkItemType.Pbi) &&
                !IsTerminalState(w.State))
            .ToList();
        var backlogItemsForStateChecks = workItems.Where(w =>
                w.Type == WorkItemType.Epic || w.Type == WorkItemType.Feature || w.Type == WorkItemType.Pbi)
            .Where(w => !string.Equals(w.State, "Removed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var invalidIds = new HashSet<int>();

        foreach (var item in activeBacklogItems)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                report.MissingDescriptionCount++;
                invalidIds.Add(item.TfsId);
            }

            if (!item.Effort.HasValue || item.Effort.Value <= 0)
            {
                report.MissingEstimateCount++;
                invalidIds.Add(item.TfsId);
            }
        }

        var features = activeBacklogItems.Where(item => item.Type == WorkItemType.Feature).ToList();
        foreach (var feature in features)
        {
            var hasPbiChildren = workItems.Any(item =>
                item.ParentTfsId == feature.TfsId &&
                item.Type == WorkItemType.Pbi);

            if (!hasPbiChildren)
            {
                report.BrokenHierarchyCount++;
                invalidIds.Add(feature.TfsId);
            }
        }

        var allById = workItems.ToDictionary(item => item.TfsId);
        var childrenByParent = workItems
            .Where(item => item.ParentTfsId.HasValue)
            .GroupBy(item => item.ParentTfsId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(child => child.TfsId).ToList());

        foreach (var item in backlogItemsForStateChecks.Where(item => IsClosedState(item.State)))
        {
            if (HasUnfinishedDescendant(item.TfsId, allById, childrenByParent))
            {
                report.InconsistentStateCount++;
            }
        }

        report.InvalidBacklogItemCount = invalidIds.Count;
        report.ValidBacklogItemCount = Math.Max(0, activeBacklogItems.Count - invalidIds.Count);
        report.InvalidBacklogItemPercentage = activeBacklogItems.Count > 0
            ? (double)invalidIds.Count / activeBacklogItems.Count * 100
            : 0;
        report.ValidBacklogItemPercentage = activeBacklogItems.Count > 0
            ? 100 - report.InvalidBacklogItemPercentage
            : 0;
        report.BacklogQualityDistributionValid = report.InvalidBacklogItemPercentage >= 5 && report.InvalidBacklogItemPercentage <= 20;
    }

    private static bool HasUnfinishedDescendant(
        int itemId,
        IReadOnlyDictionary<int, WorkItemDto> allById,
        IReadOnlyDictionary<int, List<int>> childrenByParent)
    {
        if (!childrenByParent.TryGetValue(itemId, out var children))
        {
            return false;
        }

        foreach (var childId in children)
        {
            if (!allById.TryGetValue(childId, out var child))
            {
                continue;
            }

            if (!IsTerminalState(child.State))
            {
                return true;
            }

            if (HasUnfinishedDescendant(childId, allById, childrenByParent))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateBattleshipTheme(List<WorkItemDto> workItems, ValidationReport report)
    {
        var battleshipKeywords = new[]
        {
            "battleship", "incident", "fire", "damage", "crew", "hull", "emergency",
            "sensor", "detection", "response", "safety", "control", "leakage",
            "collision", "compartment", "medical", "alert", "suppression"
        };

        var goals = workItems.Where(w => w.Type == WorkItemType.Goal).ToList();
        var themeCompliant = goals.Count(goal =>
            battleshipKeywords.Any(keyword => goal.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        report.BattleshipThemeCompliantCount = themeCompliant;
        report.BattleshipThemeValid = themeCompliant >= goals.Count * 0.7;
    }

    private static bool IsTerminalState(string state)
    {
        return IsClosedState(state) || string.Equals(state, "Removed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClosedState(string state)
    {
        return state is "Completed" or "Resolved" or "Closed" or "Done";
    }
}

/// <summary>
/// Validation report containing all metrics and validation results.
/// </summary>
public class ValidationReport
{
    public int TotalWorkItems { get; set; }
    public int GoalCount { get; set; }
    public int ObjectiveCount { get; set; }
    public int EpicCount { get; set; }
    public int FeatureCount { get; set; }
    public int PbiCount { get; set; }
    public int BugCount { get; set; }
    public int TaskCount { get; set; }

    public bool GoalQuantityValid { get; set; }
    public bool ObjectiveQuantityValid { get; set; }
    public bool EpicQuantityValid { get; set; }
    public bool FeatureQuantityValid { get; set; }
    public bool PbiQuantityValid { get; set; }
    public bool BugQuantityValid { get; set; }
    public bool TaskQuantityValid { get; set; }

    public int OrphanedWorkItemCount { get; set; }
    public bool HierarchyIntegrityValid { get; set; }

    public int AreaPathViolationCount { get; set; }
    public bool AreaPathConsistencyValid { get; set; }

    public int InvalidIterationPathCount { get; set; }
    public bool IterationPathValid { get; set; }

    public int InvalidStateCount { get; set; }
    public bool StateValidityValid { get; set; }

    public int EstimatedItemCount { get; set; }
    public int UnestimatedItemCount { get; set; }
    public double UnestimatedPercentage { get; set; }
    public int NonStandardStoryPointCount { get; set; }
    public int NonPbiStoryPointCount { get; set; }
    public double StoryPointCoveragePercentage { get; set; }
    public bool StoryPointEstimationValid { get; set; }
    public bool EffortStoryPointSeparationValid { get; set; }

    public int MissingDescriptionCount { get; set; }
    public int MissingEstimateCount { get; set; }
    public int BrokenHierarchyCount { get; set; }
    public int InconsistentStateCount { get; set; }
    public int InvalidBacklogItemCount { get; set; }
    public int ValidBacklogItemCount { get; set; }
    public double InvalidBacklogItemPercentage { get; set; }
    public double ValidBacklogItemPercentage { get; set; }
    public bool BacklogQualityDistributionValid { get; set; }

    public int TotalDependencies { get; set; }
    public double CrossTeamDependencyPercentage { get; set; }
    public int SelfDependencyCount { get; set; }
    public int OrphanedDependencyCount { get; set; }
    public double InvalidDependencyPercentage { get; set; }

    public int TotalPullRequests { get; set; }
    public bool PullRequestVolumeValid { get; set; }
    public double ActivePrPercentage { get; set; }
    public double CompletedPrPercentage { get; set; }
    public double AbandonedPrPercentage { get; set; }
    public double PrWithWorkItemLinksPercentage { get; set; }
    public bool PrMetadataValid { get; set; }

    public int BattleshipThemeCompliantCount { get; set; }
    public bool BattleshipThemeValid { get; set; }

    public bool IsValid()
    {
        return GoalQuantityValid
            && ObjectiveQuantityValid
            && EpicQuantityValid
            && FeatureQuantityValid
            && PbiQuantityValid
            && BugQuantityValid
            && TaskQuantityValid
            && HierarchyIntegrityValid
            && AreaPathConsistencyValid
            && IterationPathValid
            && StateValidityValid
            && StoryPointEstimationValid
            && EffortStoryPointSeparationValid
            && BacklogQualityDistributionValid
            && PullRequestVolumeValid
            && PrMetadataValid
            && BattleshipThemeValid;
    }

    public string GetSummary()
    {
        return $@"Mock Data Validation Report
========================================
Work Items:
  Total: {TotalWorkItems}
  Goals: {GoalCount} (Valid: {GoalQuantityValid})
  Objectives: {ObjectiveCount} (Valid: {ObjectiveQuantityValid})
  Epics: {EpicCount} (Valid: {EpicQuantityValid})
  Features: {FeatureCount} (Valid: {FeatureQuantityValid})
  PBIs: {PbiCount} (Valid: {PbiQuantityValid})
  Bugs: {BugCount} (Valid: {BugQuantityValid})
  Tasks: {TaskCount} (Valid: {TaskQuantityValid})

Data Quality:
  Hierarchy Integrity: {HierarchyIntegrityValid} (Orphaned: {OrphanedWorkItemCount})
  Area Path Consistency: {AreaPathConsistencyValid} (Violations: {AreaPathViolationCount})
  Iteration Path Valid: {IterationPathValid} (Invalid: {InvalidIterationPathCount})
  State Validity: {StateValidityValid} (Invalid: {InvalidStateCount})
  Effort Coverage: {100 - UnestimatedPercentage:F1}% estimated / {UnestimatedPercentage:F1}% missing
  Story Point Validity: {StoryPointEstimationValid} (Non-standard: {NonStandardStoryPointCount})
  Story Point Separation: {EffortStoryPointSeparationValid} (Non-PBI story-point fields: {NonPbiStoryPointCount})
  Story Point Coverage: {StoryPointCoveragePercentage:F1}% of PBIs

Backlog Quality Distribution:
  Valid Backlog Items: {ValidBacklogItemCount} ({ValidBacklogItemPercentage:F1}%)
  Invalid Backlog Items: {InvalidBacklogItemCount} ({InvalidBacklogItemPercentage:F1}%)
  Missing Descriptions: {MissingDescriptionCount}
  Missing Estimates: {MissingEstimateCount}
  Broken Hierarchy Cases: {BrokenHierarchyCount}
  Inconsistent States: {InconsistentStateCount}
  Distribution Valid: {BacklogQualityDistributionValid}

Dependencies:
  Total: {TotalDependencies}
  Cross-Team: {CrossTeamDependencyPercentage:F1}%
  Invalid: {InvalidDependencyPercentage:F1}%

Pull Requests:
  Total: {TotalPullRequests} (Valid: {PullRequestVolumeValid})
  Active: {ActivePrPercentage:F1}%
  Completed: {CompletedPrPercentage:F1}%
  Abandoned: {AbandonedPrPercentage:F1}%
  With Work Item Links: {PrWithWorkItemLinksPercentage:F1}%
  Metadata Valid: {PrMetadataValid}

Theme Validation:
  Battleship Theme: {BattleshipThemeValid} ({BattleshipThemeCompliantCount}/{GoalCount} goals compliant)

Overall Valid: {IsValid()}
";
    }
}
