using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for OnboardingWizardState service that manages TFS verification gating.
/// </summary>
[TestClass]
public class OnboardingWizardStateTests
{
    private OnboardingWizardState _state = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = new OnboardingWizardState();
    }

    [TestMethod]
    public void InitialState_NotVerified()
    {
        // Assert
        Assert.IsFalse(_state.TfsVerified);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void MarkTfsVerified_SetsVerifiedTrue()
    {
        // Act
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Assert
        Assert.IsTrue(_state.TfsVerified);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void MarkTfsUnverified_SetsVerifiedFalse()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Act
        _state.MarkTfsUnverified();

        // Assert
        Assert.IsFalse(_state.TfsVerified);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_SameValues_ReturnsTrue()
    {
        // Arrange
        var url = "https://dev.azure.com/org";
        var project = "Project";
        var areaPath = "Area\\Path";
        _state.MarkTfsVerified(url, project, areaPath);

        // Act
        var unchanged = _state.CheckTfsFieldsUnchanged(url, project, areaPath);

        // Assert
        Assert.IsTrue(unchanged);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_DifferentUrl_ReturnsFalseAndMarksDirty()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Act
        var unchanged = _state.CheckTfsFieldsUnchanged("https://dev.azure.com/other", "Project", "Area\\Path");

        // Assert
        Assert.IsFalse(unchanged);
        Assert.IsTrue(_state.TfsDirty);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_DifferentProject_ReturnsFalseAndMarksDirty()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Act
        var unchanged = _state.CheckTfsFieldsUnchanged("https://dev.azure.com/org", "OtherProject", "Area\\Path");

        // Assert
        Assert.IsFalse(unchanged);
        Assert.IsTrue(_state.TfsDirty);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_DifferentAreaPath_ReturnsFalseAndMarksDirty()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Act
        var unchanged = _state.CheckTfsFieldsUnchanged("https://dev.azure.com/org", "Project", "Other\\Path");

        // Assert
        Assert.IsFalse(unchanged);
        Assert.IsTrue(_state.TfsDirty);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_NotVerified_ReturnsFalse()
    {
        // Act
        var unchanged = _state.CheckTfsFieldsUnchanged("https://dev.azure.com/org", "Project", "Area\\Path");

        // Assert
        Assert.IsFalse(unchanged);
    }

    [TestMethod]
    public void CheckTfsFieldsUnchanged_WhitespaceHandling_IgnoresWhitespace()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");

        // Act - add trailing/leading whitespace
        var unchanged = _state.CheckTfsFieldsUnchanged(
            " https://dev.azure.com/org ",
            " Project ",
            " Area\\Path ");

        // Assert - should treat as same after trimming
        Assert.IsTrue(unchanged);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void Reset_ClearsAllState()
    {
        // Arrange
        _state.MarkTfsVerified("https://dev.azure.com/org", "Project", "Area\\Path");
        _state.CheckTfsFieldsUnchanged("https://dev.azure.com/other", "Project", "Area\\Path"); // Makes it dirty

        // Act
        _state.Reset();

        // Assert
        Assert.IsFalse(_state.TfsVerified);
        Assert.IsFalse(_state.TfsDirty);
    }

    [TestMethod]
    public void GatingScenario_InitiallyDisabled()
    {
        // Simulates wizard starting - Next should be disabled
        // Using realistic values to ensure fingerprinting works correctly
        var canProceed = _state.TfsVerified && _state.CheckTfsFieldsUnchanged(
            "https://dev.azure.com/org",
            "TestProject", 
            "TestArea\\Path");
        Assert.IsFalse(canProceed);
    }

    [TestMethod]
    public void GatingScenario_AfterSuccessfulVerify_Enabled()
    {
        // Arrange
        var url = "https://dev.azure.com/org";
        var project = "Project";
        var areaPath = "Area\\Path";

        // Act - simulate successful save+verify
        _state.MarkTfsVerified(url, project, areaPath);

        // Assert - Next should be enabled
        var canProceed = _state.TfsVerified && _state.CheckTfsFieldsUnchanged(url, project, areaPath);
        Assert.IsTrue(canProceed);
    }

    [TestMethod]
    public void GatingScenario_AfterEditingField_DisabledAgain()
    {
        // Arrange
        var url = "https://dev.azure.com/org";
        var project = "Project";
        var areaPath = "Area\\Path";
        _state.MarkTfsVerified(url, project, areaPath);

        // Act - simulate user editing URL field
        _state.MarkTfsUnverified();
        var newUrl = "https://dev.azure.com/other";

        // Assert - Next should be disabled
        var canProceed = _state.TfsVerified && _state.CheckTfsFieldsUnchanged(newUrl, project, areaPath);
        Assert.IsFalse(canProceed);
    }

    [TestMethod]
    public void GatingScenario_AfterFailedVerify_StaysDisabled()
    {
        // Arrange
        var url = "https://dev.azure.com/org";
        var project = "Project";
        var areaPath = "Area\\Path";

        // Act - simulate failed save+verify
        _state.MarkTfsUnverified();

        // Assert - Next should be disabled
        var canProceed = _state.TfsVerified && _state.CheckTfsFieldsUnchanged(url, project, areaPath);
        Assert.IsFalse(canProceed);
    }
}
