using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingActionSuggestionServiceTests
{
    private readonly OnboardingActionSuggestionService _service = new();

    [TestMethod]
    public void GetSuggestedAction_ReturnsKnownConnectionSuggestion()
    {
        var result = _service.GetSuggestedAction(
            OnboardingProblemScope.Global,
            "Connection required",
            "Connection required",
            "Onboarding workspace");

        Assert.AreEqual("Create or select a connection", result);
    }

    [TestMethod]
    public void GetSuggestedAction_ReturnsKnownBindingSuggestion()
    {
        var result = _service.GetSuggestedAction(
            OnboardingProblemScope.Binding,
            "Project binding requires attention",
            "Binding validation failed",
            "Project binding");

        Assert.AreEqual("Create binding for product root", result);
    }

    [TestMethod]
    public void GetSuggestedAction_ReturnsFallbackSuggestion_WhenReasonIsUnknown()
    {
        var result = _service.GetSuggestedAction(
            OnboardingProblemScope.Project,
            "Unexpected issue",
            "Something custom happened",
            "Project One");

        Assert.AreEqual("Resolve validation issue", result);
    }
}
