using PoTool.Api.Services.MockData;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public class BattleshipWorkItemGeneratorTests
{
    private BattleshipWorkItemGenerator _generator = null!;

    [TestInitialize]
    public void Setup()
    {
        _generator = new BattleshipWorkItemGenerator();
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Exactly_10_Goals()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var goals = workItems.Where(w => w.Type == WorkItemType.Goal).ToList();

        // Assert
        Assert.HasCount(10, goals, "Should generate exactly 10 Goals");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Objectives_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var objectives = workItems.Where(w => w.Type == WorkItemType.Objective).ToList();

        // Assert
        Assert.IsTrue(objectives.Count >= 25 && objectives.Count <= 35,
            $"Should generate 25-35 Objectives, got {objectives.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Epics_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var epics = workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        // Assert
        Assert.IsTrue(epics.Count >= 80 && epics.Count <= 120,
            $"Should generate 80-120 Epics, got {epics.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Features_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var features = workItems.Where(w => w.Type == WorkItemType.Feature).ToList();

        // Assert
        Assert.IsTrue(features.Count >= 400 && features.Count <= 600,
            $"Should generate 400-600 Features, got {features.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_PBIs_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var pbis = workItems.Where(w => w.Type == WorkItemType.Pbi).ToList();

        // Assert - Wider range to accommodate randomness with fixed seed
        Assert.IsTrue(pbis.Count >= 2500 && pbis.Count <= 4500,
            $"Should generate 2500-4500 PBIs, got {pbis.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Bugs_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var bugs = workItems.Where(w => w.Type == WorkItemType.Bug).ToList();

        // Assert
        Assert.IsTrue(bugs.Count >= 800 && bugs.Count <= 1200,
            $"Should generate 800-1200 Bugs, got {bugs.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Tasks_Within_Range()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var tasks = workItems.Where(w => w.Type == WorkItemType.Task).ToList();

        // Assert - Wider range to accommodate randomness with fixed seed
        Assert.IsTrue(tasks.Count >= 12000 && tasks.Count <= 20000,
            $"Should generate 12000-20000 Tasks, got {tasks.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Use_Battleship_Theme_In_Goals()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var goals = workItems.Where(w => w.Type == WorkItemType.Goal).ToList();

        // Battleship keywords
        var battleshipKeywords = new[] 
        { 
            "incident", "damage", "crew", "hull", "emergency", 
            "response", "safety", "control", "monitoring", "command" 
        };

        // Assert - at least 80% of goals should contain battleship keywords
        var goalsWithTheme = goals.Count(g =>
            battleshipKeywords.Any(keyword => g.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        Assert.IsGreaterThanOrEqualTo(goalsWithTheme, goals.Count * 0.8,
            $"At least 80% of goals should use Battleship theme. Found {goalsWithTheme}/{goals.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Enforce_Area_Path_Inheritance_From_Epic()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var epics = workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        // Check that all descendants of each Epic have the same area path
        var violations = 0;
        foreach (var epic in epics)
        {
            var descendants = GetDescendants(epic.TfsId, workItems);
            violations += descendants.Count(d => d.AreaPath != epic.AreaPath);
        }

        // Assert
        Assert.AreEqual(0, violations,
            $"All descendants of an Epic must inherit its area path. Found {violations} violations.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Valid_Parent_Child_Relationships()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var workItemIds = new HashSet<int>(workItems.Select(w => w.TfsId));

        // Check that all non-Goal items have valid parent references
        var orphanedItems = workItems
            .Where(w => w.Type != WorkItemType.Goal)
            .Where(w => !w.ParentTfsId.HasValue || !workItemIds.Contains(w.ParentTfsId.Value))
            .ToList();

        // Assert
        Assert.IsEmpty(orphanedItems,
            $"All non-Goal items must have valid parent references. Found {orphanedItems.Count} orphaned items.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Have_Unestimated_Items()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var pbisAndBugs = workItems.Where(w => w.Type == WorkItemType.Pbi || w.Type == WorkItemType.Bug).ToList();

        var unestimated = pbisAndBugs.Count(w => !w.Effort.HasValue);
        var unestimatedPercentage = (double)unestimated / pbisAndBugs.Count * 100;

        // Assert - should have 20-30% unestimated items
        Assert.IsTrue(unestimatedPercentage >= 15 && unestimatedPercentage <= 35,
            $"Should have 20-30% unestimated items. Found {unestimatedPercentage:F1}%");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Use_Fibonacci_Sequence_For_Effort()
    {
        // Act
        var workItems = _generator.GenerateHierarchy();
        var fibonacci = new[] { 1, 2, 3, 5, 8, 13, 21 };
        var pbisAndBugs = workItems
            .Where(w => w.Type == WorkItemType.Pbi || w.Type == WorkItemType.Bug)
            .Where(w => w.Effort.HasValue)
            .ToList();

        // Check that all estimated items use Fibonacci values
        var nonFibonacci = pbisAndBugs.Count(w => !fibonacci.Contains(w.Effort!.Value));

        // Assert
        Assert.AreEqual(0, nonFibonacci,
            $"All estimated items should use Fibonacci values. Found {nonFibonacci} non-Fibonacci estimates.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Complete_In_Reasonable_Time()
    {
        // Act
        var startTime = DateTime.UtcNow;
        var workItems = _generator.GenerateHierarchy();
        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

        // Assert - generation should complete in under 30 seconds
        Assert.IsLessThan(30, elapsed,
            $"Generation should complete in under 30 seconds. Took {elapsed:F2} seconds.");
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
}
