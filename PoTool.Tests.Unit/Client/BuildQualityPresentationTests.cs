using PoTool.Client.Components.Common;

namespace PoTool.Tests.Unit.Client;

[TestClass]
public sealed class BuildQualityPresentationTests
{
    [TestMethod]
    public void FormatPercent_ReturnsUnknown_WhenExplicitUnknownFlagIsTrue()
    {
        Assert.AreEqual("Unknown", BuildQualityPresentation.FormatPercent(0.96d, isUnknown: true));
    }

    [TestMethod]
    public void FormatPercent_ReturnsDash_WhenValueIsMissingWithoutUnknownFlag()
    {
        Assert.AreEqual("—", BuildQualityPresentation.FormatPercent(null, isUnknown: false));
    }

    [TestMethod]
    public void FormatPercent_ReturnsPercent_WhenValueExistsAndUnknownFlagIsFalse()
    {
        Assert.AreEqual("96%", BuildQualityPresentation.FormatPercent(0.96d, isUnknown: false).Replace(" ", string.Empty));
    }

    [TestMethod]
    public void GetUnknownReasonText_ReturnsFriendlyMessage_ForKnownReason()
    {
        var message = BuildQualityPresentation.GetUnknownReasonText("NoCoverage");

        Assert.AreEqual("No coverage data is available in scope.", message);
    }

    [TestMethod]
    public void FormatThresholdStatus_ReturnsFriendlyStateLabel()
    {
        Assert.AreEqual("met", BuildQualityPresentation.FormatThresholdStatus(true));
        Assert.AreEqual("not met", BuildQualityPresentation.FormatThresholdStatus(false));
    }
}
