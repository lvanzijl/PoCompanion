using PoTool.Client.Models;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public sealed class TreeNodeValidationTests
{
    [TestMethod]
    public void ValidationIcon_UsesSelfIcon_WhenSelfIssuesExist()
    {
        var node = new TreeNode
        {
            ValidationIssues = new List<string> { "error" },
            HighestCategory = ValidationCategory.StructuralIntegrity
        };

        Assert.IsTrue(node.HasValidationIssues);
        Assert.AreEqual("🔴", node.ValidationIcon);
    }

    [TestMethod]
    public void ValidationIcon_UsesDescendantIcon_WhenOnlyDescendantsHaveIssues()
    {
        var node = new TreeNode
        {
            InvalidDescendantIds = new List<int> { 42 }
        };

        Assert.IsFalse(node.HasValidationIssues);
        Assert.IsTrue(node.HasDescendantIssues);
        Assert.AreEqual("→", node.ValidationIcon);
    }

    [TestMethod]
    public void ValidationIcon_IsEmpty_WhenNoIssues()
    {
        var node = new TreeNode();

        Assert.IsFalse(node.HasValidationIssues);
        Assert.IsFalse(node.HasDescendantIssues);
        Assert.AreEqual(string.Empty, node.ValidationIcon);
    }

    [TestMethod]
    public void TypeColor_ReturnsMappedAndFallbackColors()
    {
        var epicNode = new TreeNode { Type = WorkItemTypeHelper.Epic };
        var unknownNode = new TreeNode { Type = "Unknown" };

        Assert.AreEqual("#FF9800", epicNode.TypeColor);
        Assert.AreEqual("#757575", unknownNode.TypeColor);
    }
}
