using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Pages.Metrics.SubComponents;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for IterationHealthTable component
/// </summary>
[TestClass]
public class IterationHealthTableTests : BunitTestContext
{
    [TestInitialize]
    public void Setup()
    {
        // Add MudBlazor services
        Services.AddMudServices();
        
        // Mock IHealthCalculationClient for BacklogHealthCalculationService
        var mockHealthCalculationClient = new Mock<IHealthCalculationClient>();
        // Setup mock to return a health score response
        mockHealthCalculationClient.Setup(x => x.CalculateHealthScoreAsync(
                It.IsAny<CalculateHealthScoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalculateHealthScoreResponse { HealthScore = 80 });
        
        Services.AddSingleton(mockHealthCalculationClient.Object);
        
        // Register BacklogHealthCalculationService with mocked dependency
        Services.AddSingleton<BacklogHealthCalculationService>();
        
        // Configure JSInterop in Loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestMethod]
    public void IterationHealthTable_RendersCorrectly_WithData()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>
        {
            CreateTestIteration("Sprint 1", "Project/Sprint1", 50, 5, 2, 1, 0),
            CreateTestIteration("Sprint 2", "Project/Sprint2", 60, 3, 1, 0, 0)
        };

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert
        Assert.IsNotNull(cut);
        Assert.Contains("Sprint 1", cut.Markup);
        Assert.Contains("Sprint 2", cut.Markup);
    }

    [TestMethod]
    public void IterationHealthTable_DisplaysWorkItemCounts()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>
        {
            CreateTestIteration("Sprint 1", "Project/Sprint1", 50, 5, 2, 1, 0)
        };

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert
        Assert.Contains("Total Work Items", cut.Markup);
        Assert.Contains("50", cut.Markup);
        Assert.Contains("Without Effort", cut.Markup);
        Assert.Contains("5", cut.Markup);
    }

    [TestMethod]
    public void IterationHealthTable_DisplaysValidationIssues()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>
        {
            CreateTestIterationWithValidation("Sprint 1", "Project/Sprint1", 50, 5, 2, 1, 0)
        };

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert
        Assert.Contains("Validation Issues", cut.Markup);
    }

    [TestMethod]
    public void IterationHealthTable_ShowsHealthIndicators()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>
        {
            CreateTestIteration("Sprint 1", "Project/Sprint1", 50, 5, 2, 1, 0)
        };

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert
        var icons = cut.FindComponents<MudBlazor.MudIcon>();
        Assert.IsNotEmpty(icons, "Should display health indicator icons");
    }

    [TestMethod]
    public void IterationHealthTable_HandlesEmptyList()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>();

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert - Should not throw and should render an empty grid
        Assert.IsNotNull(cut);
    }

    [TestMethod]
    public void IterationHealthTable_DisplaysMultipleIterations()
    {
        // Arrange
        var iterations = new List<BacklogHealthDto>
        {
            CreateTestIteration("Sprint 1", "Project/Sprint1", 50, 5, 2, 1, 0),
            CreateTestIteration("Sprint 2", "Project/Sprint2", 60, 3, 1, 0, 0),
            CreateTestIteration("Sprint 3", "Project/Sprint3", 55, 4, 2, 1, 1)
        };

        // Act
        var cut = RenderComponent<IterationHealthTable>(parameters => parameters
            .Add(p => p.Iterations, iterations));

        // Assert
        var cards = cut.FindComponents<MudBlazor.MudCard>();
        Assert.HasCount(3, cards, "Should render 3 iteration cards");
    }

    // Helper methods to create test data
    private BacklogHealthDto CreateTestIteration(
        string sprintName,
        string iterationPath,
        int totalWorkItems,
        int withoutEffort,
        int inProgressWithoutEffort,
        int parentProgressIssues,
        int blockedItems)
    {
        return new BacklogHealthDto
        {
            SprintName = sprintName,
            IterationPath = iterationPath,
            TotalWorkItems = totalWorkItems,
            WorkItemsWithoutEffort = withoutEffort,
            WorkItemsInProgressWithoutEffort = inProgressWithoutEffort,
            ParentProgressIssues = parentProgressIssues,
            BlockedItems = blockedItems,
            InProgressAtIterationEnd = 0,
            ValidationIssues = new List<ValidationIssueSummary>()
        };
    }

    private BacklogHealthDto CreateTestIterationWithValidation(
        string sprintName,
        string iterationPath,
        int totalWorkItems,
        int withoutEffort,
        int inProgressWithoutEffort,
        int parentProgressIssues,
        int blockedItems)
    {
        var iteration = CreateTestIteration(
            sprintName, iterationPath, totalWorkItems, withoutEffort,
            inProgressWithoutEffort, parentProgressIssues, blockedItems);
        
        iteration.ValidationIssues = new List<ValidationIssueSummary>
        {
            new ValidationIssueSummary
            {
                ValidationType = "MissingEffort",
                Count = 5
            }
        };

        return iteration;
    }
}
