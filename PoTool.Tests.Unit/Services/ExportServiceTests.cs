using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ExportServiceTests
{
    private ExportService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ExportService();
    }

    [TestMethod]
    public void ExportToCsv_EmptyCollection_ReturnsEmptyString()
    {
        // Arrange
        var workItems = Array.Empty<WorkItemDto>();

        // Act
        var result = _service.ExportToCsv(workItems);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void ExportToCsv_NullCollection_ReturnsEmptyString()
    {
        // Act
        var result = _service.ExportToCsv(null!);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void ExportToCsv_SingleItem_ReturnsCorrectCsv()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Test Item",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5.0,
                RetrievedAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.ExportToCsv(workItems);

        // Assert
        Assert.IsTrue(result.Contains("ID,Title,Type,State,Area Path,Iteration Path,Parent ID,Effort,Retrieved At"));
        Assert.IsTrue(result.Contains("123"));
        Assert.IsTrue(result.Contains("Test Item"));
        Assert.IsTrue(result.Contains("Epic"));
        Assert.IsTrue(result.Contains("Active"));
    }

    [TestMethod]
    public void ExportToCsv_ItemWithCommas_EscapesCorrectly()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Test, with, commas",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = null,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.ExportToCsv(workItems);

        // Assert
        Assert.IsTrue(result.Contains("\"Test, with, commas\""));
    }

    [TestMethod]
    public void ExportToCsv_MultipleItems_ReturnsAllItems()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Item 1",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5.0,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 456,
                Title = "Item 2",
                Type = "Feature",
                State = "Done",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 2",
                ParentTfsId = 123,
                Effort = 3.0,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.ExportToCsv(workItems);

        // Assert
        Assert.IsTrue(result.Contains("123"));
        Assert.IsTrue(result.Contains("456"));
        Assert.IsTrue(result.Contains("Item 1"));
        Assert.IsTrue(result.Contains("Item 2"));
    }

    [TestMethod]
    public void ExportToJson_EmptyCollection_ReturnsEmptyArray()
    {
        // Arrange
        var workItems = Array.Empty<WorkItemDto>();

        // Act
        var result = _service.ExportToJson(workItems);

        // Assert
        Assert.IsTrue(result.Contains("[]"));
    }

    [TestMethod]
    public void ExportToJson_NullCollection_ReturnsEmptyArray()
    {
        // Act
        var result = _service.ExportToJson(null!);

        // Assert
        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public void ExportToJson_SingleItem_ReturnsValidJson()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Test Item",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5.0,
                RetrievedAt = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.ExportToJson(workItems);

        // Assert
        Assert.IsTrue(result.Contains("\"tfsId\": 123"));
        Assert.IsTrue(result.Contains("\"title\": \"Test Item\""));
        Assert.IsTrue(result.Contains("\"type\": \"Epic\""));
    }

    [TestMethod]
    public void ExportToJson_MultipleItems_ReturnsValidJsonArray()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Item 1",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5.0,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 456,
                Title = "Item 2",
                Type = "Feature",
                State = "Done",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 2",
                ParentTfsId = 123,
                Effort = 3.0,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.ExportToJson(workItems);

        // Assert
        Assert.IsTrue(result.Contains("\"tfsId\": 123"));
        Assert.IsTrue(result.Contains("\"tfsId\": 456"));
    }
}
