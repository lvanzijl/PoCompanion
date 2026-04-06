using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingExecutionIntentServiceTests
{
    private readonly OnboardingExecutionIntentService _service = new();

    [TestMethod]
    public void CreateIntent_CreateBinding_RoutesToBindingsSectionWithRootContext()
    {
        var result = _service.CreateIntent(
            "Create binding for product root",
            OnboardingProblemScope.Binding,
            connectionId: 1,
            projectId: 10,
            rootId: 13,
            bindingId: null,
            section: OnboardingGraphSection.ProductRoots,
            anchorId: "section-product-roots",
            targetElementId: "root-13",
            visibleProjectCount: 1,
            visibleTeamCount: 1,
            visiblePipelineCount: 1);

        Assert.AreEqual("create-binding", result.IntentType);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.High, result.ConfidenceLevel);
        Assert.AreEqual(1, result.ConnectionId);
        Assert.AreEqual(10, result.ProjectId);
        Assert.AreEqual(13, result.RootId);
        Assert.IsNull(result.BindingId);
        Assert.AreEqual(OnboardingGraphSection.Bindings, result.NavigationTarget.Section);
        Assert.AreEqual("/home/onboarding?onboardingIntentType=create-binding&onboardingSection=Bindings&onboardingTarget=root-13&onboardingConnectionId=1&onboardingProjectId=10&onboardingRootId=13", result.NavigationTarget.Route);
        CollectionAssert.AreEquivalent(
            new[] { OnboardingGraphSection.ProductRoots, OnboardingGraphSection.Bindings },
            result.NavigationTarget.ExpandedSections.ToArray());
    }

    [TestMethod]
    public void CreateIntent_AssignPipeline_WithMultipleVisiblePipelines_ReturnsMediumConfidence()
    {
        var result = _service.CreateIntent(
            "Assign pipeline to project",
            OnboardingProblemScope.Project,
            connectionId: 1,
            projectId: 10,
            rootId: null,
            bindingId: null,
            section: OnboardingGraphSection.Projects,
            anchorId: "section-projects",
            targetElementId: "project-10",
            visibleProjectCount: 1,
            visibleTeamCount: 1,
            visiblePipelineCount: 3);

        Assert.AreEqual("assign-pipeline", result.IntentType);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.Medium, result.ConfidenceLevel);
        Assert.AreEqual(OnboardingGraphSection.Pipelines, result.NavigationTarget.Section);
        CollectionAssert.AreEquivalent(
            new[] { OnboardingGraphSection.Projects, OnboardingGraphSection.Pipelines },
            result.NavigationTarget.ExpandedSections.ToArray());
    }

    [TestMethod]
    public void CreateIntent_AssignTeam_RoutesToTeamsSection()
    {
        var result = _service.CreateIntent(
            "Assign team to project",
            OnboardingProblemScope.Project,
            connectionId: 1,
            projectId: 10,
            rootId: null,
            bindingId: null,
            section: OnboardingGraphSection.Projects,
            anchorId: "section-projects",
            targetElementId: "project-10",
            visibleProjectCount: 1,
            visibleTeamCount: 3,
            visiblePipelineCount: 1);

        Assert.AreEqual("assign-team", result.IntentType);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.Medium, result.ConfidenceLevel);
        Assert.AreEqual(OnboardingGraphSection.Teams, result.NavigationTarget.Section);
        CollectionAssert.AreEquivalent(
            new[] { OnboardingGraphSection.Projects, OnboardingGraphSection.Teams },
            result.NavigationTarget.ExpandedSections.ToArray());
    }

    [TestMethod]
    public void CreateIntent_FallbackSuggestion_ReturnsFallbackConfidence()
    {
        var result = _service.CreateIntent(
            "Resolve validation issue",
            OnboardingProblemScope.Root,
            connectionId: 1,
            projectId: 10,
            rootId: 13,
            bindingId: null,
            section: OnboardingGraphSection.ProductRoots,
            anchorId: "section-product-roots",
            targetElementId: "root-13",
            visibleProjectCount: 1,
            visibleTeamCount: 0,
            visiblePipelineCount: 0);

        Assert.AreEqual("resolve-validation", result.IntentType);
        Assert.AreEqual(OnboardingExecutionConfidenceLevel.Fallback, result.ConfidenceLevel);
        Assert.AreEqual("Resolve validation issue", result.SuggestedActionLabel);
    }
}
