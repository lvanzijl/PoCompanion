using PoTool.Api.Services.MockData;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public class MockDataValidatorTests
{
    private MockDataValidator _validator = null!;
    private BattleshipWorkItemGenerator _workItemGenerator = null!;
    private BattleshipDependencyGenerator _dependencyGenerator = null!;
    private BattleshipPullRequestGenerator _pullRequestGenerator = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new MockDataValidator();
        _workItemGenerator = new BattleshipWorkItemGenerator();
        _dependencyGenerator = new BattleshipDependencyGenerator();
        _pullRequestGenerator = new BattleshipPullRequestGenerator();
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Hierarchy_Quantities()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.GoalQuantityValid, "Goal quantity should be valid");
        Assert.IsTrue(report.ObjectiveQuantityValid, "Objective quantity should be valid");
        Assert.IsTrue(report.EpicQuantityValid, "Epic quantity should be valid");
        Assert.IsTrue(report.FeatureQuantityValid, "Feature quantity should be valid");
        Assert.IsTrue(report.PbiQuantityValid, "PBI quantity should be valid");
        Assert.IsTrue(report.BugQuantityValid, "Bug quantity should be valid");
        Assert.IsTrue(report.TaskQuantityValid, "Task quantity should be valid");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Hierarchy_Integrity()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.HierarchyIntegrityValid,
            $"Hierarchy integrity should be valid. Found {report.OrphanedWorkItemCount} orphaned items.");
        Assert.AreEqual(0, report.OrphanedWorkItemCount,
            "Should have no orphaned work items");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Area_Path_Consistency()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.AreaPathConsistencyValid,
            $"Area path consistency should be valid. Found {report.AreaPathViolationCount} violations.");
        Assert.AreEqual(0, report.AreaPathViolationCount,
            "Should have no area path violations (all descendants inherit from Epic)");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_States()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.StateValidityValid,
            $"All states should be valid. Found {report.InvalidStateCount} invalid states.");
        Assert.AreEqual(0, report.InvalidStateCount,
            "Should have no invalid states");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Fibonacci_Estimation()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.FibonacciEstimationValid,
            $"All estimates should use Fibonacci values. Found {report.NonFibonacciEstimateCount} non-Fibonacci estimates.");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Check_Unestimated_Percentage()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.UnestimatedPercentage >= 15 && report.UnestimatedPercentage <= 35,
            $"Unestimated percentage should be 20-30%. Found {report.UnestimatedPercentage:F1}%");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Battleship_Theme()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();

        // Act
        var report = _validator.ValidateWorkItems(workItems);

        // Assert
        Assert.IsTrue(report.BattleshipThemeValid,
            $"Battleship theme should be used. Found {report.BattleshipThemeCompliantCount}/{report.GoalCount} compliant goals.");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Volume()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(10000);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, 10000);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PullRequestVolumeValid,
            $"PR volume should be at least 100. Found {report.TotalPullRequests}");
        Assert.IsGreaterThanOrEqualTo(100, report.TotalPullRequests,
            $"Should generate at least 100 PRs. Found {report.TotalPullRequests}");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Status_Distribution()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(10000);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, 10000);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.ActivePrPercentage >= 10 && report.ActivePrPercentage <= 25,
            $"Active PR percentage should be 15-20%. Found {report.ActivePrPercentage:F1}%");
        Assert.IsTrue(report.CompletedPrPercentage >= 65 && report.CompletedPrPercentage <= 80,
            $"Completed PR percentage should be 70-75%. Found {report.CompletedPrPercentage:F1}%");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Work_Item_Links()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(10000);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, 10000);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PrWithWorkItemLinksPercentage >= 65 && report.PrWithWorkItemLinksPercentage <= 85,
            $"PR with work item links should be 70-80%. Found {report.PrWithWorkItemLinksPercentage:F1}%");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Validate_Metadata()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(10000);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, 10000);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PrMetadataValid,
            "All PRs should have required metadata (title, creator, repository)");
    }

    [TestMethod]
    public void ValidationReport_GetSummary_Should_Return_Formatted_Report()
    {
        // Arrange
        var workItems = _workItemGenerator.GenerateHierarchy();
        var report = _validator.ValidateWorkItems(workItems);

        // Act
        var summary = report.GetSummary();

        // Assert
        Assert.IsNotNull(summary);
        Assert.Contains("Mock Data Validation Report", summary);
        Assert.Contains("Work Items:", summary);
        Assert.Contains("Data Quality:", summary); 
    }
}
