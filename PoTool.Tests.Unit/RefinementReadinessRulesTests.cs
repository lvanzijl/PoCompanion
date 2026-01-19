using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public class RefinementReadinessRulesTests
{
    #region RR-1: Epic description empty

    [TestMethod]
    public void RR1_EpicWithDescription_NoViolation()
    {
        // Arrange
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "This is the epic description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RR1_EpicWithEmptyDescription_HasViolation()
    {
        // Arrange
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RR-1", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementReadiness, results[0].Rule.Category);
        Assert.AreEqual(ValidationConsequence.RefinementBlocker, results[0].Rule.Consequence);
        Assert.AreEqual(ResponsibleParty.ProductOwner, results[0].Rule.ResponsibleParty);
    }

    [TestMethod]
    public void RR1_EpicWithNullDescription_HasViolation()
    {
        // Arrange
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
    }

    [TestMethod]
    public void RR1_EpicWithWhitespaceDescription_HasViolation()
    {
        // Arrange
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "   \t\n  ")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
    }

    [TestMethod]
    public void RR1_DoneEpicWithEmptyDescription_NoViolation()
    {
        // Arrange: Done items should not be validated
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done items should be skipped");
    }

    [TestMethod]
    public void RR1_RemovedEpicWithEmptyDescription_NoViolation()
    {
        // Arrange: Removed items should not be validated
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Removed items should be skipped");
    }

    [TestMethod]
    public void RR1_FeatureWithEmptyDescription_NoViolation()
    {
        // Arrange: This rule only applies to Epics
        var rule = new EpicDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to Epics");
    }

    #endregion

    #region RR-2: Feature description empty

    [TestMethod]
    public void RR2_FeatureWithDescription_NoViolation()
    {
        // Arrange
        var rule = new FeatureDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "This is the feature description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RR2_FeatureWithEmptyDescription_HasViolation()
    {
        // Arrange
        var rule = new FeatureDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RR-2", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementReadiness, results[0].Rule.Category);
    }

    [TestMethod]
    public void RR2_DoneFeatureWithEmptyDescription_NoViolation()
    {
        // Arrange: Done items should not be validated
        var rule = new FeatureDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "Done", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done items should be skipped");
    }

    [TestMethod]
    public void RR2_EpicWithEmptyDescription_NoViolation()
    {
        // Arrange: This rule only applies to Features
        var rule = new FeatureDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to Features");
    }

    [TestMethod]
    public void RR2_MultipleFeatures_ValidatesAll()
    {
        // Arrange
        var rule = new FeatureDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Description"),
            CreateWorkItem(2, "Feature", "In Progress", null, ""),
            CreateWorkItem(3, "Feature", "New", null, null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(2, results);

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 2));

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 3));
    }

    #endregion

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId, string? description)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: "Test",
            IterationPath: "Test",
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: description
        );
    }
}
