using PoTool.Client.Helpers;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class ConfigurationImportUiHelperTests
{
    [TestMethod]
    public void TryValidateSelectedJson_ReturnsTrue_ForValidJson()
    {
        var isValid = ConfigurationImportUiHelper.TryValidateSelectedJson("{\"profiles\":[]}", out var validationError);

        Assert.IsTrue(isValid);
        Assert.IsNull(validationError);
    }

    [TestMethod]
    public void TryValidateSelectedJson_ReturnsFalse_ForInvalidJson()
    {
        var isValid = ConfigurationImportUiHelper.TryValidateSelectedJson("{ invalid", out var validationError);

        Assert.IsFalse(isValid);
        Assert.AreEqual("The selected file is not a valid configuration export.", validationError);
    }

    [TestMethod]
    public void CanImport_ReturnsFalse_WhenImportIsBusy()
    {
        var canImport = ConfigurationImportUiHelper.CanImport(
            isBusy: true,
            selectedJson: "{\"profiles\":[]}",
            validationError: null);

        Assert.IsFalse(canImport);
    }

    [TestMethod]
    public void CanImport_ReturnsTrue_WhenJsonIsPresentAndValidationPassed()
    {
        var canImport = ConfigurationImportUiHelper.CanImport(
            isBusy: false,
            selectedJson: "{\"profiles\":[]}",
            validationError: null);

        Assert.IsTrue(canImport);
    }

    [TestMethod]
    public void CanImport_ReturnsFalse_WhenValidationFailed()
    {
        var canImport = ConfigurationImportUiHelper.CanImport(
            isBusy: false,
            selectedJson: "{\"profiles\":[]}",
            validationError: "The selected file is not a valid configuration export.");

        Assert.IsFalse(canImport);
    }
}
