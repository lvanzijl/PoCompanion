using PoTool.Api.Services.MockData;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public class BattleshipWorkItemGeneratorTests
{
    private BattleshipWorkItemGenerator _generator = null!;
    private List<WorkItemDto> _workItems = null!;

    [TestInitialize]
    public void Setup()
    {
        _generator = new BattleshipWorkItemGenerator();
        _workItems = _generator.GenerateHierarchy();
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Exactly_10_Goals()
    {
        var goals = _workItems.Where(w => w.Type == WorkItemType.Goal).ToList();

        // Assert
        Assert.HasCount(10, goals, "Should generate exactly 10 Goals");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Objectives_Within_Range()
    {
        var objectives = _workItems.Where(w => w.Type == WorkItemType.Objective).ToList();

        // Assert
        Assert.IsTrue(objectives.Count >= 25 && objectives.Count <= 35,
            $"Should generate 25-35 Objectives, got {objectives.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Epics_Within_Range()
    {
        var epics = _workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        // Assert
        Assert.IsTrue(epics.Count >= 80 && epics.Count <= 120,
            $"Should generate 80-120 Epics, got {epics.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Features_Within_Range()
    {
        var features = _workItems.Where(w => w.Type == WorkItemType.Feature).ToList();

        // Assert
        Assert.IsTrue(features.Count >= 400 && features.Count <= 600,
            $"Should generate 400-600 Features, got {features.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_PBIs_Within_Range()
    {
        var pbis = _workItems.Where(w => w.Type == WorkItemType.Pbi).ToList();

        // Assert - Wider range to accommodate randomness with fixed seed
        Assert.IsTrue(pbis.Count >= 2500 && pbis.Count <= 4500,
            $"Should generate 2500-4500 PBIs, got {pbis.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Bugs_Within_Range()
    {
        var bugs = _workItems.Where(w => w.Type == WorkItemType.Bug).ToList();

        // Assert
        Assert.IsTrue(bugs.Count >= 800 && bugs.Count <= 1200,
            $"Should generate 800-1200 Bugs, got {bugs.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Tasks_Within_Range()
    {
        var tasks = _workItems.Where(w => w.Type == WorkItemType.Task).ToList();

        // Assert - Wider range to accommodate randomness with fixed seed
        Assert.IsTrue(tasks.Count >= 12000 && tasks.Count <= 20000,
            $"Should generate 12000-20000 Tasks, got {tasks.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Use_Battleship_Theme_In_Goals()
    {
        var goals = _workItems.Where(w => w.Type == WorkItemType.Goal).ToList();

        // Battleship keywords
        var battleshipKeywords = new[]
        {
            "incident", "damage", "crew", "hull", "emergency",
            "response", "safety", "control", "monitoring", "command"
        };

        // Assert - at least 70% of goals should contain battleship keywords
        var goalsWithTheme = goals.Count(g =>
            battleshipKeywords.Any(keyword => g.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        Assert.IsGreaterThanOrEqualTo(goals.Count * 0.7, goalsWithTheme,
            $"At least 70% of goals should use Battleship theme. Found {goalsWithTheme}/{goals.Count}");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Enforce_Area_Path_Inheritance_From_Epic()
    {
        var epics = _workItems.Where(w => w.Type == WorkItemType.Epic).ToList();

        // Check that all descendants of each Epic have the same area path
        var violations = 0;
        foreach (var epic in epics)
        {
            var descendants = GetDescendants(epic.TfsId, _workItems);
            violations += descendants.Count(d => d.AreaPath != epic.AreaPath);
        }

        // Assert
        Assert.AreEqual(0, violations,
            $"All descendants of an Epic must inherit its area path. Found {violations} violations.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_Valid_Parent_Child_Relationships()
    {
        var workItemIds = new HashSet<int>(_workItems.Select(w => w.TfsId));

        // Check that all non-Goal items have valid parent references
        var orphanedItems = _workItems
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
        var backlogItems = _workItems
            .Where(w => w.Type == WorkItemType.Epic || w.Type == WorkItemType.Feature || w.Type == WorkItemType.Pbi || w.Type == WorkItemType.Bug)
            .ToList();

        var unestimated = backlogItems.Count(w => !w.Effort.HasValue);
        var unestimatedPercentage = (double)unestimated / backlogItems.Count * 100;

        Assert.IsGreaterThanOrEqualTo(5d, unestimatedPercentage,
            $"Expected some missing effort for backlog realism. Found {unestimatedPercentage:F1}%.");
        Assert.IsLessThanOrEqualTo(20d, unestimatedPercentage,
            $"Expected missing effort to remain a minority. Found {unestimatedPercentage:F1}%.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Keep_StoryPoints_On_PBIs_Only()
    {
        var nonPbiSizing = _workItems
            .Where(w => w.Type != WorkItemType.Pbi)
            .Where(w => w.StoryPoints.HasValue || w.BusinessValue.HasValue)
            .ToList();

        var pbisWithSizing = _workItems
            .Where(w => w.Type == WorkItemType.Pbi)
            .Count(w => w.StoryPoints.HasValue || w.BusinessValue.HasValue);

        Assert.IsEmpty(nonPbiSizing, "Only PBIs should carry story-point sizing fields.");
        Assert.IsGreaterThan(0, pbisWithSizing, "PBIs should provide story-point inputs for delivery analytics.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Separate_Effort_Hours_From_StoryPoints()
    {
        var pbisWithBoth = _workItems
            .Where(w => w.Type == WorkItemType.Pbi)
            .Where(w => w.Effort.HasValue && w.StoryPoints.HasValue)
            .ToList();

        Assert.IsGreaterThan(0, pbisWithBoth.Count, "Expected PBIs with both effort and story-point signals.");
        Assert.IsTrue(pbisWithBoth.Any(w => w.Effort != w.StoryPoints),
            "Effort hours should not collapse to the same values as story points across the dataset.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Create_MultiSprint_History_Window()
    {
        var sprintPaths = _workItems
            .Where(w => w.IterationPath.Contains("Sprint ", StringComparison.OrdinalIgnoreCase))
            .Select(w => w.IterationPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.IsGreaterThanOrEqualTo(5, sprintPaths.Count, "Expected at least five distinct sprint paths.");
        Assert.IsLessThanOrEqualTo(10, sprintPaths.Count, "Mock history should stay concentrated in a usable 10-sprint window.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Align_Delivery_Work_With_Current_Battleship_Sprint_Window()
    {
        var deliverySprintPaths = _workItems
            .Where(w => w.Type is WorkItemType.Pbi or WorkItemType.Bug)
            .Where(w => w.IterationPath.Contains("Sprint ", StringComparison.OrdinalIgnoreCase))
            .Select(w => w.IterationPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.IsTrue(deliverySprintPaths.All(path => !path.Contains("\\2025\\", StringComparison.OrdinalIgnoreCase)),
            "Delivery work should use the current seeded Battleship sprint paths instead of legacy 2025 quarter paths.");
        CollectionAssert.Contains(deliverySprintPaths, "\\Battleship Systems\\Sprint 11",
            "The current sprint window should contain Sprint 11 work so execution surfaces can render data.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Use_TfsStyle_Semicolon_Tag_Formatting()
    {
        var taggedItems = _workItems
            .Where(w => !string.IsNullOrWhiteSpace(w.Tags))
            .Take(100)
            .ToList();

        Assert.IsNotEmpty(taggedItems, "Expected tagged mock work items.");
        Assert.IsTrue(taggedItems.All(item => !item.Tags!.Contains(',')),
            "Mock tags should use TFS-style semicolon separators rather than commas.");
        Assert.IsTrue(taggedItems.All(item => item.Tags!.Split(';', StringSplitOptions.RemoveEmptyEntries).Length >= 2),
            "Tagged mock work items should remain parseable as multi-tag TFS values.");
    }

    [TestMethod]
    public void GenerateHierarchy_Should_Include_Roadmap_Epics_And_Triageable_Bug_Tags()
    {
        var roadmapEpicCount = _workItems
            .Where(w => w.Type == WorkItemType.Epic)
            .Count(w => (w.Tags ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase)));

        var bugWithTriageTagCount = _workItems
            .Where(w => w.Type == WorkItemType.Bug)
            .Count(w => (w.Tags ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(tag => tag is "Needs Investigation" or "Regression" or "Customer Reported" or "Operational Risk" or "Hotfix Candidate" or "Needs Repro"));

        Assert.IsGreaterThan(0, roadmapEpicCount, "The mock dataset should surface roadmap-tagged epics for roadmap pages.");
        Assert.IsGreaterThan(0, bugWithTriageTagCount, "The mock dataset should include triage tags on bugs for the bug-triage UI.");
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
