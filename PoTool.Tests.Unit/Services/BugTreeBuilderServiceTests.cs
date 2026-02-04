using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Client.Models;
using System.Text.Json;
using Moq;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BugTreeBuilderServiceTests
{
    private BugTreeBuilderService _service = null!;
    private Mock<ILogger<BugTreeBuilderService>> _mockLogger = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockLogger = new Mock<ILogger<BugTreeBuilderService>>();
        _service = new BugTreeBuilderService(_mockLogger.Object);
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
            ValidationIssues = new List<ValidationIssue>(),
            Tags = null
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
        string? GetSeverity(WorkItemWithValidationDto bug) => "3 - Medium";

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

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
            CreateBug(1, "Critical Bug", new Dictionary<string, object>())
        };
        var untriagedIds = new HashSet<int>(); // All triaged
        var expandedState = new Dictionary<int, bool>();
        string? GetSeverity(WorkItemWithValidationDto bug) => "1 - Critical";

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

        // Assert
        Assert.HasCount(1, roots);
        Assert.AreEqual("1 - Critical (1)", roots[0].Title);
        Assert.AreEqual("(group)", roots[0].Type);
        Assert.HasCount(1, roots[0].Children);
    }

    [TestMethod]
    public void BuildBugTriageTree_WithMixedSeverity_CreatesMultipleGroups()
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
        // Use TFS-format severity values (what TFS actually returns)
        var severityMap = new Dictionary<int, string>
        {
            { 1, "1 - Critical" },
            { 2, "2 - High" },
            { 3, "3 - Medium" },
            { 4, "4 - Low" }
        };
        string? GetSeverity(WorkItemWithValidationDto bug) => severityMap[bug.TfsId];

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

        // Assert
        Assert.HasCount(4, roots); // Critical, High, Medium, Low groups
        // Groups are sorted by severity string value alphabetically
        Assert.AreEqual("1 - Critical (1)", roots[0].Title);
        Assert.AreEqual("2 - High (1)", roots[1].Title);
        Assert.AreEqual("3 - Medium (1)", roots[2].Title);
        Assert.AreEqual("4 - Low (1)", roots[3].Title);
    }

    [TestMethod]
    public void BuildBugTriageTree_UntriagedFirst_ThenSeverity()
    {
        // Arrange
        var bugs = new List<WorkItemWithValidationDto>
        {
            CreateBug(1, "Untriaged Bug"),
            CreateBug(2, "Critical Triaged Bug")
        };
        var untriagedIds = new HashSet<int> { 1 };
        var expandedState = new Dictionary<int, bool>();
        var severityMap = new Dictionary<int, string>
        {
            { 1, "3 - Medium" },
            { 2, "1 - Critical" }
        };
        string? GetSeverity(WorkItemWithValidationDto bug) => severityMap[bug.TfsId];

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

        // Assert
        Assert.HasCount(2, roots);
        Assert.AreEqual("New / Untriaged (1)", roots[0].Title); // First
        Assert.AreEqual("1 - Critical (1)", roots[1].Title); // Second
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
        string? GetSeverity(WorkItemWithValidationDto bug) => "3 - Medium";

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

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
        string? GetSeverity(WorkItemWithValidationDto bug) => "3 - Medium";

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

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
        string? GetSeverity(WorkItemWithValidationDto bug) => "3 - Medium";

        // Act
        var roots = _service.BuildBugTriageTree(bugs, untriagedIds, expandedState, GetSeverity);

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
