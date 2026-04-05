using PoTool.Api.Services.MockData;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public class MockDataValidatorTests
{
    private MockDataValidator _validator = null!;
    private BattleshipWorkItemGenerator _workItemGenerator = null!;
    private BattleshipDependencyGenerator _dependencyGenerator = null!;
    private BattleshipPullRequestGenerator _pullRequestGenerator = null!;
    private List<WorkItemDto> _workItems = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new MockDataValidator();
        _workItemGenerator = new BattleshipWorkItemGenerator();
        _dependencyGenerator = new BattleshipDependencyGenerator();
        _pullRequestGenerator = new BattleshipPullRequestGenerator();
        _workItems = _workItemGenerator.GenerateHierarchy();
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_Area_Path_Consistency()
    {
        var report = _validator.ValidateWorkItems(_workItems);

        // Assert
        Assert.IsTrue(report.AreaPathConsistencyValid,
            $"Area path consistency should be valid. Found {report.AreaPathViolationCount} violations.");
        Assert.AreEqual(0, report.AreaPathViolationCount,
            "Should have no area path violations (all descendants inherit from Epic)");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_States()
    {
        var report = _validator.ValidateWorkItems(_workItems);

        // Assert
        Assert.IsTrue(report.StateValidityValid,
            $"All states should be valid. Found {report.InvalidStateCount} invalid states.");
        Assert.AreEqual(0, report.InvalidStateCount,
            "Should have no invalid states");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Validate_StoryPoint_Separation()
    {
        var report = _validator.ValidateWorkItems(_workItems);

        Assert.IsTrue(report.StoryPointEstimationValid,
            $"PBI story-point values should stay in the supported sizing set. Found {report.NonStandardStoryPointCount} invalid values.");
        Assert.IsTrue(report.EffortStoryPointSeparationValid,
            $"Only PBIs should carry story-point sizing fields. Found {report.NonPbiStoryPointCount} invalid non-PBI items.");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Check_Backlog_Quality_Distribution()
    {
        var report = _validator.ValidateWorkItems(_workItems);

        Assert.IsTrue(report.BacklogQualityDistributionValid,
            $"Expected 5-20% invalid backlog items. Found {report.InvalidBacklogItemPercentage:F1}% invalid.");
        Assert.IsGreaterThan(0, report.MissingDescriptionCount, "Expected some backlog items with missing descriptions.");
        Assert.IsGreaterThan(0, report.MissingEstimateCount, "Expected some backlog items with missing effort estimates.");
        Assert.IsGreaterThan(0, report.BrokenHierarchyCount, "Expected some broken hierarchy cases for backlog realism.");
        Assert.IsGreaterThan(0, report.InconsistentStateCount, "Expected some state mismatches for backlog realism.");
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Detect_Unknown_Sprint_Definitions()
    {
        var invalidWorkItems = _workItems
            .Select(item => item.Type == WorkItemType.Pbi
                ? item with { IterationPath = "\\Battleship Systems\\Sprint 99" }
                : item)
            .ToList();

        var report = _validator.ValidateWorkItems(
            invalidWorkItems,
            BattleshipSprintSeedCatalog.GetIterationPaths("Battleship Systems"),
            BattleshipWorkItemGenerator.GetTeamStructure().SelectMany(static group => group.Teams).ToArray());

        Assert.IsFalse(report.SprintDefinitionAlignmentValid);
        Assert.IsGreaterThan(0, report.UnknownSprintDefinitionCount);
    }

    [TestMethod]
    public void ValidateWorkItems_Should_Detect_Unknown_Team_Assignments()
    {
        var invalidWorkItems = _workItems
            .Select(item => item.Type == WorkItemType.Feature
                ? item with { AreaPath = "\\Battleship Systems\\Incident Response\\Unknown Team" }
                : item)
            .ToList();

        var report = _validator.ValidateWorkItems(
            invalidWorkItems,
            BattleshipSprintSeedCatalog.GetIterationPaths("Battleship Systems"),
            BattleshipWorkItemGenerator.GetTeamStructure().SelectMany(static group => group.Teams).ToArray());

        Assert.IsFalse(report.TeamAssignmentsValid);
        Assert.IsGreaterThan(0, report.UnknownTeamAssignmentCount);
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Volume()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(_workItems);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, _workItems);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PullRequestVolumeValid,
            $"PR volume should be at least 100. Found {report.TotalPullRequests}");
        Assert.IsGreaterThan(0, report.TotalPullRequests,
            $"Should generate pull requests. Found {report.TotalPullRequests}");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Status_Distribution()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(_workItems);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, _workItems);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.ActivePrPercentage >= 3 && report.ActivePrPercentage <= 12,
            $"Active PR percentage should remain a small minority. Found {report.ActivePrPercentage:F1}%");
        Assert.IsTrue(report.CompletedPrPercentage >= 85 && report.CompletedPrPercentage <= 95,
            $"Completed PR percentage should be about 90%. Found {report.CompletedPrPercentage:F1}%");
        Assert.IsLessThanOrEqualTo(8d, report.AbandonedPrPercentage,
            $"Abandoned PR percentage should remain small. Found {report.AbandonedPrPercentage:F1}%");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Check_Work_Item_Links()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(_workItems);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, _workItems);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PrWithWorkItemLinksPercentage >= 80 && report.PrWithWorkItemLinksPercentage <= 95,
            $"PR with work item links should be at least 80%. Found {report.PrWithWorkItemLinksPercentage:F1}%");
        var workItemIds = _workItems.Select(item => item.TfsId).ToHashSet();
        Assert.IsTrue(prLinks.All(link => workItemIds.Contains(link.WorkItemId)),
            "Every generated PR link should point at an existing work item.");
    }

    [TestMethod]
    public void ValidatePullRequests_Should_Validate_Metadata()
    {
        // Arrange
        var pullRequests = _pullRequestGenerator.GeneratePullRequests(_workItems);
        var prLinks = _pullRequestGenerator.GeneratePrWorkItemLinks(pullRequests, _workItems);
        var report = new ValidationReport();

        // Act
        _validator.ValidatePullRequests(pullRequests, prLinks, report);

        // Assert
        Assert.IsTrue(report.PrMetadataValid,
            "All PRs should have required metadata (title, creator, repository)");
    }

}
