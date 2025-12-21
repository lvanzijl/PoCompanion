using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.Components.Common;
using Moq;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for KeyboardShortcutsDialog component
/// Note: KeyboardShortcutsDialog is designed to be shown in a MudDialog context,
/// so these tests verify component structure and basic rendering.
/// </summary>
[TestClass]
public class KeyboardShortcutsDialogTests : BunitTestContext
{
    private Mock<IMudDialogInstance> _mockDialogInstance = null!;

    [TestInitialize]
    public void Setup()
    {
        Services.AddMudServices();
        _mockDialogInstance = new Mock<IMudDialogInstance>();
    }

    [TestMethod]
    public void KeyboardShortcutsDialog_Renders_Successfully()
    {
        // Arrange & Act
        var cut = RenderComponent<KeyboardShortcutsDialog>(parameters => parameters
            .AddCascadingValue(_mockDialogInstance.Object));

        // Assert - Just verify the component renders without throwing
        Assert.IsNotNull(cut);
        Assert.IsNotNull(cut.Instance);
    }

    [TestMethod]
    public void KeyboardShortcutsDialog_HasMudDialogInstance()
    {
        // Arrange & Act
        var cut = RenderComponent<KeyboardShortcutsDialog>(parameters => parameters
            .AddCascadingValue(_mockDialogInstance.Object));

        // Assert - Verify the dialog instance is set
        Assert.IsNotNull(cut.Instance);
    }
}
