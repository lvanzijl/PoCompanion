using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Validates mock data against rules defined in mock-data-rules.md
/// </summary>
public class MockDataValidator
{
    /// <summary>
    /// Validates work item hierarchy and generates a validation report
    /// </summary>
    public ValidationReport ValidateWorkItems(List<WorkItemDto> workItems)
    {
        var report = new ValidationReport();

        // Count work items by type
        report.TotalWorkItems = workItems.Count;
        report.GoalCount = workItems.Count(w => w.Type == WorkItemType.Goal);
        report.ObjectiveCount = workItems.Count(w => w.Type == WorkItemType.Objective);
        report.EpicCount = workItems.Count(w => w.Type == WorkItemType.Epic);
        report.FeatureCount = workItems.Count(w => w.Type == WorkItemType.Feature);
        report.PbiCount = workItems.Count(w => w.Type == WorkItemType.Pbi);
        report.BugCount = workItems.Count(w => w.Type == WorkItemType.Bug);
        report.TaskCount = workItems.Count(w => w.Type == WorkItemType.Task);

        // Validate hierarchy quantities (Rule 2.2)
        ValidateQuantities(report);

        // Validate hierarchy integrity (Rule 10.1.1)
        ValidateHierarchyIntegrity(workItems, report);

        // Validate area path consistency (Rule 10.1.2)
        ValidateAreaPathConsistency(workItems, report);

        // Validate iteration paths (Rule 10.1.3)
        ValidateIterationPaths(workItems, report);

        // Validate states (Rule 10.1.4)
        ValidateStates(workItems, report);

        // Validate effort estimation (Rule 10.1.5)
        ValidateEstimation(workItems, report);

        // Validate Battleship theme usage
        ValidateBattleshipTheme(workItems, report);

        return report;
    }

    /// <summary>
    /// Validates dependencies against rules
    /// </summary>
    public void ValidateDependencies(List<DependencyLink> dependencies, List<WorkItemDto> workItems, ValidationReport report)
    {
        report.TotalDependencies = dependencies.Count;

        // Validate cross-team percentage (Rule 7.3)
        var crossTeamCount = dependencies.Count(d => d.IsCrossTeam);
        report.CrossTeamDependencyPercentage = report.TotalDependencies > 0
            ? (double)crossTeamCount / report.TotalDependencies * 100
            : 0;

        // Validate no self-dependencies (except intentional for testing)
        var selfDependencies = dependencies.Count(d => d.SourceWorkItemId == d.TargetWorkItemId);
        report.SelfDependencyCount = selfDependencies;

        // Validate orphaned dependencies
        var workItemIds = new HashSet<int>(workItems.Select(w => w.TfsId));
        var orphanedCount = dependencies.Count(d =>
            !workItemIds.Contains(d.SourceWorkItemId) || !workItemIds.Contains(d.TargetWorkItemId));
        report.OrphanedDependencyCount = orphanedCount;

        // Calculate invalid dependency percentage
        var invalidCount = selfDependencies + orphanedCount;
        report.InvalidDependencyPercentage = report.TotalDependencies > 0
            ? (double)invalidCount / report.TotalDependencies * 100
            : 0;
    }

    /// <summary>
    /// Validates pull requests against rules
    /// </summary>
    public void ValidatePullRequests(
        List<PullRequestDto> pullRequests,
        List<PrWorkItemLink> prWorkItemLinks,
        ValidationReport report)
    {
        report.TotalPullRequests = pullRequests.Count;

        // Validate PR volume (Rule 8.1)
        report.PullRequestVolumeValid = pullRequests.Count >= 100;

        // Validate status distribution (Rule 8.3)
        var activeCount = pullRequests.Count(pr => pr.Status == "active");
        var completedCount = pullRequests.Count(pr => pr.Status == "completed");
        var abandonedCount = pullRequests.Count(pr => pr.Status == "abandoned");

        report.ActivePrPercentage = (double)activeCount / pullRequests.Count * 100;
        report.CompletedPrPercentage = (double)completedCount / pullRequests.Count * 100;
        report.AbandonedPrPercentage = (double)abandonedCount / pullRequests.Count * 100;

        // Validate work item links (Rule 8.7)
        var linkedPrCount = prWorkItemLinks.Select(l => l.PullRequestId).Distinct().Count();
        report.PrWithWorkItemLinksPercentage = pullRequests.Count > 0
            ? (double)linkedPrCount / pullRequests.Count * 100
            : 0;

        // Validate all PRs have required metadata
        var prsWithoutTitle = pullRequests.Count(pr => string.IsNullOrEmpty(pr.Title));
        var prsWithoutCreator = pullRequests.Count(pr => string.IsNullOrEmpty(pr.CreatedBy));
        var prsWithoutRepository = pullRequests.Count(pr => string.IsNullOrEmpty(pr.RepositoryName));

        report.PrMetadataValid = prsWithoutTitle == 0 && prsWithoutCreator == 0 && prsWithoutRepository == 0;
    }

