using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public class RefinementCompletenessRulesTests
{
    #region RC-1: PBI description empty

    [TestMethod]
    public void RC1_PbiWithDescription_NoViolation()
    {
        // Arrange
        var rule = new PbiDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "User story description", 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RC1_PbiWithEmptyDescription_HasViolation()
    {
        // Arrange
        var rule = new PbiDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "", 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RC-1", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementCompleteness, results[0].Rule.Category);
        Assert.AreEqual(ValidationConsequence.IncompleteRefinement, results[0].Rule.Consequence);
        Assert.AreEqual(ResponsibleParty.DevelopmentTeam, results[0].Rule.ResponsibleParty);
    }

    [TestMethod]
    public void RC1_PbiWithNullDescription_HasViolation()
    {
        // Arrange
        var rule = new PbiDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, null, 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
    }

    [TestMethod]
    public void RC1_DonePbiWithEmptyDescription_NoViolation()
    {
        // Arrange: Done items should not be validated
        var rule = new PbiDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "Done", null, "", 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done items should be skipped");
    }

    [TestMethod]
    public void RC1_FeatureWithEmptyDescription_NoViolation()
    {
        // Arrange: This rule only applies to PBIs
        var rule = new PbiDescriptionEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "", 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to PBIs");
    }

    #endregion

    #region RC-2: PBI effort empty

    [TestMethod]
    public void RC2_PbiWithEffort_NoViolation()
    {
        // Arrange
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "Description", 8)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RC2_PbiWithNullEffort_HasViolation()
    {
        // Arrange
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "Description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RC-2", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementCompleteness, results[0].Rule.Category);
        Assert.AreEqual(ValidationConsequence.IncompleteRefinement, results[0].Rule.Consequence);
        Assert.AreEqual(ResponsibleParty.DevelopmentTeam, results[0].Rule.ResponsibleParty);
    }

    [TestMethod]
    public void RC2_PbiWithZeroEffort_HasViolation()
    {
        // Arrange
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "Description", 0)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
    }

    [TestMethod]
    public void RC2_DonePbiWithNoEffort_NoViolation()
    {
        // Arrange: Done items should not be validated
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "Done", null, "Description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done items should be skipped");
    }

    [TestMethod]
    public void RC2_RemovedPbiWithNoEffort_NoViolation()
    {
        // Arrange: Removed items should not be validated
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "Removed", null, "Description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Removed items should be skipped");
    }

    [TestMethod]
    public void RC2_FeatureWithNoEffort_NoViolation()
    {
        // Arrange: This rule only applies to PBIs
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to PBIs");
    }

    [TestMethod]
    public void RC2_MultiplePbis_ValidatesAll()
    {
        // Arrange
        var rule = new PbiEffortEmptyRule();
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Product Backlog Item", "New", null, "Description", 5),
            CreateWorkItem(2, "Product Backlog Item", "In Progress", null, "Description", null),
            CreateWorkItem(3, "Product Backlog Item", "New", null, "Description", 0),
            CreateWorkItem(4, "Product Backlog Item", "Done", null, "Description", null) // Should be skipped
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

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId, string? description, int? effort)
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
            Effort: effort,
            Description: description
        );
    }
}
