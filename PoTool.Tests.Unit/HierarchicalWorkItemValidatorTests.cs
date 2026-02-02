using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Shared.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

[TestClass]
public class HierarchicalWorkItemValidatorTests
{
    private IHierarchicalWorkItemValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        var mockStateService = CreateMockStateClassificationService();
        
        // Create validator with all rules
        var rules = new List<IHierarchicalValidationRule>
        {
            // Structural Integrity rules
            new DoneParentWithUnfinishedDescendantsRule(mockStateService),
            new RemovedParentWithUnfinishedDescendantsRule(mockStateService),
            new NewParentWithInProgressDescendantsRule(mockStateService),
            // Refinement Readiness rules
            new EpicDescriptionEmptyRule(mockStateService),
            new FeatureDescriptionEmptyRule(mockStateService),
            new EpicWithoutFeaturesRule(mockStateService),
            // Refinement Completeness rules
            new PbiDescriptionEmptyRule(mockStateService),
            new PbiEffortEmptyRule(mockStateService),
            new FeatureWithoutChildrenRule(mockStateService)
        };

        _validator = new HierarchicalWorkItemValidator(rules);
    }

    #region Suppression Logic Tests

    [TestMethod]
    public void Suppression_EpicDescriptionEmpty_SuppressesPbiValidation()
    {
        // Arrange: Epic has empty description, PBI has empty description and effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "", null),
            CreateWorkItem(2, "Feature", "New", 1, "Feature description", null),
            CreateWorkItem(3, "Product Backlog Item", "New", 2, "", null) // Missing desc and effort
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: Refinement Blockers present, PBI validation suppressed
        Assert.IsTrue(result.HasRefinementBlockers, "Should have refinement blocker for empty epic description");
        Assert.IsFalse(result.HasIncompleteRefinement, "PBI validation should be suppressed");
        Assert.IsTrue(result.WasSuppressed, "WasSuppressed flag should be true");
        Assert.IsTrue(result.RefinementBlockers.Any(r => r.Rule.RuleId == "RR-1"));
    }

    [TestMethod]
    public void Suppression_FeatureDescriptionEmpty_SuppressesPbiValidation()
    {
        // Arrange: Feature has empty description, PBI has empty description
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description", null),
            CreateWorkItem(2, "Feature", "New", 1, "", null), // Empty description
            CreateWorkItem(3, "Product Backlog Item", "New", 2, "", null) // Missing desc and effort
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: Refinement Blockers present, PBI validation suppressed
        Assert.IsTrue(result.HasRefinementBlockers);
        Assert.IsFalse(result.HasIncompleteRefinement, "PBI validation should be suppressed");
        Assert.IsTrue(result.WasSuppressed);
        Assert.IsTrue(result.RefinementBlockers.Any(r => r.Rule.RuleId == "RR-2"));
    }

    [TestMethod]
    public void Suppression_NoRefinementBlockers_PbiValidationExecutes()
    {
        // Arrange: Epic and Feature have descriptions, but PBI has empty description
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description", null),
            CreateWorkItem(2, "Feature", "New", 1, "Feature description", null),
            CreateWorkItem(3, "Product Backlog Item", "New", 2, "", null) // Empty desc and effort
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: No Refinement Blockers, PBI validation executed
        Assert.IsFalse(result.HasRefinementBlockers);
        Assert.IsTrue(result.HasIncompleteRefinement, "PBI validation should have run");
        Assert.IsFalse(result.WasSuppressed, "WasSuppressed flag should be false");
        
        // Should have both RC-1 and RC-2 violations
        Assert.IsTrue(result.IncompleteRefinementIssues.Any(r => r.Rule.RuleId == "RC-1"));
        Assert.IsTrue(result.IncompleteRefinementIssues.Any(r => r.Rule.RuleId == "RC-2"));
    }

    #endregion

    #region Mixed Scenario Tests

    [TestMethod]
    public void MixedScenario_StructuralIntegrityAndRefinementBlockers()
    {
        // Arrange: Done parent with New child (SI-1), and Epic with empty description (RR-1)
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "Done", null, "", null), // Done but empty desc
            CreateWorkItem(2, "Feature", "New", 1, "Feature description", null) // Child is New (SI-1 violation)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: Both Backlog Health Problems and Refinement Blockers present
        Assert.IsTrue(result.HasBacklogHealthProblems, "SI-1 violation should be present");
        Assert.IsFalse(result.HasRefinementBlockers, "Done Epic should be skipped for RR-1");
        
        // Backlog health problems should never suppress other categories
        Assert.IsTrue(result.BacklogHealthProblems.Any(r => r.Rule.RuleId == "SI-1"));
    }

    [TestMethod]
    public void MixedScenario_AllCategoriesClean()
    {
        // Arrange: Valid tree with all descriptions and effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null, "Epic description", 100),
            CreateWorkItem(2, "Feature", "In Progress", 1, "Feature description", 40),
            CreateWorkItem(3, "Product Backlog Item", "In Progress", 2, "PBI description", 8)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: All clean
        Assert.IsFalse(result.HasBacklogHealthProblems);
        Assert.IsFalse(result.HasRefinementBlockers);
        Assert.IsFalse(result.HasIncompleteRefinement);
        Assert.IsTrue(result.IsReadyForRefinement);
        Assert.IsTrue(result.IsReadyForImplementation);
        Assert.IsFalse(result.WasSuppressed);
    }

    [TestMethod]
    public void MixedScenario_MultipleRefinementBlockers()
    {
        // Arrange: Both Epic and Feature have empty descriptions
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "", null),
            CreateWorkItem(2, "Feature", "New", 1, "", null),
            CreateWorkItem(3, "Product Backlog Item", "New", 2, "", null)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: Multiple refinement blockers
        Assert.IsTrue(result.HasRefinementBlockers);
        Assert.HasCount(2, result.RefinementBlockers);
        Assert.IsFalse(result.HasIncompleteRefinement, "PBI validation should be suppressed");
        Assert.IsTrue(result.WasSuppressed);
    }

    #endregion

    #region Evaluation Order Tests

    [TestMethod]
    public void EvaluationOrder_StructuralIntegrityAlwaysEvaluated()
    {
        // Arrange: SI violation even with refinement blockers
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "", null), // Empty description (RR-1)
            CreateWorkItem(2, "Feature", "In Progress", 1, "Feature description", null) // In Progress under New (SI-3)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert: Both categories should have violations
        Assert.IsTrue(result.HasBacklogHealthProblems, "SI-3 should be detected");
        Assert.IsTrue(result.HasRefinementBlockers, "RR-1 should be detected");
        Assert.IsTrue(result.BacklogHealthProblems.Any(r => r.Rule.RuleId == "SI-3"));
        Assert.IsTrue(result.RefinementBlockers.Any(r => r.Rule.RuleId == "RR-1"));
    }

    #endregion

    #region Multiple Trees Tests

    [TestMethod]
    public void MultipleTrees_EachTreeValidatedIndependently()
    {
        // Arrange: Two independent trees
        var items = new List<WorkItemDto>
        {
            // Tree 1: Clean
            CreateWorkItem(1, "Epic", "In Progress", null, "Epic 1 desc", null),
            CreateWorkItem(2, "Feature", "In Progress", 1, "Feature 1 desc", null),
            // Tree 2: Has refinement blocker
            CreateWorkItem(10, "Epic", "New", null, "", null),
            CreateWorkItem(11, "Feature", "New", 10, "Feature 2 desc", null)
        };

        // Act
        var results = _validator.ValidateWorkItems(items);

        // Assert: Two results, one per tree
        Assert.HasCount(2, results);

        var tree1 = results.First(r => r.RootWorkItemId == 1);
        var tree2 = results.First(r => r.RootWorkItemId == 10);

        Assert.IsFalse(tree1.HasRefinementBlockers, "Tree 1 should be clean");
        Assert.IsTrue(tree2.HasRefinementBlockers, "Tree 2 should have refinement blocker");
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void EdgeCase_EmptyWorkItemList()
    {
        // Arrange
        var items = new List<WorkItemDto>();

        // Act
        var results = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void EdgeCase_SingleRootItem()
    {
        // Arrange
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "", null)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert
        Assert.IsTrue(result.HasRefinementBlockers);
        Assert.AreEqual(1, result.RootWorkItemId);
    }

    [TestMethod]
    public void EdgeCase_OrphanedItem_TreatedAsRoot()
    {
        // Arrange: Item with parent ID that doesn't exist in dataset
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", 999, "", null) // Parent 999 doesn't exist
        };

        // Act
        var results = _validator.ValidateWorkItems(items);

        // Assert: Treated as root, should have refinement blocker
        Assert.HasCount(1, results);
        Assert.AreEqual(1, results[0].RootWorkItemId);
        Assert.IsTrue(results[0].HasRefinementBlockers);
    }

    #endregion

    #region Result Properties Tests

    [TestMethod]
    public void ResultProperties_AllViolations_ReturnsAll()
    {
        // Arrange: SI violation + RR violation (PBI suppressed)
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "", null), // RR-1
            CreateWorkItem(2, "Feature", "In Progress", 1, "Feature description", null) // SI-3
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert
        var allViolations = result.AllViolations.ToList();
        Assert.HasCount(2, allViolations);
        Assert.IsTrue(allViolations.Any(v => v.Rule.RuleId == "SI-3"));
        Assert.IsTrue(allViolations.Any(v => v.Rule.RuleId == "RR-1"));
    }

    [TestMethod]
    public void ResultProperties_IsReadyForRefinement_NoBlockers()
    {
        // Arrange: Clean Epic and Feature, PBI with issues
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", null, "Epic description", null),
            CreateWorkItem(2, "Feature", "New", 1, "Feature description", null),
            CreateWorkItem(3, "Product Backlog Item", "New", 2, "", null)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert
        Assert.IsTrue(result.IsReadyForRefinement);
        Assert.IsFalse(result.IsReadyForImplementation);
    }

    [TestMethod]
    public void ResultProperties_IsReadyForImplementation_FullyComplete()
    {
        // Arrange: Fully complete tree
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "In Progress", null, "Epic description", 100),
            CreateWorkItem(2, "Feature", "In Progress", 1, "Feature description", 40),
            CreateWorkItem(3, "Product Backlog Item", "In Progress", 2, "PBI description", 8)
        };

        // Act
        var result = _validator.ValidateTree(1, items);

        // Assert
        Assert.IsTrue(result.IsReadyForRefinement);
        Assert.IsTrue(result.IsReadyForImplementation);
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
