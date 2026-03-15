using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CanonicalStoryPointResolutionServiceTests
{
    private readonly CanonicalStoryPointResolutionService _service = new();

    [TestMethod]
    public void Resolve_UsesStoryPointsWhenPresent()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 8),
            IsDone: false));

        Assert.AreEqual(8d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Real, result.Source);
    }

    [TestMethod]
    public void Resolve_UsesBusinessValueFallbackWhenStoryPointsAreMissing()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: null, businessValue: 13),
            IsDone: false));

        Assert.AreEqual(13d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Fallback, result.Source);
    }

    [TestMethod]
    public void Resolve_ReturnsMissingWhenNoEstimateExists()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: null, businessValue: null),
            IsDone: false));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void Resolve_TreatsZeroStoryPointsOnDoneItemAsValidRealEstimate()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 0, businessValue: 5),
            IsDone: true));

        Assert.AreEqual(0d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Real, result.Source);
    }

    [TestMethod]
    public void Resolve_TreatsZeroStoryPointsOnNonDoneItemAsMissing()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, storyPoints: 0, businessValue: null),
            IsDone: false));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void Resolve_DerivesMissingEstimateFromSameFeatureSiblingAverage()
    {
        var target = CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null);
        var siblings = new[]
        {
            new StoryPointResolutionCandidate(CreateWorkItem(102, parentTfsId: 10, storyPoints: 3), false),
            new StoryPointResolutionCandidate(CreateWorkItem(103, parentTfsId: 10, storyPoints: null, businessValue: 7), false),
            new StoryPointResolutionCandidate(CreateWorkItem(104, parentTfsId: 99, storyPoints: 100), false),
            new StoryPointResolutionCandidate(CreateWorkItem(105, type: WorkItemType.Bug, parentTfsId: 10, storyPoints: 50), false)
        };

        var result = _service.Resolve(new StoryPointResolutionRequest(
            target,
            IsDone: false,
            FeaturePbis: siblings));

        Assert.AreEqual(5d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Derived, result.Source);
    }

    [TestMethod]
    public void Resolve_KeepsDerivedEstimateFractional()
    {
        var target = CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null);
        var siblings = new[]
        {
            new StoryPointResolutionCandidate(CreateWorkItem(102, parentTfsId: 10, storyPoints: 3), false),
            new StoryPointResolutionCandidate(CreateWorkItem(103, parentTfsId: 10, storyPoints: 4), false)
        };

        var result = _service.Resolve(new StoryPointResolutionRequest(
            target,
            IsDone: false,
            FeaturePbis: siblings));

        Assert.AreEqual(3.5d, result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Derived, result.Source);
    }

    [TestMethod]
    public void Resolve_ReturnsMissingWhenAllFeatureSiblingsLackCanonicalEstimates()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null),
            IsDone: false,
            [
                new StoryPointResolutionCandidate(CreateWorkItem(102, parentTfsId: 10, storyPoints: null, businessValue: null), false),
                new StoryPointResolutionCandidate(CreateWorkItem(103, parentTfsId: 10, storyPoints: 0, businessValue: null), false)
            ]));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void Resolve_IgnoresBugAndTaskSiblingsWhenDerivingEstimates()
    {
        var result = _service.Resolve(new StoryPointResolutionRequest(
            CreateWorkItem(101, parentTfsId: 10, storyPoints: null, businessValue: null),
            IsDone: false,
            [
                new StoryPointResolutionCandidate(CreateWorkItem(102, type: WorkItemType.Bug, parentTfsId: 10, storyPoints: 8), true),
                new StoryPointResolutionCandidate(CreateWorkItem(103, type: WorkItemType.Task, parentTfsId: 10, storyPoints: 5), true)
            ]));

        Assert.IsNull(result.Value);
        Assert.AreEqual(StoryPointEstimateSource.Missing, result.Source);
    }

    [TestMethod]
    public void ResolveParentFallback_UsesCanonicalFieldPrecedenceForNonPbiParents()
    {
        var featureFallback = _service.ResolveParentFallback(new StoryPointFallbackRequest(
            CreateWorkItem(201, type: WorkItemType.Feature, storyPoints: null, businessValue: 8),
            IsDone: true));
        var epicStoryPoints = _service.ResolveParentFallback(new StoryPointFallbackRequest(
            CreateWorkItem(202, type: WorkItemType.Epic, storyPoints: 13, businessValue: 21),
            IsDone: false));

        Assert.AreEqual(8d, featureFallback.Value);
        Assert.AreEqual(StoryPointEstimateSource.Fallback, featureFallback.Source);
        Assert.AreEqual(13d, epicStoryPoints.Value);
        Assert.AreEqual(StoryPointEstimateSource.Real, epicStoryPoints.Source);
    }

    private static CanonicalWorkItem CreateWorkItem(
        int tfsId,
        string type = WorkItemType.Pbi,
        int? parentTfsId = null,
        int? storyPoints = null,
        int? businessValue = null)
    {
        return new CanonicalWorkItem(
            tfsId,
            type,
            parentTfsId,
            businessValue,
            storyPoints);
    }
}
