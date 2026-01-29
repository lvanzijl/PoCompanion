using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

[TestClass]
public class RefinementCompletenessRulesTests
{
    #region RC-1: PBI description empty

    [TestMethod]
    public void RC1_PbiWithDescription_NoViolation()
    {
        // Arrange
        var rule = new PbiDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
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

    [TestMethod]
    public void RC2_PbiWithoutEffortButParentIsBlocker_Suppressed()
    {
        // Arrange: PBI without effort but parent Feature has invalid description (refinement blocker)
        // The PBI's validation should be suppressed
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Short", null), // Invalid description (< 10 chars)
            CreateWorkItem(2, "Product Backlog Item", "New", 1, "Description", null) // No effort, but suppressed
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "PBI validation should be suppressed when parent Feature is a refinement blocker");
    }

    [TestMethod]
    public void RC2_PbiWithoutEffortAndParentHasValidDescription_NotSuppressed()
    {
        // Arrange: PBI without effort and parent Feature has valid description
        var rule = new PbiEffortEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Valid description with more than 10 characters", null),
            CreateWorkItem(2, "Product Backlog Item", "New", 1, "Description", null) // No effort
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(2, results[0].WorkItemId);
    }

    #endregion

    #region RC-3: Feature without children

    [TestMethod]
    public void RC3_FeatureWithChildren_NoViolation()
    {
        // Arrange
        var rule = new FeatureWithoutChildrenRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Valid description", null),
            CreateWorkItem(2, "Product Backlog Item", "New", 1, "PBI Description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RC3_FeatureWithoutChildren_HasViolation()
    {
        // Arrange
        var rule = new FeatureWithoutChildrenRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Valid description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RC-3", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementCompleteness, results[0].Rule.Category);
        Assert.AreEqual(ValidationConsequence.IncompleteRefinement, results[0].Rule.Consequence);
    }

    [TestMethod]
    public void RC3_FeatureWithInvalidDescription_NoViolation()
    {
        // Arrange: Feature with invalid description (refinement blocker) should not be flagged for missing children
        var rule = new FeatureWithoutChildrenRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Short", null) // < 10 chars
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Feature with invalid description should not trigger RC-3");
    }

    [TestMethod]
    public void RC3_DoneFeatureWithoutChildren_NoViolation()
    {
        // Arrange: Done items should not be validated
        var rule = new FeatureWithoutChildrenRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "Done", null, "Valid description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done items should be skipped");
    }

    [TestMethod]
    public void RC3_EpicWithoutChildren_NoViolation()
    {
        // Arrange: This rule only applies to Features
        var rule = new FeatureWithoutChildrenRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Valid description", null)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to Features");
    }

    #endregion
    
    /// <summary>
    /// Creates a simple test mock for state classification that uses hardcoded mappings.
    /// </summary>
    private static IWorkItemStateClassificationService CreateMockStateClassificationService()
    {
        return new TestStateClassificationService();
    }
    
    private class TestStateClassificationService : IWorkItemStateClassificationService
    {
        public Task<GetStateClassificationsResponse> GetClassificationsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SaveClassificationsAsync(SaveStateClassificationsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsDoneStateAsync(string workItemType, string state, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(state.Equals("Done", StringComparison.OrdinalIgnoreCase) || 
                                 state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                                 state.Equals("Resolved", StringComparison.OrdinalIgnoreCase));
        }

        public Task<bool> IsInProgressStateAsync(string workItemType, string state, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(state.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                                 state.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                                 state.Equals("Committed", StringComparison.OrdinalIgnoreCase));
        }

        public Task<bool> IsNewStateAsync(string workItemType, string state, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(state.Equals("New", StringComparison.OrdinalIgnoreCase) ||
                                 state.Equals("Proposed", StringComparison.OrdinalIgnoreCase) ||
                                 state.Equals("Approved", StringComparison.OrdinalIgnoreCase));
        }

        public Task<StateClassification> GetClassificationAsync(string workItemType, string state, CancellationToken cancellationToken = default)
        {
            if (state.Equals("Done", StringComparison.OrdinalIgnoreCase) || 
                state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(StateClassification.Done);
            }
            if (state.Equals("Removed", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(StateClassification.Removed);
            }
            if (state.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Committed", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(StateClassification.InProgress);
            }
            return Task.FromResult(StateClassification.New);
        }
    }

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