    private void ValidateQuantities(ValidationReport report)
    {
        // Exact hierarchy: 10 Goals → 30 Objectives → 100 Epics → 500 Features → 3,000 PBIs + 1,000 Bugs → 15,000 Tasks
        report.GoalQuantityValid = report.GoalCount == 10;
        report.ObjectiveQuantityValid = report.ObjectiveCount >= 25 && report.ObjectiveCount <= 35;
        report.EpicQuantityValid = report.EpicCount >= 80 && report.EpicCount <= 120;
        report.FeatureQuantityValid = report.FeatureCount >= 400 && report.FeatureCount <= 600;
        report.PbiQuantityValid = report.PbiCount >= 2500 && report.PbiCount <= 3500;
        report.BugQuantityValid = report.BugCount >= 800 && report.BugCount <= 1200;
        report.TaskQuantityValid = report.TaskCount >= 12000 && report.TaskCount <= 18000;
    }

    private void ValidateHierarchyIntegrity(List<WorkItemDto> workItems, ValidationReport report)
    {
        var workItemIds = new HashSet<int>(workItems.Select(w => w.TfsId));
        var orphanedCount = 0;

        foreach (var item in workItems)
        {
            // Goals should have no parent
            if (item.Type == WorkItemType.Goal)
            {
                if (item.ParentTfsId.HasValue)
                    orphanedCount++;
            }
            else
            {
                // All other items must have a parent
                if (!item.ParentTfsId.HasValue || !workItemIds.Contains(item.ParentTfsId.Value))
                    orphanedCount++;
            }
        }

        report.OrphanedWorkItemCount = orphanedCount;
        report.HierarchyIntegrityValid = orphanedCount == 0;
    }

    private void ValidateAreaPathConsistency(List<WorkItemDto> workItems, ValidationReport report)
    {
        var violations = 0;

        // Group by Epic and validate all descendants have the same area path
        var epics = workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        foreach (var epic in epics)
        {
            var descendants = GetDescendants(epic.TfsId, workItems);
            var inconsistentDescendants = descendants.Count(d => d.AreaPath != epic.AreaPath);
            violations += inconsistentDescendants;
        }

        report.AreaPathViolationCount = violations;
        report.AreaPathConsistencyValid = violations == 0;
    }

    private List<WorkItemDto> GetDescendants(int parentId, List<WorkItemDto> allItems)
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

    private void ValidateIterationPaths(List<WorkItemDto> workItems, ValidationReport report)
    {
        // Validate that iteration paths follow expected format
        var invalidIterations = workItems.Count(w =>
            string.IsNullOrEmpty(w.IterationPath) ||
            (!w.IterationPath.Contains("Backlog") && !w.IterationPath.Contains("Sprint") && !w.IterationPath.Contains("2025")));

        report.InvalidIterationPathCount = invalidIterations;
        report.IterationPathValid = invalidIterations == 0;
    }

    private void ValidateStates(List<WorkItemDto> workItems, ValidationReport report)
    {
        var invalidStates = 0;

        foreach (var item in workItems)
        {
            var validStates = item.Type switch
            {
                WorkItemType.Goal => new[] { "Proposed", "Active", "Completed", "Removed" },
                WorkItemType.Objective => new[] { "Proposed", "Active", "Completed", "Removed" },
                WorkItemType.Epic => new[] { "New", "Active", "Resolved", "Closed", "Removed" },
                WorkItemType.Feature => new[] { "New", "Active", "Resolved", "Closed", "Removed" },
                WorkItemType.Pbi => new[] { "New", "Approved", "Committed", "Done", "Removed" },
                WorkItemType.Bug => new[] { "New", "Approved", "Committed", "Done", "Removed" },
                WorkItemType.Task => new[] { "To Do", "In Progress", "Done", "Removed" },
                _ => Array.Empty<string>()
            };

            if (!validStates.Contains(item.State))
                invalidStates++;
        }

        report.InvalidStateCount = invalidStates;
        report.StateValidityValid = invalidStates == 0;
    }

