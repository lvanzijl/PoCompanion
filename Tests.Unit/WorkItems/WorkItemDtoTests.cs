using Core.WorkItems;

namespace Tests.Unit.WorkItems;

[TestClass]
public sealed class WorkItemDtoTests
{
    [TestMethod]
    public void WorkItemDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        
        // Act
        var dto = new WorkItemDto
        {
            TfsId = 12345,
            Type = "Epic",
            Title = "Test Epic",
            AreaPath = "Project\\Area",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = now
        };

        // Assert
        Assert.AreEqual(12345, dto.TfsId);
        Assert.AreEqual("Epic", dto.Type);
        Assert.AreEqual("Test Epic", dto.Title);
        Assert.AreEqual("Project\\Area", dto.AreaPath);
        Assert.AreEqual("Project\\Sprint 1", dto.IterationPath);
        Assert.AreEqual("Active", dto.State);
        Assert.AreEqual("{}", dto.JsonPayload);
        Assert.AreEqual(now, dto.RetrievedAt);
    }

    [TestMethod]
    public void WorkItemDto_IsImmutable()
    {
        // Arrange
        var dto = new WorkItemDto
        {
            TfsId = 12345,
            Type = "Feature",
            Title = "Test Feature",
            AreaPath = "Project\\Area",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = DateTimeOffset.UtcNow
        };

        // Act & Assert - Verify that with syntax creates new instance
        var modified = dto with { Title = "Modified Title" };
        
        Assert.AreEqual("Test Feature", dto.Title);
        Assert.AreEqual("Modified Title", modified.Title);
        Assert.AreNotSame(dto, modified);
    }

    [TestMethod]
    public void WorkItemDto_SupportsRecordEquality()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var dto1 = new WorkItemDto
        {
            TfsId = 12345,
            Type = "PBI",
            Title = "Test PBI",
            AreaPath = "Project\\Area",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = now
        };

        var dto2 = new WorkItemDto
        {
            TfsId = 12345,
            Type = "PBI",
            Title = "Test PBI",
            AreaPath = "Project\\Area",
            IterationPath = "Project\\Sprint 1",
            State = "Active",
            JsonPayload = "{}",
            RetrievedAt = now
        };

        // Act & Assert
        Assert.AreEqual(dto1, dto2);
        Assert.IsTrue(dto1 == dto2);
    }
}
