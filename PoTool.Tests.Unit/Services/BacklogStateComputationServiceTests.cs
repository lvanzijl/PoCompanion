using PoTool.Core.Health;
using PoTool.Core.WorkItems;
using PoTool.Shared.Health;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BacklogStateComputationServiceTests
{
    private BacklogStateComputationService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new BacklogStateComputationService();
    }

    #region PBI scoring

    [TestMethod]
    public void ComputePbiScore_DescriptionEmpty_Returns0()
    {
        var pbi = CreateWorkItem(1, WorkItemType.Pbi, description: null, effort: null);
        var result = _service.ComputePbiScore(pbi);
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(1, result.TfsId);
    }

    [TestMethod]
    public void ComputePbiScore_DescriptionWhitespace_Returns0()
    {
        var pbi = CreateWorkItem(1, WorkItemType.Pbi, description: "   ", effort: null);
        var result = _service.ComputePbiScore(pbi);
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public void ComputePbiScore_DescriptionOkEffortMissing_Returns75()
    {
        var pbi = CreateWorkItem(1, WorkItemType.Pbi, description: "A PBI with description", effort: null);
        var result = _service.ComputePbiScore(pbi);
        Assert.AreEqual(75, result.Score);
    }

    [TestMethod]
    public void ComputePbiScore_DescriptionOkEffortZero_Returns75()
    {
        var pbi = CreateWorkItem(1, WorkItemType.Pbi, description: "A PBI with description", effort: 0);
        var result = _service.ComputePbiScore(pbi);
        Assert.AreEqual(75, result.Score);
    }

    [TestMethod]
    public void ComputePbiScore_DescriptionAndEffortPresent_Returns100()
    {
        var pbi = CreateWorkItem(1, WorkItemType.Pbi, description: "A PBI with description", effort: 5);
        var result = _service.ComputePbiScore(pbi);
        Assert.AreEqual(100, result.Score);
    }

    [TestMethod]
    public void ComputePbiScore_ThrowsOnNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _service.ComputePbiScore(null!));
    }

    #endregion

    #region Feature scoring

    [TestMethod]
    public void ComputeFeatureScore_DescriptionEmpty_Returns0_OwnerPO()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: null, effort: null);
        var result = _service.ComputeFeatureScore(feature, Array.Empty<WorkItemDto>());
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(FeatureOwnerState.PO, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_DescriptionWhitespace_Returns0_OwnerPO()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "  ", effort: null);
        var result = _service.ComputeFeatureScore(feature, Array.Empty<WorkItemDto>());
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(FeatureOwnerState.PO, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_DescriptionOkNoPbis_Returns25_OwnerTeam()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature with description", effort: null);
        var result = _service.ComputeFeatureScore(feature, Array.Empty<WorkItemDto>());
        Assert.AreEqual(25, result.Score);
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_DescriptionOkAllPbisScore100_Returns100_OwnerReady()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature with description", effort: null);
        var pbi1 = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 3, parentId: 10);
        var pbi2 = CreateWorkItem(102, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10);
        var items = new[] { feature, pbi1, pbi2 };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(100, result.Score);
        Assert.AreEqual(FeatureOwnerState.Ready, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_DescriptionOkMixedPbis_ReturnsAverage_OwnerTeam()
    {
        // One PBI at 100, one at 75 → average = 88 (rounded)
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var pbi1 = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 3, parentId: 10);   // 100
        var pbi2 = CreateWorkItem(102, WorkItemType.Pbi, description: "PBI desc", effort: null, parentId: 10); // 75
        var items = new[] { feature, pbi1, pbi2 };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(88, result.Score); // Math.Round((100 + 75) / 2.0) = 88
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_DescriptionOkAllPbisScore0_Returns0_OwnerTeam()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var pbi1 = CreateWorkItem(101, WorkItemType.Pbi, description: null, effort: null, parentId: 10); // 0
        var pbi2 = CreateWorkItem(102, WorkItemType.Pbi, description: null, effort: null, parentId: 10); // 0
        var items = new[] { feature, pbi1, pbi2 };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_OnlyCountsPbisNotOtherTypes()
    {
        // Task children should not be counted as PBIs
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var task = CreateWorkItem(101, WorkItemType.Task, description: "Task", effort: 5, parentId: 10);
        var items = new[] { feature, task };

        var result = _service.ComputeFeatureScore(feature, items);

        // No PBIs → 25
        Assert.AreEqual(25, result.Score);
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_OnlyCountsDirectPbiChildren()
    {
        // PBI under a different feature should not affect this one
        var feature1 = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var feature2 = CreateWorkItem(11, WorkItemType.Feature, description: "Other feature", effort: null);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 11); // child of feature2
        var items = new[] { feature1, feature2, pbi };

        var result = _service.ComputeFeatureScore(feature1, items);

        // feature1 has no direct PBI children → 25
        Assert.AreEqual(25, result.Score);
    }

    [TestMethod]
    public void ComputeFeatureScore_ThrowsOnNullFeature()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.ComputeFeatureScore(null!, Array.Empty<WorkItemDto>()));
    }

    [TestMethod]
    public void ComputeFeatureScore_ThrowsOnNullItems()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "desc", effort: null);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.ComputeFeatureScore(feature, null!));
    }

    #endregion

    #region Epic scoring

    [TestMethod]
    public void ComputeEpicScore_DescriptionEmpty_Returns0()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: null, effort: null);
        var result = _service.ComputeEpicScore(epic, Array.Empty<WorkItemDto>());
        Assert.AreEqual(0, result.Score);
        Assert.AreEqual(1, result.TfsId);
    }

    [TestMethod]
    public void ComputeEpicScore_DescriptionWhitespace_Returns0()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "  ", effort: null);
        var result = _service.ComputeEpicScore(epic, Array.Empty<WorkItemDto>());
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public void ComputeEpicScore_DescriptionOkNoFeatures_Returns30()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic description", effort: null);
        var result = _service.ComputeEpicScore(epic, Array.Empty<WorkItemDto>());
        Assert.AreEqual(30, result.Score);
    }

    [TestMethod]
    public void ComputeEpicScore_DescriptionOkWithFeatures_ReturnsAverageFeatureScore()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var feature1 = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 1);
        var feature2 = CreateWorkItem(11, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 1);
        // Both features have no PBIs → score 25 each
        var items = new[] { epic, feature1, feature2 };

        var result = _service.ComputeEpicScore(epic, items);

        Assert.AreEqual(25, result.Score); // (25 + 25) / 2 = 25
    }

    [TestMethod]
    public void ComputeEpicScore_EpicEffortDoesNotInfluenceScore()
    {
        // Epic effort is present but must NOT influence the refinement score
        var epicWithEffort = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: 100, parentId: null);
        var epicWithoutEffort = CreateWorkItem(2, WorkItemType.Epic, description: "Epic desc", effort: null, parentId: null);
        var feature1 = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 1);
        var feature2 = CreateWorkItem(11, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 2);
        var allItems = new[] { epicWithEffort, epicWithoutEffort, feature1, feature2 };

        var scoreWithEffort = _service.ComputeEpicScore(epicWithEffort, allItems);
        var scoreWithoutEffort = _service.ComputeEpicScore(epicWithoutEffort, allItems);

        Assert.AreEqual(scoreWithEffort.Score, scoreWithoutEffort.Score, "Epic effort must not affect refinement score");
    }

    [TestMethod]
    public void ComputeEpicScore_AllFeaturesAt100_Returns100()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 1);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10); // 100
        var items = new[] { epic, feature, pbi };

        var result = _service.ComputeEpicScore(epic, items);

        Assert.AreEqual(100, result.Score);
    }

    [TestMethod]
    public void ComputeEpicScore_OnlyCountsDirectFeatureChildren()
    {
        // Feature belonging to a different epic must not affect this epic's score
        var epic1 = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var epic2 = CreateWorkItem(2, WorkItemType.Epic, description: "Other epic", effort: null);
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 2);
        var items = new[] { epic1, epic2, feature };

        var result = _service.ComputeEpicScore(epic1, items);

        // epic1 has no direct Feature children → 30
        Assert.AreEqual(30, result.Score);
    }

    [TestMethod]
    public void ComputeEpicScore_ThrowsOnNullEpic()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.ComputeEpicScore(null!, Array.Empty<WorkItemDto>()));
    }

    [TestMethod]
    public void ComputeEpicScore_ThrowsOnNullItems()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.ComputeEpicScore(epic, null!));
    }

    #endregion

    #region OwnerState transitions

    [TestMethod]
    public void OwnerState_DescriptionMissing_IsPO()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: null, effort: null);
        var result = _service.ComputeFeatureScore(feature, Array.Empty<WorkItemDto>());
        Assert.AreEqual(FeatureOwnerState.PO, result.OwnerState);
    }

    [TestMethod]
    public void OwnerState_DescriptionOkNoPbis_IsTeam()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Some desc", effort: null);
        var result = _service.ComputeFeatureScore(feature, Array.Empty<WorkItemDto>());
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void OwnerState_DescriptionOkPartialPbis_IsTeam()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Some desc", effort: null);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI", effort: null, parentId: 10); // 75
        var items = new[] { feature, pbi };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void OwnerState_AllPbisFullyRefined_IsReady()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Some desc", effort: null);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI", effort: 5, parentId: 10); // 100
        var items = new[] { feature, pbi };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(FeatureOwnerState.Ready, result.OwnerState);
    }

    #endregion

    #region Done-item scoring (done items count as 100%)

    [TestMethod]
    public void ComputeFeatureScore_DonePbi_CountsAs100()
    {
        // Feature has one done PBI (no description, would score 0) and one incomplete PBI (75).
        // Done PBI should count as 100, so average = (100 + 75) / 2 = 88.
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var donePbi = CreateWorkItem(101, WorkItemType.Pbi, description: null, effort: null, parentId: 10); // would be 0
        var activePbi = CreateWorkItem(102, WorkItemType.Pbi, description: "desc", effort: null, parentId: 10); // 75
        var items = new[] { feature, donePbi, activePbi };
        var doneIds = new HashSet<int> { 101 };

        var result = _service.ComputeFeatureScore(feature, items, doneIds);

        Assert.AreEqual(88, result.Score); // Math.Round((100 + 75) / 2.0) = 88
        Assert.AreEqual(FeatureOwnerState.Team, result.OwnerState);
    }

    [TestMethod]
    public void ComputeFeatureScore_AllPbisDone_Returns100_OwnerReady()
    {
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var pbi1 = CreateWorkItem(101, WorkItemType.Pbi, description: null, effort: null, parentId: 10); // would be 0
        var pbi2 = CreateWorkItem(102, WorkItemType.Pbi, description: "desc", effort: null, parentId: 10); // would be 75
        var items = new[] { feature, pbi1, pbi2 };
        var doneIds = new HashSet<int> { 101, 102 };

        var result = _service.ComputeFeatureScore(feature, items, doneIds);

        Assert.AreEqual(100, result.Score);
        Assert.AreEqual(FeatureOwnerState.Ready, result.OwnerState);
    }

    [TestMethod]
    public void ComputeEpicScore_DoneFeature_CountsAs100()
    {
        // Epic with one done feature (would score 0, no description) and one active feature at 100.
        // Done feature counts as 100, so average = (100 + 100) / 2 = 100.
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var doneFeature = CreateWorkItem(10, WorkItemType.Feature, description: null, effort: null, parentId: 1); // would be 0
        var activeFeature = CreateWorkItem(11, WorkItemType.Feature, description: "desc", effort: null, parentId: 1);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 11); // 100
        var items = new[] { epic, doneFeature, activeFeature, pbi };
        var doneIds = new HashSet<int> { 10 };

        var result = _service.ComputeEpicScore(epic, items, doneIds);

        Assert.AreEqual(100, result.Score);
    }

    [TestMethod]
    public void ComputeEpicScore_AllFeaturesDone_Returns100()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var feature1 = CreateWorkItem(10, WorkItemType.Feature, description: null, effort: null, parentId: 1); // would be 0
        var feature2 = CreateWorkItem(11, WorkItemType.Feature, description: null, effort: null, parentId: 1); // would be 0
        var items = new[] { epic, feature1, feature2 };
        var doneIds = new HashSet<int> { 10, 11 };

        var result = _service.ComputeEpicScore(epic, items, doneIds);

        Assert.AreEqual(100, result.Score);
    }

    [TestMethod]
    public void ComputeFeatureScore_EmptyDoneIds_BehavesLikeOriginalOverload()
    {
        // Using empty doneItemIds should produce the same result as the parameterless overload.
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10); // 100
        var items = new[] { feature, pbi };

        var resultOld = _service.ComputeFeatureScore(feature, items);
        var resultNew = _service.ComputeFeatureScore(feature, items, new HashSet<int>());

        Assert.AreEqual(resultOld.Score, resultNew.Score);
        Assert.AreEqual(resultOld.OwnerState, resultNew.OwnerState);
    }

    [TestMethod]
    public void ComputeEpicScore_EmptyDoneIds_BehavesLikeOriginalOverload()
    {
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: "Epic desc", effort: null);
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "Feature desc", effort: null, parentId: 1);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10); // 100
        var items = new[] { epic, feature, pbi };

        var resultOld = _service.ComputeEpicScore(epic, items);
        var resultNew = _service.ComputeEpicScore(epic, items, new HashSet<int>());

        Assert.AreEqual(resultOld.Score, resultNew.Score);
    }

    #endregion

    #region Suppression alignment (RR suppresses RC per Feature subtree)

    [TestMethod]
    public void SuppressionAlignment_FeatureWithNoDescription_PbiScoresDoNotContribute()
    {
        // Even if PBIs are fully refined, a Feature with no description scores 0 (Owner = PO)
        // This mirrors the RR suppresses RC rule: once the Feature is a refinement blocker,
        // PBI-level readiness is irrelevant.
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: null, effort: null);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10); // 100
        var items = new[] { feature, pbi };

        var result = _service.ComputeFeatureScore(feature, items);

        Assert.AreEqual(0, result.Score, "PBI scores must not contribute when Feature description is empty");
        Assert.AreEqual(FeatureOwnerState.PO, result.OwnerState);
    }

    [TestMethod]
    public void SuppressionAlignment_EpicWithNoDescription_FeatureScoresDoNotContribute()
    {
        // Even if all Features are 100%, an Epic with no description scores 0
        var epic = CreateWorkItem(1, WorkItemType.Epic, description: null, effort: null);
        var feature = CreateWorkItem(10, WorkItemType.Feature, description: "desc", effort: null, parentId: 1);
        var pbi = CreateWorkItem(101, WorkItemType.Pbi, description: "PBI desc", effort: 5, parentId: 10);
        var items = new[] { epic, feature, pbi };

        var result = _service.ComputeEpicScore(epic, items);

        Assert.AreEqual(0, result.Score, "Feature scores must not contribute when Epic description is empty");
    }

    #endregion

    #region Helpers

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string? description,
        int? effort,
        int? parentId = null)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"{type} {id}",
            ParentTfsId: parentId,
            AreaPath: "Test",
            IterationPath: "Test\\Sprint 1",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: description
        );
    }

    #endregion
}