    private void ValidateEstimation(List<WorkItemDto> workItems, ValidationReport report)
    {
        var fibonacci = new[] { 1, 2, 3, 5, 8, 13, 21 };
        var pbisAndBugs = workItems.Where(w => w.Type == WorkItemType.Pbi || w.Type == WorkItemType.Bug).ToList();

        var estimated = pbisAndBugs.Count(w => w.Effort.HasValue);
        var unestimated = pbisAndBugs.Count(w => !w.Effort.HasValue);

        report.EstimatedItemCount = estimated;
        report.UnestimatedItemCount = unestimated;
        report.UnestimatedPercentage = pbisAndBugs.Count > 0
            ? (double)unestimated / pbisAndBugs.Count * 100
            : 0;

        // Validate Fibonacci values
        var nonFibonacci = pbisAndBugs.Count(w => w.Effort.HasValue && !fibonacci.Contains(w.Effort.Value));
        report.NonFibonacciEstimateCount = nonFibonacci;
        report.FibonacciEstimationValid = nonFibonacci == 0;
    }

    private void ValidateBattleshipTheme(List<WorkItemDto> workItems, ValidationReport report)
    {
        var battleshipKeywords = new[]
        {
            "battleship", "incident", "fire", "damage", "crew", "hull", "emergency",
            "sensor", "detection", "response", "safety", "control", "leakage",
            "collision", "compartment", "medical", "alert", "suppression"
        };

        var goals = workItems.Where(w => w.Type == WorkItemType.Goal).ToList();
        var themeCompliant = goals.Count(g =>
            battleshipKeywords.Any(keyword => g.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        report.BattleshipThemeCompliantCount = themeCompliant;
        report.BattleshipThemeValid = themeCompliant >= goals.Count * 0.7; // At least 70% should use theme
    }
}

/// <summary>
/// Validation report containing all metrics and validation results
/// </summary>
public class ValidationReport
{
    // Work item counts
    public int TotalWorkItems { get; set; }
    public int GoalCount { get; set; }
    public int ObjectiveCount { get; set; }
    public int EpicCount { get; set; }
    public int FeatureCount { get; set; }
    public int PbiCount { get; set; }
    public int BugCount { get; set; }
    public int TaskCount { get; set; }

    // Quantity validations
    public bool GoalQuantityValid { get; set; }
    public bool ObjectiveQuantityValid { get; set; }
    public bool EpicQuantityValid { get; set; }
    public bool FeatureQuantityValid { get; set; }
    public bool PbiQuantityValid { get; set; }
    public bool BugQuantityValid { get; set; }
    public bool TaskQuantityValid { get; set; }

    // Hierarchy integrity
    public int OrphanedWorkItemCount { get; set; }
    public bool HierarchyIntegrityValid { get; set; }

    // Area path consistency
    public int AreaPathViolationCount { get; set; }
    public bool AreaPathConsistencyValid { get; set; }

    // Iteration paths
    public int InvalidIterationPathCount { get; set; }
    public bool IterationPathValid { get; set; }

    // States
    public int InvalidStateCount { get; set; }
    public bool StateValidityValid { get; set; }

    // Estimation
    public int EstimatedItemCount { get; set; }
    public int UnestimatedItemCount { get; set; }
    public double UnestimatedPercentage { get; set; }
    public int NonFibonacciEstimateCount { get; set; }
    public bool FibonacciEstimationValid { get; set; }

    // Dependencies
    public int TotalDependencies { get; set; }
    public double CrossTeamDependencyPercentage { get; set; }
    public int SelfDependencyCount { get; set; }
    public int OrphanedDependencyCount { get; set; }
    public double InvalidDependencyPercentage { get; set; }

    // Pull requests
    public int TotalPullRequests { get; set; }
    public bool PullRequestVolumeValid { get; set; }
    public double ActivePrPercentage { get; set; }
    public double CompletedPrPercentage { get; set; }
    public double AbandonedPrPercentage { get; set; }
    public double PrWithWorkItemLinksPercentage { get; set; }
    public bool PrMetadataValid { get; set; }

    // Theme validation
    public int BattleshipThemeCompliantCount { get; set; }
    public bool BattleshipThemeValid { get; set; }

    /// <summary>
    /// Returns true if all validations pass
    /// </summary>
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
            && FibonacciEstimationValid
            && PullRequestVolumeValid
            && PrMetadataValid
            && BattleshipThemeValid;
    }

    /// <summary>
    /// Returns a summary of the validation report
    /// </summary>
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
  Fibonacci Estimation: {FibonacciEstimationValid} (Non-Fibonacci: {NonFibonacciEstimateCount})
  Unestimated: {UnestimatedPercentage:F1}%

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
