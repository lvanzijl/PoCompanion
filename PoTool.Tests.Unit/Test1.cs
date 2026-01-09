using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class WorkItemDtoTests
{
    [TestMethod]
    public void WorkItemDto_CanBeCreated_WithValidData()
    {
        // Arrange
        var tfsId = 123;
        var type = "Epic";
        var title = "Test Epic";
        var areaPath = "Project\\Team";
        var iterationPath = "Sprint 1";
        var state = "Active";
        var jsonPayload = "{}";
        var retrievedAt = DateTimeOffset.UtcNow;

        // Act
        var dto = new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: null,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            JsonPayload: jsonPayload,
            RetrievedAt: retrievedAt,
                    Effort: null
        );

        // Assert
        Assert.AreEqual(tfsId, dto.TfsId);
        Assert.AreEqual(type, dto.Type);
        Assert.AreEqual(title, dto.Title);
        Assert.AreEqual(areaPath, dto.AreaPath);
        Assert.AreEqual(iterationPath, dto.IterationPath);
        Assert.AreEqual(state, dto.State);
        Assert.AreEqual(jsonPayload, dto.JsonPayload);
        Assert.AreEqual(retrievedAt, dto.RetrievedAt);
    }

    [TestMethod]
    public void WorkItemDto_IsImmutable()
    {
        // Arrange & Act
        var dto1 = new WorkItemDto(
            TfsId: 123,
            Type: "Epic",
            Title: "Test Epic",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: null
        );

        var dto2 = dto1 with { Title = "Modified Epic" };

        // Assert
        Assert.AreNotEqual(dto1.Title, dto2.Title);
        Assert.AreEqual("Test Epic", dto1.Title);
        Assert.AreEqual("Modified Epic", dto2.Title);
    }

    [TestMethod]
    public void WorkItemDto_EqualityCheck_WorksCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dto1 = new WorkItemDto(
            TfsId: 123,
            Type: "Epic",
            Title: "Test Epic",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: now,
                    Effort: null
        );

        var dto2 = new WorkItemDto(
            TfsId: 123,
            Type: "Epic",
            Title: "Test Epic",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: now,
                    Effort: null
        );

        // Act & Assert
        Assert.AreEqual(dto1, dto2);
        Assert.IsTrue(dto1 == dto2);
    }
}
