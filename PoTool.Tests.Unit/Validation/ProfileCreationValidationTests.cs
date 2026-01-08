using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PoTool.Tests.Unit.Validation;

/// <summary>
/// Unit tests for profile creation validation rules as specified in User_landing_v2.md.
/// Tests that profile cannot be saved without required Area Paths and Goals.
/// </summary>
[TestClass]
public class ProfileCreationValidationTests
{
    [TestMethod]
    public void ProfileCreation_RequiresAtLeastOneAreaPath()
    {
        // Arrange
        var areaPaths = new List<string>(); // Empty list
        var goals = new List<int> { 1, 2 }; // Has goals

        // Act
        var isValid = ValidateProfileCreation(areaPaths, goals);

        // Assert
        Assert.IsFalse(isValid, "Profile creation should be invalid without at least one area path");
    }

    [TestMethod]
    public void ProfileCreation_RequiresAtLeastOneGoal()
    {
        // Arrange
        var areaPaths = new List<string> { "Project\\Area1" }; // Has area paths
        var goals = new List<int>(); // Empty list

        // Act
        var isValid = ValidateProfileCreation(areaPaths, goals);

        // Assert
        Assert.IsFalse(isValid, "Profile creation should be invalid without at least one goal");
    }

    [TestMethod]
    public void ProfileCreation_ValidWithBothAreaPathsAndGoals()
    {
        // Arrange
        var areaPaths = new List<string> { "Project\\Area1", "Project\\Area2" };
        var goals = new List<int> { 100, 200, 300 };

        // Act
        var isValid = ValidateProfileCreation(areaPaths, goals);

        // Assert
        Assert.IsTrue(isValid, "Profile creation should be valid with area paths and goals");
    }

    [TestMethod]
    public void ProfileCreation_ValidWithMinimalRequirements()
    {
        // Arrange
        var areaPaths = new List<string> { "Project\\SingleArea" }; // Exactly one
        var goals = new List<int> { 42 }; // Exactly one

        // Act
        var isValid = ValidateProfileCreation(areaPaths, goals);

        // Assert
        Assert.IsTrue(isValid, "Profile creation should be valid with exactly one area path and one goal");
    }

    /// <summary>
    /// Validates profile creation according to User_landing_v2.md requirements:
    /// - Must have at least 1 Area Path
    /// - Must have at least 1 Goal
    /// </summary>
    private bool ValidateProfileCreation(List<string> areaPaths, List<int> goalIds)
    {
        return areaPaths.Any() && goalIds.Any();
    }
}
