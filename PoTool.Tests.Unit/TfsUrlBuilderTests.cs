using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class TfsUrlBuilderTests
{
    [TestMethod]
    public void BuildWorkItemUrl_ValidInputs_ReturnsCorrectUrl()
    {
        // Arrange
        var organizationUrl = "https://dev.azure.com/myorg";
        var project = "MyProject";
        var workItemId = 12345;
        var expected = "https://dev.azure.com/myorg/MyProject/_workitems/edit/12345";

        // Act
        var result = TfsUrlBuilder.BuildWorkItemUrl(organizationUrl, project, workItemId);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BuildWorkItemUrl_WithTrailingSlash_RemovesSlash()
    {
        // Arrange
        var organizationUrl = "https://dev.azure.com/myorg/";
        var project = "MyProject";
        var workItemId = 12345;
        var expected = "https://dev.azure.com/myorg/MyProject/_workitems/edit/12345";

        // Act
        var result = TfsUrlBuilder.BuildWorkItemUrl(organizationUrl, project, workItemId);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BuildWorkItemUrl_WithSpacesInProject_EncodesProjectName()
    {
        // Arrange
        var organizationUrl = "https://dev.azure.com/myorg";
        var project = "My Project Name";
        var workItemId = 12345;
        var expected = "https://dev.azure.com/myorg/My%20Project%20Name/_workitems/edit/12345";

        // Act
        var result = TfsUrlBuilder.BuildWorkItemUrl(organizationUrl, project, workItemId);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BuildWorkItemUrl_NullOrganizationUrl_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl(null!, "MyProject", 12345));
    }

    [TestMethod]
    public void BuildWorkItemUrl_EmptyOrganizationUrl_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl(string.Empty, "MyProject", 12345));
    }

    [TestMethod]
    public void BuildWorkItemUrl_NullProject_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl("https://dev.azure.com/myorg", null!, 12345));
    }

    [TestMethod]
    public void BuildWorkItemUrl_EmptyProject_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl("https://dev.azure.com/myorg", string.Empty, 12345));
    }

    [TestMethod]
    public void BuildWorkItemUrl_ZeroWorkItemId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl("https://dev.azure.com/myorg", "MyProject", 0));
    }

    [TestMethod]
    public void BuildWorkItemUrl_NegativeWorkItemId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentException>(() => 
            TfsUrlBuilder.BuildWorkItemUrl("https://dev.azure.com/myorg", "MyProject", -1));
    }

    [TestMethod]
    public void BuildWorkItemUrls_ValidInputs_ReturnsCorrectUrls()
    {
        // Arrange
        var organizationUrl = "https://dev.azure.com/myorg";
        var project = "MyProject";
        var workItemIds = new[] { 123, 456, 789 };
        var expected = new[]
        {
            "https://dev.azure.com/myorg/MyProject/_workitems/edit/123",
            "https://dev.azure.com/myorg/MyProject/_workitems/edit/456",
            "https://dev.azure.com/myorg/MyProject/_workitems/edit/789"
        };

        // Act
        var result = TfsUrlBuilder.BuildWorkItemUrls(organizationUrl, project, workItemIds);

        // Assert
        CollectionAssert.AreEqual(expected, result);
    }

    [TestMethod]
    public void BuildWorkItemUrls_EmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        var organizationUrl = "https://dev.azure.com/myorg";
        var project = "MyProject";
        var workItemIds = Array.Empty<int>();

        // Act
        var result = TfsUrlBuilder.BuildWorkItemUrls(organizationUrl, project, workItemIds);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void BuildWorkItemUrls_NullCollection_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            TfsUrlBuilder.BuildWorkItemUrls("https://dev.azure.com/myorg", "MyProject", null!));
    }
}
