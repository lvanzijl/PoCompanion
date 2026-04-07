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
    public void GetSuggestedAction_ReturnsPipelineReplacementSuggestion_ForInvalidPipelineBinding()
    {
        var result = _service.GetSuggestedAction(
            OnboardingProblemScope.Binding,
            "The pipeline binding references a pipeline source that is not enabled and valid.",
            "The pipeline binding references a pipeline source that is not enabled and valid.",
            "Pipeline binding");

        Assert.AreEqual("Assign pipeline to project", result);
    }

    [TestMethod]
    public void GetSuggestedAction_KeepsTeamReplacementSuggestion_ForInvalidTeamBinding()
    {
        var result = _service.GetSuggestedAction(
            OnboardingProblemScope.Binding,
            "The team binding references a team source that is not enabled and valid.",
            "The team binding references a team source that is not enabled and valid.",
            "Team binding");

        Assert.AreEqual("Assign team to project", result);
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
