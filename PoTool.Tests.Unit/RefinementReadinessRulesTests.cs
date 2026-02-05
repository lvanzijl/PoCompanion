using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

[TestClass]
public class RefinementReadinessRulesTests
{
    #region RR-1: Epic description empty

    [TestMethod]
    public void RR1_EpicWithDescription_NoViolation()
    {
        // Arrange
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to Epics");
    }

    [TestMethod]
    public void RR1_EpicWithShortDescription_HasViolation()
    {
        // Arrange: Description must be at least 10 characters
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Short")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
    }

    [TestMethod]
    public void RR1_EpicWithExactly10CharDescription_NoViolation()
    {
        // Arrange: Description with exactly 10 characters should pass
        var rule = new EpicDescriptionEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "1234567890")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    #endregion

    #region RR-2: Feature description empty

    [TestMethod]
    public void RR2_FeatureWithDescription_NoViolation()
    {
        // Arrange
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
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
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
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
    public void RR2_FeatureWithShortDescription_HasViolation()
    {
        // Arrange: Description must be at least 10 characters
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "Short")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
    }

    [TestMethod]
    public void RR2_FeatureWithExactly10CharDescription_NoViolation()
    {
        // Arrange: Description with exactly 10 characters should pass
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null, "1234567890")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void RR2_MultipleFeatures_ValidatesAll()
    {
        // Arrange
        var rule = new FeatureDescriptionEmptyRule(CreateMockStateClassificationService());
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

    #region RR-3: Epic without Features

    [TestMethod]
    public void RR3_EpicWithFeatures_NoViolation()
    {
        // Arrange
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description"),
            CreateWorkItem(2, "Feature", "New", 1, "Feature description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Epic with Features should not violate");
    }

    [TestMethod]
    public void RR3_EpicWithoutFeatures_HasViolation()
    {
        // Arrange
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("RR-3", results[0].Rule.RuleId);
        Assert.AreEqual(ValidationCategory.RefinementReadiness, results[0].Rule.Category);
        Assert.AreEqual(ValidationConsequence.RefinementBlocker, results[0].Rule.Consequence);
        Assert.AreEqual(ResponsibleParty.ProductOwner, results[0].Rule.ResponsibleParty);
    }

    [TestMethod]
    public void RR3_EpicWithPbisButNoFeatures_HasViolation()
    {
        // Arrange: Epic has PBIs but no Features (invalid hierarchy)
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description"),
            CreateWorkItem(2, "Product Backlog Item", "New", 1, "PBI description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results, "Epic should have Feature children, not PBI children");
    }

    [TestMethod]
    public void RR3_DoneEpicWithoutFeatures_NoViolation()
    {
        // Arrange: Done items should be skipped
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null, "Epic description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Done Epics should not be validated");
    }

    [TestMethod]
    public void RR3_RemovedEpicWithoutFeatures_NoViolation()
    {
        // Arrange: Removed items should be skipped
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null, "Epic description")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Removed Epics should not be validated");
    }

    [TestMethod]
    public void RR3_MultipleEpics_ValidatesAll()
    {
        // Arrange
        var rule = new EpicWithoutFeaturesRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic 1"),
            CreateWorkItem(2, "Feature", "New", 1, "Feature of Epic 1"),
            CreateWorkItem(3, "Epic", "New", null, "Epic 2"),
            CreateWorkItem(4, "Epic", "Done", null, "Epic 3 - Done")
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results, "Only Epic 2 should violate (Epic 1 has Features, Epic 3 is Done)");
        Assert.AreEqual(3, results[0].WorkItemId);
    }

    #endregion

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
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: description,
            Tags: null
        );
    }
}
