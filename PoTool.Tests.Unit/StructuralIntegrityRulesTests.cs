using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

[TestClass]
public class StructuralIntegrityRulesTests
{
    #region SI-1: Done parent with unfinished descendants

    [TestMethod]
    public void SI1_DoneParentWithDoneDescendants_NoViolation()
    {
        // Arrange
        var rule = new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null),
            CreateWorkItem(2, "Feature", "Done", 1),
            CreateWorkItem(3, "Product Backlog Item", "Done", 2)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "No violations expected when all descendants are Done");
    }

    [TestMethod]
    public void SI1_DoneParentWithRemovedDescendants_NoViolation()
    {
        // Arrange
        var rule = new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null),
            CreateWorkItem(2, "Feature", "Removed", 1),
            CreateWorkItem(3, "Product Backlog Item", "Done", 2)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "No violations expected when descendants are Done or Removed");
    }

    [TestMethod]
    public void SI1_DoneParentWithNewDescendant_HasViolation()
    {
        // Arrange
        var rule = new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null),
            CreateWorkItem(2, "Feature", "New", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-1", results[0].Rule.RuleId);
        Assert.IsTrue(results[0].IsViolated);
    }

    [TestMethod]
    public void SI1_DoneParentWithInProgressDescendant_HasViolation()
    {
        // Arrange: Both Epic and Feature are Done, but PBI is In Progress
        // Both Done parents should be flagged as violations
        var rule = new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null),
            CreateWorkItem(2, "Feature", "Done", 1),
            CreateWorkItem(3, "Product Backlog Item", "In Progress", 2)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert: Both Done parents (1 and 2) have unfinished descendants
        Assert.HasCount(2, results);

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 1), "Epic should be flagged");

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 2), "Feature should be flagged");
    }

    [TestMethod]
    public void SI1_DoneParentWithDeepUnfinishedDescendant_HasViolation()
    {
        // Arrange: All parents are Done but Task is New
        // All Done parents should be flagged (Epic, Feature, PBI)
        var rule = new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null),
            CreateWorkItem(2, "Feature", "Done", 1),
            CreateWorkItem(3, "Product Backlog Item", "Done", 2),
            CreateWorkItem(4, "Task", "New", 3) // Deep descendant not finished
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert: All three Done parents (1, 2, 3) have unfinished descendants
        Assert.HasCount(3, results);

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 1), "Epic should be flagged");

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 2), "Feature should be flagged");

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 3), "PBI should be flagged");
        
        // Verify additional context on at least one result
        var epicResult = results.First(r => r.WorkItemId == 1);
        Assert.IsNotNull(epicResult.AdditionalContext);
        Assert.Contains("#4", epicResult.AdditionalContext!);
    }

    #endregion

    #region SI-2: Removed parent with non-Removed descendants

    [TestMethod]
    public void SI2_RemovedParentWithRemovedDescendants_NoViolation()
    {
        // Arrange
        var rule = new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null),
            CreateWorkItem(2, "Feature", "Removed", 1),
            CreateWorkItem(3, "Product Backlog Item", "Removed", 2)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "No violations expected when all descendants are Removed");
    }

    [TestMethod]
    public void SI2_RemovedParentWithDoneDescendant_HasViolation()
    {
        // Arrange
        var rule = new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null),
            CreateWorkItem(2, "Feature", "Done", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results, "Violation expected when descendant is Done");
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-2", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void SI2_RemovedParentWithNewDescendant_HasViolation()
    {
        // Arrange
        var rule = new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null),
            CreateWorkItem(2, "Feature", "New", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-2", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void SI2_RemovedParentWithInProgressDescendant_HasViolation()
    {
        // Arrange
        var rule = new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Removed", null),
            CreateWorkItem(2, "Feature", "In Progress", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-2", results[0].Rule.RuleId);
    }

    #endregion

    #region SI-3: New parent with InProgress or Done descendants

    [TestMethod]
    public void SI3_NewParentWithNewDescendants_NoViolation()
    {
        // Arrange
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null),
            CreateWorkItem(2, "Feature", "New", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void SI3_NewParentWithRemovedDescendants_NoViolation()
    {
        // Arrange
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null),
            CreateWorkItem(2, "Feature", "Removed", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Removed descendants are allowed for New parents");
    }

    [TestMethod]
    public void SI3_NewParentWithInProgressDescendant_HasViolation()
    {
        // Arrange
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null),
            CreateWorkItem(2, "Feature", "In Progress", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-3", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void SI3_NewParentWithDoneDescendant_HasViolation()
    {
        // Arrange
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null),
            CreateWorkItem(2, "Feature", "Done", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.HasCount(1, results, "Done descendants should be invalid for New parents");
        Assert.AreEqual(1, results[0].WorkItemId);
        Assert.AreEqual("SI-3", results[0].Rule.RuleId);
    }

    [TestMethod]
    public void SI3_NewParentWithDeepInProgressDescendant_HasViolation()
    {
        // Arrange: Both Epic and Feature are New, but PBI is In Progress
        // Both New parents should be flagged
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null),
            CreateWorkItem(2, "Feature", "New", 1),
            CreateWorkItem(3, "Product Backlog Item", "In Progress", 2)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert: Both New parents (1 and 2) have In Progress descendants
        Assert.HasCount(2, results);

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 1), "Epic should be flagged");

#pragma warning disable MSTEST0037
        Assert.IsTrue(results.Any(r => r.WorkItemId == 2), "Feature should be flagged");
    }

    [TestMethod]
    public void SI3_InProgressParentWithInProgressDescendant_NoViolation()
    {
        // Arrange
        var rule = new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService());
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null),
            CreateWorkItem(2, "Feature", "In Progress", 1)
        };

        // Act
        var results = rule.Evaluate(items);

        // Assert
        Assert.IsEmpty(results, "Rule only applies to New parents");
    }

    #endregion

    #region InProgress parent validation - should allow all descendant states

    [TestMethod]
    public void InProgressParent_WithNewDescendant_NoViolation()
    {
        // Arrange: InProgress parent with New descendant should be valid
        var allRules = new List<IHierarchicalValidationRule>
        {
            new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService())
        };

        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null),
            CreateWorkItem(2, "Feature", "New", 1)
        };

        // Act: Run all SI rules
        var allResults = allRules.SelectMany(r => r.Evaluate(items)).ToList();

        // Assert
        Assert.IsEmpty(allResults, "InProgress parent should allow New descendants");
    }

    [TestMethod]
    public void InProgressParent_WithInProgressDescendant_NoViolation()
    {
        // Arrange
        var allRules = new List<IHierarchicalValidationRule>
        {
            new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService())
        };

        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null),
            CreateWorkItem(2, "Feature", "In Progress", 1)
        };

        // Act
        var allResults = allRules.SelectMany(r => r.Evaluate(items)).ToList();

        // Assert
        Assert.IsEmpty(allResults, "InProgress parent should allow InProgress descendants");
    }

    [TestMethod]
    public void InProgressParent_WithDoneDescendant_NoViolation()
    {
        // Arrange
        var allRules = new List<IHierarchicalValidationRule>
        {
            new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService())
        };

        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null),
            CreateWorkItem(2, "Feature", "Done", 1)
        };

        // Act
        var allResults = allRules.SelectMany(r => r.Evaluate(items)).ToList();

        // Assert
        Assert.IsEmpty(allResults, "InProgress parent should allow Done descendants");
    }

    [TestMethod]
    public void InProgressParent_WithRemovedDescendant_NoViolation()
    {
        // Arrange
        var allRules = new List<IHierarchicalValidationRule>
        {
            new DoneParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new RemovedParentWithUnfinishedDescendantsRule(CreateMockStateClassificationService()),
            new NewParentWithInProgressDescendantsRule(CreateMockStateClassificationService())
        };

        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null),
            CreateWorkItem(2, "Feature", "Removed", 1)
        };

        // Act
        var allResults = allRules.SelectMany(r => r.Evaluate(items)).ToList();

        // Assert
        Assert.IsEmpty(allResults, "InProgress parent should allow Removed descendants");
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

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId)
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
            Description: null
        );
    }
}
