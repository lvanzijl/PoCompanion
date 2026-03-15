using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Core.Domain.BacklogQuality.Services;
using BacklogQualityWorkItemSnapshot = PoTool.Core.Domain.BacklogQuality.Models.WorkItemSnapshot;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogQualityDomainModelsTests
{
    [TestMethod]
    public void BacklogGraph_PreservesRemovedItemsAndAllWorkItemTypes()
    {
        var graph = new BacklogGraph(
        [
            new BacklogQualityWorkItemSnapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.New),
            new BacklogQualityWorkItemSnapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.InProgress),
            new BacklogQualityWorkItemSnapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, "PBI description", 5, StateClassification.Removed),
            new BacklogQualityWorkItemSnapshot(4, BacklogWorkItemTypes.Task, 3, "Task description", null, StateClassification.Done)
        ]);

        Assert.HasCount(4, graph.Items);
        Assert.IsTrue(graph.Contains(3), "Removed items must remain in the canonical graph.");
        Assert.AreEqual(StateClassification.Removed, graph.GetWorkItem(3).StateClassification);
        CollectionAssert.AreEqual(new[] { 3 }, graph.GetChildren(2).Select(item => item.WorkItemId).ToArray());
        CollectionAssert.AreEqual(new[] { 4 }, graph.GetChildren(3).Select(item => item.WorkItemId).ToArray());
    }

    [TestMethod]
    public void BacklogGraph_ThrowsWhenDuplicateIdsExist()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() => new BacklogGraph(
        [
            new BacklogQualityWorkItemSnapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.New),
            new BacklogQualityWorkItemSnapshot(1, BacklogWorkItemTypes.Feature, null, "Feature description", null, StateClassification.New)
        ]));

        StringAssert.Contains(ex.Message, "Duplicate work item ID");
    }

    [TestMethod]
    public void ReadinessScore_UsesBoundedValueAndMidpointToEvenAverage()
    {
        var score = new ReadinessScore(100);
        var average = ReadinessScore.Average(
        [
            new ReadinessScore(75),
            new ReadinessScore(100)
        ]);

        Assert.IsTrue(score.IsFullyReady);
        Assert.AreEqual(88, average.Value);
        Assert.AreEqual("88%", average.ToString());
    }

    [TestMethod]
    public void ReadinessScore_ThrowsWhenValueFallsOutsidePercentageRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ReadinessScore(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ReadinessScore(101));
    }

    [TestMethod]
    public void RuleMetadata_ExposesCanonicalImplementationRuleShape()
    {
        var metadata = new RuleMetadata(
            "RC-2",
            RuleFamily.ImplementationReadiness,
            "MissingEffort",
            "PBI effort must be present and greater than zero.",
            RuleResponsibleParty.Team,
            RuleFindingClass.ImplementationBlocker,
            BacklogWorkItemTypes.PbiTypes);

        Assert.AreEqual("RC-2", metadata.RuleId);
        Assert.AreEqual(RuleFamily.ImplementationReadiness, metadata.Family);
        Assert.AreEqual("MissingEffort", metadata.SemanticTag);
        Assert.AreEqual(RuleResponsibleParty.Team, metadata.ResponsibleParty);
        CollectionAssert.AreEqual(
            BacklogWorkItemTypes.PbiTypes.ToArray(),
            metadata.ApplicableWorkItemTypes.ToArray());
    }

    [TestMethod]
    public void RuleCatalog_RegistersRulesInCanonicalStableOrder()
    {
        var catalog = new RuleCatalog();

        CollectionAssert.AreEqual(
            new[] { "SI-1", "SI-2", "SI-3", "RR-1", "RR-2", "RR-3", "RC-1", "RC-2", "RC-3" },
            catalog.Rules.Select(rule => rule.Metadata.RuleId).ToArray());

        Assert.HasCount(3, catalog.GetByFamily(RuleFamily.StructuralIntegrity));
        Assert.HasCount(3, catalog.GetByFamily(RuleFamily.RefinementReadiness));
        Assert.HasCount(3, catalog.GetByFamily(RuleFamily.ImplementationReadiness));
        Assert.IsTrue(catalog.TryGetRule("RC-2", out var rule));
        Assert.IsNotNull(rule);
        Assert.AreEqual("MissingEffort", rule.Metadata.SemanticTag);
    }
}
