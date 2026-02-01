using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Client.Models;
using System.Text.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BugTreeBuilderServiceTests
{
    private BugTreeBuilderService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new BugTreeBuilderService();
    }

    private WorkItemWithValidationDto CreateBug(int id, string title, Dictionary<string, object>? fields = null)
    {
        var jsonFields = fields ?? new Dictionary<string, object>();
        var jsonPayload = JsonSerializer.Serialize(jsonFields);

        return new WorkItemWithValidationDto
        {
            TfsId = id,
            Type = "Bug",
            Title = title,
            ParentTfsId = null,
            AreaPath = "Project\\Team",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = jsonPayload,
            RetrievedAt = DateTimeOffset.UtcNow,
            Effort = null,
            Description = null,
            CreatedDate = DateTimeOffset.UtcNow,
            ClosedDate = null,
            ValidationIssues = new List<ValidationIssue>()
        };
    }

    #region BuildBugTriageTree Tests

    [TestMethod]
    public void BuildBugTriageTree_WithUntriagedBugs_CreatesNewUntriagedGroup()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1"),
            CreateBug(2, "Bug 2")
        };
        var untriagedIds = new HashSet<int> { 1, 2 };
        var expandedState = new Dictionary<int, bool>();
        string GetCriticality(WorkItemWithValidationDto bug) => BugCriticality.Medium;

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.HasCount(1, roots); // Only "New / Untriaged" group
        Assert.AreEqual("New / Untriaged (2)", roots[0].Title);
        Assert.AreEqual("(group)", roots[0].Type);
        Assert.HasCount(2, roots[0].Children);
    }

    [TestMethod]
    public void BuildBugTriageTree_WithCriticalBugs_CreatesCriticalGroup()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Critical Bug", new Dictionary<string, object> { { "Microsoft.VSTS.Common.Priority", 1 } })
        };
        var untriagedIds = new HashSet<int>(); // All triaged
        var expandedState = new Dictionary<int, bool>();
        string GetCriticality(WorkItemWithValidationDto bug) => BugCriticality.Critical;

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.HasCount(1, roots);
        Assert.AreEqual("Critical (1)", roots[0].Title);
        Assert.AreEqual("(group)", roots[0].Type);
        Assert.HasCount(1, roots[0].Children);
    }

    [TestMethod]
    public void BuildBugTriageTree_WithMixedCriticality_CreatesMultipleGroups()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Critical Bug"),
            CreateBug(2, "High Bug"),
            CreateBug(3, "Medium Bug"),
            CreateBug(4, "Low Bug")
        };
        var untriagedIds = new HashSet<int>(); // All triaged
        var expandedState = new Dictionary<int, bool>();
        var criticalityMap = new Dictionary<int, string>
        {
            { 1, BugCriticality.Critical },
            { 2, BugCriticality.High },
            { 3, BugCriticality.Medium },
            { 4, BugCriticality.Low }
        };
        string GetCriticality(WorkItemWithValidationDto bug) => criticalityMap[bug.TfsId];

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.HasCount(4, roots); // Critical, High, Medium, Low groups
        Assert.AreEqual("Critical (1)", roots[0].Title);
        Assert.AreEqual("High (1)", roots[1].Title);
        Assert.AreEqual("Medium (1)", roots[2].Title);
        Assert.AreEqual("Low (1)", roots[3].Title);
    }

    [TestMethod]
    public void BuildBugTriageTree_UntriagedFirst_ThenCriticality()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Untriaged Bug"),
            CreateBug(2, "Critical Triaged Bug")
        };
        var untriagedIds = new HashSet<int> { 1 };
        var expandedState = new Dictionary<int, bool>();
        var criticalityMap = new Dictionary<int, string>
        {
            { 1, BugCriticality.Medium },
            { 2, BugCriticality.Critical }
        };
        string GetCriticality(WorkItemWithValidationDto bug) => criticalityMap[bug.TfsId];

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.HasCount(2, roots);
        Assert.AreEqual("New / Untriaged (1)", roots[0].Title); // First
        Assert.AreEqual("Critical (1)", roots[1].Title); // Second
    }

    [TestMethod]
    public void BuildBugTriageTree_WithExpandedState_RestoresState()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1")
        };
        var untriagedIds = new HashSet<int> { 1 };
        var expandedState = new Dictionary<int, bool>
        {
            { -1000, false } // New/Untriaged group collapsed
        };
        string GetCriticality(WorkItemWithValidationDto bug) => BugCriticality.Medium;

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.IsFalse(roots[0].IsExpanded);
    }

    [TestMethod]
    public void BuildBugTriageTree_DefaultExpandedState_GroupsExpanded()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1")
        };
        var untriagedIds = new HashSet<int> { 1 };
        var expandedState = new Dictionary<int, bool>();
        string GetCriticality(WorkItemWithValidationDto bug) => BugCriticality.Medium;

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        Assert.IsTrue(roots[0].IsExpanded); // Default is expanded
    }

    [TestMethod]
    public void BuildBugTriageTree_SortsByIdDescending()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1"),
            CreateBug(3, "Bug 3"),
            CreateBug(2, "Bug 2")
        };
        var untriagedIds = new HashSet<int> { 1, 2, 3 };
        var expandedState = new Dictionary<int, bool>();
        string GetCriticality(WorkItemWithValidationDto bug) => BugCriticality.Medium;

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetCriticality);

        // Assert
        var children = roots[0].Children;
        Assert.AreEqual(3, children[0].Id); // Highest ID first
        Assert.AreEqual(2, children[1].Id);
        Assert.AreEqual(1, children[2].Id);
    }

    #endregion

    #region ApplyTagFilters Tests

    [TestMethod]
    public void ApplyTagFilters_MatchAny_ReturnsBugsWithAnyTag()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1", new Dictionary<string, object> { { "System.Tags", "Tag1; Tag2" } }),
            CreateBug(2, "Bug 2", new Dictionary<string, object> { { "System.Tags", "Tag2; Tag3" } }),
            CreateBug(3, "Bug 3", new Dictionary<string, object> { { "System.Tags", "Tag4" } })
        };
        var selectedTags = new List<string> { "Tag1", "Tag3" };
        var matchMode = TagMatchMode.Any;
        List<string> GetTags(WorkItemWithValidationDto bug)
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bug.JsonPayload);
            if (fields != null && fields.TryGetValue("System.Tags", out var tagsElement))
            {
                return tagsElement.GetString()!.Split(';').Select(t => t.Trim()).ToList();
            }
            return new List<string>();
        }

        // Act
        var result = _service.ApplyTagFilters(bugs, selectedTags, matchMode, GetTags).ToList();

        // Assert
        Assert.HasCount(2, result); // Bug 1 has Tag1, Bug 2 has Tag3
        Assert.IsTrue(result.Any(b => b.TfsId == 1));
        Assert.IsTrue(result.Any(b => b.TfsId == 2));
    }

    [TestMethod]
    public void ApplyTagFilters_MatchAll_ReturnsBugsWithAllTags()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1", new Dictionary<string, object> { { "System.Tags", "Tag1; Tag2; Tag3" } }),
            CreateBug(2, "Bug 2", new Dictionary<string, object> { { "System.Tags", "Tag1; Tag2" } }),
            CreateBug(3, "Bug 3", new Dictionary<string, object> { { "System.Tags", "Tag1" } })
        };
        var selectedTags = new List<string> { "Tag1", "Tag2" };
        var matchMode = TagMatchMode.All;
        List<string> GetTags(WorkItemWithValidationDto bug)
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bug.JsonPayload);
            if (fields != null && fields.TryGetValue("System.Tags", out var tagsElement))
            {
                return tagsElement.GetString()!.Split(';').Select(t => t.Trim()).ToList();
            }
            return new List<string>();
        }

        // Act
        var result = _service.ApplyTagFilters(bugs, selectedTags, matchMode, GetTags).ToList();

        // Assert
        Assert.HasCount(2, result); // Bug 1 and Bug 2 have both Tag1 and Tag2
        Assert.IsTrue(result.Any(b => b.TfsId == 1));
        Assert.IsTrue(result.Any(b => b.TfsId == 2));
    }

    [TestMethod]
    public void ApplyTagFilters_NoSelectedTags_ReturnsAllBugs()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1"),
            CreateBug(2, "Bug 2")
        };
        var selectedTags = new List<string>();
        var matchMode = TagMatchMode.Any;
        List<string> GetTags(WorkItemWithValidationDto bug) => new List<string>();

        // Act
        var result = _service.ApplyTagFilters(bugs, selectedTags, matchMode, GetTags).ToList();

        // Assert
        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void ApplyTagFilters_NoMatchingTags_ReturnsEmpty()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1", new Dictionary<string, object> { { "System.Tags", "Tag1" } }),
            CreateBug(2, "Bug 2", new Dictionary<string, object> { { "System.Tags", "Tag2" } })
        };
        var selectedTags = new List<string> { "Tag3" };
        var matchMode = TagMatchMode.Any;
        List<string> GetTags(WorkItemWithValidationDto bug)
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bug.JsonPayload);
            if (fields != null && fields.TryGetValue("System.Tags", out var tagsElement))
            {
                return tagsElement.GetString()!.Split(';').Select(t => t.Trim()).ToList();
            }
            return new List<string>();
        }

        // Act
        var result = _service.ApplyTagFilters(bugs, selectedTags, matchMode, GetTags).ToList();

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ApplyTagFilters_CaseInsensitive_MatchesTags()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Bug 1", new Dictionary<string, object> { { "System.Tags", "TAG1" } })
        };
        var selectedTags = new List<string> { "tag1" };
        var matchMode = TagMatchMode.Any;
        List<string> GetTags(WorkItemWithValidationDto bug)
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bug.JsonPayload);
            if (fields != null && fields.TryGetValue("System.Tags", out var tagsElement))
            {
                return tagsElement.GetString()!.Split(';').Select(t => t.Trim()).ToList();
            }
            return new List<string>();
        }

        // Act
        var result = _service.ApplyTagFilters(bugs, selectedTags, matchMode, GetTags).ToList();

        // Assert
        Assert.HasCount(1, result);
    }

    #endregion
}
