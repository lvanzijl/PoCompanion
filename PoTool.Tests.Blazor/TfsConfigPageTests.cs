using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.Pages;
using PoTool.Client.Services;
using Moq;
using PoTool.Api.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for TfsConfig page component
/// </summary>
[TestClass]
public class TfsConfigPageTests : BunitTestContext
{
    [TestMethod]
    public void TfsConfig_RendersFormElements()
    {
        // Arrange
        var mockService = new Mock<ITfsConfigurationService>();
        mockService.Setup(s => s.GetConfigAsync())
            .ReturnsAsync((TfsConfig?)null);

        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<TfsConfig>();

        // Assert
        Assert.IsTrue(cut.Markup.Contains("TFS Configuration") || cut.Markup.Contains("Azure DevOps"), 
            "Should show page title");
        // Form inputs should be present
        var inputs = cut.FindAll("input");
        Assert.IsTrue(inputs.Count >= 3, "Should have at least 3 input fields (URL, Project, PAT)");
    }

    [TestMethod]
    public void TfsConfig_DisplaysSaveButton()
    {
        // Arrange
        var mockService = new Mock<ITfsConfigurationService>();
        mockService.Setup(s => s.GetConfigAsync())
            .ReturnsAsync((TfsConfig?)null);

        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<TfsConfig>();

        // Assert
        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Save"));
        Assert.IsNotNull(saveButton, "Should have a Save button");
    }

    [TestMethod]
    public void TfsConfig_LoadsExistingConfiguration()
    {
        // Arrange
        var existingConfig = new TfsConfig
        {
            Url = "https://dev.azure.com/myorg",
            Project = "MyProject"
        };

        var mockService = new Mock<ITfsConfigurationService>();
        mockService.Setup(s => s.GetConfigAsync())
            .ReturnsAsync(existingConfig);

        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<TfsConfig>();

        // Assert
        Assert.IsTrue(cut.Markup.Contains("https://dev.azure.com/myorg") || 
                      cut.Markup.Contains("myorg"), 
            "Should display existing URL");
        Assert.IsTrue(cut.Markup.Contains("MyProject"), "Should display existing project name");
    }
}
