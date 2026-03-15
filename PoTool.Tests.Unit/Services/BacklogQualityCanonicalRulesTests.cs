using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Core.Domain.BacklogQuality.Services;
using StateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogQualityCanonicalRulesTests
{
    [TestMethod]
    public void SI1_FiresForDoneParentWithNewDescendant()
    {
        var rule = new DoneParentWithUnfinishedDescendantsRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.Done),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.New));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        var finding = results[0] as BacklogIntegrityFinding;
        Assert.IsNotNull(finding);
        Assert.AreEqual("SI-1", finding.Rule.RuleId);
        CollectionAssert.AreEqual(new[] { 2 }, finding.ConflictingDescendantIds.ToArray());
    }

    [TestMethod]
    public void SI2_AllowsDoneDescendantButFlagsInProgressDescendant()
    {
        var rule = new RemovedParentWithUnfinishedDescendantsRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.Removed),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.Done),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, "PBI description", 3, StateClassification.InProgress));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        var finding = results[0] as BacklogIntegrityFinding;
        Assert.IsNotNull(finding);
        CollectionAssert.AreEqual(new[] { 3 }, finding.ConflictingDescendantIds.ToArray());
    }

    [TestMethod]
    public void SI3_FiresForNewParentWithDoneDescendant()
    {
        var rule = new NewParentWithStartedDescendantsRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.Done));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        var finding = results[0] as BacklogIntegrityFinding;
        Assert.IsNotNull(finding);
        CollectionAssert.AreEqual(new[] { 2 }, finding.ConflictingDescendantIds.ToArray());
    }

    [TestMethod]
    public void RR1_FiresForActiveEpicWithTooShortDescription()
    {
        var rule = new EpicDescriptionRule();
        var graph = CreateGraph(Snapshot(1, BacklogWorkItemTypes.Epic, null, "short", null, StateClassification.New));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RR-1", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void RR2_FiresForActiveFeatureWithTooShortDescription()
    {
        var rule = new FeatureDescriptionRule();
        var graph = CreateGraph(Snapshot(1, BacklogWorkItemTypes.Feature, null, "short", null, StateClassification.InProgress));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RR-2", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void RR3_FiresWhenEpicHasNoActiveFeatureChild()
    {
        var rule = new EpicMissingChildrenRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.Done));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        Assert.AreEqual("RR-3", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void RC1_FiresForActivePbiWithMissingDescription()
    {
        var rule = new PbiDescriptionRule();
        var graph = CreateGraph(Snapshot(1, BacklogWorkItemTypes.ProductBacklogItem, null, null, 3, StateClassification.New));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        Assert.AreEqual("RC-1", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void RC2_PreservesCanonicalIdentityAndAppliesOnlyToPbis()
    {
        var rule = new PbiMissingEffortRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Epic, null, "Epic description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.Feature, 1, "Feature description", null, StateClassification.New),
            Snapshot(3, BacklogWorkItemTypes.ProductBacklogItem, 2, "PBI description", null, StateClassification.New));

        var results = rule.Evaluate(graph);

        Assert.AreEqual("RC-2", rule.Metadata.RuleId);
        Assert.AreEqual("MissingEffort", rule.Metadata.SemanticTag);
        CollectionAssert.AreEqual(BacklogWorkItemTypes.PbiTypes.ToArray(), rule.Metadata.ApplicableWorkItemTypes.ToArray());
        Assert.HasCount(1, results);
        Assert.AreEqual(3, results[0].WorkItemId);
    }

    [TestMethod]
    public void RC3_FiresWhenFeatureHasNoActivePbiChild()
    {
        var rule = new FeatureMissingChildrenRule();
        var graph = CreateGraph(
            Snapshot(1, BacklogWorkItemTypes.Feature, null, "Feature description", null, StateClassification.New),
            Snapshot(2, BacklogWorkItemTypes.ProductBacklogItem, 1, "PBI description", 3, StateClassification.Done));

        var results = rule.Evaluate(graph);

        Assert.HasCount(1, results);
        Assert.AreEqual("RC-3", results[0].Rule.RuleId);
    }

    [TestMethod]
    [DataRow("SI-1", 0, "DoneParentWithUnfinishedDescendants", 2, 0)]
    [DataRow("SI-2", 0, "RemovedParentWithUnfinishedDescendants", 2, 0)]
    [DataRow("SI-3", 0, "NewParentWithStartedDescendants", 2, 0)]
    [DataRow("RR-1", 1, "MissingDescription", 0, 1)]
    [DataRow("RR-2", 1, "MissingDescription", 0, 1)]
    [DataRow("RR-3", 1, "MissingChildren", 0, 1)]
    [DataRow("RC-1", 2, "MissingDescription", 1, 2)]
    [DataRow("RC-2", 2, "MissingEffort", 1, 2)]
    [DataRow("RC-3", 2, "MissingChildren", 1, 2)]
    public void RuleCatalog_ExposesCanonicalMetadata(
        string ruleId,
        int family,
        string semanticTag,
        int responsibleParty,
        int findingClass)
    {
        var catalog = new RuleCatalog();

        Assert.IsTrue(catalog.TryGetRule(ruleId, out var rule));
        Assert.IsNotNull(rule);
        Assert.AreEqual((RuleFamily)family, rule.Metadata.Family);
        Assert.AreEqual(semanticTag, rule.Metadata.SemanticTag);
        Assert.AreEqual((RuleResponsibleParty)responsibleParty, rule.Metadata.ResponsibleParty);
        Assert.AreEqual((RuleFindingClass)findingClass, rule.Metadata.FindingClass);
    }

    [TestMethod]
    public void RuleCatalog_GetByFamily_PreservesDeterministicOrder()
    {
        var catalog = new RuleCatalog();

        CollectionAssert.AreEqual(
            new[] { "SI-1", "SI-2", "SI-3" },
            catalog.GetByFamily(RuleFamily.StructuralIntegrity).Select(rule => rule.Metadata.RuleId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "RR-1", "RR-2", "RR-3" },
            catalog.GetByFamily(RuleFamily.RefinementReadiness).Select(rule => rule.Metadata.RuleId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "RC-1", "RC-2", "RC-3" },
            catalog.GetByFamily(RuleFamily.ImplementationReadiness).Select(rule => rule.Metadata.RuleId).ToArray());
    }

    private static BacklogGraph CreateGraph(params WorkItemSnapshot[] items)
    {
        return new BacklogGraph(items);
    }

    private static WorkItemSnapshot Snapshot(
        int workItemId,
        string workItemType,
        int? parentWorkItemId,
        string? description,
        decimal? effort,
        StateClassification stateClassification)
    {
        return new WorkItemSnapshot(workItemId, workItemType, parentWorkItemId, description, effort, stateClassification);
    }
}
